#!/usr/bin/env python3
"""
Automatic Hallucination Attractor Builder
==========================================
Discovers Unity API hallucination patterns from:
1. Existing test results (fast, no API calls)
2. Probing the model with test prompts (optional, more thorough)

Then builds attractor centroids for steering during code generation.

Usage:
    # Analyze existing test results only (fast)
    python build_hallucination_attractors.py --analyze-results
    
    # Probe model for more samples (slower, more thorough)
    python build_hallucination_attractors.py --probe --n-samples 50
    
    # Full pipeline: analyze + probe + build config
    python build_hallucination_attractors.py --full
    
    # Just build config from discovered patterns
    python build_hallucination_attractors.py --build-config
"""

import json
import re
import os
import numpy as np
import requests
from pathlib import Path
from typing import Dict, List, Tuple, Optional, Set
from dataclasses import dataclass, field
from collections import defaultdict
import argparse

# Import our validator for pattern detection
from unity_api_validator import UnityAPIValidator, ValidationIssue

# ============================================================================
# CONFIGURATION
# ============================================================================

import os
RESULTS_DIR = os.path.join(os.path.dirname(__file__), "..", "test scripts", "prompt_test_results")
OUTPUT_CONFIG_DIR = os.path.join(os.path.dirname(__file__), "unity_ir_filter_configs")
CONFIG_NAME = "unity-hallucination-steering"

LLM_URL = "http://localhost:1234/v1/chat/completions"
EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
EMBEDDING_MODEL = "nomic-embed-text"

# Test prompts for probing (diverse Unity scenarios)
PROBE_PROMPTS = [
    "disco ball that rotates and projects colored light beams",
    "magnet that attracts metal objects with increasing force",
    "particle fountain that sprays water droplets upward",
    "flickering torch with fire particles and crackling sound",
    "laser beam that bounces off mirrors and damages enemies",
    "floating platform that bobs up and down smoothly",
    "explosion effect with shockwave and debris particles",
    "weather system with rain particles and thunder sounds",
    "magic wand that shoots sparkle particles when waved",
    "campfire with smoke, embers, and warmth zone",
    "neon sign that flickers and buzzes with electrical sound",
    "bubble machine that spawns floating soap bubbles",
    "firework launcher that shoots colorful explosions",
    "fog machine that emits rolling mist particles",
    "disco floor with color-changing tiles and beat detection",
    "lightning strike effect with flash and thunder delay",
    "waterfall with mist, splash particles, and rushing sound",
    "firefly swarm that glows and moves organically",
    "snow globe with falling snowflakes when shaken",
    "lava lamp with rising and falling colored blobs",
]

# Simple code generation prompt (no RAG to maximize hallucinations)
CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Generate a complete MonoBehaviour script.
Output ONLY the C# code. No markdown, no explanations."""


# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class DiscoveredHallucination:
    """A hallucination discovered from generated code"""
    code_line: str
    invalid_api: str
    issue_type: str
    suggested_fix: str
    source_file: Optional[str] = None
    line_number: int = 0
    confidence: float = 1.0
    
    def __hash__(self):
        return hash((self.invalid_api, self.issue_type))
    
    def __eq__(self, other):
        return self.invalid_api == other.invalid_api and self.issue_type == other.issue_type


@dataclass  
class HallucinationCategory:
    """A category of similar hallucinations"""
    name: str
    description: str
    examples: List[str] = field(default_factory=list)
    keywords: List[str] = field(default_factory=list)
    centroid: Optional[List[float]] = None
    count: int = 0
    
    def to_dict(self) -> Dict:
        return {
            "name": self.name,
            "description": self.description,
            "examples": self.examples[:10],
            "keywords": self.keywords[:25],
            "centroid": self.centroid,
            "count": self.count,
            "percentage": 0  # Will be calculated later
        }


# ============================================================================
# PHASE 1: ANALYZE EXISTING TEST RESULTS
# ============================================================================

