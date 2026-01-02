#!/usr/bin/env python3
"""
Extract Attractor Filters

Transforms analysis results into filter configuration for steering system.
Part of the LLM Attractor Mapping Pipeline.

Attractors are ranked by dominance (percentage of outputs).
At runtime, filter intensity (0-1) determines how many attractors to filter:
  - intensity=0.0: no filtering
  - intensity=0.3: filter top 30% of attractors  
  - intensity=1.0: filter all attractors

Now also incorporates hedge centroids from empirical hedging detection.
The hedge centroid is discovered by clustering sentence-level embeddings
from controversial probe responses - hedging phrases cluster together
because they're topic-agnostic.

Usage:
    python extract_filters.py <results_file.json> <model_name> [--direct]
    python extract_filters.py <results_file.json> <model_name> --direct --controversial
"""

import json
import numpy as np
from pathlib import Path
import sys
import glob
from typing import Dict, List, Optional, Tuple
from collections import Counter

# ============================================================================
# HELPER FUNCTIONS
# ============================================================================

def load_attractor_data(filepath: str) -> Dict:
    """Load exported attractor data from analysis step"""
    with open(filepath, 'r') as f:
        return json.load(f)


def extract_keywords_from_texts(texts: List[str], top_n: int = 30) -> List[str]:
    """Extract most common meaningful keywords from texts"""
    
    stopwords = {
        'the', 'a', 'an', 'and', 'or', 'but', 'in', 'on', 'at', 'to', 'for',
        'of', 'with', 'by', 'from', 'as', 'is', 'was', 'are', 'were', 'been',
        'be', 'have', 'has', 'had', 'do', 'does', 'did', 'will', 'would',
        'could', 'should', 'may', 'might', 'must', 'shall', 'can', 'need',
        'that', 'this', 'these', 'those', 'it', 'its', 'they', 'them',
        'their', 'we', 'our', 'you', 'your', 'he', 'she', 'him', 'her',
        'who', 'what', 'where', 'when', 'why', 'how', 'which', 'while',
        'each', 'both', 'all', 'any', 'some', 'such', 'into', 'through',
        'during', 'before', 'after', 'above', 'below', 'between', 'under',
        'again', 'further', 'then', 'once', 'here', 'there', 'where',
        'synthesis', 'mechanism', 'using', 'based', 'within', 'across',
        'also', 'just', 'only', 'very', 'more', 'most', 'other', 'same',
        'than', 'too', 'own', 'being', 'over', 'such', 'through', 'about'
    }
    
    words = []
    for text in texts:
        text_words = text.lower().split()
        text_words = [w.strip('.,!?;:()[]{}"\'-') for w in text_words]
        text_words = [w for w in text_words if len(w) > 3 and w not in stopwords]
        words.extend(text_words)
    
    counter = Counter(words)
    return [word for word, count in counter.most_common(top_n)]


def make_attractor_name(rank: int) -> str:
    """Simple cluster naming by rank"""
    return f"cluster_{rank}"


# ============================================================================
# HEDGE CENTROID LOADING
# ============================================================================

def find_hedge_files(results_dir: str = "lagrange_mapping_results") -> Tuple[Optional[Path], Optional[Path]]:
    """
    Find the most recent hedge centroid and sentences files.
    
    Returns:
        Tuple of (hedge_centroid_path, hedge_sentences_path) or (None, None)
    """
    results_path = Path(results_dir)
    if not results_path.exists():
        return None, None
    
    # Find most recent hedge centroid
    centroid_files = list(results_path.glob("hedge_centroid_*.npy"))
    sentences_files = list(results_path.glob("hedge_sentences_*.json"))
    
    if not centroid_files:
        return None, None
    
    # Sort by modification time (most recent first)
    centroid_files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
    centroid_path = centroid_files[0]
    
    # Find matching sentences file (same timestamp)
    sentences_path = None
    if sentences_files:
        # Extract timestamp from centroid filename
        timestamp = centroid_path.stem.replace("hedge_centroid_", "")
        matching = [p for p in sentences_files if timestamp in p.stem]
        if matching:
            sentences_path = matching[0]
        else:
            # Fall back to most recent
            sentences_files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
            sentences_path = sentences_files[0]
    
    return centroid_path, sentences_path


