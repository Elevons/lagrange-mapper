#!/usr/bin/env python3
"""
Unity API Validator v2 - Ground Truth Validation
=================================================
Validates Unity C# code against ACTUAL Unity documentation,
not just hardcoded patterns.

Validation tiers:
1. RAG Whitelist Check - Is this API in the Unity documentation?
2. Similarity Search - Is it close to a real API? (catches typos)
3. Pattern Matching - Catches structural errors (new AudioSource(), etc.)

Usage:
    from unity_api_validator_v2 import UnityAPIValidatorV2
    
    validator = UnityAPIValidatorV2()
    issues = validator.validate_code(code)
    
    # Each issue includes:
    # - Whether it's CONFIRMED invalid (not in docs)
    # - Suggested correct API if similar one exists
"""

import re
from typing import List, Dict, Set, Tuple, Optional
from dataclasses import dataclass, field

# Import RAG for ground truth validation
try:
    from unity_rag_query import UnityRAG
    HAS_RAG = True
except ImportError:
    HAS_RAG = False
    print("Warning: unity_rag_query not available, falling back to pattern-only validation")


@dataclass
class ValidationIssue:
    """A detected API issue with ground truth verification"""
    line_num: int
    code_snippet: str
    issue_type: str
    invalid_api: str
    suggested_fix: str
    
    # NEW: Ground truth verification
    verified_invalid: bool = False  # True if confirmed not in Unity docs
    nearest_valid_api: Optional[str] = None  # Closest real API if found
    similarity_score: float = 0.0  # How close to a real API
    confidence: float = 1.0
    
    def __str__(self):
        verified = "✓ CONFIRMED" if self.verified_invalid else "? PATTERN"
        return f"Line {self.line_num} [{verified}]: {self.invalid_api} -> {self.suggested_fix}"


