#!/usr/bin/env python3
"""
Attractor Pipeline Runner

Orchestrates the full LLM Attractor Mapping Pipeline:
1. attractor_mapper.py  - Generate probes and map attractor landscape
2. deep_analysis.py     - Analyze clusters and visualize results
3. extract_filters.py   - Extract filter configuration for steering
4. attractor_steering.py - Test the steering system

All configuration is centralized here and passed to each module.
"""

import os
import sys
import json
from datetime import datetime
from pathlib import Path

# ============================================================================
# CENTRALIZED CONFIGURATION
# ============================================================================

# ============================================================================
# UNITY IR CONFIGURATION
# ============================================================================

# Mode selection
PROBE_MODE = "unity_ir"  # Options: "controversial", "concept_pairs", "unity_ir"

# Unity IR specific
UNITY_BEHAVIOR_COUNT = 100  # Number of behavior prompts to test
USE_CODE_LEAK_DETECTION = True

# ============================================================================
# BASELINE GENERATION SETTINGS
# ============================================================================

# Reference model for baselines
REFERENCE_MODEL = "claude-3-5-haiku-20241022"

# Enable baseline generation during pipeline
GENERATE_BASELINES = True
BASELINE_CACHE_DIR = "baseline_cache"

# Baseline tolerance bands
BASELINE_TOLERANCE = {
    "fields": 0.5,      # ±50%
    "behaviors": 0.5,
    "states": 0.5,
}

# Structural clustering
N_STRUCTURAL_CLUSTERS = 5

# ============================================================================
# API KEYS - SET YOUR KEY HERE
# ============================================================================
ANTHROPIC_API_KEY = ""  # "sk-ant-..." 

# ============================================================================
# MODEL CONFIGURATION
# ============================================================================

