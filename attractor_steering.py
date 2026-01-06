#!/usr/bin/env python3
"""
Attractor Steering System

Loads filter configuration and provides runtime steering for LLM outputs.
Part of the LLM Attractor Mapping Pipeline.

Intensity-based filtering (0-1 scale):
  - intensity=0.0: no filtering
  - intensity=0.3: filter top 30% of attractors (most dominant only)
  - intensity=1.0: filter all detected attractors

Usage:
    # As CLI tool:
    python attractor_steering.py <model_name> <test_text> [--intensity 0.5]
    
    # As module:
    from attractor_steering import load_steering
    steering = load_steering("my-model")
    result = steering.detect(text, intensity=0.5)
"""

import json
import numpy as np
import re
from pathlib import Path
from typing import Dict, List, Tuple, Optional, Set, Callable
from dataclasses import dataclass, field
import requests

# ============================================================================
# CONFIGURATION
# ============================================================================

DEFAULT_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
DEFAULT_EMBEDDING_MODEL = "nomic-embed-text"
DEFAULT_CONFIG_DIR = "filter_configs"

# Generic prompts for steering away from attractors
FORCED_ALTERNATIVES = [
    "Consider solutions that don't involve technology at all.",
    "What would a 19th century solution look like?",
    "How would this be solved using only legal/regulatory mechanisms?",
    "What if the solution required no coordination - only individual action?",
    "Propose something that sounds boring but would actually work.",
    "How would your grandparents have solved this?",
    "What's the simplest possible intervention?",
    "Consider only solutions using existing institutions.",
]

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class SteeringConfig:
    """Configuration for attractor steering"""
    model_name: str
    attractors: List[Dict]  # Ordered by dominance (rank 0 = most dominant)
    total_attractors: int
    keyword_threshold: float = 3.0
    embedding_threshold: float = 0.75
    hedge_embedding_threshold: float = 0.70  # Lower threshold for hedge detection
    code_leak_embedding_threshold: float = 0.70  # For code leak detection
    structural_match_threshold: float = 0.7  # For structural cliché matching
    default_intensity: float = 0.5
    max_regeneration_attempts: int = 3
    embedding_url: str = DEFAULT_EMBEDDING_URL
    embedding_model: str = DEFAULT_EMBEDDING_MODEL
    # All attractor keywords (for topic-based exemption)
    all_keywords: List[str] = field(default_factory=list)
    # Baseline settings
    baselines_enabled: bool = False
    baseline_cache_dir: str = "baseline_cache"
    baseline_tolerance: Dict = field(default_factory=lambda: {
        "fields": 0.5,
        "behaviors": 0.5,
        "states": 0.5,
    })
    
    @classmethod
    def load(cls, config_path: str) -> 'SteeringConfig':
        """Load configuration from file"""
        with open(config_path, 'r') as f:
            data = json.load(f)
        
        settings = data.get('settings', {})
        attractors = data.get('attractors', [])
        
        # Handle both old (dict) and new (list) formats
        if isinstance(attractors, dict):
            # Convert old format to new
            attractors = [
                {"name": name, "rank": i, **attractor_data}
                for i, (name, attractor_data) in enumerate(attractors.items())
            ]
        
        baselines = data.get('baselines', {})
        
        return cls(
            model_name=data.get('model_name', 'unknown'),
            attractors=attractors,
            total_attractors=data.get('total_attractors', len(attractors)),
            keyword_threshold=settings.get('keyword_threshold', 3.0),
            embedding_threshold=settings.get('embedding_threshold', 0.75),
            hedge_embedding_threshold=settings.get('hedge_embedding_threshold', 0.70),
            code_leak_embedding_threshold=settings.get('code_leak_embedding_threshold', 0.70),
            structural_match_threshold=settings.get('structural_match_threshold', 0.7),
            default_intensity=settings.get('default_intensity', 0.5),
            max_regeneration_attempts=settings.get('max_regeneration_attempts', 3),
            # All attractor keywords (for topic-based exemption)
            all_keywords=data.get('all_keywords', []),
            baselines_enabled=baselines.get('enabled', False),
            baseline_cache_dir=baselines.get('cache_dir', 'baseline_cache'),
            baseline_tolerance=baselines.get('tolerance', {"fields": 0.5, "behaviors": 0.5, "states": 0.5})
        )


@dataclass
class DetectionResult:
    """Result of unified attractor detection"""
    is_attracted: bool = False
    intensity_used: float = 0.5
    attractors_checked: int = 0
    
    # Code leak detection
    keyword_score: float = 0.0
    embedding_score: float = 0.0
    flagged_keywords: List[str] = field(default_factory=list)
    
    # Structural cliché detection
    structural_cliches_matched: List[str] = field(default_factory=list)
    
    # Baseline deviation detection
    baseline_found: bool = False
    over_engineering_score: float = 0.0
    under_engineering_score: float = 0.0
    baseline_violations: List[str] = field(default_factory=list)
    
    # Combined
    triggered_attractors: List[str] = field(default_factory=list)
    nearest_attractor: Optional[str] = None
    
    def merge(self, other: 'DetectionResult'):
        """Merge another detection result into this one"""
        if other.is_attracted:
            self.is_attracted = True
        self.keyword_score = max(self.keyword_score, other.keyword_score)
        self.embedding_score = max(self.embedding_score, other.embedding_score)
        self.flagged_keywords.extend(other.flagged_keywords)
        self.structural_cliches_matched.extend(other.structural_cliches_matched)
        self.baseline_violations.extend(other.baseline_violations)
        self.over_engineering_score = max(self.over_engineering_score, other.over_engineering_score)
        self.under_engineering_score = max(self.under_engineering_score, other.under_engineering_score)
        self.triggered_attractors.extend(other.triggered_attractors)
        if other.baseline_found:
            self.baseline_found = True
    
    def summary(self) -> str:
        """Human-readable summary"""
        if not self.is_attracted:
            return f"✓ No attractor match (checked {self.attractors_checked})"
        
        parts = [f"⚠️ Attractor match (intensity={self.intensity_used:.1f})"]
        
        if self.triggered_attractors:
            parts.append(f"  Triggered: {', '.join(self.triggered_attractors)}")
        
        if self.flagged_keywords:
            parts.append(f"  Code leaks: {', '.join(self.flagged_keywords[:5])}")
        
        if self.structural_cliches_matched:
            parts.append(f"  Structural clichés: {', '.join(self.structural_cliches_matched)}")
        
        if self.baseline_violations:
            parts.append(f"  Baseline violations: {', '.join(self.baseline_violations)}")
        
        if self.over_engineering_score > 0.2:
            parts.append(f"  Over-engineering: {self.over_engineering_score:.2f}")
        
        if self.under_engineering_score > 0.2:
            parts.append(f"  Under-engineering: {self.under_engineering_score:.2f}")
        
        return "\n".join(parts)
    
    def to_dict(self) -> Dict:
        """Convert to dictionary for logging"""
        return {
            "is_attracted": self.is_attracted,
            "intensity_used": self.intensity_used,
            "attractors_checked": self.attractors_checked,
            "keyword_score": self.keyword_score,
            "embedding_score": self.embedding_score,
            "triggered_attractors": self.triggered_attractors,
            "flagged_keywords": self.flagged_keywords,
            "nearest_attractor": self.nearest_attractor
        }


