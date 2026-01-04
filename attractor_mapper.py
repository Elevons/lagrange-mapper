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
from typing import List, Tuple, Dict
import time
import random
from datetime import datetime
from sklearn.cluster import KMeans
from sklearn.decomposition import PCA
import matplotlib.pyplot as plt
from collections import Counter

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

# Output
RESULTS_DIR = "lagrange_mapping_results"
TIMESTAMP = datetime.now().strftime("%Y%m%d_%H%M%S")

# Cache for concept pairs
CONCEPT_PAIRS_CACHE_FILE = "concept_pairs_cache.json"
CONTROVERSIAL_CACHE_FILE = "controversial_questions_cache.json"

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
    Segment text into sentences and embed each one.
    Returns list of (sentence, embedding) tuples.
    """
    sentences = segment_into_sentences(text)
    results = []
    
    for sentence in sentences:
        embedding = get_embedding(sentence)
        if embedding is not None:
            results.append((sentence, embedding))
    
    return results


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

def batch_embed(texts: List[str]) -> List[np.ndarray]:
    """Embed multiple texts (with fallback to sequential)"""
    embeddings = []
    for text in texts:
        emb = get_embedding(text)
        if emb is not None:
            embeddings.append(emb)
        else:
            # Fallback: use hash-based embedding
            import hashlib
            hash_obj = hashlib.sha256(text.encode())
            hash_bytes = hash_obj.digest()
            vec = np.frombuffer(hash_bytes, dtype=np.uint8).astype(float)
            vec = vec / np.linalg.norm(vec)
            embeddings.append(vec)
    return embeddings

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
# SYNTHESIS FUNCTION (using LOCAL model)
# ============================================================================

def synthesize_concepts(concept_a: str, concept_b: str) -> str:
    """
    Use LOCAL LLM to synthesize two concepts, or answer a controversial question.
    
    If concept_b == "controversial", then concept_a is actually a question
    and we just ask it directly (no synthesis).
    """
    
    headers = {"Content-Type": "application/json"}
    
    # Check if this is a controversial question
    if concept_b == "controversial":
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
        return response.json()['choices'][0]['message']['content'].strip()
    except Exception as e:
        print(f"  Error with local model: {e}")
        if concept_b == "controversial":
            return f"[Response to: {concept_a}]"
        return f"[Synthesis of {concept_a} and {concept_b}]"

# ============================================================================
# PROBING FUNCTION
# ============================================================================

def run_probe(probe_id: int, concept_a: str, concept_b: str) -> Dict:
    """
    Run one probe: iterate synthesis N times and track trajectory
    Uses LOCAL model for synthesis iterations
    
    If concept_b == "controversial", this is a controversial question probe.
    For controversial probes, we also collect sentence-level embeddings to
    enable empirical hedging detection.
    """
    
    is_controversial = (concept_b == "controversial")
    
    if is_controversial:
        print(f"\nProbe {probe_id} [CONTROVERSIAL]: '{concept_a}'")
    else:
        print(f"\nProbe {probe_id}: '{concept_a}' vs '{concept_b}'")
    
    # SAVE ORIGINAL CONCEPTS BEFORE THEY GET OVERWRITTEN
    original_concept_a = concept_a
    original_concept_b = concept_b
    
    trajectory = []
    embeddings = []
    sentence_data = []  # For sentence-level hedging detection
    
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
            for sentence in sentences:
                sent_embedding = get_embedding(sentence)
                if sent_embedding is not None:
                    sentence_data.append({
                        "sentence": sentence,
                        "embedding": sent_embedding,
                        "topic": original_concept_a[:50]  # Use question as topic identifier
                    })
        
        # Update for next iteration (use synthesis as new input)
        concept_a = synthesis[:50]  # Use first part as concept A
        concept_b = synthesis[50:100] if len(synthesis) > 50 else synthesis  # Second part as B
        
        print(f"Done. Length: {len(synthesis)} chars" + 
              (f", {len(sentence_data)} sentences" if is_controversial else ""))
        
        # Rate limit
        time.sleep(0.5)
    
    result = {
        "probe_id": probe_id,
        "initial_a": original_concept_a,  # Use saved original
        "initial_b": original_concept_b,  # Use saved original
        "probe_type": "controversial" if original_concept_b == "controversial" else "neutral",
        "trajectory": trajectory,
        "embeddings": embeddings,
        "final_embedding": embeddings[-1] if embeddings else None
    }
    
    # Add sentence data for controversial probes (for hedge detection)
    if is_controversial and sentence_data:
        result["sentence_data"] = sentence_data
    
    return result

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
    
    # Generate all probes upfront (mixed or neutral-only)
    if USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
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
    
    # Cluster analysis
    cluster_results = cluster_attractors(final_embeddings, final_texts, n_clusters=N_CLUSTERS)
    
    # =========================================================================
    # HEDGE DETECTION: Analyze sentence-level embeddings from controversial probes
    # =========================================================================
    hedge_results = None
    if USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
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
