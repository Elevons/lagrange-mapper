"""
Fine-tune LLM for IR → C# translation using Unsloth.
Uses LoRA for efficient training on consumer GPUs.

Usage:
    python train_unsloth.py                    # Use default config
    python train_unsloth.py --epochs 5         # Override epochs
    python train_unsloth.py --model "unsloth/Llama-3.1-8B-bnb-4bit"  # Different model

Requirements:
    pip install unsloth
    # Or see requirements.txt for full list
"""

import os
import sys

# =============================================================================
# WINDOWS FIX: Disable multiprocessing BEFORE any imports
# This prevents Unsloth/datasets from spawning worker processes that fail
# =============================================================================
os.environ["TOKENIZERS_PARALLELISM"] = "false"

# Disable Unsloth's compiled cache which causes Windows multiprocessing issues
os.environ["UNSLOTH_DISABLE_COMPILE"] = "1"

# Set this before any imports
if sys.platform == "win32":
    # Windows: Force single-process mode everywhere
    import multiprocessing
    multiprocessing.set_start_method('spawn', force=True)

import argparse
from pathlib import Path

# Add parent to path
sys.path.insert(0, str(Path(__file__).parent.parent))

def main():
    parser = argparse.ArgumentParser(description="Fine-tune LLM for Unity IR → C# translation")
    parser.add_argument("--model", help="Override base model")
    parser.add_argument("--epochs", type=int, help="Override number of epochs")
    parser.add_argument("--batch-size", type=int, help="Override batch size")
    parser.add_argument("--lr", type=float, help="Override learning rate")
    parser.add_argument("--lora-r", type=int, help="Override LoRA rank")
    parser.add_argument("--output", help="Override output directory")
    parser.add_argument("--no-export", action="store_true", help="Skip model export")
    args = parser.parse_args()
    
    # Import config
    from config import (
        BASE_MODEL, MAX_SEQ_LENGTH, LOAD_IN_4BIT,
        LORA_R, LORA_ALPHA, LORA_DROPOUT, TARGET_MODULES,
        OUTPUT_DIR, BATCH_SIZE, GRADIENT_ACCUMULATION, LEARNING_RATE,
        NUM_EPOCHS, WARMUP_RATIO, SAVE_STEPS, SAVE_TOTAL_LIMIT,
        LOGGING_STEPS, SEED,
        TRAIN_DATA_PATH, EVAL_DATA_PATH,
        EXPORT_MERGED, EXPORT_FORMAT, EXPORT_GGUF, GGUF_QUANTIZATION,
        REPORT_TO, WANDB_PROJECT
    )
    
    # Apply overrides
    if args.model:
        BASE_MODEL = args.model
    if args.epochs:
        NUM_EPOCHS = args.epochs
    if args.batch_size:
        BATCH_SIZE = args.batch_size
    if args.lr:
        LEARNING_RATE = args.lr
    if args.lora_r:
        LORA_R = args.lora_r
        LORA_ALPHA = args.lora_r
    if args.output:
        OUTPUT_DIR = args.output
    
    print("="*70)
    print("UNITY IR → C# FINE-TUNING WITH UNSLOTH")
    print("="*70)
    
    # Check for GPU
    try:
        import torch
        if not torch.cuda.is_available():
            print("\n⚠️  WARNING: CUDA not available!")
            print("   Training will be very slow on CPU.")
            print("   Consider using Google Colab or a cloud GPU.")
            response = input("\nContinue anyway? (y/n): ")
            if response.lower() != 'y':
                return
    except ImportError:
        print("❌ PyTorch not installed. Run: pip install torch")
        return
    
    # Check data files exist
    train_path = Path(__file__).parent.parent / TRAIN_DATA_PATH
    eval_path = Path(__file__).parent.parent / EVAL_DATA_PATH
    
    if not train_path.exists():
        print(f"\n❌ Training data not found: {train_path}")
        print("\nPlease prepare data first:")
        print("   python data/prepare_data.py")
        return
    
    # Count examples
    with open(train_path) as f:
        train_count = sum(1 for _ in f)
    with open(eval_path) as f:
        eval_count = sum(1 for _ in f)
    
    print(f"\nConfiguration:")
    print(f"  Base Model:      {BASE_MODEL}")
    print(f"  Max Seq Length:  {MAX_SEQ_LENGTH}")
    print(f"  LoRA Rank:       {LORA_R}")
    print(f"  Batch Size:      {BATCH_SIZE} (effective: {BATCH_SIZE * GRADIENT_ACCUMULATION})")
    print(f"  Learning Rate:   {LEARNING_RATE}")
    print(f"  Epochs:          {NUM_EPOCHS}")
    print(f"  Train Examples:  {train_count}")
    print(f"  Eval Examples:   {eval_count}")
    print(f"  Output:          {OUTPUT_DIR}")
    
    # Confirm
    print("\n" + "-"*70)
    response = input("Start training? (y/n): ")
    if response.lower() != 'y':
        print("Cancelled.")
        return
    
    # Check if using OLMo - recommend train_peft.py instead
    if "olmo" in BASE_MODEL.lower():
        print("\n⚠️  WARNING: OLMo models may not be fully supported by Unsloth.")
        print("   For OLMo 3, we recommend using train_peft.py instead:")
        print(f"   python training/train_peft.py --model {BASE_MODEL}")
        response = input("\nContinue with Unsloth anyway? (y/n): ")
        if response.lower() != 'y':
            return
    
    # Import Unsloth (after confirmation to avoid slow import if user cancels)
    print("\n[1/6] Loading Unsloth...")
    try:
        from unsloth import FastLanguageModel
        from unsloth.chat_templates import get_chat_template
    except ImportError as e:
        print(f"❌ Unsloth not installed: {e}")
        print("\nInstall with:")
        print("   pip install unsloth")
        print("   # Or for specific CUDA version:")
        print("   pip install 'unsloth[colab-new]'")
        return
    
    from datasets import load_dataset
    from trl import SFTTrainer
    from transformers import TrainingArguments
    import torch
    
    # 1. Load base model
    print("\n[2/6] Loading base model...")
    print(f"       {BASE_MODEL}")
    
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_name=BASE_MODEL,
        max_seq_length=MAX_SEQ_LENGTH,
        load_in_4bit=LOAD_IN_4BIT,
        dtype=None,  # Auto-detect
    )
    
    # Set chat template if not already set
    if tokenizer.chat_template is None:
        tokenizer = get_chat_template(
            tokenizer,
            chat_template="chatml",  # Works with most models
        )
    
    # 2. Add LoRA adapters
    print("\n[3/6] Adding LoRA adapters...")
    print(f"       Rank: {LORA_R}, Alpha: {LORA_ALPHA}")
    
    model = FastLanguageModel.get_peft_model(
        model,
        r=LORA_R,
        lora_alpha=LORA_ALPHA,
        lora_dropout=LORA_DROPOUT,
        target_modules=TARGET_MODULES,
        use_gradient_checkpointing="unsloth",
        random_state=SEED,
    )
    
    # Print trainable parameters
    trainable, total = model.get_nb_trainable_parameters()
    print(f"       Trainable params: {trainable:,} / {total:,} ({100*trainable/total:.2f}%)")
    
    # 3. Load dataset
    print("\n[4/6] Loading training data...")
    
    dataset = load_dataset("json", data_files={
        "train": str(train_path),
        "eval": str(eval_path)
    })
    
    print(f"       Train: {len(dataset['train'])} examples")
    print(f"       Eval:  {len(dataset['eval'])} examples")
    
    # 4. Format and PRE-TOKENIZE the data (single-process, bypasses SFTTrainer's multiprocess tokenization)
    print("       Pre-tokenizing data (single-process for Windows)...")
    
    def format_and_tokenize(examples):
        """Apply chat template and tokenize in one pass."""
        all_input_ids = []
        all_attention_mask = []
        all_labels = []
        
        for convos in examples["conversations"]:
            # Convert ShareGPT format to messages format
            messages = []
            for turn in convos:
                role = turn["from"]
                content = turn["value"]
                
                if role == "system":
                    messages.append({"role": "system", "content": content})
                elif role == "human":
                    messages.append({"role": "user", "content": content})
                elif role == "gpt":
                    messages.append({"role": "assistant", "content": content})
            
            text = tokenizer.apply_chat_template(
                messages,
                tokenize=False,
                add_generation_prompt=False
            )
            
            # Tokenize
            tokenized = tokenizer(
                text,
                truncation=True,
                max_length=MAX_SEQ_LENGTH,
                padding=False,
                return_tensors=None
            )
            
            all_input_ids.append(tokenized["input_ids"])
            all_attention_mask.append(tokenized["attention_mask"])
            # For causal LM, labels = input_ids (shifted internally)
            all_labels.append(tokenized["input_ids"].copy())
        
        return {
            "input_ids": all_input_ids,
            "attention_mask": all_attention_mask,
            "labels": all_labels
        }
    
    # Pre-tokenize with num_proc=1 to avoid Windows multiprocessing issues
    tokenized_dataset = dataset.map(
        format_and_tokenize, 
        batched=True, 
        num_proc=1, 
        remove_columns=["conversations"]
    )
    
    print(f"       Tokenized {len(tokenized_dataset['train'])} train, {len(tokenized_dataset['eval'])} eval examples")
    
    # 5. Training
    print("\n[5/6] Starting training...")
    print(f"       This may take a while...")
    
    output_dir = Path(__file__).parent.parent / OUTPUT_DIR
    output_dir.mkdir(parents=True, exist_ok=True)
    
    training_args = TrainingArguments(
        output_dir=str(output_dir),
        per_device_train_batch_size=BATCH_SIZE,
        gradient_accumulation_steps=GRADIENT_ACCUMULATION,
        learning_rate=LEARNING_RATE,
        num_train_epochs=NUM_EPOCHS,
        warmup_ratio=WARMUP_RATIO,
        logging_steps=LOGGING_STEPS,
        save_steps=SAVE_STEPS,
        eval_strategy="steps",
        eval_steps=SAVE_STEPS,
        save_total_limit=SAVE_TOTAL_LIMIT,
        fp16=not torch.cuda.is_bf16_supported(),
        bf16=torch.cuda.is_bf16_supported(),
        optim="adamw_8bit",
        seed=SEED,
        report_to=REPORT_TO,
        run_name=WANDB_PROJECT if REPORT_TO == "wandb" else None,
        remove_unused_columns=False,  # Keep our pre-tokenized columns
    )
    
    # Use vanilla Trainer instead of SFTTrainer to avoid Unsloth's multiprocessing
    from transformers import Trainer, DataCollatorForLanguageModeling
    
    data_collator = DataCollatorForLanguageModeling(
        tokenizer=tokenizer,
        mlm=False  # Causal LM, not masked LM
    )
    
    trainer = Trainer(
        model=model,
        tokenizer=tokenizer,
        train_dataset=tokenized_dataset["train"],
        eval_dataset=tokenized_dataset["eval"],
        data_collator=data_collator,
        args=training_args,
    )
    
    # Train!
    trainer.train()
    
    # 6. Save model
    print("\n[6/6] Saving model...")
    
    # Save LoRA adapters
    model.save_pretrained(str(output_dir))
    tokenizer.save_pretrained(str(output_dir))
    print(f"       LoRA adapters saved to: {output_dir}")
    
    # Export merged model if requested
    if EXPORT_MERGED and not args.no_export:
        merged_dir = output_dir.parent / f"{output_dir.name}-merged"
        print(f"\n       Exporting merged model to: {merged_dir}")
        
        model.save_pretrained_merged(
            str(merged_dir),
            tokenizer,
            save_method=EXPORT_FORMAT,
        )
        
        # Export GGUF if requested
        if EXPORT_GGUF:
            print(f"\n       Exporting GGUF ({GGUF_QUANTIZATION})...")
            try:
                model.save_pretrained_gguf(
                    str(merged_dir),
                    tokenizer,
                    quantization_method=GGUF_QUANTIZATION,
                )
                print(f"       GGUF saved to: {merged_dir}")
            except Exception as e:
                print(f"       ⚠️ GGUF export failed: {e}")
                print("       You can export GGUF manually using llama.cpp")
    
    # Done!
    print("\n" + "="*70)
    print("TRAINING COMPLETE!")
    print("="*70)
    print(f"\nModel saved to: {output_dir}")
    
    if EXPORT_MERGED:
        print(f"Merged model:   {output_dir.parent / f'{output_dir.name}-merged'}")
    
    print("\nTo use with your pipeline:")
    print("  1. Load the merged model in LM Studio")
    print("  2. Or use the LoRA adapters with transformers:")
    print("     from peft import PeftModel")
    print(f"     model = PeftModel.from_pretrained(base_model, '{output_dir}')")
    
    print("\nTo evaluate:")
    print("  python training/evaluate.py")


if __name__ == "__main__":
    main()

