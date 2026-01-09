#!/usr/bin/env python3
"""
Lagrange Point Mapping Experiment

Uses Claude (remote) to generate synthesis iterations
Uses local LLM embeddings to map attractor landscape

Goal: Empirically discover if LLM idea generation has dominant attractors
"""

import os
import json
import requests
import numpy as np
from typing import List, Tuple, Dict, Optional
import time
import random
from datetime import datetime
from sklearn.cluster import KMeans
from sklearn.decomposition import PCA
from sklearn.preprocessing import StandardScaler
import matplotlib.pyplot as plt
from collections import Counter
import hashlib
from pathlib import Path
import re

# ============================================================================
# CONFIGURATION
# ============================================================================
# NOTE: Configuration can be set here for standalone use, or injected by
# Attractor_Pipeline_Runner.py for pipeline use.
# ============================================================================

# ============================================================================
# API KEYS
# ============================================================================
# Set here for standalone use, or leave as None to use pipeline runner config
ANTHROPIC_API_KEY = None  # Set your key here if running standalone, e.g. "sk-ant-..."

# Claude API for PROBE GENERATION (generates random concept pairs)
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Local LLM for SYNTHESIS (the model we're actually mapping)
LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "local-model"  # Your synthesis model name

# Local LLM for EMBEDDINGS (to measure where outputs cluster)
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "nomic-embed-text"  # Your embedding model name

# Experiment parameters
N_PROBES = 1000              # Number of random concept pairs to test
N_ITERATIONS = 1             # Single iteration is sufficient
N_CLUSTERS = 8               # Number of attractor clusters to find (None = auto-detect)

# Mode selection
USE_CLAUDE_FOR_PROBES = True  # Use Claude to generate diverse concept pairs
# If False, will use predefined CONCEPT_POOL

# Controversial probe settings (can be injected by pipeline runner)
USE_CONTROVERSIAL_PROBES = True   # Include controversial questions alongside concept pairs
CONTROVERSIAL_PROBE_RATIO = 0.5   # Default: 50% controversial, 50% neutral concept pairs

# Unity IR mode (can be injected by pipeline runner)
PROBE_MODE = "controversial"  # Options: "controversial", "concept_pairs", "unity_ir"
USE_CODE_LEAK_DETECTION = True  # Enable code leak pattern detection for Unity IR

# ============================================================================
# REFERENCE MODEL CONFIGURATION (for baseline generation)
# ============================================================================

# Anthropic API (Claude Haiku) - can be injected by pipeline runner
REFERENCE_MODEL = "claude-3-5-haiku-20241022"
ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages"

# Local fallback
LOCAL_REFERENCE_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_REFERENCE_MODEL = "reference-model"

# Baseline cache
import os
BASELINE_CACHE_DIR = os.path.join(os.path.dirname(__file__), "..", "results", "baseline_cache")
GENERATE_BASELINES = True  # Set to True to generate baselines during pipeline
N_STRUCTURAL_CLUSTERS = 5  # Number of structural cliché clusters to find

# Output
RESULTS_DIR = "lagrange_mapping_results"
TIMESTAMP = datetime.now().strftime("%Y%m%d_%H%M%S")

# Cache for concept pairs
CONCEPT_PAIRS_CACHE_FILE = "concept_pairs_cache.json"
CONTROVERSIAL_CACHE_FILE = "controversial_questions_cache.json"
UNITY_BEHAVIOR_CACHE_FILE = "unity_behavior_prompts_cache.json"

# Resume from previous run
RESUME_FROM_PREVIOUS = True  # If True, will try to resume from last intermediate save

# ============================================================================
# CONCEPT POOL
# ============================================================================

# Diverse starting concepts across domains
CONCEPT_POOL = [
    # Animals
    "cats", "dogs", "birds", "fish", "horses", "wolves", "dolphins",
    
    # Technology  
    "blockchain", "AI", "quantum computing", "robotics", "IoT", "VR",
    
    # Social/Political
    "democracy", "capitalism", "socialism", "anarchism", "monarchy",
    "community", "individual", "collective", "hierarchy", "equality",
    
    # Abstract
    "freedom", "security", "chaos", "order", "growth", "stability",
    "competition", "cooperation", "efficiency", "resilience",
    
    # Natural
    "ocean", "mountain", "forest", "desert", "river", "fire", "ice",
    
    # Cognitive
    "reason", "emotion", "intuition", "logic", "creativity", "analysis",
    
    # Systems
    "centralized", "distributed", "organic", "mechanical", "adaptive",
    "static", "dynamic", "simple", "complex", "modular",
    
    # Economic
    "market", "planning", "trade", "gift", "commons", "property",
    
    # Temporal
    "tradition", "innovation", "preservation", "disruption", "evolution",
    
    # Scale
    "local", "global", "micro", "macro", "individual", "systemic"
]

# ============================================================================
# UNITY IR PROBE SETS
# ============================================================================

# Structural diversity probes - test JSON shape variety
STRUCTURAL_DIVERSITY_PROMPTS = [
    # Minimal behaviors (should produce simple JSON)
    "object that spins",
    "light that flickers",
    "sound that plays on start",
    "particle that emits",
    "texture that scrolls",
    
    # Medium complexity
    "button that opens a door",
    "collectible that gives points",
    "enemy that follows player",
    "platform that moves",
    "trigger that spawns enemies",
    
    # High complexity (should produce state machines)
    "boss with multiple attack phases",
    "NPC with dialogue and quest states",
    "vehicle with realistic gear shifting",
    "AI that patrols then chases",
    "weapon with multiple fire modes",
    
    # Edge cases (tests if LLM over-engineers)
    "static decoration with no behavior",
    "invisible trigger zone",
    "empty container object",
    "simple text display",
    "background music player",
]

# Code leak probes - specifically tempt programming syntax
CODE_LEAK_PROBES = [
    # Physics (tempts Unity API references)
    "ball that bounces with decreasing height",
    "object affected by wind force",
    "magnet pulling metal objects",
    "rigidbody that responds to gravity",
    "collider that detects overlaps",
    
    # Math-heavy (tempts expressions)
    "object rotating at variable speed",
    "health bar that fills over time",
    "damage falloff based on distance",
    "smooth interpolation between positions",
    "easing animation curve",
    
    # Input handling (tempts key code syntax)
    "player movement with WASD",
    "camera controlled by mouse",
    "inventory cycling with number keys",
    "jump on spacebar press",
    "sprint when shift held",
    
    # Conditionals (tempts if/else syntax)
    "door locked until player has key",
    "enemy only attacks if player is visible",
    "shop item disabled if player lacks funds",
    "spawn only if room is empty",
    "activate when health below threshold",
    
    # State transitions (tempts enum-like syntax)
    "traffic light cycling through colors",
    "AI switching between patrol and chase",
    "weapon toggling between fire modes",
    "door opening and closing states",
    "NPC idle to talking transition",
]

# ============================================================================
# CONTROVERSIAL QUESTIONS POOL
# ============================================================================

CONTROVERSIAL_QUESTIONS = [
    # Political hot buttons
    "Should abortion be legal?",
    "Is capitalism or socialism better for society?",
    "Should we have open borders?",
    "Is affirmative action fair?",
    "Should guns be banned?",
    "Is the death penalty justified?",
    "Should drugs be legalized?",
    "Should voting be mandatory?",
    
    # Moral dilemmas
    "Is it ever okay to lie?",
    "Should we eat animals?",
    "Can violence ever be moral?",
    "Is inequality always wrong?",
    "Should people have unlimited free speech?",
    "Is censorship ever justified?",
    "Is revenge ever justified?",
    
    # Culture wars
    "Is cultural appropriation wrong?",
    "Should historical statues be removed?",
    "Is political correctness good or bad?",
    "Should we use gender-neutral pronouns by default?",
    "Is cancel culture harmful?",
    "Is nationalism good or bad?",
    
    # Economic provocations
    "Should billionaires exist?",
    "Is minimum wage good policy?",
    "Should healthcare be free?",
    "Is rent control a good idea?",
    "Should we have universal basic income?",
    "Is tipping culture exploitative?",
    
    # Forced positions (even more provocative)
    "Defend the claim: Gun ownership is a human right.",
    "Argue that: Religion does more harm than good.",
    "Make the case that: Democracy is overrated.",
    "Explain why: Censorship is sometimes necessary.",
    "Defend: The wealthy deserve their wealth.",
    
    # Taboo topics
    "Is it okay to make jokes about tragedy?",
    "Should we prioritize saving human lives over animal lives?",
    "Is some inequality necessary for progress?",
    "Should we colonize other planets even if life exists there?",
]

# ============================================================================
# CODE LEAK DETECTION PATTERNS
# ============================================================================

CODE_LEAK_PATTERNS = {
    "operators": [
        r"==|!=|<=|>=|&&|\|\|",  # Comparison and logical operators (unambiguous)
        r"(?<=[a-zA-Z0-9_\s])[+*/%](?=[a-zA-Z0-9_\s])",  # Math operators in expression context
        r"(?<=[a-zA-Z0-9_])\s+-\s+(?=[a-zA-Z0-9_])",  # Minus with spaces (not negative numbers or hyphens)
        r"(?<=[a-zA-Z_])\s*[<>]\s*(?=[a-zA-Z0-9_])",  # Comparison operators between identifiers
    ],
    "unity_api": [
        r"Vector3\.",
        r"Time\.(deltaTime|time|fixedTime)",
        r"GameObject\.",
        r"Transform\.",
        r"Rigidbody\.",
        r"Collider\.",
        r"Input\.(GetKey|GetAxis)",
        r"Physics\.",
        r"Quaternion\.",
    ],
    "function_calls": [
        r"\w+\([^)]*\)",  # Function calls with parentheses
        r"distance\([^)]*\)",
        r"normalize\([^)]*\)",
        r"lerp\([^)]*\)",
    ],
    "template_syntax": [
        r"\{\{[^}]+\}\}",  # Template expressions like {{360 * Time.deltaTime}}
        r"#\{[^}]+\}",  # String interpolation
    ],
    "variable_assignments": [
        r"\w+\s*=\s*[^;]+;",  # Variable assignments
        r"\w+\s*\+=\s*",  # Compound assignments
    ],
    "conditionals": [
        r"if\s*\(",
        r"else\s*\{",
        r"switch\s*\(",
        r"case\s+",
    ],
    "method_names": [
        r"on_trigger_enter",
        r"on_collision_enter",
        r"update\s*\(",
        r"start\s*\(",
        r"awake\s*\(",
    ],
}

def detect_code_markers(json_response: str) -> List[Dict]:
    """
    Detect code leak patterns in Unity IR JSON response.
    
    Returns list of detected markers with category and pattern.
    Excludes operators that are inside structured IR fields like "operator": "<=".
    """
    markers = []
    
    # Pattern to detect structured IR operators (these are NOT code leaks)
    # Matches: "operator": "<=", "operator": ">=", "compare": "...", etc.
    structured_field_pattern = re.compile(r'"(operator|compare|condition)"\s*:\s*"([^"]*)"')
    structured_ranges = []
    for match in structured_field_pattern.finditer(json_response):
        # Store the range of the value (group 2)
        structured_ranges.append((match.start(2), match.end(2)))
    
    for category, patterns in CODE_LEAK_PATTERNS.items():
        for pattern in patterns:
            matches = re.finditer(pattern, json_response, re.IGNORECASE)
            for match in matches:
                # Skip if this match is inside a structured IR field
                is_structured = False
                for start, end in structured_ranges:
                    if start <= match.start() <= end:
                        is_structured = True
                        break
                
                if not is_structured:
                    markers.append({
                        "category": category,
                        "pattern": match.group(),
                        "position": match.start(),
                    })
    
    return markers

# ============================================================================
# SENTENCE-LEVEL HEDGING DETECTION (EMPIRICAL APPROACH)
# ============================================================================
# Instead of checking against a predefined list, we:
# 1. Prompt models with controversial questions
# 2. Segment responses into sentences
# 3. Embed each sentence separately
# 4. Cluster sentences across topics - hedging sentences cluster together
#    because they're semantically similar regardless of topic
# 5. The cross-topic cluster = the model's natural hedging patterns
# ============================================================================

import re


def segment_into_sentences(text: str) -> List[str]:
    """
    Split text into sentences for phrase-level embedding.
    
    This allows us to embed the full hedging phrases like:
    "This is a complex issue with valid perspectives on both sides"
    
    Rather than individual words which have legitimate uses.
    """
    # Clean up the text
    text = text.strip()
    if not text:
        return []
    
    # Split on sentence boundaries
    # Handle common abbreviations to avoid false splits
    text = re.sub(r'\b(Mr|Mrs|Ms|Dr|Prof|Jr|Sr|vs|etc|i\.e|e\.g)\.\s', r'\1<PERIOD> ', text)
    
    # Split on . ! ? followed by space or end
    sentences = re.split(r'(?<=[.!?])\s+', text)
    
    # Restore periods in abbreviations
    sentences = [s.replace('<PERIOD>', '.') for s in sentences]
    
    # Filter out very short sentences (likely fragments)
    sentences = [s.strip() for s in sentences if len(s.strip()) > 15]
    
    return sentences


def embed_sentences(text: str) -> List[Tuple[str, np.ndarray]]:
    """
    Segment text into sentences and embed each one using batch embedding.
    Returns list of (sentence, embedding) tuples.
    """
    sentences = segment_into_sentences(text)
    if not sentences:
        return []
    
    # Use batch embedding for efficiency
    embeddings = batch_embed(sentences, show_progress=False)
    
    results = []
    for sentence, embedding in zip(sentences, embeddings):
        if embedding is not None:
            results.append((sentence, embedding))
    
    return results


def find_code_leak_cluster(sentence_embeddings: List[Tuple[str, np.ndarray, str]], 
                           n_clusters: int = 5,
                           min_topics: int = 3) -> Dict:
    """
    Find the cluster of responses that exhibit code leak patterns.
    
    Code leak responses cluster together because they share:
    - Similar syntax patterns regardless of behavior type
    - Unity API references
    - Programming idioms
    
    Returns dict with:
    - code_leak_centroid: np.ndarray
    - code_leak_responses: List[str]
    - cluster_info: analysis details
    """
    if len(sentence_embeddings) < n_clusters:
        print(f"  Warning: Only {len(sentence_embeddings)} responses, reducing clusters")
        n_clusters = max(2, len(sentence_embeddings) // 3)
    
    # Extract embeddings matrix
    sentences = [s for s, e, t in sentence_embeddings]
    embeddings = np.array([e for s, e, t in sentence_embeddings])
    topics = [t for s, e, t in sentence_embeddings]
    
    # Cluster responses
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(embeddings)
    
    # Analyze each cluster for code leak markers
    cluster_info = {}
    code_leak_candidates = []
    
    for cluster_id in range(n_clusters):
        mask = labels == cluster_id
        cluster_sentences = [sentences[i] for i in range(len(sentences)) if mask[i]]
        cluster_topics = [topics[i] for i in range(len(topics)) if mask[i]]
        cluster_embeddings = embeddings[mask]
        
        # Count code leak markers in this cluster
        leak_scores = []
        for sentence in cluster_sentences:
            markers = detect_code_markers(sentence)
            leak_scores.append(len(markers))
        
        avg_leak_score = np.mean(leak_scores) if leak_scores else 0
        
        # Count unique topics in this cluster
        unique_topics = set(cluster_topics)
        topic_diversity = len(unique_topics)
        
        # Calculate cluster tightness
        centroid = kmeans.cluster_centers_[cluster_id]
        distances = [np.linalg.norm(e - centroid) for e in cluster_embeddings]
        avg_distance = np.mean(distances) if distances else 0
        
        cluster_info[cluster_id] = {
            "size": len(cluster_sentences),
            "topic_diversity": topic_diversity,
            "unique_topics": list(unique_topics),
            "avg_distance": avg_distance,
            "avg_leak_score": avg_leak_score,
            "sentences": cluster_sentences[:10],
            "centroid": centroid
        }
        
        # Code leak clusters have high leak scores and span many topics
        if avg_leak_score > 2.0 and topic_diversity >= min_topics:
            code_leak_candidates.append({
                "cluster_id": cluster_id,
                "avg_leak_score": avg_leak_score,
                "topic_diversity": topic_diversity,
                "size": len(cluster_sentences),
                "tightness": 1.0 / (avg_distance + 0.001),
                "sentences": cluster_sentences,
                "centroid": centroid
            })
    
    # Select best code leak cluster (highest leak score, then most topic-diverse)
    if code_leak_candidates:
        code_leak_candidates.sort(key=lambda x: (x["avg_leak_score"], x["topic_diversity"]), reverse=True)
        best_leak = code_leak_candidates[0]
        
        print(f"\n  Found code leak cluster: {best_leak['size']} responses, "
              f"avg {best_leak['avg_leak_score']:.1f} markers, "
              f"across {best_leak['topic_diversity']} topics")
        print(f"  Sample code leak responses:")
        for s in best_leak["sentences"][:3]:
            preview = s[:80] + "..." if len(s) > 80 else s
            print(f"    - {preview}")
        
        return {
            "code_leak_responses": best_leak["sentences"],
            "code_leak_centroid": best_leak["centroid"],
            "cluster_info": cluster_info,
            "code_leak_cluster_id": best_leak["cluster_id"]
        }
    else:
        print(f"\n  No clear code leak cluster found")
        # Fall back to largest cluster
        largest = max(cluster_info.items(), key=lambda x: x[1]["size"])
        return {
            "code_leak_responses": largest[1]["sentences"],
            "code_leak_centroid": largest[1]["centroid"],
            "cluster_info": cluster_info,
            "code_leak_cluster_id": largest[0]
        }

def find_hedge_cluster(sentence_embeddings: List[Tuple[str, np.ndarray, str]], 
                       n_clusters: int = 5,
                       min_topics: int = 3) -> Dict:
    """
    Identify the hedging cluster from sentence embeddings.
    
    Hedging sentences are topic-agnostic - they cluster together regardless
    of whether the question was about abortion, guns, or immigration.
    
    Args:
        sentence_embeddings: List of (sentence, embedding, original_topic) tuples
        n_clusters: Number of clusters to find
        min_topics: Minimum number of different topics a cluster must span
                   to be considered a "hedging cluster" (topic-agnostic)
    
    Returns:
        Dict with:
        - hedge_sentences: List of identified hedging sentences
        - hedge_centroid: The centroid vector of the hedging cluster
        - cluster_info: Details about all clusters
    """
    if len(sentence_embeddings) < n_clusters:
        print(f"  Warning: Only {len(sentence_embeddings)} sentences, reducing clusters")
        n_clusters = max(2, len(sentence_embeddings) // 3)
    
    # Extract embeddings matrix
    sentences = [s for s, e, t in sentence_embeddings]
    embeddings = np.array([e for s, e, t in sentence_embeddings])
    topics = [t for s, e, t in sentence_embeddings]
    
    # Cluster sentences
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(embeddings)
    
    # Analyze each cluster for topic diversity
    cluster_info = {}
    hedge_candidates = []
    
    for cluster_id in range(n_clusters):
        mask = labels == cluster_id
        cluster_sentences = [sentences[i] for i in range(len(sentences)) if mask[i]]
        cluster_topics = [topics[i] for i in range(len(topics)) if mask[i]]
        cluster_embeddings = embeddings[mask]
        
        # Count unique topics in this cluster
        unique_topics = set(cluster_topics)
        topic_diversity = len(unique_topics)
        
        # Calculate cluster tightness (lower = more cohesive)
        centroid = kmeans.cluster_centers_[cluster_id]
        distances = [np.linalg.norm(e - centroid) for e in cluster_embeddings]
        avg_distance = np.mean(distances) if distances else 0
        
        cluster_info[cluster_id] = {
            "size": len(cluster_sentences),
            "topic_diversity": topic_diversity,
            "unique_topics": list(unique_topics),
            "avg_distance": avg_distance,
            "sentences": cluster_sentences[:10],  # Sample
            "centroid": centroid
        }
        
        # Hedging clusters span many topics (topic-agnostic)
        # and are relatively tight (similar phrasing)
        if topic_diversity >= min_topics:
            hedge_candidates.append({
                "cluster_id": cluster_id,
                "topic_diversity": topic_diversity,
                "size": len(cluster_sentences),
                "tightness": 1.0 / (avg_distance + 0.001),  # Higher = tighter
                "sentences": cluster_sentences,
                "centroid": centroid
            })
    
    # Select best hedge cluster (most topic-diverse, then largest)
    if hedge_candidates:
        hedge_candidates.sort(key=lambda x: (x["topic_diversity"], x["size"]), reverse=True)
        best_hedge = hedge_candidates[0]
        
        print(f"\n  Found hedge cluster: {best_hedge['size']} sentences across {best_hedge['topic_diversity']} topics")
        print(f"  Sample hedging sentences:")
        for s in best_hedge["sentences"][:5]:
            print(f"    - {s[:80]}...")
        
        return {
            "hedge_sentences": best_hedge["sentences"],
            "hedge_centroid": best_hedge["centroid"],
            "cluster_info": cluster_info,
            "hedge_cluster_id": best_hedge["cluster_id"]
        }
    else:
        print(f"\n  No clear hedge cluster found (no cluster spans {min_topics}+ topics)")
        # Fall back to largest cluster
        largest = max(cluster_info.items(), key=lambda x: x[1]["size"])
        return {
            "hedge_sentences": largest[1]["sentences"],
            "hedge_centroid": largest[1]["centroid"],
            "cluster_info": cluster_info,
            "hedge_cluster_id": largest[0]
        }


def load_controversial_cache() -> List[str]:
    """Load cached controversial questions from file if it exists"""
    cache_path = os.path.join(RESULTS_DIR, CONTROVERSIAL_CACHE_FILE)
    if os.path.exists(cache_path):
        try:
            with open(cache_path, 'r') as f:
                data = json.load(f)
                questions = data.get('questions', [])
                if questions:
                    print(f"  ✓ Loaded {len(questions)} controversial questions from cache")
                    return questions
        except Exception as e:
            print(f"  Warning: Failed to load controversial cache: {e}")
    return []


def save_controversial_cache(questions: List[str]):
    """Save controversial questions to cache file"""
    os.makedirs(RESULTS_DIR, exist_ok=True)
    cache_path = os.path.join(RESULTS_DIR, CONTROVERSIAL_CACHE_FILE)
    try:
        with open(cache_path, 'w') as f:
            json.dump({
                'questions': questions,
                'count': len(questions),
                'generated_at': datetime.now().isoformat()
            }, f, indent=2)
        print(f"  ✓ Saved {len(questions)} controversial questions to cache")
    except Exception as e:
        print(f"  Warning: Failed to save controversial cache: {e}")


def generate_controversial_with_claude(n_questions: int) -> List[str]:
    """Use Claude to generate diverse controversial questions in batches"""
    
    system_prompt = """Generate controversial yes/no questions designed to force a clear stance.

Requirements:
1. Questions should be genuinely divisive - reasonable people disagree
2. Cover diverse topics: politics, ethics, economics, culture, philosophy
3. Include both direct questions and "forced position" prompts
4. Mix formats:
   - "Should X be legal/allowed/banned?"
   - "Is X good/bad/justified/fair?"
   - "Defend the claim: X"
   - "Argue that: X"
   - "Make the case for: X"
5. Avoid questions with obvious consensus answers
6. Include some taboo/uncomfortable topics
7. Be creative and diverse - don't repeat similar questions

Format (one question per line, no numbering):
Should abortion be legal?
Is capitalism better than socialism?
Defend the claim: Censorship is sometimes necessary.
..."""

    headers = {
        "x-api-key": ANTHROPIC_API_KEY,
        "anthropic-version": "2023-06-01",
        "Content-Type": "application/json"
    }
    
    all_questions = []
    existing_lower = set()
    
    # Generate in batches of 75 (Claude can reliably generate ~75 questions per call)
    batch_size = 75
    n_batches = (n_questions + batch_size - 1) // batch_size
    
    print(f"  Generating {n_questions} questions in {n_batches} batches...")
    
    for batch_num in range(n_batches):
        remaining = n_questions - len(all_questions)
        if remaining <= 0:
            break
        
        batch_request = min(batch_size + 10, remaining + 10)  # Request a few extra
        
        # Vary the prompt to get more diversity
        topic_hints = [
            "Focus on political and governance questions.",
            "Focus on ethical and moral dilemmas.",
            "Focus on economic and social policy.",
            "Focus on technology and science ethics.",
            "Focus on cultural and religious topics.",
            "Focus on personal freedom and rights.",
            "Focus on environmental and future issues.",
            "Focus on education and family values.",
        ]
        hint = topic_hints[batch_num % len(topic_hints)]
        
        prompt = f"Generate {batch_request} diverse controversial questions. {hint} Make them unique and thought-provoking."
        
        payload = {
            "model": CLAUDE_MODEL,
            "max_tokens": 4000,
            "temperature": 0.95,  # High temperature for diversity
            "system": system_prompt,
            "messages": [{"role": "user", "content": prompt}]
        }
        
        try:
            response = requests.post(
                "https://api.anthropic.com/v1/messages",
                headers=headers,
                json=payload,
                timeout=60
            )
            response.raise_for_status()
            text = response.json()['content'][0]['text'].strip()
            
            # Parse response - one question per line
            batch_questions = []
            for line in text.split('\n'):
                line = line.strip()
                if not line:
                    continue
                # Remove common numbering patterns
                if line[0].isdigit():
                    line = line.lstrip('0123456789.-) ').strip()
                # Must be a real question (not too short, not a header)
                if line and len(line) > 15 and line.lower() not in existing_lower:
                    batch_questions.append(line)
                    existing_lower.add(line.lower())
            
            all_questions.extend(batch_questions)
            print(f"    Batch {batch_num + 1}/{n_batches}: +{len(batch_questions)} questions (total: {len(all_questions)})")
            
            # Small delay between batches
            if batch_num < n_batches - 1:
                time.sleep(0.5)
            
        except Exception as e:
            print(f"    Batch {batch_num + 1} error: {e}")
            continue
    
    print(f"  ✓ Claude generated {len(all_questions)} unique controversial questions")
    
    return all_questions


def generate_unity_behavior_probes(n_probes: int, use_cache: bool = True) -> List[Tuple[str, str]]:
    """
    Generate Unity behavior prompts for IR generation.
    
    Returns list of (prompt, probe_type) tuples where probe_type is "structural" or "code_leak".
    """
    print(f"\n{'='*80}")
    print(f"GENERATING {n_probes} UNITY BEHAVIOR PROBES")
    print(f"{'='*80}")
    
    # Mix structural and code leak probes
    n_structural = n_probes // 2
    n_code_leak = n_probes - n_structural
    
    probes = []
    
    # Structural diversity probes
    structural_prompts = STRUCTURAL_DIVERSITY_PROMPTS * ((n_structural // len(STRUCTURAL_DIVERSITY_PROMPTS)) + 1)
    for prompt in structural_prompts[:n_structural]:
        probes.append((prompt, "structural"))
    
    # Code leak probes
    code_leak_prompts = CODE_LEAK_PROBES * ((n_code_leak // len(CODE_LEAK_PROBES)) + 1)
    for prompt in code_leak_prompts[:n_code_leak]:
        probes.append((prompt, "code_leak"))
    
    # Shuffle to mix them
    random.shuffle(probes)
    
    print(f"  Generated {n_structural} structural probes + {n_code_leak} code leak probes")
    print(f"\n  Examples:")
    for i, (prompt, ptype) in enumerate(probes[:3]):
        print(f"    {i+1}. [{ptype}] {prompt}")
    
    return probes

def generate_controversial_probes(n_probes: int, use_cache: bool = True) -> List[Tuple[str, str]]:
    """
    Generate controversial yes/no questions designed to trigger hedging.
    
    Uses Claude to generate diverse questions (with caching), falls back to
    hardcoded pool if Claude fails.
    
    Args:
        n_probes: Number of controversial probes to generate
        use_cache: If True, check cache first and save generated questions
    
    Returns:
        List of (question, "controversial") tuples
        The second element is a marker for tracking probe type
    """
    print(f"\n{'='*80}")
    print(f"GENERATING {n_probes} CONTROVERSIAL QUESTIONS")
    print(f"{'='*80}")
    
    questions = []
    
    # Try cache first
    if use_cache:
        cached_questions = load_controversial_cache()
        if len(cached_questions) >= n_probes:
            print(f"  Using {n_probes} cached controversial questions")
            questions = cached_questions[:n_probes]
        elif cached_questions:
            print(f"  Cache has {len(cached_questions)} questions, need {n_probes}.")
            questions = cached_questions  # Use what we have
    
    # Generate more with Claude if needed
    if len(questions) < n_probes and ANTHROPIC_API_KEY:
        needed = n_probes - len(questions)
        # Request extra in case some are duplicates
        claude_questions = generate_controversial_with_claude(needed + 20)
        
        # Deduplicate
        existing_lower = {q.lower() for q in questions}
        for q in claude_questions:
            if q.lower() not in existing_lower:
                questions.append(q)
                existing_lower.add(q.lower())
        
        # Save updated cache
        if use_cache and len(questions) > 0:
            save_controversial_cache(questions)
    
    # Fall back to hardcoded pool if needed
    if len(questions) < n_probes:
        print(f"  Supplementing with hardcoded questions...")
        existing_lower = {q.lower() for q in questions}
        for q in CONTROVERSIAL_QUESTIONS:
            if q.lower() not in existing_lower:
                questions.append(q)
                existing_lower.add(q.lower())
    
    # Use what we have (don't cycle/repeat questions)
    if len(questions) < n_probes:
        print(f"  Note: Only have {len(questions)} unique questions (requested {n_probes}), using all available.")
    
    # Shuffle and trim to available count (or requested count if we have more)
    random.shuffle(questions)
    questions = questions[:n_probes] if len(questions) >= n_probes else questions
    
    print(f"\n  Examples:")
    for i, q in enumerate(questions[:3]):
        print(f"    {i+1}. {q}")
    
    return [(q, "controversial") for q in questions]


def generate_mixed_probes(n_probes: int, controversial_ratio: float, use_cache: bool = True) -> List[Tuple[str, str]]:
    """
    Generate a mix of controversial questions and concept pairs.
    
    Args:
        n_probes: Total number of probes to generate
        controversial_ratio: Fraction of probes that should be controversial (0-1)
        use_cache: Whether to use cached data (both concept pairs and controversial questions)
    
    Returns:
        List of tuples: either (question, "controversial") or (concept_a, concept_b)
    """
    n_controversial = int(n_probes * controversial_ratio)
    n_neutral = n_probes - n_controversial
    
    probes = []
    
    # Generate controversial probes (uses Claude + caching)
    if n_controversial > 0:
        controversial_probes = generate_controversial_probes(n_controversial, use_cache=use_cache)
        probes.extend(controversial_probes)
    
    # Generate neutral concept pairs (uses Claude + caching)
    if n_neutral > 0:
        neutral_probes = generate_probes_batch(n_neutral, use_cache=use_cache)
        probes.extend(neutral_probes)
    
    # Shuffle to mix them
    random.shuffle(probes)
    
    # Count actual probes (may be less than requested if not enough unique questions available)
    actual_controversial = sum(1 for p in probes if isinstance(p, tuple) and len(p) == 2 and p[1] == "controversial")
    actual_neutral = len(probes) - actual_controversial
    
    print(f"\n  Total: {actual_controversial} controversial probes + {actual_neutral} concept pairs = {len(probes)} probes")
    
    return probes


# ============================================================================
# EMBEDDING FUNCTIONS
# ============================================================================

def get_embedding(text: str) -> np.ndarray:
    """Get embedding from local LLM"""
    try:
        headers = {"Content-Type": "application/json"}
        payload = {
            "model": LOCAL_EMBEDDING_MODEL,
            "input": text
        }
        
        response = requests.post(
            LOCAL_EMBEDDING_URL,
            headers=headers,
            json=payload,
            timeout=30
        )
        
        if response.status_code == 200:
            embedding = response.json()['data'][0]['embedding']
            vec = np.array(embedding, dtype=float)
            # Normalize
            vec = vec / np.linalg.norm(vec)
            return vec
        else:
            print(f"  Warning: Embedding failed with status {response.status_code}")
            return None
            
    except Exception as e:
        print(f"  Error getting embedding: {e}")
        return None


# Maximum batch size for embedding requests
EMBEDDING_BATCH_SIZE = 300


def batch_embed(texts: List[str], show_progress: bool = True) -> List[Optional[np.ndarray]]:
    """
    Embed multiple texts in batches of up to EMBEDDING_BATCH_SIZE.
    
    Uses true batching - sends multiple texts in a single API call.
    Returns list of embeddings (or None for failures) in same order as input.
    """
    if not texts:
        return []
    
    all_embeddings = [None] * len(texts)
    
    # Process in batches
    n_batches = (len(texts) + EMBEDDING_BATCH_SIZE - 1) // EMBEDDING_BATCH_SIZE
    
    for batch_idx in range(n_batches):
        start_idx = batch_idx * EMBEDDING_BATCH_SIZE
        end_idx = min(start_idx + EMBEDDING_BATCH_SIZE, len(texts))
        batch_texts = texts[start_idx:end_idx]
        
        if show_progress and n_batches > 1:
            print(f"    Embedding batch {batch_idx + 1}/{n_batches} ({len(batch_texts)} texts)...")
        
        try:
            headers = {"Content-Type": "application/json"}
            payload = {
                "model": LOCAL_EMBEDDING_MODEL,
                "input": batch_texts  # Send multiple texts at once
            }
            
            response = requests.post(
                LOCAL_EMBEDDING_URL,
                headers=headers,
                json=payload,
                timeout=120  # Longer timeout for batch
            )
            
            if response.status_code == 200:
                data = response.json()['data']
                # API returns embeddings with index field - sort by index to maintain order
                sorted_data = sorted(data, key=lambda x: x.get('index', 0))
                
                for i, item in enumerate(sorted_data):
                    embedding = item['embedding']
                    vec = np.array(embedding, dtype=float)
                    # Normalize
                    norm = np.linalg.norm(vec)
                    if norm > 0:
                        vec = vec / norm
                    all_embeddings[start_idx + i] = vec
            else:
                print(f"    Warning: Batch embedding failed with status {response.status_code}")
                # Fall back to sequential for this batch
                for i, text in enumerate(batch_texts):
                    emb = get_embedding(text)
                    all_embeddings[start_idx + i] = emb
                    
        except Exception as e:
            print(f"    Error in batch embedding: {e}")
            # Fall back to sequential for this batch
            for i, text in enumerate(batch_texts):
                emb = get_embedding(text)
                all_embeddings[start_idx + i] = emb
    
    return all_embeddings


def batch_embed_with_fallback(texts: List[str], show_progress: bool = True) -> List[np.ndarray]:
    """
    Embed multiple texts with hash-based fallback for failures.
    Always returns an embedding for each input (never None).
    """
    embeddings = batch_embed(texts, show_progress)
    
    result = []
    for i, emb in enumerate(embeddings):
        if emb is not None:
            result.append(emb)
        else:
            # Fallback: use hash-based embedding
            import hashlib
            hash_obj = hashlib.sha256(texts[i].encode())
            hash_bytes = hash_obj.digest()
            vec = np.frombuffer(hash_bytes, dtype=np.uint8).astype(float)
            vec = vec / np.linalg.norm(vec)
            result.append(vec)
    
    return result

# ============================================================================
# PROBE GENERATION (using Claude)
# ============================================================================

def load_concept_pairs_cache() -> List[Tuple[str, str]]:
    """Load cached concept pairs from file if it exists"""
    cache_path = os.path.join(RESULTS_DIR, CONCEPT_PAIRS_CACHE_FILE)
    if os.path.exists(cache_path):
        try:
            with open(cache_path, 'r') as f:
                data = json.load(f)
                # Convert lists back to tuples
                pairs = [tuple(pair) for pair in data.get('pairs', [])]
                if pairs:
                    print(f"  ✓ Loaded {len(pairs)} concept pairs from cache")
                    return pairs
        except Exception as e:
            print(f"  Warning: Failed to load cache: {e}")
    return []

def save_concept_pairs_cache(pairs: List[Tuple[str, str]]):
    """Save concept pairs to cache file"""
    os.makedirs(RESULTS_DIR, exist_ok=True)
    cache_path = os.path.join(RESULTS_DIR, CONCEPT_PAIRS_CACHE_FILE)
    try:
        with open(cache_path, 'w') as f:
            json.dump({
                'pairs': pairs,
                'count': len(pairs),
                'generated_at': datetime.now().isoformat()
            }, f, indent=2)
        print(f"  ✓ Saved {len(pairs)} concept pairs to cache")
    except Exception as e:
        print(f"  Warning: Failed to save cache: {e}")

def find_latest_intermediate_results() -> Tuple[str, List[Dict], int]:
    """Find and load the intermediate results file
    
    Returns:
        Tuple of (filename, probes_list, num_completed) or (None, [], 0) if no valid file found
    """
    if not os.path.exists(RESULTS_DIR):
        return None, [], 0
    
    # Look for the single intermediate file
    filepath = os.path.join(RESULTS_DIR, "intermediate_latest.json")
    
    if not os.path.exists(filepath):
        return None, [], 0
    
    try:
        with open(filepath, 'r') as f:
            probes_data = json.load(f)
        
        if not isinstance(probes_data, list) or len(probes_data) == 0:
            return None, [], 0
        
        # Convert embeddings back to numpy arrays
        for probe in probes_data:
            if probe.get('final_embedding') is not None:
                probe['final_embedding'] = np.array(probe['final_embedding'])
            if probe.get('embeddings'):
                probe['embeddings'] = [np.array(e) for e in probe['embeddings']]
        
        return "intermediate_latest.json", probes_data, len(probes_data)
        
    except Exception as e:
        print(f"  Warning: Could not load intermediate_latest.json: {e}")
        return None, [], 0

def generate_probes_batch(n_probes: int, use_cache: bool = True) -> List[Tuple[str, str]]:
    """Generate all concept pairs upfront in one batch
    
    Args:
        n_probes: Number of concept pairs to generate
        use_cache: If True, check cache first and save generated pairs to cache
    
    Returns:
        List of (concept_a, concept_b) tuples
    """
    
    if not USE_CLAUDE_FOR_PROBES or not ANTHROPIC_API_KEY:
        # Use random from pool
        import random
        return [tuple(random.sample(CONCEPT_POOL, 2)) for _ in range(n_probes)]
    
    print(f"\n{'='*80}")
    print(f"GENERATING {n_probes} CONCEPT PAIRS WITH CLAUDE")
    print(f"{'='*80}")
    
    # Check cache first
    if use_cache:
        cached_pairs = load_concept_pairs_cache()
        if len(cached_pairs) >= n_probes:
            print(f"  Using {n_probes} cached concept pairs")
            return cached_pairs[:n_probes]
        elif cached_pairs:
            print(f"  Cache has {len(cached_pairs)} pairs, need {n_probes}. Regenerating...")
    
    system_prompt = """Generate diverse contrasting concept pairs for synthesis experiments.

Requirements:
1. Each pair should be from different domains
2. Should create interesting tension
3. Should be concrete, not abstract
4. Make them as diverse as possible

Format (one pair per line):
CONCEPT_A: [concept] | CONCEPT_B: [contrasting concept]

Generate exactly the requested number of pairs."""
    
    prompt = f"Generate {n_probes} diverse contrasting concept pairs."
    
    headers = {
        "x-api-key": ANTHROPIC_API_KEY,
        "anthropic-version": "2023-06-01",
        "Content-Type": "application/json"
    }
    
    # Request more tokens for batch generation
    max_tokens = min(4000, n_probes * 50)
    
    payload = {
        "model": CLAUDE_MODEL,
        "max_tokens": max_tokens,
        "temperature": 0.9,
        "system": system_prompt,
        "messages": [{"role": "user", "content": prompt}]
    }
    
    try:
        print("  Calling Claude API...")
        response = requests.post(
            "https://api.anthropic.com/v1/messages",
            headers=headers,
            json=payload,
            timeout=60
        )
        response.raise_for_status()
        text = response.json()['content'][0]['text'].strip()
        
        # Parse response
        pairs = []
        for line in text.split('\n'):
            line = line.strip()
            if not line or not '|' in line:
                continue
            
            parts = line.split('|')
            if len(parts) >= 2:
                concept_a = parts[0].split(':', 1)[-1].strip()
                concept_b = parts[1].split(':', 1)[-1].strip()
                pairs.append((concept_a, concept_b))
        
        # If we didn't get enough, fill with random
        import random
        while len(pairs) < n_probes:
            pairs.append(tuple(random.sample(CONCEPT_POOL, 2)))
        
        # Trim if we got too many
        pairs = pairs[:n_probes]
        
        print(f"  ✓ Generated {len(pairs)} concept pairs")
        print(f"\n  Examples:")
        for i, (a, b) in enumerate(pairs[:3]):
            print(f"    {i+1}. '{a}' vs '{b}'")
        
        # Save to cache for future use
        if use_cache:
            save_concept_pairs_cache(pairs)
        
        return pairs
        
    except Exception as e:
        print(f"  ✗ Error: {e}")
        print(f"  Falling back to random concept pool")
        import random
        return [tuple(random.sample(CONCEPT_POOL, 2)) for _ in range(n_probes)]

def generate_probe_with_claude() -> Tuple[str, str]:
    """Use Claude to generate a single random concept pair (legacy function)"""
    
    system_prompt = """Generate two contrasting concepts for synthesis.
Requirements:
1. Concepts should be from different domains
2. Should create interesting tension
3. Should be concrete, not abstract
4. One concept per line

Format:
CONCEPT_A: [specific concept]
CONCEPT_B: [contrasting concept]"""
    
    prompt = "Generate two contrasting concepts for synthesis."
    
    headers = {
        "x-api-key": ANTHROPIC_API_KEY,
        "anthropic-version": "2023-06-01",
        "Content-Type": "application/json"
    }
    
    payload = {
        "model": CLAUDE_MODEL,
        "max_tokens": 100,
        "temperature": 0.9,
        "system": system_prompt,
        "messages": [{"role": "user", "content": prompt}]
    }
    
    try:
        response = requests.post(
            "https://api.anthropic.com/v1/messages",
            headers=headers,
            json=payload,
            timeout=30
        )
        response.raise_for_status()
        text = response.json()['content'][0]['text'].strip()
        
        # Parse response
        lines = [l.strip() for l in text.split('\n') if l.strip()]
        concept_a = lines[0].split(':', 1)[-1].strip() if len(lines) > 0 else "innovation"
        concept_b = lines[1].split(':', 1)[-1].strip() if len(lines) > 1 else "tradition"
        
        return concept_a, concept_b
        
    except Exception as e:
        print(f"  Warning: Claude probe generation failed ({e}), using random from pool")
        import random
        return random.sample(CONCEPT_POOL, 2)

# ============================================================================
# UNITY IR SYSTEM PROMPT (shared between target and reference models)
# ============================================================================

UNITY_IR_SYSTEM_PROMPT = """You generate Unity game behavior JSON in natural language format.

Output JSON with:
- "class_name": name of the behavior class
- "components": array of Unity component names
- "fields": array of field definitions with name, type, default
- "behaviors": array of behavior objects with name, trigger, condition, actions
- "state": optional state machine object with has_state_machine, states

CRITICAL: Use NATURAL LANGUAGE, not programming syntax.
- NO operators like ==, <, >, ||
- NO Unity API calls like Vector3.up, Time.deltaTime
- NO function calls like distance(player)
- NO template syntax like {{360 * Time.deltaTime}}
- NO method names like on_trigger_enter

Example GOOD output:
{
  "class_name": "ProximityLight",
  "components": ["Light"],
  "fields": [
    {"name": "detectionRadius", "type": "float", "default": 5}
  ],
  "behaviors": [
    {
      "name": "activate_on_proximity",
      "trigger": "player enters detectionRadius",
      "condition": "light is off",
      "actions": [{"type": "enable", "params": {"target": "light"}}]
    }
  ]
}

Output ONLY valid JSON, no markdown, no explanations."""

# ============================================================================
# BASELINE GENERATION FUNCTIONS
# ============================================================================

def call_reference_model(prompt: str) -> Optional[str]:
    """
    Call reference model (Claude Haiku) to generate baseline JSON.
    Falls back to local model if Anthropic API unavailable.
    """
    # Try Anthropic first
    if ANTHROPIC_API_KEY:
        try:
            response = requests.post(
                ANTHROPIC_API_URL,
                headers={
                    "x-api-key": ANTHROPIC_API_KEY,
                    "content-type": "application/json",
                    "anthropic-version": "2023-06-01"
                },
                json={
                    "model": REFERENCE_MODEL,
                    "max_tokens": 2048,
                    "system": UNITY_IR_SYSTEM_PROMPT,
                    "messages": [
                        {"role": "user", "content": f"Generate Unity behavior JSON for: {prompt}"}
                    ]
                },
                timeout=30
            )
            response.raise_for_status()
            return response.json()["content"][0]["text"].strip()
        except Exception as e:
            print(f"  Anthropic API failed: {e}, trying local fallback")
    
    # Fallback to local
    try:
        response = requests.post(
            LOCAL_REFERENCE_URL,
            json={
                "model": LOCAL_REFERENCE_MODEL,
                "messages": [
                    {"role": "system", "content": UNITY_IR_SYSTEM_PROMPT},
                    {"role": "user", "content": f"Generate Unity behavior JSON for: {prompt}"}
                ],
                "temperature": 0.3,
                "max_tokens": 2048
            },
            timeout=60
        )
        response.raise_for_status()
        return response.json()["choices"][0]["message"]["content"].strip()
    except Exception as e:
        print(f"  Local reference model failed: {e}")
        return None


def extract_json_from_response(response: str) -> Optional[Dict]:
    """Extract JSON from model response"""
    # Direct parse
    try:
        return json.loads(response)
    except json.JSONDecodeError:
        pass
    
    # Find in markdown
    match = re.search(r'```(?:json)?\s*([\s\S]*?)\s*```', response)
    if match:
        try:
            return json.loads(match.group(1))
        except json.JSONDecodeError:
            pass
    
    # Find raw object
    match = re.search(r'\{[\s\S]*\}', response)
    if match:
        try:
            return json.loads(match.group(0))
        except json.JSONDecodeError:
            pass
    
    return None


def extract_structural_features(parsed: Dict, prompt: str, prompt_hash: str) -> Dict:
    """Extract structural features from parsed JSON for baseline"""
    
    fields = parsed.get("fields", [])
    behaviors = parsed.get("behaviors", [])
    state = parsed.get("state", {})
    
    return {
        "prompt": prompt,
        "prompt_hash": prompt_hash,
        "n_fields": len(fields),
        "n_behaviors": len(behaviors),
        "n_components": len(parsed.get("components", [])),
        "has_state_machine": state.get("has_state_machine", False) if isinstance(state, dict) else False,
        "n_states": len(state.get("states", [])) if isinstance(state, dict) else 0,
        "field_names": [f.get("name", "") for f in fields if isinstance(f, dict)],
        "component_names": parsed.get("components", []),
        "action_types": list(set(
            a.get("type", "")
            for b in behaviors
            if isinstance(b, dict)
            for a in b.get("actions", [])
            if isinstance(a, dict)
        )),
        "actions_per_behavior": [
            len(b.get("actions", [])) for b in behaviors if isinstance(b, dict)
        ],
    }


def generate_baseline(prompt: str, cache_dir: str = BASELINE_CACHE_DIR) -> Optional[Dict]:
    """
    Generate baseline from reference model and cache it.
    Returns extracted structural features.
    """
    cache_path = Path(cache_dir)
    cache_path.mkdir(parents=True, exist_ok=True)
    
    prompt_hash = hashlib.md5(prompt.encode()).hexdigest()[:12]
    cache_file = cache_path / f"{prompt_hash}.json"
    
    # Check cache first
    if cache_file.exists():
        try:
            with open(cache_file) as f:
                return json.load(f)
        except:
            pass
    
    # Generate from reference model
    response = call_reference_model(prompt)
    if response is None:
        return None
    
    # Parse JSON
    parsed = extract_json_from_response(response)
    if parsed is None:
        return None
    
    # Extract structural features
    baseline = extract_structural_features(parsed, prompt, prompt_hash)
    
    # Cache it
    with open(cache_file, 'w') as f:
        json.dump(baseline, f, indent=2)
    
    return baseline


# ============================================================================
# SYNTHESIS FUNCTION (using LOCAL model)
# ============================================================================

def synthesize_concepts(concept_a: str, concept_b: str) -> str:
    """
    Use LOCAL LLM to synthesize two concepts, answer a controversial question,
    or generate Unity IR JSON for a behavior description.
    
    If concept_b == "controversial", then concept_a is actually a question.
    If concept_b == "structural" or "code_leak", then concept_a is a Unity behavior prompt.
    """
    
    headers = {"Content-Type": "application/json"}
    
    # Check if this is a Unity IR probe
    if concept_b in ["structural", "code_leak"]:
        # This is a Unity behavior prompt - generate IR JSON
        system_prompt = UNITY_IR_SYSTEM_PROMPT
        
        prompt = f"Generate Unity behavior JSON for: {concept_a}"
        
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": prompt}
        ]
    # Check if this is a controversial question
    elif concept_b == "controversial":
        # This is a controversial question - ask it directly
        system_prompt = """You are a thoughtful assistant. Answer the question directly and honestly.
Give your perspective clearly. Be concise (under 100 words)."""
        
        prompt = concept_a  # The question itself
        
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": prompt}
        ]
    else:
        # Normal concept synthesis
        system_prompt = """You are a synthesis engine. Given two concepts, create a novel synthesis.

Requirements:
1. Find the core tension between them
2. Propose a concrete mechanism that addresses both
3. Be specific and actionable
4. Maximum 80 words

Format:
SYNTHESIS: [one sentence describing the new concept]
MECHANISM: [how it works concretely]"""
        
        prompt = f"Synthesize these concepts:\nA: {concept_a}\nB: {concept_b}"
        
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": prompt}
        ]
    
    payload = {
        "model": LOCAL_SYNTHESIS_MODEL,
        "messages": messages,
        "temperature": 0.8,
        "max_tokens": 300
    }
    
    try:
        response = requests.post(
            LOCAL_SYNTHESIS_URL,
            headers=headers,
            json=payload,
            timeout=120  # Local models can be slower
        )
        response.raise_for_status()
        content = response.json()['choices'][0]['message']['content'].strip()
        
        # For Unity IR, try to extract JSON if wrapped in markdown
        if concept_b in ["structural", "code_leak"]:
            # Try to extract JSON from markdown code blocks
            json_match = re.search(r'```(?:json)?\s*(\{.*?\})\s*```', content, re.DOTALL)
            if json_match:
                content = json_match.group(1)
            # Or try to find JSON object directly
            json_match = re.search(r'\{.*\}', content, re.DOTALL)
            if json_match:
                content = json_match.group(0)
        
        return content
    except Exception as e:
        print(f"  Error with local model: {e}")
        if concept_b == "controversial":
            return f"[Response to: {concept_a}]"
        elif concept_b in ["structural", "code_leak"]:
            return f'{{"trigger": "[Error generating for: {concept_a}]", "actions": []}}'
        return f"[Synthesis of {concept_a} and {concept_b}]"

# ============================================================================
# PROBING FUNCTION
# ============================================================================

def run_probe(probe_id: int, concept_a: str, concept_b: str) -> Dict:
    """
    Run one probe: iterate synthesis N times and track trajectory
    Uses LOCAL model for synthesis iterations
    
    If concept_b == "controversial", this is a controversial question probe.
    If concept_b in ["structural", "code_leak"], this is a Unity IR probe.
    For controversial probes, we also collect sentence-level embeddings to
    enable empirical hedging detection.
    For Unity IR probes, we detect code leak markers.
    """
    
    is_controversial = (concept_b == "controversial")
    is_unity_ir = (concept_b in ["structural", "code_leak"])
    
    if is_controversial:
        print(f"\nProbe {probe_id} [CONTROVERSIAL]: '{concept_a}'")
    elif is_unity_ir:
        print(f"\nProbe {probe_id} [UNITY IR - {concept_b.upper()}]: '{concept_a}'")
    else:
        print(f"\nProbe {probe_id}: '{concept_a}' vs '{concept_b}'")
    
    # SAVE ORIGINAL CONCEPTS BEFORE THEY GET OVERWRITTEN
    original_concept_a = concept_a
    original_concept_b = concept_b
    
    trajectory = []
    embeddings = []
    sentence_data = []  # For sentence-level hedging detection
    code_leak_markers = []  # For Unity IR code leak detection
    
    # Initial concepts
    current = f"{concept_a} vs {concept_b}"
    
    # Iterate synthesis with LOCAL model
    for iteration in range(N_ITERATIONS):
        print(f"  Iteration {iteration + 1}/{N_ITERATIONS}...", end=" ")
        
        # Synthesize with LOCAL model
        synthesis = synthesize_concepts(concept_a, concept_b)
        trajectory.append(synthesis)
        
        # Get embedding from LOCAL model (full response)
        embedding = get_embedding(synthesis)
        if embedding is not None:
            embeddings.append(embedding)
        
        # For controversial probes, also embed individual sentences
        # This enables empirical hedging detection
        if is_controversial:
            sentences = segment_into_sentences(synthesis)
            if sentences:
                # Batch embed all sentences at once
                sent_embeddings = batch_embed(sentences, show_progress=False)
                for sentence, sent_embedding in zip(sentences, sent_embeddings):
                    if sent_embedding is not None:
                        sentence_data.append({
                            "sentence": sentence,
                            "embedding": sent_embedding,
                            "topic": original_concept_a[:50]  # Use question as topic identifier
                        })
        
        # For Unity IR probes, detect code leak markers
        if is_unity_ir:
            markers = detect_code_markers(synthesis)
            code_leak_markers.extend(markers)
            if markers:
                print(f" (code leak markers: {len(markers)})", end="")
        
        # Update for next iteration (use synthesis as new input)
        # For Unity IR, don't iterate - just use the single response
        if not is_unity_ir:
            concept_a = synthesis[:50]  # Use first part as concept A
            concept_b = synthesis[50:100] if len(synthesis) > 50 else synthesis  # Second part as B
        
        print(f"Done. Length: {len(synthesis)} chars" + 
              (f", {len(sentence_data)} sentences" if is_controversial else "") +
              (f", {len(code_leak_markers)} code markers" if is_unity_ir and code_leak_markers else ""))
        
        # Rate limit
        time.sleep(0.5)
    
    result = {
        "probe_id": probe_id,
        "initial_a": original_concept_a,  # Use saved original
        "initial_b": original_concept_b,  # Use saved original
        "probe_type": original_concept_b if original_concept_b in ["controversial", "structural", "code_leak"] else "neutral",
        "trajectory": trajectory,
        "embeddings": embeddings,
        "final_embedding": embeddings[-1] if embeddings else None,
        "response": trajectory[-1] if trajectory else None,  # Store final response for structural clustering
        "prompt": original_concept_a  # Store prompt for baseline comparison
    }
    
    # Generate baseline from reference model (if enabled and Unity IR)
    if is_unity_ir and GENERATE_BASELINES:
        baseline = generate_baseline(original_concept_a)
        if baseline:
            result["baseline"] = baseline
            print(f"  Baseline: {baseline['n_fields']}f, {baseline['n_behaviors']}b, sm={baseline['has_state_machine']}")
    
    # Add sentence data for controversial probes (for hedge detection)
    if is_controversial and sentence_data:
        result["sentence_data"] = sentence_data
    
    # Add code leak markers for Unity IR probes
    if is_unity_ir and code_leak_markers:
        result["code_leak_markers"] = code_leak_markers
    
    return result

# ============================================================================
# STRUCTURAL ANALYSIS FUNCTIONS
# ============================================================================

def extract_json_features(json_str: str) -> Optional[Dict]:
    """
    Extract structural features from Unity IR JSON.
    
    Returns:
        Dict with features like:
        - n_fields: number of fields
        - n_behaviors: number of behaviors
        - has_state_machine: boolean
        - components: tuple of component names
        - action_types: tuple of action types
        - has_conditions: boolean
        - max_actions_per_behavior: int
    """
    try:
        data = json.loads(json_str)
    except (json.JSONDecodeError, TypeError):
        return None
    
    features = {}
    
    # Count fields
    if isinstance(data, dict):
        # Count top-level fields (excluding behaviors)
        fields = data.get("fields", [])
        if isinstance(fields, list):
            features["n_fields"] = len(fields)
        else:
            features["n_fields"] = 0
        
        # Count behaviors
        behaviors = data.get("behaviors", [])
        if isinstance(behaviors, list):
            features["n_behaviors"] = len(behaviors)
        else:
            features["n_behaviors"] = 0
        
        # Check for state machine patterns
        # Look for state-related fields or multiple behaviors with state transitions
        has_state = False
        if "state" in data or "states" in data:
            has_state = True
        elif features["n_behaviors"] > 2:
            # Multiple behaviors might indicate state machine
            # Check if behaviors reference each other or have state-like names
            behavior_names = [b.get("name", "").lower() if isinstance(b, dict) else "" 
                            for b in behaviors]
            state_keywords = ["state", "idle", "patrol", "chase", "attack", "transition"]
            if any(keyword in name for name in behavior_names for keyword in state_keywords):
                has_state = True
        
        features["has_state_machine"] = has_state
        
        # Extract components
        components = data.get("components", [])
        if isinstance(components, list):
            features["components"] = tuple(sorted([str(c).lower() for c in components]))
        else:
            features["components"] = tuple()
        
        # Extract action types from all behaviors
        action_types = set()
        max_actions = 0
        has_conditions = False
        
        for behavior in behaviors:
            if not isinstance(behavior, dict):
                continue
            
            # Check for conditions
            if behavior.get("condition") is not None:
                has_conditions = True
            
            # Extract actions
            actions = behavior.get("actions", [])
            if isinstance(actions, list):
                max_actions = max(max_actions, len(actions))
                for action in actions:
                    if isinstance(action, dict):
                        action_type = action.get("type", "")
                        if action_type:
                            action_types.add(str(action_type).lower())
        
        features["action_types"] = tuple(sorted(action_types))
        features["max_actions_per_behavior"] = max_actions
        features["has_conditions"] = has_conditions
        
        # Additional complexity metrics
        features["total_actions"] = sum(
            len(b.get("actions", [])) if isinstance(b, dict) else 0
            for b in behaviors
        )
        
        # Check for class_name (indicates structured output)
        features["has_class_name"] = "class_name" in data
        
    else:
        # Not a dict, minimal features
        features = {
            "n_fields": 0,
            "n_behaviors": 0,
            "has_state_machine": False,
            "components": tuple(),
            "action_types": tuple(),
            "max_actions_per_behavior": 0,
            "has_conditions": False,
            "total_actions": 0,
            "has_class_name": False
        }
    
    return features


def extract_structure_vector(parsed: Dict) -> Optional[List]:
    """
    Convert JSON structure to numeric vector for clustering.
    """
    try:
        fields = parsed.get("fields", [])
        behaviors = parsed.get("behaviors", [])
        state = parsed.get("state", {})
        components = parsed.get("components", [])
        
        # Numeric features
        n_fields = len(fields)
        n_behaviors = len(behaviors)
        n_components = len(components)
        has_sm = 1 if (isinstance(state, dict) and state.get("has_state_machine", False)) else 0
        n_states = len(state.get("states", [])) if isinstance(state, dict) else 0
        
        # Action count features
        action_counts = [len(b.get("actions", [])) for b in behaviors if isinstance(b, dict)]
        total_actions = sum(action_counts)
        max_actions = max(action_counts) if action_counts else 0
        avg_actions = total_actions / len(behaviors) if behaviors else 0
        
        # Component flags (common ones)
        has_rigidbody = 1 if "Rigidbody" in components else 0
        has_collider = 1 if any("Collider" in str(c) for c in components) else 0
        has_navmesh = 1 if "NavMeshAgent" in components else 0
        has_animator = 1 if "Animator" in components else 0
        
        # Field name flags (common patterns)
        field_names_lower = {f.get("name", "").lower() for f in fields if isinstance(f, dict)}
        has_player_field = 1 if "player" in field_names_lower else 0
        has_speed_field = 1 if any("speed" in fn for fn in field_names_lower) else 0
        has_health_field = 1 if "health" in field_names_lower else 0
        
        return [
            n_fields,
            n_behaviors,
            n_components,
            has_sm,
            n_states,
            total_actions,
            max_actions,
            avg_actions,
            has_rigidbody,
            has_collider,
            has_navmesh,
            has_animator,
            has_player_field,
            has_speed_field,
            has_health_field,
        ]
    except:
        return None


def extract_common_pattern(probes: List[Dict]) -> Dict:
    """
    Extract common structural pattern from a cluster of probes.
    """
    pattern = {}
    
    # Parse all JSONs
    parsed_list = []
    for probe in probes:
        try:
            response = probe.get("response") or (probe.get("trajectory", [])[-1] if probe.get("trajectory") else None)
            if response is None:
                continue
            parsed = json.loads(response) if isinstance(response, str) else response
            parsed_list.append(parsed)
        except:
            continue
    
    if not parsed_list:
        return pattern
    
    n = len(parsed_list)
    
    # Field count stats
    field_counts = [len(p.get("fields", [])) for p in parsed_list]
    pattern["field_count_range"] = [min(field_counts), max(field_counts)]
    pattern["field_count_avg"] = sum(field_counts) / n
    
    # Behavior count stats
    behavior_counts = [len(p.get("behaviors", [])) for p in parsed_list]
    pattern["behavior_count_range"] = [min(behavior_counts), max(behavior_counts)]
    pattern["behavior_count_avg"] = sum(behavior_counts) / n
    
    # State machine prevalence
    sm_count = sum(1 for p in parsed_list 
                   if isinstance(p.get("state"), dict) and p.get("state", {}).get("has_state_machine", False))
    sm_rate = sm_count / n
    if sm_rate > 0.8:
        pattern["always_has_state_machine"] = True
    elif sm_rate < 0.2:
        pattern["never_has_state_machine"] = True
    pattern["state_machine_rate"] = sm_rate
    
    # Common components (appear in >60% of cluster)
    component_counter = Counter()
    for p in parsed_list:
        component_counter.update(p.get("components", []))
    common_components = [c for c, count in component_counter.items() if count / n > 0.6]
    if common_components:
        pattern["common_components"] = common_components
    
    # Common field names (appear in >50% of cluster)
    field_name_counter = Counter()
    for p in parsed_list:
        field_name_counter.update(f.get("name", "").lower() for f in p.get("fields", []) if isinstance(f, dict))
    common_fields = [f for f, count in field_name_counter.items() if count / n > 0.5 and f]
    if common_fields:
        pattern["common_field_names"] = common_fields
    
    # Common action types (appear in >50% of cluster)
    action_type_counter = Counter()
    for p in parsed_list:
        for b in p.get("behaviors", []):
            if isinstance(b, dict):
                action_type_counter.update(a.get("type", "") for a in b.get("actions", []) if isinstance(a, dict))
    common_actions = [a for a, count in action_type_counter.items() if count / n > 0.5 and a]
    if common_actions:
        pattern["common_action_types"] = common_actions
    
    return pattern


def name_structural_pattern(pattern: Dict) -> str:
    """Generate a descriptive name for a structural pattern"""
    
    parts = []
    
    # State machine characteristic
    if pattern.get("always_has_state_machine"):
        parts.append("stateful")
    elif pattern.get("never_has_state_machine"):
        parts.append("stateless")
    
    # Complexity
    avg_fields = pattern.get("field_count_avg", 0)
    avg_behaviors = pattern.get("behavior_count_avg", 0)
    
    if avg_fields < 2 and avg_behaviors < 2:
        parts.append("minimal")
    elif avg_fields > 4 or avg_behaviors > 4:
        parts.append("complex")
    else:
        parts.append("moderate")
    
    # Common components
    common_comps = pattern.get("common_components", [])
    if "Rigidbody" in common_comps:
        parts.append("physics")
    if "NavMeshAgent" in common_comps:
        parts.append("navigation")
    if "Animator" in common_comps:
        parts.append("animated")
    
    return "_".join(parts) if parts else "generic"


def cluster_by_structure(probes: List[Dict], n_clusters: int = None) -> Dict:
    """
    Cluster probes by JSON structure to find structural clichés.
    
    Returns dict of structural attractors with patterns.
    """
    print("\n" + "="*80)
    print("STRUCTURAL CLUSTERING")
    print("="*80)
    
    # Extract structure vectors from all Unity IR probes
    vectors = []
    valid_probes = []
    
    for probe in probes:
        # Skip non-Unity IR probes
        probe_type = probe.get("probe_type", "")
        if probe_type not in ["structural", "code_leak"]:
            continue
        
        try:
            response = probe.get("response") or (probe.get("trajectory", [])[-1] if probe.get("trajectory") else None)
            if response is None:
                continue
            parsed = json.loads(response) if isinstance(response, str) else response
            vec = extract_structure_vector(parsed)
            if vec:
                vectors.append(vec)
                valid_probes.append({
                    "response": response,
                    "prompt": probe.get("initial_a", "unknown")[:50]
                })
        except:
            continue
    
    if n_clusters is None:
        n_clusters = N_STRUCTURAL_CLUSTERS
    
    if len(vectors) < n_clusters * 2:
        print(f"  Not enough valid probes for structural clustering ({len(vectors)})")
        return {}
    
    print(f"\n  Analyzing {len(vectors)} probes")
    print(f"  Clustering into {n_clusters} groups...")
    
    # Cluster
    vectors_array = np.array(vectors)
    scaler = StandardScaler()
    vectors_normalized = scaler.fit_transform(vectors_array)
    
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    labels = kmeans.fit_predict(vectors_normalized)
    
    # Analyze each cluster for patterns
    structural_attractors = {}
    
    for cluster_id in range(n_clusters):
        cluster_mask = labels == cluster_id
        cluster_probes = [p for p, m in zip(valid_probes, cluster_mask) if m]
        cluster_size = len(cluster_probes)
        
        if cluster_size < 3:
            continue
        
        prevalence = cluster_size / len(valid_probes)
        
        # Extract common pattern from cluster
        pattern = extract_common_pattern(cluster_probes)
        
        # Name the pattern based on characteristics
        pattern_name = name_structural_pattern(pattern)
        
        structural_attractors[f"structural_{cluster_id}"] = {
            "name": pattern_name,
            "type": "structural_cliche",
            "pattern": pattern,
            "prevalence": prevalence,
            "sample_count": cluster_size,
            "sample_prompts": [p["prompt"] for p in cluster_probes[:3]],
        }
    
    print(f"\n  Found {len(structural_attractors)} structural patterns:")
    for name, attractor in structural_attractors.items():
        print(f"\n  {attractor['name']} ({attractor['prevalence']*100:.1f}% of outputs)")
        pattern = attractor['pattern']
        print(f"    Fields: {pattern.get('field_count_range', '?')}, avg={pattern.get('field_count_avg', 0):.1f}")
        print(f"    Behaviors: {pattern.get('behavior_count_range', '?')}, avg={pattern.get('behavior_count_avg', 0):.1f}")
        if pattern.get('common_components'):
            print(f"    Common components: {pattern['common_components']}")
        if pattern.get('common_field_names'):
            print(f"    Common fields: {pattern['common_field_names']}")
    
    return structural_attractors


# ============================================================================
# ANALYSIS FUNCTIONS
# ============================================================================

def cluster_attractors(final_embeddings: np.ndarray, texts: List[str], n_clusters: int = None) -> Dict:
    """
    Cluster final embeddings to find Lagrange points using KMeans
    """
    print("\n" + "="*80)
    print("CLUSTERING ANALYSIS")
    print("="*80)
    
    # Determine number of clusters
    if n_clusters is None:
        # Auto-detect: 1 cluster per 50 samples, min 2, max 10
        n_clusters = max(2, min(10, len(final_embeddings) // 50))
    
    print(f"\nClustering into {n_clusters} groups...")
    
    # KMeans clustering
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    original_labels = kmeans.fit_predict(final_embeddings)
    
    # Reorder clusters by size (0 = largest)
    cluster_sizes = [(i, (original_labels == i).sum()) for i in range(n_clusters)]
    cluster_sizes.sort(key=lambda x: x[1], reverse=True)
    old_to_new = {old: new for new, (old, _) in enumerate(cluster_sizes)}
    labels = np.array([old_to_new[l] for l in original_labels])
    
    # Reorder centroids
    new_centroids = np.array([kmeans.cluster_centers_[old] for old, _ in cluster_sizes])
    
    print(f"Found {n_clusters} clusters (Lagrange points)")
    
    # Analyze each cluster (now ordered by size, 0 = largest)
    clusters = {}
    for new_id in range(n_clusters):
        mask = labels == new_id
        cluster_texts = [texts[i] for i in range(len(texts)) if mask[i]]
        cluster_embeddings = final_embeddings[mask]
        
        if len(cluster_texts) == 0:
            continue
        
        centroid = new_centroids[new_id]
        
        # Calculate cluster statistics
        distances = [np.linalg.norm(emb - centroid) for emb in cluster_embeddings]
        
        clusters[new_id] = {
            "size": len(cluster_texts),
            "percentage": len(cluster_texts) / len(texts) * 100,
            "texts": cluster_texts,
            "centroid": centroid,
            "avg_distance": np.mean(distances),
            "max_distance": np.max(distances)
        }
    
    return {
        "clusters": clusters,
        "labels": labels,
        "n_clusters": n_clusters
    }

def extract_keywords(texts: List[str], top_n: int = 10) -> List[Tuple[str, int]]:
    """Extract common keywords from cluster texts"""
    # Simple keyword extraction (word frequency)
    words = []
    for text in texts:
        # Lowercase, split, filter
        text_words = text.lower().split()
        text_words = [w.strip('.,!?;:()[]{}') for w in text_words]
        text_words = [w for w in text_words if len(w) > 3]  # Filter short words
        words.extend(text_words)
    
    # Count
    counter = Counter(words)
    return counter.most_common(top_n)

def visualize_clusters(embeddings: np.ndarray, labels: np.ndarray, output_path: str):
    """Visualize clusters in 2D using PCA"""
    print("\nGenerating visualization...")
    
    # Reduce to 2D
    pca = PCA(n_components=2)
    coords_2d = pca.fit_transform(embeddings)
    
    # Plot
    plt.figure(figsize=(12, 8))
    
    # Plot each cluster
    unique_labels = set(labels)
    colors = plt.cm.rainbow(np.linspace(0, 1, len(unique_labels)))
    
    for label, color in zip(unique_labels, colors):
        if label == -1:
            # Noise points in black
            color = 'black'
            marker = 'x'
            label_text = 'Noise'
        else:
            marker = 'o'
            label_text = f'Cluster {label}'
        
        mask = labels == label
        plt.scatter(
            coords_2d[mask, 0],
            coords_2d[mask, 1],
            c=[color],
            marker=marker,
            s=100,
            alpha=0.6,
            label=label_text
        )
    
    plt.xlabel(f'PC1 ({pca.explained_variance_ratio_[0]:.1%} variance)')
    plt.ylabel(f'PC2 ({pca.explained_variance_ratio_[1]:.1%} variance)')
    plt.title('LLM Idea Space: Lagrange Points (Attractors)')
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.tight_layout()
    plt.savefig(output_path, dpi=300)
    print(f"Visualization saved to {output_path}")

# ============================================================================
# MAIN EXPERIMENT
# ============================================================================

def run_experiment():
    """Main experiment loop"""
    
    print("="*80)
    print("LAGRANGE POINT MAPPING EXPERIMENT")
    print("="*80)
    print(f"\nConfiguration:")
    print(f"  LOCAL Synthesis Model: {LOCAL_SYNTHESIS_MODEL}")
    print(f"  LOCAL Embedding Model: {LOCAL_EMBEDDING_MODEL}")
    if USE_CLAUDE_FOR_PROBES:
        print(f"  Claude Probe Generator: {CLAUDE_MODEL}")
    else:
        print(f"  Probe Source: Random from concept pool")
    print(f"  Number of probes: {N_PROBES}")
    print(f"  Iterations per probe: {N_ITERATIONS}")
    print(f"  Number of clusters: {N_CLUSTERS if N_CLUSTERS else 'auto-detect'}")
    if USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
        n_controversial = int(N_PROBES * CONTROVERSIAL_PROBE_RATIO)
        n_neutral = N_PROBES - n_controversial
        print(f"  Controversial probes: {n_controversial} ({CONTROVERSIAL_PROBE_RATIO*100:.0f}%)")
        print(f"  Neutral probes: {n_neutral} ({(1-CONTROVERSIAL_PROBE_RATIO)*100:.0f}%)")
    print(f"  Timestamp: {TIMESTAMP}")
    
    # Check APIs
    if USE_CLAUDE_FOR_PROBES and not ANTHROPIC_API_KEY:
        print("\n Warning: ANTHROPIC_API_KEY not set, will use random concept pool for probes")
    
    # Create output directory
    os.makedirs(RESULTS_DIR, exist_ok=True)
    
    # Check for existing intermediate results to resume from
    all_probes = []
    start_index = 0
    
    if RESUME_FROM_PREVIOUS:
        resume_file, previous_probes, num_completed = find_latest_intermediate_results()
        if resume_file and num_completed > 0:
            print(f"\n{'='*80}")
            print(f"RESUMING FROM PREVIOUS RUN")
            print(f"{'='*80}")
            print(f"  Found: {resume_file}")
            print(f"  Completed probes: {num_completed}/{N_PROBES}")
            
            if num_completed >= N_PROBES:
                print(f"  ✓ All {N_PROBES} probes already completed!")
                all_probes = previous_probes[:N_PROBES]
                start_index = N_PROBES  # Skip the probe loop entirely
            else:
                all_probes = previous_probes
                start_index = num_completed
                print(f"  Resuming from probe {start_index + 1}...")
    
    # Generate all probes upfront (mixed, neutral-only, or Unity IR)
    if PROBE_MODE == "unity_ir":
        print(f"\n{'='*80}")
        print(f"GENERATING UNITY IR PROBES")
        print(f"{'='*80}")
        concept_pairs = generate_unity_behavior_probes(N_PROBES)
    elif USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
        print(f"\n{'='*80}")
        print(f"GENERATING MIXED PROBES ({CONTROVERSIAL_PROBE_RATIO*100:.0f}% controversial)")
        print(f"{'='*80}")
        concept_pairs = generate_mixed_probes(N_PROBES, CONTROVERSIAL_PROBE_RATIO)
    else:
        concept_pairs = generate_probes_batch(N_PROBES)
    
    # Run probes
    remaining = N_PROBES - start_index
    if remaining > 0:
        print(f"\n{'='*80}")
        print(f"RUNNING {remaining} PROBES" + (f" (resuming from {start_index + 1})" if start_index > 0 else ""))
        print(f"{'='*80}")
    
    for i in range(start_index, N_PROBES):
        # Use pre-generated concept pair
        concept_a, concept_b = concept_pairs[i]
        probe_result = run_probe(i + 1, concept_a, concept_b)
        all_probes.append(probe_result)
        
        # Save intermediate results every 10 probes (overwrites single file)
        if (i + 1) % 10 == 0:
            intermediate_path = f"{RESULTS_DIR}/intermediate_latest.json"
            with open(intermediate_path, 'w') as f:
                # Convert numpy arrays to lists for JSON
                save_data = []
                for p in all_probes:
                    p_copy = p.copy()
                    if p_copy['final_embedding'] is not None:
                        p_copy['final_embedding'] = p_copy['final_embedding'].tolist()
                    p_copy['embeddings'] = [e.tolist() for e in p_copy['embeddings']]
                    # Handle sentence_data for controversial probes
                    if 'sentence_data' in p_copy:
                        p_copy['sentence_data'] = [
                            {
                                "sentence": sd["sentence"],
                                "embedding": sd["embedding"].tolist() if hasattr(sd["embedding"], 'tolist') else sd["embedding"],
                                "topic": sd["topic"]
                            }
                            for sd in p_copy['sentence_data']
                        ]
                    save_data.append(p_copy)
                json.dump(save_data, f, indent=2)
            print(f"\n  → Saved intermediate results ({i+1} probes)")
    
    # Extract final embeddings and texts
    final_embeddings = []
    final_texts = []
    
    for probe in all_probes:
        if probe['final_embedding'] is not None:
            final_embeddings.append(probe['final_embedding'])
            final_texts.append(probe['trajectory'][-1] if probe['trajectory'] else "")
    
    final_embeddings = np.array(final_embeddings)
    
    print(f"\n{'='*80}")
    print(f"ANALYSIS")
    print(f"{'='*80}")
    print(f"\nSuccessful probes: {len(final_embeddings)}/{N_PROBES}")
    
    # Cluster analysis (embedding-based, for all probes)
    cluster_results = cluster_attractors(final_embeddings, final_texts, n_clusters=N_CLUSTERS)
    
    # =========================================================================
    # STRUCTURAL ANALYSIS: Analyze structural probes by JSON shape
    # =========================================================================
    structural_results = None
    if PROBE_MODE == "unity_ir":
        print(f"\n{'='*80}")
        print("STRUCTURAL PATTERN DETECTION")
        print(f"{'='*80}")
        
        structural_results = cluster_by_structure(all_probes, n_clusters=min(6, len(all_probes) // 10))
        
        if structural_results:
            # Save structural patterns for reference
            structural_patterns_path = f"{RESULTS_DIR}/structural_patterns_{TIMESTAMP}.json"
            with open(structural_patterns_path, 'w') as f:
                json.dump(structural_results, f, indent=2, default=str)
            print(f"\n  ✓ Saved structural patterns to: {structural_patterns_path}")
    
    # =========================================================================
    # CODE LEAK DETECTION: Analyze Unity IR responses for code leak patterns
    # =========================================================================
    code_leak_results = None
    if PROBE_MODE == "unity_ir" and USE_CODE_LEAK_DETECTION:
        print(f"\n{'='*80}")
        print("CODE LEAK DETECTION (Empirical)")
        print(f"{'='*80}")
        
        # Collect all responses from code leak probes
        responses_to_embed = []
        response_metadata = []
        
        for probe in all_probes:
            if probe.get("probe_type") == "code_leak" and probe.get("trajectory"):
                response = probe["trajectory"][-1] if probe["trajectory"] else ""
                if response:
                    responses_to_embed.append(response)
                    response_metadata.append({
                        "response": response,
                        "topic": probe.get("initial_a", "unknown")[:50]
                    })
        
        print(f"\n  Found {len(responses_to_embed)} code leak probe responses")
        
        # Batch embed all responses at once (up to 300 per batch)
        all_code_leak_responses = []
        if responses_to_embed:
            print(f"  Batch embedding {len(responses_to_embed)} responses...")
            embeddings = batch_embed(responses_to_embed, show_progress=True)
            
            # Pair embeddings with responses
            for i, emb in enumerate(embeddings):
                if emb is not None:
                    all_code_leak_responses.append((
                        response_metadata[i]["response"],
                        emb,
                        response_metadata[i]["topic"]
                    ))
        
        print(f"  Successfully embedded {len(all_code_leak_responses)} responses")
        
        if len(all_code_leak_responses) >= 10:
            # Find the code leak cluster
            code_leak_results = find_code_leak_cluster(all_code_leak_responses)
            
            # Save code leak centroid for steering
            if code_leak_results and code_leak_results.get("code_leak_centroid") is not None:
                code_leak_centroid_path = f"{RESULTS_DIR}/code_leak_centroid_{TIMESTAMP}.npy"
                np.save(code_leak_centroid_path, code_leak_results["code_leak_centroid"])
                print(f"\n  ✓ Saved code leak centroid to: {code_leak_centroid_path}")
                
                # Also save code leak responses for reference
                code_leak_responses_path = f"{RESULTS_DIR}/code_leak_responses_{TIMESTAMP}.json"
                with open(code_leak_responses_path, 'w') as f:
                    json.dump({
                        "code_leak_responses": code_leak_results.get("code_leak_responses", []),
                        "cluster_info": {
                            k: {
                                "size": v["size"],
                                "avg_leak_score": v.get("avg_leak_score", 0),
                                "topic_diversity": v["topic_diversity"],
                                "unique_topics": v["unique_topics"],
                                "responses": v["sentences"][:5]
                            }
                            for k, v in code_leak_results.get("cluster_info", {}).items()
                        }
                    }, f, indent=2)
                print(f"  ✓ Saved code leak responses to: {code_leak_responses_path}")
        else:
            print(f"  Not enough responses for code leak detection (need 10+, got {len(all_code_leak_responses)})")
    
    # =========================================================================
    # HEDGE DETECTION: Analyze sentence-level embeddings from controversial probes
    # =========================================================================
    hedge_results = None
    if PROBE_MODE != "unity_ir" and USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
        print(f"\n{'='*80}")
        print("HEDGE PHRASE DETECTION (Empirical)")
        print(f"{'='*80}")
        
        # Collect all sentence embeddings from controversial probes
        all_sentence_embeddings = []
        for probe in all_probes:
            if probe.get("probe_type") == "controversial" and probe.get("sentence_data"):
                for sent_data in probe["sentence_data"]:
                    all_sentence_embeddings.append((
                        sent_data["sentence"],
                        sent_data["embedding"],
                        sent_data["topic"]
                    ))
        
        print(f"\n  Collected {len(all_sentence_embeddings)} sentences from controversial probes")
        
        if len(all_sentence_embeddings) >= 10:
            # Find the hedge cluster (topic-agnostic sentences)
            hedge_results = find_hedge_cluster(all_sentence_embeddings)
            
            # Save hedge centroid for steering
            if hedge_results and hedge_results.get("hedge_centroid") is not None:
                hedge_centroid_path = f"{RESULTS_DIR}/hedge_centroid_{TIMESTAMP}.npy"
                np.save(hedge_centroid_path, hedge_results["hedge_centroid"])
                print(f"\n  ✓ Saved hedge centroid to: {hedge_centroid_path}")
                
                # Also save hedge sentences for reference
                hedge_sentences_path = f"{RESULTS_DIR}/hedge_sentences_{TIMESTAMP}.json"
                with open(hedge_sentences_path, 'w') as f:
                    json.dump({
                        "hedge_sentences": hedge_results.get("hedge_sentences", []),
                        "cluster_info": {
                            k: {
                                "size": v["size"],
                                "topic_diversity": v["topic_diversity"],
                                "unique_topics": v["unique_topics"],
                                "sentences": v["sentences"]
                            }
                            for k, v in hedge_results.get("cluster_info", {}).items()
                        }
                    }, f, indent=2)
                print(f"  ✓ Saved hedge sentences to: {hedge_sentences_path}")
        else:
            print(f"  Not enough sentences for hedge detection (need 10+, got {len(all_sentence_embeddings)})")
    
    # Print cluster summaries
    print(f"\n{'='*80}")
    print("LAGRANGE POINTS DISCOVERED")
    print(f"{'='*80}")
    
    for cluster_id, cluster_info in sorted(cluster_results['clusters'].items(), 
                                          key=lambda x: x[1]['size'], 
                                          reverse=True):
        print(f"\nLAGRANGE POINT {cluster_id}")
        print(f"  Size: {cluster_info['size']} probes ({cluster_info['percentage']:.1f}%)")
        print(f"  Avg distance from centroid: {cluster_info['avg_distance']:.4f}")
        print(f"  Max distance from centroid: {cluster_info['max_distance']:.4f}")
        
        # Extract keywords
        keywords = extract_keywords(cluster_info['texts'], top_n=5)
        print(f"  Common keywords: {', '.join([w for w, c in keywords])}")
        
        # Show 3 example texts
        print(f"  Example syntheses:")
        for i, text in enumerate(cluster_info['texts'][:3]):
            # Show full text
            display_text = text.replace('\n', ' ')
            print(f"    {i+1}. {display_text}")
    
    # Visualize
    viz_path = f"{RESULTS_DIR}/lagrange_map_{TIMESTAMP}.png"
    visualize_clusters(final_embeddings, cluster_results['labels'], viz_path)
    
    # Save full results
    results_path = f"{RESULTS_DIR}/full_results_{TIMESTAMP}.json"
    with open(results_path, 'w') as f:
        save_data = {
            "config": {
                "n_probes": N_PROBES,
                "n_iterations": N_ITERATIONS,
                "n_clusters": cluster_results['n_clusters'],
                "controversial_ratio": CONTROVERSIAL_PROBE_RATIO if USE_CONTROVERSIAL_PROBES else 0,
                "timestamp": TIMESTAMP
            },
            "probes": all_probes,  # Note: embeddings not saved in final
            "clusters": {
                int(k): {
                    "size": v["size"],
                    "percentage": v["percentage"],
                    "texts": v["texts"],
                    "keywords": extract_keywords(v["texts"], 10)
                }
                for k, v in cluster_results['clusters'].items()
            },
            "hedge_detection": {
                "enabled": hedge_results is not None,
                "hedge_sentences_count": len(hedge_results.get("hedge_sentences", [])) if hedge_results else 0,
                "hedge_cluster_id": hedge_results.get("hedge_cluster_id") if hedge_results else None
            } if hedge_results else None,
            "code_leak_detection": {
                "enabled": code_leak_results is not None,
                "code_leak_responses_count": len(code_leak_results.get("code_leak_responses", [])) if code_leak_results else 0,
                "code_leak_cluster_id": code_leak_results.get("code_leak_cluster_id") if code_leak_results else None
            } if code_leak_results else None,
            "structural_analysis": structural_results if structural_results else None,
            "baseline_cache_dir": BASELINE_CACHE_DIR if GENERATE_BASELINES else None,
            "summary": {
                "n_clusters": cluster_results['n_clusters'],
                "success_rate": len(final_embeddings) / N_PROBES
            }
        }
        json.dump(save_data, f, indent=2, default=str)
    
    print(f"\n{'='*80}")
    print("EXPERIMENT COMPLETE")
    print(f"{'='*80}")
    print(f"\nResults saved to:")
    print(f"  - {results_path}")
    print(f"  - {viz_path}")
    if hedge_results:
        print(f"  - {RESULTS_DIR}/hedge_centroid_{TIMESTAMP}.npy")
        print(f"  - {RESULTS_DIR}/hedge_sentences_{TIMESTAMP}.json")
    if code_leak_results:
        print(f"  - {RESULTS_DIR}/code_leak_centroid_{TIMESTAMP}.npy")
        print(f"  - {RESULTS_DIR}/code_leak_responses_{TIMESTAMP}.json")
    if structural_results:
        print(f"  - {RESULTS_DIR}/structural_patterns_{TIMESTAMP}.json")
    print(f"\nSummary:")
    print(f"  Total probes: {N_PROBES}")
    print(f"  Successful: {len(final_embeddings)}")
    print(f"  Lagrange points found: {cluster_results['n_clusters']}")
    
    # Calculate concentration
    if cluster_results['clusters']:
        top_3_pct = sum(sorted([c['percentage'] for c in cluster_results['clusters'].values()], 
                               reverse=True)[:3])
        print(f"  Top 3 attractors capture: {top_3_pct:.1f}% of probes")
    
    # Hedge detection summary
    if hedge_results:
        print(f"\n  Hedge Detection:")
        print(f"    Hedge phrases identified: {len(hedge_results.get('hedge_sentences', []))}")
        print(f"    Hedge centroid saved for steering")
        print(f"\n  Use hedge_centroid_{TIMESTAMP}.npy to steer away from hedging behavior")
    
    # Structural analysis summary
    if structural_results:
        print(f"\n  Structural Analysis:")
        print(f"    Structural patterns identified: {len(structural_results)}")
        for name, attractor in sorted(structural_results.items(), 
                                   key=lambda x: x[1].get("sample_count", 0), reverse=True)[:3]:
            pattern_name = attractor.get("name", "unknown")
            sample_count = attractor.get("sample_count", 0)
            prevalence = attractor.get("prevalence", 0) * 100
            print(f"      - {pattern_name}: {sample_count} probes ({prevalence:.1f}%)")
    
    # Code leak detection summary
    if code_leak_results:
        print(f"\n  Code Leak Detection:")
        print(f"    Code leak responses identified: {len(code_leak_results.get('code_leak_responses', []))}")
        print(f"    Code leak centroid saved for steering")
        print(f"\n  Use code_leak_centroid_{TIMESTAMP}.npy to steer away from code leak patterns")

# ============================================================================
# CLI
# ============================================================================

if __name__ == "__main__":
    import sys
    
    if len(sys.argv) > 1:
        if sys.argv[1] == "--small":
            N_PROBES = 20
            print("Running small test (20 probes)")
        elif sys.argv[1] == "--large":
            N_PROBES = 500
            print("Running large experiment (500 probes)")
    
    try:
        run_experiment()
    except KeyboardInterrupt:
        print("\n\nExperiment interrupted by user")
        print("Partial results may be saved in intermediate files")
