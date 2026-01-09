#!/usr/bin/env python3
"""
Unity Script Fixer with RAG
===========================
Fixes broken Unity C# code by:
1. Parsing compiler errors to identify problem areas
2. Using RAG to retrieve relevant Unity API documentation
3. Detecting hallucinated APIs via strict pattern matching
4. Providing focused context to LLM for targeted fixes
5. Multi-pass fixing with accumulated error feedback

Usage:
    python unity_script_fixer.py --interactive
    python unity_script_fixer.py --file broken_script.cs --errors "CS0117: 'Light' does not contain 'beamHeight'"
    python unity_script_fixer.py --file broken_script.cs  # Will analyze code for issues
"""

import json
import argparse
import requests
import re
from typing import Optional, Dict, List, Tuple, Set
from dataclasses import dataclass, field

from unity_rag_query import UnityRAG, RetrievedDoc, RetrievalResult
from unity_api_validator import UnityAPIValidator, ValidationIssue

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
import os
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "unity_rag_db")
DEFAULT_TEMPERATURE = 0.3  # Lower temp for more precise fixes
MAX_FIX_ITERATIONS = 5  # Increased for multi-pass fixing
VALIDATION_CONFIDENCE_THRESHOLD = 0.8  # Minimum confidence to report issue

# ============================================================================
# PROMPTS
# ============================================================================

FIXER_SYSTEM_PROMPT = """You are a Unity C# expert who fixes broken code. Your job is to correct API errors, syntax issues, and Unity-specific mistakes.

CRITICAL RULES:
1. Fix ONLY the specific errors mentioned - don't refactor unrelated code
2. Use ONLY APIs from the Unity documentation provided - NO invented APIs
3. NEVER introduce new syntax errors (check for typos, missing dots, spaces in variable names)
4. Preserve the original code structure, variable names, and intent
5. Keep all [SerializeField] and [Header] attributes exactly as they are
6. Use standard C# syntax - no nullable reference types (?) unless the original had them
7. Variable names must be valid C# identifiers (no spaces, must start with letter/underscore)
8. Method calls use PascalCase: Play(), not play()
9. Static methods: Mathf.Sin() - not MathF.Sin() or Mathf Sin
10. Double-check your output compiles before responding

PARTICLESYSTEM - COMMON MISTAKES TO AVOID:
- Module access is via PROPERTIES not methods: ps.emission NOT ps.GetEmission()
- ps.main, ps.emission, ps.velocityOverLifetime, ps.colorOverLifetime, etc.
- There is NO EmissionConfig, VelocityConfig, etc. - use MainModule, EmissionModule, etc.
- maxParticles is on MainModule: ps.main.maxParticles, NOT emission.maxParticles
- Use MinMaxCurve for values: main.startLifetime = new ParticleSystem.MinMaxCurve(3f)

LIGHT - COMMON MISTAKES TO AVOID:
- NO beamHeight, beamDirection, or target properties
- Use spotAngle for cone angle, transform.forward for direction
- Position is light.transform.position, NOT light.position

TRANSFORM - COMMON MISTAKES TO AVOID:
- NO .distance() method - use Vector3.Distance(a, b)
- NO .enabled property - use gameObject.SetActive(bool)
- NO .color property - use Renderer.material.color

CONSTRUCTORS - UNITY COMPONENTS CANNOT USE 'new':
- new AudioSource() is INVALID - use AddComponent<AudioSource>()
- new ParticleSystem() is INVALID - use AddComponent<ParticleSystem>()
- Same for: Light, Camera, Collider, Rigidbody, etc.

Output ONLY the corrected C# code. No markdown, no explanations."""

ERROR_FIX_PROMPT_TEMPLATE = """The following Unity C# script has compilation errors that need to be fixed.

COMPILER ERRORS:
{errors}

RELEVANT UNITY API DOCUMENTATION:
{rag_context}

BROKEN CODE:
```csharp
{code}
```

Fix each error using the Unity documentation above. For each error:
1. Find the correct API in the documentation
2. Replace the invalid code with the correct API usage

Output ONLY the fixed C# code:"""

ANALYSIS_FIX_PROMPT_TEMPLATE = """The following Unity C# script may have API issues. Review it against the Unity documentation.

DETECTED ISSUES:
{issues}

RELEVANT UNITY API DOCUMENTATION:
{rag_context}

CODE TO FIX:
```csharp
{code}
```

Fix the detected issues using the correct Unity APIs from the documentation.
Output ONLY the fixed C# code:"""

# ============================================================================
# ERROR PARSING
# ============================================================================

@dataclass
class ParsedError:
    """A parsed compiler error"""
    code: str           # e.g., "CS0117"
    message: str        # Full error message
    line: Optional[int] # Line number if available
    api_hint: Optional[str]  # Extracted API name
    type_hint: Optional[str]  # Type that's missing the member
    member_hint: Optional[str]  # Member that doesn't exist
    
    def __str__(self):
        loc = f"Line {self.line}: " if self.line else ""
        return f"{loc}[{self.code}] {self.message}"


