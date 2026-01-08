"""
Fine-tune LLM for IR → C# translation using standard transformers + PEFT.
No Unsloth - works reliably on Windows.

Usage:
    python train_peft.py                    # Use default config
    python train_peft.py --epochs 5         # Override epochs
    python train_peft.py --model "Qwen/Qwen2-7B"  # Different model
"""

import os
import sys
import json
import argparse
from pathlib import Path
from datetime import datetime

# Disable tokenizer parallelism warnings
os.environ["TOKENIZERS_PARALLELISM"] = "false"

# Add parent to path for config import
sys.path.insert(0, str(Path(__file__).parent))

def main():
    parser = argparse.ArgumentParser(description="Fine-tune LLM for Unity IR → C# (PEFT)")
    parser.add_argument("--model", default="allenai/OLMo-3-7B-Instruct", help="Base model name")
    parser.add_argument("--epochs", type=int, default=6, help="Number of epochs")
    parser.add_argument("--batch-size", type=int, default=1, help="Batch size")
    parser.add_argument("--lr", type=float, default=2e-4, help="Learning rate")
    parser.add_argument("--lora-r", type=int, default=32, help="LoRA rank")
    parser.add_argument("--output", default="models/unity-ir-to-csharp-peft", help="Output directory")
    parser.add_argument("--max-length", type=int, default=2048, help="Max sequence length")
    parser.add_argument("--gradient-accumulation", type=int, default=16, help="Gradient accumulation steps")
    args = parser.parse_args()
    
    print("="*70)
    print("UNITY IR → C# FINE-TUNING (Standard PEFT - Windows Compatible)")
    print("="*70)
    
    # Check for CUDA
    import torch
    if not torch.cuda.is_available():
        print("\n❌ CUDA not available! This script requires a GPU.")
        return
    
    print(f"\nGPU: {torch.cuda.get_device_name(0)}")
    print(f"VRAM: {torch.cuda.get_device_properties(0).total_memory / 1024**3:.1f} GB")
    
    # Check data files
    base_dir = Path(__file__).parent.parent
    train_path = base_dir / "data" / "processed" / "train.jsonl"
    eval_path = base_dir / "data" / "processed" / "eval.jsonl"
    
    if not train_path.exists():
        print(f"\n❌ Training data not found: {train_path}")
        print("\nRun: python data/prepare_data.py")
        return
    
    # Count examples
    with open(train_path) as f:
        train_count = sum(1 for _ in f)
    with open(eval_path) as f:
        eval_count = sum(1 for _ in f)
    
    print(f"\nConfiguration:")
    print(f"  Base Model:      {args.model}")
    print(f"  Max Seq Length:  {args.max_length}")
    print(f"  LoRA Rank:       {args.lora_r}")
    print(f"  Batch Size:      {args.batch_size} (effective: {args.batch_size * args.gradient_accumulation})")
    print(f"  Learning Rate:   {args.lr}")
    print(f"  Epochs:          {args.epochs}")
    print(f"  Train Examples:  {train_count}")
    print(f"  Eval Examples:   {eval_count}")
    print(f"  Output:          {args.output}")
    
    print("\n" + "-"*70)
    response = input("Start training? (y/n): ")
    if response.lower() != 'y':
        print("Cancelled.")
        return
    
    # =========================================================================
    # IMPORTS (after confirmation to avoid slow startup if user cancels)
    # =========================================================================
    print("\n[1/6] Loading libraries...")
    
    from transformers import (
        AutoModelForCausalLM, 
        AutoTokenizer, 
        TrainingArguments,
        Trainer,
        DataCollatorForLanguageModeling,
        BitsAndBytesConfig
    )
    from peft import LoraConfig, get_peft_model, prepare_model_for_kbit_training
    from datasets import load_dataset
    
    # =========================================================================
    # LOAD MODEL (4-bit quantization)
    # =========================================================================
    print("\n[2/6] Loading base model (4-bit quantization)...")
    print(f"       {args.model}")
    print("       This may take a few minutes on first run...")
    
    # 4-bit quantization config
    bnb_config = BitsAndBytesConfig(
        load_in_4bit=True,
        bnb_4bit_quant_type="nf4",
        bnb_4bit_compute_dtype=torch.bfloat16 if torch.cuda.is_bf16_supported() else torch.float16,
        bnb_4bit_use_double_quant=True,  # Nested quantization for more memory savings
    )
    
    # Load model
    model = AutoModelForCausalLM.from_pretrained(
        args.model,
        quantization_config=bnb_config,
        device_map="auto",
        trust_remote_code=True,
    )
    
    # Load tokenizer
    tokenizer = AutoTokenizer.from_pretrained(args.model, trust_remote_code=True)
    
    # Set pad token if not set
    if tokenizer.pad_token is None:
        tokenizer.pad_token = tokenizer.eos_token
        model.config.pad_token_id = tokenizer.eos_token_id
    
    # =========================================================================
    # ADD LORA ADAPTERS
    # =========================================================================
    print("\n[3/6] Adding LoRA adapters...")
    print(f"       Rank: {args.lora_r}")
    
    # Prepare model for k-bit training
    model = prepare_model_for_kbit_training(model)
    
    # Determine target modules based on model architecture
    # OLMo 3 uses Llama-style architecture
    if "olmo" in args.model.lower():
        target_modules = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]
        print(f"       Using OLMo-compatible target modules")
    else:
        target_modules = ["q_proj", "k_proj", "v_proj", "o_proj", "gate_proj", "up_proj", "down_proj"]
    
    # LoRA config
    lora_config = LoraConfig(
        r=args.lora_r,
        lora_alpha=args.lora_r,  # Usually same as r
        lora_dropout=0.05,
        bias="none",
        task_type="CAUSAL_LM",
        target_modules=target_modules
    )
    
    # Add LoRA to model
    model = get_peft_model(model, lora_config)
    
    # Print trainable parameters
    trainable_params = sum(p.numel() for p in model.parameters() if p.requires_grad)
    total_params = sum(p.numel() for p in model.parameters())
    print(f"       Trainable: {trainable_params:,} / {total_params:,} ({100*trainable_params/total_params:.2f}%)")
    
    # =========================================================================
    # LOAD AND TOKENIZE DATA
    # =========================================================================
    print("\n[4/6] Loading and tokenizing data...")
    
    # Load dataset
    dataset = load_dataset("json", data_files={
        "train": str(train_path),
        "eval": str(eval_path)
    })
    
    print(f"       Train: {len(dataset['train'])} examples")
    print(f"       Eval:  {len(dataset['eval'])} examples")
    
    # Tokenization function
    def tokenize_function(examples):
        """Convert ShareGPT format to tokenized inputs."""
        all_input_ids = []
        all_attention_mask = []
        
        for convos in examples["conversations"]:
            # Convert ShareGPT to messages format
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
            
            # Apply chat template
            if hasattr(tokenizer, 'apply_chat_template'):
                text = tokenizer.apply_chat_template(
                    messages,
                    tokenize=False,
                    add_generation_prompt=False
                )
            else:
                # Fallback for tokenizers without chat template
                text = "\n".join([f"{m['role']}: {m['content']}" for m in messages])
            
            # Tokenize
            tokenized = tokenizer(
                text,
                truncation=True,
                max_length=args.max_length,
                padding=False,
            )
            
            all_input_ids.append(tokenized["input_ids"])
            all_attention_mask.append(tokenized["attention_mask"])
        
        return {
            "input_ids": all_input_ids,
            "attention_mask": all_attention_mask,
        }
    
    # Tokenize (single process - no Windows issues!)
    print("       Tokenizing (single-process)...")
    tokenized_dataset = dataset.map(
        tokenize_function,
        batched=True,
        num_proc=1,  # Single process - Windows compatible
        remove_columns=["conversations"],
        desc="Tokenizing"
    )
    
    # =========================================================================
    # TRAINING
    # =========================================================================
    print("\n[5/6] Starting training...")
    
    output_dir = base_dir / args.output
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Training arguments
    training_args = TrainingArguments(
        output_dir=str(output_dir),
        num_train_epochs=args.epochs,
        per_device_train_batch_size=args.batch_size,
        per_device_eval_batch_size=args.batch_size,
        gradient_accumulation_steps=args.gradient_accumulation,
        learning_rate=args.lr,
        weight_decay=0.01,
        warmup_ratio=0.05,
        logging_steps=10,
        save_steps=50,
        eval_strategy="steps",
        eval_steps=50,
        save_total_limit=2,
        fp16=not torch.cuda.is_bf16_supported(),
        bf16=torch.cuda.is_bf16_supported(),
        optim="paged_adamw_8bit",  # Memory-efficient optimizer
        gradient_checkpointing=True,  # Save memory
        max_grad_norm=1.0,
        report_to="none",
        dataloader_num_workers=0,  # Single process - Windows compatible
        remove_unused_columns=False,
    )
    
    # Data collator
    data_collator = DataCollatorForLanguageModeling(
        tokenizer=tokenizer,
        mlm=False,  # Causal LM
    )
    
    # Create trainer
    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_dataset["train"],
        eval_dataset=tokenized_dataset["eval"],
        data_collator=data_collator,
    )
    
    # Train!
    print("       Training started...")
    print("       (This will take a while - grab a coffee ☕)")
    
    start_time = datetime.now()
    trainer.train()
    elapsed = datetime.now() - start_time
    
    print(f"\n       Training completed in {elapsed}")
    
    # =========================================================================
    # SAVE MODEL
    # =========================================================================
    print("\n[6/6] Saving model...")
    
    # Save LoRA adapters
    model.save_pretrained(str(output_dir))
    tokenizer.save_pretrained(str(output_dir))
    
    print(f"       LoRA adapters saved to: {output_dir}")
    
    # Save training info
    info = {
        "base_model": args.model,
        "lora_r": args.lora_r,
        "max_length": args.max_length,
        "epochs": args.epochs,
        "train_examples": train_count,
        "eval_examples": eval_count,
        "training_time": str(elapsed),
        "timestamp": datetime.now().isoformat()
    }
    
    with open(output_dir / "training_info.json", "w") as f:
        json.dump(info, f, indent=2)
    
    # =========================================================================
    # DONE
    # =========================================================================
    print("\n" + "="*70)
    print("TRAINING COMPLETE!")
    print("="*70)
    print(f"\nModel saved to: {output_dir}")
    print("\nTo use the model:")
    print("  from peft import PeftModel")
    print("  from transformers import AutoModelForCausalLM")
    print(f"  base = AutoModelForCausalLM.from_pretrained('{args.model}')")
    print(f"  model = PeftModel.from_pretrained(base, '{output_dir}')")
    print("\nOr merge and export for LM Studio:")
    print(f"  python training/export_model.py --input {args.output}")


if __name__ == "__main__":
    main()

