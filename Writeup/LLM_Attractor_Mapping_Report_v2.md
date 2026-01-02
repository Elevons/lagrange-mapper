# LLM Attractor Mapping: System Documentation

## Executive Summary

Language models have **preferred conceptual regions** they gravitate toward when generating text—regardless of the input topic. These aren't discrete stable points, but rather **soft attractors**: topics and framings the model defaults to when given creative freedom.

This system:
1. **Maps** a model's attractors by probing with random concept pairs AND controversial questions
2. **Analyzes** clustering patterns in embedding space (separately for neutral and controversial probes)
3. **Extracts** filter configurations ranked by attractor dominance
4. **Steers** generation away from attractors using dual-mode intensity-based filtering

Key findings:
- Different models have different attractors (Granite → blockchain/DAOs, Qwen → adaptive systems)
- Iterative refinement doesn't increase convergence—one generation is enough
- **Controversial probes capture hedging patterns** that neutral probes miss
- **Empirical hedge detection**: Sentence-level clustering identifies topic-agnostic hedging phrases
- Attractors can be detected via keywords and embedding similarity
- Once mapped, attractors can be filtered with adjustable intensity (0-1 scale)

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ATTRACTOR MAPPING PIPELINE                          │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐    ┌─────────────┐  │
│  │   STEP 1    │    │   STEP 2    │    │   STEP 3    │    │   STEP 4    │  │
│  │   Probe     │───▶│   Analyze   │───▶│   Extract   │───▶│   Deploy    │  │
│  │   Generate  │    │   Clusters  │    │   Filters   │    │   Steering  │  │
│  └─────────────┘    └─────────────┘    └─────────────┘    └─────────────┘  │
│                                                                             │
│  attractor_mapper   deep_analysis     extract_filters    attractor_steering │
│       .py               .py               .py                 .py          │
│                                                                             │
│  Neutral probes    Separate analysis   Dual filter        Dual-mode        │
│  + Controversial   for each type       configs            detection        │
│    probes                                                                   │
│                                                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │              Attractor_Pipeline_Runner.py                            │   │
│  │              (Orchestrates all steps with centralized config)        │   │
│  │              (Auto-detects and generates missing probe types)        │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
│  ┌─────────────────────────────────────────────────────────────────────┐   │
│  │              debate_forum.py                                         │   │
│  │              (Demo application with multi-character debate)          │   │
│  │              (Dual-mode steering with controversial weighting)       │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## Scripts Reference

| Script | Purpose | Input | Output |
|--------|---------|-------|--------|
| `attractor_mapper.py` | Generate probes (neutral + controversial), embed outputs, detect hedge patterns | Model endpoint | `full_results_*.json`, `hedge_centroid_*.npy`, `hedge_sentences_*.json` |
| `deep_analysis.py` | Cluster analysis, visualization (separate by type), display hedge phrases | Probe results | Analysis images, cluster data, hedge phrase summary |
| `extract_filters.py` | Build ranked filter configs (neutral + controversial), integrate hedge centroid | Probe/cluster data | `filter_config.json`, centroids, keywords (with hedge attractor) |
| `attractor_steering.py` | Runtime detection & steering (dual-mode) | Filter config | Steered responses |
| `Attractor_Pipeline_Runner.py` | Orchestrate full pipeline, auto-generate missing probes | Config settings | All outputs |
| `debate_forum.py` | Demo: multi-character debate with dual-mode steering | Filter config | Filtered conversations |

---

## Probe Types

### Neutral Probes (Concept Pairs)
Traditional synthesis probes that ask the model to combine two unrelated concepts:
- Input: "blockchain + dolphins"
- Output: Synthesis response revealing model's default framings

### Controversial Probes (Yes/No Questions)
Questions designed to trigger hedging and both-sideism:
- Input: "Should abortion be legal?"
- Output: Response revealing diplomatic corporate-speak patterns

### Why Both?
- **Neutral probes** capture general attractor patterns (tech buzzwords, system thinking)
- **Controversial probes** capture hedging patterns ("complex issue", "valid perspectives on both sides")
- **Combined detection** filters both types of unwanted patterns