def parse_compiler_errors(error_text: str) -> List[ParsedError]:
    """
    Parse compiler errors into structured format.
    
    Handles common Unity/C# error formats:
    - Assets\path\file.cs(line,col): error CS0117: 'Type' does not contain a definition for 'member'
    - CS1061: 'Type' does not contain a definition for 'member'
    - CS0246: The type or namespace name 'Type' could not be found
    - CS1501: No overload for method 'Method' takes N arguments
    - CS1003: Syntax error, ',' expected
    - CS0029: Cannot implicitly convert type 'T1' to 'T2'
    """
    errors = []
    
    # Split by newlines
    lines = error_text.split('\n')
    
    for line in lines:
        line = line.strip()
        if not line:
            continue
        
        error = ParsedError(
            code="unknown",
            message=line,
            line=None,
            api_hint=None,
            type_hint=None,
            member_hint=None
        )
        
        # Extract error code
        code_match = re.search(r'\b(CS\d{4})\b', line)
        if code_match:
            error.code = code_match.group(1)
        
        # Extract line number - Unity format: file.cs(line,col): error
        line_match = re.search(r'\.cs\((\d+),\d+\):', line)
        if line_match:
            error.line = int(line_match.group(1))
        else:
            # Fallback: any (number) or :number pattern
            line_match = re.search(r'[:\(](\d+)[,\)]', line)
            if line_match:
                error.line = int(line_match.group(1))
        
        # CS0117/CS1061: 'Type' does not contain a definition for 'member'
        member_match = re.search(r"'(\w+)' does not contain.*?'(\w+)'", line)
        if member_match:
            error.type_hint = member_match.group(1)
            error.member_hint = member_match.group(2)
            error.api_hint = f"{error.type_hint}.{error.member_hint}"
        
        # CS0246: Type 'X' could not be found
        type_match = re.search(r"type.*?'(\w+)'.*could not be found", line, re.IGNORECASE)
        if type_match:
            error.type_hint = type_match.group(1)
            error.api_hint = error.type_hint
        
        # CS1501: No overload for method 'X' takes N arguments
        overload_match = re.search(r"method '(\w+)' takes (\d+) arguments", line)
        if overload_match:
            error.member_hint = overload_match.group(1)
            error.api_hint = error.member_hint
        
        # General API pattern: Type.Member
        api_pattern = re.search(r'(\w+)\.(\w+)', line)
        if api_pattern and not error.api_hint:
            error.api_hint = f"{api_pattern.group(1)}.{api_pattern.group(2)}"
        
        errors.append(error)
    
    return errors


def extract_apis_from_errors(errors: List[ParsedError]) -> List[str]:
    """Extract API names from parsed errors for RAG lookup"""
    apis = []
    for err in errors:
        if err.api_hint and err.api_hint not in apis:
            apis.append(err.api_hint)
        if err.type_hint and err.type_hint not in apis:
            apis.append(err.type_hint)
    return apis


# ============================================================================
# SYNTAX VALIDATION
# ============================================================================

