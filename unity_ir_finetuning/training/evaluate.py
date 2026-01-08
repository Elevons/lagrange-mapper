"""
Evaluate fine-tuned IR → C# model.

Usage:
    python evaluate.py                          # Evaluate on held-out set
    python evaluate.py --samples 10             # Generate 10 sample outputs
    python evaluate.py --interactive            # Interactive testing mode
"""

import json
import argparse
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

def load_model(model_path: str, use_4bit: bool = True):
    """Load the fine-tuned model."""
    from unsloth import FastLanguageModel
    
    print(f"Loading model from: {model_path}")
    
    model, tokenizer = FastLanguageModel.from_pretrained(
        model_path,
        max_seq_length=4096,
        load_in_4bit=use_4bit,
    )
    
    # Set to inference mode
    FastLanguageModel.for_inference(model)
    
    return model, tokenizer


def generate_code(model, tokenizer, ir_json: dict, max_tokens: int = 2000) -> str:
    """Generate C# code from IR JSON."""
    from config import CODE_SYSTEM_PROMPT
    
    # Build prompt
    user_content = f"""BEHAVIOR SPECIFICATION:
{json.dumps(ir_json, indent=2)}

Generate the complete Unity C# MonoBehaviour script:"""
    
    messages = [
        {"role": "system", "content": CODE_SYSTEM_PROMPT},
        {"role": "user", "content": user_content}
    ]
    
    # Apply template
    prompt = tokenizer.apply_chat_template(
        messages,
        tokenize=False,
        add_generation_prompt=True
    )
    
    # Tokenize
    inputs = tokenizer(prompt, return_tensors="pt").to("cuda")
    
    # Generate
    outputs = model.generate(
        **inputs,
        max_new_tokens=max_tokens,
        temperature=0.3,
        do_sample=True,
        top_p=0.9,
        pad_token_id=tokenizer.pad_token_id,
    )
    
    # Decode (only the new tokens)
    generated = tokenizer.decode(
        outputs[0][inputs.input_ids.shape[1]:],
        skip_special_tokens=True
    )
    
    return generated.strip()


def evaluate_samples(model, tokenizer, eval_path: str, num_samples: int = 5):
    """Evaluate on sample examples and compare to ground truth."""
    
    print(f"\nEvaluating on {num_samples} samples from: {eval_path}")
    print("="*70)
    
    # Load eval data
    examples = []
    with open(eval_path, 'r', encoding='utf-8') as f:
        for line in f:
            examples.append(json.loads(line))
    
    examples = examples[:num_samples]
    
    for i, ex in enumerate(examples):
        convos = ex["conversations"]
        
        # Extract IR from the human turn
        human_msg = convos[1]["value"]
        
        # Parse IR JSON from the message
        ir_start = human_msg.find("{")
        ir_end = human_msg.rfind("}") + 1
        ir_str = human_msg[ir_start:ir_end]
        
        try:
            ir_json = json.loads(ir_str)
        except json.JSONDecodeError:
            print(f"\n⚠️ Could not parse IR from example {i+1}")
            continue
        
        # Generate
        print(f"\n{'='*70}")
        print(f"EXAMPLE {i+1}: {ir_json.get('class_name', 'Unknown')}")
        print("="*70)
        
        print("\n--- IR JSON ---")
        print(json.dumps(ir_json, indent=2)[:500] + "...")
        
        print("\n--- GENERATING ---")
        generated = generate_code(model, tokenizer, ir_json)
        
        print("\n--- GENERATED CODE ---")
        print(generated[:1500])
        if len(generated) > 1500:
            print(f"\n... ({len(generated)} chars total)")
        
        # Show expected (ground truth)
        expected = convos[2]["value"]
        print("\n--- EXPECTED CODE ---")
        print(expected[:800])
        if len(expected) > 800:
            print(f"\n... ({len(expected)} chars total)")
        
        # Basic comparison
        print("\n--- COMPARISON ---")
        gen_lines = len(generated.split('\n'))
        exp_lines = len(expected.split('\n'))
        print(f"  Generated: {gen_lines} lines, {len(generated)} chars")
        print(f"  Expected:  {exp_lines} lines, {len(expected)} chars")
        
        # Check for key patterns
        patterns = ["class", "MonoBehaviour", "void Start", "void Update", 
                   "GetComponent", "public ", "[SerializeField]"]
        
        print("\n  Key patterns:")
        for pattern in patterns:
            gen_has = "✓" if pattern in generated else "✗"
            exp_has = "✓" if pattern in expected else "✗"
            if gen_has != exp_has:
                print(f"    {pattern}: generated={gen_has}, expected={exp_has} ⚠️")
            else:
                print(f"    {pattern}: {gen_has}")