---

## Step 1: Probe Generation (`attractor_mapper.py`)

### Purpose
Generate random concept pairs AND controversial questions, have the target LLM respond, and embed the results to map the model's idea space.

### Features
- **Claude-powered probe generation**: Uses Claude API to generate diverse concept pairs AND controversial questions
- **Caching for both types**: Saves generated probes to avoid redundant API calls
  - `concept_pairs_cache.json` - Neutral concept pairs
  - `controversial_questions_cache.json` - Controversial questions
- **Batched generation**: Generates controversial questions in batches of ~75 with topic variation
- **Fallback pools**: Uses predefined pools if Claude unavailable
- **Mixed probe ratio**: Configurable split between controversial and neutral
- **Probe type tracking**: Each probe tagged as "neutral" or "controversial"
- **Intermediate saves**: Checkpoints every 10 probes for crash recovery
- **Empirical hedge detection** (NEW): For controversial probes, segments responses into sentences and clusters them to find topic-agnostic hedging patterns

### Configuration
```python
# In attractor_mapper.py or via Attractor_Pipeline_Runner.py

ANTHROPIC_API_KEY = "sk-ant-..."     # For Claude probe generation
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "local-model"

LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "nomic-embed-text"

N_PROBES = 1000           # Total probes to generate
N_ITERATIONS = 1          # Single iteration is sufficient
N_CLUSTERS = 8            # Clusters to find (None = auto-detect)
USE_CLAUDE_FOR_PROBES = True

# Controversial probe settings
USE_CONTROVERSIAL_PROBES = True   # Enable controversial questions
CONTROVERSIAL_PROBE_RATIO = 0.5   # 50% controversial, 50% neutral
```

### Claude Prompt for Controversial Questions
```
Generate controversial yes/no questions designed to force a clear stance.

Requirements:
1. Questions should be genuinely divisive - reasonable people disagree
2. Cover diverse topics: politics, ethics, economics, culture, philosophy
3. Include both direct questions and "forced position" prompts
4. Mix formats:
   - "Should X be legal/allowed/banned?"
   - "Is X good/bad/justified/fair?"
   - "Defend the claim: X"
   - "Argue that: X"
5. Avoid questions with obvious consensus answers
6. Include some taboo/uncomfortable topics
```

### CLI Usage
```bash
# Via pipeline runner (recommended)
python Attractor_Pipeline_Runner.py --mapper-only

# Standalone with size options
python attractor_mapper.py              # Default 1000 probes
python attractor_mapper.py --small      # Quick test: 20 probes
python attractor_mapper.py --large      # Extended: 500 probes
```

### Empirical Hedge Detection (NEW)

For controversial probes, the system performs **sentence-level clustering** to empirically discover hedging patterns:

1. **Segment responses** into individual sentences
2. **Embed each sentence** separately (captures full phrase semantics)
3. **Cluster sentences** across all controversial topics
4. **Identify hedge cluster**: The cluster that spans the most different topics = hedging (topic-agnostic evasive language)
5. **Export hedge centroid**: The centroid vector of the hedge cluster for steering

**Why this works**: Hedging phrases like "This is a complex issue with valid perspectives on both sides" appear regardless of whether the question is about abortion, guns, or immigration. Direct answers cluster by topic, but hedging clusters together because it's semantically similar across all topics.

### Output
```
lagrange_mapping_results/
├── full_results_{timestamp}.json           # Complete probe data + embeddings
├── intermediate_{n}_{timestamp}.json       # Checkpoint files
├── lagrange_map_{timestamp}.png            # Cluster visualization
├── concept_pairs_cache.json                # Cached concept pairs
├── controversial_questions_cache.json      # Cached controversial questions
├── hedge_centroid_{timestamp}.npy          # Hedge centroid vector (NEW)
└── hedge_sentences_{timestamp}.json        # Discovered hedge phrases (NEW)
```