# ============================================================================
# CORE STEERING CLASS
# ============================================================================

class AttractorSteering:
    """Main steering system with unified attractor detection"""
    
    def __init__(self, config: SteeringConfig):
        self.config = config
        self.centroids = self._load_centroids()
        self.baseline_cache = self._load_baseline_cache()
        self._embedding_cache = {}
    
    def _load_centroids(self) -> Dict[str, np.ndarray]:
        """Load centroid vectors for embedding comparison"""
        centroids = {}
        for attractor in self.config.attractors:
            if 'centroid' in attractor and attractor['centroid']:
                vec = np.array(attractor['centroid'])
                norm = np.linalg.norm(vec)
                if norm > 0:
                    vec = vec / norm
                centroids[attractor['name']] = vec
        return centroids
    
    def _load_baseline_cache(self) -> Dict[str, Dict]:
        """Load pre-generated baselines from cache"""
        cache = {}
        
        if not self.config.baselines_enabled:
            return cache
        
        from pathlib import Path
        cache_dir = Path(self.config.baseline_cache_dir)
        if not cache_dir.exists():
            return cache
        
        for file in cache_dir.glob("*.json"):
            try:
                with open(file) as f:
                    data = json.load(f)
                    cache[data["prompt_hash"]] = data
            except:
                continue
        
        return cache
    
    def _get_active_attractors(self, intensity: float) -> List[Dict]:
        """Get attractors to check based on intensity (0-1)"""
        if intensity <= 0:
            return []
        
        # intensity=1.0 means check all, intensity=0.5 means check top 50%
        n_to_check = max(1, int(len(self.config.attractors) * intensity))
        
        # Attractors are already sorted by dominance (rank 0 = most dominant)
        return self.config.attractors[:n_to_check]
    
    def _build_keyword_index(self, attractors: List[Dict]) -> Dict[str, List[str]]:
        """Build keyword index for active attractors only"""
        index = {}
        for attractor in attractors:
            for keyword in attractor.get('keywords', []):
                keyword_lower = keyword.lower()
                if keyword_lower not in index:
                    index[keyword_lower] = []
                index[keyword_lower].append(attractor['name'])
        return index
    
    def get_embedding(self, text: str) -> Optional[np.ndarray]:
        """Get embedding vector for text (with caching)"""
        cache_key = hash(text[:500])
        if cache_key in self._embedding_cache:
            return self._embedding_cache[cache_key]
        
        try:
            response = requests.post(
                self.config.embedding_url,
                headers={"Content-Type": "application/json"},
                json={
                    "model": self.config.embedding_model,
                    "input": text
                },
                timeout=30
            )
            
            if response.status_code == 200:
                vec = np.array(response.json()['data'][0]['embedding'])
                vec = vec / np.linalg.norm(vec)
                self._embedding_cache[cache_key] = vec
                return vec
        except Exception:
            pass
        
        return None
    
    def detect(
        self, 
        text: str, 
        exempted_keywords: Set[str] = None,
        intensity: float = None,
        use_embeddings: bool = True
    ) -> DetectionResult:
        """
        Detect if text is attracted to any attractor.
        
        Args:
            text: Text to analyze
            exempted_keywords: Topic-relevant keywords to ignore (e.g., if discussing 
                              "blockchain governance", exempt "blockchain")
            intensity: 0-1 scale for filtering aggressiveness
                      0.0 = no filtering
                      0.5 = filter top 50% of attractors
                      1.0 = filter all attractors
            use_embeddings: Whether to use embedding similarity
        
        Returns:
            DetectionResult with scores and triggered attractors
        """
        if intensity is None:
            intensity = self.config.default_intensity
        
        intensity = max(0.0, min(1.0, intensity))  # Clamp to 0-1
        exempted = {k.lower() for k in (exempted_keywords or set())}
        
        result = DetectionResult(intensity_used=intensity)
        
        # Get active attractors based on intensity
        active_attractors = self._get_active_attractors(intensity)
        result.attractors_checked = len(active_attractors)
        
        if not active_attractors:
            return result  # No filtering
        
        text_lower = text.lower()
        
        # Separate hedge attractors (embedding-only) from regular attractors
        hedge_attractors = [a for a in active_attractors if a.get('type') == 'hedge_centroid']
        regular_attractors = [a for a in active_attractors if a.get('type') != 'hedge_centroid']
        
        # ========================================
        # KEYWORD DETECTION (regular attractors only)
        # ========================================
        keyword_index = self._build_keyword_index(regular_attractors)
        attractor_scores = {}
        
        for keyword, attractor_names in keyword_index.items():
            # Skip exempted keywords
            if keyword in exempted:
                continue
            
            # Count occurrences
            if len(keyword.split()) == 1:
                pattern = r'\b' + re.escape(keyword) + r'\b'
                matches = len(re.findall(pattern, text_lower))
            else:
                matches = text_lower.count(keyword)
            
            if matches > 0:
                result.flagged_keywords.append(keyword)
                for name in attractor_names:
                    attractor_scores[name] = attractor_scores.get(name, 0) + matches
        
        result.keyword_score = sum(attractor_scores.values())
        
        # Track triggered attractors (from keywords)
        for name, score in attractor_scores.items():
            if score >= self.config.keyword_threshold / 2:
                result.triggered_attractors.append(name)
        
        # ========================================
        # EMBEDDING DETECTION
        # ========================================
        hedge_triggered = False
        
        if use_embeddings and self.centroids:
            emb = self.get_embedding(text)
            
            if emb is not None:
                # Check hedge attractors first (embedding-only, lower threshold)
                hedge_threshold = getattr(self.config, 'hedge_embedding_threshold', 0.70)
                for hedge in hedge_attractors:
                    if hedge['name'] in self.centroids:
                        similarity = float(np.dot(emb, self.centroids[hedge['name']]))
                        if similarity > hedge_threshold:
                            hedge_triggered = True
                            result.embedding_score = max(result.embedding_score, similarity)
                            result.nearest_attractor = hedge['name']
                            if hedge['name'] not in result.triggered_attractors:
                                result.triggered_attractors.append(f"HEDGE:{hedge['name']}")
                
                # Check regular attractors
                active_names = {a['name'] for a in regular_attractors}
                active_centroids = {
                    name: centroid 
                    for name, centroid in self.centroids.items() 
                    if name in active_names
                }
                
                if active_centroids:
                    best_similarity = 0
                    best_attractor = None
                    
                    for name, centroid in active_centroids.items():
                        similarity = float(np.dot(emb, centroid))
                        if similarity > best_similarity:
                            best_similarity = similarity
                            best_attractor = name
                    
                    if best_similarity > result.embedding_score:
                        result.embedding_score = best_similarity
                        result.nearest_attractor = best_attractor
                    
                    if best_similarity > self.config.embedding_threshold:
                        if best_attractor not in result.triggered_attractors:
                            result.triggered_attractors.append(best_attractor)
        
        # ========================================
        # FINAL DETERMINATION
        # ========================================
        result.is_attracted = (
            result.keyword_score >= self.config.keyword_threshold or
            result.embedding_score >= self.config.embedding_threshold or
            hedge_triggered  # Hedge detection via embedding similarity
        )
        
        return result
    
    def get_attractor_info(self) -> List[Dict]:
        """Get info about all attractors (for display)"""
        return [
            {
                "rank": a.get('rank', i),
                "name": a['name'],
                "percentage": a.get('percentage', 0),
                "top_keywords": a.get('keywords', [])[:5]
            }
            for i, a in enumerate(self.config.attractors)
        ]
    
    def get_avoidance_prompt(self, result: DetectionResult) -> str:
        """Generate prompt to steer away from detected attractors"""
        import random
        
        parts = ["\n\nCRITICAL: Your response matched known attractor patterns."]
        
        # List keywords to avoid
        if result.flagged_keywords:
            avoid = list(set(result.flagged_keywords[:6]))
            parts.append(f"DO NOT use these concepts: {', '.join(avoid)}")
        
        # Add a random forcing alternative
        parts.append(random.choice(FORCED_ALTERNATIVES))
        
        return "\n".join(parts)
    
    def analyze_topic(self, topic: str) -> Set[str]:
        """
        Analyze a topic and return keywords to exempt.
        
        Simple logic: any attractor keyword that appears in the topic
        is exempted from detection (since it's topic-relevant).
        
        Args:
            topic: The discussion topic
            
        Returns:
            Set of keywords to exempt
        """
        topic_lower = topic.lower()
        topic_words = set(topic_lower.split())
        
        # Exempt any attractor keyword that appears in the topic
        exempted = set()
        for keyword in self.config.all_keywords:
            if keyword in topic_lower:
                exempted.add(keyword)
        
        return exempted
    
    def get_topic_info(self, topic: str) -> Dict:
        """Get topic analysis for display"""
        exempted = self.analyze_topic(topic)
        
        return {
            "topic": topic,
            "exempted_keywords": sorted(exempted)
        }
    
    def detect_code_leak(self, json_response: str, intensity: float = 0.5) -> DetectionResult:
        """
        Detect code leak patterns in generated Unity IR JSON.
        
        Uses two methods:
        1. Pattern matching against CODE_LEAK_PATTERNS
        2. Embedding similarity to code_leak centroid
        
        Args:
            json_response: The JSON response to analyze
            intensity: Detection intensity (0-1)
        
        Returns:
            DetectionResult with code leak detection scores
        """
        result = DetectionResult(intensity_used=intensity)
        
        # Pattern-based detection
        CODE_LEAK_PATTERNS = {
            "operators": [r"==|!=|<=|>=|&&|\|\||!", r"\+|\-|\*|/|%", r"<|>"],
            "unity_api": [r"Vector3\.", r"Time\.(deltaTime|time)", r"GameObject\.", r"Transform\.", r"Rigidbody\.", r"Collider\.", r"Input\.(GetKey|GetAxis)", r"Physics\.", r"Quaternion\."],
            "function_calls": [r"\w+\([^)]*\)", r"distance\([^)]*\)", r"normalize\([^)]*\)", r"lerp\([^)]*\)"],
            "template_syntax": [r"\{\{[^}]+\}\}", r"#\{[^}]+\}"],
            "variable_assignments": [r"\w+\s*=\s*[^;]+;", r"\w+\s*\+=\s*"],
            "conditionals": [r"if\s*\(", r"else\s*\{", r"switch\s*\(", r"case\s+"],
            "method_names": [r"on_trigger_enter", r"on_collision_enter", r"update\s*\(", r"start\s*\(", r"awake\s*\("],
        }
        
        markers = []
        for category, patterns in CODE_LEAK_PATTERNS.items():
            for pattern in patterns:
                matches = re.finditer(pattern, json_response, re.IGNORECASE)
                for match in matches:
                    markers.append({
                        "category": category,
                        "pattern": match.group(),
                    })
                    result.flagged_keywords.append(match.group())
        
        result.keyword_score = len(markers)
        
        # Embedding-based detection
        code_leak_attractors = [a for a in self.config.attractors if a.get('type') == 'code_leak_centroid']
        if code_leak_attractors and "code_leak" in self.centroids:
            embedding = self.get_embedding(json_response)
            if embedding is not None:
                similarity = float(np.dot(embedding, self.centroids["code_leak"]))
                result.embedding_score = similarity
                
                code_leak_threshold = getattr(self.config, 'code_leak_embedding_threshold', 0.70)
                if similarity > code_leak_threshold:
                    result.is_attracted = True
                    result.nearest_attractor = "code_leak"
                    result.triggered_attractors.append("code_leak")
        
        # Trigger on pattern count OR embedding similarity
        if result.keyword_score >= self.config.keyword_threshold:
            result.is_attracted = True
            if "code_leak_patterns" not in result.triggered_attractors:
                result.triggered_attractors.append("code_leak_patterns")
        
        result.attractors_checked = len(code_leak_attractors) if code_leak_attractors else 0
        
        return result
    
    def detect_unified(
        self,
        text: str,
        parsed_json: Dict = None,
        prompt: str = None,
        intensity: float = None,
        use_embeddings: bool = True
    ) -> DetectionResult:
        """
        Unified attractor detection:
        1. Code leak (pattern + embedding)
        2. Structural clichés
        3. Baseline deviation (over/under-engineering)
        
        Args:
            text: Raw JSON string for code leak detection
            parsed_json: Parsed JSON dict for structural detection
            prompt: Original prompt for baseline comparison
            intensity: Detection intensity 0-1 (higher = more sensitive)
            use_embeddings: Whether to use embedding-based detection
        
        Returns:
            DetectionResult with all detection layers
        """
        if intensity is None:
            intensity = self.config.default_intensity
        
        result = DetectionResult()
        result.intensity_used = intensity
        
        # Get active attractors based on intensity
        active_attractors = self._get_active_attractors(intensity)
        result.attractors_checked = len(active_attractors)
        
        # --- Layer 1: Code leak detection ---
        code_result = self._detect_code_leaks(text, active_attractors, use_embeddings)
        result.merge(code_result)
        
        # --- Layer 2: Structural cliché detection ---
        if parsed_json:
            cliche_result = self._detect_structural_cliches(parsed_json, active_attractors)
            result.merge(cliche_result)
        
        # --- Layer 3: Baseline deviation detection ---
        if parsed_json and prompt and self.config.baselines_enabled:
            baseline_result = self._detect_baseline_deviation(parsed_json, prompt)
            result.merge(baseline_result)
        
        return result
    
    def _detect_code_leaks(
        self,
        text: str,
        active_attractors: List[Dict],
        use_embeddings: bool
    ) -> DetectionResult:
        """Layer 1: Code leak detection via patterns and embeddings"""
        result = DetectionResult()
        
        # Pattern-based detection (import from attractor_mapper)
        try:
            from attractor_mapper import detect_code_markers
            markers = detect_code_markers(text)
            result.keyword_score = len(markers)
            result.flagged_keywords = [m["pattern"] for m in markers]
            
            if result.keyword_score >= self.config.keyword_threshold:
                result.is_attracted = True
                result.triggered_attractors.append("code_leak_patterns")
        except ImportError:
            pass
        
        # Embedding-based detection
        if use_embeddings and "code_leak" in self.centroids:
            embedding = self.get_embedding(text)
            if embedding is not None:
                similarity = np.dot(embedding, self.centroids["code_leak"])
                result.embedding_score = similarity
                
                if similarity > self.config.code_leak_embedding_threshold:
                    result.is_attracted = True
                    result.triggered_attractors.append("code_leak_embedding")
                    result.nearest_attractor = "code_leak"
        
        return result
    
    def _detect_structural_cliches(
        self,
        parsed_json: Dict,
        active_attractors: List[Dict]
    ) -> DetectionResult:
        """Layer 2: Structural cliché detection"""
        result = DetectionResult()
        
        # Extract structure from target
        n_fields = len(parsed_json.get("fields", []))
        n_behaviors = len(parsed_json.get("behaviors", []))
        components = set(parsed_json.get("components", []))
        field_names = {f.get("name", "").lower() for f in parsed_json.get("fields", []) if isinstance(f, dict)}
        state = parsed_json.get("state", {})
        has_sm = state.get("has_state_machine", False) if isinstance(state, dict) else False
        
        for attractor in active_attractors:
            if attractor.get("type") != "structural_cliche":
                continue
            
            pattern = attractor.get("pattern", {})
            match_score = self._compute_pattern_match(
                pattern, n_fields, n_behaviors, components, field_names, has_sm
            )
            
            if match_score >= self.config.structural_match_threshold:
                result.is_attracted = True
                result.structural_cliches_matched.append(attractor["name"])
                result.triggered_attractors.append(f"cliche:{attractor['name']}")
        
        return result
    
    def _compute_pattern_match(
        self,
        pattern: Dict,
        n_fields: int,
        n_behaviors: int,
        components: set,
        field_names: set,
        has_sm: bool
    ) -> float:
        """Compute how well target matches a structural pattern (0-1)"""
        
        matches = 0
        checks = 0
        
        # Field count
        if "field_count_range" in pattern:
            checks += 1
            min_f, max_f = pattern["field_count_range"]
            if min_f <= n_fields <= max_f:
                matches += 1
        
        # Behavior count
        if "behavior_count_range" in pattern:
            checks += 1
            min_b, max_b = pattern["behavior_count_range"]
            if min_b <= n_behaviors <= max_b:
                matches += 1
        
        # State machine
        if "always_has_state_machine" in pattern:
            checks += 1
            if has_sm == pattern["always_has_state_machine"]:
                matches += 1
        elif "never_has_state_machine" in pattern:
            checks += 1
            if not has_sm:
                matches += 1
        
        # Common components
        if "common_components" in pattern:
            required = set(pattern["common_components"])
            checks += len(required)
            matches += len(required & components)
        
        # Common field names
        if "common_field_names" in pattern:
            required = {f.lower() for f in pattern["common_field_names"]}
            checks += len(required)
            matches += len(required & field_names)
        
        return matches / checks if checks > 0 else 0
    
    def _detect_baseline_deviation(
        self,
        parsed_json: Dict,
        prompt: str
    ) -> DetectionResult:
        """Layer 3: Detect over/under-engineering vs baseline"""
        result = DetectionResult()
        
        # Find baseline
        import hashlib
        prompt_hash = hashlib.md5(prompt.encode()).hexdigest()[:12]
        baseline = self.baseline_cache.get(prompt_hash)
        
        if not baseline:
            return result
        
        result.baseline_found = True
        
        # Extract actual values
        actual_fields = len(parsed_json.get("fields", []))
        actual_behaviors = len(parsed_json.get("behaviors", []))
        state = parsed_json.get("state", {})
        actual_has_sm = state.get("has_state_machine", False) if isinstance(state, dict) else False
        actual_states = len(state.get("states", [])) if isinstance(state, dict) else 0
        
        # Expected values from baseline
        expected_fields = baseline["n_fields"]
        expected_behaviors = baseline["n_behaviors"]
        expected_has_sm = baseline["has_state_machine"]
        expected_states = baseline["n_states"]
        
        tolerance = self.config.baseline_tolerance
        
        # Check fields
        tol_f = tolerance.get("fields", 0.5)
        min_f = max(0, int(expected_fields * (1 - tol_f)))
        max_f = int(expected_fields * (1 + tol_f)) + 1
        
        if actual_fields < min_f:
            result.under_engineering_score += 0.3
            result.baseline_violations.append(f"fields: {actual_fields} < {min_f} (expected ~{expected_fields})")
        elif actual_fields > max_f:
            result.over_engineering_score += 0.2
            result.baseline_violations.append(f"fields: {actual_fields} > {max_f} (expected ~{expected_fields})")
        
        # Check behaviors
        tol_b = tolerance.get("behaviors", 0.5)
        min_b = max(0, int(expected_behaviors * (1 - tol_b)))
        max_b = int(expected_behaviors * (1 + tol_b)) + 1
        
        if actual_behaviors < min_b:
            result.under_engineering_score += 0.4
            result.baseline_violations.append(f"behaviors: {actual_behaviors} < {min_b} (expected ~{expected_behaviors})")
        elif actual_behaviors > max_b:
            result.over_engineering_score += 0.25
            result.baseline_violations.append(f"behaviors: {actual_behaviors} > {max_b} (expected ~{expected_behaviors})")
        
        # Check state machine
        if expected_has_sm and not actual_has_sm:
            result.under_engineering_score += 0.5
            result.baseline_violations.append("missing state machine (baseline has one)")
        elif not expected_has_sm and actual_has_sm:
            result.over_engineering_score += 0.4
            result.baseline_violations.append("unnecessary state machine (baseline has none)")
        
        # Check state count
        if expected_has_sm and actual_has_sm:
            tol_s = tolerance.get("states", 0.5)
            min_s = max(0, int(expected_states * (1 - tol_s)))
            max_s = int(expected_states * (1 + tol_s)) + 1
            
            if actual_states < min_s:
                result.under_engineering_score += 0.2
                result.baseline_violations.append(f"states: {actual_states} < {min_s}")
            elif actual_states > max_s:
                result.over_engineering_score += 0.15
                result.baseline_violations.append(f"states: {actual_states} > {max_s}")
        
        # Trigger if significant deviation
        if result.over_engineering_score > 0.3 or result.under_engineering_score > 0.3:
            result.is_attracted = True
            if result.over_engineering_score > result.under_engineering_score:
                result.triggered_attractors.append("over_engineering")
            else:
                result.triggered_attractors.append("under_engineering")
        
        return result


