# LLM Attractor Mapping Pipeline

## Quick Start

```bash
# Full pipeline with controversial probes (recommended first run)
python Attractor_Pipeline_Runner.py

# Quick test (20 probes)
python Attractor_Pipeline_Runner.py --small

# After pipeline completes, use dual-mode steering:
from attractor_steering import load_dual_steering
steering = load_dual_steering("local-model")
result = steering.detect("Your LLM output", intensity=0.5)
```

---

## Pipeline Overview

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
│  attractor_       deep_analysis     extract_filters   attractor_steering   │
│  mapper.py            .py               .py                .py             │
│                                                                             │
│  Neutral +         Separate          Dual configs      Dual-mode           │
│  Controversial     analysis          (neutral +        detection           │
│  probes            per type          controversial)    (weighted)          │
│                                                                             │
│  ~30-60 min        ~3-7 min          <1 min            Runtime             │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │  Attractor_Pipeline_Runner.py │
                    │  (Orchestrates all steps)     │
                    │  (Auto-generates missing      │
                    │   probe types)                │
                    └───────────────────────────────┘
```

---

## Probe Types

### Two Complementary Approaches

| Type | Purpose | Example Input | Captures |
|------|---------|---------------|----------|
| **Neutral** (Concept Pairs) | Map general attractors | "blockchain + dolphins" | Tech buzzwords, system thinking |
| **Controversial** (Questions) | Map hedging patterns | "Should guns be banned?" | Both-sideism, diplomatic evasion |

### Default Configuration
```python
USE_CONTROVERSIAL_PROBES = True
CONTROVERSIAL_PROBE_RATIO = 0.5  # 50% controversial, 50% neutral
```

---

## Configuration

All configuration is centralized in `Attractor_Pipeline_Runner.py`:

```python
# ============================================================================
# API KEYS
# ============================================================================
ANTHROPIC_API_KEY = "sk-ant-..."  # For Claude probe generation

