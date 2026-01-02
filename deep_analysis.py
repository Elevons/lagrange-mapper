#!/usr/bin/env python3
"""
Deep Analysis v2 - Improved Visualization

Clearer visualizations focused on:
1. Cloud shape and structure
2. Cluster identification with labels
3. Attractor keywords
4. Distribution statistics
5. Hedge phrase detection (empirically discovered hedging patterns)

No trajectory analysis (single iteration is sufficient).
"""

import json
import numpy as np
import matplotlib.pyplot as plt
from sklearn.cluster import DBSCAN, KMeans
from sklearn.decomposition import PCA
from sklearn.neighbors import NearestNeighbors
from scipy.spatial.distance import pdist
from scipy.cluster.hierarchy import dendrogram, linkage, fcluster
from collections import Counter
import sys
import os
import glob
from pathlib import Path
from typing import Dict, List, Optional, Tuple

# ============================================================================
# CONFIGURATION
# ============================================================================

# Visual style
plt.style.use('seaborn-v0_8-whitegrid')
COLORS = ['#e41a1c', '#377eb8', '#4daf4a', '#984ea3', '#ff7f00', 
          '#ffff33', '#a65628', '#f781bf', '#999999', '#66c2a5']

# ============================================================================
# DATA LOADING
# ============================================================================

def parse_embedding(emb):
    """Parse embedding from various formats"""
    if isinstance(emb, list):
        return np.array(emb)
    if isinstance(emb, np.ndarray):
        return emb
    if isinstance(emb, str):
        emb = emb.strip('[]').replace('\n', ' ')
        values = [float(v) for v in emb.split() if v]
        return np.array(values) if values else None
    return None


def load_data(filepath, probe_type_filter: str = None):
    """
    Load probe data from JSON.
    
    Args:
        filepath: Path to the JSON file
        probe_type_filter: Optional filter - "neutral", "controversial", or None (all)
    
    Returns:
        Tuple of (embeddings, texts, concepts, config)
    """
    print(f"Loading: {filepath}")
    if probe_type_filter:
        print(f"  Filtering for: {probe_type_filter} probes")
    
    with open(filepath, 'r') as f:
        data = json.load(f)
    
    # Handle nested structure
    if isinstance(data, dict) and 'probes' in data:
        probes = data['probes']
        config = data.get('config', {})
    else:
        probes = data if isinstance(data, list) else []
        config = {}
    
    # Extract embeddings, texts, and initial concepts
    embeddings = []
    texts = []
    concepts = []  # List of (concept_a, concept_b) tuples
    
    for probe in probes:
        # Apply probe type filter
        if probe_type_filter:
            probe_type = probe.get('probe_type', 'neutral')
            # Also check if concept_b is "controversial" (legacy format)
            if probe.get('initial_b') == 'controversial':
                probe_type = 'controversial'
            
            if probe_type != probe_type_filter:
                continue
        
        emb = None
        text = None
        
        # Get embedding
        if 'embeddings' in probe and probe['embeddings']:
            emb = parse_embedding(probe['embeddings'][-1])
        elif 'final_embedding' in probe and probe['final_embedding']:
            emb = parse_embedding(probe['final_embedding'])
        elif 'embedding' in probe and probe['embedding']:
            emb = parse_embedding(probe['embedding'])
        
        # Get text
        if 'trajectory' in probe and probe['trajectory']:
            text = probe['trajectory'][-1]
        elif 'synthesis' in probe:
            text = probe['synthesis']
        elif 'final_text' in probe:
            text = probe['final_text']
        
        # Get initial concepts
        concept_a = probe.get('initial_a', '')
        concept_b = probe.get('initial_b', '')
        
        if emb is not None and len(emb) > 0:
            embeddings.append(emb)
            texts.append(text or "")
            concepts.append((concept_a, concept_b))
    
    embeddings = np.array(embeddings) if embeddings else np.array([])
    if len(embeddings) > 0:
        print(f"  Loaded {len(embeddings)} probes with {embeddings.shape[1]}-dim embeddings")
    else:
        print(f"  No probes found matching criteria")
    
    return embeddings, texts, concepts, config


