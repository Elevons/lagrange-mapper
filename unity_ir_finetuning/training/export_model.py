"""
Export trained LoRA model to merged format for LM Studio / Ollama.

Usage:
    python export_model.py                                    # Use defaults
    python export_model.py --input models/unity-ir-to-csharp-peft
    python export_model.py --format gguf --quant q4_k_m       # Export as GGUF
"""

import os
import sys
import json
import argparse
from pathlib import Path

def main():
    parser = argparse.ArgumentParser(description="Export LoRA model to merged format")
    parser.add_argument("--input", default="models/unity-ir-to-csharp-peft", help="Input LoRA adapter path")
    parser.add_argument("--output", help="Output path (default: input-merged)")
    parser.add_argument("--format", choices=["safetensors", "gguf"], default="safetensors", help="Output format")
    parser.add_argument("--quant", default="q4_k_m", help="GGUF quantization (q4_k_m, q5_k_m, q8_0, f16)")
    args = parser.parse_args()
    
    print("="*60)
    print("EXPORT LORA MODEL")
    print("="*60)
    
    base_dir = Path(__file__).parent.parent
    input_path = base_dir / args.input
    
    if not input_path.exists():
        print(f"\n❌ Input path not found: {input_path}")
        return
    
    # Load training info
    info_path = input_path / "training_info.json"
    if info_path.exists():
        with open(info_path) as f:
            info = json.load(f)
        base_model = info.get("base_model", "allenai/OLMo-3-7B-Instruct")
    else:
        print("⚠️  training_info.json not found, assuming allenai/OLMo-3-7B-Instruct")
        base_model = "allenai/OLMo-3-7B-Instruct"
    
    output_path = base_dir / (args.output or f"{args.input}-merged")
    
    print(f"\nConfiguration:")
    print(f"  Input:      {input_path}")
    print(f"  Base Model: {base_model}")
    print(f"  Output:     {output_path}")
    print(f"  Format:     {args.format}")
    
    print("\n" + "-"*60)
    response = input("Continue? (y/n): ")
    if response.lower() != 'y':
        return
    
    # Import after confirmation
    print("\n[1/3] Loading libraries...")
    import torch
    from transformers import AutoModelForCausalLM, AutoTokenizer
    from peft import PeftModel
    
    # Load base model (full precision for merging)
    print("\n[2/3] Loading and merging model...")
    print("       This may take a few minutes...")
    
    # Load base model
    base = AutoModelForCausalLM.from_pretrained(
        base_model,
        torch_dtype=torch.float16,
        device_map="auto",
        trust_remote_code=True,
    )
    
    tokenizer = AutoTokenizer.from_pretrained(base_model, trust_remote_code=True)
    
    # Load LoRA adapters
    model = PeftModel.from_pretrained(base, str(input_path))
    
    # Merge LoRA into base model
    print("       Merging LoRA weights...")
    model = model.merge_and_unload()
    
    # Save
    print("\n[3/3] Saving merged model...")
    output_path.mkdir(parents=True, exist_ok=True)
    
    if args.format == "safetensors":
        model.save_pretrained(str(output_path), safe_serialization=True)
        tokenizer.save_pretrained(str(output_path))
        print(f"\n✓ Saved to: {output_path}")
        print("\nTo use in LM Studio:")
        print(f"  1. Copy {output_path} to your LM Studio models folder")
        print("  2. Load and use!")
        
    elif args.format == "gguf":
        # For GGUF, we need llama.cpp's convert script
        print("\n⚠️  GGUF export requires llama.cpp")
        print("   First, save as safetensors, then convert:")
        print(f"   python llama.cpp/convert_hf_to_gguf.py {output_path} --outtype {args.quant}")
        
        # Save safetensors first
        model.save_pretrained(str(output_path), safe_serialization=True)
        tokenizer.save_pretrained(str(output_path))
        print(f"\n✓ Saved safetensors to: {output_path}")
    
    print("\n" + "="*60)
    print("EXPORT COMPLETE")
    print("="*60)


if __name__ == "__main__":
    main()