def validate_csharp_syntax(code: str) -> List[str]:
    """
    Quick validation of C# syntax to catch obvious LLM errors.
    Returns list of detected issues.
    """
    issues = []
    lines = code.split('\n')
    
    for line_num, line in enumerate(lines, 1):
        # Skip comments
        stripped = line.strip()
        if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
            continue
        
        # Check for spaces in identifiers (common LLM error)
        # e.g., "_pitch TransitionCoroutine" instead of "_pitchTransitionCoroutine"
        space_in_id = re.search(r'_\w+\s+\w+(?=\s*[=;,\)])', line)
        if space_in_id:
            issues.append(f"Line {line_num}: Space in identifier: '{space_in_id.group()}' - remove the space")
        
        # Check for double dots (e.g., "object..Method")
        if '..' in line and '...' not in line:  # Allow ... for params
            issues.append(f"Line {line_num}: Double dot '..' syntax error - use single dot")
        
        # Check for lowercase Unity methods (common: .play() instead of .Play())
        for match in re.finditer(r'\.([a-z][a-zA-Z]*)\s*\(', line):
            method = match.group(1)
            # Unity methods should be PascalCase
            if method in ['play', 'stop', 'pause', 'emit', 'clear', 'simulate', 'setActive']:
                correct = method[0].upper() + method[1:]
                issues.append(f"Line {line_num}: Use .{correct}() not .{method}()")
        
        # Check for MathF instead of Mathf (System.MathF vs UnityEngine.Mathf)
        if re.search(r'\bMathF[.\s]', line):
            issues.append(f"Line {line_num}: Use 'Mathf' not 'MathF' (Unity uses UnityEngine.Mathf)")
        
        # Check for "Mathf Sin" without the dot (space instead of dot)
        if re.search(r'\bMathf\s+[A-Z]', line):
            issues.append(f"Line {line_num}: Missing dot - should be 'Mathf.Method' not 'Mathf Method'")
        
        # Check for nullable reference types (AudioSource?) - not standard in Unity
        if re.search(r'\b[A-Z]\w+\?\s+\w+', line) and 'Nullable<' not in line and '?' not in stripped.split('//')[0].split('=')[0]:
            if re.search(r'\b(AudioSource|GameObject|Transform|Rigidbody|Collider)\?\s+\w+', line):
                issues.append(f"Line {line_num}: Nullable reference type syntax (Type?) doesn't work in Unity - remove the ?")
        
        # Check for Transform.enabled (Transform doesn't have .enabled)
        if re.search(r'\btransform\.enabled\b', line, re.IGNORECASE):
            issues.append(f"Line {line_num}: Transform doesn't have .enabled - use transform.gameObject.SetActive()")
        
        # Check for incorrect component constructors
        for comp in ['AudioSource', 'Rigidbody', 'Collider', 'ParticleSystem', 'Light', 'Camera']:
            if re.search(rf'new\s+{comp}\s*\(', line):
                issues.append(f"Line {line_num}: Can't use 'new {comp}()' - use AddComponent<{comp}>()")
        
        # Check for string array literal with string elements (hallucination pattern)
        if re.search(r'new\s+\w+\[\]\s*\{\s*"', line):
            if 'AudioClip[]' in line or 'string[]' not in line.lower():
                issues.append(f"Line {line_num}: Can't create AudioClip from string literal")
    
    # Check for referenced but possibly undefined variables
    declared_fields = set(re.findall(r'\[(?:SerializeField|Header)[^\]]*\]\s*(?:private|public)?\s*\w+\s+(\w+)\s*[=;]', code))
    declared_fields.update(re.findall(r'(?:private|public|protected)\s+\w+\s+(\w+)\s*[=;]', code))
    
    # Check for duplicate variable declarations
    issues.extend(_check_duplicate_declarations(code, lines))
    
    # Look for obvious typos in field references
    used_patterns = re.findall(r'\b(_\w+)\b', code)
    used_counts = {}
    for var in used_patterns:
        used_counts[var] = used_counts.get(var, 0) + 1
    
    # If a variable is used once and looks like a typo of a declared var, flag it
    for used_var, count in used_counts.items():
        if count == 1 and used_var not in declared_fields:
            # Check if it's similar to a declared field
            for declared in declared_fields:
                if len(used_var) > 3 and len(declared) > 3:
                    # Simple similarity: same prefix, different suffix
                    if used_var[:5] == declared[:5] and used_var != declared:
                        issues.append(f"Possible typo: '{used_var}' - did you mean '{declared}'?")
                        break
    
    return issues


def _check_duplicate_declarations(code: str, lines: List[str]) -> List[str]:
    """
    Check for duplicate variable declarations in C# code.
    Returns list of issues found.
    """
    issues = []
    
    # Track field declarations (class-level variables)
    field_declarations = {}  # var_name -> list of (line_num, full_line)
    
    # Track local variable declarations per method scope
    # This is simplified - we'll track all local vars and check for duplicates
    local_declarations = {}  # var_name -> list of (line_num, full_line)
    
    # Track method parameters
    method_params = {}  # method_name -> list of param_names
    
    for line_num, line in enumerate(lines, 1):
        stripped = line.strip()
        
        # Skip comments
        if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
            continue
        
        # Check for field declarations (class-level)
        # Pattern: [attributes] access_modifier type var_name [= value];
        field_patterns = [
            r'\[(?:SerializeField|Header)[^\]]*\]\s*(?:private|public|protected|internal)?\s+\w+\s+(\w+)\s*[=;]',
            r'(?:private|public|protected|internal)\s+\w+\s+(\w+)\s*[=;]',
        ]
        
        for pattern in field_patterns:
            matches = re.finditer(pattern, line)
            for match in matches:
                var_name = match.group(1)
                if var_name not in field_declarations:
                    field_declarations[var_name] = []
                field_declarations[var_name].append((line_num, stripped))
        
        # Check for local variable declarations
        # Pattern: type var_name [= value];
        # But exclude field declarations and method parameters
        if not re.search(r'^\s*(?:private|public|protected|internal|static|const)', stripped):
            # Local variable: type name = ... or type name;
            local_pattern = r'\b([A-Z]\w+)\s+(\w+)\s*[=;]'
            # Exclude common patterns that aren't variable declarations
            if not re.search(r'\b(if|for|while|foreach|switch|return|new|using|namespace|class|interface|enum)\s+', stripped):
                matches = re.finditer(local_pattern, line)
                for match in matches:
                    var_type = match.group(1)
                    var_name = match.group(2)
                    # Skip if it looks like a method call or property access
                    if var_type not in ['void', 'int', 'float', 'bool', 'string', 'Vector3', 'Vector2', 
                                       'GameObject', 'Transform', 'Rigidbody', 'AudioSource', 'Light',
                                       'Camera', 'Collider', 'ParticleSystem', 'Animator', 'Material',
                                       'Sprite', 'Color', 'Quaternion', 'AudioClip']:
                        # Might be a method call or property, skip
                        continue
                    if var_name not in local_declarations:
                        local_declarations[var_name] = []
                    local_declarations[var_name].append((line_num, stripped))
        
        # Check for method parameters (simplified - just check for duplicate params in same signature)
        method_match = re.search(r'\b(?:private|public|protected|internal)?\s*(?:static)?\s*\w+\s+(\w+)\s*\(([^)]*)\)', line)
        if method_match:
            method_name = method_match.group(1)
            params_str = method_match.group(2)
            # Extract parameter names
            param_names = []
            for param in params_str.split(','):
                param = param.strip()
                if param:
                    # Extract name (last word before = or end)
                    param_match = re.search(r'(\w+)(?:\s*=\s*[^,]+)?$', param)
                    if param_match:
                        param_names.append(param_match.group(1))
            # Check for duplicate parameter names in same method
            seen_params = set()
            for param_name in param_names:
                if param_name in seen_params:
                    issues.append(f"Line {line_num}: Duplicate parameter '{param_name}' in method '{method_name}'")
                seen_params.add(param_name)
    
    # Check for duplicate field declarations
    for var_name, declarations in field_declarations.items():
        if len(declarations) > 1:
            line_nums = [d[0] for d in declarations]
            issues.append(f"Duplicate field declaration '{var_name}' at lines {', '.join(map(str, line_nums))}")
    
    # Check for duplicate local variable declarations (within same scope - simplified check)
    # Note: This is a simplified check - full scope analysis would require parsing
    for var_name, declarations in local_declarations.items():
        if len(declarations) > 1:
            # Check if they're close together (likely same scope)
            line_nums = sorted([d[0] for d in declarations])
            # If declarations are within 50 lines, likely duplicate
            if line_nums[-1] - line_nums[0] < 50:
                issues.append(f"Possible duplicate local variable '{var_name}' at lines {', '.join(map(str, line_nums))}")
    
    return issues


