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


def cluster_and_label(embeddings, texts, concepts, n_clusters=5):
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
        
        clusters[new_i] = {
            'size': mask.sum(),
            'percentage': mask.sum() / len(labels) * 100,
            'keywords': keywords,
            'keyword_str': keyword_str,
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

def create_analysis_figure(embeddings, texts, concepts, output_path, n_clusters_override=None):
    """Create comprehensive analysis figure with clean layout"""
    
    fig = plt.figure(figsize=(20, 14))
    
    # Create grid layout: 
    # Row 1: Cluster Map (large), Density Map, Cluster Bar Chart
    # Row 2: Sample Outputs (wide), Keyword Table
    
    # Compute shared data
    pca = PCA(n_components=2)
    coords_2d = pca.fit_transform(embeddings)
    
    density = compute_density(embeddings)
    
    # Determine number of clusters
    if n_clusters_override:
        n_clusters = n_clusters_override
    else:
        optimal_k, k_range, inertias = find_optimal_clusters(embeddings)
        n_clusters = max(optimal_k, 3)
        n_clusters = min(n_clusters, 10)  # Increased cap for more clusters
    
    labels, clusters, kmeans = cluster_and_label(embeddings, texts, concepts, n_clusters)
    
    # Project centroids to 2D
    centroids_2d = pca.transform(kmeans.cluster_centers_)
    
    # ========================================================================
    # PANEL 1: Main Cloud with Clusters (top left, large)
    # ========================================================================
    ax1 = fig.add_axes([0.03, 0.38, 0.42, 0.58])  # [left, bottom, width, height]
    
    # Plot points colored by cluster
    for i in range(n_clusters):
        mask = labels == i
        ax1.scatter(
            coords_2d[mask, 0], coords_2d[mask, 1],
            c=COLORS[i % len(COLORS)],
            s=40, alpha=0.6,
            label=f"{clusters[i]['label']} ({clusters[i]['percentage']:.0f}%)"
        )
    
    # Plot centroids with labels
    for i in range(n_clusters):
        ax1.scatter(
            centroids_2d[i, 0], centroids_2d[i, 1],
            c=COLORS[i % len(COLORS)],
            s=300, marker='*', edgecolors='black', linewidths=1.5
        )
        ax1.annotate(
            clusters[i]['label'],
            (centroids_2d[i, 0], centroids_2d[i, 1]),
            xytext=(10, 10), textcoords='offset points',
            fontsize=9, fontweight='bold',
            bbox=dict(boxstyle='round,pad=0.3', facecolor='white', alpha=0.8)
        )
    
    ax1.set_xlabel(f'PC1 ({pca.explained_variance_ratio_[0]*100:.1f}% variance)', fontsize=11)
    ax1.set_ylabel(f'PC2 ({pca.explained_variance_ratio_[1]*100:.1f}% variance)', fontsize=11)
    ax1.set_title('Idea Space: Cluster Map', fontsize=14, fontweight='bold')
    
    # Move legend outside
    ax1.legend(loc='upper left', fontsize=8, framealpha=0.9)
    
    # ========================================================================
    # PANEL 2: Density Heatmap with Labels (top middle)
    # ========================================================================
    ax2 = fig.add_axes([0.48, 0.55, 0.24, 0.40])
    
    scatter = ax2.scatter(
        coords_2d[:, 0], coords_2d[:, 1],
        c=density, cmap='YlOrRd', s=25, alpha=0.7
    )
    cbar = plt.colorbar(scatter, ax=ax2, shrink=0.8)
    cbar.set_label('Density', fontsize=9)
    
    # Find and label high-density regions
    density_threshold = np.percentile(density, 90)
    high_density_mask = density > density_threshold
    high_density_indices = np.where(high_density_mask)[0]
    
    if len(high_density_indices) > 0:
        high_density_coords = coords_2d[high_density_indices]
        n_peaks = min(4, max(2, len(high_density_indices) // 15))
        
        if n_peaks >= 2 and len(high_density_indices) >= n_peaks:
            peak_kmeans = KMeans(n_clusters=n_peaks, random_state=42, n_init=10)
            peak_labels = peak_kmeans.fit_predict(high_density_coords)
            peak_centers = peak_kmeans.cluster_centers_
            
            for i in range(n_peaks):
                peak_mask = peak_labels == i
                peak_point_indices = high_density_indices[peak_mask]
                peak_texts = [texts[j] for j in peak_point_indices]
                
                peak_keywords = extract_keywords(peak_texts, top_n=2, min_word_len=4)
                if peak_keywords:
                    label = '/'.join([w for w, c in peak_keywords[:2]])
                    
                    ax2.annotate(
                        label,
                        (peak_centers[i, 0], peak_centers[i, 1]),
                        fontsize=7, fontweight='bold',
                        color='darkred',
                        bbox=dict(boxstyle='round,pad=0.2', facecolor='white', alpha=0.8, edgecolor='darkred'),
                        ha='center'
                    )
    
    ax2.set_xlabel('PC1', fontsize=9)
    ax2.set_ylabel('PC2', fontsize=9)
    ax2.set_title('Density Map: Hot Spots', fontsize=11, fontweight='bold')
    
    # ========================================================================
    # PANEL 3: Cluster Size Bar Chart (top right)
    # ========================================================================
    ax3 = fig.add_axes([0.76, 0.55, 0.22, 0.40])
    
    # Sort by percentage for cleaner display
    sorted_indices = sorted(range(n_clusters), key=lambda i: clusters[i]['percentage'], reverse=True)
    cluster_names = [clusters[i]['label'] for i in sorted_indices]
    cluster_sizes = [clusters[i]['percentage'] for i in sorted_indices]
    bar_colors = [COLORS[i % len(COLORS)] for i in sorted_indices]
    
    bars = ax3.barh(range(n_clusters), cluster_sizes, color=bar_colors)
    
    ax3.set_yticks(range(n_clusters))
    ax3.set_yticklabels(cluster_names, fontsize=9)
    ax3.set_xlabel('% of Outputs', fontsize=10)
    ax3.set_title('Cluster Distribution', fontsize=11, fontweight='bold')
    ax3.invert_yaxis()  # Largest at top
    
    # Add percentage labels
    for bar, pct in zip(bars, cluster_sizes):
        ax3.text(bar.get_width() + 0.3, bar.get_y() + bar.get_height()/2,
                f'{pct:.1f}%', va='center', fontsize=8)
    
    ax3.set_xlim(0, max(cluster_sizes) * 1.2)
    
    # ========================================================================
    # PANEL 4: Sample Outputs with Input Concepts (bottom left)
    # ========================================================================
    ax4 = fig.add_axes([0.03, 0.03, 0.45, 0.30])
    ax4.axis('off')
    
    # Show top 4 clusters with examples (need room for concepts)
    sorted_clusters = sorted(clusters.items(), key=lambda x: x[1]['percentage'], reverse=True)[:4]
    
    y_pos = 0.95
    ax4.text(0.0, y_pos, "Sample Outputs by Cluster", fontsize=11, fontweight='bold',
             transform=ax4.transAxes, verticalalignment='top')
    y_pos -= 0.06
    
    import textwrap
    
    for idx, (cluster_id, c) in enumerate(sorted_clusters):
        # Get example text (full, no truncation)
        if c['texts'] and len(c['texts']) > 0:
            example = c['texts'][0]
            example = example.replace('\n', ' ').replace('  ', ' ')
        else:
            example = "(no examples)"
        
        # Get input concepts - check if they look like real concepts (short words, not synthesis)
        input_text = None
        if c.get('concepts') and len(c['concepts']) > 0:
            concept_a, concept_b = c['concepts'][0]
            # Only show if they look like actual concept words (not synthesis text)
            if (concept_a and concept_b and 
                len(concept_a) < 40 and len(concept_b) < 40 and
                not concept_a.upper().startswith('SYNTHESIS') and
                not concept_b.upper().startswith('SYNTHESIS')):
                input_text = f"{concept_a} + {concept_b}"
        
        # Cluster name in color
        ax4.text(0.0, y_pos, f"● {c['label']} ({c['percentage']:.0f}%)", 
                fontsize=9, fontweight='bold', color=COLORS[cluster_id % len(COLORS)],
                transform=ax4.transAxes, verticalalignment='top')
        
        # Show input concepts only if valid
        if input_text:
            y_pos -= 0.04
            ax4.text(0.02, y_pos, f"Input: {input_text}",
                    fontsize=7, color='#666666',
                    transform=ax4.transAxes, verticalalignment='top')
        
        # Output text - wrap to multiple lines
        wrapped = textwrap.fill(f"→ \"{example}\"", width=90)
        n_lines = wrapped.count('\n') + 1
        y_pos -= 0.04
        ax4.text(0.02, y_pos, wrapped,
                fontsize=7, style='italic', color='#333333',
                transform=ax4.transAxes, verticalalignment='top')
        y_pos -= 0.04 * n_lines + 0.02
    
    # ========================================================================
    # PANEL 5: Cluster Keywords Table (bottom right)
    # ========================================================================
    ax5 = fig.add_axes([0.52, 0.03, 0.46, 0.30])
    ax5.axis('off')
    
    # Build table data - sorted by size
    table_data = []
    for i in sorted_indices:
        c = clusters[i]
        keywords = ', '.join([w for w, _ in c['keywords'][:4]])
        table_data.append([
            c['label'],
            f"{c['percentage']:.1f}%",
            keywords
        ])
    
    table = ax5.table(
        cellText=table_data,
        colLabels=['Cluster', 'Size', 'Top Keywords'],
        loc='upper center',
        cellLoc='left',
        colWidths=[0.28, 0.10, 0.62]
    )
    table.auto_set_font_size(False)
    table.set_fontsize(9)
    table.scale(1.0, 1.6)
    
    # Style header
    for i in range(3):
        table[(0, i)].set_facecolor('#4472C4')
        table[(0, i)].set_text_props(color='white', fontweight='bold')
    
    # Alternate row colors
    for i in range(1, len(table_data) + 1):
        for j in range(3):
            if i % 2 == 0:
                table[(i, j)].set_facecolor('#E8EEF7')
    
    # ========================================================================
    # MAIN TITLE
    # ========================================================================
    fig.suptitle(
        f'LLM Output Analysis: {len(embeddings)} Probes → {n_clusters} Clusters',
        fontsize=16, fontweight='bold', y=0.98
    )
    
    plt.savefig(output_path, dpi=150, bbox_inches='tight', facecolor='white')
    print(f"Saved: {output_path}")
    
    return clusters


def create_detailed_cluster_view(embeddings, texts, clusters, labels, output_path):
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
        
        # Title with keywords
        keywords = ', '.join([w for w, _ in clusters[i]['keywords'][:3]])
        ax.set_title(
            f"Cluster {i+1}: {clusters[i]['label']}\n({clusters[i]['percentage']:.1f}%) - {keywords}",
            fontsize=10, fontweight='bold'
        )
        
        ax.set_xlabel('PC1', fontsize=9)
        ax.set_ylabel('PC2', fontsize=9)
    
    # Hide unused axes
    for i in range(n_clusters, 6):
        axes[i].axis('off')
    
    plt.suptitle('Individual Cluster Views', fontsize=14, fontweight='bold')
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
        print("  <n_clusters>      Force specific number of clusters (e.g., 5)")
        print("  --hedge-dir <dir> Directory containing hedge files (default: lagrange_mapping_results)")
        print("  --no-hedge        Skip hedge phrase analysis")
        print("\nExamples:")
        print("  python deep_analysis.py results.json           # Auto-detect clusters")
        print("  python deep_analysis.py results.json 5         # Force 5 clusters")
        print("  python deep_analysis.py results.json --no-hedge  # Skip hedge analysis")
        print("\nHedge Phrase Analysis:")
        print("  If hedge_sentences_*.json exists, will display empirically discovered")
        print("  hedging patterns - phrases the model uses to avoid taking positions.")
        return
    
    filepath = sys.argv[1]
    
    # Parse arguments
    user_n_clusters = None
    hedge_dir = "lagrange_mapping_results"
    show_hedge = True
    
    i = 2
    while i < len(sys.argv):
        arg = sys.argv[i]
        if arg == "--hedge-dir" and i + 1 < len(sys.argv):
            hedge_dir = sys.argv[i + 1]
            i += 2
        elif arg == "--no-hedge":
            show_hedge = False
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
    clusters = create_analysis_figure(embeddings, texts, concepts, main_output, n_clusters_override=user_n_clusters)
    
    # Print cluster summary
    print_cluster_summary(clusters)
    
    # Create detailed cluster views
    n_clusters = len(clusters)
    labels, _, _ = cluster_and_label(embeddings, texts, concepts, n_clusters)
    detail_output = os.path.join(output_dir, f'{base_name}_clusters.png')
    create_detailed_cluster_view(embeddings, texts, clusters, labels, detail_output)
    
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