class UnityAPIValidatorV2:
    """
    Ground-truth Unity API validator.
    
    Uses the RAG database whitelist as source of truth,
    with pattern matching as backup for structural errors.
    """
    
    def __init__(self, rag_db_path: str = None, verbose: bool = False):
        self.verbose = verbose
        self.rag: Optional[UnityRAG] = None
        
        # Try to load RAG for ground truth validation
        if HAS_RAG:
            try:
                if rag_db_path is None:
                    rag_db_path = os.path.join(os.path.dirname(__file__), "unity_rag_db")
                self.rag = UnityRAG(db_path=rag_db_path, verbose=verbose)
                if verbose:
                    print(f"Loaded RAG whitelist: {len(self.rag.api_whitelist)} valid APIs")
            except Exception as e:
                if verbose:
                    print(f"Could not load RAG: {e}")
        
        # Build pattern rules for structural errors
        self._build_structural_patterns()
        
        # Cache for API validation results
        self._validation_cache: Dict[str, Tuple[bool, Optional[str], float]] = {}
    
    def _build_structural_patterns(self):
        """Build patterns for structural errors (not API-specific)"""
        
        # Components that can't use 'new'
        self.no_constructor_types = {
            'AudioSource', 'AudioClip', 'AudioListener',
            'Rigidbody', 'Rigidbody2D',
            'Collider', 'BoxCollider', 'SphereCollider', 'CapsuleCollider', 'MeshCollider',
            'Collider2D', 'BoxCollider2D', 'CircleCollider2D', 'PolygonCollider2D',
            'ParticleSystem', 'ParticleSystemRenderer',
            'Light', 'Camera',
            'Animator', 'Animation',
            'Renderer', 'MeshRenderer', 'SkinnedMeshRenderer', 'SpriteRenderer', 
            'LineRenderer', 'TrailRenderer',
            'Canvas', 'CanvasRenderer', 'CanvasGroup',
            'RectTransform',
            'NavMeshAgent', 'NavMeshObstacle',
            'CharacterController',
            'Text', 'Image', 'Button', 'Toggle', 'Slider',
        }
        
        # Methods that should be PascalCase
        self.lowercase_methods = {
            'play': 'Play', 'stop': 'Stop', 'pause': 'Pause',
            'emit': 'Emit', 'clear': 'Clear', 'simulate': 'Simulate',
            'setActive': 'SetActive', 'getComponent': 'GetComponent',
            'addComponent': 'AddComponent', 'addForce': 'AddForce',
        }
        
        # Structural syntax errors (always wrong regardless of API)
        self.syntax_patterns = [
            (r'\.\.(?!\.)', "double_dot", "Double dot '..' - use single dot"),
            (r'\bMathF\s*\.', "wrong_math_class", "Use Mathf not MathF (Unity's class)"),
            (r'\bMathf\s+[A-Z]', "missing_dot", "Missing dot - use Mathf.Method()"),
        ]
    
    def _check_api_against_rag(self, api: str) -> Tuple[bool, Optional[str], float]:
        """
        Check if an API exists in the RAG whitelist.
        
        Returns: (is_valid, nearest_valid_api, similarity_score)
        """
        # Check cache first
        if api in self._validation_cache:
            return self._validation_cache[api]
        
        if not self.rag:
            # No RAG available - can't verify
            return (True, None, 0.0)  # Assume valid if can't check
        
        # Method 1: Exact match in whitelist
        if self.rag.is_valid_api(api):
            result = (True, api, 1.0)
            self._validation_cache[api] = result
            return result
        
        # Method 2: Semantic search for similar APIs
        try:
            is_valid, suggestions = self.rag.validate_api(api, threshold=0.7)
            
            if is_valid:
                result = (True, api, 1.0)
            elif suggestions:
                # Found similar valid API
                nearest, score = suggestions[0]
                result = (False, nearest, score)
            else:
                # Not found at all
                result = (False, None, 0.0)
            
            self._validation_cache[api] = result
            return result
            
        except Exception as e:
            if self.verbose:
                print(f"RAG validation error for {api}: {e}")
            return (True, None, 0.0)  # Assume valid on error
    
    def _extract_apis_from_code(self, code: str) -> List[Tuple[int, str, str]]:
        """
        Extract API calls from code.
        
        Returns: List of (line_num, api_name, full_line)
        """
        apis = []
        lines = code.split('\n')
        
        for line_num, line in enumerate(lines, 1):
            # Skip comments
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
                continue
            
            # Pattern: Type.Method( or Type.Property
            # Captures: ClassName.MemberName
            for match in re.finditer(r'\b([A-Z][a-zA-Z0-9]*)\s*\.\s*([a-zA-Z][a-zA-Z0-9]*)', line):
                class_name = match.group(1)
                member_name = match.group(2)
                api = f"{class_name}.{member_name}"
                
                # Filter out common non-Unity patterns
                if class_name in ('System', 'Console', 'String', 'Math', 'List', 'Dictionary'):
                    continue
                
                apis.append((line_num, api, line.strip()))
        
        return apis
    
    def validate_code(self, code: str) -> List[ValidationIssue]:
        """
        Validate C# code against Unity API ground truth.
        
        Returns list of ValidationIssue objects with:
        - verified_invalid=True if API confirmed not in docs
        - nearest_valid_api if similar API exists
        """
        issues = []
        lines = code.split('\n')
        
        # ================================================================
        # TIER 1: Structural errors (always wrong)
        # ================================================================
        
        for line_num, line in enumerate(lines, 1):
            stripped = line.strip()
            if stripped.startswith('//'):
                continue
            
            # Check invalid constructors
            for comp_type in self.no_constructor_types:
                if re.search(rf'new\s+{comp_type}\s*\(', line):
                    issues.append(ValidationIssue(
                        line_num=line_num,
                        code_snippet=line.strip(),
                        issue_type="invalid_constructor",
                        invalid_api=f"new {comp_type}()",
                        suggested_fix=f"Use AddComponent<{comp_type}>() or assign via Inspector",
                        verified_invalid=True,  # Structurally always wrong
                        confidence=1.0
                    ))
            
            # Check lowercase methods
            for wrong, correct in self.lowercase_methods.items():
                if re.search(rf'\.{wrong}\s*\(', line):
                    issues.append(ValidationIssue(
                        line_num=line_num,
                        code_snippet=line.strip(),
                        issue_type="wrong_case",
                        invalid_api=f".{wrong}()",
                        suggested_fix=f"Use .{correct}() - Unity uses PascalCase",
                        verified_invalid=True,
                        confidence=1.0
                    ))
            
            # Check syntax patterns
            for pattern, issue_type, fix in self.syntax_patterns:
                if re.search(pattern, line):
                    match = re.search(pattern, line)
                    issues.append(ValidationIssue(
                        line_num=line_num,
                        code_snippet=line.strip(),
                        issue_type=issue_type,
                        invalid_api=match.group() if match else pattern,
                        suggested_fix=fix,
                        verified_invalid=True,
                        confidence=1.0
                    ))
        
        # ================================================================
        # TIER 2: API verification against RAG whitelist
        # ================================================================
        
        if self.rag:
            extracted_apis = self._extract_apis_from_code(code)
            
            # Known Unity types to check
            unity_types = {
                'Transform', 'GameObject', 'Component', 'MonoBehaviour',
                'Rigidbody', 'Rigidbody2D', 'Collider', 'Collider2D',
                'AudioSource', 'AudioClip', 'Light', 'Camera',
                'ParticleSystem', 'Animator', 'Animation',
                'Material', 'Renderer', 'MeshRenderer', 'SpriteRenderer',
                'Physics', 'Physics2D', 'Mathf', 'Vector3', 'Vector2',
                'Quaternion', 'Color', 'Time', 'Input', 'Random', 'Debug',
            }
            
            for line_num, api, full_line in extracted_apis:
                class_name = api.split('.')[0]
                
                # Only validate Unity types
                if class_name not in unity_types:
                    continue
                
                # Skip already-detected issues on this line
                if any(i.line_num == line_num and api in i.code_snippet for i in issues):
                    continue
                
                # Check against RAG whitelist
                is_valid, nearest, similarity = self._check_api_against_rag(api)
                
                if not is_valid:
                    if nearest and similarity > 0.7:
                        suggested = f"Did you mean '{nearest}'? (similarity: {similarity:.0%})"
                    else:
                        suggested = f"'{api}' not found in Unity documentation"
                    
                    issues.append(ValidationIssue(
                        line_num=line_num,
                        code_snippet=full_line,
                        issue_type="invalid_api",
                        invalid_api=api,
                        suggested_fix=suggested,
                        verified_invalid=True,  # Confirmed via RAG
                        nearest_valid_api=nearest,
                        similarity_score=similarity,
                        confidence=1.0 if similarity < 0.5 else 0.8
                    ))
        
        # Sort by line number
        issues.sort(key=lambda x: x.line_num)
        
        return issues
    
    def get_validation_stats(self) -> Dict:
        """Get statistics about validation cache"""
        if not self._validation_cache:
            return {"cached": 0, "valid": 0, "invalid": 0}
        
        valid = sum(1 for v in self._validation_cache.values() if v[0])
        invalid = len(self._validation_cache) - valid
        
        return {
            "cached": len(self._validation_cache),
            "valid": valid,
            "invalid": invalid,
        }


