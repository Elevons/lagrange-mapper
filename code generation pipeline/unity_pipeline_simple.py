"""
Unity Pipeline Simple - Streamlined NL -> IR -> C# Generation

Simplified flow:
1. User prompt -> IR JSON
2. IR JSON -> RAG retrieval  
3. RAG context + IR -> C# code generation
4. RAG-based steering (if needed)
5. Output code

No attractor detection, no calibration, just clean generation with RAG support.
"""

import json
import re
import requests
import sys
from typing import Dict, List, Optional
from dataclasses import dataclass
from unity_ir_inference import extract_json_from_response

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
import os
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "unity_rag_db")
DEFAULT_TEMPERATURE = 0.4

# ============================================================================
# PROMPTS
# ============================================================================

IR_SYSTEM_PROMPT = """You are a helpful assistant. Output only valid JSON."""

CODE_SYSTEM_PROMPT = """You are a Unity Script Assistant generating scaffolding code.
Your goal is to create 80% correct, well-structured code that developers can quickly customize.

ALWAYS include TODO comments for:
- Complex physics interactions
- Performance-critical sections
- Component dependencies that might not exist
- Edge cases or assumptions made

Example output quality:
✓ Correct basic structure
✓ Proper lifecycle methods
✓ Safe null checks
✓ Clear TODO markers
✗ Perfect physics (let humans tune)
✗ Optimized algorithms (let humans optimize)
✗ Complete error handling (let humans harden)

Convert the behavior specification into a complete MonoBehaviour script.

REQUIRED:
- Compilable C# (no syntax errors)
- Correct Unity lifecycle
- Safe defaults and null checks
- TODO comments for complex sections

ACCEPTABLE GAPS:
- Physics tuning needed
- Performance not optimized
- Edge cases not fully handled
- Placeholder algorithms

CRITICAL RULES - FOLLOW EXACTLY:

1. CLASS NAME: Use EXACTLY the "class_name" from IR - no changes, no underscores, no prefixes
2. FIELDS: Declare EXACTLY the fields from "fields" array:
   - Use the EXACT field name from IR (e.g., if IR says "chaseTarget", use "chaseTarget" not "_chaseTarget" or "target")
   - Use the EXACT type from IR (e.g., if IR says "GameObject", use GameObject not Transform)
   - Use the EXACT default value from IR
   - Declare as [SerializeField] private
   - DO NOT add extra fields not in the IR
3. COMPONENTS: For each component in "components":
   - Add GetComponent<T>() call in Start() using the EXACT component name
   - Store in a private field (e.g., Rigidbody _rigidbody)
   - If IR says "Rigidbody", use Rigidbody NOT Rigidbody2D
4. BEHAVIORS: Implement EXACTLY each behavior from "behaviors":
   - Use the behavior "name" as a method name or comment
   - Map "trigger" to Unity method:
     * "enters detection radius/range" → OnTriggerEnter
     * "exits detection radius/range" → OnTriggerExit  
     * "when X exists and Y" → Update() with condition
     * "when reaching X" → Update() with condition check
     * "when timer expires" → Update() with timer check
   - Implement ALL actions from the behavior's "actions" array
   - DO NOT skip behaviors or add extra behaviors

FIELD NAMING RULES:
- If IR field is "chaseTarget", use "chaseTarget" (not "_chaseTarget", not "target", not "_target")
- If IR field is "detectionRadius", use "detectionRadius" (not "_detectionRadius", not "detectionRange")
- Preserve exact spelling and casing from IR

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, OnTriggerExit, etc.)
2. Use correct Unity APIs from the documentation provided
3. Add required using statements (UnityEngine, System.Collections, etc.)
4. Make the code compilable (no syntax errors)
5. SELF-CONTAINED: Do NOT reference classes that aren't defined in this script or Unity's standard API
6. DO NOT add extra fields, methods, or behaviors not in the IR specification
7. Use the exact component types from IR (Rigidbody vs Rigidbody2D, Collider vs Collider2D)
8. Add TODO comments for complex physics, performance-critical sections, and edge cases

ACTION MAPPING:
- "set target to X" → chaseTarget = X (using exact field name from IR)
- "move toward X" → transform.position += direction * speed * Time.deltaTime
  // TODO: Consider NavMeshAgent for pathfinding or obstacle avoidance
- "rotate to face X" → transform.rotation = Quaternion.LookRotation(direction)
  // TODO: Add smooth rotation with Quaternion.Slerp for better visual quality
- "play X animation" → animator.SetTrigger("X")
- "deal damage to X" → X.SendMessage("TakeDamage", damage)
  // TODO: Replace SendMessage with direct component reference for better performance
- "set cooldown timer" → lastAttackTime = Time.time

STRUCTURE:
1. using statements
2. public class [EXACT_CLASS_NAME] : MonoBehaviour
3. [SerializeField] private fields (EXACT names from IR)
4. private component references (Rigidbody, Collider, etc.)
5. Start() - GetComponent calls
6. Update() - if needed for continuous checks
7. Unity event methods (OnTriggerEnter, etc.) for trigger-based behaviors
8. Behavior implementation methods
9. Helper methods if needed

Output ONLY the C# code. No markdown, no explanations."""

STEERING_PROMPT_TEMPLATE = """The following C# code has INVALID Unity APIs that need to be fixed.

INVALID APIS DETECTED (must be fixed):
{invalid_apis}

UNITY DOCUMENTATION FOR REFERENCE:
{rag_context}

ORIGINAL SPECIFICATION:
{ir_json}

CODE TO FIX:
```csharp
{code}
```

Fix EACH invalid API listed above. Use the suggested alternatives or find the correct Unity API.
Output ONLY the corrected C# code, no explanations:"""

ONESHOT_SYSTEM_PROMPT = """You are a Unity C# code generator. Generate a complete MonoBehaviour script from the description.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, etc.)
2. Use correct Unity APIs
3. Declare all fields as public or [SerializeField]
4. Add required using statements
5. Make the code production-ready and compilable

Output ONLY the C# code. No markdown, no explanations."""

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class PipelineResult:
    """Result of the pipeline"""
    success: bool
    ir_json: Optional[Dict] = None
    code: Optional[str] = None
    error: Optional[str] = None
    was_steered: bool = False
    rag_docs_used: int = 0
    rag_doc_names: Optional[List[str]] = None  # List of "APIName (score)" strings


@dataclass
class CompareResult:
    """Result of oneshot vs IR comparison"""
    prompt: str
    oneshot_code: Optional[str] = None
    ir_json: Optional[Dict] = None
    ir_code: Optional[str] = None
    ir_steered: bool = False
    ir_rag_docs: int = 0
    ir_rag_doc_names: Optional[List[str]] = None  # List of "APIName (score)" strings


# ============================================================================
# SIMPLE PIPELINE CLASS
# ============================================================================

