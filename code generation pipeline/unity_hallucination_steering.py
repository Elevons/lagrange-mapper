#!/usr/bin/env python3
"""
Unity Hallucination Steering System
====================================
Uses the Lagrange attractor mapping system to steer AWAY from 
known Unity API hallucinations during code generation.

Key insight: LLMs hallucinate CONSISTENTLY - the same fake APIs appear
repeatedly. This means hallucinations form attractors in embedding space
that we can detect and steer away from.

Integrates with:
- attractor_steering.py: Core steering infrastructure
- unity_api_validator.py: Hallucination pattern definitions
- unity_full_pipeline_rag.py: Code generation pipeline

Usage:
    from unity_hallucination_steering import UnityHallucinationSteering
    
    steering = UnityHallucinationSteering()
    result = steering.detect(generated_code)
    
    if result.is_attracted:
        # Regenerate with avoidance prompt
        avoidance = steering.get_avoidance_prompt(result)
"""

import json
import numpy as np
import re
import requests
from pathlib import Path
from typing import Dict, List, Optional, Set
from dataclasses import dataclass, field

# Import validator for hallucination patterns
from unity_api_validator import UnityAPIValidator, ValidationIssue

# ============================================================================
# CONFIGURATION
# ============================================================================

DEFAULT_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
DEFAULT_EMBEDDING_MODEL = "nomic-embed-text"

# Default config path (from build_hallucination_attractors.py)
import os
DEFAULT_CONFIG_DIR = os.path.join(os.path.dirname(__file__), "unity_ir_filter_configs")
DEFAULT_CONFIG_NAME = "unity-hallucination-steering"

# Thresholds (can be overridden by config)
KEYWORD_THRESHOLD = 2.0  # Lower than general attractors - we want to catch early
EMBEDDING_THRESHOLD = 0.72  # Similarity threshold for hallucination centroids

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class HallucinationDetectionResult:
    """Result of hallucination detection"""
    is_attracted: bool = False
    keyword_score: float = 0.0
    embedding_score: float = 0.0
    
    # Specific hallucinations found
    hallucinations: List[ValidationIssue] = field(default_factory=list)
    triggered_categories: List[str] = field(default_factory=list)
    
    # For steering
    nearest_centroid: Optional[str] = None
    avoidance_keywords: List[str] = field(default_factory=list)
    
    def summary(self) -> str:
        if not self.is_attracted:
            return "✓ No hallucinations detected"
        
        parts = [f"⚠️ Hallucination detected (kw={self.keyword_score:.1f}, emb={self.embedding_score:.2f})"]
        
        if self.triggered_categories:
            parts.append(f"  Categories: {', '.join(self.triggered_categories)}")
        
        if self.hallucinations:
            parts.append(f"  Issues ({len(self.hallucinations)}):")
            for h in self.hallucinations[:5]:
                parts.append(f"    - Line {h.line_num}: {h.invalid_api}")
        
        return "\n".join(parts)


# ============================================================================
# HALLUCINATION ATTRACTOR DEFINITIONS
# ============================================================================