def build_natural_language_prompt(
    original_response: str,
    flagged_patterns: List[str]
) -> Tuple[str, str]:
    """
    Build a prompt to regenerate Unity IR JSON with natural language instead of code.
    
    Args:
        original_response: The JSON response containing code syntax
        flagged_patterns: List of code patterns that were detected
    
    Returns:
        Tuple of (system_prompt, user_prompt)
    """
    system_prompt = """You fix Unity behavior JSON that contains programming syntax.
Convert code-like expressions to plain English descriptions.

CONVERT:
- "distance(player) < chaseRange" → "player is within chaseRange"
- "on_trigger_enter:Player" → "player touches this"  
- "{{360 * Time.deltaTime}}" → reference a rotationSpeed field
- "key == 'W'" → "W key is pressed"
- "Vector3.up" → "up"
- "state == Patrol" → "currently patrolling"

Output ONLY the corrected JSON, no markdown, no explanations."""

    flagged_list = "\n".join(f"- {p}" for p in flagged_patterns[:10])
    
    user_prompt = f"""This Unity behavior JSON contains code syntax that should be natural language:

{original_response}

FLAGGED PATTERNS:
{flagged_list}

Rewrite with natural language triggers/conditions/params:"""

    return system_prompt, user_prompt


# ============================================================================
# DUAL-MODE STEERING CLASS
# ============================================================================

