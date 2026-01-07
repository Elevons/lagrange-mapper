"""
Unity C# Code Validator using Lagrange Clustering
Validates generated Unity C# code against known API patterns
"""
import re
import json
import numpy as np
from typing import List, Dict, Tuple, Optional
from sentence_transformers import SentenceTransformer
from dataclasses import dataclass


@dataclass
class ValidationResult:
    """Result of C# code validation"""
    is_valid: bool
    invalid_apis: List[Dict]  # [{"api": "Mathf.CeilDesiredValue", "line": 42, "suggestion": "Mathf.Ceil"}]
    invalid_imports: List[str]
    corrected_code: Optional[str] = None
    confidence: float = 1.0


class UnityAPIValidator:
    """Validates Unity C# code using embedding-based clustering"""
    
    def __init__(self, model_name: str = "all-MiniLM-L6-v2"):
        self.model = SentenceTransformer(model_name)
        self.valid_apis = {}  # Loaded from Unity documentation
        self.api_embeddings = None
        self.api_list = []
        
        # Load valid Unity APIs
        self._load_unity_apis()
    
    def _load_unity_apis(self):
        """Load valid Unity API patterns"""
        # You can build this from Unity documentation or your existing dataset
        # For now, hardcode common ones as proof of concept
        self.api_list = [
            # Math
            "Mathf.Ceil", "Mathf.Floor", "Mathf.Round", "Mathf.Clamp",
            "Mathf.Abs", "Mathf.Min", "Mathf.Max", "Mathf.Sqrt",
            "Mathf.Sin", "Mathf.Cos", "Mathf.Tan",
            
            # Physics
            "Physics.OverlapSphere", "Physics.Raycast", "Physics.SphereCast",
            "Rigidbody.AddForce", "Rigidbody.AddExplosionForce", "Rigidbody.AddTorque",
            "Collider.ClosestPoint", "Collider.Raycast",
            
            # GameObject
            "GameObject.Find", "GameObject.FindWithTag", "GameObject.FindGameObjectsWithTag",
            "GameObject.Instantiate", "GameObject.Destroy",
            "GetComponent", "AddComponent", "GetComponentInChildren",
            
            # Transform
            "Transform.Translate", "Transform.Rotate", "Transform.LookAt",
            "Transform.SetParent", "Transform.GetChild",
            
            # Audio
            "AudioSource.Play", "AudioSource.PlayOneShot", "AudioSource.Stop",
            "AudioSource.Pause", "AudioClip.LoadAudioData",
            
            # Lifecycle
            "Start", "Update", "FixedUpdate", "LateUpdate",
            "OnTriggerEnter", "OnTriggerExit", "OnCollisionEnter", "OnCollisionExit",
            "OnEnable", "OnDisable", "Awake",
            
            # Coroutines
            "StartCoroutine", "StopCoroutine", "StopAllCoroutines",
            "InvokeRepeating", "CancelInvoke",
            
            # Common Unity types
            "Vector3", "Vector2", "Quaternion", "Color", "Time",
            "Random", "Debug", "Application", "Screen",
        ]
        
        # Compute embeddings for all valid APIs
        print(f"Computing embeddings for {len(self.api_list)} Unity APIs...")
        self.api_embeddings = self.model.encode(self.api_list)
        print("✓ API database loaded")
    
    def extract_api_calls(self, csharp_code: str) -> List[Tuple[str, int]]:
        """
        Extract API calls from C# code.
        Returns list of (api_call, line_number) tuples.
        """
        api_calls = []
        lines = csharp_code.split('\n')
        
        # Patterns to match Unity API calls
        patterns = [
            r'\b(Mathf\.\w+)',           # Mathf.Something
            r'\b(Physics\.\w+)',          # Physics.Something
            r'\b(Rigidbody\.\w+)',        # Rigidbody.Something
            r'\b(GameObject\.\w+)',       # GameObject.Something
            r'\b(Transform\.\w+)',        # Transform.Something
            r'\b(AudioSource\.\w+)',      # AudioSource.Something
            r'\b(Vector[23]\.\w+)',       # Vector2/Vector3.Something
            r'\b(Quaternion\.\w+)',       # Quaternion.Something
            r'\b(GetComponent\w*)\<',     # GetComponent variants
            r'\b(StartCoroutine|StopCoroutine|InvokeRepeating)',  # Coroutine methods
            r'\bvoid\s+(Start|Update|FixedUpdate|LateUpdate|Awake|OnEnable|OnDisable)\s*\(',  # Lifecycle
            r'\bvoid\s+(OnTriggerEnter|OnTriggerExit|OnCollisionEnter|OnCollisionExit)\s*\(',  # Events
        ]
        
        for line_num, line in enumerate(lines, 1):
            for pattern in patterns:
                matches = re.finditer(pattern, line)
                for match in matches:
                    api_call = match.group(1)
                    api_calls.append((api_call, line_num))
        
        return api_calls
    
    def validate_api(self, api_call: str, threshold: float = 0.75) -> Tuple[bool, Optional[str], float]:
        """
        Validate a single API call against known Unity APIs.
        
        Returns:
            (is_valid, suggested_correction, confidence)
        """
        # Compute embedding for the API call
        query_embedding = self.model.encode([api_call])[0]
        
        # Find closest match in valid APIs
        similarities = np.dot(self.api_embeddings, query_embedding) / (
            np.linalg.norm(self.api_embeddings, axis=1) * np.linalg.norm(query_embedding)
        )
        
        best_idx = np.argmax(similarities)
        best_similarity = similarities[best_idx]
        best_match = self.api_list[best_idx]
        
        # Check if exact match
        if api_call in self.api_list:
            return True, None, 1.0
        
        # Check if close enough to be valid
        if best_similarity >= threshold:
            # Likely a typo or variation - suggest correction
            return False, best_match, best_similarity
        else:
            # Completely invalid API
            return False, best_match, best_similarity
    
    def validate_code(self, csharp_code: str, auto_correct: bool = False) -> ValidationResult:
        """
        Validate entire C# script.
        
        Args:
            csharp_code: The generated C# code
            auto_correct: If True, attempt to automatically correct invalid APIs
            
        Returns:
            ValidationResult with detected issues and optional corrections
        """
        invalid_apis = []
        corrected_code = csharp_code
        
        # Extract and validate all API calls
        api_calls = self.extract_api_calls(csharp_code)
        
        for api_call, line_num in api_calls:
            is_valid, suggestion, confidence = self.validate_api(api_call)
            
            if not is_valid:
                invalid_apis.append({
                    "api": api_call,
                    "line": line_num,
                    "suggestion": suggestion,
                    "confidence": confidence
                })
                
                # Auto-correct if requested and confidence is high
                if auto_correct and confidence > 0.85:
                    corrected_code = corrected_code.replace(api_call, suggestion)
        
        # Validate imports (basic check)
        invalid_imports = self._validate_imports(csharp_code)
        
        return ValidationResult(
            is_valid=len(invalid_apis) == 0 and len(invalid_imports) == 0,
            invalid_apis=invalid_apis,
            invalid_imports=invalid_imports,
            corrected_code=corrected_code if auto_correct else None,
            confidence=1.0 - (len(invalid_apis) / max(len(api_calls), 1))
        )
    
    def _validate_imports(self, csharp_code: str) -> List[str]:
        """Validate using statements"""
        invalid_imports = []
        
        # Valid Unity namespaces
        valid_namespaces = [
            "UnityEngine", "UnityEditor", "System", "System.Collections",
            "System.Collections.Generic", "UnityEngine.UI", "UnityEngine.Events",
            "TMPro", "UnityEngine.SceneManagement", "UnityEngine.AI",
        ]
        
        # Extract using statements
        using_pattern = r'using\s+([\w\.]+)\s*;'
        matches = re.finditer(using_pattern, csharp_code)
        
        for match in matches:
            namespace = match.group(1)
            # Check if it's a valid namespace or a subnamespace of valid ones
            is_valid = any(
                namespace == valid or namespace.startswith(valid + ".")
                for valid in valid_namespaces
            )
            if not is_valid:
                invalid_imports.append(namespace)
        
        return invalid_imports