# Group hallucinations into semantic categories for centroid generation
HALLUCINATION_CATEGORIES = {
    "particle_system_accessors": {
        "description": "ParticleSystem module access via methods instead of properties",
        "examples": [
            "ParticleSystem.GetEmission()",
            "ps.GetVelocity()",
            "particles.GetColor()",
            "ps.GetMain().startLifetime",
            "emission = ps.GetEmission()",
        ],
        "keywords": [
            "GetEmission", "GetVelocity", "GetColor", "GetMain", "GetLifetime",
            "GetScale", "GetDrift", "GetAudio", "GetForce", "GetNoise",
        ],
    },
    "particle_system_fake_classes": {
        "description": "Invented ParticleSystem helper classes",
        "examples": [
            "ParticleSystem.EmissionConfig emission",
            "ParticleSystemVelocity velocity",
            "ParticleSystemLifetimeConfig lifetime",
            "ParticleSystemColorConfig color",
            "ParticleSystemDriftConfig drift",
            "ParticleSystemScaleConfig scale",
        ],
        "keywords": [
            "EmissionConfig", "VelocityConfig", "LifetimeConfig", "ColorConfig",
            "DriftConfig", "ScaleConfig", "AudioConfig", "ParticleSystemVelocity",
            "ParticleSystemLifetime", "ParticleSystemColor", "ParticleSystemDrift",
        ],
    },
    "particle_system_wrong_properties": {
        "description": "Wrong property locations on ParticleSystem",
        "examples": [
            "emission.maxParticles = 100",
            "emission.startPos[0] = pos",
            "velocity.startSpeed[i] = speed",
            "ps.main.startParticles",
        ],
        "keywords": [
            "emission.maxParticles", "emission.startPos", "velocity.startSpeed",
            "startParticles", "emission.startSize", "emission.startSpeed",
        ],
    },
    "light_fake_properties": {
        "description": "Non-existent Light component properties",
        "examples": [
            "light.beamHeight = 10f",
            "light.beamDirection = Vector3.forward",
            "light.target = player.transform",
            "light.position = Vector3.zero",
        ],
        "keywords": [
            "beamHeight", "beamDirection", "Light.target", "Light.position",
            "light.beam", "beamWidth", "beamAngle",
        ],
    },
    "transform_fake_methods": {
        "description": "Non-existent Transform methods and properties",
        "examples": [
            "transform.distance(other.position)",
            "transform.distanceTo(target)",
            "position.distanceTo(other)",
            "transform.enabled = false",
            "transform.isPlaying",
            "transform.color = Color.red",
        ],
        "keywords": [
            "transform.distance", "distanceTo", "transform.enabled",
            "transform.isPlaying", "transform.color", "transform.active",
        ],
    },
    "invalid_constructors": {
        "description": "Using 'new' on Unity components",
        "examples": [
            "AudioSource clip = new AudioSource()",
            "ParticleSystem ps = new ParticleSystem()",
            "Light light = new Light()",
            "Rigidbody rb = new Rigidbody()",
            "new AudioClip()",
        ],
        "keywords": [
            "new AudioSource", "new ParticleSystem", "new Light", "new Rigidbody",
            "new Collider", "new Camera", "new Animator", "new AudioClip",
        ],
    },
    "math_confusion": {
        "description": "MathF vs Mathf confusion and syntax errors",
        "examples": [
            "MathF.Sin(angle)",
            "MathF.Cos(0.5f)",
            "Mathf Sin(angle)",
            "System.MathF.Sqrt(x)",
        ],
        "keywords": [
            "MathF.", "MathF Sin", "MathF Cos", "Mathf Sin", "Mathf Cos",
            "System.MathF", "System.Math.Sin",
        ],
    },
    "quaternion_confusion": {
        "description": "Invalid Quaternion syntax",
        "examples": [
            "Quaternion.Rotation * direction",
            "direction.Rotation",
            ".Rotation * transform.forward",
        ],
        "keywords": [
            "Quaternion.Rotation", ".Rotation *", "direction.Rotation",
        ],
    },
    "wrong_case_methods": {
        "description": "Lowercase Unity methods (should be PascalCase)",
        "examples": [
            "audioSource.play()",
            "ps.stop()",
            "rb.addForce()",
            "gameObject.setActive(false)",
        ],
        "keywords": [
            ".play()", ".stop()", ".pause()", ".emit()", ".clear()",
            ".addForce()", ".addTorque()", ".setActive()", ".getComponent(",
        ],
    },
}


# ============================================================================
# STEERING CLASS
# ============================================================================