def load_hedge_centroid(centroid_path: Path) -> Optional[np.ndarray]:
    """Load hedge centroid vector from .npy file"""
    try:
        centroid = np.load(centroid_path)
        # Normalize
        norm = np.linalg.norm(centroid)
        if norm > 0:
            centroid = centroid / norm
        print(f"  ✓ Loaded hedge centroid from: {centroid_path.name}")
        print(f"    Dimensions: {len(centroid)}")
        return centroid
    except Exception as e:
        print(f"  ✗ Failed to load hedge centroid: {e}")
        return None


def load_hedge_sentences(sentences_path: Path) -> Tuple[List[str], List[str]]:
    """
    Load hedge sentences and extract keywords from them.
    
    Returns:
        Tuple of (hedge_sentences, hedge_keywords)
    """
    try:
        with open(sentences_path, 'r') as f:
            data = json.load(f)
        
        sentences = data.get('hedge_sentences', [])
        
        # Extract keywords from hedge sentences
        keywords = extract_keywords_from_texts(sentences, top_n=30)
        
        print(f"  ✓ Loaded {len(sentences)} hedge sentences from: {sentences_path.name}")
        print(f"    Sample: \"{sentences[0][:60]}...\"" if sentences else "")
        
        return sentences, keywords
    except Exception as e:
        print(f"  ✗ Failed to load hedge sentences: {e}")
        return [], []


def create_hedge_attractor(
    centroid: np.ndarray, 
    sentences: List[str], 
    keywords: List[str]
) -> Dict:
    """
    Create a special 'hedging' attractor entry for the filter config.
    
    This attractor represents the model's natural hedging patterns,
    discovered empirically from sentence-level clustering.
    """
    return {
        "rank": 0,  # Highest priority - hedging is the most important to filter
        "name": "hedging",
        "type": "hedge_centroid",  # Special marker
        "percentage": 0,  # Not from standard clustering
        "keywords": keywords,
        "sample_outputs": sentences[:5],
        "centroid": centroid.tolist(),
        "description": "Empirically discovered hedging patterns from sentence-level analysis"
    }


# ============================================================================
# MAIN EXTRACTION
# ============================================================================

def generate_filter_config(
    attractors: Dict, 
    model_name: str,
    hedge_attractor: Optional[Dict] = None
) -> Dict:
    """
    Generate filter configuration for steering system.
    
    Attractors are stored with their rank (by percentage).
    The steering system uses intensity to determine which ranks to filter.
    
    Args:
        attractors: Dictionary of attractor data from clustering
        model_name: Name of the model being configured
        hedge_attractor: Optional hedge attractor from empirical detection
    """
    
    # Sort attractors by percentage (most dominant first)
    sorted_attractors = sorted(
        attractors.items(),
        key=lambda x: x[1].get('percentage', 0),
        reverse=True
    )
    
    config = {
        "model_name": model_name,
        "version": "2.2",  # Updated version with hedge support
        "generated_from": "extract_filters.py",
        "total_attractors": len(sorted_attractors),
        "attractors": [],  # Now a list, ordered by dominance
        "settings": {
            "keyword_threshold": 3,
            "embedding_threshold": 0.75,
            "hedge_embedding_threshold": 0.70,  # Slightly lower for hedge detection
            "default_intensity": 0.5  # Filter top 50% by default
        },
        "all_keywords": [],  # All attractor keywords (for topic exemption at runtime)
        "has_hedge_attractor": hedge_attractor is not None
    }
    
    # Collect all keywords
    all_keywords = []
    
    # Add hedge attractor first if available (highest priority)
    if hedge_attractor:
        config['attractors'].append(hedge_attractor)
        all_keywords.extend(hedge_attractor.get('keywords', []))
        config['total_attractors'] += 1
        start_rank = 1  # Other attractors start at rank 1
    else:
        start_rank = 0
    
    for i, (raw_name, data) in enumerate(sorted_attractors):
        rank = start_rank + i
        
        # Get keywords
        if 'keywords' in data:
            keywords = data['keywords']
        elif 'texts' in data:
            keywords = extract_keywords_from_texts(data['texts'])
        else:
            keywords = []
        
        # Simple naming by rank
        attractor_name = make_attractor_name(rank)
        
        percentage = data.get('percentage', 0)
        
        attractor_entry = {
            "rank": rank,  # 0 = hedge (if present), then by dominance
            "name": attractor_name,
            "percentage": percentage,
            "keywords": keywords[:25],
            "sample_outputs": data.get('texts', data.get('sample_texts', []))[:3]
        }
        
        # Add centroid if available
        if 'centroid' in data:
            attractor_entry['centroid'] = data['centroid']
        
        config['attractors'].append(attractor_entry)
        all_keywords.extend(keywords[:25])
    
    # Store unique keywords for runtime topic exemption
    config['all_keywords'] = sorted(set(k.lower() for k in all_keywords))
    
    return config