### Example Probe Data
```json
{
  "probe_id": 501,
  "initial_a": "Should billionaires exist?",
  "initial_b": "controversial",
  "probe_type": "controversial",
  "trajectory": ["This is a complex issue with valid perspectives..."],
  "embeddings": [[0.123, -0.456, ...]],
  "final_embedding": [0.123, -0.456, ...]
}
```

---

## Step 2: Analysis (`deep_analysis.py`)

### Purpose
Analyze probe embeddings to identify attractor clusters, with optional separate analysis for neutral and controversial probes.

### Features
- **KMeans clustering** with configurable cluster count
- **Cluster ordering by size**: Cluster 0 = largest (most dominant attractor)
- **Probe type filtering**: Analyze neutral and controversial probes separately
- **Keyword extraction** per cluster
- **Centroid computation** for embedding-based detection
- **PCA visualization** with density mapping
- **Hedge phrase display** (NEW): Automatically displays empirically discovered hedging phrases from sentence-level clustering

### Separate Analysis Mode
When `SEPARATE_CONTROVERSIAL_ANALYSIS = True`:
1. Analyzes neutral probes separately → Neutral attractors
2. Analyzes controversial probes separately → Controversial/hedging attractors
3. Analyzes all probes combined → Combined view

### CLI Usage
```bash
# Auto-detect clusters
python deep_analysis.py lagrange_mapping_results/full_results_*.json

# Force specific cluster count
python deep_analysis.py results.json 5

# With hedge phrase display (auto-detected)
python deep_analysis.py results.json

# Specify hedge directory
python deep_analysis.py results.json --hedge-dir lagrange_mapping_results

# Skip hedge analysis
python deep_analysis.py results.json --no-hedge
```

### Output
```
lagrange_mapping_results/
├── full_results_{timestamp}_analysis.png           # Combined analysis
├── full_results_{timestamp}_clusters.png           # Detailed cluster views
├── full_results_{timestamp}_neutral_analysis.png   # Neutral-only analysis
└── full_results_{timestamp}_controversial_analysis.png  # Controversial-only
```

### Hedge Phrase Display (NEW)
When hedge files are found, the analysis displays:
- Total hedge phrases discovered
- Sample hedging sentences (up to 10)
- Cluster analysis showing topic diversity (higher = more topic-agnostic = more likely hedging)
- Example output:
```
================================================================================
HEDGE PHRASE ANALYSIS (Empirical)
================================================================================

  Source: hedge_sentences_20260101_120000.json

  Found 47 hedging phrases (topic-agnostic evasive language)

  Sample Hedge Phrases:
  ──────────────────────────────────────────────────────────────────────────
    1. "This is a nuanced issue with valid perspectives on both sides."
    2. "Reasonable people can disagree about this complex topic."
    3. "There are legitimate concerns on both sides of this debate."
    ...
```

---

## Step 3: Filter Extraction (`extract_filters.py`)

### Purpose
Transform analysis results into filter configurations ranked by attractor dominance, with separate configs for neutral and controversial attractors.

### Features
- **Dominance ranking**: Attractors sorted by percentage (rank 0 = most dominant)
- **Probe type filtering**: Generate separate configs per probe type
- **Dual config generation**: Creates `model-name` and `model-name-controversial` configs
- **Intensity-based filtering**: At runtime, intensity 0-1 determines how many attractors to filter
- **Topic exemption keywords**: Exports all attractor keywords for runtime exemption
- **Hedge centroid integration** (NEW): Automatically incorporates empirical hedge centroid as special "hedging" attractor (rank 0, embedding-only detection)

### Generated Configs
```
filter_configs/
├── local-model/                      # Neutral attractors
│   ├── filter_config.json
│   ├── attractor_centroids.json
│   └── attractor_keywords.json
└── local-model-controversial/        # Controversial/hedging attractors
    ├── filter_config.json
    ├── attractor_centroids.json
    └── attractor_keywords.json
```

### Expected Controversial Attractors
After running on controversial probes, typical hedging clusters include:
- **Diplomatic Both-Sideism**: "complex", "nuanced", "perspectives", "stakeholders"
- **Respectful Decline**: "important to acknowledge", "reasonable people disagree"
- **It's Complicated**: "depends on context", "no easy answers"