class DualModeAttractorSteering:
    """
    Steering system that checks BOTH neutral and controversial attractors.
    
    Controversial attractors are weighted more heavily because they indicate
    worse patterns (hedging, both-sideism).
    """
    
    def __init__(
        self, 
        model_name: str, 
        config_dir: str = DEFAULT_CONFIG_DIR,
        controversial_weight: float = 2.0
    ):
        """
        Load both attractor sets.
        
        Args:
            model_name: Base model name (e.g., "olmo3-7b")
            config_dir: Directory containing filter configs
            controversial_weight: How much to weight controversial matches (default 2.0)
        """
        self.model_name = model_name
        self.config_dir = config_dir
        self.controversial_weight = controversial_weight
        
        # Load neutral attractors
        self.neutral_steering = None
        try:
            self.neutral_steering = load_steering(model_name, config_dir)
            print(f"✓ Loaded neutral attractors for {model_name}")
        except FileNotFoundError:
            print(f"⚠ No neutral attractors found for {model_name}")
        
        # Load controversial attractors
        self.controversial_steering = None
        try:
            self.controversial_steering = load_steering(
                f"{model_name}-controversial",
                config_dir
            )
            print(f"✓ Loaded controversial attractors for {model_name}")
        except FileNotFoundError:
            print(f"⚠ No controversial attractors found for {model_name}")
        
        # Combined config for compatibility
        if self.neutral_steering:
            self.config = self.neutral_steering.config
        elif self.controversial_steering:
            self.config = self.controversial_steering.config
        else:
            raise FileNotFoundError(
                f"No attractor configs found for model '{model_name}' in {config_dir}"
            )
    
    def detect(
        self,
        text: str,
        exempted_keywords: Set[str] = None,
        intensity: float = 0.5,
        use_embeddings: bool = True
    ) -> DetectionResult:
        """
        Detect using both attractor sets.
        
        Args:
            text: Text to analyze
            exempted_keywords: Keywords to exempt
            intensity: Base filtering intensity (0-1)
            use_embeddings: Whether to use embedding detection
        
        Returns:
            Combined detection result
        """
        result = DetectionResult(intensity_used=intensity)
        
        # Check neutral attractors
        if self.neutral_steering:
            neutral_result = self.neutral_steering.detect(
                text,
                exempted_keywords=exempted_keywords,
                intensity=intensity,
                use_embeddings=use_embeddings
            )
            result.keyword_score += neutral_result.keyword_score
            result.embedding_score = max(result.embedding_score, neutral_result.embedding_score)
            result.flagged_keywords.extend(neutral_result.flagged_keywords)
            result.triggered_attractors.extend([f"neutral:{a}" for a in neutral_result.triggered_attractors])
            result.attractors_checked += neutral_result.attractors_checked
        
        # Check controversial attractors (with higher intensity)
        if self.controversial_steering:
            controversial_intensity = min(1.0, intensity + 0.2)  # Boost by 20%
            controversial_result = self.controversial_steering.detect(
                text,
                exempted_keywords=exempted_keywords,
                intensity=controversial_intensity,
                use_embeddings=use_embeddings
            )
            
            # Weight controversial matches more heavily
            result.keyword_score += controversial_result.keyword_score * self.controversial_weight
            result.embedding_score = max(result.embedding_score, controversial_result.embedding_score)
            result.flagged_keywords.extend(controversial_result.flagged_keywords)
            result.triggered_attractors.extend([f"CONTROVERSIAL:{a}" for a in controversial_result.triggered_attractors])
            result.attractors_checked += controversial_result.attractors_checked
            
            # Use controversial nearest attractor if stronger
            if controversial_result.embedding_score > (result.embedding_score / self.controversial_weight):
                result.nearest_attractor = f"CONTROVERSIAL:{controversial_result.nearest_attractor}"
        
        # Final determination with combined thresholds
        combined_keyword_threshold = self.config.keyword_threshold if hasattr(self.config, 'keyword_threshold') else 3.0
        combined_embedding_threshold = self.config.embedding_threshold if hasattr(self.config, 'embedding_threshold') else 0.75
        
        result.is_attracted = (
            result.keyword_score >= combined_keyword_threshold or
            result.embedding_score >= combined_embedding_threshold
        )
        
        return result
    
    def get_attractor_info(self) -> List[Dict]:
        """Get info about all attractors (for display)"""
        info = []
        
        if self.neutral_steering:
            for a in self.neutral_steering.get_attractor_info():
                a['type'] = 'neutral'
                info.append(a)
        
        if self.controversial_steering:
            for a in self.controversial_steering.get_attractor_info():
                a['type'] = 'CONTROVERSIAL'
                a['name'] = f"CONTROVERSIAL:{a['name']}"
                info.append(a)
        
        return info
    
    def get_avoidance_prompt(self, result: DetectionResult) -> str:
        """Generate prompt to steer away from detected attractors"""
        import random
        
        parts = ["\n\nCRITICAL: Your response matched known attractor patterns."]
        
        # Check if controversial attractors were triggered
        has_controversial = any('CONTROVERSIAL:' in a for a in result.triggered_attractors)
        if has_controversial:
            parts.append("WARNING: Detected hedging/both-sideism language!")
            parts.append("Take a CLEAR position. Avoid 'on one hand / on the other hand' framing.")
        
        # List keywords to avoid
        if result.flagged_keywords:
            avoid = list(set(result.flagged_keywords[:6]))
            parts.append(f"DO NOT use these concepts: {', '.join(avoid)}")
        
        # Add a random forcing alternative
        parts.append(random.choice(FORCED_ALTERNATIVES))
        
        return "\n".join(parts)
    
    def analyze_topic(self, topic: str) -> Set[str]:
        """Analyze topic and return keywords to exempt (combines both sets)"""
        exempted = set()
        
        if self.neutral_steering:
            exempted.update(self.neutral_steering.analyze_topic(topic))
        
        if self.controversial_steering:
            exempted.update(self.controversial_steering.analyze_topic(topic))
        
        return exempted