# ============================================================================
# COMPARISON TEST
# ============================================================================

def compare_validators():
    """Compare pattern-based vs RAG-based validation"""
    
    test_code = '''
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private ParticleSystem ps;
    private Light light;
    private AudioSource audio;
    
    void Start()
    {
        // These should be detected:
        
        // 1. Invented accessor method (RAG should catch: GetEmission doesn't exist)
        var emission = ps.GetEmission();
        
        // 2. Invented property (RAG should catch: beamHeight doesn't exist)
        light.beamHeight = 10f;
        
        // 3. Invented method (RAG should catch: distance() doesn't exist on Transform)
        float dist = transform.distance(target.position);
        
        // 4. Invalid constructor (structural - always wrong)
        AudioSource newAudio = new AudioSource();
        
        // 5. Wrong case (structural - always wrong)
        audio.play();
        
        // 6. Wrong math class (structural - always wrong)
        float angle = MathF.Sin(0.5f);
        
        // 7. Valid API (should NOT be flagged)
        transform.Translate(Vector3.up);
        ps.Play();
        audio.PlayOneShot(null);
    }
}
'''
    
    print("=" * 70)
    print("VALIDATOR COMPARISON: Pattern-based vs RAG-verified")
    print("=" * 70)
    
    # Test v2 (RAG-based)
    print("\n--- RAG-Verified Validator (Ground Truth) ---")
    try:
        v2 = UnityAPIValidatorV2(verbose=True)
        issues_v2 = v2.validate_code(test_code)
        
        print(f"\nFound {len(issues_v2)} issues:")
        for issue in issues_v2:
            print(f"  {issue}")
            if issue.nearest_valid_api:
                print(f"      Nearest valid: {issue.nearest_valid_api} ({issue.similarity_score:.0%})")
        
        print(f"\nValidation stats: {v2.get_validation_stats()}")
        
    except Exception as e:
        print(f"Error: {e}")
    
    # Compare with v1 (pattern-based)
    print("\n--- Pattern-Based Validator (Original) ---")
    try:
        from unity_api_validator import UnityAPIValidator
        v1 = UnityAPIValidator(verbose=False)
        issues_v1 = v1.validate_code(test_code)
        
        print(f"\nFound {len(issues_v1)} issues:")
        for issue in issues_v1:
            print(f"  {issue}")
            
    except Exception as e:
        print(f"Error: {e}")


if __name__ == "__main__":
    compare_validators()