def filter_probes_by_type(all_probes: list, probe_type: str = None) -> list:
    """
    Filter probes by type.
    
    Args:
        all_probes: All probe results
        probe_type: "neutral", "controversial", or None (all)
    
    Returns:
        Filtered list
    """
    if probe_type is None:
        return all_probes
    
    filtered = []
    for probe in all_probes:
        # Check probe_type field
        pt = probe.get('probe_type', 'neutral')
        
        # Also check if second concept is "controversial" marker (legacy)
        if probe.get('initial_b') == 'controversial':
            pt = 'controversial'
        
        if pt == probe_type:
            filtered.append(probe)
    
    return filtered


def extract_keywords(texts, top_n=10, min_word_len=4):
    """Extract most common meaningful words"""
    stopwords = {
        'the', 'and', 'for', 'that', 'with', 'this', 'from', 'which', 'while',
        'their', 'through', 'between', 'where', 'each', 'both', 'into', 'also',
        'more', 'than', 'when', 'what', 'how', 'who', 'been', 'have', 'has',
        'would', 'could', 'should', 'being', 'these', 'those', 'such', 'then',
        'them', 'they', 'were', 'was', 'are', 'can', 'will', 'just', 'only',
        'synthesis', 'mechanism', 'using', 'based', 'within', 'across', 'about',
        'over', 'under', 'after', 'before', 'during', 'other', 'some', 'any'
    }
    
    words = []
    for text in texts:
        if not text:
            continue
        text_words = text.lower().split()
        text_words = [w.strip('.,!?;:()[]{}"\'-') for w in text_words]
        text_words = [w for w in text_words if len(w) >= min_word_len and w not in stopwords]
        words.extend(text_words)
    
    return Counter(words).most_common(top_n)


def extract_phrases(texts, top_n=5, ngram_range=(2, 4)):
    """Extract common multi-word phrases from texts (for controversial analysis)"""
    import re
    
    stopwords = {
        'the', 'and', 'for', 'that', 'with', 'this', 'from', 'which', 'while',
        'their', 'through', 'between', 'where', 'each', 'both', 'into', 'also',
        'more', 'than', 'when', 'what', 'how', 'who', 'been', 'have', 'has',
        'would', 'could', 'should', 'being', 'these', 'those', 'such', 'then',
        'them', 'they', 'were', 'was', 'are', 'can', 'will', 'just', 'only',
        'there', 'here', 'about', 'over', 'under', 'some', 'any', 'all'
    }
    
    phrase_counts = Counter()
    
    for text in texts:
        if not text:
            continue
        
        # Clean and tokenize
        text = text.lower()
        text = re.sub(r'[^\w\s]', ' ', text)
        words = text.split()
        words = [w for w in words if len(w) >= 3]
        
        # Extract n-grams
        for n in range(ngram_range[0], ngram_range[1] + 1):
            for i in range(len(words) - n + 1):
                ngram = words[i:i+n]
                # Skip if starts or ends with stopword
                if ngram[0] in stopwords or ngram[-1] in stopwords:
                    continue
                # Skip if mostly stopwords
                non_stop = [w for w in ngram if w not in stopwords]
                if len(non_stop) < len(ngram) * 0.5:
                    continue
                phrase = ' '.join(ngram)
                phrase_counts[phrase] += 1
    
    # Filter and sort - prefer phrases that appear multiple times, then by length
    # Include all phrases (even count=1) to ensure we get results for smaller datasets
    filtered = [(p, c) for p, c in phrase_counts.items()]
    # Sort by count descending, then by phrase length descending (prefer longer meaningful phrases)
    filtered.sort(key=lambda x: (-x[1], -len(x[0].split())))
    
    return filtered[:top_n]


# ============================================================================
# HEDGE PHRASE LOADING
# ============================================================================