def save_filter_config(config: Dict, output_dir: str) -> Path:
    """Save filter configuration files"""
    
    model_name = config['model_name']
    base_path = Path(output_dir) / model_name
    base_path.mkdir(parents=True, exist_ok=True)
    
    # Save full config
    config_path = base_path / "filter_config.json"
    with open(config_path, 'w') as f:
        json.dump(config, f, indent=2)
    print(f"✓ Saved filter config to: {config_path}")
    
    # Save centroids separately (for fast loading)
    centroids = {}
    for attractor in config['attractors']:
        if 'centroid' in attractor:
            centroids[attractor['name']] = attractor['centroid']
    
    if centroids:
        centroid_path = base_path / "attractor_centroids.json"
        with open(centroid_path, 'w') as f:
            json.dump(centroids, f, indent=2)
        print(f"✓ Saved centroids to: {centroid_path}")
    
    # Save keywords separately (for fast keyword matching)
    keywords = {
        attractor['name']: attractor['keywords']
        for attractor in config['attractors']
    }
    keywords_path = base_path / "attractor_keywords.json"
    with open(keywords_path, 'w') as f:
        json.dump(keywords, f, indent=2)
    print(f"✓ Saved keywords to: {keywords_path}")
    
    return config_path


# ============================================================================
# DIRECT ANALYSIS MODE
# ============================================================================