class UnityHallucinationSteering:
    """
    Steering system for Unity API hallucinations using Lagrange attractor centroids.
    
    Uses keyword detection + embedding similarity to detect when
    generated code is drifting toward known hallucination patterns.
    
    Centroids can be:
    1. Loaded from pre-computed config (fast, recommended)
    2. Computed from hardcoded examples (fallback)
    
    To build centroids from your model's actual outputs:
        python build_hallucination_attractors.py --full
    """
    
    def __init__(
        self,
        config_dir: str = DEFAULT_CONFIG_DIR,
        config_name: str = DEFAULT_CONFIG_NAME,
        embedding_url: str = DEFAULT_EMBEDDING_URL,
        embedding_model: str = DEFAULT_EMBEDDING_MODEL,
        verbose: bool = False
    ):
        self.embedding_url = embedding_url
        self.embedding_model = embedding_model
        self.verbose = verbose
        
        # Initialize validator for pattern matching
        self.validator = UnityAPIValidator(verbose=verbose)
        
        # Cache for embeddings
        self._embedding_cache = {}
        
        # Try to load pre-computed config
        self._config = None
        self._centroids = None
        self._categories = HALLUCINATION_CATEGORIES  # Default to hardcoded
        
        config_loaded = self._load_config(config_dir, config_name)
        
        if config_loaded:
            if verbose:
                print(f"Loaded {len(self._centroids)} pre-computed hallucination centroids")
        else:
            if verbose:
                print("Using hardcoded hallucination patterns (run build_hallucination_attractors.py for better results)")
        
        # Build keyword index (from config or hardcoded)
        self._build_keyword_index()
    
    def _load_config(self, config_dir: str, config_name: str) -> bool:
        """
        Load pre-computed hallucination centroids from config file.
        
        Returns True if loaded successfully, False to use fallback.
        """
        config_path = Path(config_dir) / config_name / "filter_config.json"
        
        if not config_path.exists():
            if self.verbose:
                print(f"No pre-computed config at {config_path}")
            return False
        
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                self._config = json.load(f)
            
            # Extract settings
            settings = self._config.get("settings", {})
            self.keyword_threshold = settings.get("keyword_threshold", KEYWORD_THRESHOLD)
            self.embedding_threshold = settings.get("embedding_threshold", EMBEDDING_THRESHOLD)
            
            # Load pre-computed centroids
            self._centroids = {}
            self._categories = {}
            
            for attractor in self._config.get("attractors", []):
                name = attractor.get("name", "")
                if not name:
                    continue
                
                # Load centroid if available
                centroid_list = attractor.get("centroid")
                if centroid_list:
                    centroid = np.array(centroid_list, dtype=np.float32)
                    # Normalize
                    norm = np.linalg.norm(centroid)
                    if norm > 0:
                        centroid = centroid / norm
                    self._centroids[name] = centroid
                
                # Build category data for keyword index
                self._categories[name] = {
                    "description": attractor.get("description", ""),
                    "examples": attractor.get("sample_outputs", []),
                    "keywords": attractor.get("keywords", []),
                }
            
            if self.verbose:
                print(f"Loaded config: {len(self._centroids)} centroids, {len(self._categories)} categories")
            
            return len(self._centroids) > 0
            
        except Exception as e:
            if self.verbose:
                print(f"Error loading config: {e}")
            return False
    
    def _build_keyword_index(self):
        """Build keyword → category index for fast detection"""
        self.keyword_index: Dict[str, List[str]] = {}
        
        for category, data in self._categories.items():
            for keyword in data.get("keywords", []):
                keyword_lower = keyword.lower()
                if keyword_lower not in self.keyword_index:
                    self.keyword_index[keyword_lower] = []
                self.keyword_index[keyword_lower].append(category)
    
    def _get_embedding(self, text: str) -> Optional[np.ndarray]:
        """Get embedding for text (with caching)"""
        cache_key = hash(text[:500])
        if cache_key in self._embedding_cache:
            return self._embedding_cache[cache_key]
        
        try:
            response = requests.post(
                self.embedding_url,
                json={"model": self.embedding_model, "input": text},
                timeout=30
            )
            
            if response.status_code == 200:
                vec = np.array(response.json()["data"][0]["embedding"], dtype=np.float32)
                vec = vec / np.linalg.norm(vec)
                self._embedding_cache[cache_key] = vec
                return vec
        except Exception as e:
            if self.verbose:
                print(f"Embedding error: {e}")
        
        return None
    
    def _compute_centroids(self) -> Dict[str, np.ndarray]:
        """
        Get centroids for hallucination categories.
        
        Returns pre-loaded centroids if available, otherwise computes
        from hardcoded examples (slower, first-time only).
        """
        # Return pre-loaded centroids if available
        if self._centroids is not None and len(self._centroids) > 0:
            return self._centroids
        
        # Fallback: compute from hardcoded examples
        if self.verbose:
            print("Computing hallucination centroids from hardcoded examples...")
            print("  (Run build_hallucination_attractors.py for faster startup)")
        
        centroids = {}
        
        for category, data in self._categories.items():
            # Embed all examples for this category
            embeddings = []
            for example in data.get("examples", []):
                emb = self._get_embedding(example)
                if emb is not None:
                    embeddings.append(emb)
            
            if embeddings:
                # Compute mean centroid
                centroid = np.mean(embeddings, axis=0)
                centroid = centroid / np.linalg.norm(centroid)
                centroids[category] = centroid
                
                if self.verbose:
                    print(f"  {category}: {len(embeddings)} examples")
        
        self._centroids = centroids
        return centroids
    
    def detect(
        self,
        code: str,
        use_embeddings: bool = True,
        keyword_threshold: Optional[float] = None,
        embedding_threshold: Optional[float] = None,
    ) -> HallucinationDetectionResult:
        """
        Detect hallucinations in generated Unity C# code.
        
        Uses two detection methods:
        1. Keyword matching against known hallucination patterns
        2. Embedding similarity to hallucination category centroids (Lagrange)
        
        Args:
            code: Generated C# code to analyze
            use_embeddings: Whether to use embedding-based detection
            keyword_threshold: Minimum keyword score to trigger (uses config or default)
            embedding_threshold: Minimum embedding similarity to trigger (uses config or default)
        
        Returns:
            HallucinationDetectionResult with detection details
        """
        # Use instance thresholds (from config) or defaults
        kw_threshold = keyword_threshold if keyword_threshold is not None else getattr(self, 'keyword_threshold', KEYWORD_THRESHOLD)
        emb_threshold = embedding_threshold if embedding_threshold is not None else getattr(self, 'embedding_threshold', EMBEDDING_THRESHOLD)
        
        result = HallucinationDetectionResult()
        
        code_lower = code.lower()
        
        # ========================================
        # METHOD 1: Strict Pattern Validation
        # ========================================
        validation_issues = self.validator.validate_code(code)
        high_confidence = [i for i in validation_issues if i.confidence >= 0.8]
        
        result.hallucinations = high_confidence
        
        # Group by issue type
        issue_types = set(i.issue_type for i in high_confidence)
        result.triggered_categories.extend(list(issue_types))
        
        # Extract keywords to avoid
        for issue in high_confidence:
            result.avoidance_keywords.append(issue.invalid_api)
        
        # ========================================
        # METHOD 2: Keyword Detection
        # ========================================
        category_scores: Dict[str, float] = {}
        
        for keyword, categories in self.keyword_index.items():
            # Count occurrences
            if len(keyword.split()) == 1:
                pattern = r'\b' + re.escape(keyword) + r'\b'
                matches = len(re.findall(pattern, code_lower))
            else:
                matches = code_lower.count(keyword)
            
            if matches > 0:
                for category in categories:
                    category_scores[category] = category_scores.get(category, 0) + matches
                    if category not in result.triggered_categories:
                        result.triggered_categories.append(category)
        
        result.keyword_score = sum(category_scores.values())
        
        # ========================================
        # METHOD 3: Embedding Similarity
        # ========================================
        if use_embeddings:
            centroids = self._compute_centroids()
            
            if centroids:
                code_emb = self._get_embedding(code[:2000])  # Limit for embedding
                
                if code_emb is not None:
                    best_sim = 0.0
                    best_category = None
                    
                    for category, centroid in centroids.items():
                        similarity = float(np.dot(code_emb, centroid))
                        if similarity > best_sim:
                            best_sim = similarity
                            best_category = category
                    
                    result.embedding_score = best_sim
                    result.nearest_centroid = best_category
                    
                    if best_sim > emb_threshold:
                        if best_category not in result.triggered_categories:
                            result.triggered_categories.append(f"emb:{best_category}")
        
        # ========================================
        # FINAL DETERMINATION
        # ========================================
        result.is_attracted = (
            len(result.hallucinations) > 0 or
            result.keyword_score >= kw_threshold or
            result.embedding_score >= emb_threshold
        )
        
        return result
    
    def get_avoidance_prompt(self, result: HallucinationDetectionResult) -> str:
        """
        Generate a steering prompt to avoid detected hallucinations.
        
        This prompt can be prepended to the regeneration request to
        help the model avoid the same mistakes.
        """
        parts = ["\n\n⚠️ CRITICAL - AVOID THESE UNITY API ERRORS:\n"]
        
        # List specific hallucinations with fixes
        if result.hallucinations:
            for h in result.hallucinations[:8]:
                parts.append(f"❌ {h.invalid_api} → {h.suggested_fix}")
        
        # Category-specific warnings
        if "particle_system_accessors" in result.triggered_categories:
            parts.append("\nPARTICLESYSTEM: Access modules as PROPERTIES, not methods:")
            parts.append("  ❌ ps.GetEmission() → ✅ ps.emission")
            parts.append("  ❌ ps.GetMain() → ✅ ps.main")
            parts.append("  ❌ ps.GetVelocity() → ✅ ps.velocityOverLifetime")
        
        if "particle_system_fake_classes" in result.triggered_categories:
            parts.append("\nPARTICLESYSTEM: Use real module classes:")
            parts.append("  ❌ ParticleSystem.EmissionConfig → ✅ ParticleSystem.EmissionModule")
            parts.append("  ❌ ParticleSystemVelocity → ✅ ParticleSystem.VelocityOverLifetimeModule")
        
        if "light_fake_properties" in result.triggered_categories:
            parts.append("\nLIGHT: These properties don't exist:")
            parts.append("  ❌ light.beamHeight, light.beamDirection, light.target")
            parts.append("  ✅ Use: spotAngle, transform.forward, transform.LookAt()")
        
        if "transform_fake_methods" in result.triggered_categories:
            parts.append("\nTRANSFORM: These don't exist:")
            parts.append("  ❌ transform.distance() → ✅ Vector3.Distance(a, b)")
            parts.append("  ❌ transform.enabled → ✅ gameObject.SetActive(bool)")
        
        if "invalid_constructors" in result.triggered_categories:
            parts.append("\nCOMPONENTS: Can't use 'new' on Unity components:")
            parts.append("  ❌ new AudioSource() → ✅ gameObject.AddComponent<AudioSource>()")
        
        if "math_confusion" in result.triggered_categories:
            parts.append("\nMATH: Use Unity's Mathf class:")
            parts.append("  ❌ MathF.Sin() → ✅ Mathf.Sin()")
            parts.append("  ❌ Mathf Sin() → ✅ Mathf.Sin() (don't forget the dot!)")
        
        if "wrong_case_methods" in result.triggered_categories:
            parts.append("\nMETHOD CASE: Unity uses PascalCase:")
            parts.append("  ❌ .play() → ✅ .Play()")
            parts.append("  ❌ .addForce() → ✅ .AddForce()")
        
        parts.append("\nGenerate code using ONLY correct Unity APIs from the documentation.")
        
        return "\n".join(parts)
    
    def get_negative_examples_prompt(self, max_examples: int = 5) -> str:
        """
        Generate a prompt section showing negative examples.
        
        This can be added to the system prompt to proactively prevent
        hallucinations before they occur.
        """
        parts = ["COMMON MISTAKES TO AVOID:\n"]
        
        # Select diverse examples from different categories
        example_count = 0
        for category, data in HALLUCINATION_CATEGORIES.items():
            if example_count >= max_examples:
                break
            
            examples = data.get("examples", [])
            if examples:
                parts.append(f"❌ {examples[0]}")
                example_count += 1
        
        # Add correct patterns
        parts.append("\nCORRECT PATTERNS:")
        parts.append("✅ var emission = ps.emission; (property access)")
        parts.append("✅ emission.rateOverTime = value; (set module properties)")
        parts.append("✅ float dist = Vector3.Distance(a, b);")
        parts.append("✅ gameObject.SetActive(false);")
        parts.append("✅ gameObject.AddComponent<AudioSource>();")
        
        return "\n".join(parts)