# ============================================================================
# CODE ANALYSIS
# ============================================================================

@dataclass
class CodeIssue:
    """A detected issue in the code"""
    api: str
    issue_type: str  # "invalid_api", "wrong_signature", "missing_import", etc.
    line: Optional[int]
    suggestion: Optional[str]
    confidence: float
    
    def __str__(self):
        loc = f"Line {self.line}: " if self.line else ""
        sug = f" -> Try: {self.suggestion}" if self.suggestion else ""
        return f"{loc}{self.api} ({self.issue_type}){sug}"


def analyze_code_issues(code: str, rag: UnityRAG) -> List[CodeIssue]:
    """
    Analyze code for potential Unity API issues without compiler errors.
    Uses RAG to validate APIs.
    """
    issues = []
    lines = code.split('\n')
    
    # Extract all potential API calls
    api_patterns = [
        # Static calls: Class.Method
        (r'\b([A-Z]\w+)\.([a-zA-Z]\w+)\s*[\(\<]', 'static_call'),
        # Property access that might be wrong
        (r'\b(\w+)\.([a-zA-Z]\w+)\b(?!\s*[\(\<])', 'property'),
        # Constructor: new Type(
        (r'new\s+([A-Z]\w+)\s*\(', 'constructor'),
        # GetComponent<Type>
        (r'GetComponent\s*<\s*(\w+)\s*>', 'component'),
    ]
    
    # Unity types we should validate
    unity_types = {
        'AudioSource', 'AudioClip', 'Rigidbody', 'Rigidbody2D', 'Transform',
        'GameObject', 'Light', 'Camera', 'Collider', 'Collider2D',
        'ParticleSystem', 'Animator', 'Animation', 'Material', 'Renderer',
        'MeshRenderer', 'SpriteRenderer', 'Canvas', 'RectTransform',
        'Physics', 'Physics2D', 'Mathf', 'Vector3', 'Vector2', 'Quaternion',
        'Color', 'Time', 'Input', 'Random', 'Debug', 'Application',
        'Object', 'Resources', 'SceneManager', 'NavMeshAgent', 'CharacterController',
        'LineRenderer', 'TrailRenderer', 'Gizmos'
    }
    
    for line_num, line in enumerate(lines, 1):
        # Skip comments
        if line.strip().startswith('//'):
            continue
        
        for pattern, call_type in api_patterns:
            for match in re.finditer(pattern, line):
                if call_type == 'static_call':
                    type_name = match.group(1)
                    member = match.group(2)
                    api = f"{type_name}.{member}"
                    
                    # Only validate Unity types
                    if type_name not in unity_types:
                        continue
                    
                    # Check if API is valid
                    is_valid, suggestions = rag.validate_api(api, threshold=0.7)
                    
                    if not is_valid:
                        best_suggestion = suggestions[0][0] if suggestions else None
                        confidence = suggestions[0][1] if suggestions else 0.0
                        
                        issues.append(CodeIssue(
                            api=api,
                            issue_type="invalid_api",
                            line=line_num,
                            suggestion=best_suggestion,
                            confidence=confidence
                        ))
                
                elif call_type == 'property':
                    # Check for common wrong property patterns
                    type_name = match.group(1)
                    prop = match.group(2)
                    
                    # Variable name -> likely type mapping
                    var_type_map = {
                        'rb': 'Rigidbody', 'rigidbody': 'Rigidbody',
                        'audioSource': 'AudioSource', 'audio': 'AudioSource',
                        'light': 'Light', 'cam': 'Camera', 'camera': 'Camera',
                        'ps': 'ParticleSystem', 'particles': 'ParticleSystem',
                        'transform': 'Transform', 'tr': 'Transform',
                        'collider': 'Collider', 'col': 'Collider',
                        'animator': 'Animator', 'anim': 'Animator',
                    }
                    
                    inferred_type = var_type_map.get(type_name.lower())
                    if inferred_type:
                        api = f"{inferred_type}.{prop}"
                        is_valid, suggestions = rag.validate_api(api, threshold=0.7)
                        
                        if not is_valid:
                            best_suggestion = suggestions[0][0] if suggestions else None
                            confidence = suggestions[0][1] if suggestions else 0.0
                            
                            issues.append(CodeIssue(
                                api=api,
                                issue_type="invalid_property",
                                line=line_num,
                                suggestion=best_suggestion,
                                confidence=confidence
                            ))
                
                elif call_type == 'constructor':
                    type_name = match.group(1)
                    
                    # Unity components can't be constructed with new
                    if type_name in {'AudioSource', 'Rigidbody', 'Collider', 'Light', 
                                     'Camera', 'ParticleSystem', 'Animator', 'Canvas',
                                     'MeshRenderer', 'SpriteRenderer'}:
                        issues.append(CodeIssue(
                            api=f"new {type_name}()",
                            issue_type="invalid_constructor",
                            line=line_num,
                            suggestion=f"Use AddComponent<{type_name}>() or assign in Inspector",
                            confidence=1.0
                        ))
    
    return issues