def test_validator():
    """Test the validator with sample code"""
    
    validator = UnityAPIValidator()
    
    # Sample code with intentional errors
    test_code = """
using UnityEngine;

public class TestScript : MonoBehaviour
{
    void Start()
    {
        // Valid API
        float x = Mathf.Ceil(5.3f);
        
        // Invalid API (hallucination)
        float y = Mathf.CeilDesiredValue(10);
        
        // Valid API
        Physics.OverlapSphere(transform.position, 10f);
    }
}
"""
    
    print("="*60)
    print("TESTING UNITY C# VALIDATOR")
    print("="*60)
    
    result = validator.validate_code(test_code, auto_correct=True)
    
    print(f"\nValidation Result: {'✓ VALID' if result.is_valid else '✗ INVALID'}")
    print(f"Confidence: {result.confidence:.1%}")
    
    if result.invalid_apis:
        print(f"\n⚠️ Found {len(result.invalid_apis)} invalid API calls:")
        for issue in result.invalid_apis:
            print(f"  Line {issue['line']}: {issue['api']}")
            print(f"    → Suggested: {issue['suggestion']} (confidence: {issue['confidence']:.2f})")
    
    if result.invalid_imports:
        print(f"\n⚠️ Found {len(result.invalid_imports)} invalid imports:")
        for imp in result.invalid_imports:
            print(f"  - {imp}")
    
    if result.corrected_code:
        print(f"\n{'='*60}")
        print("CORRECTED CODE:")
        print(f"{'='*60}")
        print(result.corrected_code)


if __name__ == "__main__":
    test_validator()

