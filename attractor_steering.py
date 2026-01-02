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
    default_intensity: float = 0.5
    max_regeneration_attempts: int = 3
    embedding_url: str = DEFAULT_EMBEDDING_URL
    embedding_model: str = DEFAULT_EMBEDDING_MODEL
    # All attractor keywords (for topic-based exemption)
    all_keywords: List[str] = field(default_factory=list)
    
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
        
        return cls(
            model_name=data.get('model_name', 'unknown'),
            attractors=attractors,
            total_attractors=data.get('total_attractors', len(attractors)),
            keyword_threshold=settings.get('keyword_threshold', 3.0),
            embedding_threshold=settings.get('embedding_threshold', 0.75),
            default_intensity=settings.get('default_intensity', 0.5),
            max_regeneration_attempts=settings.get('max_regeneration_attempts', 3),
            # All attractor keywords (for topic-based exemption)
            all_keywords=data.get('all_keywords', [])
        )


@dataclass
class DetectionResult:
    """Result of attractor detection"""
    is_attracted: bool = False
    intensity_used: float = 0.5
    attractors_checked: int = 0
    keyword_score: float = 0.0
    embedding_score: float = 0.0
    triggered_attractors: List[str] = field(default_factory=list)
    flagged_keywords: List[str] = field(default_factory=list)
    nearest_attractor: Optional[str] = None
    
    def summary(self) -> str:
        """Human-readable summary"""
        if not self.is_attracted:
            return f"✓ No attractor match (intensity={self.intensity_used:.1f}, checked {self.attractors_checked} attractors)"

        parts = [f"⚠️ Attractor match detected (intensity={self.intensity_used:.1f})"]
        parts.append(f"  Checked {self.attractors_checked} attractors")
        parts.append(f"  Keyword score: {self.keyword_score:.1f}")
        
        if self.triggered_attractors:
            parts.append(f"  Triggered: {', '.join(self.triggered_attractors)}")
        
        if self.flagged_keywords:
            parts.append(f"  Flagged keywords: {', '.join(self.flagged_keywords[:8])}")
        
        if self.embedding_score > 0:
            parts.append(f"  Embedding similarity: {self.embedding_score:.3f}")
            if self.nearest_attractor:
                parts.append(f"  Nearest attractor: {self.nearest_attractor}")
        
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
    """Main steering system with intensity-based filtering"""
    
    def __init__(self, config: SteeringConfig):
        self.config = config
        self.centroids = self._load_centroids()
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
        
        # ========================================
        # KEYWORD DETECTION
        # ========================================
        keyword_index = self._build_keyword_index(active_attractors)
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
        
        # Track triggered attractors
        for name, score in attractor_scores.items():
            if score >= self.config.keyword_threshold / 2:
                result.triggered_attractors.append(name)
        
        # ========================================
        # EMBEDDING DETECTION
        # ========================================
        if use_embeddings and self.centroids:
            # Only check centroids for active attractors
            active_names = {a['name'] for a in active_attractors}
            active_centroids = {
                name: centroid 
                for name, centroid in self.centroids.items() 
                if name in active_names
            }
            
            if active_centroids:
                emb = self.get_embedding(text)
                
                if emb is not None:
                    best_similarity = 0
                    best_attractor = None
                    
                    for name, centroid in active_centroids.items():
                        similarity = float(np.dot(emb, centroid))
                        if similarity > best_similarity:
                            best_similarity = similarity
                            best_attractor = name
                    
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
            result.embedding_score >= self.config.embedding_threshold
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