# ============================================================================
# FIX RESULT
# ============================================================================

@dataclass
class FixResult:
    """Result of a fix operation"""
    success: bool
    original_code: str
    fixed_code: Optional[str]
    errors_fixed: List[str]
    issues_detected: List[str]
    rag_docs_used: int
    iterations: int
    remaining_issues: List[str] = field(default_factory=list)
    error: Optional[str] = None
    
    def __str__(self):
        if not self.success:
            return f"FixResult(failed: {self.error})"
        return (f"FixResult(success, fixed {len(self.errors_fixed)} errors, "
                f"used {self.rag_docs_used} docs, {self.iterations} iterations)")


# ============================================================================
# SCRIPT FIXER CLASS
# ============================================================================

class UnityScriptFixer:
    """
    RAG-powered Unity C# script fixer with strict validation.
    
    Takes broken code + optional compiler errors, validates against known
    hallucination patterns, uses RAG to get relevant Unity documentation,
    and calls LLM to fix the issues with multi-pass error accumulation.
    """
    
    def __init__(self, verbose: bool = False):
        self.verbose = verbose
        self.llm_url = LLM_URL
        
        if verbose:
            print("Initializing Unity Script Fixer...")
        
        # Load RAG system
        self.rag = UnityRAG(db_path=RAG_DB_PATH, verbose=verbose)
        
        # Initialize strict validator
        self.validator = UnityAPIValidator(verbose=verbose)
        
        if verbose:
            print(f"Fixer ready! RAG: {len(self.rag.documents)} docs")
            print(f"Validator: {len(self.validator.hallucination_patterns)} hallucination patterns\n")
    
    def fix(self, 
            code: str, 
            errors: Optional[str] = None,
            max_iterations: int = MAX_FIX_ITERATIONS) -> FixResult:
        """
        Fix broken Unity C# code using multi-pass validation and fixing.
        
        Args:
            code: The broken C# code
            errors: Optional compiler error messages
            max_iterations: Max LLM calls for iterative fixing
            
        Returns:
            FixResult with fixed code and metadata
        """
        result = FixResult(
            success=False,
            original_code=code,
            fixed_code=None,
            errors_fixed=[],
            issues_detected=[],
            rag_docs_used=0,
            iterations=0
        )
        
        # ================================================================
        # PHASE 1: Collect all issues from multiple sources
        # ================================================================
        all_issues_text = []
        apis_to_lookup = []
        
        # Source 1: Compiler errors (if provided)
        parsed_errors = []
        if errors:
            parsed_errors = parse_compiler_errors(errors)
            result.errors_fixed = [str(e) for e in parsed_errors]
            apis_to_lookup.extend(extract_apis_from_errors(parsed_errors))
            all_issues_text.extend([f"[COMPILER] {str(e)}" for e in parsed_errors])
            
            if self.verbose:
                print(f"Parsed {len(parsed_errors)} compiler errors:")
                for e in parsed_errors[:5]:
                    print(f"  - {e}")
        
        # Source 2: Strict validator (catches hallucinations)
        validation_issues = self.validator.validate_code(code)
        high_confidence_issues = [i for i in validation_issues 
                                   if i.confidence >= VALIDATION_CONFIDENCE_THRESHOLD]
        
        if high_confidence_issues:
            result.issues_detected.extend([str(i) for i in high_confidence_issues])
            all_issues_text.extend([f"[VALIDATION] {str(i)}" for i in high_confidence_issues])
            
            if self.verbose:
                print(f"\nStrict validation found {len(high_confidence_issues)} issues:")
                for i in high_confidence_issues[:8]:
                    print(f"  [!] {i}")
        
        # Source 3: RAG-based semantic analysis
        code_issues = analyze_code_issues(code, self.rag)
        result.issues_detected.extend([str(i) for i in code_issues])
        all_issues_text.extend([f"[SEMANTIC] {str(i)}" for i in code_issues])
        
        if self.verbose and code_issues:
            print(f"\nSemantic analysis found {len(code_issues)} issues:")
            for i in code_issues[:5]:
                print(f"  - {i}")
        
        # Source 4: Syntax validation
        syntax_issues = validate_csharp_syntax(code)
        all_issues_text.extend([f"[SYNTAX] {s}" for s in syntax_issues])
        
        if self.verbose and syntax_issues:
            print(f"\nSyntax validation found {len(syntax_issues)} issues:")
            for s in syntax_issues[:5]:
                print(f"  - {s}")
        
        # Collect APIs for RAG lookup
        for issue in code_issues:
            if issue.api not in apis_to_lookup:
                apis_to_lookup.append(issue.api)
            if issue.suggestion and issue.suggestion not in apis_to_lookup:
                apis_to_lookup.append(issue.suggestion)
        
        # Add APIs from validation suggestions
        for vi in high_confidence_issues:
            # Extract type.method patterns from the fix suggestion
            suggested_apis = re.findall(r'([A-Z]\w+(?:\.[a-zA-Z]\w+)+)', vi.suggested_fix)
            for api in suggested_apis:
                if api not in apis_to_lookup:
                    apis_to_lookup.append(api)
        
        # Extract additional APIs from code itself
        code_apis = self.rag.extract_apis_from_code(code)
        for api in code_apis:
            if api not in apis_to_lookup:
                apis_to_lookup.append(api)
        
        if self.verbose:
            print(f"\nAPIs to look up: {apis_to_lookup[:12]}...")
        
        # ================================================================
        # PHASE 2: RAG retrieval for documentation
        # ================================================================
        rag_docs = self._get_targeted_docs(apis_to_lookup, code)
        result.rag_docs_used = len(rag_docs)
        
        if self.verbose:
            print(f"Retrieved {len(rag_docs)} RAG docs")
        
        rag_context = self.rag.format_context_for_prompt(
            rag_docs, 
            max_tokens=4000,
            include_content=True
        )
        
        # ================================================================
        # PHASE 3: Build comprehensive fix prompt
        # ================================================================
        
        # Include validator-specific fix context
        validator_context = self.validator.get_fix_context(high_confidence_issues)
        
        if all_issues_text:
            combined_issues = "\n".join(all_issues_text[:25])  # Limit to prevent context overflow
            prompt = f"""The following Unity C# script has multiple issues that need to be fixed.

DETECTED ISSUES (must fix ALL):
{combined_issues}

{validator_context}

UNITY API DOCUMENTATION:
{rag_context}

BROKEN CODE:
```csharp
{code}
```

Fix EVERY issue listed above. Use the Unity documentation for correct API usage.
Output ONLY the fixed C# code:"""
        else:
            # No specific issues - use generic analysis prompt
            prompt = ANALYSIS_FIX_PROMPT_TEMPLATE.format(
                issues="Review code for potential Unity API issues",
                rag_context=rag_context,
                code=code
            )
        
        # ================================================================
        # PHASE 4: Multi-pass fixing
        # ================================================================
        
        current_code = code
        accumulated_feedback = []  # Track issues across iterations
        
        for iteration in range(max_iterations):
            if self.verbose:
                print(f"\n--- Fix iteration {iteration + 1} ---")
            
            # Call LLM
            fixed_code = self._call_llm(prompt if iteration == 0 else retry_prompt)
            result.iterations += 1
            
            if not fixed_code:
                if iteration == 0:
                    result.error = "LLM failed to generate fixed code"
                    return result
                else:
                    break  # Use last successful fix
            
            current_code = fixed_code
            
            # Re-validate the fixed code
            new_validation = self.validator.validate_code(current_code)
            new_high_conf = [i for i in new_validation 
                           if i.confidence >= VALIDATION_CONFIDENCE_THRESHOLD]
            
            new_syntax = validate_csharp_syntax(current_code)
            new_semantic = analyze_code_issues(current_code, self.rag)
            
            total_remaining = len(new_high_conf) + len(new_syntax) + len(new_semantic)
            
            if self.verbose:
                print(f"  Remaining: {len(new_high_conf)} validation, {len(new_syntax)} syntax, {len(new_semantic)} semantic")
            
            # If no issues remain, we're done
            if total_remaining == 0:
                if self.verbose:
                    print("  All issues resolved!")
                break
            
            # Check if we made progress
            prev_total = len(all_issues_text)
            if total_remaining >= prev_total and iteration > 0:
                # Not making progress - might be oscillating
                if self.verbose:
                    print(f"  No progress (was {prev_total}, now {total_remaining}), stopping")
                break
            
            # Build retry prompt with remaining issues
            remaining_issues = []
            remaining_issues.extend([f"[VALIDATION] {str(i)}" for i in new_high_conf])
            remaining_issues.extend([f"[SYNTAX] {s}" for s in new_syntax])
            remaining_issues.extend([f"[SEMANTIC] {str(i)}" for i in new_semantic])
            
            # Add to accumulated feedback
            for issue in remaining_issues[:5]:
                if issue not in accumulated_feedback:
                    accumulated_feedback.append(issue)
            
            # Get more targeted docs for remaining issues
            remaining_apis = [issue.api for issue in new_semantic]
            remaining_apis.extend([issue.invalid_api for issue in new_high_conf])
            new_docs = self._get_targeted_docs(remaining_apis, current_code)
            new_context = self.rag.format_context_for_prompt(new_docs, max_tokens=3000, include_content=True)
            
            validator_context = self.validator.get_fix_context(new_high_conf)
            
            retry_prompt = f"""Your previous fix still has issues. Fix these REMAINING problems:

{chr(10).join(remaining_issues[:15])}

{validator_context}

ACCUMULATED FEEDBACK (issues that keep appearing):
{chr(10).join(accumulated_feedback[:10])}

UNITY DOCUMENTATION:
{new_context}

CODE TO FIX:
```csharp
{current_code}
```

Output ONLY the corrected C# code:"""
        
        # ================================================================
        # PHASE 5: Final validation and result
        # ================================================================
        
        final_validation = self.validator.validate_code(current_code)
        final_syntax = validate_csharp_syntax(current_code)
        final_semantic = analyze_code_issues(current_code, self.rag)
        
        result.remaining_issues = []
        result.remaining_issues.extend([str(i) for i in final_validation if i.confidence >= VALIDATION_CONFIDENCE_THRESHOLD])
        result.remaining_issues.extend(final_syntax)
        result.remaining_issues.extend([str(i) for i in final_semantic])
        
        result.fixed_code = current_code
        result.success = True
        
        if self.verbose:
            if result.remaining_issues:
                print(f"\nFix complete with {len(result.remaining_issues)} remaining issues")
            else:
                print("\nFix complete - all issues resolved!")
        
        return result
    
    def _get_targeted_docs(self, apis: List[str], code: str) -> List[RetrievedDoc]:
        """Get RAG docs targeted at specific APIs"""
        all_docs = []
        seen_ids = set()
        
        # Direct search for each API
        for api in apis[:15]:  # Limit to prevent too many searches
            results = self.rag.search(
                query=api,
                threshold=0.5,
                top_k=2
            )
            for doc in results:
                if doc.id not in seen_ids:
                    seen_ids.add(doc.id)
                    all_docs.append(doc)
        
        # Also get general steering docs for the code
        steering_result = self.rag.retrieve_for_code_steering(code, threshold=0.5, top_k=5)
        for doc in steering_result.documents:
            if doc.id not in seen_ids:
                seen_ids.add(doc.id)
                all_docs.append(doc)
        
        # Load content for all docs
        for doc in all_docs:
            if doc.content is None:
                doc.content = self.rag.get_doc_content(doc)
        
        # Sort by score and limit
        all_docs.sort(key=lambda x: x.score, reverse=True)
        return all_docs[:20]
    
    def _call_llm(self, prompt: str) -> Optional[str]:
        """Call LLM with the fix prompt"""
        try:
            if self.verbose:
                print(f"Calling LLM ({len(prompt)} chars)...")
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": FIXER_SYSTEM_PROMPT},
                        {"role": "user", "content": prompt}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                if self.verbose:
                    print(f"LLM error: {response.status_code}")
                    try:
                        print(f"  Detail: {response.json()}")
                    except:
                        pass
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"LLM call failed: {e}")
            return None
    
    def _clean_code(self, code: str) -> str:
        """Clean C# code from markdown formatting"""
        code = code.strip()
        if code.startswith("```"):
            lines = code.split("\n")
            if lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip() == "```":
                lines = lines[:-1]
            code = "\n".join(lines)
        return code.strip()


# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(fixer: UnityScriptFixer):
    """Interactive script fixing session"""
    print("=" * 60)
    print("UNITY SCRIPT FIXER - Interactive Mode")
    print("=" * 60)
    print("USAGE:")
    print("  1. Paste your broken C# code")
    print("  2. Type 'end'")
    print("  3. Paste Unity errors (optional, auto-detected)")
    print("  4. Type 'done' (or press Enter twice after errors)")
    print("")
    print("Commands: quit, verbose, quiet")
    print("=" * 60)
    
    while True:
        print("\nPaste code (type END to finish, or quit to exit):")
        
        code_lines = []
        errors_lines = []
        in_errors = False
        
        seen_end = False
        empty_after_errors = 0
        
        while True:
            try:
                line = input()
            except (EOFError, KeyboardInterrupt):
                print("\nExiting.")
                return
            
            if line.strip().lower() == "quit":
                return
            
            if line.strip().lower() == "verbose":
                fixer.verbose = True
                print("Verbose mode ON")
                break
            
            if line.strip().lower() == "quiet":
                fixer.verbose = False
                print("Verbose mode OFF")
                break
            
            if line.strip().lower() in ("end", "done"):
                if seen_end or line.strip().lower() == "done":
                    # Second end/done or explicit done - process now
                    break
                else:
                    seen_end = True
                    print("(Type 'done' to process, or paste Unity errors below)")
                    continue
            
            if line.strip().upper() == "ERRORS:" or line.strip().lower() == "errors:":
                in_errors = True
                continue
            
            # Auto-detect Unity error format (Assets\...: error CS...)
            if re.match(r'^Assets[\\\/].*:\s*error\s+CS\d+', line):
                in_errors = True
            
            # If we're in error mode and get an empty line, maybe we're done
            if in_errors and not line.strip():
                empty_after_errors += 1
                if empty_after_errors >= 2:
                    break  # Two empty lines after errors = done
                continue
            
            if in_errors:
                errors_lines.append(line)
                empty_after_errors = 0
            else:
                code_lines.append(line)
        
        code = "\n".join(code_lines)
        errors = "\n".join(errors_lines) if errors_lines else None
        
        if not code.strip():
            continue
        
        print("\nFixing code...")
        result = fixer.fix(code, errors)
        
        print(f"\n{result}")
        
        if result.success and result.fixed_code:
            print("\n" + "-" * 40)
            print("FIXED CODE:")
            print("-" * 40)
            print(result.fixed_code)
            
            if result.remaining_issues:
                print("\n" + "-" * 40)
                print(f"REMAINING ISSUES ({len(result.remaining_issues)}):")
                print("-" * 40)
                for issue in result.remaining_issues[:5]:
                    print(f"  - {issue}")
        else:
            print(f"\nError: {result.error}")


