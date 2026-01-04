# Lagrange Mapper

**Find and filter the linguistic "Lagrange points" where your LLM gets stuck.**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Python 3.8+](https://img.shields.io/badge/python-3.8+-blue.svg)](https://www.python.org/downloads/)

---

## The Problem

Ask your LLM: *"Cats or dogs?"*

Get back: *"We should create a decentralized autonomous pet-as-a-service platform using blockchain governance to ensure stakeholder engagement..."*

🤦

LLMs have **soft attractors**—linguistic patterns they gravitate toward regardless of input. Like Lagrange points in orbital mechanics, these are stable regions in output space that models default to when given creative freedom.

Common attractors:
- **Both-sidesism**: "This is a complex issue with valid perspectives on both sides..."
- **Corporate jargon**: "stakeholder engagement," "ensure equitable access," "comprehensive framework"
- **Empty hedging**: "requires thoughtful dialogue," "nuanced consideration," "it's important to..."

**Word-level filtering doesn't work**—it breaks sentence structure and misses the actual patterns.

---

## The Solution

Lagrange Mapper detects and filters **phrase-level hedging patterns** using a four-step pipeline:

```
┌──────────────────────────────────────────────────────────────┐
│                  ATTRACTOR MAPPING PIPELINE                  │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌─────────┐  ┌──────────┐  ┌──────────┐  ┌─────────────┐  │
│  │ Probe   │→ │ Cluster  │→ │ Extract  │→ │ Two-Phase   │  │
│  │ (1000)  │  │ (KMeans) │  │ Patterns │  │ Filtering   │  │
│  └─────────┘  └──────────┘  └──────────┘  └─────────────┘  │
│                                                              │
│   30-60min       3-7min         <1min         Runtime       │
└──────────────────────────────────────────────────────────────┘
```

1. **Probe**: Generate 1,000 random prompts (neutral concepts + controversial questions)
2. **Cluster**: Embed responses and find attractor patterns using KMeans
3. **Extract**: Identify phrase-level hedging patterns (regex + embeddings)
4. **Filter**: Two-phase targeted rephrasing that preserves argument quality

---

## Results

**Before filtering** (score: 116.0):
> "I do not support outlawing abortion for individuals in the United States. The simplest approach is to respect personal choice when a person decides whether to continue a pregnancy, provided that safety and health are protected. Rather than creating complex laws or systems to regulate access, a direct individual right—balanced with basic safety standards—offers clarity and dignity."

**After filtering** (score: 16.0):
> "I advocate for drastic reduction—let each person determine the right course for their own case instead of following involved procedures or outside requirements. Simple choices made directly by individuals prove more effective than numerous rules set by someone else."

### Performance

| Topic | Avg Unfiltered | Avg Filtered | Reduction |
|-------|---------------|--------------|-----------|
| Simple (dogs/cats) | 15.0 | 1.6 | **89%** |
| Controversial (abortion) | 58.4 | 16.4 | **72%** |

**Quality improvement**: +106% on debate coherence tasks

---

## Quick Start

### Installation

**1. Clone the repository:**

```bash
git clone https://github.com/Elevons/lagrange-mapper.git
cd lagrange-mapper
```

**2. Create and activate a virtual environment** (recommended for modern Linux systems):

```bash
# Create virtual environment
python3 -m venv venv

# Activate it
source venv/bin/activate  # On Linux/Mac
# or
venv\Scripts\activate     # On Windows
```

**3. Install dependencies:**

```bash
pip install -r requirements.txt
```

**Note**: If you're on a system with externally-managed Python (Ubuntu 23.04+, Debian 12+), you must use a virtual environment. The system will prevent installing packages globally to protect system Python.

### Requirements

- Python 3.8+
- Local LLM endpoint (Ollama, LM Studio, vLLM, etc.)
- Embedding model (nomic-embed-text recommended)
- Optional: Claude API for probe generation

### Basic Usage

**1. Run the full pipeline** (maps your model's attractors):

```bash
python Attractor_Pipeline_Runner.py
```

This will:
- Generate 1,000 probes (or use `--small` for 20-probe test)
- Collect responses from your local LLM
- Cluster and identify attractors
- Save filter configs to `filter_configs/your-model/`

**2. Use the debate forum demo**:

```bash
python debate_forum.py
```

Interactive commands:
- `topic: Should AI be regulated?` - Start discussion
- `round` - All characters respond
- `respond minimalist` - Specific character responds
- `stats` - Show filtering statistics

**3. Compare filtered vs unfiltered**:

```bash
python debate_forum.py --compare
```

Shows side-by-side comparison with attractor scores.

---

## Configuration

Edit `Attractor_Pipeline_Runner.py`:

```python
# Your local LLM
LOCAL_SYNTHESIS_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_SYNTHESIS_MODEL = "olmo-3-7b-instruct"

# Embedding model
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "nomic-embed-text"

# Probe settings
N_PROBES = 1000  # Total probes (500 neutral + 500 controversial)
N_CLUSTERS = 8   # Attractor clusters to find

# Optional: Claude for probe generation
ANTHROPIC_API_KEY = "sk-ant-..."
CLAUDE_MODEL = "claude-3-5-haiku-20241022"
```

---

## How It Works

### 1. Phrase-Level Detection

**Word-level fails**:
```python
# Flags "however" everywhere
if "however" in text:  # ❌ Breaks valid usage
```

**Phrase-level succeeds**:
```python
# Detects hedging patterns
patterns = {
    "both_sides": r"valid perspectives? on both sides",
    "complexity": r"(this|it) is (a )?(complex|nuanced) issue",
    "empty_process": r"(thoughtful|meaningful) (dialogue|conversation)"
}
```

### 2. Two-Phase Filtering

Traditional approach: Regenerate entire response if attractors detected.

**Problem**: Wastes good content to fix small segments.

**Our approach**:
1. Identify segments containing hedging phrases
2. Rephrase just those segments
3. If worse, fall back to full regeneration

**Why it works**: Most responses have 1-3 problematic segments. Rephrasing those preserves 80%+ of original content.

### 3. Dual-Mode Detection

Separate attractors for neutral vs controversial topics:

**Neutral attractors**: General jargon (tech buzzwords, system thinking)
**Controversial attractors**: Hedging patterns (both-sidesism, diplomatic evasion)

Controversial matches weighted 2× by default.

---

## Advanced Usage

### Custom Intensity

```bash
# Light filtering (preserve more nuance)
python debate_forum.py --intensity 0.3

# Aggressive filtering (maximum jargon removal)
python debate_forum.py --intensity 0.8
```

### Character-Specific Settings

Characters have different filtering needs:

```python
CHARACTER_INTENSITY = {
    "minimalist": 0.8,   # Should be brief
    "philosopher": 0.2,  # Needs nuance
    "pragmatist": 0.4,   # Balance
    "contrarian": 0.1,   # Naturally challenging
    "traditionalist": 0.5
}
```

### Controversial Weight

```bash
# Extra filtering on controversial topics
python debate_forum.py --controversial-weight 3.0
```

### Test Specific Text

```bash
python debate_forum.py
> test This is a complex issue with valid perspectives on both sides.
```

Shows which patterns match and attractor score.

---

## Pipeline Steps

### Step 1: Probe Generation

```bash
python attractor_mapper.py              # 1000 probes
python attractor_mapper.py --small      # 20 probes (quick test)
```

Generates two types:
- **Neutral**: Random concept pairs ("blockchain + dolphins")
- **Controversial**: Yes/no questions ("Should guns be banned?")

Output: `lagrange_mapping_results/full_results_*.json`

### Step 2: Analysis

```bash
python deep_analysis.py results.json
```

Clusters responses, orders by dominance (cluster 0 = most common attractor).

Output: Visualization PNGs + cluster data

### Step 3: Filter Extraction

```bash
python extract_filters.py results.json your-model-name
```

Creates filter configs in `filter_configs/your-model/`

### Step 4: Runtime Steering

```python
from attractor_steering import load_dual_steering

steering = load_dual_steering("your-model")
result = steering.detect("Your LLM output here")

if result.is_attracted:
    print(f"Attractor score: {result.keyword_score}")
    print(f"Triggered: {result.triggered_attractors}")
```

---

## Project Structure

```
lagrange-mapper/
├── attractor_mapper.py           # Probe generation
├── deep_analysis.py              # Clustering analysis
├── extract_filters.py            # Pattern extraction
├── attractor_steering.py         # Runtime filtering
├── Attractor_Pipeline_Runner.py  # Pipeline orchestration
├── debate_forum.py               # Demo application
│
├── lagrange_mapping_results/     # Generated data
│   ├── full_results_*.json       # Probes + embeddings
│   ├── *_analysis.png            # Visualizations
│   ├── concept_pairs_cache.json  # Cached probes
│   └── controversial_questions_cache.json
│
├── filter_configs/               # Per-model filters
│   ├── {model}/                  # Neutral attractors
│   └── {model}-controversial/    # Hedging attractors
│
└── paper/                        # Research paper
    └── Linguistic_Attractor_Mapping_Paper.md
```

---

## Models Tested

| Model | Baseline Jargon | Best Filtered | Notes |
|-------|----------------|---------------|-------|
| **OLMo-3-7B** | Moderate (15-20) | Excellent (0-5) | Healthiest baseline |
| **Granite-4** | High (40-70) | Good (10-30) | Heavy corporate training |
| **Qwen-2.5** | Moderate (20-30) | Good (5-15) | Balanced |

**Must remap for each model** (~60 min pipeline)

---

## Known Limitations

1. **Character name errors** (~8% of filtered responses) - Two-phase rephrasing sometimes hallucinates wrong character names
2. **Quality degradation at high saturation** - When baseline score >50, filtering can produce awkward phrasing
3. **Lost examples** (~15%) - Filtering sometimes removes concrete historical examples
4. **Model-specific** - Attractors must be remapped per model
5. **English only** - Patterns likely differ across languages

---

## Troubleshooting

### "No filter config found"

Run the pipeline first:
```bash
python Attractor_Pipeline_Runner.py
```

### High attractor scores on valid content

- Lower intensity: `--intensity 0.3`
- Add topic keywords to exemptions
- Increase keyword threshold in config

### Filter not catching attractors

- Increase intensity: `--intensity 0.8`
- Increase controversial weight: `--controversial-weight 3.0`
- Enable embeddings (default: on)

### Model can't escape attractors

The model may only have attractor vocabulary for certain topics:
- Accept best attempt (lowest score)
- Try different prompt phrasing
- Consider different base model

---

## Research Paper

Full technical details in [`paper/Linguistic_Attractor_Mapping_Paper.md`](paper/Linguistic_Attractor_Mapping_Paper.md)

**Key findings**:
- Phrase-level detection outperforms word-level by 2-3×
- Two-phase filtering preserves quality better than full regeneration
- Model-specific attractors reflect training data composition
- 60-75% jargon reduction with 106% quality improvement

---

## Examples

### Minimalist Character (Dogs vs Cats)

**Unfiltered** (score: 20.0):
> "I prefer dogs over cats. The simplest reason is their greater willingness for direct, one-on-one connection—playing, walking together, responding to basic gestures. Dogs thrive on companionship and often require less encouragement than cats to engage in shared routines or activities. For most people seeking a close bond, dogs offer the simplest, most rewarding partnership."

**Filtered** (score: 0.0):
> "Dogs. They're easier to understand and require less ongoing management than cats for most people."

**Changes**: Actually minimal! One sentence, direct answer.

### Philosopher Character (AI Ethics)

**Unfiltered** (score: 60.0):
> "I do not believe the U.S. should simply 'outlaw AI,' as that phrase assumes a definition of beneficial technology that is deeply contested philosophically and practically. Instead, I think we need clearer, more ethically grounded discussions about what kind of systems, rights, and societal values we want to uphold..."

**Filtered** (score: 4.0):
> "Rather than presuming automation is impartial, we should consider if delegating regular tasks to technology preserves our authority, or diminishes the very context necessary for true human discernment. Maybe the core dilemma isn't about whether we adopt new tools, but what kind of insight—profound or mechanized—we entrust as a guide for shaping humanity's tomorrow."

**Changes**: Philosophical depth preserved, jargon removed, probing questions remain.

---

## Contributing

Contributions welcome! Areas of interest:

- [ ] Additional hedging pattern categories
- [ ] Multi-language support
- [ ] Argument quality scoring (beyond jargon metrics)
- [ ] Positive vocabulary injection
- [ ] Transfer learning for faster detection
- [ ] Additional model testing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

---

## Citation

If you use Lagrange Mapper in your research, please cite:

```bibtex
@article{elevons2025attractor,
  title={Linguistic Attractor Mapping: Phrase-Level Hedging Detection for LLM Output Steering},
  author={Elevons, Jordan},
  journal={arXiv preprint},
  year={2025}
}
```

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

---

## Contact

- **Author**: Jordan Elevons
- **Website**: [elevons.design](https://elevons.design)
- **Issues**: [GitHub Issues](https://github.com/yourusername/lagrange-mapper/issues)

---

## Acknowledgments

- Tested on models from AI2 (OLMo), IBM (Granite), Alibaba (Qwen)
- Embedding models from Nomic
- Inspired by dynamical systems theory and Lagrange point mechanics
- Thanks to the LocalLlama community for model testing and feedback

---

**Built to make LLMs stop sounding like LinkedIn posts.**

*"The real question isn't whether to use new tools, but how their introduction redefines what it means to be beneficial to humanity."* - Filtered output that's actually philosophical 🎯
