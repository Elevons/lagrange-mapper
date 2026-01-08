# Unity IR → C# Fine-tuning

Fine-tune a local LLM to translate Unity IR (Intermediate Representation) JSON specifications into correct, compilable C# MonoBehaviour scripts.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                           PIPELINE                                   │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│   Natural Language    →    IR JSON    →    C# Code                  │
│   "rotating coin"         (structured)     (compilable)             │
│                                ↑                                     │
│                          Fine-tuned                                  │
│                            Model                                     │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

## Quick Start

### 1. Install Dependencies

```bash
pip install -r requirements.txt
```

For specific CUDA versions, see [Unsloth installation guide](https://github.com/unslothai/unsloth).

### 2. Generate Training Data

First, generate calibration examples using Claude as a teacher model:

```bash
# From the parent Pipeline directory
python generate_calibration_examples.py --count 100
```

This creates `calibration_examples/calibration_data.json` with (prompt, IR, code) triplets.

### 3. Prepare Data for Training

```bash
cd unity_ir_finetuning
python data/prepare_data.py ../calibration_examples/calibration_data.json
```

This creates:
- `data/processed/train.jsonl` (85% of examples)
- `data/processed/eval.jsonl` (15% of examples)

### 4. Train the Model

**For OLMo 3 (recommended):**
```bash
python training/train_peft.py
```

**For other models (Qwen, Llama, etc.) with Unsloth:**
```bash
python training/train_unsloth.py --model unsloth/Qwen2-7B-bnb-4bit
```

Training options:
```bash
python training/train_peft.py --epochs 5           # More epochs
python training/train_peft.py --lora-r 64          # Higher LoRA rank
python training/train_peft.py --batch-size 1       # Lower if OOM
```

### 5. Evaluate

```bash
python training/evaluate.py --samples 5       # Eval on held-out set
python training/evaluate.py --interactive     # Interactive testing
```

### 6. Deploy

The trained model is saved to:
- `models/unity-ir-to-csharp/` - LoRA adapters
- `models/unity-ir-to-csharp-merged/` - Full merged model

To use with LM Studio or Ollama:
1. Copy the merged model or GGUF file
2. Load in your inference server
3. Use with the existing pipeline

## Project Structure

```
unity_ir_finetuning/
├── data/
│   ├── raw/                    # Raw calibration data
│   ├── processed/              # Processed train/eval splits
│   │   ├── train.jsonl
│   │   └── eval.jsonl
│   └── prepare_data.py         # Data processing script
├── training/
│   ├── config.py               # Training configuration
│   ├── train_unsloth.py        # Main training script
│   └── evaluate.py             # Evaluation script
├── models/                     # Trained model outputs
│   └── unity-ir-to-csharp/
├── requirements.txt
└── README.md
```

## Configuration

Edit `training/config.py` to customize:

| Setting | Default | Description |
|---------|---------|-------------|
| `BASE_MODEL` | OLMo-3-7B-Instruct | Base model to fine-tune (OLMo 3) |
| `LORA_R` | 32 | LoRA rank (higher = more capacity) |
| `NUM_EPOCHS` | 3 | Training epochs |
| `BATCH_SIZE` | 1 | Batch size per GPU |
| `LEARNING_RATE` | 2e-4 | Learning rate |

## Training Data Format

The training data uses ShareGPT conversation format:

```json
{
  "conversations": [
    {"from": "system", "value": "You are a Unity C# code generator..."},
    {"from": "human", "value": "BEHAVIOR SPECIFICATION:\n{...IR JSON...}\n\nGenerate..."},
    {"from": "gpt", "value": "using UnityEngine;\n\npublic class..."}
  ]
}
```

## Recommended Base Models

| Model | Size | Notes |
|-------|------|-------|
| `allenai/OLMo-3-7B-Instruct` | 7B | **Default** - OLMo 3, open weights |
| `unsloth/Qwen2-7B-bnb-4bit` | 7B | Good for code, pre-quantized |
| `unsloth/Mistral-7B-v0.3-bnb-4bit` | 7B | General purpose |
| `unsloth/codellama-7b-bnb-4bit` | 7B | Code-focused |
| `unsloth/deepseek-coder-6.7b-base-bnb-4bit` | 6.7B | Code specialist |
| `unsloth/Llama-3.1-8B-bnb-4bit` | 8B | Latest Llama |

**Note:** For OLMo 3, use `train_peft.py` (standard transformers). For pre-quantized unsloth models, use `train_unsloth.py`.

## Hardware Requirements

- **Minimum**: 16GB VRAM (RTX 4080, A4000)
- **Recommended**: 24GB VRAM (RTX 4090, A5000, A6000)
- **With gradient checkpointing**: 12GB VRAM may work

For lower VRAM:
- Reduce `BATCH_SIZE` to 1
- Reduce `LORA_R` to 32 or 16
- Use `MAX_SEQ_LENGTH` of 2048

## Troubleshooting

### Out of Memory (OOM)
- Reduce `BATCH_SIZE` in config.py
- Reduce `LORA_R` 
- Enable gradient checkpointing (already enabled by default)

### Slow Training
- Ensure CUDA is properly installed
- Check that xformers is installed
- Use a faster base model (7B vs 13B)

### Poor Output Quality
- Generate more training examples (200-500)
- Increase `NUM_EPOCHS` to 5
- Increase `LORA_R` to 128
- Check that training data is high quality

## Integration with Pipeline

After training, update your pipeline to use the new model:

```python
# In unity_pipeline_simple.py or create a new pipeline
LLM_URL = "http://localhost:1234/v1/chat/completions"  # Point to new model
```

Or load directly with transformers:

```python
from peft import PeftModel
from transformers import AutoModelForCausalLM, AutoTokenizer

base_model = AutoModelForCausalLM.from_pretrained("allenai/OLMo-3-7B-Instruct")
model = PeftModel.from_pretrained(base_model, "models/unity-ir-to-csharp-peft")
tokenizer = AutoTokenizer.from_pretrained("models/unity-ir-to-csharp-peft")
```


