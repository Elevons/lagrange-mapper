"""
Configuration for Unity IR → C# fine-tuning.
Edit these values to customize training.
"""

# =============================================================================
# SYSTEM PROMPT (Used for both training and inference)
# =============================================================================

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert the behavior specification into a complete MonoBehaviour script.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, FixedUpdate, OnTriggerEnter, OnCollisionEnter, etc.)
2. Use correct Unity APIs - never invent methods that don't exist
3. Declare all configurable fields as public or [SerializeField] private
4. Add required using statements (UnityEngine, System.Collections, etc.)
5. Make the code production-ready and compilable
6. Match the behavior specification exactly - implement all described behaviors
7. Use GetComponent<T>() in Start() or Awake() for component references
8. Handle null checks appropriately

COMMON UNITY PATTERNS:
- Play audio: audioSource.Play() or AudioSource.PlayClipAtPoint(clip, position)
- Apply force: rigidbody.AddForce(direction * force)
- Detect collision: OnCollisionEnter(Collision collision) or OnTriggerEnter(Collider other)
- Spawn object: Instantiate(prefab, position, rotation)
- Destroy: Destroy(gameObject) or Destroy(gameObject, delay)
- Check tag: other.CompareTag("TagName")
- Move: transform.Translate() or rigidbody.MovePosition()
- Rotate: transform.Rotate() or Quaternion.Euler()

Output ONLY the C# code. No markdown, no explanations, no comments about what you're doing."""

# =============================================================================
# MODEL CONFIGURATION
# =============================================================================

# Base model to fine-tune
# Options (sorted by VRAM efficiency):
#   - "allenai/OLMo-3-7B-Instruct"           (OLMo 3, ~12GB with 4bit) ** CURRENT **
#   - "unsloth/Qwen2-7B-bnb-4bit"            (Good for code, ~12GB)
#   - "unsloth/Mistral-7B-v0.3-bnb-4bit"     (Good general purpose, ~12GB)
#   - "unsloth/codellama-7b-bnb-4bit"        (Code-focused, ~12GB)
#   - "unsloth/deepseek-coder-6.7b-base-bnb-4bit"  (Code specialist, ~11GB)
#   - "unsloth/Llama-3.1-8B-bnb-4bit"        (Latest Llama, ~14GB)
#
# OLMo 3 variants:
#   - "allenai/OLMo-3-7B-Instruct"           (Instruction-tuned, best for tasks)
#   - "allenai/OLMo-3-7B-Think"              (Reasoning-focused)
#   - "allenai/OLMo-3-7B-Instruct-DPO"       (DPO fine-tuned)
#   - "allenai/OLMo-3-7B-Instruct-SFT"       (SFT fine-tuned)
#
# NOTE: OLMo uses standard transformers loading with manual 4-bit quantization
#       (not pre-quantized unsloth format). Use train_peft.py for OLMo models.
#       Requires: pip install ai2-olmo
#
# NOTE: Your 5060 Ti has 16GB VRAM. Settings below are optimized for this.
BASE_MODEL = "allenai/OLMo-3-7B-Instruct"

# Sequence length - reduced for 16GB VRAM
# IR JSON (~500 tokens) + C# code (~1000 tokens) = ~1500 tokens typical
# Using 2048 gives headroom while saving VRAM
MAX_SEQ_LENGTH = 2048

# Load model in 4-bit for memory efficiency (REQUIRED for 16GB)
LOAD_IN_4BIT = True

# =============================================================================
# LORA CONFIGURATION (Optimized for 16GB VRAM)
# =============================================================================

# LoRA rank - reduced to 32 for memory efficiency
# r=32 is still very capable for specialized tasks like IR → C#
# Increase to 64 if you have headroom, decrease to 16 if OOM
LORA_R = 32

# LoRA scaling factor (usually equals LORA_R)
LORA_ALPHA = 32

# Dropout for regularization
LORA_DROPOUT = 0.05

# Which modules to adapt
# For most models, these are the attention and MLP layers
# OLMo 3 uses Llama-style architecture with standard module names
TARGET_MODULES = [
    "q_proj", "k_proj", "v_proj", "o_proj",  # Attention
    "gate_proj", "up_proj", "down_proj"       # MLP (Llama-style, works for OLMo 3)
]

# Alternative target modules for different architectures:
# OLMo 2 (older): ["att_proj", "ff_proj"] or ["q_proj", "k_proj", "v_proj", "ff_proj"]
# Qwen/Llama: ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]

# =============================================================================
# TRAINING CONFIGURATION (Optimized for 16GB VRAM - 5060 Ti)
# =============================================================================

# Output directory for model checkpoints
OUTPUT_DIR = "models/unity-ir-to-csharp"

# Batch size per GPU - SET TO 1 for 16GB VRAM
# This is the minimum; we compensate with gradient accumulation
BATCH_SIZE = 1

# Gradient accumulation - effective batch = BATCH_SIZE * GRADIENT_ACCUMULATION
# With batch_size=1, this gives effective batch of 16
# Larger effective batch = more stable training
GRADIENT_ACCUMULATION = 16

# Learning rate (2e-4 is good starting point for LoRA)
LEARNING_RATE = 2e-4

# Number of training epochs (2-5 is usually enough for specialized tasks)
# With small dataset, may need more epochs
NUM_EPOCHS = 3

# Warmup ratio (fraction of steps for LR warmup)
WARMUP_RATIO = 0.05

# Save checkpoint every N steps
SAVE_STEPS = 50

# Keep only N most recent checkpoints (saves disk space)
SAVE_TOTAL_LIMIT = 2

# Logging frequency
LOGGING_STEPS = 10

# Random seed for reproducibility
SEED = 42

# =============================================================================
# MEMORY OPTIMIZATION FLAGS
# =============================================================================

# These are applied automatically by Unsloth, but documented here for reference:
# - Gradient checkpointing: ENABLED (trades compute for memory)
# - 8-bit optimizer: ENABLED (AdamW 8-bit saves ~50% optimizer memory)
# - Flash Attention: AUTO (uses if available)
#
# If you still get OOM, try:
# 1. Reduce MAX_SEQ_LENGTH to 1536 or 1024
# 2. Reduce LORA_R to 16
# 3. Remove some TARGET_MODULES (try just attention: q_proj, v_proj)

# =============================================================================
# DATA PATHS
# =============================================================================

# Training data (JSONL files)
TRAIN_DATA_PATH = "data/processed/train.jsonl"
EVAL_DATA_PATH = "data/processed/eval.jsonl"

# =============================================================================
# EXPORT CONFIGURATION
# =============================================================================

# Export merged model for deployment
EXPORT_MERGED = True

# Export format: "merged_16bit", "merged_4bit", or "lora_only"
# Using 4bit saves disk space (~4GB vs ~14GB for 7B model)
EXPORT_FORMAT = "merged_4bit"

# Also export GGUF for llama.cpp / LM Studio / Ollama
EXPORT_GGUF = True
GGUF_QUANTIZATION = "q4_k_m"  # Options: q4_k_m, q5_k_m, q8_0, f16

# =============================================================================
# WANDB CONFIGURATION (Optional)
# =============================================================================

# Set to "wandb" to enable Weights & Biases tracking
# Set to "none" to disable
REPORT_TO = "none"

# W&B project name (if enabled)
WANDB_PROJECT = "unity-ir-finetune"