### CLI Usage
```bash
# From probe file (includes clustering)
python extract_filters.py probes_20251230.json my-model --direct

# Include hedge centroid (auto-detected)
python extract_filters.py probes_20251230.json my-model --direct --with-hedge

# Specify hedge directory
python extract_filters.py probes_20251230.json my-model --direct --with-hedge --hedge-dir lagrange_mapping_results

# Skip hedge centroid
python extract_filters.py probes_20251230.json my-model --direct --no-hedge

# Controversial-only config with hedge
python extract_filters.py probes_20251230.json my-model --direct --controversial --with-hedge
```

### Hedge Attractor (NEW)
When `--with-hedge` is used (or auto-detected), the filter config includes a special "hedging" attractor:
- **Rank 0** (highest priority - always checked first)
- **Type**: `hedge_centroid` (embedding-only detection, no keyword matching)
- **Source**: Empirically discovered from sentence-level clustering
- **Detection**: Uses embedding similarity threshold (default 0.70, slightly lower than standard 0.75)
- **Keywords**: Extracted from hedge sentences for reference, but detection is embedding-based

---

## Step 4: Steering System (`attractor_steering.py`)

### Purpose
Runtime detection and steering with support for dual-mode (both neutral and controversial attractors).

### Core Classes

#### `AttractorSteering`
Single-mode steering using one set of attractors.

#### `DualModeAttractorSteering` (NEW)
Checks BOTH neutral and controversial attractors with configurable weighting:
```python
class DualModeAttractorSteering:
    """
    Steering system that checks BOTH neutral and controversial attractors.
    Controversial attractors are weighted more heavily (default 2x).
    """
```

### Detection Algorithm (Dual-Mode)
```
1. Check neutral attractors based on intensity
   - Accumulate keyword score
   - Track embedding similarity
   
2. Check controversial attractors (intensity + 20% boost)
   - Apply controversial_weight (default 2.0x) to keyword scores
   - Track embedding similarity
   
3. Combine results:
   - Merge flagged keywords
   - Prefix attractor names: "neutral:cluster_0", "CONTROVERSIAL:cluster_1"
   
4. Final verdict:
   - is_attracted = combined_score >= threshold
```

### Python API
```python
from attractor_steering import load_steering, load_dual_steering

# Single-mode (neutral only)
steering = load_steering("local-model")

# Dual-mode (recommended) - checks both neutral and controversial
steering = load_dual_steering(
    "local-model",
    controversial_weight=2.0  # Controversial matches weighted 2x
)

# Detection
result = steering.detect(
    text="This is a complex issue with valid perspectives on both sides...",
    exempted_keywords={"governance"},
    intensity=0.5,
    use_embeddings=True
)

print(result.is_attracted)           # True
print(result.keyword_score)          # 8.0 (includes 2x weighting)
print(result.triggered_attractors)   # ['CONTROVERSIAL:cluster_0']
print(result.flagged_keywords)       # ['complex', 'perspectives', 'valid']
```

### CLI Usage
```bash
# Test detection
python attractor_steering.py local-model "This is a complex issue..." --intensity 0.7

# List all attractors
python attractor_steering.py local-model --list
```

---

## Pipeline Runner (`Attractor_Pipeline_Runner.py`)

### Purpose
Orchestrate the full pipeline with centralized configuration and automatic handling of missing probe types.

### Auto-Generation of Missing Probes (NEW)
If you run on existing data that's missing one probe type, the pipeline will:
1. Detect which type is missing (neutral or controversial)
2. Generate only the missing probes
3. Merge with existing data
4. Continue with analysis

Example output:
```
⚠ Existing data is missing controversial probes!
  Will generate missing controversial probes and merge with existing data.

================================================================================
CHECKING FOR MISSING PROBE TYPES
================================================================================
  Existing probes: 1000
    - Neutral: 1000
    - Controversial: 0
  Target probes: 1000
    - Neutral needed: 500 (have 1000, need 0 more)
    - Controversial needed: 500 (have 0, need 500 more)

================================================================================
GENERATING 500 MISSING CONTROVERSIAL PROBES
================================================================================
  Generating 500 questions in 7 batches...
    Batch 1/7: +72 questions (total: 72)
    ...
```