class UnityPipelineSimple:
    """
    Simplified Unity code generation pipeline.
    
    Flow: Prompt -> IR JSON -> RAG -> C# Code -> (optional steering) -> Output
    """
    
    def __init__(self, 
                 llm_url: str = LLM_URL,
                 rag_db_path: str = RAG_DB_PATH,
                 verbose: bool = False):
        self.llm_url = llm_url
        self.verbose = verbose
        self.rag = None
        
        # Load RAG system
        try:
            from unity_rag_query import UnityRAG
            self.rag = UnityRAG(db_path=rag_db_path, verbose=verbose)
            if verbose:
                print(f"RAG loaded: {len(self.rag.documents)} docs, content={self.rag.has_embedded_content}")
        except Exception as e:
            if verbose:
                print(f"RAG not available: {e}")
    
    def generate(self, prompt: str, steer: bool = True) -> PipelineResult:
        """
        Generate Unity C# code from a natural language prompt.
        
        Args:
            prompt: Natural language description of desired behavior
            steer: Whether to apply RAG-based steering if code looks problematic
            
        Returns:
            PipelineResult with IR JSON and generated code
        """
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"PROMPT: {prompt[:80]}...")
            print(f"{'='*60}")
        
        # Step 1: Generate IR JSON
        if self.verbose:
            print("\n[1/4] Generating IR JSON...")
        
        ir_json = self._generate_ir(prompt)
        if ir_json is None:
            return PipelineResult(success=False, error="Failed to generate IR JSON")
        
        if self.verbose:
            print(f"  -> Class: {ir_json.get('class_name')}")
            print(f"  -> Components: {ir_json.get('components', [])}")
            print(f"  -> Fields: {len(ir_json.get('fields', []))}")
            print(f"  -> Behaviors: {len(ir_json.get('behaviors', []))}")
        
        # Step 2: RAG retrieval based on IR
        rag_context = ""
        rag_docs_used = 0
        rag_doc_names = []
        
        if self.rag:
            if self.verbose:
                print("\n[2/4] Retrieving Unity documentation...")
            
            rag_result = self.rag.retrieve_for_ir(
                ir_json, 
                threshold=0.5, 
                top_k_total=8,
                include_content=True
            )
            
            if rag_result.documents:
                rag_docs_used = len(rag_result.documents)
                rag_doc_names = [f"{doc.api_name} ({doc.score:.2f})" for doc in rag_result.documents]
                rag_context = self.rag.format_context_for_prompt(
                    rag_result.documents, 
                    max_tokens=2500
                )
                
                if self.verbose:
                    print(f"  -> Retrieved {rag_docs_used} docs from {len(rag_result.selected_namespaces)} namespaces")
                    for doc in rag_result.documents[:3]:
                        print(f"     - {doc.api_name} ({doc.score:.2f})")
        else:
            if self.verbose:
                print("\n[2/4] RAG not available, skipping...")
        
        # Step 3: Generate C# code
        if self.verbose:
            print("\n[3/4] Generating C# code...")
        
        code = self._generate_code(ir_json, rag_context)
        if code is None:
            return PipelineResult(
                success=False, 
                ir_json=ir_json,
                error="Failed to generate code"
            )
        
        if self.verbose:
            print(f"  -> Generated {len(code)} chars")
        
        # Step 3.5: Verify IR fields are in generated code
        missing_fields = self._verify_ir_fields_in_code(code, ir_json)
        if missing_fields:
            if self.verbose:
                print(f"  -> {len(missing_fields)} IR fields missing, injecting...")
            code = self._inject_missing_fields(code, missing_fields)
            if self.verbose:
                for f in missing_fields:
                    print(f"     + {f['type']} {f['name']}")
        
        # Step 4: RAG-based API validation and steering
        was_steered = False
        
        if steer and self.rag:
            if self.verbose:
                print("\n[4/4] Validating APIs against RAG database...")
            
            # Validate all extracted APIs against RAG database (use IR for type context)
            suspicious = self._validate_apis_against_rag(code, ir_json=ir_json, threshold=0.70)
            
            # Also validate property chains (e.g., audioSource.clip.Play())
            chain_suspicious = self._validate_property_chains(code, ir_json)
            if chain_suspicious:
                if self.verbose:
                    print(f"  -> Chain validation found {len(chain_suspicious)} issues")
                suspicious.extend(chain_suspicious)
            
            if suspicious:
                if self.verbose:
                    print(f"  -> {len(suspicious)} suspicious APIs found:")
                    for s in suspicious[:5]:
                        if s["nearest"]:
                            print(f"     [!] {s['api']} -> nearest: {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"     [!] {s['api']} -> not found in Unity docs")
                
                # Get RAG docs for the suspicious APIs to help with steering
                steering_result = self.rag.retrieve_for_code_steering(code, threshold=0.5)
                
                if steering_result.documents:
                    steering_context = self.rag.format_context_for_prompt(
                        steering_result.documents,
                        max_tokens=2000
                    )
                    
                    # Pass the suspicious APIs to steering so LLM knows exactly what to fix
                    steered_code = self._steer_code(code, ir_json, steering_context, suspicious=suspicious)
                    if steered_code and steered_code != code:
                        code = steered_code
                        was_steered = True
                        if self.verbose:
                            print(f"  -> Steering applied (fixing {len(suspicious)} APIs)")
            else:
                if self.verbose:
                    print(f"  -> All APIs validated [OK]")
        else:
            if self.verbose:
                print("\n[4/4] Validation skipped")
        
        if self.verbose:
            print(f"\n{'='*60}")
            print("COMPLETE")
            print(f"{'='*60}")
        
        return PipelineResult(
            success=True,
            ir_json=ir_json,
            code=code,
            was_steered=was_steered,
            rag_docs_used=rag_docs_used,
            rag_doc_names=rag_doc_names
        )
    
    def _generate_ir(self, prompt: str) -> Optional[Dict]:
        """Generate IR JSON from prompt"""
        try:
            # Build user message with embedded schema (model responds better to this)
            user_content = f"""Describe this Unity behavior as JSON with class_name, components, fields, and behaviors.

Request: {prompt}

Output format:
{{
  "class_name": "BehaviorName",
  "components": ["Rigidbody", "AudioSource"],
  "fields": [{{"name": "speed", "type": "float", "default": 5.0}}],
  "behaviors": [{{"name": "movement", "trigger": "when key pressed", "actions": [{{"action": "move forward"}}]}}]
}}

JSON:"""
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": IR_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 2000
                },
                timeout=60
            )
            
            if response.status_code != 200:
                if self.verbose:
                    print(f"  IR error: HTTP {response.status_code}")
                    print(f"  Response: {response.text[:500]}")
                return None
            
            content = response.json()["choices"][0]["message"]["content"]
            
            # DEBUG: Print raw response if verbose
            if self.verbose:
                print(f"  Raw IR response ({len(content)} chars):")
                print(f"  {content[:300]}...")
            
            # Use robust JSON parsing (json-repair, json5, regex fallback)
            _, parsed = extract_json_from_response(content)
            
            # DEBUG: If parsing failed, show why
            if parsed is None and self.verbose:
                print(f"  [WARN] JSON parsing failed!")
                print(f"  Full response:\n{content}")
            
            # Validate that parsed result is a dict, not a list
            if parsed is not None and not isinstance(parsed, dict):
                if self.verbose:
                    print(f"  IR error: Expected dict, got {type(parsed).__name__}")
                return None
            
            return parsed
            
        except Exception as e:
            if self.verbose:
                print(f"  IR generation error: {e}")
                import traceback
                traceback.print_exc()
            return None
    
    def _generate_code(self, ir_json: Dict, rag_context: str) -> Optional[str]:
        """Generate C# code from IR JSON with RAG context, following IR structure closely."""
        try:
            # Build structured prompt that emphasizes IR mapping
            user_content = self._build_structured_prompt(ir_json, rag_context)
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": CODE_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            code = self._clean_code(code)
            
            # Deduplicate any duplicate declarations first
            code = self._deduplicate_declarations(code)
            
            # Validate and fix issues with targeted regeneration
            validation = self._validate_code_against_ir(code, ir_json)
            
            if validation:
                if self.verbose:
                    print(f"  Validation: {validation}")
                    print(f"  Applying targeted fixes...")
                
                # Apply targeted fixes
                fixed_code = self._apply_targeted_fixes(code, ir_json, validation, rag_context)
                if fixed_code:
                    code = fixed_code
                    # Deduplicate again after fixes (in case fixes introduced duplicates)
                    code = self._deduplicate_declarations(code)
                    # Re-validate after fixes
                    final_validation = self._validate_code_against_ir(code, ir_json)
                    if self.verbose:
                        if final_validation:
                            print(f"  After fixes: {final_validation}")
                        else:
                            print(f"  After fixes: ✓ Passed")
                else:
                    if self.verbose:
                        print(f"  Targeted fixes failed, using original code")
            else:
                if self.verbose:
                    print(f"  Validation: ✓ Passed")
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Code generation error: {e}")
            return None
    
    def _apply_targeted_fixes(self, code: str, ir_json: Dict, validation_issues: str, rag_context: str) -> Optional[str]:
        """Apply targeted fixes to specific issues in the code."""
        try:
            # Parse validation issues to identify what needs fixing
            issues = validation_issues.split("; ")
            missing_fields = []
            wrong_component_types = []
            
            for issue in issues:
                if "Field '" in issue and "not found" in issue:
                    # Extract field name
                    field_match = issue.split("Field '")[1].split("'")[0]
                    missing_fields.append(field_match)
                elif "Component '" in issue:
                    # Extract component name
                    comp_match = issue.split("Component '")[1].split("'")[0]
                    wrong_component_types.append(comp_match)
            
            if not missing_fields and not wrong_component_types:
                return None  # Can't fix unknown issues
            
            # Try programmatic fixes first (for missing fields)
            if missing_fields:
                fixed_code = self._inject_missing_fields_programmatically(code, ir_json, missing_fields)
                if fixed_code:
                    code = fixed_code
                    if self.verbose:
                        print(f"  Injected {len(missing_fields)} missing fields programmatically")
            
            # Always ensure field names match IR exactly (rename if needed)
            code = self._rename_fields_to_match_ir(code, ir_json)
            
            # For component type fixes, use LLM
            if wrong_component_types:
                fix_prompt = self._build_fix_prompt(code, ir_json, [], wrong_component_types, rag_context)
                
                response = requests.post(
                    self.llm_url,
                    json={
                        "model": "local-model",
                        "messages": [
                            {"role": "system", "content": "You are a Unity C# code fixer. Fix ONLY the component type issues. Preserve all other code unchanged."},
                            {"role": "user", "content": fix_prompt}
                        ],
                        "temperature": DEFAULT_TEMPERATURE * 0.5,
                        "max_tokens": 4000
                    },
                    timeout=120
                )
                
                if response.status_code == 200:
                    fixed_code = response.json()["choices"][0]["message"]["content"]
                    code = self._clean_code(fixed_code)
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Targeted fix error: {e}")
            return None
    
    def _inject_missing_fields_programmatically(self, code: str, ir_json: Dict, missing_field_names: List[str]) -> Optional[str]:
        """Programmatically inject missing fields into the code."""
        try:
            # Get field definitions from IR
            fields = ir_json.get("fields", [])
            fields_to_add = [f for f in fields if f.get("name", "") in missing_field_names]
            
            if not fields_to_add:
                return None
            
            # Find the class declaration and opening brace
            lines = code.split('\n')
            class_line_idx = None
            class_brace_idx = None
            first_field_idx = None
            
            for i, line in enumerate(lines):
                # Find class declaration
                if class_line_idx is None and ('class ' in line or 'public class' in line):
                    class_line_idx = i
                    # Check if opening brace is on same line
                    if '{' in line:
                        class_brace_idx = i
                    continue
                
                # Find opening brace if not on same line as class
                if class_line_idx is not None and class_brace_idx is None:
                    if '{' in line:
                        class_brace_idx = i
                        continue
                
                # After finding the class body, look for first field or method
                if class_brace_idx is not None and first_field_idx is None:
                    stripped = line.strip()
                    # Skip empty lines and closing braces
                    if not stripped or stripped == '}':
                        continue
                    # Find first field declaration (with [SerializeField] or private/public)
                    if '[SerializeField]' in line or (stripped.startswith('private ') or stripped.startswith('public ')):
                        # Make sure it's not a method (has parentheses or is void)
                        if '(' not in line and 'void' not in line and '{' not in line:
                            first_field_idx = i
                            break
                    # Or find first method
                    elif 'void ' in line or stripped.startswith('private ') or stripped.startswith('public '):
                        if '(' in line:  # It's a method
                            first_field_idx = i
                            break
            
            if class_line_idx is None:
                return None  # Can't find class
            
            if class_brace_idx is None:
                # Try to find opening brace after class line
                for i in range(class_line_idx + 1, min(class_line_idx + 5, len(lines))):
                    if '{' in lines[i]:
                        class_brace_idx = i
                        break
                
                if class_brace_idx is None:
                    return None  # Can't find class body
            
            # If no fields found, insert right after opening brace
            if first_field_idx is None:
                first_field_idx = class_brace_idx + 1
            
            # Make sure we're inserting inside the class (after the brace)
            if first_field_idx <= class_brace_idx:
                first_field_idx = class_brace_idx + 1
            
            # Build field declarations using EXACT names from IR
            field_declarations = []
            for field in fields_to_add:
                name = field.get("name", "")
                ftype = field.get("type", "")
                default = field.get("default", "")
                
                # Format default value
                if default is None:
                    default_str = "null"
                elif isinstance(default, str):
                    default_str = f'"{default}"'
                elif isinstance(default, bool):
                    default_str = "true" if default else "false"
                else:
                    default_str = str(default)
                
                # Handle special types
                if ftype.endswith("[]"):
                    default_str = "null"
                elif ftype in ["Vector3", "Vector2", "Quaternion", "Color"]:
                    if default is None or default == "null":
                        default_str = f"{ftype}.zero" if ftype != "Color" else "Color.white"
                    else:
                        default_str = str(default)
                
                # Use EXACT field name from IR (no underscore prefix)
                field_decl = f"    [SerializeField] private {ftype} {name} = {default_str};"
                field_declarations.append(field_decl)
            
            # Insert fields
            if field_declarations:
                # Add blank line before if needed
                if first_field_idx > 0 and lines[first_field_idx - 1].strip() and not lines[first_field_idx - 1].strip().startswith('['):
                    field_declarations.insert(0, "")
                
                # Insert fields
                lines = lines[:first_field_idx] + field_declarations + lines[first_field_idx:]
                
                return '\n'.join(lines)
            
            return None
            
        except Exception as e:
            if self.verbose:
                print(f"  Programmatic field injection error: {e}")
            return None
    
    def _rename_fields_to_match_ir(self, code: str, ir_json: Dict) -> str:
        """Rename fields in code to match IR field names exactly."""
        try:
            import re
            fields = ir_json.get("fields", [])
            if not fields:
                return code
            
            # Build mapping of variations to IR field names
            field_mappings = {}
            for field in fields:
                ir_name = field.get("name", "")
                if not ir_name:
                    continue
                
                # Always check for underscore prefix variation (most common)
                underscore_variation = f"_{ir_name}"
                
                # Check if underscore variation exists in code
                if underscore_variation in code:
                    # Check if it's actually used as a field (not just a substring)
                    # Look for field declaration patterns
                    field_patterns = [
                        rf'\bprivate\s+\w+\s+{re.escape(underscore_variation)}\b',
                        rf'\[SerializeField\]\s+private\s+\w+\s+{re.escape(underscore_variation)}\b',
                        rf'\bpublic\s+\w+\s+{re.escape(underscore_variation)}\b',
                    ]
                    
                    is_field = False
                    for pattern in field_patterns:
                        if re.search(pattern, code):
                            is_field = True
                            break
                    
                    # Also check if it's used as a variable (with word boundaries)
                    if not is_field:
                        if re.search(rf'\b{re.escape(underscore_variation)}\b', code):
                            is_field = True
                    
                    if is_field:
                        field_mappings[underscore_variation] = ir_name
            
            # Perform replacements (order matters - do declarations first, then usage)
            if field_mappings:
                for old_name, new_name in field_mappings.items():
                    # Replace field declarations (most specific patterns first)
                    code = re.sub(
                        rf'(\[SerializeField\]\s+private\s+\w+\s+){re.escape(old_name)}(\s*[=;])',
                        rf'\1{new_name}\2',
                        code
                    )
                    code = re.sub(
                        rf'\bprivate\s+\w+\s+{re.escape(old_name)}(\s*[=;])',
                        lambda m: m.group(0).replace(old_name, new_name),
                        code
                    )
                    code = re.sub(
                        rf'\bpublic\s+\w+\s+{re.escape(old_name)}(\s*[=;])',
                        lambda m: m.group(0).replace(old_name, new_name),
                        code
                    )
                    
                    # Replace all other usages (with word boundaries to avoid partial matches)
                    code = re.sub(rf'\b{re.escape(old_name)}\b', new_name, code)
                    
                    if self.verbose:
                        print(f"  Renamed field '{old_name}' -> '{new_name}' to match IR")
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Field renaming error: {e}")
            return code
    
    def _build_fix_prompt(self, code: str, ir_json: Dict, missing_fields: List[str], wrong_components: List[str], rag_context: str) -> str:
        """Build a prompt for targeted fixes."""
        parts = []
        
        parts.append("=== TARGETED CODE FIX ===")
        parts.append("")
        parts.append("Fix ONLY the specific issues below. Keep ALL other code unchanged.")
        parts.append("")
        
        if missing_fields:
            parts.append("=== MISSING FIELDS ===")
            parts.append("Add these fields to the class using EXACT names from IR:")
            fields = ir_json.get("fields", [])
            for field in fields:
                field_name = field.get("name", "")
                if field_name in missing_fields:
                    ftype = field.get("type", "")
                    default = field.get("default", "")
                    parts.append(f"  - {field_name} ({ftype}) = {default}")
                    parts.append(f"    → Add: [SerializeField] private {ftype} {field_name} = {default};")
            parts.append("")
            parts.append("CRITICAL: Use the EXACT field names above. Do NOT rename them.")
            parts.append("")
        
        if wrong_components:
            parts.append("=== COMPONENT FIXES ===")
            parts.append("Ensure these components are referenced correctly:")
            for comp in wrong_components:
                parts.append(f"  - Use GetComponent<{comp}>() (not {comp}2D)")
            parts.append("")
        
        if rag_context:
            parts.append("=== UNITY API DOCUMENTATION ===")
            parts.append(rag_context[:1000])  # Limit context for fix prompt
            parts.append("")
        
        parts.append("=== CURRENT CODE (fix the issues above) ===")
        parts.append("```csharp")
        parts.append(code)
        parts.append("```")
        parts.append("")
        parts.append("Output ONLY the fixed C# code with the missing fields added and components corrected.")
        parts.append("Keep all other code exactly as it is.")
        
        return "\n".join(parts)
    
    def _build_structured_prompt(self, ir_json: Dict, rag_context: str) -> str:
        """Build a structured prompt that emphasizes IR mapping."""
        parts = []
        
        if rag_context:
            parts.append("=== UNITY API DOCUMENTATION ===")
            parts.append(rag_context)
            parts.append("")
        
        parts.append("=== BEHAVIOR SPECIFICATION (MUST FOLLOW EXACTLY) ===")
        parts.append("")
        
        # Class name
        class_name = ir_json.get("class_name", "Behavior")
        parts.append(f"CLASS NAME: {class_name}")
        parts.append("")
        
        # Components
        components = ir_json.get("components", [])
        if components:
            parts.append("COMPONENTS (get in Start()):")
            for comp in components:
                parts.append(f"  - {comp}")
            parts.append("")
        
        # Fields
        fields = ir_json.get("fields", [])
        if fields:
            parts.append("FIELDS (declare EXACTLY as shown - use these EXACT names):")
            for field in fields:
                name = field.get("name", "")
                ftype = field.get("type", "")
                default = field.get("default", "")
                parts.append(f"  - {name} ({ftype}) = {default}")
                parts.append(f"    → Declare as: [SerializeField] private {ftype} {name} = {default};")
            parts.append("")
            parts.append("CRITICAL: Use the EXACT field names above. Do NOT rename them.")
            parts.append("  Example: If IR says 'detectionRange', use 'detectionRange' NOT '_detectionRadius'")
            parts.append("")
        
        # Behaviors
        behaviors = ir_json.get("behaviors", [])
        if behaviors:
            parts.append("BEHAVIORS (implement each one):")
            for i, behavior in enumerate(behaviors, 1):
                if isinstance(behavior, dict):
                    name = behavior.get("name", f"behavior_{i}")
                    trigger = behavior.get("trigger", "")
                    actions = behavior.get("actions", [])
                    parts.append(f"  {i}. {name}")
                    parts.append(f"     Trigger: {trigger}")
                    parts.append(f"     Actions:")
                    for action in actions:
                        if isinstance(action, dict):
                            action_text = action.get("action", str(action))
                        else:
                            action_text = str(action)
                        parts.append(f"       - {action_text}")
                else:
                    parts.append(f"  {i}. {behavior}")
            parts.append("")
        
        # Full JSON for reference
        parts.append("=== FULL IR JSON (for reference) ===")
        parts.append(json.dumps(ir_json, indent=2))
        parts.append("")
        parts.append("Generate the complete Unity C# MonoBehaviour script following the specification above.")
        
        return "\n".join(parts)
    
    def _validate_code_against_ir(self, code: str, ir_json: Dict) -> Optional[str]:
        """Validate that code follows IR structure exactly."""
        issues = []
        
        # Check class name
        class_name = ir_json.get("class_name", "")
        if class_name:
            # Check for exact class name (allowing for "public class ClassName")
            if f"class {class_name}" not in code and f"class {class_name} " not in code:
                issues.append(f"Class name '{class_name}' not found exactly")
        
        # Check fields - must use exact names (strict checking)
        fields = ir_json.get("fields", [])
        for field in fields:
            field_name = field.get("name", "")
            if not field_name:
                continue
            
            # Check for exact field name (prefer exact match, but allow underscore prefix as warning)
            # Look for exact field_name first
            exact_patterns = [
                f" {field_name} ",  # Exact match with spaces
                f" {field_name}=",  # As assignment
                f"[SerializeField] private {field_name}",  # Serialized field
                f"private {field_name}",  # Private field
                f"public {field_name}",  # Public field
            ]
            
            found_exact = False
            for pattern in exact_patterns:
                if pattern in code:
                    found_exact = True
                    break
            
            # Also check if it's used as a variable
            if not found_exact:
                usage_patterns = [
                    f"{field_name}.",
                    f"{field_name} =",
                ]
                for pattern in usage_patterns:
                    if pattern in code:
                        found_exact = True
                        break
            
            # If exact not found, check for underscore variant (but flag as issue)
            if not found_exact:
                underscore_patterns = [
                    f"_{field_name} ",
                    f"_{field_name}=",
                    f"[SerializeField] private _{field_name}",
                    f"private _{field_name}",
                ]
                found_underscore = False
                for pattern in underscore_patterns:
                    if pattern in code:
                        found_underscore = True
                        break
                
                if not found_underscore:
                    # Check usage with underscore
                    if f"_{field_name}." in code or f"_{field_name} =" in code:
                        found_underscore = True
                
                if found_underscore:
                    issues.append(f"Field '{field_name}' found as '_{field_name}' (should use exact IR name)")
                else:
                    issues.append(f"Field '{field_name}' not found (check exact name)")
        
        # Check components - must use exact types
        components = ir_json.get("components", [])
        for comp in components:
            # Check for GetComponent<ComponentType> or component reference
            if f"GetComponent<{comp}>" not in code and f"GetComponent<{comp}2D>" not in code:
                # Also check if it's referenced as a variable
                comp_lower = comp.lower()
                if comp_lower not in code.lower() and comp not in code:
                    issues.append(f"Component '{comp}' not referenced (check exact type)")
        
        # Check behaviors - must implement all
        behaviors = ir_json.get("behaviors", [])
        found_behaviors = 0
        for behavior in behaviors:
            if isinstance(behavior, dict):
                trigger = behavior.get("trigger", "")
                name = behavior.get("name", "")
                actions = behavior.get("actions", [])
                
                # Check if trigger keywords appear
                trigger_keywords = [w for w in trigger.split() if len(w) > 3][:3]
                trigger_found = any(kw.lower() in code.lower() for kw in trigger_keywords)
                
                # Check if behavior name appears
                name_found = name.lower() in code.lower() if name else False
                
                # Check if action keywords appear
                action_found = False
                for action in actions:
                    if isinstance(action, dict):
                        action_text = action.get("action", "")
                    else:
                        action_text = str(action)
                    action_keywords = [w for w in action_text.split() if len(w) > 3][:2]
                    if any(kw.lower() in code.lower() for kw in action_keywords):
                        action_found = True
                        break
                
                if trigger_found or name_found or action_found:
                    found_behaviors += 1
            else:
                if str(behavior).lower()[:20] in code.lower():
                    found_behaviors += 1
        
        if found_behaviors < len(behaviors) * 0.6:  # At least 60% should be found
            issues.append(f"Only {found_behaviors}/{len(behaviors)} behaviors detected")
        
        # Check for duplicate variable declarations
        duplicate_issues = self._check_duplicate_declarations(code)
        issues.extend(duplicate_issues)
        
        if issues:
            return "; ".join(issues)
        return None
    
    def _check_duplicate_declarations(self, code: str) -> List[str]:
        """
        Check for duplicate variable declarations in C# code.
        Returns list of issue messages.
        """
        issues = []
        lines = code.split('\n')
        
        # Track field declarations (class-level variables)
        field_declarations = {}  # var_name -> list of line_nums
        
        # Track method parameters
        method_params = {}  # method_name -> list of param_names
        
        for line_num, line in enumerate(lines, 1):
            stripped = line.strip()
            
            # Skip comments
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
                continue
            
            # Check for field declarations (class-level)
            # Pattern: [attributes] access_modifier type var_name [= value];
            # Try more specific pattern first (with attributes), then fallback
            var_name = None
            # First try: [SerializeField] or [Header] pattern
            attr_match = re.search(r'\[(?:SerializeField|Header)[^\]]*\]\s*(?:private|public|protected|internal)?\s+\w+\s+(\w+)\s*[=;]', line)
            if attr_match:
                var_name = attr_match.group(1)
            else:
                # Fallback: simple access modifier pattern (but not if it's already matched above)
                simple_match = re.search(r'(?:private|public|protected|internal)\s+\w+\s+(\w+)\s*[=;]', line)
                if simple_match:
                    var_name = simple_match.group(1)
            
            if var_name:
                if var_name not in field_declarations:
                    field_declarations[var_name] = []
                field_declarations[var_name].append(line_num)
            
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
        
        # Check for duplicate field declarations (deduplicate line numbers first)
        for var_name, line_nums in field_declarations.items():
            unique_lines = sorted(set(line_nums))  # Remove duplicate line numbers
            if len(unique_lines) > 1:
                issues.append(f"Duplicate field declaration '{var_name}' at lines {', '.join(map(str, unique_lines))}")
        
        return issues
    
    def _deduplicate_declarations(self, code: str) -> str:
        """
        Remove duplicate variable declarations from C# code.
        Keeps the first declaration and removes subsequent duplicates.
        """
        lines = code.split('\n')
        seen_declarations = set()  # Track var_name -> (line_index, full_line)
        lines_to_remove = set()  # Track line indices to remove
        
        for line_idx, line in enumerate(lines):
            stripped = line.strip()
            
            # Skip comments and empty lines
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*') or not stripped:
                continue
            
            # Check for field declarations
            var_name = None
            # First try: [SerializeField] or [Header] pattern
            attr_match = re.search(r'\[(?:SerializeField|Header)[^\]]*\]\s*(?:private|public|protected|internal)?\s+\w+\s+(\w+)\s*[=;]', line)
            if attr_match:
                var_name = attr_match.group(1)
            else:
                # Fallback: simple access modifier pattern
                simple_match = re.search(r'(?:private|public|protected|internal)\s+\w+\s+(\w+)\s*[=;]', line)
                if simple_match:
                    var_name = simple_match.group(1)
            
            if var_name:
                if var_name in seen_declarations:
                    # This is a duplicate - mark for removal
                    lines_to_remove.add(line_idx)
                else:
                    # First occurrence - keep it
                    seen_declarations.add(var_name)
        
        # Remove duplicate lines (in reverse order to maintain indices)
        for line_idx in sorted(lines_to_remove, reverse=True):
            lines.pop(line_idx)
        
        return '\n'.join(lines)
    
    def _steer_code(self, code: str, ir_json: Dict, rag_context: str, suspicious: list = None) -> Optional[str]:
        """Apply RAG-based steering to fix code issues"""
        try:
            # Format the invalid APIs list for the prompt
            invalid_apis_text = ""
            if suspicious:
                for s in suspicious:
                    if s["nearest"]:
                        invalid_apis_text += f"- {s['api']} -> Try using: {s['nearest']}\n"
                    else:
                        invalid_apis_text += f"- {s['api']} -> Does not exist in Unity, remove or replace\n"
            else:
                invalid_apis_text = "- General API issues detected\n"
            
            prompt = STEERING_PROMPT_TEMPLATE.format(
                invalid_apis=invalid_apis_text,
                rag_context=rag_context,
                ir_json=json.dumps(ir_json, indent=2),
                code=code
            )
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [{"role": "user", "content": prompt}],
                    "temperature": 0.2,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            steered = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(steered)
            
        except Exception as e:
            if self.verbose:
                print(f"  Steering error: {e}")
            return None
    
    def _validate_apis_against_rag(self, code: str, ir_json: Dict = None, threshold: float = 0.85) -> list:
        """
        Validate ALL method calls in code against RAG database.
        
        Uses a two-tier approach:
        1. EXACT MATCH: Check against API whitelist from RAG index (fast, reliable)
        2. SEMANTIC FALLBACK: If not in whitelist, use embedding similarity (slower, catches variations)
        
        Also validates:
        - Attributes: [AttributeName]
        - Constructors: new ClassName(...)
        
        Args:
            code: Generated C# code
            ir_json: Optional IR JSON for type context
            threshold: Minimum similarity score for semantic fallback (default 0.85)
            
        Returns:
            List of suspicious APIs that don't match known Unity APIs
        """
        import re
        
        if not self.rag:
            return []
        
        suspicious = []
        checked = set()  # Avoid duplicate checks
        
        # Get component types from IR for context
        ir_components = []
        if ir_json:
            raw_components = ir_json.get("components", [])
            # Normalize: handle both string and dict formats
            for c in raw_components:
                if isinstance(c, str):
                    ir_components.append(c)
                elif isinstance(c, dict):
                    # Extract from {"component_type": "Rigidbody"} or {"name": "Rigidbody"}
                    comp_name = c.get("component_type") or c.get("name") or c.get("type", "")
                    if comp_name:
                        ir_components.append(comp_name)
        
        # Common properties/methods to skip (always valid on most Unity objects)
        skip_members = {
            'name', 'tag', 'gameObject', 'transform', 'enabled', 'position', 
            'rotation', 'localPosition', 'localRotation', 'localScale', 'parent',
            'GetComponent', 'GetComponents', 'GetComponentInChildren', 'GetComponentsInChildren',
            'AddComponent', 'Destroy', 'Instantiate', 'DontDestroyOnLoad',
        }
        
        # Common C# types to skip entirely
        skip_classes = {
            'System', 'List', 'Dictionary', 'Math', 'String', 'Array', 'Console', 
            'IEnumerator', 'IEnumerable', 'Action', 'Func', 'Task', 'File', 'Path',
            'Exception', 'Type', 'Object', 'Convert', 'Int32', 'Float', 'Boolean',
            'StringBuilder', 'Regex', 'Match', 'Group', 'Collections',
        }
        
        # Common C# methods to skip
        skip_methods = {'Length', 'Count', 'ToString', 'GetType', 'Equals', 'GetHashCode', 'CompareTo'}
        
        def check_api(api: str) -> dict:
            """Check a single API against whitelist, then semantic fallback."""
            # TIER 1: Exact match against whitelist
            if hasattr(self.rag, 'is_valid_api') and self.rag.is_valid_api(api):
                return None  # Valid, no issue
            
            # TIER 2: Semantic search fallback
            results = self.rag.search(query=api, top_k=1, threshold=0.0)
            if results:
                if results[0].score >= threshold:
                    return None  # Close enough match
                else:
                    return {
                        "api": api,
                        "nearest": results[0].api_name,
                        "score": results[0].score
                    }
            else:
                return {"api": api, "nearest": None, "score": 0.0}
        
        # =====================================================================
        # PATTERN 0: Validate attributes [AttributeName]
        # =====================================================================
        attr_pattern = r'\[(\w+)(?:\([^\)]*\))?\]'
        for match in re.finditer(attr_pattern, code):
            attr_name = match.group(1)
            
            if attr_name in checked:
                continue
            checked.add(attr_name)
            
            # Check if valid attribute
            if hasattr(self.rag, 'is_valid_attribute') and self.rag.is_valid_attribute(attr_name):
                continue
            
            # Also check whitelist for the attribute class itself
            if hasattr(self.rag, 'is_valid_api'):
                if self.rag.is_valid_api(attr_name) or self.rag.is_valid_api(f"{attr_name}Attribute"):
                    continue
            
            suspicious.append({
                "api": f"[{attr_name}]",
                "nearest": None,
                "score": 0.0,
                "type": "attribute"
            })
        
        # =====================================================================
        # PATTERN 1: Type.Method calls using IR components
        # e.g., animator.SetState -> check "Animator.SetState"
        # =====================================================================
        for component in ir_components:
            var_pattern = rf'\b(\w*{re.escape(component.lower())}\w*)\s*\.\s*(\w+)'
            
            for match in re.finditer(var_pattern, code, re.IGNORECASE):
                method = match.group(2)
                if method in skip_members or method in skip_methods:
                    continue
                    
                api = f"{component}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                issue = check_api(api)
                if issue:
                    suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 2: Common Unity base types (always check these)
        # =====================================================================
        base_types = [
            ("transform", "Transform"),
            ("gameObject", "GameObject"),
            ("rb", "Rigidbody"),
            ("rigidbody", "Rigidbody"),
            ("collider", "Collider"),
            ("renderer", "Renderer"),
            ("camera", "Camera"),
            ("light", "Light"),
            ("audio", "AudioSource"),
            ("animator", "Animator"),
        ]
        
        for var_hint, type_name in base_types:
            pattern = rf'\b{re.escape(var_hint)}\s*\.\s*(\w+)'
            for match in re.finditer(pattern, code, re.IGNORECASE):
                method = match.group(1)
                if method in skip_members or method in skip_methods:
                    continue
                    
                api = f"{type_name}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                issue = check_api(api)
                if issue:
                    suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 3: ALL Class.member patterns (methods AND properties)
        # Catches: Time.deltaTime, Physics.Raycast(), Vector3.zero, etc.
        # =====================================================================
        static_pattern = r'\b([A-Z][a-zA-Z0-9]+)\s*\.\s*([a-zA-Z][a-zA-Z0-9]*)'
        
        for match in re.finditer(static_pattern, code):
            class_name = match.group(1)
            member = match.group(2)
            
            if class_name in skip_classes:
                continue
            
            if member in skip_methods:
                continue
            
            api = f"{class_name}.{member}"
            if api in checked:
                continue
            checked.add(api)
            
            issue = check_api(api)
            if issue:
                suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 4: Constructor calls - new ClassName(...)
        # Catches hallucinated constructors like: new AudioClip("path")
        # =====================================================================
        ctor_pattern = r'\bnew\s+([A-Z][a-zA-Z0-9]+)\s*\('
        
        # Known Unity types that CAN be constructed with new
        constructable_types = {
            'Vector2', 'Vector3', 'Vector4', 'Quaternion', 'Color', 'Rect', 'Bounds',
            'Ray', 'RaycastHit', 'ContactPoint', 'WaitForSeconds', 'WaitForSecondsRealtime',
            'WaitUntil', 'WaitWhile', 'WaitForEndOfFrame', 'WaitForFixedUpdate',
            'Material', 'Mesh', 'Texture2D', 'RenderTexture', 'AnimationCurve',
            'GUIStyle', 'GUIContent', 'GUILayoutOption',
        }
        
        # Types that should NOT be constructed with new (use factory methods or assignment)
        non_constructable_types = {
            'AudioClip', 'AudioSource', 'GameObject', 'Transform', 'Component',
            'Rigidbody', 'Collider', 'Renderer', 'Camera', 'Light', 'Animator',
            'MonoBehaviour', 'ScriptableObject', 'Sprite', 'Font',
        }
        
        for match in re.finditer(ctor_pattern, code):
            type_name = match.group(1)
            
            ctor_key = f"new {type_name}"
            if ctor_key in checked:
                continue
            checked.add(ctor_key)
            
            # Flag if trying to construct a non-constructable type
            if type_name in non_constructable_types:
                suspicious.append({
                    "api": f"new {type_name}()",
                    "nearest": f"Use GetComponent<{type_name}>() or Resources.Load<{type_name}>()",
                    "score": 0.0,
                    "type": "invalid_constructor"
                })
        
        return suspicious
    
    def _validate_property_chains(self, code: str, ir_json: Dict) -> list:
        """
        Validate property chains like audioSource.clip.Play().
        
        Uses the RAG type map to resolve each step of the chain
        and validate that the final method/property exists.
        
        Args:
            code: Generated C# code
            ir_json: IR JSON for type context
            
        Returns:
            List of suspicious chain APIs
        """
        import re
        
        if not self.rag or not hasattr(self.rag, 'type_map'):
            return []
        
        suspicious = []
        checked_chains = set()
        
        # Get component types from IR for base type inference
        components = {}
        if ir_json:
            for comp in ir_json.get("components", []):
                # Handle both string and dict formats from LLM
                if isinstance(comp, dict):
                    comp_name = comp.get("name", "")
                elif isinstance(comp, str):
                    comp_name = comp
                else:
                    continue
                
                if not comp_name:
                    continue
                
                # Map common variable patterns to component types
                components[comp_name.lower()] = comp_name
                # Common abbreviations
                if comp_name == "Rigidbody":
                    components["rb"] = comp_name
                if comp_name == "AudioSource":
                    components["audio"] = comp_name
                    components["source"] = comp_name
        
        # Add common Unity types always present
        common_types = {
            "transform": "Transform",
            "gameobject": "GameObject",
            "renderer": "Renderer",
            "collider": "Collider",
        }
        components.update(common_types)
        
        # Find property chains: variable.prop1.prop2 or variable.prop1.method()
        # Match chains with 2+ segments
        chain_pattern = r'\b(\w+)((?:\.\w+){2,})'
        
        for match in re.finditer(chain_pattern, code):
            var_name = match.group(1).lower()
            chain_str = match.group(2)  # ".clip.Play" or ".transform.position.x"
            
            # Skip if we've already checked this exact chain
            chain_key = f"{var_name}{chain_str}"
            if chain_key in checked_chains:
                continue
            checked_chains.add(chain_key)
            
            # Determine base type from variable name
            base_type = None
            for pattern, type_name in components.items():
                if pattern in var_name:
                    base_type = type_name
                    break
            
            if not base_type:
                continue
            
            # Parse chain: ".clip.Play()" -> ["clip", "Play"]
            chain_parts = [s.rstrip('()[]') for s in chain_str.split('.') if s]
            
            if len(chain_parts) < 2:
                continue
            
            # Resolve and validate the chain
            final_type, invalid_steps = self.rag.resolve_chain_type(base_type, chain_parts)
            
            for inv in invalid_steps:
                suspicious.append({
                    "api": inv["api"],
                    "nearest": None,
                    "score": 0.0,
                    "reason": f"chain: {base_type}.{'.'.join(chain_parts)}"
                })
        
        return suspicious
    
    def _verify_ir_fields_in_code(self, code: str, ir_json: Dict) -> list:
        """
        Verify that all fields from the IR JSON are declared in the generated code.
        
        Args:
            code: Generated C# code
            ir_json: IR JSON specification
            
        Returns:
            List of missing fields (each with name, type, default)
        """
        import re
        
        if not ir_json:
            return []
        
        ir_fields = ir_json.get("fields", [])
        if not ir_fields:
            return []
        
        missing = []
        
        for field in ir_fields:
            # Handle case where field is not a dict
            if isinstance(field, str):
                field_name = field
                field_type = "float"
                field_default = None
            elif isinstance(field, dict):
                field_name = field.get("name", "")
                field_type = field.get("type", "float")
                field_default = field.get("default")
            else:
                continue
            
            if not field_name:
                continue
            
            # Check if field is declared in code
            # Patterns: "public float fieldName", "private float fieldName", "[SerializeField] float fieldName"
            patterns = [
                rf'\b(public|private|protected)?\s*{re.escape(field_type)}\s+{re.escape(field_name)}\b',
                rf'\[SerializeField\]\s*(private|public)?\s*{re.escape(field_type)}\s+{re.escape(field_name)}\b',
                rf'\bpublic\s+{re.escape(field_type)}\s+{re.escape(field_name)}\s*[=;]',
            ]
            
            found = False
            for pattern in patterns:
                if re.search(pattern, code, re.IGNORECASE):
                    found = True
                    break
            
            if not found:
                missing.append({
                    "name": field_name,
                    "type": field_type,
                    "default": field_default
                })
        
        return missing
    
    def _inject_missing_fields(self, code: str, missing_fields: list) -> str:
        """
        Inject missing fields into the generated code.
        
        Adds fields after the class declaration opening brace.
        """
        import re
        
        if not missing_fields:
            return code
        
        # Build field declarations
        field_lines = []
        for field in missing_fields:
            name = field["name"]
            ftype = field["type"]
            default = field.get("default")
            
            if default is not None:
                if isinstance(default, str):
                    field_lines.append(f'    public {ftype} {name} = "{default}";')
                elif isinstance(default, bool):
                    field_lines.append(f'    public {ftype} {name} = {str(default).lower()};')
                else:
                    field_lines.append(f'    public {ftype} {name} = {default}f;' if ftype == "float" else f'    public {ftype} {name} = {default};')
            else:
                field_lines.append(f'    public {ftype} {name};')
        
        fields_block = "\n".join(field_lines)
        
        # Find the class opening and insert after it
        # Pattern: "class ClassName : MonoBehaviour {" or "class ClassName {"
        class_pattern = r'(class\s+\w+[^{]*\{)'
        
        match = re.search(class_pattern, code)
        if match:
            insert_pos = match.end()
            code = code[:insert_pos] + f"\n    // IR-specified fields (auto-injected)\n{fields_block}\n" + code[insert_pos:]
        
        return code
    
    def _clean_code(self, code: str) -> str:
        """Clean C# code from markdown formatting"""
        if "```csharp" in code:
            code = code.split("```csharp")[1].split("```")[0]
        elif "```cs" in code:
            code = code.split("```cs")[1].split("```")[0]
        elif "```" in code:
            parts = code.split("```")
            if len(parts) >= 2:
                code = parts[1]
        return code.strip()
    
    def generate_oneshot(self, prompt: str) -> Optional[str]:
        """
        Generate Unity C# code directly from prompt (no IR step).
        
        Args:
            prompt: Natural language description
            
        Returns:
            Generated C# code or None on failure
        """
        try:
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": ONESHOT_SYSTEM_PROMPT},
                        {"role": "user", "content": prompt}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"  Oneshot generation error: {e}")
            return None
    
    def compare(self, prompt: str) -> CompareResult:
        """
        Compare oneshot generation vs IR pipeline.
        
        Args:
            prompt: Natural language description
            
        Returns:
            CompareResult with both outputs
        """
        result = CompareResult(prompt=prompt)
        
        # Generate oneshot
        if self.verbose:
            print(f"\n{'='*70}")
            print("COMPARE MODE: Oneshot vs IR Pipeline")
            print(f"{'='*70}")
            print(f"Prompt: {prompt[:70]}...")
            print(f"{'='*70}")
            print(f"\n{'─'*70}")
            print("│ ONESHOT (NL -> C# direct)")
            print(f"{'─'*70}")
        
        result.oneshot_code = self.generate_oneshot(prompt)
        
        if self.verbose and result.oneshot_code:
            print(result.oneshot_code)
        elif self.verbose:
            print("  [Oneshot failed]")
        
        # Generate via IR pipeline
        if self.verbose:
            print(f"\n{'─'*70}")
            print("│ IR PIPELINE (NL -> IR -> C#)")
            print(f"{'─'*70}")
        
        ir_result = self.generate(prompt)
        
        if ir_result.success:
            result.ir_json = ir_result.ir_json
            result.ir_code = ir_result.code
            result.ir_steered = ir_result.was_steered
            result.ir_rag_docs = ir_result.rag_docs_used
            result.ir_rag_doc_names = ir_result.rag_doc_names
            
            # Print IR JSON
            if self.verbose:
                print(f"\n{'─'*40}")
                print("IR JSON:")
                print(f"{'─'*40}")
                print(json.dumps(ir_result.ir_json, indent=2))
            
            # Print generated code
            if self.verbose:
                print(f"\n{'─'*40}")
                print("Generated C# Code:")
                print(f"{'─'*40}")
                print(ir_result.code)
        elif self.verbose:
            print("  [IR Pipeline failed]")
        
        # Print comparison summary
        if self.verbose:
            self._print_comparison(result)
        
        return result
    
    def _print_comparison(self, result: CompareResult):
        """Print comparison summary"""
        print(f"\n{'='*70}")
        print("│ COMPARISON")
        print(f"{'='*70}")
        
        oneshot_lines = len(result.oneshot_code.split('\n')) if result.oneshot_code else 0
        oneshot_chars = len(result.oneshot_code) if result.oneshot_code else 0
        ir_lines = len(result.ir_code.split('\n')) if result.ir_code else 0
        ir_chars = len(result.ir_code) if result.ir_code else 0
        
        print(f"  Oneshot:     {oneshot_lines} lines, {oneshot_chars:>5} chars")
        print(f"  IR Pipeline: {ir_lines} lines, {ir_chars:>5} chars (RAG: {result.ir_rag_docs} docs, steered: {result.ir_steered})")
        
        # Check for Unity patterns
        patterns = [
            ("Update()", "Update"),
            ("Start()", "Start"),
            ("OnCollision", "OnCollision"),
            ("OnTrigger", "OnTrigger"),
            ("Rigidbody", "Rigidbody"),
            ("AddForce", "AddForce"),
            ("AudioSource", "AudioSource"),
            ("GetComponent", "GetComponent"),
        ]
        
        print(f"\n  Unity Patterns Found:")
        for name, pattern in patterns:
            oneshot_has = "[OK]" if result.oneshot_code and pattern in result.oneshot_code else "[X]"
            ir_has = "[OK]" if result.ir_code and pattern in result.ir_code else "[X]"
            print(f"    {name:<20} Oneshot: {oneshot_has}  IR: {ir_has}")
        
        # RAG-based API validation for both outputs
        if self.rag:
            print(f"\n  API Validation (RAG-based):")
            
            # Validate oneshot code
            if result.oneshot_code:
                oneshot_suspicious = self._validate_apis_against_rag(result.oneshot_code, threshold=0.70)
                if oneshot_suspicious:
                    print(f"    Oneshot: {len(oneshot_suspicious)} suspicious APIs")
                    for s in oneshot_suspicious[:3]:
                        if s["nearest"]:
                            print(f"      [!] {s['api']} -> {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      [!] {s['api']} -> not found")
                else:
                    print(f"    Oneshot: All APIs validated [OK]")
            
            # Validate IR pipeline code (with IR context for better type inference)
            if result.ir_code:
                ir_suspicious = self._validate_apis_against_rag(result.ir_code, ir_json=result.ir_json, threshold=0.70)
                if ir_suspicious:
                    print(f"    IR:      {len(ir_suspicious)} suspicious APIs")
                    for s in ir_suspicious[:3]:
                        if s["nearest"]:
                            print(f"      [!] {s['api']} -> {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      [!] {s['api']} -> not found")
                else:
                    print(f"    IR:      All APIs validated [OK]")
        
        print(f"\n{'='*70}")
        print("Compare complete.")
        print(f"{'='*70}")


# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(verbose: bool = True):
    """Interactive mode for testing"""
    print("="*60)
    print("UNITY PIPELINE SIMPLE - Interactive Mode")
    print("="*60)
    print("Commands:")
    print("  quit    - Exit")
    print("  verbose - Enable verbose output")
    print("  quiet   - Disable verbose output")
    print("  compare - Enter compare mode (oneshot vs IR)")
    print("  normal  - Exit compare mode")
    print("="*60)
    
    pipeline = UnityPipelineSimple(verbose=verbose)
    compare_mode = False
    
    while True:
        try:
            mode_indicator = "[compare] " if compare_mode else ""
            user_input = input(f"\n{mode_indicator}> ").strip()
            
            if not user_input:
                continue
            if user_input.lower() == "quit":
                break
            if user_input.lower() == "verbose":
                pipeline.verbose = True
                print("Verbose ON")
                continue
            if user_input.lower() == "quiet":
                pipeline.verbose = False
                print("Verbose OFF")
                continue
            if user_input.lower() == "compare":
                compare_mode = True
                print("Compare mode ON - will show oneshot vs IR pipeline")
                continue
            if user_input.lower() == "normal":
                compare_mode = False
                print("Compare mode OFF - normal IR pipeline only")
                continue
            
            if compare_mode:
                # Compare mode: show both oneshot and IR
                pipeline.compare(user_input)
            else:
                # Normal mode: IR pipeline only
                result = pipeline.generate(user_input)
                
                if result.success:
                    print(f"\n{'─'*60}")
                    print("IR JSON:")
                    print(f"{'─'*60}")
                    print(json.dumps(result.ir_json, indent=2))
                    
                    print(f"\n{'─'*60}")
                    print(f"C# CODE: (steered={result.was_steered}, rag_docs={result.rag_docs_used})")
                    print(f"{'─'*60}")
                    print(result.code)
                else:
                    print(f"Error: {result.error}")
                
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"Error: {e}")
    
    print("Exiting.")


def main():
    args = sys.argv[1:]
    
    if not args or args[0] in ["--help", "-h"]:
        print("Unity Pipeline Simple")
        print()
        print("Usage:")
        print("  python unity_pipeline_simple.py --interactive")
        print("  python unity_pipeline_simple.py \"your prompt here\"")
        return
    
    if args[0] in ["--interactive", "-i"]:
        verbose = "--verbose" in args or "-v" in args
        interactive_mode(verbose=verbose)
    else:
        prompt = " ".join(args)
        pipeline = UnityPipelineSimple(verbose=True)
        result = pipeline.generate(prompt)
        
        if result.success:
            print("\n" + "="*60)
            print("GENERATED CODE:")
            print("="*60)
            print(result.code)
        else:
            print(f"Error: {result.error}")


if __name__ == "__main__":
    main()