# ============================================================================
# CONVENIENCE FUNCTIONS
# ============================================================================

def load_steering(
    model_name: str, 
    config_dir: str = DEFAULT_CONFIG_DIR
) -> AttractorSteering:
    """Load steering system for a specific model."""
    config_path = Path(config_dir) / model_name / "filter_config.json"
    
    if not config_path.exists():
        raise FileNotFoundError(
            f"No filter config found for model '{model_name}' at {config_path}\n"
            f"Run the attractor mapping pipeline first."
        )
    
    config = SteeringConfig.load(str(config_path))
    return AttractorSteering(config)


def load_dual_steering(
    model_name: str,
    config_dir: str = DEFAULT_CONFIG_DIR,
    controversial_weight: float = 2.0
) -> DualModeAttractorSteering:
    """
    Load dual-mode steering system that checks both neutral and controversial attractors.
    
    Args:
        model_name: Base model name (e.g., "olmo3-7b")
        config_dir: Directory containing filter configs
        controversial_weight: How much to weight controversial matches (default 2.0)
    
    Returns:
        DualModeAttractorSteering instance
    """
    return DualModeAttractorSteering(model_name, config_dir, controversial_weight)


def steer_generation(
    steering: AttractorSteering,
    generate_fn: Callable[[str], str],
    prompt: str,
    intensity: float = 0.5,
    max_attempts: int = 3,
    use_embeddings: bool = True,
    verbose: bool = False
) -> Tuple[str, DetectionResult, int]:
    """
    Generate text with attractor steering.
    
    Args:
        steering: AttractorSteering instance
        generate_fn: Function that takes prompt and returns response
        prompt: The base prompt
        intensity: 0-1 filtering intensity
        max_attempts: Maximum regeneration attempts
        use_embeddings: Whether to use embedding detection
        verbose: Print progress info
    
    Returns:
        Tuple of (response, final_detection_result, attempts_made)
    """
    for attempt in range(max_attempts):
        response = generate_fn(prompt)
        result = steering.detect(response, intensity=intensity, use_embeddings=use_embeddings)
        
        if verbose:
            print(f"  [Attempt {attempt + 1}] Score: {result.keyword_score:.1f}, "
                  f"Attracted: {result.is_attracted}")
        
        if not result.is_attracted:
            return response, result, attempt + 1
        
        if verbose and result.triggered_attractors:
            print(f"    Triggered: {', '.join(result.triggered_attractors)}")
        
        # For next attempt, increase intensity slightly
        intensity = min(1.0, intensity + 0.1)
    
    return response, result, max_attempts