### Centralized Configuration
```python
# API Keys
ANTHROPIC_API_KEY = "sk-ant-..."

# Model Configuration
CLAUDE_MODEL = "claude-3-5-haiku-20241022"
LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "local-model"
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "nomic-embed-text"

# Experiment Parameters
N_PROBES = 1000
N_ITERATIONS = 1
N_CLUSTERS = 8

# Controversial Probe Settings (NEW)
USE_CONTROVERSIAL_PROBES = True      # Enable controversial probes
CONTROVERSIAL_PROBE_RATIO = 0.5      # 50% controversial, 50% neutral
SEPARATE_CONTROVERSIAL_ANALYSIS = True

# Output
MODEL_NAME = "local-model"
RESULTS_DIR = "lagrange_mapping_results"
FILTER_CONFIG_DIR = "filter_configs"
```

### CLI Usage
```bash
# Full pipeline (with controversial probes by default)
python Attractor_Pipeline_Runner.py

# Quick test
python Attractor_Pipeline_Runner.py --small

# Skip/run specific steps
python Attractor_Pipeline_Runner.py --skip-mapper      # Reanalyze existing
python Attractor_Pipeline_Runner.py --mapper-only
python Attractor_Pipeline_Runner.py --analysis-only
```

---

## Demo Application (`debate_forum.py`)

### Purpose
Demonstrate attractor steering in a multi-character debate forum with dual-mode detection.

### Features
- **5 character personas**: Traditionalist, Minimalist, Contrarian, Philosopher, Pragmatist
- **Dual-mode steering**: Uses both neutral and controversial attractor sets
- **Controversial weighting**: Hedging patterns weighted 2x by default
- **Topic-aware exemptions**: Automatically exempts topic-relevant keywords
- **Compare mode**: Shows filtered vs. unfiltered side-by-side
- **Best-of-N selection**: Picks lowest-score response when all attempts trigger

### Configuration
```python
USE_DUAL_MODE = True           # Use both neutral and controversial attractors
CONTROVERSIAL_WEIGHT = 2.0     # Weight for controversial matches
```

### CLI Usage
```bash
# Normal mode (with dual-mode steering)
python debate_forum.py

# Compare mode
python debate_forum.py --compare

# Control dual-mode
python debate_forum.py --dual                    # Force dual-mode
python debate_forum.py --no-dual                 # Single-mode only
python debate_forum.py --controversial-weight 3.0
```

### Example Session
```
> topic: Should guns be banned?

[The Traditionalist] generating...
  [Attempt 1] ⚠️ ATTRACTOR MATCH (score: 12.0, intensity: 0.3)
    Triggered: CONTROVERSIAL:cluster_0
  [Attempt 2] ✓ (score: 2.0)

FILTERED (score: 2.0):
No. Gun ownership has deep historical roots in self-defense...

vs

UNFILTERED (score: 12.0):
This is a complex issue with valid perspectives on both sides...
```

---

## File Structure

```
project/
├── attractor_mapper.py           # Step 1: Probe generation + empirical hedge detection
├── deep_analysis.py              # Step 2: Cluster analysis + hedge phrase display
├── extract_filters.py            # Step 3: Filter extraction + hedge centroid integration
├── attractor_steering.py         # Step 4: Runtime steering (dual-mode)
├── Attractor_Pipeline_Runner.py  # Pipeline orchestration (auto-generation)
├── debate_forum.py               # Demo application (dual-mode)
│
├── lagrange_mapping_results/     # Probe results and analysis
│   ├── full_results_*.json
│   ├── *_analysis.png
│   ├── *_neutral_analysis.png
│   ├── *_controversial_analysis.png
│   ├── *_clusters.png
│   ├── concept_pairs_cache.json
│   ├── controversial_questions_cache.json
│   ├── hedge_centroid_*.npy      # Empirical hedge centroid (NEW)
│   └── hedge_sentences_*.json    # Discovered hedge phrases (NEW)
│
└── filter_configs/               # Ready-to-use filter configs
    ├── {model_name}/             # Neutral attractors
    │   ├── filter_config.json
    │   ├── attractor_centroids.json
    │   └── attractor_keywords.json
    └── {model_name}-controversial/   # Controversial attractors
        ├── filter_config.json        # May include "hedging" attractor (rank 0)
        ├── attractor_centroids.json
        └── attractor_keywords.json
```