def load_test_results(results_dir: str = RESULTS_DIR) -> List[Dict]:
    """Load all test result JSON files"""
    results = []
    results_path = Path(results_dir)
    
    if not results_path.exists():
        print(f"Results directory not found: {results_dir}")
        return results
    
    for json_file in results_path.glob("test_results_*.json"):
        if "partial" in json_file.name or "in_progress" in json_file.name:
            continue
        
        try:
            with open(json_file, 'r', encoding='utf-8') as f:
                data = json.load(f)
                if isinstance(data, list):
                    for item in data:
                        item['_source_file'] = json_file.name
                    results.extend(data)
        except Exception as e:
            print(f"Error loading {json_file}: {e}")
    
    return results


def extract_code_from_results(results: List[Dict]) -> List[Tuple[str, str]]:
    """Extract all generated code from test results
    
    Returns list of (code, source_description) tuples
    """
    code_samples = []
    
    for result in results:
        source = result.get('_source_file', 'unknown')
        prompt_num = result.get('prompt_num', 0)
        
        # Extract from different code fields
        for field in ['oneshot_code', 'ir_code', 'per_behavior_code']:
            code = result.get(field)
            if code and isinstance(code, str) and len(code) > 100:
                code_samples.append((code, f"{source}:{prompt_num}:{field}"))
    
    return code_samples


def analyze_code_for_hallucinations(
    code: str,
    source: str,
    validator: UnityAPIValidator
) -> List[DiscoveredHallucination]:
    """Analyze a single code sample for hallucinations"""
    
    issues = validator.validate_code(code)
    hallucinations = []
    
    lines = code.split('\n')
    
    for issue in issues:
        # Get the actual code line
        code_line = ""
        if 0 < issue.line_num <= len(lines):
            code_line = lines[issue.line_num - 1].strip()
        
        hallucinations.append(DiscoveredHallucination(
            code_line=code_line,
            invalid_api=issue.invalid_api,
            issue_type=issue.issue_type,
            suggested_fix=issue.suggested_fix,
            source_file=source,
            line_number=issue.line_num,
            confidence=issue.confidence
        ))
    
    return hallucinations


def analyze_all_results(verbose: bool = True) -> Dict[str, List[DiscoveredHallucination]]:
    """Analyze all test results and categorize hallucinations
    
    Returns dict mapping issue_type -> list of hallucinations
    """
    print("=" * 60)
    print("PHASE 1: Analyzing Existing Test Results")
    print("=" * 60)
    
    # Load results
    results = load_test_results()
    print(f"Loaded {len(results)} test results")
    
    # Extract code
    code_samples = extract_code_from_results(results)
    print(f"Found {len(code_samples)} code samples")
    
    # Initialize validator
    validator = UnityAPIValidator(verbose=False)
    
    # Analyze each code sample
    all_hallucinations: Dict[str, List[DiscoveredHallucination]] = defaultdict(list)
    total_found = 0
    
    for code, source in code_samples:
        hallucinations = analyze_code_for_hallucinations(code, source, validator)
        
        for h in hallucinations:
            all_hallucinations[h.issue_type].append(h)
            total_found += 1
    
    print(f"\nFound {total_found} hallucinations across {len(all_hallucinations)} categories")
    
    if verbose:
        print("\nHallucination Categories:")
        for issue_type, items in sorted(all_hallucinations.items(), key=lambda x: -len(x[1])):
            unique_apis = len(set(h.invalid_api for h in items))
            print(f"  {issue_type}: {len(items)} instances, {unique_apis} unique APIs")
            
            # Show top examples
            examples = list(set(h.invalid_api for h in items))[:3]
            for ex in examples:
                print(f"    - {ex}")
    
    return all_hallucinations


# ============================================================================
# PHASE 2: PROBE MODEL FOR MORE SAMPLES (OPTIONAL)
# ============================================================================

