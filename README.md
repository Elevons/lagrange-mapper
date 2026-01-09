# Unity Code Generation Pipeline

A complete pipeline for generating Unity C# scripts from natural language descriptions using LLMs, RAG (Retrieval-Augmented Generation), and fine-tuning.

## Table of Contents

- [Quick Start](#quick-start)
- [Setup](#setup)
- [Code Generation Pipeline](#code-generation-pipeline)
- [Test Scripts](#test-scripts)
- [Training & Fine-tuning](#training--fine-tuning)
- [Configuration](#configuration)
- [Project Structure](#project-structure)

---

## Quick Start

**Generate a Unity script from a description:**
```bash
cd "code generation pipeline"
python unity_pipeline_simple.py "rotating coin that gives points when collected"
```

**Interactive mode:**
```bash
python unity_pipeline_simple.py --interactive
```

**Full pipeline with RAG:**
```bash
python unity_full_pipeline_rag.py "enemy AI that follows the player"
```

---

## Setup

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

### 2. Environment Variables

Create a `.env` file in the project root:

```env
ANTHROPIC_API_KEY=sk-ant-...  # Optional: for Claude-based generation
```

### 3. Local LLM Setup

You need a local LLM server running (LM Studio, Ollama, vLLM, etc.):

- **LLM URL**: `http://localhost:1234/v1/chat/completions`
- **Embedding URL**: `http://localhost:1234/v1/embeddings`
- **Recommended models**: OLMo-3-7B, Qwen2-7B, or any 7B+ code-capable model

### 4. Build RAG Database (First Time Only)

Before using RAG-based generation, build the Unity API documentation database:

```bash
cd "code generation pipeline"
python build_rag_database.py --input <path_to_unity_docs> --output unity_rag_db
```

**Prerequisites:**
- Unity API documentation in markdown format
- LM Studio running with `nomic-embed-text-v1.5` model loaded

---

## Code Generation Pipeline

All pipeline scripts are in the `code generation pipeline/` directory.

### Main Pipelines

#### 1. Simple Pipeline (`unity_pipeline_simple.py`)

Streamlined NL → IR → C# generation with RAG support.

```bash
# Single generation
python unity_pipeline_simple.py "player that jumps with spacebar"

# Interactive mode
python unity_pipeline_simple.py --interactive

# With custom RAG database
python unity_pipeline_simple.py "enemy AI" --rag-db custom_rag_db --verbose 
```

**Features:**
- Natural language → IR JSON → C# code
- RAG-based documentation retrieval
- No attractor detection (fastest option)

#### 2. Full Pipeline with RAG (`unity_full_pipeline_rag.py`)

Complete pipeline with code leak steering and RAG.

```bash
# Monolithic RAG mode (faster, ~10 docs)
python unity_full_pipeline_rag.py "rotating coin"

# Per-behavior RAG mode (more comprehensive, ~20-30 docs)
python unity_full_pipeline_rag.py --rag-mode per_behavior "enemy AI"

# Interactive mode
python unity_full_pipeline_rag.py --interactive
```

**Features:**
- Code leak detection and steering
- Two RAG modes: monolithic or per-behavior
- Full validation pipeline

#### 3. Per-Behavior Pipeline (`unity_pipeline_per_behavior.py`)

Generates code by querying RAG for each behavior separately.

```bash
# Generate code
python unity_pipeline_per_behavior.py "complex boss with multiple attack phases"

# Compare monolithic vs per-behavior
python unity_pipeline_per_behavior.py --compare
```

**Features:**
- More comprehensive RAG coverage
- Better for complex behaviors
- Comparison tool included

### IR Generation

#### Unity IR Inference (`unity_ir_inference.py`)

Generates Unity IR JSON from natural language descriptions.

```bash
# Single generation
python unity_ir_inference.py "rotating coin that gives points when collected"

# Interactive mode
python unity_ir_inference.py --interactive

# With custom steering intensity
python unity_ir_inference.py "enemy AI" --intensity 0.7

# Batch processing
python unity_ir_inference.py --batch prompts.txt --output results/

# Skip steering (raw generation)
python unity_ir_inference.py "player jump" --no-steering
```

**As a module:**
```python
from unity_ir_inference import UnityIRGenerator

generator = UnityIRGenerator()
result = generator.generate("player that jumps with space")
print(result.json_output)
```

### RAG System

#### Build RAG Database (`build_rag_database.py`)

Indexes Unity API documentation for retrieval.

```bash
python build_rag_database.py --input <docs_path> --output unity_rag_db
python build_rag_database.py --input unity_docs/ --output unity_rag_db --include-content
```

**Options:**
- `--input`: Path to Unity markdown documentation
- `--output`: Output database path (default: `unity_rag_db`)
- `--include-content`: Embed full documentation content (larger DB, better retrieval)

#### Query RAG (`unity_rag_query.py`)

Test RAG retrieval directly:

```python
from unity_rag_query import UnityRAG

rag = UnityRAG(verbose=True)
results = rag.query("ParticleSystem emission", top_k=5)
for doc in results:
    print(f"{doc.api_name}: {doc.snippet}")
```

### Code Validation & Fixing

#### API Validator (`unity_api_validator.py` / `unity_api_validator_v2.py`)

Validates Unity C# code against documentation.

```python
from unity_api_validator_v2 import UnityAPIValidatorV2

validator = UnityAPIValidatorV2(verbose=True)
issues = validator.validate_code(csharp_code)

for issue in issues:
    print(f"Line {issue.line_num}: {issue.invalid_api}")
    print(f"  Suggested: {issue.suggested_fix}")
```

#### Script Fixer (`unity_script_fixer.py`)

Automatically fixes common Unity API errors.

```bash
# Interactive mode
python unity_script_fixer.py --interactive

# Fix specific file
python unity_script_fixer.py --file broken_script.cs

# Fix with specific errors
python unity_script_fixer.py --file script.cs --errors "CS0117: 'Light' does not contain 'beamHeight'"
```

### Attractor Mapping & Steering

#### Attractor Pipeline Runner (`Attractor_Pipeline_Runner.py`)

Full attractor mapping pipeline for detecting and filtering code leaks.

```bash
# Full pipeline (maps attractors for your model)
python Attractor_Pipeline_Runner.py

# Unity IR mode (recommended for Unity code generation)
# Edit PROBE_MODE = "unity_ir" in the file first
```

**Configuration:**
Edit the file to set:
- `PROBE_MODE`: `"unity_ir"` for Unity-specific attractors
- `N_PROBES`: Number of probes to generate (default: 1000)
- `LOCAL_SYNTHESIS_URL`: Your LLM endpoint
- `ANTHROPIC_API_KEY`: For Claude probe generation

#### Attractor Steering (`attractor_steering.py`)

Runtime code leak detection and steering.

```python
from attractor_steering import load_steering

steering = load_steering("local-model-unity-ir", "unity_ir_filter_configs")
result = steering.detect_code_leak(ir_json_string, intensity=0.5)

if result.is_attracted:
    print(f"Code leak detected! Triggered: {result.triggered_attractors}")
    avoidance_prompt = steering.get_avoidance_prompt(result)
```

### Calibration

#### Calibration Pipeline (`Calibration_Pipeline_Runner.py`)

Calibrates IR generation using Claude as a reference model.

```bash
# Full pipeline
python Calibration_Pipeline_Runner.py

# Run specific step
python Calibration_Pipeline_Runner.py --step 1  # Generate ideals
python Calibration_Pipeline_Runner.py --step 2  # Generate actuals
python Calibration_Pipeline_Runner.py --step 3  # Compute offsets

# Quick test with fewer examples
python Calibration_Pipeline_Runner.py --count 20
```

**Steps:**
1. Generate ideal IR examples using Claude
2. Generate actual outputs from your local model
3. Compute offset vectors between ideal and actual
4. Integrate calibration into IR system

---

## Test Scripts

All test scripts are in the `test scripts/` directory.

### Quick Tests

#### Test Imports (`test_imports.py`)

Verify all imports work correctly:

```bash
cd "test scripts"
python test_imports.py
```

#### Check RAG (`check_rag.py`)

Quick check of RAG database coverage:

```bash
python check_rag.py
```

Outputs to `rag_check_output.txt` with coverage statistics.

### Pipeline Tests

#### Test Crazy Prompts (`test_crazy_prompts.py`)

Tests all prompts from `crazy_test_prompts.txt` and compares approaches.

```bash
python test_crazy_prompts.py
```

**Features:**
- Tests oneshot vs IR (monolithic) vs IR (per-behavior)
- Grades outputs using Claude Haiku
- Saves results to `prompt_test_results/`

#### Test Per-Behavior (`test_per_behavior.py`)

Quick comparison of monolithic vs per-behavior pipelines.

```bash
python test_per_behavior.py
```

#### Test IR vs Oneshot (`test_ir_vs_oneshot.py`)

Compares IR pipeline (no RAG) vs direct oneshot generation.

```bash
python test_ir_vs_oneshot.py
```

### Validation Tests

#### Test Validation Pipeline (`test_validation_pipeline.py`)

Tests the full validation pipeline (pattern + RAG-based).

```bash
python test_validation_pipeline.py
```

**Tests:**
- Pattern-based validation
- RAG-verified validation
- Script fixing
- Hallucination detection

### Steering Tests

#### Test Unity Steering (`test_unity_steering.py`)

Tests Unity IR steering against code leak patterns.

```bash
python test_unity_steering.py
```

**Requirements:**
- Filter config must exist at `code generation pipeline/unity_ir_filter_configs/local-model-unity-ir/filter_config.json`
- Run attractor pipeline first to generate configs

### Direct Model Tests

#### Test LLM Direct (`test_llm_direct.py`)

Direct LLM API testing without pipeline.

```bash
python test_llm_direct.py
```

#### Test Model Direct (`test_model_direct.py`)

Direct model inference testing.

```bash
python test_model_direct.py
```

### Tool RAG Test

#### Test Tool RAG (`test_tool_rag.py`)

Tests tool-based RAG retrieval system.

```bash
python test_tool_rag.py
```

---

## Training & Fine-tuning

Fine-tune models to translate Unity IR JSON to C# code. See `unity_ir_finetuning/README.md` for detailed instructions.

### Quick Start

```bash
# 1. Generate calibration examples
cd "code generation pipeline"
python generate_calibration_examples.py --count 100

# 2. Prepare training data
cd ../unity_ir_finetuning
python data/prepare_data.py ../code\ generation\ pipeline/calibration_examples/calibration_data.json

# 3. Train model
python training/train_peft.py

# 4. Evaluate
python training/evaluate.py --interactive
```

### Training Options

**For OLMo 3 (recommended):**
```bash
python training/train_peft.py --epochs 5 --lora-r 64
```

**For other models (Qwen, Llama, etc.):**
```bash
python training/train_unsloth.py --model unsloth/Qwen2-7B-bnb-4bit
```

**Hardware Requirements:**
- Minimum: 16GB VRAM
- Recommended: 24GB VRAM
- With gradient checkpointing: 12GB VRAM may work

---

## Configuration

### Environment Variables

Create a `.env` file:

```env
ANTHROPIC_API_KEY=sk-ant-...  # For Claude-based generation
```

### LLM Configuration

Edit pipeline files to configure your LLM:

```python
# In unity_pipeline_simple.py or unity_full_pipeline_rag.py
LLM_URL = "http://localhost:1234/v1/chat/completions"
LLM_MODEL = "local-model"  # Your model name
DEFAULT_TEMPERATURE = 0.4
```

### RAG Configuration

```python
# RAG database path (relative to script location)
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "unity_rag_db")

# Embedding model
EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
EMBEDDING_MODEL = "nomic-embed-text"
```

### Steering Configuration

Filter configs are stored in `code generation pipeline/unity_ir_filter_configs/`:

```
unity_ir_filter_configs/
├── local-model-unity-ir/
│   ├── filter_config.json
│   ├── attractor_keywords.json
│   └── attractor_centroids.json
└── unity-hallucination-steering/
    └── filter_config.json
```

Generate configs by running:
```bash
python Attractor_Pipeline_Runner.py
```

---

## Project Structure

```
Pipeline/
├── code generation pipeline/          # Main pipeline scripts
│   ├── unity_pipeline_simple.py     # Simple pipeline
│   ├── unity_full_pipeline_rag.py   # Full RAG pipeline
│   ├── unity_ir_inference.py         # IR generation
│   ├── unity_rag_query.py            # RAG system
│   ├── build_rag_database.py        # RAG builder
│   ├── unity_api_validator*.py      # Code validation
│   ├── unity_script_fixer.py         # Auto-fix errors
│   ├── Attractor_Pipeline_Runner.py # Attractor mapping
│   ├── Calibration_Pipeline_Runner.py # IR calibration
│   ├── unity_rag_db/                 # RAG database
│   └── unity_ir_filter_configs/      # Steering configs
│
├── test scripts/                      # Test scripts
│   ├── test_crazy_prompts.py         # Comprehensive tests
│   ├── test_validation_pipeline.py  # Validation tests
│   ├── test_unity_steering.py        # Steering tests
│   └── prompt_test_results/          # Test outputs
│
├── unity_ir_finetuning/              # Model training
│   ├── data/                         # Training data
│   ├── training/                     # Training scripts
│   └── models/                       # Trained models
│
├── results/                          # Generated results
│   ├── baseline_cache/              # Baseline cache
│   └── unity_ir_mapping_results/   # Mapping results
│
├── archive_deprecated/               # Old/deprecated code
├── requirements.txt                  # Dependencies
└── README.md                         # This file
```

---

## Common Workflows

### Generate a Unity Script

```bash
cd "code generation pipeline"
python unity_pipeline_simple.py "player that collects coins and gains score"
```

### Test Your Setup

```bash
cd "test scripts"
python test_imports.py
python check_rag.py
```

### Build RAG Database

```bash
cd "code generation pipeline"
python build_rag_database.py --input <unity_docs_path> --output unity_rag_db
```

### Map Attractors for Your Model

1. Edit `Attractor_Pipeline_Runner.py`:
   - Set `PROBE_MODE = "unity_ir"`
   - Set `LOCAL_SYNTHESIS_URL` to your LLM
   - Set `ANTHROPIC_API_KEY` (optional, for probe generation)

2. Run pipeline:
   ```bash
   python Attractor_Pipeline_Runner.py
   ```

3. Configs saved to `unity_ir_filter_configs/local-model-unity-ir/`

### Fine-tune a Model

1. Generate training data:
   ```bash
   python generate_calibration_examples.py --count 200
   ```

2. Prepare data:
   ```bash
   cd ../unity_ir_finetuning
   python data/prepare_data.py ../code\ generation\ pipeline/calibration_examples/calibration_data.json
   ```

3. Train:
   ```bash
   python training/train_peft.py
   ```

4. Evaluate:
   ```bash
   python training/evaluate.py --interactive
   ```

---

## Troubleshooting

### "RAG database not found"

Build the RAG database first:
```bash
python build_rag_database.py --input <docs_path> --output unity_rag_db
```

### "Filter config not found"

Run the attractor pipeline:
```bash
python Attractor_Pipeline_Runner.py
```

### "Connection refused" (LLM server)

Ensure your local LLM server is running:
- LM Studio: Start server on port 1234
- Ollama: `ollama serve`
- vLLM: Configure to serve on port 1234

### Import errors after reorganization

All imports have been updated for the new structure. If you see import errors:
1. Ensure you're running scripts from the correct directory
2. Check that `sys.path` modifications in test scripts are correct
3. Verify relative imports in pipeline files

---

## License

MIT License - see [LICENSE](LICENSE) file for details.

---

## Contributing

Contributions welcome! Areas of interest:
- Additional Unity API coverage
- Improved code validation
- Better RAG retrieval strategies
- Model fine-tuning improvements

---

**Built to generate Unity C# code from natural language descriptions.**