# ============================================================================
# INTEGRATION HELPER
# ============================================================================

def create_steered_code_prompt(
    ir_json: Dict,
    rag_context: str,
    hallucination_result: Optional[HallucinationDetectionResult] = None,
    steering: Optional[UnityHallucinationSteering] = None,
) -> str:
    """
    Create a code generation prompt with hallucination steering.
    
    Args:
        ir_json: The IR specification
        rag_context: RAG-retrieved Unity documentation
        hallucination_result: Optional previous detection result
        steering: Optional steering instance for negative examples
    
    Returns:
        Complete prompt string with steering elements
    """
    parts = []
    
    # Add proactive negative examples if no prior result
    if steering and not hallucination_result:
        parts.append(steering.get_negative_examples_prompt())
        parts.append("")
    
    # Add avoidance prompt if previous hallucinations detected
    if hallucination_result and hallucination_result.is_attracted:
        parts.append(steering.get_avoidance_prompt(hallucination_result))
        parts.append("")
    
    # Add RAG context
    if rag_context:
        parts.append("UNITY API DOCUMENTATION:")
        parts.append(rag_context)
        parts.append("")
    
    # Add IR specification
    parts.append("BEHAVIOR SPECIFICATION:")
    parts.append(json.dumps(ir_json, indent=2))
    parts.append("")
    
    parts.append("Generate the complete Unity C# MonoBehaviour script using ONLY correct APIs:")
    
    return "\n".join(parts)


# ============================================================================
# CLI TEST
# ============================================================================

def test_steering():
    """Test the hallucination steering system"""
    
    test_code = '''
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private ParticleSystem ps;
    private Light light;
    
    void Start()
    {
        // These should be detected:
        ParticleSystem.EmissionConfig emission = ps.GetEmission();
        emission.maxParticles = 100;
        
        light.beamHeight = 10f;
        float dist = transform.distance(target.position);
        
        AudioSource audio = new AudioSource();
        audio.play();
        
        float angle = MathF.Sin(0.5f);
    }
}
'''
    
    print("=" * 60)
    print("Unity Hallucination Steering Test")
    print("=" * 60)
    
    steering = UnityHallucinationSteering(verbose=True)
    
    print("\nAnalyzing test code...")
    result = steering.detect(test_code)
    
    print(f"\n{result.summary()}")
    
    if result.is_attracted:
        print("\n" + "=" * 60)
        print("AVOIDANCE PROMPT:")
        print("=" * 60)
        print(steering.get_avoidance_prompt(result))
        
        print("\n" + "=" * 60)
        print("NEGATIVE EXAMPLES PROMPT:")
        print("=" * 60)
        print(steering.get_negative_examples_prompt())


if __name__ == "__main__":
    test_steering()