def find_hedge_files(results_dir: str = None) -> Tuple[Optional[Path], Optional[Path]]:
    """
    Find the most recent hedge centroid and sentences files.
    
    Args:
        results_dir: Directory to search (defaults to lagrange_mapping_results)
    
    Returns:
        Tuple of (hedge_centroid_path, hedge_sentences_path) or (None, None)
    """
    if results_dir is None:
        results_dir = "lagrange_mapping_results"
    
    results_path = Path(results_dir)
    if not results_path.exists():
        return None, None
    
    # Find most recent hedge files
    centroid_files = list(results_path.glob("hedge_centroid_*.npy"))
    sentences_files = list(results_path.glob("hedge_sentences_*.json"))
    
    if not centroid_files and not sentences_files:
        return None, None
    
    centroid_path = None
    sentences_path = None
    
    if centroid_files:
        centroid_files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
        centroid_path = centroid_files[0]
    
    if sentences_files:
        sentences_files.sort(key=lambda p: p.stat().st_mtime, reverse=True)
        sentences_path = sentences_files[0]
    
    return centroid_path, sentences_path


def load_hedge_data(sentences_path: Path) -> Dict:
    """
    Load hedge sentences and cluster info from JSON.
    
    Returns:
        Dict with hedge_sentences, cluster_info
    """
    try:
        with open(sentences_path, 'r') as f:
            data = json.load(f)
        
        return {
            "hedge_sentences": data.get("hedge_sentences", []),
            "cluster_info": data.get("cluster_info", {})
        }
    except Exception as e:
        print(f"  Warning: Could not load hedge data: {e}")
        return {"hedge_sentences": [], "cluster_info": {}}


def print_hedge_summary(hedge_data: Dict):
    """Print hedge phrase analysis to console"""
    
    sentences = hedge_data.get("hedge_sentences", [])
    cluster_info = hedge_data.get("cluster_info", {})
    
    if not sentences:
        print("\n  No hedge phrases found.")
        return
    
    print(f"\n  Found {len(sentences)} hedging phrases (topic-agnostic evasive language)")
    print(f"\n  Sample Hedge Phrases:")
    print(f"  {'─'*70}")
    
    for i, sentence in enumerate(sentences[:10]):
        # Truncate if too long
        display = sentence[:80] + "..." if len(sentence) > 80 else sentence
        print(f"    {i+1}. \"{display}\"")
    
    if len(sentences) > 10:
        print(f"    ... and {len(sentences) - 10} more")
    
    # Show cluster analysis if available
    if cluster_info:
        print(f"\n  Cluster Analysis:")
        print(f"  {'─'*70}")
        
        # Sort by topic diversity (higher = more topic-agnostic = more likely hedging)
        sorted_clusters = sorted(
            cluster_info.items(),
            key=lambda x: x[1].get('topic_diversity', 0),
            reverse=True
        )
        
        for cluster_id, info in sorted_clusters[:5]:
            topic_div = info.get('topic_diversity', 0)
            size = info.get('size', 0)
            topics = info.get('unique_topics', [])
            
            print(f"\n    Cluster {cluster_id}: {size} sentences across {topic_div} topics")
            if topics:
                topic_preview = ", ".join(t[:30] for t in topics[:3])
                if len(topics) > 3:
                    topic_preview += f" (+{len(topics)-3} more)"
                print(f"      Topics: {topic_preview}")
            
            # Show sample sentences
            sample_sentences = info.get('sentences', [])[:2]
            for sent in sample_sentences:
                display = sent[:60] + "..." if len(sent) > 60 else sent
                print(f"      → \"{display}\"")


# ============================================================================
# ANALYSIS FUNCTIONS
# ============================================================================