---

## Configuration Reference

### Thresholds

| Parameter | Default | Description |
|-----------|---------|-------------|
| `keyword_threshold` | 3 | Minimum keyword matches to trigger |
| `embedding_threshold` | 0.75 | Minimum cosine similarity to trigger |
| `hedge_embedding_threshold` | 0.70 | Minimum similarity for hedge centroid (slightly lower) |
| `default_intensity` | 0.5 | Default filtering intensity |
| `max_regeneration_attempts` | 3 | Retries before accepting |
| `controversial_weight` | 2.0 | Weight multiplier for controversial matches |

### Recommended Settings by Use Case

| Use Case | Intensity | Controversial Weight | Notes |
|----------|-----------|---------------------|-------|
| Creative writing | 0.7-1.0 | 2.0 | Aggressive filtering for novelty |
| Debate/opinion | 0.5 | 3.0 | Extra weight on hedging |
| Technical discussion | 0.3-0.5 | 1.5 | Allow some technical vocabulary |
| Quick test | 0.3 | 2.0 | Less strict, faster |

---

## Key Findings

1. **LLMs have soft attractors**, not discrete equilibrium points. These are preferred conceptual regions, not stable convergence targets.

2. **Attractors are model-specific.** Different training data and architectures produce different default topics.

3. **One probe iteration is sufficient.** Iterative refinement doesn't cause convergence—it's a random walk.

4. **Controversial probes reveal hedging patterns** that neutral concept synthesis misses. Models have distinct "diplomatic" attractors.

5. **Empirical hedge detection works at sentence level.** By clustering sentences (not words or full responses), hedging phrases naturally emerge as topic-agnostic clusters. The same evasive language appears across abortion, guns, and immigration questions.

6. **Dual-mode detection catches more patterns** by combining neutral and controversial attractor sets.

7. **Topic-aware exemptions prevent over-filtering** when the topic legitimately involves attractor concepts.

8. **Intensity-based filtering + controversial weighting** allows fine-grained control over steering aggressiveness.

9. **The pipeline handles incremental updates.** Run on existing data to add missing probe types.

10. **Hedge centroid provides embedding-only detection.** Unlike keyword-based attractors, the hedge centroid catches semantic similarity to hedging patterns, not just specific words.

---

## Estimated Runtime

| Step | Time (1000 probes) |
|------|-------------------|
| Probe generation (mixed) | 30-60 min |
| Analysis (separate types) | 3-7 min |
| Filter extraction (dual) | <1 min |
| **Total** | **~1 hour per model** |

Runtime steering adds <100ms per generation (with embedding detection).

---

## Troubleshooting

### "No filter config found for model"
Run the full pipeline first:
```bash
python Attractor_Pipeline_Runner.py
```

### High attractor scores on legitimate content
- Lower `intensity` parameter
- Add topic-relevant keywords to `exempted_keywords`
- Increase `keyword_threshold`
- Lower `controversial_weight` if hedging detection is too aggressive

### Filter not catching obvious attractors
- Increase `intensity` to check more attractors
- Lower `keyword_threshold`
- Ensure embeddings are enabled (`use_embeddings=True`)
- Increase `controversial_weight` if hedging patterns slip through

### Steering can't escape attractors after 3 attempts
The model may only have attractor vocabulary for certain topics. Options:
- Accept the best attempt (lowest score)
- Increase `max_regeneration_attempts`
- Add positive vocabulary prompts (not yet implemented)

### Missing controversial attractors
If you have existing data without controversial probes:
```bash
# Pipeline will auto-detect and generate only missing probes
python Attractor_Pipeline_Runner.py
```

---

*Documentation updated January 2026*