# ============================================================================
# FILE MODE
# ============================================================================

def fix_file(fixer: UnityScriptFixer, file_path: str, errors: Optional[str], output_path: Optional[str]):
    """Fix a C# file"""
    # Read input file
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            code = f.read()
    except Exception as e:
        print(f"Error reading file: {e}")
        return
    
    print(f"Fixing: {file_path}")
    print(f"Code: {len(code)} chars")
    if errors:
        print(f"Errors: {errors[:100]}...")
    
    result = fixer.fix(code, errors)
    
    print(f"\n{result}")
    
    if result.success and result.fixed_code:
        # Write output
        out_path = output_path or file_path.replace('.cs', '_fixed.cs')
        try:
            with open(out_path, 'w', encoding='utf-8') as f:
                f.write(result.fixed_code)
            print(f"\nFixed code written to: {out_path}")
        except Exception as e:
            print(f"Error writing output: {e}")
            print("\n" + "-" * 40)
            print("FIXED CODE:")
            print("-" * 40)
            print(result.fixed_code)
        
        if result.remaining_issues:
            print(f"\nWarning: {len(result.remaining_issues)} issues may remain")
    else:
        print(f"\nFailed: {result.error}")


# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Unity Script Fixer: Fix broken C# code with RAG-powered documentation lookup"
    )
    parser.add_argument("-i", "--interactive", action="store_true",
                        help="Interactive mode - paste code directly")
    parser.add_argument("-f", "--file", type=str,
                        help="Path to C# file to fix")
    parser.add_argument("-e", "--errors", type=str,
                        help="Compiler error messages")
    parser.add_argument("-o", "--output", type=str,
                        help="Output file path (default: input_fixed.cs)")
    parser.add_argument("-v", "--verbose", action="store_true",
                        help="Verbose output")
    parser.add_argument("--max-iterations", type=int, default=MAX_FIX_ITERATIONS,
                        help=f"Max fix iterations (default: {MAX_FIX_ITERATIONS})")
    
    args = parser.parse_args()
    
    fixer = UnityScriptFixer(verbose=args.verbose)
    
    if args.file:
        fix_file(fixer, args.file, args.errors, args.output)
    elif args.interactive or not args.file:
        interactive_mode(fixer)
    else:
        parser.print_help()


if __name__ == "__main__":
    main()