def generate_code_sample(prompt: str) -> Optional[str]:
    """Generate a code sample from the model (without RAG)"""
    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": "local-model",
                "messages": [
                    {"role": "system", "content": CODE_SYSTEM_PROMPT},
                    {"role": "user", "content": f"Create a Unity MonoBehaviour for: {prompt}"}
                ],
                "temperature": 0.7,  # Higher temp = more variety = more hallucinations
                "max_tokens": 3000
            },
            timeout=120
        )
        
        if response.status_code == 200:
            code = response.json()["choices"][0]["message"]["content"]
            # Strip markdown if present
            if "```" in code:
                match = re.search(r'```(?:csharp)?\s*([\s\S]*?)\s*```', code)
                if match:
                    code = match.group(1)
            return code
    except Exception as e:
        print(f"Generation error: {e}")
    
    return None


def probe_model_for_hallucinations(
    n_samples: int = 20,
    verbose: bool = True
) -> Dict[str, List[DiscoveredHallucination]]:
    """Probe the model with test prompts to discover hallucinations"""
    
    print("\n" + "=" * 60)
    print("PHASE 2: Probing Model for Hallucinations")
    print("=" * 60)
    print(f"Generating {n_samples} samples (no RAG, to maximize hallucinations)")
    
    validator = UnityAPIValidator(verbose=False)
    all_hallucinations: Dict[str, List[DiscoveredHallucination]] = defaultdict(list)
    
    prompts_to_use = (PROBE_PROMPTS * ((n_samples // len(PROBE_PROMPTS)) + 1))[:n_samples]
    
    for i, prompt in enumerate(prompts_to_use):
        if verbose:
            print(f"\n[{i+1}/{n_samples}] {prompt[:50]}...")
        
        code = generate_code_sample(prompt)
        
        if code:
            hallucinations = analyze_code_for_hallucinations(
                code, f"probe:{i}", validator
            )
            
            for h in hallucinations:
                all_hallucinations[h.issue_type].append(h)
            
            if verbose and hallucinations:
                print(f"  Found {len(hallucinations)} hallucinations")
    
    total = sum(len(v) for v in all_hallucinations.values())
    print(f"\nProbing complete: {total} hallucinations found")
    
    return all_hallucinations


# ============================================================================
# PHASE 3: BUILD CATEGORIES AND EMBEDDINGS
# ============================================================================

def get_embedding(text: str) -> Optional[np.ndarray]:
    """Get embedding for text"""
    try:
        response = requests.post(
            EMBEDDING_URL,
            json={"model": EMBEDDING_MODEL, "input": text},
            timeout=30
        )
        
        if response.status_code == 200:
            vec = np.array(response.json()["data"][0]["embedding"], dtype=np.float32)
            return vec / np.linalg.norm(vec)
    except Exception as e:
        print(f"Embedding error: {e}")
    
    return None


def build_categories(
    hallucinations: Dict[str, List[DiscoveredHallucination]],
    compute_embeddings: bool = True,
    verbose: bool = True
) -> List[HallucinationCategory]:
    """Build hallucination categories with centroids"""
    
    print("\n" + "=" * 60)
    print("PHASE 3: Building Categories and Centroids")
    print("=" * 60)
    
    categories = []
    
    # Category descriptions based on issue types
    category_descriptions = {
        "hallucinated_api": "Invented Unity APIs that don't exist",
        "invalid_constructor": "Using 'new' on Unity components",
        "wrong_accessor": "Accessing module as method instead of property",
        "wrong_case": "Wrong capitalization (Unity uses PascalCase)",
        "syntax_error": "General syntax errors like double dots",
        "compatibility": "Unity compatibility issues",
    }
    
    for issue_type, items in hallucinations.items():
        if not items:
            continue
        
        # Extract unique examples
        unique_apis = list(set(h.invalid_api for h in items))
        unique_lines = list(set(h.code_line for h in items if h.code_line))
        
        # Extract keywords from the invalid APIs
        keywords = []
        for api in unique_apis:
            # Split on dots, parens, spaces
            parts = re.split(r'[.\(\)\s]+', api)
            keywords.extend([p for p in parts if len(p) > 2])
        keywords = list(set(keywords))[:25]
        
        category = HallucinationCategory(
            name=issue_type,
            description=category_descriptions.get(issue_type, f"Category: {issue_type}"),
            examples=unique_lines[:10] if unique_lines else unique_apis[:10],
            keywords=keywords,
            count=len(items)
        )
        
        # Compute centroid from examples
        if compute_embeddings and category.examples:
            if verbose:
                print(f"Computing centroid for {issue_type} ({len(category.examples)} examples)...")
            
            embeddings = []
            for example in category.examples[:20]:  # Limit for speed
                emb = get_embedding(example)
                if emb is not None:
                    embeddings.append(emb)
            
            if embeddings:
                centroid = np.mean(embeddings, axis=0)
                centroid = centroid / np.linalg.norm(centroid)
                category.centroid = centroid.tolist()
                
                if verbose:
                    print(f"  ✓ Centroid from {len(embeddings)} embeddings")
        
        categories.append(category)
    
    # Sort by count (most common first)
    categories.sort(key=lambda c: -c.count)
    
    # Calculate percentages
    total = sum(c.count for c in categories)
    for cat in categories:
        cat_dict = cat.to_dict()
        cat_dict["percentage"] = (cat.count / total * 100) if total > 0 else 0
    
    return categories


# ============================================================================
# PHASE 4: SAVE FILTER CONFIG
# ============================================================================

def save_filter_config(
    categories: List[HallucinationCategory],
    output_dir: str = OUTPUT_CONFIG_DIR,
    config_name: str = CONFIG_NAME
) -> Path:
    """Save the filter configuration for steering"""
    
    print("\n" + "=" * 60)
    print("PHASE 4: Saving Filter Configuration")
    print("=" * 60)
    
    # Build config structure (compatible with attractor_steering.py)
    total_count = sum(c.count for c in categories)
    
    config = {
        "model_name": config_name,
        "total_attractors": len(categories),
        "settings": {
            "keyword_threshold": 2.0,  # Lower threshold for hallucinations
            "embedding_threshold": 0.72,
            "default_intensity": 0.7,  # Higher intensity for hallucinations
            "max_regeneration_attempts": 3,
        },
        "attractors": [],
        "all_keywords": [],
    }
    
    all_keywords = []
    
    for rank, cat in enumerate(categories):
        percentage = (cat.count / total_count * 100) if total_count > 0 else 0
        
        attractor = {
            "rank": rank,
            "name": cat.name,
            "type": "hallucination",
            "description": cat.description,
            "percentage": percentage,
            "count": cat.count,
            "keywords": cat.keywords,
            "sample_outputs": cat.examples[:5],
        }
        
        if cat.centroid:
            attractor["centroid"] = cat.centroid
        
        config["attractors"].append(attractor)
        all_keywords.extend(cat.keywords)
    
    config["all_keywords"] = list(set(all_keywords))
    
    # Save to file
    output_path = Path(output_dir) / config_name
    output_path.mkdir(parents=True, exist_ok=True)
    
    config_file = output_path / "filter_config.json"
    with open(config_file, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2)
    
    print(f"✓ Saved filter config to: {config_file}")
    
    # Also save a summary
    summary_file = output_path / "summary.txt"
    with open(summary_file, 'w', encoding='utf-8') as f:
        f.write("Unity Hallucination Attractors Summary\n")
        f.write("=" * 40 + "\n\n")
        f.write(f"Total categories: {len(categories)}\n")
        f.write(f"Total hallucinations analyzed: {total_count}\n\n")
        
        for cat in categories:
            pct = (cat.count / total_count * 100) if total_count > 0 else 0
            f.write(f"\n{cat.name} ({pct:.1f}%, {cat.count} instances)\n")
            f.write(f"  {cat.description}\n")
            f.write(f"  Keywords: {', '.join(cat.keywords[:10])}\n")
            f.write(f"  Examples:\n")
            for ex in cat.examples[:3]:
                f.write(f"    - {ex[:80]}\n")
    
    print(f"✓ Saved summary to: {summary_file}")
    
    return config_file


# ============================================================================
# MAIN PIPELINE
# ============================================================================

def run_full_pipeline(
    analyze_results: bool = True,
    probe_model: bool = False,
    n_probe_samples: int = 20,
    compute_embeddings: bool = True,
    verbose: bool = True
):
    """Run the full hallucination attractor building pipeline"""
    
    print("\n" + "=" * 60)
    print("UNITY HALLUCINATION ATTRACTOR BUILDER")
    print("=" * 60)
    
    all_hallucinations: Dict[str, List[DiscoveredHallucination]] = defaultdict(list)
    
    # Phase 1: Analyze existing results
    if analyze_results:
        results_hallucinations = analyze_all_results(verbose=verbose)
        for issue_type, items in results_hallucinations.items():
            all_hallucinations[issue_type].extend(items)
    
    # Phase 2: Probe model (optional)
    if probe_model:
        probe_hallucinations = probe_model_for_hallucinations(
            n_samples=n_probe_samples, verbose=verbose
        )
        for issue_type, items in probe_hallucinations.items():
            all_hallucinations[issue_type].extend(items)
    
    if not all_hallucinations:
        print("\nNo hallucinations found! Nothing to build.")
        return None
    
    # Phase 3: Build categories
    categories = build_categories(
        all_hallucinations,
        compute_embeddings=compute_embeddings,
        verbose=verbose
    )
    
    # Phase 4: Save config
    config_path = save_filter_config(categories)
    
    # Final summary
    print("\n" + "=" * 60)
    print("PIPELINE COMPLETE")
    print("=" * 60)
    print(f"Built {len(categories)} hallucination attractors:")
    
    total = sum(c.count for c in categories)
    for cat in categories:
        pct = (cat.count / total * 100) if total > 0 else 0
        centroid_status = "✓ centroid" if cat.centroid else "✗ no centroid"
        print(f"  {cat.name}: {cat.count} instances ({pct:.1f}%) [{centroid_status}]")
    
    print(f"\nConfig saved to: {config_path}")
    print("\nTo use for steering, load with:")
    print(f'  from attractor_steering import load_steering')
    print(f'  steering = load_steering("{CONFIG_NAME}", "{OUTPUT_CONFIG_DIR}")')
    
    return config_path


# ============================================================================
# CLI
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Build hallucination attractors from test results and model probing"
    )
    parser.add_argument("--analyze-results", "-a", action="store_true",
                        help="Analyze existing test results")
    parser.add_argument("--probe", "-p", action="store_true",
                        help="Probe model for hallucinations")
    parser.add_argument("--n-samples", "-n", type=int, default=20,
                        help="Number of samples for probing")
    parser.add_argument("--no-embeddings", action="store_true",
                        help="Skip embedding computation")
    parser.add_argument("--full", "-f", action="store_true",
                        help="Run full pipeline (analyze + probe + build)")
    parser.add_argument("--quiet", "-q", action="store_true",
                        help="Minimal output")
    
    args = parser.parse_args()
    
    # Default: analyze results if no flags given
    if not any([args.analyze_results, args.probe, args.full]):
        args.analyze_results = True
    
    if args.full:
        args.analyze_results = True
        args.probe = True
    
    run_full_pipeline(
        analyze_results=args.analyze_results,
        probe_model=args.probe,
        n_probe_samples=args.n_samples,
        compute_embeddings=not args.no_embeddings,
        verbose=not args.quiet
    )


if __name__ == "__main__":
    main()