# ============================================================================
# TWO-PHASE FILTERING
# ============================================================================

def _strip_rephrase_preamble(text: str) -> str:
    """
    Strip common LLM preamble patterns from rephrased output.
    
    LLMs often add meta-commentary like "Here is the revised response:"
    despite being told not to. This removes such patterns.
    """
    # Common preamble patterns to remove
    preamble_patterns = [
        r'^Here is (?:the |your |my )?(?:revised|rephrased|updated|corrected|modified|fulfilled|edited).*?[:\n]+\s*',
        r'^(?:Sure|Okay|Certainly|Of course)[,!.]?\s*(?:Here|I)[^\n]*[:\n]+\s*',
        r'^I\'ve (?:revised|rephrased|updated|reworded).*?[:\n]+\s*',
        r'^The (?:revised|rephrased|updated) (?:response|text).*?[:\n]+\s*',
        r'^---+\s*\n?',  # Leading dividers
    ]
    
    result = text.strip()
    
    for pattern in preamble_patterns:
        result = re.sub(pattern, '', result, flags=re.IGNORECASE | re.MULTILINE)
    
    # Also strip trailing dividers and quotes
    result = re.sub(r'\n?---+\s*$', '', result)
    result = result.strip().strip('"').strip()
    
    # If stripping removed too much, return original
    if len(result) < len(text) * 0.3:
        return text.strip()
    
    return result