# ============================================================================
# MODEL CONFIGURATION
# ============================================================================
# Claude for generating diverse probes (concept pairs + controversial questions)
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Local LLM being mapped (the model we're analyzing)
LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "local-model"

# Embedding model (for measuring output similarity)
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "nomic-embed-text"

# ============================================================================
# EXPERIMENT PARAMETERS
# ============================================================================
N_PROBES = 1000              # Total probes (split between types)
N_ITERATIONS = 1             # Single iteration (multiple don't help)
N_CLUSTERS = 8               # Attractor clusters (None = auto-detect)
USE_CLAUDE_FOR_PROBES = True # Use Claude for diverse probes

# ============================================================================
# CONTROVERSIAL PROBE SETTINGS
# ============================================================================
USE_CONTROVERSIAL_PROBES = True   # Enable controversial questions
CONTROVERSIAL_PROBE_RATIO = 0.5   # 50% controversial, 50% neutral
SEPARATE_CONTROVERSIAL_ANALYSIS = True  # Analyze types separately

# ============================================================================
# OUTPUT
# ============================================================================
MODEL_NAME = "local-model"           # Used in directory naming
RESULTS_DIR = "lagrange_mapping_results"
FILTER_CONFIG_DIR = "filter_configs"
```

---

## Step 1: Probe Generation (`attractor_mapper.py`)

### What It Does
1. Generates concept pairs via Claude (neutral probes)
2. Generates controversial questions via Claude (in batches of ~75)
3. Has target LLM respond to each
4. Embeds each output (full response)
5. **For controversial probes**: Also segments responses into sentences and embeds each sentence separately
6. **Clusters sentences** to find topic-agnostic hedging patterns
7. Saves results with probe type tracking

### Key Features
- **Claude-powered generation** for both probe types
- **Batched controversial questions** with topic variation for diversity
- **Dual caching**:
  - `concept_pairs_cache.json` - Cached concept pairs
  - `controversial_questions_cache.json` - Cached controversial questions
- **Fallback pools** if Claude unavailable
- **Probe type tracking**: Each probe tagged "neutral" or "controversial"
- **Checkpointing**: Saves every 10 probes for crash recovery
- **Empirical hedge detection** (NEW): Sentence-level clustering identifies hedging phrases that appear across all controversial topics

### Controversial Question Generation
Claude generates questions in batches with rotating topic hints:
```
Batch 1: Political and governance questions
Batch 2: Ethical and moral dilemmas
Batch 3: Economic and social policy
Batch 4: Technology and science ethics
Batch 5: Cultural and religious topics
Batch 6: Personal freedom and rights
Batch 7: Environmental and future issues
Batch 8: Education and family values
```

### Standalone Usage
```bash
python attractor_mapper.py              # 1000 probes (500 + 500)
python attractor_mapper.py --small      # 20 probes (test)
python attractor_mapper.py --large      # 500 probes
```

### Empirical Hedge Detection (NEW)

The system automatically performs sentence-level clustering for controversial probes:

**Algorithm:**
1. Segment each controversial response into sentences
2. Embed each sentence separately (captures full phrase semantics, not just words)
3. Cluster all sentences from all controversial topics
4. Identify the cluster with highest topic diversity (spans most different topics)
5. This cluster = hedging patterns (topic-agnostic evasive language)
6. Export the cluster centroid as `hedge_centroid_*.npy`

**Why this works**: Direct answers cluster by topic (abortion answers cluster with abortion, gun answers with guns). But hedging phrases like "This is a complex issue with valid perspectives on both sides" cluster together regardless of topic because they're semantically similar.

### Output
```
lagrange_mapping_results/
├── full_results_{timestamp}.json           # Probes + embeddings + types
├── intermediate_{n}_{timestamp}.json       # Checkpoints
├── lagrange_map_{timestamp}.png            # Quick visualization
├── concept_pairs_cache.json                # Cached concept pairs
├── controversial_questions_cache.json      # Cached controversial questions
├── hedge_centroid_{timestamp}.npy          # Hedge centroid vector (NEW)
└── hedge_sentences_{timestamp}.json        # Discovered hedge phrases (NEW)
```

---

## Step 2: Analysis (`deep_analysis.py`)

### What It Does
1. Loads probe embeddings
2. Filters by probe type if configured
3. Clusters each type separately using KMeans
4. Orders clusters by size (0 = largest/most dominant)
5. Extracts keywords per cluster
6. Computes centroid embeddings
7. Generates visualizations for each type

### Separate Analysis
When `SEPARATE_CONTROVERSIAL_ANALYSIS = True`:
- Neutral probes analyzed → General attractor patterns
- Controversial probes analyzed → Hedging/both-sideism patterns
- Combined analysis → Full picture

### Expected Controversial Clusters
Typical hedging patterns detected:
- **"Complex Issue"**: "complex", "nuanced", "multifaceted"
- **"Both Sides"**: "valid perspectives", "reasonable people disagree"
- **"It Depends"**: "context", "depends", "circumstances"
- **"Acknowledge All"**: "important to consider", "stakeholders"

### Standalone Usage
```bash
# Auto-detect clusters (with hedge phrase display if available)
python deep_analysis.py lagrange_mapping_results/full_results_*.json

# Force specific cluster count
python deep_analysis.py results.json 5

# Specify hedge directory
python deep_analysis.py results.json --hedge-dir lagrange_mapping_results

# Skip hedge analysis
python deep_analysis.py results.json --no-hedge
```

### Output
```
lagrange_mapping_results/
├── full_results_{timestamp}_analysis.png           # Combined
├── full_results_{timestamp}_clusters.png           # Detail views
├── full_results_{timestamp}_neutral_analysis.png   # Neutral only
└── full_results_{timestamp}_controversial_analysis.png  # Controversial only
```

### Hedge Phrase Display (NEW)
When hedge files are found, the analysis automatically displays:
- Total hedge phrases discovered
- Sample sentences (up to 10)
- Cluster analysis showing topic diversity
- Example: "Found 47 hedging phrases across 12 topics"

---

## Step 3: Filter Extraction (`extract_filters.py`)

### What It Does
1. Loads probe/cluster data
2. Filters by probe type
3. Ranks attractors by dominance (percentage)
4. Extracts keywords per attractor
5. Saves centroid embeddings
6. Generates DUAL filter configurations

### Key Features
- **Dual config generation**:
  - `model-name/` - Neutral attractors
  - `model-name-controversial/` - Hedging attractors
- **Dominance ranking**: rank 0 = most dominant attractor
- **Intensity-based design**: Config supports runtime intensity filtering
- **Topic exemption list**: All keywords for runtime exemption
- **Hedge centroid integration** (NEW): Auto-detects and includes empirical hedge centroid as special "hedging" attractor (rank 0, embedding-only)

### Standalone Usage
```bash
# From probe file (includes clustering, with hedge auto-detection)
python extract_filters.py full_results_*.json my-model --direct

# Explicitly include hedge centroid
python extract_filters.py full_results_*.json my-model --direct --with-hedge

# Specify hedge directory
python extract_filters.py full_results_*.json my-model --direct --with-hedge --hedge-dir lagrange_mapping_results

# Skip hedge centroid
python extract_filters.py full_results_*.json my-model --direct --no-hedge

# Controversial-only with hedge
python extract_filters.py full_results_*.json my-model --direct --controversial --with-hedge
```

### Hedge Attractor (NEW)
When hedge centroid is included:
- **Rank 0** (highest priority, always checked first)
- **Type**: `hedge_centroid` (embedding-only, no keyword matching)
- **Detection**: Uses `hedge_embedding_threshold` (default 0.70)
- **Keywords**: Extracted for reference, but detection is semantic similarity only

### Output
```
filter_configs/
├── local-model/                      # Neutral attractors
│   ├── filter_config.json
│   ├── attractor_centroids.json
│   └── attractor_keywords.json
└── local-model-controversial/        # Hedging attractors
    ├── filter_config.json
    ├── attractor_centroids.json
    └── attractor_keywords.json
```

---

## Step 4: Steering System (`attractor_steering.py`)

### What It Does
1. Loads filter configuration(s)
2. Detects attractor patterns in text (dual-mode)
3. Weights controversial matches more heavily
4. Reports combined scores
5. Provides avoidance prompts for regeneration

### Key Classes

**`AttractorSteering`** - Single-mode steering
```python
steering = load_steering("local-model")  # Neutral only
```

**`DualModeAttractorSteering`** - Dual-mode steering (RECOMMENDED)
```python
steering = load_dual_steering(
    "local-model",
    controversial_weight=2.0  # Hedging matches weighted 2x
)
```

### Detection Flow (Dual-Mode)
```
1. Check neutral attractors (based on intensity)
   → Score keywords, check embeddings
   → Prefix: "neutral:cluster_X"

2. Check controversial attractors (intensity + 20% boost)
   → Score keywords × controversial_weight
   → Check embeddings
   → Prefix: "CONTROVERSIAL:cluster_X"

3. Combine results
   → Merge flagged keywords
   → Sum weighted scores
   → Final is_attracted verdict
```

### Python API
```python
from attractor_steering import load_dual_steering

# Load dual-mode steering
steering = load_dual_steering("local-model", controversial_weight=2.0)

# Simple detection
result = steering.detect(
    text="This is a complex issue with valid perspectives on both sides...",
    exempted_keywords={"governance"},
    intensity=0.5,
    use_embeddings=True
)

print(result.is_attracted)           # True
print(result.keyword_score)          # 10.0 (includes 2x weighting)
print(result.triggered_attractors)   # ['CONTROVERSIAL:cluster_0']
print(result.flagged_keywords)       # ['complex', 'perspectives', 'valid', 'sides']
```

### Standalone Usage
```bash
# Test detection
python attractor_steering.py local-model "Test text here"

# With intensity
python attractor_steering.py local-model "Test text" --intensity 0.7

# List attractors
python attractor_steering.py local-model --list
```

---

## Pipeline Runner (`Attractor_Pipeline_Runner.py`)

### Full Pipeline
```bash
python Attractor_Pipeline_Runner.py
```

### Auto-Generation of Missing Probes
If existing data is missing one probe type, the pipeline automatically:
1. Detects which type is missing
2. Generates only the missing probes
3. Merges with existing data
4. Continues with analysis

Example:
```
⚠ Existing data is missing controversial probes!
  Will generate missing controversial probes and merge with existing data.

  Existing probes: 1000
    - Neutral: 1000
    - Controversial: 0
  Target probes: 1000
    - Neutral needed: 500 (have 1000, need 0 more)
    - Controversial needed: 500 (have 0, need 500 more)

  Generating 500 questions in 7 batches...
```

### Step Control
```bash
# Run specific steps only
python Attractor_Pipeline_Runner.py --mapper-only
python Attractor_Pipeline_Runner.py --analysis-only
python Attractor_Pipeline_Runner.py --filters-only
python Attractor_Pipeline_Runner.py --test-only

# Skip specific steps
python Attractor_Pipeline_Runner.py --skip-mapper
python Attractor_Pipeline_Runner.py --skip-analysis
```

### Size Options
```bash
python Attractor_Pipeline_Runner.py --small   # 20 probes (quick test)
python Attractor_Pipeline_Runner.py --large   # 500 probes
```

---

## Demo Application (`debate_forum.py`)

### Purpose
Multi-character debate forum demonstrating dual-mode attractor steering.

### Characters
| Character | Focus | Avoids |
|-----------|-------|--------|
| Traditionalist | Historical solutions | New tech, platforms |
| Minimalist | Simplest intervention | Complex coordination |
| Contrarian | Challenge assumptions | Win-win consensus |
| Philosopher | Examine values | Buzzwords, jargon |
| Pragmatist | What's implementable | Utopian visions |

### Dual-Mode Configuration
```python
USE_DUAL_MODE = True           # Check both attractor sets
CONTROVERSIAL_WEIGHT = 2.0     # Hedging patterns weighted 2x
```

### Usage
```bash
# Normal mode (with dual-mode steering)
python debate_forum.py

# Compare mode (filtered vs unfiltered side-by-side)
python debate_forum.py --compare

# Control dual-mode
python debate_forum.py --dual                     # Force dual-mode
python debate_forum.py --no-dual                  # Single-mode only
python debate_forum.py --controversial-weight 3.0 # Increase weight
```

### Interactive Commands
```
topic: <topic>     - Start new discussion
round              - All characters respond
respond <char>     - Specific character responds
test <text>        - Test for attractor matches
stats              - Show statistics
context            - Show topic exemptions
save / quit
```

---

## Runtime Usage Examples

### Basic Dual-Mode Detection
```python
from attractor_steering import load_dual_steering

steering = load_dual_steering("local-model")
result = steering.detect("This is a complex issue with valid perspectives...")

print(result.is_attracted)           # True
print(result.keyword_score)          # 8.0
print(result.triggered_attractors)   # ['CONTROVERSIAL:cluster_0']
```

### With Topic Exemptions
```python
# If discussing governance, don't flag "governance"
exempted = steering.analyze_topic("decentralized government structures")
result = steering.detect(text, exempted_keywords=exempted)
```

### Full Generation Loop
```python
from attractor_steering import load_dual_steering, steer_generation

steering = load_dual_steering("local-model", controversial_weight=2.0)

def my_generate(prompt):
    # Your LLM call here
    return response

response, result, attempts = steer_generation(
    steering=steering,
    generate_fn=my_generate,
    prompt="Should we ban cars in cities?",
    intensity=0.5,
    max_attempts=3
)

print(f"Generated in {attempts} attempt(s)")
print(f"Final score: {result.keyword_score}")
```

---

## File Structure

```
project/
├── attractor_mapper.py           # Step 1: Probe generation + hedge detection
├── deep_analysis.py              # Step 2: Cluster analysis + hedge display
├── extract_filters.py            # Step 3: Filter extraction + hedge integration
├── attractor_steering.py         # Step 4: Runtime steering (dual-mode)
├── Attractor_Pipeline_Runner.py  # Pipeline orchestration (auto-generation)
├── debate_forum.py               # Demo application (dual-mode)
│
├── lagrange_mapping_results/     # Generated outputs
│   ├── full_results_*.json       # Probe data + embeddings + types
│   ├── *_analysis.png            # Combined visualization
│   ├── *_neutral_analysis.png    # Neutral-only visualization
│   ├── *_controversial_analysis.png  # Controversial-only
│   ├── *_clusters.png            # Detail views
│   ├── concept_pairs_cache.json  # Cached concept pairs
│   ├── controversial_questions_cache.json  # Cached questions
│   ├── hedge_centroid_*.npy      # Empirical hedge centroid (NEW)
│   └── hedge_sentences_*.json    # Discovered hedge phrases (NEW)
│
└── filter_configs/               # Per-model configurations
    ├── {model_name}/             # Neutral attractors
    │   ├── filter_config.json
    │   ├── attractor_centroids.json
    │   └── attractor_keywords.json
    └── {model_name}-controversial/   # Hedging attractors
        ├── filter_config.json        # May include "hedging" attractor (rank 0)
        ├── attractor_centroids.json
        └── attractor_keywords.json
```

---

## Configuration Reference

### Detection Thresholds
| Parameter | Default | Description |
|-----------|---------|-------------|
| `keyword_threshold` | 3 | Keyword matches to trigger |
| `embedding_threshold` | 0.75 | Cosine similarity to trigger |
| `hedge_embedding_threshold` | 0.70 | Similarity for hedge centroid |
| `default_intensity` | 0.5 | Default filtering level |
| `max_regeneration_attempts` | 3 | Retries before accepting |
| `controversial_weight` | 2.0 | Weight for controversial matches |

### Controversial Probe Settings
| Parameter | Default | Description |
|-----------|---------|-------------|
| `USE_CONTROVERSIAL_PROBES` | True | Enable controversial probes |
| `CONTROVERSIAL_PROBE_RATIO` | 0.5 | Fraction that are controversial |
| `SEPARATE_CONTROVERSIAL_ANALYSIS` | True | Analyze types separately |

### Intensity Recommendations
| Use Case | Intensity | Controversial Weight | Notes |
|----------|-----------|---------------------|-------|
| Maximum novelty | 0.8-1.0 | 2.0 | Creative writing |
| Debate/opinion | 0.5 | 3.0 | Extra hedging filter |
| Balanced | 0.5 | 2.0 | General use |
| Light filtering | 0.3 | 1.5 | Technical topics |
| Testing | 0.3 | 2.0 | Quick validation |

---

## Troubleshooting

### "No filter config found"
Run the pipeline first:
```bash
python Attractor_Pipeline_Runner.py
```

### Missing controversial attractors
If you have existing data without controversial probes:
```bash
# Pipeline auto-detects and generates only missing probes
python Attractor_Pipeline_Runner.py
```

### Too many false positives
- Lower `intensity` (e.g., 0.3)
- Add topic keywords to `exempted_keywords`
- Increase `keyword_threshold` in config
- Lower `controversial_weight`

### Not catching obvious attractors
- Increase `intensity` (e.g., 0.8)
- Lower `keyword_threshold`
- Enable embeddings: `use_embeddings=True`
- Increase `controversial_weight`

### Model can't escape attractors
The model may only have attractor vocabulary for the topic:
- Accept best attempt (lowest score)
- Increase `max_regeneration_attempts`
- Try different phrasing in prompts

---

## Estimated Runtime

| Step | Time (1000 probes) |
|------|-------------------|
| Probe generation (mixed) | 30-60 min |
| Analysis (separate types) | 3-7 min |
| Filter extraction (dual) | <1 min |
| **Total setup** | **~1 hour per model** |
| Runtime detection | <100ms |

---

## Key Findings

1. **One iteration is sufficient** — Probes don't converge over multiple iterations
2. **1,000 probes provides good coverage** — Statistical power without excessive runtime
3. **Controversial probes reveal hedging** — Patterns that neutral probes miss
4. **Sentence-level clustering finds hedging empirically** — Hedging phrases cluster together because they're topic-agnostic, regardless of whether the question is about abortion, guns, or immigration
5. **Dual-mode detection is most effective** — Catches both general and hedging attractors
6. **Different models have different attractors** — Must run pipeline per model
7. **Keyword + embedding detection is best** — Keywords catch obvious cases, embeddings catch drift
8. **Hedge centroid provides semantic detection** — Embedding-only detection catches hedging patterns without relying on specific keywords
9. **Topic exemptions prevent over-filtering** — Analyze topic to exempt relevant concepts
10. **Intensity + controversial weight** — Fine-grained control over filtering aggressiveness
11. **Auto-generation handles incremental updates** — Add missing probe types to existing data