def interactive_mode(model, tokenizer):
    """Interactive mode for testing custom IR."""
    
    print("\n" + "="*70)
    print("INTERACTIVE MODE")
    print("="*70)
    print("Enter IR JSON to generate C# code.")
    print("Type 'quit' to exit, 'example' for a sample IR.\n")
    
    sample_ir = {
        "class_name": "SpinningCoin",
        "components": ["Collider"],
        "fields": [
            {"name": "rotationSpeed", "type": "float", "default": 100},
            {"name": "scoreValue", "type": "int", "default": 10}
        ],
        "behaviors": [
            {
                "name": "spin",
                "trigger": "every frame",
                "actions": [
                    {"action": "rotate around Y axis at rotationSpeed"}
                ]
            },
            {
                "name": "collect",
                "trigger": "player touches this object",
                "actions": [
                    {"action": "add scoreValue to player score"},
                    {"action": "destroy this object"}
                ]
            }
        ]
    }
    
    while True:
        try:
            print("\nEnter IR JSON (or 'example', 'quit'):")
            user_input = input("> ").strip()
            
            if user_input.lower() == 'quit':
                break
            
            if user_input.lower() == 'example':
                ir_json = sample_ir
                print("\nUsing sample IR:")
                print(json.dumps(ir_json, indent=2))
            else:
                # Try to parse as JSON
                if not user_input.startswith("{"):
                    print("⚠️ Input should be JSON starting with '{'")
                    continue
                
                try:
                    ir_json = json.loads(user_input)
                except json.JSONDecodeError as e:
                    print(f"⚠️ Invalid JSON: {e}")
                    continue
            
            print("\n--- GENERATING ---")
            generated = generate_code(model, tokenizer, ir_json)
            
            print("\n--- GENERATED C# CODE ---")
            print(generated)
            
        except KeyboardInterrupt:
            print("\n\nExiting...")
            break


def main():
    parser = argparse.ArgumentParser(description="Evaluate IR → C# model")
    parser.add_argument(
        "--model", "-m",
        default="models/unity-ir-to-csharp",
        help="Path to fine-tuned model"
    )
    parser.add_argument(
        "--eval-data", "-e",
        default="data/processed/eval.jsonl",
        help="Path to evaluation data"
    )
    parser.add_argument(
        "--samples", "-s",
        type=int,
        default=3,
        help="Number of samples to evaluate"
    )
    parser.add_argument(
        "--interactive", "-i",
        action="store_true",
        help="Interactive testing mode"
    )
    parser.add_argument(
        "--no-4bit",
        action="store_true",
        help="Load model in full precision (more VRAM)"
    )
    
    args = parser.parse_args()
    
    # Resolve paths relative to this script's parent
    base_dir = Path(__file__).parent.parent
    model_path = base_dir / args.model
    eval_path = base_dir / args.eval_data
    
    # Check paths
    if not model_path.exists():
        print(f"❌ Model not found: {model_path}")
        print("\nTrain a model first:")
        print("   python training/train_unsloth.py")
        return
    
    # Load model
    try:
        model, tokenizer = load_model(str(model_path), use_4bit=not args.no_4bit)
    except Exception as e:
        print(f"❌ Failed to load model: {e}")
        return
    
    if args.interactive:
        interactive_mode(model, tokenizer)
    else:
        if not eval_path.exists():
            print(f"⚠️ Eval data not found: {eval_path}")
            print("   Entering interactive mode instead...")
            interactive_mode(model, tokenizer)
        else:
            evaluate_samples(model, tokenizer, str(eval_path), args.samples)
    
    print("\n✓ Evaluation complete!")


if __name__ == "__main__":
    main()