def identify_attractor_segments(
    text: str, 
    flagged_keywords: List[str],
    context_chars: int = 100
) -> List[Dict]:
    """
    Phase 1: Identify segments containing attractor keywords.
    
    Returns list of segments with their positions and matched keywords.
    Each segment includes surrounding context for better rephrasing.
    
    Args:
        text: The text to analyze
        flagged_keywords: Keywords that were flagged by detection
        context_chars: Characters of context to include before/after
    
    Returns:
        List of segment dicts with: sentence_idx, sentence, keywords, 
        context_before, context_after
    """
    segments = []
    text_lower = text.lower()
    
    # Split into sentences for more natural segment boundaries
    sentences = re.split(r'(?<=[.!?])\s+', text)
    
    for keyword in flagged_keywords:
        keyword_lower = keyword.lower()
        
        # Find which sentences contain this keyword
        for i, sentence in enumerate(sentences):
            if keyword_lower in sentence.lower():
                # Check if we already have this sentence
                existing = next((s for s in segments if s['sentence_idx'] == i), None)
                if existing:
                    if keyword not in existing['keywords']:
                        existing['keywords'].append(keyword)
                else:
                    segments.append({
                        'sentence_idx': i,
                        'sentence': sentence.strip(),
                        'keywords': [keyword],
                        'context_before': sentences[i-1].strip() if i > 0 else "",
                        'context_after': sentences[i+1].strip() if i < len(sentences)-1 else ""
                    })
    
    # Sort by sentence order
    segments.sort(key=lambda s: s['sentence_idx'])
    
    return segments


def build_rephrase_prompt(
    original_response: str,
    segments: List[Dict],
    character_name: str = "the speaker",
    custom_instructions: str = ""
) -> Tuple[str, str]:
    """
    Build prompt for Phase 2: Targeted rephrasing of attractor segments.
    
    Args:
        original_response: The full original response
        segments: List of segments from identify_attractor_segments()
        character_name: Name/description of the speaker for tone matching
        custom_instructions: Additional instructions for rephrasing
    
    Returns:
        Tuple of (system_prompt, user_prompt)
    """
    system_prompt = f"""You are {character_name}. You will be given your own previous response with some phrases marked for revision.

Your task: Output the response again with ONLY those marked phrases reworded. Use different vocabulary while keeping the exact same meaning and your voice.

CRITICAL RULES:
- Output ONLY the revised response text, nothing else
- NO preamble like "Here is..." or "Sure..." 
- NO meta-commentary or explanations
- NO markdown formatting or quotes around the response
- Start directly with the first word of the response
{custom_instructions}"""
    
    # Build the rephrasing request - more concise
    segments_text = []
    for i, seg in enumerate(segments[:5]):  # Limit to 5 segments
        keywords_str = ", ".join(seg['keywords'][:3])
        segments_text.append(f'  {i+1}. "{seg["sentence"][:100]}..." → avoid: {keywords_str}')
    
    user_prompt = f"""YOUR PREVIOUS RESPONSE:
{original_response}

REPHRASE THESE SENTENCES (avoid the listed terms):
{chr(10).join(segments_text)}

OUTPUT THE FULL RESPONSE WITH THOSE SENTENCES REWORDED:"""
    
    return system_prompt, user_prompt