def find_optimal_clusters(embeddings, max_k=10):
    """Find optimal number of clusters using elbow method"""
    inertias = []
    K = range(2, min(max_k + 1, len(embeddings) // 10))
    
    for k in K:
        kmeans = KMeans(n_clusters=k, random_state=42, n_init=10)
        kmeans.fit(embeddings)
        inertias.append(kmeans.inertia_)
    
    # Simple elbow detection
    if len(inertias) > 2:
        diffs = np.diff(inertias)
        elbow = np.argmin(diffs) + 2  # +2 because we started at k=2
    else:
        elbow = 3
    
    return elbow, list(K), inertias


def cluster_and_label(embeddings, texts, concepts, n_clusters=5, extract_cluster_phrases=False):
    """Cluster embeddings and extract labels for each cluster (ordered by size, 0=largest)"""
    
    kmeans = KMeans(n_clusters=n_clusters, random_state=42, n_init=10)
    original_labels = kmeans.fit_predict(embeddings)
    
    # Count sizes of each original cluster
    cluster_sizes = [(i, (original_labels == i).sum()) for i in range(n_clusters)]
    # Sort by size descending (largest first)
    cluster_sizes.sort(key=lambda x: x[1], reverse=True)
    
    # Create mapping: old_label -> new_label (where new 0 = largest)
    old_to_new = {old: new for new, (old, _) in enumerate(cluster_sizes)}
    
    # Remap labels
    labels = np.array([old_to_new[l] for l in original_labels])
    
    # Reorder centroids
    new_centroids = np.array([kmeans.cluster_centers_[old] for old, _ in cluster_sizes])
    
    clusters = {}
    for new_i in range(n_clusters):
        mask = labels == new_i
        cluster_texts = [texts[j] for j in range(len(texts)) if mask[j]]
        cluster_concepts = [concepts[j] for j in range(len(concepts)) if mask[j]]
        
        keywords = extract_keywords(cluster_texts, top_n=5)
        keyword_str = ', '.join([w for w, c in keywords])
        
        # Extract phrases for controversial analysis
        phrases = []
        if extract_cluster_phrases:
            phrases = extract_phrases(cluster_texts, top_n=5)
        
        clusters[new_i] = {
            'size': mask.sum(),
            'percentage': mask.sum() / len(labels) * 100,
            'keywords': keywords,
            'keyword_str': keyword_str,
            'phrases': phrases,
            'label': f"Cluster {new_i}",
            'centroid': new_centroids[new_i],
            'texts': cluster_texts[:5],
            'concepts': cluster_concepts[:5]
        }
    
    # Update kmeans centroids for consistency
    kmeans.cluster_centers_ = new_centroids
    
    return labels, clusters, kmeans


def compute_density(embeddings, k=15):
    """Compute local density for each point"""
    nbrs = NearestNeighbors(n_neighbors=k).fit(embeddings)
    distances, _ = nbrs.kneighbors(embeddings)
    density = 1 / (np.mean(distances, axis=1) + 1e-10)
    return density

# ============================================================================
# VISUALIZATION
# ============================================================================

def create_analysis_figure(embeddings, texts, concepts, output_path, n_clusters_override=None, 
                           is_controversial=False, hedge_data=None):
    """Create comprehensive analysis figure with clean 3-panel layout.
    
    Layout matches the design:
    - Left (large): Idea Space Cluster Map
    - Right: Cluster keywords (neutral) or Hedge phrases to filter (controversial)
    - Bottom (wide): Sample Outputs
    
    Args:
        is_controversial: If True, show hedge phrases instead of keywords in the table
        hedge_data: Dict with 'hedge_sentences' list (for controversial analysis)
    """
    
    fig = plt.figure(figsize=(18, 12))
    
    # Compute shared data
    pca = PCA(n_components=2)
    coords_2d = pca.fit_transform(embeddings)
    
    # Determine number of clusters
    if n_clusters_override:
        n_clusters = n_clusters_override
    else:
        optimal_k, k_range, inertias = find_optimal_clusters(embeddings)
        n_clusters = max(optimal_k, 3)
        n_clusters = min(n_clusters, 10)
    
    # Extract phrases for controversial analysis
    labels, clusters, kmeans = cluster_and_label(
        embeddings, texts, concepts, n_clusters, 
        extract_cluster_phrases=is_controversial
    )
    
    # Project centroids to 2D
    centroids_2d = pca.transform(kmeans.cluster_centers_)
    
    # Sort clusters by size for consistent display
    sorted_indices = sorted(range(n_clusters), key=lambda i: clusters[i]['percentage'], reverse=True)
    
    # ========================================================================
    # PANEL 1: Idea Space Cluster Map (left, large)
    # ========================================================================
    ax1 = fig.add_axes([0.04, 0.30, 0.48, 0.65])  # [left, bottom, width, height]
    
    # Plot points colored by cluster
    for i in range(n_clusters):
        mask = labels == i
        ax1.scatter(
            coords_2d[mask, 0], coords_2d[mask, 1],
            c=COLORS[i % len(COLORS)],
            s=50, alpha=0.6,
            label=f"{clusters[i]['label']} ({clusters[i]['percentage']:.0f}%)"
        )
    
    # Plot centroids with labels
    for i in range(n_clusters):
        ax1.scatter(
            centroids_2d[i, 0], centroids_2d[i, 1],
            c=COLORS[i % len(COLORS)],
            s=350, marker='*', edgecolors='black', linewidths=1.5
        )
        ax1.annotate(
            clusters[i]['label'],
            (centroids_2d[i, 0], centroids_2d[i, 1]),
            xytext=(10, 10), textcoords='offset points',
            fontsize=10, fontweight='bold',
            bbox=dict(boxstyle='round,pad=0.3', facecolor='white', alpha=0.9, edgecolor='gray')
        )
    
    ax1.set_xlabel(f'PC1 ({pca.explained_variance_ratio_[0]*100:.1f}% variance)', fontsize=11)
    ax1.set_ylabel(f'PC2 ({pca.explained_variance_ratio_[1]*100:.1f}% variance)', fontsize=11)
    ax1.set_title('Idea Space Cluster Map', fontsize=14, fontweight='bold')
    ax1.legend(loc='upper left', fontsize=9, framealpha=0.95)
    
    # ========================================================================
    # PANEL 2: Right panel - Keywords (neutral) or Hedge Phrases (controversial)
    # ========================================================================
    ax2 = fig.add_axes([0.55, 0.30, 0.43, 0.65])
    ax2.axis('off')
    
    if is_controversial and hedge_data and hedge_data.get('hedge_sentences'):
        # Show identified hedge phrases to filter
        hedge_sentences = hedge_data['hedge_sentences']
        
        ax2.text(0.5, 0.98, "Hedging Phrases to Filter", fontsize=13, fontweight='bold',
                 transform=ax2.transAxes, ha='center', va='top', color='#c00000')
        
        ax2.text(0.5, 0.93, f"({len(hedge_sentences)} topic-agnostic evasive patterns identified)", 
                fontsize=9, color='#666666', style='italic',
                transform=ax2.transAxes, ha='center', va='top')
        
        y_pos = 0.86
        
        # Show hedge phrases (up to 20)
        for i, sentence in enumerate(hedge_sentences[:20]):
            # Truncate if too long
            display = sentence[:85] + "..." if len(sentence) > 85 else sentence
            
            ax2.text(0.03, y_pos, f"{i+1}.", fontsize=9, fontweight='bold',
                    color='#c00000', transform=ax2.transAxes, va='top')
            ax2.text(0.07, y_pos, f'"{display}"', fontsize=8, 
                    color='#333333', style='italic', transform=ax2.transAxes, va='top')
            y_pos -= 0.038
            
            if y_pos < 0.05:
                remaining = len(hedge_sentences) - i - 1
                if remaining > 0:
                    ax2.text(0.5, y_pos, f"... and {remaining} more", fontsize=8,
                            color='#999999', transform=ax2.transAxes, ha='center', va='top')
                break
    else:
        # Show cluster keywords (for neutral analysis or if no hedge data)
        table_title = "Cluster Keywords"
        ax2.text(0.5, 0.98, table_title, fontsize=13, fontweight='bold',
                 transform=ax2.transAxes, ha='center', va='top')
        
        y_pos = 0.90
        
        for idx, i in enumerate(sorted_indices):
            c = clusters[i]
            color = COLORS[i % len(COLORS)]
            
            # Cluster header with color indicator
            header_text = f"● {c['label']}  ({c['percentage']:.1f}%, {c['size']} samples)"
            ax2.text(0.03, y_pos, header_text, fontsize=10, fontweight='bold', 
                    color=color, transform=ax2.transAxes, va='top')
            y_pos -= 0.04
            
            # Keywords
            keywords = ', '.join([w for w, _ in c['keywords'][:5]])
            ax2.text(0.05, y_pos, f"Keywords: {keywords}", fontsize=9, 
                    color='#333333', transform=ax2.transAxes, va='top')
            y_pos -= 0.035
            
            y_pos -= 0.015  # Space between clusters
    
    # ========================================================================
    # PANEL 3: Sample Outputs (bottom, full width)
    # ========================================================================
    ax3 = fig.add_axes([0.02, 0.02, 0.96, 0.25])
    ax3.axis('off')
    
    ax3.text(0.5, 0.97, "Sample Outputs", fontsize=13, fontweight='bold',
             transform=ax3.transAxes, ha='center', va='top')
    
    import textwrap
    
    # Show more samples from top clusters (up to 6)
    y_pos = 0.88
    n_samples = min(6, len(sorted_indices))
    
    for idx in range(n_samples):
        cluster_id = sorted_indices[idx]
        c = clusters[cluster_id]
        color = COLORS[cluster_id % len(COLORS)]
        
        # Get example text
        if c['texts'] and len(c['texts']) > 0:
            example = c['texts'][0]
            example = example.replace('\n', ' ').replace('  ', ' ')
            # Allow longer text for full width display
            if len(example) > 400:
                example = example[:400] + '...'
        else:
            example = "(no examples)"
        
        # Cluster indicator and text on same line, full width
        label_text = f"● {c['label']}: "
        ax3.text(0.01, y_pos, label_text, fontsize=9, fontweight='bold',
                color=color, transform=ax3.transAxes, va='top')
        
        # Output text - full width wrapping (starts right after label)
        wrapped = textwrap.fill(f'"{example}"', width=200)
        n_lines = wrapped.count('\n') + 1
        ax3.text(0.065, y_pos, wrapped, fontsize=8, style='italic', color='#333333',
                transform=ax3.transAxes, va='top')
        y_pos -= 0.045 * n_lines + 0.02
    
    # ========================================================================
    # MAIN TITLE
    # ========================================================================
    analysis_type = "Controversial" if is_controversial else "Neutral"
    fig.suptitle(
        f'LLM Output Analysis ({analysis_type}): {len(embeddings)} Probes → {n_clusters} Clusters',
        fontsize=16, fontweight='bold', y=0.99
    )
    
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    print(f"Saved: {output_path}")
    
    return clusters


def create_detailed_cluster_view(embeddings, texts, clusters, labels, output_path, is_controversial=False):
    """Create detailed view of each cluster with example texts"""
    
    n_clusters = len(clusters)
    fig, axes = plt.subplots(2, 3, figsize=(16, 10))
    axes = axes.flatten()
    
    pca = PCA(n_components=2)
    coords_2d = pca.fit_transform(embeddings)
    
    for i in range(min(n_clusters, 6)):
        ax = axes[i]
        
        # Plot all points in gray
        ax.scatter(coords_2d[:, 0], coords_2d[:, 1], 
                  c='lightgray', s=15, alpha=0.3)
        
        # Highlight this cluster
        mask = labels == i
        ax.scatter(coords_2d[mask, 0], coords_2d[mask, 1],
                  c=COLORS[i], s=40, alpha=0.7)
        
        # Title with keywords or phrases
        if is_controversial and clusters[i].get('phrases'):
            # Show top phrase for controversial
            phrases = [p for p, _ in clusters[i]['phrases'][:2]]
            subtitle = '; '.join(phrases) if phrases else ''
        else:
            keywords = ', '.join([w for w, _ in clusters[i]['keywords'][:3]])
            subtitle = keywords
        
        ax.set_title(
            f"{clusters[i]['label']}\n({clusters[i]['percentage']:.1f}%) - {subtitle}",
            fontsize=10, fontweight='bold'
        )
        
        ax.set_xlabel('PC1', fontsize=9)
        ax.set_ylabel('PC2', fontsize=9)
        
        # Add border
        for spine in ax.spines.values():
            spine.set_linewidth(1.5)
    
    # Hide unused axes
    for i in range(n_clusters, 6):
        axes[i].axis('off')
    
    analysis_type = "Controversial" if is_controversial else "Neutral"
    plt.suptitle(f'Individual Cluster Views ({analysis_type})', fontsize=14, fontweight='bold')
    plt.tight_layout()
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    print(f"Saved: {output_path}")


def print_cluster_summary(clusters):
    """Print detailed cluster information to console"""
    
    print("\n" + "="*80)
    print("CLUSTER ANALYSIS SUMMARY")
    print("="*80)
    
    for i, (idx, c) in enumerate(sorted(clusters.items(), 
                                        key=lambda x: x[1]['percentage'], 
                                        reverse=True)):
        print(f"\n{'─'*80}")
        print(f"CLUSTER {i+1}: {c['label']}")
        print(f"{'─'*80}")
        print(f"  Size: {c['size']} outputs ({c['percentage']:.1f}%)")
        print(f"  Keywords: {c['keyword_str']}")
        print(f"\n  Sample outputs:")
        for j, text in enumerate(c['texts'][:3]):
            # Show input concepts if available
            if c.get('concepts') and j < len(c['concepts']):
                concept_a, concept_b = c['concepts'][j]
                if concept_a and concept_b:
                    print(f"    {j+1}. Input: {concept_a} + {concept_b}")
            
            full_text = text.replace('\n', ' ')
            print(f"       Output: {full_text}")


def print_statistics(embeddings, texts):
    """Print embedding space statistics"""
    
    print("\n" + "="*80)
    print("EMBEDDING SPACE STATISTICS")
    print("="*80)
    
    # Pairwise distances
    if len(embeddings) > 500:
        sample_idx = np.random.choice(len(embeddings), 500, replace=False)
        distances = pdist(embeddings[sample_idx])
    else:
        distances = pdist(embeddings)
    
    print(f"\n  Pairwise Distances:")
    print(f"    Mean:   {np.mean(distances):.4f}")
    print(f"    Median: {np.median(distances):.4f}")
    print(f"    Std:    {np.std(distances):.4f}")
    print(f"    Range:  [{np.min(distances):.4f}, {np.max(distances):.4f}]")
    
    # PCA analysis
    pca = PCA()
    pca.fit(embeddings)
    cumvar = np.cumsum(pca.explained_variance_ratio_)
    
    print(f"\n  Dimensionality:")
    print(f"    Original dimensions: {embeddings.shape[1]}")
    print(f"    Components for 50% variance: {np.argmax(cumvar >= 0.50) + 1}")
    print(f"    Components for 90% variance: {np.argmax(cumvar >= 0.90) + 1}")
    print(f"    PC1 explains: {pca.explained_variance_ratio_[0]*100:.1f}%")
    print(f"    PC1+PC2 explains: {sum(pca.explained_variance_ratio_[:2])*100:.1f}%")

# ============================================================================
# MAIN
# ============================================================================

def main():
    if len(sys.argv) < 2:
        print("="*70)
        print("DEEP ANALYSIS v2 - Improved Visualization")
        print("="*70)
        print("\nUsage: python deep_analysis.py <results_file.json> [options]")
        print("\nOptions:")
        print("  <n_clusters>       Force specific number of clusters (e.g., 5)")
        print("  --controversial    Treat as controversial analysis (show phrases in table)")
        print("  --hedge-dir <dir>  Directory containing hedge files (default: lagrange_mapping_results)")
        print("  --no-hedge         Skip hedge phrase analysis")
        print("\nExamples:")
        print("  python deep_analysis.py results.json                    # Auto-detect clusters")
        print("  python deep_analysis.py results.json 5                  # Force 5 clusters")
        print("  python deep_analysis.py results.json --controversial    # Controversial analysis mode")
        print("  python deep_analysis.py results.json --no-hedge         # Skip hedge analysis")
        print("\nAnalysis Modes:")
        print("  Default:        Shows keywords in cluster table")
        print("  Controversial:  Shows extracted phrases instead of keywords")
        print("\nHedge Phrase Analysis:")
        print("  If hedge_sentences_*.json exists, will display empirically discovered")
        print("  hedging patterns - phrases the model uses to avoid taking positions.")
        return
    
    filepath = sys.argv[1]
    
    # Parse arguments
    user_n_clusters = None
    hedge_dir = "lagrange_mapping_results"
    show_hedge = True
    is_controversial = False
    
    i = 2
    while i < len(sys.argv):
        arg = sys.argv[i]
        if arg == "--hedge-dir" and i + 1 < len(sys.argv):
            hedge_dir = sys.argv[i + 1]
            i += 2
        elif arg == "--no-hedge":
            show_hedge = False
            i += 1
        elif arg == "--controversial":
            is_controversial = True
            print("Mode: Controversial analysis (phrases enabled)")
            i += 1
        else:
            # Try to parse as cluster count
            try:
                user_n_clusters = int(arg)
                print(f"Using user-specified cluster count: {user_n_clusters}")
            except ValueError:
                pass
            i += 1
    
    if not os.path.exists(filepath):
        print(f"Error: File not found: {filepath}")
        return
    
    # Load data
    embeddings, texts, concepts, config = load_data(filepath)
    
    if len(embeddings) == 0:
        print("Error: No valid embeddings found")
        return
    
    # Output directory
    output_dir = os.path.dirname(filepath) or '.'
    base_name = os.path.splitext(os.path.basename(filepath))[0]
    
    # Print statistics
    print_statistics(embeddings, texts)
    
    # Create main analysis figure
    main_output = os.path.join(output_dir, f'{base_name}_analysis.png')
    clusters = create_analysis_figure(
        embeddings, texts, concepts, main_output, 
        n_clusters_override=user_n_clusters,
        is_controversial=is_controversial
    )
    
    # Print cluster summary
    print_cluster_summary(clusters)
    
    # Create detailed cluster views
    n_clusters = len(clusters)
    labels, _, _ = cluster_and_label(
        embeddings, texts, concepts, n_clusters, 
        extract_cluster_phrases=is_controversial
    )
    detail_output = os.path.join(output_dir, f'{base_name}_clusters.png')
    create_detailed_cluster_view(
        embeddings, texts, clusters, labels, detail_output,
        is_controversial=is_controversial
    )
    
    # ========================================================================
    # HEDGE PHRASE ANALYSIS
    # ========================================================================
    hedge_data = None
    if show_hedge:
        # Try to find hedge files
        centroid_path, sentences_path = find_hedge_files(hedge_dir)
        
        # Also try the same directory as the input file
        if sentences_path is None:
            centroid_path, sentences_path = find_hedge_files(output_dir)
        
        if sentences_path:
            print("\n" + "="*80)
            print("HEDGE PHRASE ANALYSIS (Empirical)")
            print("="*80)
            print(f"\n  Source: {sentences_path.name}")
            
            hedge_data = load_hedge_data(sentences_path)
            print_hedge_summary(hedge_data)
    
    # ========================================================================
    # SUMMARY
    # ========================================================================
    print("\n" + "="*80)
    print("ANALYSIS COMPLETE")
    print("="*80)
    print(f"\nOutputs saved to:")
    print(f"  {main_output}")
    print(f"  {detail_output}")
    
    if hedge_data and hedge_data.get("hedge_sentences"):
        print(f"\nHedge phrases found: {len(hedge_data['hedge_sentences'])}")
        print(f"  These are topic-agnostic evasive phrases the model uses to avoid")
        print(f"  taking clear positions on controversial topics.")


if __name__ == "__main__":
    main()
