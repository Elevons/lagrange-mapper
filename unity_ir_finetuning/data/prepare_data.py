"""
Prepare training data for IR → C# fine-tuning.
Converts calibration examples to Unsloth-compatible JSONL format.

Usage:
    python prepare_data.py                                    # Use default path
    python prepare_data.py path/to/calibration_data.json      # Custom input path

First generate calibration data (from Pipeline directory):
    python generate_calibration_examples.py --count 100
"""

import json
import random
import argparse
from pathlib import Path
from typing import List, Dict, Optional
import sys
import os

# Add parent directories to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent.parent))

# =============================================================================
# CONFIGURATION
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

TRAIN_SPLIT = 0.85  # 85% train, 15% eval

# =============================================================================
# DATA PROCESSING
# =============================================================================

def load_calibration_data(path: str) -> List[Dict]:
    """Load calibration data from Claude-generated examples."""
    with open(path, encoding='utf-8') as f:
        data = json.load(f)
    return data.get("pairs", [])


def validate_ir_structure(ir_json: Dict) -> bool:
    """Check if IR JSON has required structure."""
    required_keys = ["class_name", "components", "behaviors"]
    
    if not all(key in ir_json for key in required_keys):
        return False
    
    # Must have at least one behavior
    if not ir_json.get("behaviors"):
        return False
    
    return True


def validate_code(code: str) -> bool:
    """Basic validation of generated C# code."""
    if not code or len(code) < 50:
        return False
    
    # Must have class declaration
    if "class" not in code:
        return False
    
    # Must inherit from MonoBehaviour
    if "MonoBehaviour" not in code:
        return False
    
    return True


def format_for_training(pair: Dict, format_type: str = "chatml") -> Optional[Dict]:
    """
    Convert a (prompt, ir, code) pair to training format.
    
    Supports multiple formats:
    - "chatml": ChatML format for most models
    - "alpaca": Alpaca instruction format
    - "sharegpt": ShareGPT conversation format (Unsloth default)
    """
    ir_json = pair.get("ideal_ir", {})
    code = pair.get("good_code", "")
    
    # Validate
    if not validate_ir_structure(ir_json):
        return None
    if not validate_code(code):
        return None
    
    # Format the user input (IR specification)
    user_content = f"""BEHAVIOR SPECIFICATION:
{json.dumps(ir_json, indent=2)}

Generate the complete Unity C# MonoBehaviour script:"""
    
    if format_type == "sharegpt":
        # ShareGPT format - what Unsloth expects by default
        return {
            "conversations": [
                {"from": "system", "value": CODE_SYSTEM_PROMPT},
                {"from": "human", "value": user_content},
                {"from": "gpt", "value": code}
            ]
        }
    elif format_type == "chatml":
        # ChatML format
        return {
            "messages": [
                {"role": "system", "content": CODE_SYSTEM_PROMPT},
                {"role": "user", "content": user_content},
                {"role": "assistant", "content": code}
            ]
        }
    elif format_type == "alpaca":
        # Alpaca format
        return {
            "instruction": CODE_SYSTEM_PROMPT,
            "input": user_content,
            "output": code
        }
    else:
        raise ValueError(f"Unknown format: {format_type}")


def prepare_dataset(
    input_path: str, 
    output_dir: str,
    format_type: str = "sharegpt",
    seed: int = 42
):
    """Process calibration data and split into train/eval sets."""
    
    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)
    
    # Load raw data
    print(f"Loading calibration data from: {input_path}")
    pairs = load_calibration_data(input_path)
    print(f"  Loaded {len(pairs)} calibration pairs")
    
    # Convert to training format
    formatted = []
    skipped = 0
    
    for pair in pairs:
        example = format_for_training(pair, format_type)
        if example:
            formatted.append(example)
        else:
            skipped += 1
    
    print(f"  Formatted {len(formatted)} valid examples ({skipped} skipped)")
    
    if len(formatted) < 10:
        print("\n⚠️  WARNING: Very few training examples!")
        print("   Consider generating more calibration examples first.")
        print("   Run: python generate_calibration_examples.py --count 100")
    
    # Shuffle and split
    random.seed(seed)
    random.shuffle(formatted)
    split_idx = int(len(formatted) * TRAIN_SPLIT)
    
    train_data = formatted[:split_idx]
    eval_data = formatted[split_idx:]
    
    # Save
    train_path = output_path / "train.jsonl"
    eval_path = output_path / "eval.jsonl"
    
    with open(train_path, 'w', encoding='utf-8') as f:
        for example in train_data:
            f.write(json.dumps(example) + "\n")
    
    with open(eval_path, 'w', encoding='utf-8') as f:
        for example in eval_data:
            f.write(json.dumps(example) + "\n")
    
    print(f"\n✓ Saved {len(train_data)} train examples to {train_path}")
    print(f"✓ Saved {len(eval_data)} eval examples to {eval_path}")
    
    # Print sample
    if formatted:
        print("\n" + "="*60)
        print("SAMPLE TRAINING EXAMPLE")
        print("="*60)
        sample = formatted[0]
        if format_type == "sharegpt":
            print(f"User: {sample['conversations'][1]['value'][:200]}...")
            print(f"\nAssistant: {sample['conversations'][2]['value'][:300]}...")
        elif format_type == "chatml":
            print(f"User: {sample['messages'][1]['content'][:200]}...")
            print(f"\nAssistant: {sample['messages'][2]['content'][:300]}...")
    
    return len(train_data), len(eval_data)


def main():
    parser = argparse.ArgumentParser(
        description="Prepare training data for IR → C# fine-tuning"
    )
    parser.add_argument(
        "input", 
        nargs="?",
        default="./calibration_examples/calibration_data.json",
        help="Path to calibration_data.json"
    )
    parser.add_argument(
        "--output", "-o",
        default="data/processed",
        help="Output directory for processed data"
    )
    parser.add_argument(
        "--format", "-f",
        choices=["sharegpt", "chatml", "alpaca"],
        default="sharegpt",
        help="Output format (default: sharegpt for Unsloth)"
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=42,
        help="Random seed for shuffling"
    )
    
    args = parser.parse_args()
    
    print("="*60)
    print("UNITY IR → C# DATA PREPARATION")
    print("="*60)
    
    # Resolve input path
    input_path = args.input
    if not os.path.isabs(input_path):
        # Try relative to script location first
        script_dir = Path(__file__).parent.parent
        candidate = script_dir / input_path
        if candidate.exists():
            input_path = str(candidate)
        else:
            # Try relative to CWD
            input_path = args.input
    
    # Check input file exists
    if not os.path.exists(input_path):
        print(f"\n❌ Input file not found: {input_path}")
        print("\nTo generate calibration data, first run from the Pipeline directory:")
        print("   cd ..")
        print("   python generate_calibration_examples.py --count 100")
        print("\nThen run this script again:")
        print("   cd unity_ir_finetuning")
        print("   python data/prepare_data.py")
        return
    
    print(f"\nInput file: {input_path}")
    
    # Prepare dataset
    prepare_dataset(
        input_path,
        args.output,
        format_type=args.format,
        seed=args.seed
    )
    
    print("\n" + "="*60)
    print("DATA PREPARATION COMPLETE")
    print("="*60)
    print("\nNext step: Run training")
    print("   python training/train_unsloth.py")


if __name__ == "__main__":
    main()