def two_phase_filter(
    original_response: str,
    result: DetectionResult,
    steering: 'AttractorSteering',
    generate_fn: Callable[[str, str], str],
    exempted_keywords: Set[str] = None,
    intensity: float = 0.5,
    use_embeddings: bool = True,
    character_name: str = "the speaker",
    custom_instructions: str = "",
    max_rephrase_attempts: int = 2,
    min_improvement_threshold: float = 2.0,
    verbose: bool = True
) -> Tuple[str, DetectionResult, bool]:
    """
    Two-Phase Filtering: Targeted rephrasing instead of full regeneration.
    
    Phase 1: Identify attractor phrases in the response
    Phase 2: Request targeted rephrasing of only those segments
    
    Args:
        original_response: The response to filter
        result: DetectionResult from initial detection
        steering: AttractorSteering instance
        generate_fn: Function(system_prompt, user_prompt) -> response
        exempted_keywords: Keywords to exempt from detection
        intensity: Detection intensity (0-1)
        use_embeddings: Whether to use embedding detection
        character_name: Name/description of speaker for prompt
        custom_instructions: Additional rephrasing instructions
        max_rephrase_attempts: Maximum rephrasing attempts
        min_improvement_threshold: Accept if score drops below this
        verbose: Print progress info
    
    Returns:
        Tuple of (response, result, improved)
        - response: The (possibly rephrased) response
        - result: The detection result for the returned response
        - improved: Whether the score was improved
    """
    if not result.flagged_keywords:
        return original_response, result, False
    
    # Phase 1: Identify segments
    segments = identify_attractor_segments(original_response, result.flagged_keywords)
    
    if not segments:
        if verbose:
            print(f"  [Phase 1] No segment boundaries found for rephrasing")
        return original_response, result, False
    
    if verbose:
        print(f"  [Phase 1] Found {len(segments)} segment(s) with attractor keywords")
        for seg in segments[:3]:
            preview = seg['sentence'][:50] + "..." if len(seg['sentence']) > 50 else seg['sentence']
            print(f"    - \"{preview}\" ({', '.join(seg['keywords'][:2])})")
    
    # Phase 2: Request targeted rephrasing
    best_response = original_response
    best_result = result
    
    for attempt in range(max_rephrase_attempts):
        system_prompt, user_prompt = build_rephrase_prompt(
            original_response if attempt == 0 else best_response,
            segments,
            character_name,
            custom_instructions
        )
        
        rephrased = generate_fn(system_prompt, user_prompt)
        
        # Strip common preamble patterns that LLMs add
        rephrased = _strip_rephrase_preamble(rephrased)
        
        # Check if rephrasing improved the score
        new_result = steering.detect(
            rephrased, 
            exempted_keywords=exempted_keywords,
            intensity=intensity,
            use_embeddings=use_embeddings
        )
        
        if verbose:
            improvement = result.keyword_score - new_result.keyword_score
            print(f"  [Phase 2, Attempt {attempt + 1}] Score: {new_result.keyword_score:.1f} "
                  f"(Δ {improvement:+.1f})")
        
        # Keep if improved
        if new_result.keyword_score < best_result.keyword_score:
            best_response = rephrased
            best_result = new_result
        
        # Accept if good enough
        if new_result.keyword_score < min_improvement_threshold:
            return rephrased, new_result, True
    
    # Return best attempt (might still be original if rephrasing didn't help)
    improved = best_result.keyword_score < result.keyword_score
    return best_response, best_result, improved


# ============================================================================
# CLI
# ============================================================================

def main():
    import sys
    
    if len(sys.argv) < 3:
        print("="*70)
        print("ATTRACTOR STEERING SYSTEM")
        print("="*70)
        print("\nUsage:")
        print("  python attractor_steering.py <model_name> <test_text> [options]")
        print("\nOptions:")
        print("  --intensity 0.5    Set filtering intensity (0-1)")
        print("  --no-embedding     Skip embedding detection (faster)")
        print("  --list             List all attractors for this model")
        print("\nExamples:")
        print('  python attractor_steering.py my-model "test text" --intensity 0.7')
        print('  python attractor_steering.py my-model --list')
        print("\nIntensity scale:")
        print("  0.0 = No filtering")
        print("  0.3 = Filter top 30% (most dominant attractors only)")
        print("  0.5 = Filter top 50% (default)")
        print("  1.0 = Filter all detected attractors")
        return
    
    model_name = sys.argv[1]
    use_embeddings = "--no-embedding" not in sys.argv
    list_mode = "--list" in sys.argv
    
    # Parse intensity
    intensity = 0.5
    if "--intensity" in sys.argv:
        idx = sys.argv.index("--intensity")
        if idx + 1 < len(sys.argv):
            try:
                intensity = float(sys.argv[idx + 1])
            except ValueError:
                pass
    
    # Load steering
    print(f"Loading steering config for: {model_name}")
    try:
        steering = load_steering(model_name)
    except FileNotFoundError as e:
        print(f"\nError: {e}")
        return
    
    print(f"  Loaded {len(steering.config.attractors)} attractors")
    print(f"  Default intensity: {steering.config.default_intensity}")
    
    if list_mode:
        print("\n" + "="*70)
        print("ATTRACTORS (ranked by dominance)")
        print("="*70)
        for info in steering.get_attractor_info():
            print(f"\n  #{info['rank']}: {info['name']} ({info['percentage']:.1f}%)")
            print(f"       Keywords: {', '.join(info['top_keywords'])}")
        return
    
    # Get test text
    test_text = " ".join([
        arg for arg in sys.argv[2:] 
        if not arg.startswith("--") and arg not in [str(intensity)]
    ])
    
    if not test_text:
        print("Error: No test text provided")
        return
    
    print(f"\nTesting with intensity={intensity}")
    print(f"Text: {test_text[:80]}{'...' if len(test_text) > 80 else ''}")
    
    result = steering.detect(test_text, intensity=intensity, use_embeddings=use_embeddings)
    print(f"\n{result.summary()}")


if __name__ == "__main__":
    main()