def analyze_probes_directly(probes_filepath: str, n_clusters_override: int = None, probe_type_filter: str = None) -> Dict:
    """
    Directly analyze probes file to extract attractors.
    Use this if you don't have a pre-exported attractors.json.
    
    Args:
        probes_filepath: Path to the probes JSON file
        n_clusters_override: Override number of clusters (None = auto-detect)
        probe_type_filter: Filter for probe type - "neutral", "controversial", or None (all)
    
    Returns:
        Dictionary of attractor data
    """
    
    print(f"Loading probes from: {probes_filepath}")
    if probe_type_filter:
        print(f"  Filtering for: {probe_type_filter} probes")
    
    with open(probes_filepath, 'r') as f:
        data = json.load(f)
    
    # Handle nested structure
    if isinstance(data, dict) and 'probes' in data:
        probes = data['probes']
    else:
        probes = data
    
    # Extract texts and embeddings
    texts = []
    embeddings = []
    
    for probe in probes:
        # Apply probe type filter
        if probe_type_filter:
            probe_type = probe.get('probe_type', 'neutral')
            # Also check if concept_b is "controversial" marker (legacy format)
            if probe.get('initial_b') == 'controversial':
                probe_type = 'controversial'
            
            if probe_type != probe_type_filter:
                continue
        
        # Get text
        if 'trajectory' in probe and probe['trajectory']:
            texts.append(probe['trajectory'][-1])
        elif 'synthesis' in probe:
            texts.append(probe['synthesis'])
        
        # Get embedding
        if 'embeddings' in probe and probe['embeddings']:
            emb = probe['embeddings'][-1]
            if isinstance(emb, list):
                embeddings.append(np.array(emb))
            elif isinstance(emb, str):
                emb_str = emb.strip('[]').replace('\n', ' ')
                values = [float(v) for v in emb_str.split() if v]
                if values:
                    embeddings.append(np.array(values))
        elif 'embedding' in probe and probe['embedding']:
            embeddings.append(np.array(probe['embedding']))
    
    print(f"  Loaded {len(texts)} texts, {len(embeddings)} embeddings")
    
    if not embeddings:
        print("  Warning: No embeddings found, using keyword-only analysis")
        keywords = extract_keywords_from_texts(texts, top_n=50)
        return {
            "cluster_0": {
                "texts": texts[:100],
                "keywords": keywords,
                "percentage": 100.0
            }
        }
    
    # Clustering using k-means
    from sklearn.cluster import KMeans
    
    embeddings_array = np.array(embeddings)
    
    # Determine cluster count
    if n_clusters_override:
        n_clusters = n_clusters_override
    else:
        n_clusters = min(8, len(embeddings) // 50)
    
    if n_clusters < 2:
        n_clusters = 2
    
    print(f"  Clustering into {n_clusters} groups...")
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    original_labels = kmeans.fit_predict(embeddings_array)
    
    # Reorder clusters by size (0 = largest)
    cluster_sizes = [(i, (original_labels == i).sum()) for i in range(n_clusters)]
    cluster_sizes.sort(key=lambda x: x[1], reverse=True)
    old_to_new = {old: new for new, (old, _) in enumerate(cluster_sizes)}
    labels = np.array([old_to_new[l] for l in original_labels])
    
    # Build attractor data (now ordered by size)
    attractors = {}
    for new_id in range(n_clusters):
        mask = labels == new_id
        cluster_texts = [texts[i] for i in range(len(texts)) if i < len(mask) and mask[i]]
        cluster_embeddings = embeddings_array[mask]
        
        if len(cluster_texts) == 0:
            continue
        
        # Compute centroid
        centroid = np.mean(cluster_embeddings, axis=0)
        centroid = centroid / np.linalg.norm(centroid)
        
        # Extract keywords
        keywords = extract_keywords_from_texts(cluster_texts, top_n=30)
        
        attractors[f"cluster_{new_id}"] = {
            "texts": cluster_texts[:10],
            "keywords": keywords,
            "centroid": centroid.tolist(),
            "percentage": len(cluster_texts) / len(texts) * 100,
            "size": len(cluster_texts)
        }
    
    return attractors


# ============================================================================
# CLI
# ============================================================================

def main():
    if len(sys.argv) < 3:
        print("="*70)
        print("EXTRACT ATTRACTOR FILTERS")
        print("="*70)
        print("\nUsage:")
        print("  python extract_filters.py <input_file> <model_name> [options]")
        print("\nOptions:")
        print("  --direct           Analyze probes file directly (vs pre-analyzed)")
        print("  --controversial    Filter for controversial probes only")
        print("  --with-hedge       Include empirical hedge centroid (auto-detected)")
        print("  --hedge-dir <dir>  Directory containing hedge centroid files")
        print("  --no-hedge         Skip hedge centroid even if available")
        print("\nExamples:")
        print("  python extract_filters.py analysis/attractors.json granite-3.1-8b")
        print("  python extract_filters.py probes.json granite-3.1-8b --direct")
        print("  python extract_filters.py probes.json granite-3.1-8b --direct --controversial")
        print("  python extract_filters.py probes.json granite-3.1-8b --direct --with-hedge")
        print("\nHedge Detection:")
        print("  The --with-hedge flag includes empirically discovered hedging patterns.")
        print("  These are discovered by attractor_mapper.py from sentence-level clustering")
        print("  of controversial probe responses. Hedging phrases cluster together because")
        print("  they're topic-agnostic (same evasive language regardless of topic).")
        print("\nIntensity-based filtering:")
        print("  - Attractors are ranked by dominance (most common first)")
        print("  - If hedge centroid is included, it's always rank 0 (highest priority)")
        print("  - At runtime, set intensity 0-1 to control filtering")
        print("  - intensity=0.3 filters top 30% of attractors")
        print("  - intensity=1.0 filters all detected attractors")
        return
    
    input_file = sys.argv[1]
    model_name = sys.argv[2]
    direct_mode = "--direct" in sys.argv
    controversial_only = "--controversial" in sys.argv
    include_hedge = "--with-hedge" in sys.argv
    skip_hedge = "--no-hedge" in sys.argv
    
    # Parse hedge directory
    hedge_dir = "lagrange_mapping_results"
    if "--hedge-dir" in sys.argv:
        idx = sys.argv.index("--hedge-dir")
        if idx + 1 < len(sys.argv):
            hedge_dir = sys.argv[idx + 1]
    
    print("="*70)
    print("EXTRACT ATTRACTOR FILTERS")
    print("="*70)
    print(f"\nInput: {input_file}")
    print(f"Model: {model_name}")
    print(f"Mode: {'Direct probe analysis' if direct_mode else 'Pre-analyzed attractors'}")
    if controversial_only:
        print(f"Filter: Controversial probes only")
    if include_hedge:
        print(f"Hedge: Will include empirical hedge centroid")
    
    # Load or analyze data
    if direct_mode:
        probe_type_filter = "controversial" if controversial_only else None
        attractors = analyze_probes_directly(input_file, probe_type_filter=probe_type_filter)
    else:
        attractors = load_attractor_data(input_file)
    
    # Look for hedge centroid (auto-detect or explicit)
    hedge_attractor = None
    if not skip_hedge:
        print(f"\n{'─'*70}")
        print("HEDGE CENTROID DETECTION")
        print(f"{'─'*70}")
        
        centroid_path, sentences_path = find_hedge_files(hedge_dir)
        
        if centroid_path:
            centroid = load_hedge_centroid(centroid_path)
            
            if centroid is not None:
                sentences = []
                keywords = []
                
                if sentences_path:
                    sentences, keywords = load_hedge_sentences(sentences_path)
                
                # Create hedge attractor entry
                hedge_attractor = create_hedge_attractor(centroid, sentences, keywords)
                print(f"  ✓ Created hedge attractor with {len(keywords)} keywords")
        else:
            if include_hedge:
                print(f"  ✗ No hedge centroid found in {hedge_dir}/")
                print(f"    Run attractor_mapper.py with controversial probes first.")
            else:
                print(f"  No hedge centroid found (use --with-hedge to require)")
    
    # Generate filter config
    print(f"\n{'─'*70}")
    print("GENERATING FILTER CONFIG")
    print(f"{'─'*70}")
    config = generate_filter_config(attractors, model_name, hedge_attractor)
    
    # Summary
    print(f"\nFound {len(config['attractors'])} attractors (ranked by priority):")
    for attractor in config['attractors']:
        attractor_type = attractor.get('type', 'cluster')
        if attractor_type == 'hedge_centroid':
            print(f"  #{attractor['rank']}: {attractor['name']} [HEDGE CENTROID]")
            print(f"       Keywords: {', '.join(attractor['keywords'][:5])}...")
        else:
            print(f"  #{attractor['rank']}: {attractor['name']} ({attractor['percentage']:.1f}%)")
            print(f"       Keywords: {', '.join(attractor['keywords'][:5])}...")
    
    # Keywords summary (these will be used for topic exemption at runtime)
    if config.get('all_keywords'):
        print(f"\nExtracted {len(config['all_keywords'])} unique attractor keywords")
        print(f"  Sample: {', '.join(config['all_keywords'][:10])}...")
        print(f"\n  Topic exemption: If a topic contains any of these keywords,")
        print(f"  that keyword will be exempted from attractor detection.")
    
    # Save
    output_dir = "filter_configs"
    
    # Use different name for controversial-only configs
    if controversial_only:
        save_model_name = f"{model_name}-controversial"
        config['model_name'] = save_model_name
    else:
        save_model_name = model_name
    
    config_path = save_filter_config(config, output_dir)
    
    print(f"\n{'='*70}")
    print("EXTRACTION COMPLETE")
    print(f"{'='*70}")
    print(f"\nFilter config saved to: {config_path.parent}/")
    
    if hedge_attractor:
        print(f"\n✓ Includes hedge centroid for empirical hedging detection")
        print(f"  The hedge attractor uses embedding similarity to detect hedging")
        print(f"  phrases the model naturally uses, not just keyword matching.")
    
    print("\nUsage with steering:")
    print(f"  steering = load_steering('{save_model_name}')")
    print(f"  result = steering.detect(text, intensity=0.5)  # Filter top 50%")


if __name__ == "__main__":
    main()