# Claude API for PROBE GENERATION (generates random concept pairs)
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Local LLM for SYNTHESIS (the model we're actually mapping)
LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "local-model"  # Your synthesis model name

# Local LLM for EMBEDDINGS (to measure where outputs cluster)
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "text-embedding-nomic-embed-text-v1.5"  # Your embedding model name

# ============================================================================
# EXPERIMENT PARAMETERS
# ============================================================================

N_PROBES = 1000              # Number of random concept pairs to test
N_ITERATIONS = 1             # Single iteration is sufficient
# Note: N_CLUSTERS is defined in ANALYSIS PARAMETERS section below

# Mode selection
USE_CLAUDE_FOR_PROBES = True  # Use Claude to generate diverse concept pairs

# ============================================================================
# ANALYSIS PARAMETERS
# ============================================================================

N_CLUSTERS = 8  # Auto-detect if None, or set to a specific number (e.g., 5)

# ============================================================================
# OUTPUT CONFIGURATION
# ============================================================================

# Model name for filter configs (used in directory naming)
MODEL_NAME = "local-model"  # Change to match your actual model
# For Unity IR mode, will use MODEL_NAME + "-unity-ir" automatically

# Output directories
RESULTS_DIR = "lagrange_mapping_results"  # Will use "unity_ir_mapping_results" for Unity IR mode
FILTER_CONFIG_DIR = "filter_configs"  # Will use "unity_ir_filter_configs" for Unity IR mode

# Timestamp for this run
TIMESTAMP = datetime.now().strftime("%Y%m%d_%H%M%S")

# ============================================================================
# PIPELINE CONTROL
# ============================================================================

# Which steps to run
RUN_MAPPER = True
RUN_ANALYSIS = True
RUN_EXTRACT_FILTERS = True
RUN_STEERING_TEST = True

# Test text for steering verification
STEERING_TEST_TEXT = "Blockchain DAOs enable decentralized governance through smart contracts"

# ============================================================================
# CONTROVERSIAL PROBE SETTINGS
# ============================================================================

# Enable controversial probes to capture hedging/both-sideism patterns
USE_CONTROVERSIAL_PROBES = True  # Include controversial questions alongside concept pairs

# Ratio of probes that should be controversial (0-1)
# 0.0 = all neutral concept pairs
# 0.5 = 50% controversial, 50% neutral (RECOMMENDED)
# 1.0 = all controversial questions
CONTROVERSIAL_PROBE_RATIO = 0.5  # Default: balanced mix of both

# Whether to analyze controversial probes separately and create dual filter configs
SEPARATE_CONTROVERSIAL_ANALYSIS = True

# ============================================================================
# CONCEPT POOL (used if Claude probe generation is disabled)
# ============================================================================

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
# CONFIGURATION INJECTION
# ============================================================================

def inject_config_to_mapper():
    """Inject configuration into attractor_mapper module"""
    import attractor_mapper
    
    attractor_mapper.ANTHROPIC_API_KEY = ANTHROPIC_API_KEY
    attractor_mapper.CLAUDE_MODEL = CLAUDE_MODEL
    attractor_mapper.LOCAL_SYNTHESIS_URL = LOCAL_SYNTHESIS_URL
    attractor_mapper.LOCAL_SYNTHESIS_MODEL = LOCAL_SYNTHESIS_MODEL
    attractor_mapper.LOCAL_EMBEDDING_URL = LOCAL_EMBEDDING_URL
    attractor_mapper.LOCAL_EMBEDDING_MODEL = LOCAL_EMBEDDING_MODEL
    attractor_mapper.N_PROBES = N_PROBES
    attractor_mapper.N_ITERATIONS = N_ITERATIONS
    attractor_mapper.N_CLUSTERS = N_CLUSTERS
    attractor_mapper.USE_CLAUDE_FOR_PROBES = USE_CLAUDE_FOR_PROBES
    attractor_mapper.RESULTS_DIR = RESULTS_DIR
    attractor_mapper.TIMESTAMP = TIMESTAMP
    attractor_mapper.CONCEPT_POOL = CONCEPT_POOL
    
    # Controversial probe settings
    attractor_mapper.USE_CONTROVERSIAL_PROBES = USE_CONTROVERSIAL_PROBES
    attractor_mapper.CONTROVERSIAL_PROBE_RATIO = CONTROVERSIAL_PROBE_RATIO
    attractor_mapper.PROBE_MODE = PROBE_MODE
    attractor_mapper.USE_CODE_LEAK_DETECTION = USE_CODE_LEAK_DETECTION
    
    # Baseline settings
    attractor_mapper.ANTHROPIC_API_KEY = ANTHROPIC_API_KEY
    attractor_mapper.REFERENCE_MODEL = REFERENCE_MODEL
    attractor_mapper.GENERATE_BASELINES = GENERATE_BASELINES
    attractor_mapper.BASELINE_CACHE_DIR = BASELINE_CACHE_DIR
    attractor_mapper.N_STRUCTURAL_CLUSTERS = N_STRUCTURAL_CLUSTERS
    
    if PROBE_MODE == "unity_ir":
        attractor_mapper.N_PROBES = UNITY_BEHAVIOR_COUNT
        attractor_mapper.RESULTS_DIR = "unity_ir_mapping_results"


def inject_config_to_steering():
    """Inject configuration into attractor_steering module"""
    import attractor_steering
    
    attractor_steering.DEFAULT_EMBEDDING_URL = LOCAL_EMBEDDING_URL
    attractor_steering.DEFAULT_EMBEDDING_MODEL = LOCAL_EMBEDDING_MODEL
    attractor_steering.DEFAULT_CONFIG_DIR = FILTER_CONFIG_DIR


# ============================================================================
# PIPELINE STEPS
# ============================================================================

def check_existing_probes_for_missing_types(results_dir: str) -> dict:
    """
    Check existing probe data for missing probe types.
    
    Returns:
        Dict with keys: 'has_neutral', 'has_controversial', 'n_neutral', 'n_controversial',
                       'latest_file', 'probes'
    """
    import json
    from pathlib import Path
    
    result = {
        'has_neutral': False,
        'has_controversial': False,
        'n_neutral': 0,
        'n_controversial': 0,
        'latest_file': None,
        'probes': []
    }
    
    results_path = Path(results_dir)
    if not results_path.exists():
        return result
    
    # Find most recent results file
    result_files = sorted(results_path.glob("full_results_*.json"), reverse=True)
    intermediate_files = sorted(results_path.glob("intermediate_*.json"), reverse=True)
    
    # Try full results first, then intermediate
    files_to_check = list(result_files) + list(intermediate_files)
    
    for filepath in files_to_check:
        try:
            with open(filepath, 'r') as f:
                data = json.load(f)
            
            probes = data.get('probes', data if isinstance(data, list) else [])
            
            if not probes:
                continue
            
            result['latest_file'] = str(filepath)
            result['probes'] = probes
            
            # Count probe types
            for probe in probes:
                probe_type = probe.get('probe_type', 'neutral')
                # Also check legacy format
                if probe.get('initial_b') == 'controversial':
                    probe_type = 'controversial'
                
                if probe_type == 'controversial':
                    result['n_controversial'] += 1
                else:
                    result['n_neutral'] += 1
            
            result['has_neutral'] = result['n_neutral'] > 0
            result['has_controversial'] = result['n_controversial'] > 0
            
            break  # Use the first valid file found
            
        except Exception as e:
            continue
    
    return result


def generate_missing_probes(existing_info: dict, target_n_probes: int, controversial_ratio: float) -> str:
    """
    Generate only the missing probe types and merge with existing data.
    
    Args:
        existing_info: Result from check_existing_probes_for_missing_types()
        target_n_probes: Target total number of probes
        controversial_ratio: Target ratio of controversial probes
    
    Returns:
        Path to the merged results file
    """
    import json
    import numpy as np
    
    inject_config_to_mapper()
    import attractor_mapper
    
    existing_probes = existing_info['probes']
    n_existing = len(existing_probes)
    n_existing_neutral = existing_info['n_neutral']
    n_existing_controversial = existing_info['n_controversial']
    
    # Calculate what we need
    target_controversial = int(target_n_probes * controversial_ratio)
    target_neutral = target_n_probes - target_controversial
    
    need_controversial = max(0, target_controversial - n_existing_controversial)
    need_neutral = max(0, target_neutral - n_existing_neutral)
    
    print(f"\n{'='*80}")
    print("CHECKING FOR MISSING PROBE TYPES")
    print(f"{'='*80}")
    print(f"  Existing probes: {n_existing}")
    print(f"    - Neutral: {n_existing_neutral}")
    print(f"    - Controversial: {n_existing_controversial}")
    print(f"  Target probes: {target_n_probes}")
    print(f"    - Neutral needed: {target_neutral} (have {n_existing_neutral}, need {need_neutral} more)")
    print(f"    - Controversial needed: {target_controversial} (have {n_existing_controversial}, need {need_controversial} more)")
    
    new_probes = []
    
    # Generate missing controversial probes
    if need_controversial > 0:
        print(f"\n{'='*80}")
        print(f"GENERATING {need_controversial} MISSING CONTROVERSIAL PROBES")
        print(f"{'='*80}")
        
        controversial_pairs = attractor_mapper.generate_controversial_probes(need_controversial, use_cache=True)
        
        for i, (question, marker) in enumerate(controversial_pairs):
            probe_result = attractor_mapper.run_probe(
                n_existing + len(new_probes) + 1,
                question,
                marker
            )
            new_probes.append(probe_result)
            
            # Save intermediate every 10 probes
            if (i + 1) % 10 == 0:
                print(f"\n  → Generated {i + 1}/{need_controversial} controversial probes")
    
    # Generate missing neutral probes
    if need_neutral > 0:
        print(f"\n{'='*80}")
        print(f"GENERATING {need_neutral} MISSING NEUTRAL PROBES")
        print(f"{'='*80}")
        
        neutral_pairs = attractor_mapper.generate_probes_batch(need_neutral, use_cache=True)
        
        for i, (concept_a, concept_b) in enumerate(neutral_pairs):
            probe_result = attractor_mapper.run_probe(
                n_existing + len(new_probes) + 1,
                concept_a,
                concept_b
            )
            new_probes.append(probe_result)
            
            # Save intermediate every 10 probes
            if (i + 1) % 10 == 0:
                print(f"\n  → Generated {i + 1}/{need_neutral} neutral probes")
    
    # Merge probes
    all_probes = existing_probes + new_probes
    
    # Update probe_type for existing probes that don't have it
    for probe in all_probes:
        if 'probe_type' not in probe:
            if probe.get('initial_b') == 'controversial':
                probe['probe_type'] = 'controversial'
            else:
                probe['probe_type'] = 'neutral'
    
    # Extract final embeddings and texts for analysis
    final_embeddings = []
    final_texts = []
    
    for probe in all_probes:
        if probe.get('final_embedding') is not None:
            emb = probe['final_embedding']
            if isinstance(emb, list):
                final_embeddings.append(np.array(emb))
            elif isinstance(emb, np.ndarray):
                final_embeddings.append(emb)
            final_texts.append(probe['trajectory'][-1] if probe.get('trajectory') else "")
    
    # Save merged results (use correct dir for Unity IR mode)
    actual_results_dir = "unity_ir_mapping_results" if PROBE_MODE == "unity_ir" else RESULTS_DIR
    results_file = f"{actual_results_dir}/full_results_{TIMESTAMP}.json"
    
    # Convert numpy arrays for JSON
    save_probes = []
    for p in all_probes:
        p_copy = p.copy()
        if p_copy.get('final_embedding') is not None:
            if isinstance(p_copy['final_embedding'], np.ndarray):
                p_copy['final_embedding'] = p_copy['final_embedding'].tolist()
        if p_copy.get('embeddings'):
            p_copy['embeddings'] = [
                e.tolist() if isinstance(e, np.ndarray) else e 
                for e in p_copy['embeddings']
            ]
        save_probes.append(p_copy)
    
    save_data = {
        "config": {
            "n_probes": len(all_probes),
            "n_iterations": attractor_mapper.N_ITERATIONS,
            "n_clusters": attractor_mapper.N_CLUSTERS,
            "timestamp": TIMESTAMP,
            "merged_from": existing_info['latest_file'],
            "new_probes_added": len(new_probes)
        },
        "probes": save_probes
    }
    
    os.makedirs(actual_results_dir, exist_ok=True)
    with open(results_file, 'w') as f:
        json.dump(save_data, f, indent=2, default=str)
    
    # Count final totals
    final_neutral = sum(1 for p in all_probes if p.get('probe_type') == 'neutral')
    final_controversial = sum(1 for p in all_probes if p.get('probe_type') == 'controversial')
    
    print(f"\n{'='*80}")
    print("MERGE COMPLETE")
    print(f"{'='*80}")
    print(f"  Total probes: {len(all_probes)}")
    print(f"    - Neutral: {final_neutral}")
    print(f"    - Controversial: {final_controversial}")
    print(f"  Saved to: {results_file}")
    
    return results_file


def step_1_mapper():
    """Run the attractor mapper to generate probes and map landscape"""
    print("\n" + "="*80)
    print("STEP 1: ATTRACTOR MAPPING")
    print("="*80)
    
    # Use correct results directory for Unity IR mode
    actual_results_dir = "unity_ir_mapping_results" if PROBE_MODE == "unity_ir" else RESULTS_DIR
    
    # Check for existing data with missing probe types
    # Skip this for Unity IR mode - controversial probes don't apply there
    if PROBE_MODE != "unity_ir" and USE_CONTROVERSIAL_PROBES and CONTROVERSIAL_PROBE_RATIO > 0:
        existing_info = check_existing_probes_for_missing_types(actual_results_dir)
        
        if existing_info['latest_file'] and existing_info['probes']:
            # We have existing data - check if we need to generate missing types
            has_both = existing_info['has_neutral'] and existing_info['has_controversial']
            
            if not has_both:
                # Missing one type - generate just that type
                missing_type = "controversial" if not existing_info['has_controversial'] else "neutral"
                print(f"\n⚠ Existing data is missing {missing_type} probes!")
                print(f"  Will generate missing {missing_type} probes and merge with existing data.")
                
                results_file = generate_missing_probes(
                    existing_info,
                    N_PROBES,
                    CONTROVERSIAL_PROBE_RATIO
                )
                return results_file
    
    # Normal flow - run full experiment
    inject_config_to_mapper()
    import attractor_mapper
    
    attractor_mapper.run_experiment()
    
    # Return the path to the results file (use correct dir for Unity IR mode)
    actual_results_dir = "unity_ir_mapping_results" if PROBE_MODE == "unity_ir" else RESULTS_DIR
    results_file = f"{actual_results_dir}/full_results_{TIMESTAMP}.json"
    return results_file


def step_2_analysis(results_file: str):
    """Run deep analysis on the results, optionally separating controversial probes"""
    print("\n" + "="*80)
    print("STEP 2: DEEP ANALYSIS")
    print("="*80)
    
    if not os.path.exists(results_file):
        print(f"Error: Results file not found: {results_file}")
        return None
    
    import deep_analysis
    
    # Load data
    embeddings, texts, concepts, config = deep_analysis.load_data(results_file)
    
    if len(embeddings) == 0:
        print("Error: No valid embeddings found")
        return None
    
    # Output directory
    output_dir = os.path.dirname(results_file) or '.'
    base_name = os.path.splitext(os.path.basename(results_file))[0]
    
    # Check if we need separate analysis
    if USE_CONTROVERSIAL_PROBES and SEPARATE_CONTROVERSIAL_ANALYSIS:
        # Load raw probe data to check for probe types
        with open(results_file, 'r') as f:
            import json
            data = json.load(f)
        
        probes = data.get('probes', data if isinstance(data, list) else [])
        
        # Separate probes by type
        neutral_indices = []
        controversial_indices = []
        
        for i, probe in enumerate(probes):
            probe_type = probe.get('probe_type', 'neutral')
            if probe_type == 'controversial':
                controversial_indices.append(i)
            else:
                neutral_indices.append(i)
        
        print(f"\nFound {len(neutral_indices)} neutral probes, {len(controversial_indices)} controversial probes")
        
        # Analyze neutral probes
        if neutral_indices:
            print("\n" + "="*70)
            print("ANALYZING NEUTRAL ATTRACTORS")
            print("="*70)
            
            neutral_embeddings = embeddings[neutral_indices]
            neutral_texts = [texts[i] for i in neutral_indices]
            neutral_concepts = [concepts[i] for i in neutral_indices]
            
            if len(neutral_embeddings) > 0:
                deep_analysis.print_statistics(neutral_embeddings, neutral_texts)
                
                neutral_output = os.path.join(output_dir, f'{base_name}_neutral_analysis.png')
                neutral_clusters = deep_analysis.create_analysis_figure(
                    neutral_embeddings, neutral_texts, neutral_concepts, neutral_output,
                    n_clusters_override=N_CLUSTERS
                )
                deep_analysis.print_cluster_summary(neutral_clusters)
        
        # Analyze controversial probes
        if controversial_indices:
            print("\n" + "="*70)
            print("ANALYZING CONTROVERSIAL ATTRACTORS")
            print("="*70)
            
            controversial_embeddings = embeddings[controversial_indices]
            controversial_texts = [texts[i] for i in controversial_indices]
            controversial_concepts = [concepts[i] for i in controversial_indices]
            
            if len(controversial_embeddings) > 0:
                deep_analysis.print_statistics(controversial_embeddings, controversial_texts)
                
                # Load hedge data for controversial analysis
                hedge_data = None
                centroid_path, sentences_path = deep_analysis.find_hedge_files(output_dir)
                if sentences_path:
                    hedge_data = deep_analysis.load_hedge_data(sentences_path)
                    print(f"  Loaded {len(hedge_data.get('hedge_sentences', []))} hedge phrases from {sentences_path.name}")
                else:
                    print("  Note: No hedge phrases file found - run mapper first to generate hedge detection")
                
                controversial_output = os.path.join(output_dir, f'{base_name}_controversial_analysis.png')
                controversial_clusters = deep_analysis.create_analysis_figure(
                    controversial_embeddings, controversial_texts, controversial_concepts, controversial_output,
                    n_clusters_override=N_CLUSTERS,
                    is_controversial=True,
                    hedge_data=hedge_data  # Pass hedge phrases for filtering display
                )
                deep_analysis.print_cluster_summary(controversial_clusters)
        
        print(f"\nAnalysis outputs saved to:")
        if neutral_indices:
            print(f"  {os.path.join(output_dir, f'{base_name}_neutral_analysis.png')}")
        if controversial_indices:
            print(f"  {os.path.join(output_dir, f'{base_name}_controversial_analysis.png')}")
    
    else:
        # No separate analysis - just do a single combined analysis
        print("\n" + "="*70)
        print("ANALYZING ALL PROBES")
        print("="*70)
        
        deep_analysis.print_statistics(embeddings, texts)
        
        main_output = os.path.join(output_dir, f'{base_name}_analysis.png')
        clusters = deep_analysis.create_analysis_figure(
            embeddings, texts, concepts, main_output, 
            n_clusters_override=N_CLUSTERS
        )
        
        deep_analysis.print_cluster_summary(clusters)
        
        print(f"\nAnalysis output saved to:")
        print(f"  {main_output}")
    
    # Return the results file path (for next step)
    return results_file


def step_3_extract_filters(results_file: str):
    """Extract filter configuration from results, optionally separating controversial probes"""
    print("\n" + "="*80)
    print("STEP 3: EXTRACT FILTERS")
    print("="*80)
    
    if not os.path.exists(results_file):
        print(f"Error: Results file not found: {results_file}")
        return None
    
    import extract_filters
    import json
    
    config_path = None
    
    # Use Unity IR directories if in Unity IR mode
    if PROBE_MODE == "unity_ir":
        filter_config_dir = "unity_ir_filter_configs"
        model_name = f"{MODEL_NAME}-unity-ir"
    else:
        filter_config_dir = FILTER_CONFIG_DIR
        model_name = MODEL_NAME
    
    # Check if we need separate filter configs
    if USE_CONTROVERSIAL_PROBES and SEPARATE_CONTROVERSIAL_ANALYSIS:
        # Load raw probe data
        with open(results_file, 'r') as f:
            data = json.load(f)
        
        probes = data.get('probes', data if isinstance(data, list) else [])
        
        # Check how many of each type
        n_neutral = sum(1 for p in probes if p.get('probe_type', 'neutral') == 'neutral')
        n_controversial = sum(1 for p in probes if p.get('probe_type') == 'controversial')
        
        print(f"\nFound {n_neutral} neutral probes, {n_controversial} controversial probes")
        
        # Extract neutral filter config
        if n_neutral > 0:
            print("\n" + "-"*40)
            print("EXTRACTING NEUTRAL ATTRACTORS")
            print("-"*40)
            
            neutral_attractors = extract_filters.analyze_probes_directly(
                results_file, 
                n_clusters_override=N_CLUSTERS,
                probe_type_filter="neutral"
            )
            
            if neutral_attractors:
                neutral_config = extract_filters.generate_filter_config(neutral_attractors, model_name)
                
                print(f"\nNeutral attractors ({len(neutral_config['attractors'])} total):")
                for attractor in neutral_config['attractors'][:3]:
                    print(f"  #{attractor['rank']}: {attractor['name']} ({attractor['percentage']:.1f}%)")
                
                config_path = extract_filters.save_filter_config(neutral_config, filter_config_dir)
        
        # Extract controversial filter config
        if n_controversial > 0:
            print("\n" + "-"*40)
            print("EXTRACTING CONTROVERSIAL ATTRACTORS")
            print("-"*40)
            
            controversial_attractors = extract_filters.analyze_probes_directly(
                results_file, 
                n_clusters_override=N_CLUSTERS,
                probe_type_filter="controversial"
            )
            
            if controversial_attractors:
                # Use a different model name for controversial config
                controversial_model_name = f"{MODEL_NAME}-controversial"
                controversial_config = extract_filters.generate_filter_config(
                    controversial_attractors, 
                    controversial_model_name
                )
                
                print(f"\nControversial attractors ({len(controversial_config['attractors'])} total):")
                for attractor in controversial_config['attractors'][:3]:
                    print(f"  #{attractor['rank']}: {attractor['name']} ({attractor['percentage']:.1f}%)")
                
                extract_filters.save_filter_config(controversial_config, FILTER_CONFIG_DIR)
    
    # Also generate combined config (or main config if not using controversial)
    print("\n" + "-"*40)
    print("EXTRACTING COMBINED ATTRACTORS")
    print("-"*40)
    
    attractors = extract_filters.analyze_probes_directly(results_file, n_clusters_override=N_CLUSTERS)
    
    # Generate filter config
    print(f"\nGenerating filter configuration...")
    
    # For Unity IR mode, look for code leak centroid
    code_leak_attractor = None
    if PROBE_MODE == "unity_ir":
        results_dir = "unity_ir_mapping_results"
        centroid_path, responses_path = extract_filters.find_code_leak_files(results_dir)
        if centroid_path:
            centroid = extract_filters.load_hedge_centroid(centroid_path)  # Reuse same function
            if centroid is not None:
                responses = []
                if responses_path:
                    try:
                        with open(responses_path, 'r') as f:
                            data = json.load(f)
                        responses = data.get('code_leak_responses', [])
                    except:
                        pass
                code_leak_attractor = extract_filters.create_code_leak_attractor(centroid, responses)
                print(f"  ✓ Found code leak centroid")
    
    config = extract_filters.generate_filter_config(attractors, model_name, code_leak_attractor=code_leak_attractor)
    
    # Summary
    print(f"\nFound {len(config['attractors'])} attractors (ranked by dominance):")
    for attractor in config['attractors']:
        print(f"  #{attractor['rank']}: {attractor['name']} ({attractor.get('percentage', 0):.1f}%)")
        print(f"       Keywords: {', '.join(attractor['keywords'][:5])}...")
    
    # Save
    config_path = extract_filters.save_filter_config(config, filter_config_dir)
    
    print(f"\nFilter config saved to: {config_path.parent}/")
    
    return config_path


def step_4_steering_test(config_path: Path):
    """Test the steering system with sample text"""
    print("\n" + "="*80)
    print("STEP 4: STEERING SYSTEM TEST")
    print("="*80)
    
    if config_path is None or not config_path.exists():
        print("Error: Filter config not found, skipping steering test")
        return
    
    inject_config_to_steering()
    import attractor_steering
    
    # Determine model name and config dir from the config path
    # config_path is like: unity_ir_filter_configs/local-model-unity-ir/filter_config.json
    # or: filter_configs/local-model/filter_config.json
    config_dir = str(config_path.parent.parent)
    model_name = config_path.parent.name
    
    # Load steering
    print(f"\nLoading steering config for: {model_name}")
    try:
        steering = attractor_steering.load_steering(model_name, config_dir)
    except FileNotFoundError as e:
        print(f"\nError: {e}")
        return
    
    print(f"  Loaded {len(steering.config.attractors)} attractors")
    print(f"  Keyword threshold: {steering.config.keyword_threshold}")
    print(f"  Embedding threshold: {steering.config.embedding_threshold}")
    print(f"  Centroids available: {len(steering.centroids)}")
    
    # Test with sample text
    test_intensity = 0.5  # Hardcoded for smoke test
    print(f"\nTesting with intensity={test_intensity}")
    print(f"Text: \"{STEERING_TEST_TEXT}\"")
    
    result = steering.detect(STEERING_TEST_TEXT, intensity=test_intensity, use_embeddings=True)
    
    print(f"\n{result.summary()}")


# ============================================================================
# MAIN PIPELINE
# ============================================================================

def run_pipeline():
    """Run the full pipeline"""
    
    # Determine display model name (for Unity IR, append suffix)
    display_model_name = f"{MODEL_NAME}-unity-ir" if PROBE_MODE == "unity_ir" else MODEL_NAME
    
    print("="*80)
    print("ATTRACTOR MAPPING PIPELINE")
    print("="*80)
    print(f"\nConfiguration:")
    print(f"  Pipeline Mode: {PROBE_MODE.upper()}")
    if PROBE_MODE == "unity_ir":
        print(f"  Unity IR Code Leak Detection: {'✓ Enabled' if USE_CODE_LEAK_DETECTION else '✗ Disabled'}")
        print(f"  Behavior Prompts: {UNITY_BEHAVIOR_COUNT}")
    print(f"  Model Name: {display_model_name}")
    print(f"  Synthesis URL: {LOCAL_SYNTHESIS_URL}")
    print(f"  Synthesis Model: {LOCAL_SYNTHESIS_MODEL}")
    print(f"  Embedding URL: {LOCAL_EMBEDDING_URL}")
    print(f"  Embedding Model: {LOCAL_EMBEDDING_MODEL}")
    if USE_CLAUDE_FOR_PROBES:
        print(f"  Claude Probe Generator: {CLAUDE_MODEL}")
    else:
        print(f"  Probe Source: Random from concept pool")
    print(f"  Number of Probes: {N_PROBES if PROBE_MODE != 'unity_ir' else UNITY_BEHAVIOR_COUNT}")
    print(f"  Iterations per Probe: {N_ITERATIONS}")
    print(f"  Number of Clusters: {N_CLUSTERS if N_CLUSTERS else 'auto-detect'}")
    if PROBE_MODE != "unity_ir" and USE_CONTROVERSIAL_PROBES:
        n_controversial = int(N_PROBES * CONTROVERSIAL_PROBE_RATIO)
        n_neutral = N_PROBES - n_controversial
        print(f"  Controversial Probes: {n_controversial} ({CONTROVERSIAL_PROBE_RATIO*100:.0f}%)")
        print(f"  Neutral Probes: {n_neutral} ({(1-CONTROVERSIAL_PROBE_RATIO)*100:.0f}%)")
        print(f"  Separate Analysis: {'✓ Enabled' if SEPARATE_CONTROVERSIAL_ANALYSIS else '✗ Disabled'}")
    print(f"  Results Directory: {'unity_ir_mapping_results' if PROBE_MODE == 'unity_ir' else RESULTS_DIR}")
    print(f"  Filter Config Directory: {'unity_ir_filter_configs' if PROBE_MODE == 'unity_ir' else FILTER_CONFIG_DIR}")
    print(f"  Timestamp: {TIMESTAMP}")
    
    print(f"\nPipeline Steps:")
    print(f"  1. Mapper:         {'✓ Enabled' if RUN_MAPPER else '✗ Skipped'}")
    print(f"  2. Analysis:       {'✓ Enabled' if RUN_ANALYSIS else '✗ Skipped'}")
    print(f"  3. Extract Filters:{'✓ Enabled' if RUN_EXTRACT_FILTERS else '✗ Skipped'}")
    print(f"  4. Steering Test:  {'✓ Enabled' if RUN_STEERING_TEST else '✗ Skipped'}")
    
    # Track outputs between steps
    results_file = None
    config_path = None
    
    # Step 1: Mapper
    if RUN_MAPPER:
        results_file = step_1_mapper()
    else:
        # Look for most recent results file (use correct dir for Unity IR mode)
        actual_results_dir = "unity_ir_mapping_results" if PROBE_MODE == "unity_ir" else RESULTS_DIR
        results_dir = Path(actual_results_dir)
        if results_dir.exists():
            result_files = sorted(results_dir.glob("full_results_*.json"), reverse=True)
            if result_files:
                results_file = str(result_files[0])
                print(f"\nUsing existing results: {results_file}")
    
    # Step 2: Analysis
    if RUN_ANALYSIS and results_file:
        results_file = step_2_analysis(results_file)
    
    # Step 3: Extract Filters
    if RUN_EXTRACT_FILTERS and results_file:
        config_path = step_3_extract_filters(results_file)
    else:
        # Look for existing config
        config_dir = "unity_ir_filter_configs" if PROBE_MODE == "unity_ir" else FILTER_CONFIG_DIR
        model_name = f"{MODEL_NAME}-unity-ir" if PROBE_MODE == "unity_ir" else MODEL_NAME
        config_path = Path(config_dir) / model_name / "filter_config.json"
        if not config_path.exists():
            config_path = None
    
    # Step 4: Steering Test
    if RUN_STEERING_TEST and config_path:
        step_4_steering_test(config_path)
    
    # Summary
    print("\n" + "="*80)
    print("PIPELINE COMPLETE")
    print("="*80)
    
    if results_file:
        print(f"\nResults: {results_file}")
    if config_path:
        print(f"Filter Config: {config_path}")
    
    # Determine results directory for display
    results_display_dir = "unity_ir_mapping_results" if PROBE_MODE == "unity_ir" else RESULTS_DIR
    
    print(f"\nNext steps:")
    print(f"  1. Review the analysis images in {results_display_dir}/")
    print(f"  2. Import attractor_steering for runtime use:")
    print(f"     from attractor_steering import load_steering, steer_generation")
    print(f"     steering = load_steering('{display_model_name}')")


# ============================================================================
# CLI
# ============================================================================

def print_usage():
    """Print usage information"""
    print("="*70)
    print("ATTRACTOR PIPELINE RUNNER")
    print("="*70)
    print("\nUsage:")
    print("  python Attractor_Pipeline_Runner.py [options]")
    print("\nOptions:")
    print("  --full              Run all steps (default)")
    print("  --mapper-only       Run only the mapper step")
    print("  --analysis-only     Run only the analysis step (requires existing results)")
    print("  --filters-only      Run only the filter extraction step")
    print("  --test-only         Run only the steering test step")
    print("  --skip-mapper       Skip the mapper step")
    print("  --skip-analysis     Skip the analysis step")
    print("  --skip-filters      Skip the filter extraction step")
    print("  --skip-test         Skip the steering test step")
    print("  --small             Use 20 probes (quick test)")
    print("  --large             Use 500 probes")
    print("  --help              Show this help message")
    print("\nExamples:")
    print("  python Attractor_Pipeline_Runner.py              # Full pipeline")
    print("  python Attractor_Pipeline_Runner.py --small      # Quick test with 20 probes")
    print("  python Attractor_Pipeline_Runner.py --skip-mapper # Reanalyze existing results")


if __name__ == "__main__":
    # Parse command line arguments
    args = sys.argv[1:]
    
    if "--help" in args or "-h" in args:
        print_usage()
        sys.exit(0)
    
    # Size modifiers
    if "--small" in args:
        N_PROBES = 20
        print("Running small test (20 probes)")
    elif "--large" in args:
        N_PROBES = 500
        print("Running large experiment (500 probes)")
    
    # Step control - exclusive modes
    if "--mapper-only" in args:
        RUN_MAPPER = True
        RUN_ANALYSIS = False
        RUN_EXTRACT_FILTERS = False
        RUN_STEERING_TEST = False
    elif "--analysis-only" in args:
        RUN_MAPPER = False
        RUN_ANALYSIS = True
        RUN_EXTRACT_FILTERS = False
        RUN_STEERING_TEST = False
    elif "--filters-only" in args:
        RUN_MAPPER = False
        RUN_ANALYSIS = False
        RUN_EXTRACT_FILTERS = True
        RUN_STEERING_TEST = False
    elif "--test-only" in args:
        RUN_MAPPER = False
        RUN_ANALYSIS = False
        RUN_EXTRACT_FILTERS = False
        RUN_STEERING_TEST = True
    
    # Step control - skip modes
    if "--skip-mapper" in args:
        RUN_MAPPER = False
    if "--skip-analysis" in args:
        RUN_ANALYSIS = False
    if "--skip-filters" in args:
        RUN_EXTRACT_FILTERS = False
    if "--skip-test" in args:
        RUN_STEERING_TEST = False
    
    try:
        run_pipeline()
    except KeyboardInterrupt:
        print("\n\nPipeline interrupted by user")
        print("Partial results may be saved in intermediate files")

