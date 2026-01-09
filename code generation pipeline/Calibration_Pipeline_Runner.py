#!/usr/bin/env python3
"""
Calibration Pipeline Runner

Orchestrates the full IR Calibration Pipeline:
1. Generate ideal examples using Claude Haiku (ground truth)
2. Generate actual outputs from your local model
3. Compute offset vectors between ideal and actual
4. Integrate calibration into the IR system

Usage:
    python Calibration_Pipeline_Runner.py              # Full pipeline
    python Calibration_Pipeline_Runner.py --step 1     # Only generate ideals
    python Calibration_Pipeline_Runner.py --step 2     # Only generate actuals
    python Calibration_Pipeline_Runner.py --step 3     # Only compute offsets
    python Calibration_Pipeline_Runner.py --count 20   # Quick test with 20 examples
"""

import os
import sys
import json
import asyncio
from datetime import datetime
from pathlib import Path
from typing import Optional, Dict, List

# ============================================================================
# CONFIGURATION
# ============================================================================

# Load .env for API key
try:
    from dotenv import load_dotenv
    load_dotenv()
except ImportError:
    pass

# API Keys
ANTHROPIC_API_KEY = os.getenv("ANTHROPIC_API_KEY", "")

# Local LLM Configuration (same as your other pipeline runners)
LOCAL_LLM_URL = "http://localhost:1234/v1/chat/completions"
LOCAL_LLM_MODEL = "local-model"
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "text-embedding-nomic-embed-text-v1.5"

# Embedding batch size (max 300 parallel requests)
EMBEDDING_BATCH_SIZE = 300

# Calibration settings
N_EXAMPLES = 100                    # Number of calibration examples
MAX_CONCURRENT_REQUESTS = 10        # Parallel requests to Claude
OUTPUT_DIR = "calibration_data"     # Output directory

# Pipeline control
RUN_STEP_1_GENERATE_IDEALS = True
RUN_STEP_2_GENERATE_ACTUALS = True
RUN_STEP_3_COMPUTE_OFFSETS = True
RUN_STEP_4_INTEGRATE = True

# Timestamp
TIMESTAMP = datetime.now().strftime("%Y%m%d_%H%M%S")

# ============================================================================
# STEP 1: GENERATE IDEAL EXAMPLES (Claude Haiku)
# ============================================================================

async def step_1_generate_ideals(n_examples: int, output_dir: str) -> str:
    """Generate ideal IR examples using Claude Haiku"""
    
    print("\n" + "="*70)
    print("STEP 1: GENERATE IDEAL EXAMPLES (Claude Haiku)")
    print("="*70)
    
    if not ANTHROPIC_API_KEY:
        print("\n❌ Error: ANTHROPIC_API_KEY not set!")
        print("\nTo fix:")
        print("  1. Create a .env file with: ANTHROPIC_API_KEY=sk-ant-...")
        print("  2. Or set environment variable directly")
        print("  3. Install python-dotenv: pip install python-dotenv")
        return None
    
    # Import the generator
    from generate_calibration_examples import (
        generate_all_examples, 
        save_progress, 
        save_final_output,
        load_progress,
        BEHAVIOR_PROMPTS
    )
    
    # Select prompts
    prompts = BEHAVIOR_PROMPTS[:n_examples]
    
    print(f"\nConfiguration:")
    print(f"  Examples to generate: {len(prompts)}")
    print(f"  Concurrent requests: {MAX_CONCURRENT_REQUESTS}")
    print(f"  Output directory: {output_dir}")
    
    # Check for existing progress
    existing, start_idx = load_progress(output_dir)
    
    if start_idx >= len(prompts):
        print(f"\n✓ All {len(prompts)} ideal examples already generated!")
        output_path = os.path.join(output_dir, "calibration_data.json")
        return output_path
    
    if start_idx > 0:
        print(f"\n  Resuming from example {start_idx + 1}")
    
    print(f"\n{'='*70}")
    print(f"GENERATING {len(prompts) - start_idx} IDEAL EXAMPLES")
    print(f"{'='*70}\n")
    
    # Generate
    new_examples = await generate_all_examples(prompts, start_idx)
    
    # Combine with existing
    all_examples = existing + new_examples
    
    # Save
    save_progress(all_examples, output_dir)
    output_path = save_final_output(all_examples, output_dir)
    
    # Summary
    complete = sum(1 for e in all_examples if e.is_complete())
    print(f"\n  Complete: {complete}/{len(all_examples)}")
    
    return output_path


# ============================================================================
# STEP 2: GENERATE ACTUAL OUTPUTS (Local Model)
# ============================================================================

def _save_actuals_progress(pairs, output_dir, ir_success, ir_fail, code_success, code_fail):
    """Save progress for actuals generation"""
    from datetime import datetime
    output_path = os.path.join(output_dir, "calibration_with_actuals.json")
    output_data = {
        "generated_at": datetime.now().isoformat(),
        "ideal_model": "claude-3-5-haiku-20241022",
        "actual_model": LOCAL_LLM_MODEL,
        "total_examples": len(pairs),
        "ir_successful": ir_success,
        "ir_failed": ir_fail,
        "code_successful": code_success,
        "code_failed": code_fail,
        "pairs": pairs
    }
    os.makedirs(output_dir, exist_ok=True)
    with open(output_path, 'w') as f:
        json.dump(output_data, f, indent=2)


def step_2_generate_actuals(calibration_path: str, output_dir: str) -> str:
    """Generate actual IR AND Code outputs from local model"""
    
    print("\n" + "="*70)
    print("STEP 2: GENERATE ACTUAL OUTPUTS (IR + Code)")
    print("="*70)
    
    if not os.path.exists(calibration_path):
        print(f"\n❌ Error: Calibration data not found: {calibration_path}")
        return None
    
    # Check for existing actuals file (resume support)
    actuals_path = os.path.join(output_dir, "calibration_with_actuals.json")
    if os.path.exists(actuals_path):
        with open(actuals_path) as f:
            existing = json.load(f)
        
        existing_pairs = existing.get("pairs", [])
        ir_complete = sum(1 for p in existing_pairs if p.get("actual_ir"))
        code_complete = sum(1 for p in existing_pairs if p.get("actual_code"))
        
        if ir_complete >= len(existing_pairs) and code_complete >= len(existing_pairs):
            print(f"\n✓ All {len(existing_pairs)} actuals already generated!")
            print(f"  IR: {ir_complete}, Code: {code_complete}")
            return actuals_path
        else:
            print(f"\n  Resuming from existing progress...")
            print(f"    IR complete: {ir_complete}/{len(existing_pairs)}")
            print(f"    Code complete: {code_complete}/{len(existing_pairs)}")
            # Use existing pairs as base
            pairs = existing_pairs
    else:
        # Load from calibration data
        with open(calibration_path) as f:
            data = json.load(f)
        pairs = data.get("pairs", [])
    
    print(f"\n  Loaded {len(pairs)} examples")
    
    # Import local IR generator and assembler
    from unity_ir_inference import UnityIRGenerator
    from unity_script_assembler_v2 import UnityScriptAssemblerV2
    
    # Create generator WITHOUT steering (raw model output)
    print(f"\n  Initializing local IR generator...")
    print(f"    URL: {LOCAL_LLM_URL}")
    print(f"    Model: {LOCAL_LLM_MODEL}")
    
    ir_generator = UnityIRGenerator(
        use_steering=False,  # Raw output, no corrections
        verbose=False
    )
    
    # Create assembler for code generation
    print(f"  Initializing local code assembler...")
    assembler = UnityScriptAssemblerV2(
        llm_url=LOCAL_LLM_URL,
        verbose=False
    )
    
    # ─────────────────────────────────────────────────────────────────────
    # GENERATE ACTUAL IR
    # ─────────────────────────────────────────────────────────────────────
    ir_already_done = sum(1 for p in pairs if p.get("actual_ir"))
    ir_remaining = len(pairs) - ir_already_done
    
    print(f"\n{'='*70}")
    print(f"STAGE 2A: GENERATING ACTUAL IR FROM LOCAL MODEL")
    print(f"{'='*70}")
    print(f"  Already complete: {ir_already_done}/{len(pairs)}")
    print(f"  Remaining: {ir_remaining}\n")
    
    if ir_remaining == 0:
        print("  ✓ All IR already generated!")
        ir_successful = ir_already_done
        ir_failed = 0
    else:
        ir_successful = ir_already_done
        ir_failed = 0
        
        for i, pair in enumerate(pairs):
            # Skip if already done
            if pair.get("actual_ir"):
                continue
            
            prompt = pair["prompt"]
            
            print(f"  [{i+1:3d}/{len(pairs)}] IR: {prompt[:45]}...", end=" ", flush=True)
            
            try:
                result = ir_generator.generate(prompt, max_attempts=1)
                
                if result.success and result.parsed:
                    pair["actual_ir"] = result.parsed
                    ir_successful += 1
                    print("✓")
                else:
                    pair["actual_ir"] = None
                    pair["ir_error"] = result.error or "IR generation failed"
                    ir_failed += 1
                    print("✗")
                    
            except Exception as e:
                pair["actual_ir"] = None
                pair["ir_error"] = str(e)
                ir_failed += 1
                print(f"✗ ({e})")
            
            # Save progress periodically
            if (i + 1) % 10 == 0:
                _save_actuals_progress(pairs, output_dir, ir_successful, ir_failed, 0, 0)
        
        print(f"\n  IR Stage: {ir_successful} successful, {ir_failed} failed")
    
    # ─────────────────────────────────────────────────────────────────────
    # GENERATE ACTUAL CODE (from actual IR)
    # ─────────────────────────────────────────────────────────────────────
    code_already_done = sum(1 for p in pairs if p.get("actual_code"))
    code_remaining = len(pairs) - code_already_done
    
    print(f"\n{'='*70}")
    print(f"STAGE 2B: GENERATING ACTUAL CODE FROM LOCAL MODEL")
    print(f"{'='*70}")
    print(f"  Already complete: {code_already_done}/{len(pairs)}")
    print(f"  Remaining: {code_remaining}\n")
    
    if code_remaining == 0:
        print("  ✓ All Code already generated!")
        code_successful = code_already_done
        code_failed = 0
    else:
        code_successful = code_already_done
        code_failed = 0
        
        for i, pair in enumerate(pairs):
            # Skip if already done
            if pair.get("actual_code"):
                continue
            
            prompt = pair["prompt"]
            
            # Use actual_ir if available, otherwise use ideal_ir
            ir_to_use = pair.get("actual_ir") or pair.get("ideal_ir")
            
            if not ir_to_use:
                pair["actual_code"] = None
                pair["code_error"] = "No IR available"
                code_failed += 1
                continue
            
            print(f"  [{i+1:3d}/{len(pairs)}] Code: {prompt[:43]}...", end=" ", flush=True)
            
            try:
                result = assembler.assemble(ir_to_use)
                
                if result.success and result.code:
                    pair["actual_code"] = result.code
                    code_successful += 1
                    print("✓")
                else:
                    pair["actual_code"] = None
                    pair["code_error"] = result.error or "Code generation failed"
                    code_failed += 1
                    print("✗")
                    
            except Exception as e:
                pair["actual_code"] = None
                pair["code_error"] = str(e)
                code_failed += 1
                print(f"✗ ({e})")
            
            # Save progress periodically
            if (i + 1) % 10 == 0:
                _save_actuals_progress(pairs, output_dir, ir_successful, ir_failed, code_successful, code_failed)
        
        print(f"\n  Code Stage: {code_successful} successful, {code_failed} failed")
    
    # Save updated data
    output_path = os.path.join(output_dir, "calibration_with_actuals.json")
    
    output_data = {
        "generated_at": datetime.now().isoformat(),
        "ideal_model": "claude-3-5-haiku-20241022",
        "actual_model": LOCAL_LLM_MODEL,
        "total_examples": len(pairs),
        "ir_successful": ir_successful,
        "ir_failed": ir_failed,
        "code_successful": code_successful,
        "code_failed": code_failed,
        "pairs": pairs
    }
    
    os.makedirs(output_dir, exist_ok=True)
    with open(output_path, 'w') as f:
        json.dump(output_data, f, indent=2)
    
    print(f"\n{'='*70}")
    print("ACTUAL GENERATION COMPLETE")
    print(f"{'='*70}")
    print(f"  IR Stage:   {ir_successful} successful, {ir_failed} failed")
    print(f"  Code Stage: {code_successful} successful, {code_failed} failed")
    print(f"  Output: {output_path}")
    
    return output_path


# ============================================================================
# STEP 3: COMPUTE OFFSET VECTORS
# ============================================================================

def step_3_compute_offsets(actuals_path: str, output_dir: str) -> str:
    """Compute offset vectors for BOTH IR and Code stages (with batch embedding)"""
    
    print("\n" + "="*70)
    print("STEP 3: COMPUTE OFFSET VECTORS (IR + Code)")
    print("="*70)
    
    if not os.path.exists(actuals_path):
        print(f"\n❌ Error: Actuals data not found: {actuals_path}")
        return None
    
    # Load data
    with open(actuals_path) as f:
        data = json.load(f)
    
    pairs = data.get("pairs", [])
    
    # Count valid pairs for each stage
    ir_valid = [p for p in pairs if p.get("ideal_ir") and p.get("actual_ir")]
    code_valid = [p for p in pairs if p.get("good_code") and p.get("actual_code")]
    
    print(f"\n  Loaded {len(pairs)} pairs")
    print(f"  Valid IR pairs: {len(ir_valid)}")
    print(f"  Valid Code pairs: {len(code_valid)}")
    
    if len(ir_valid) < 5 and len(code_valid) < 5:
        print(f"\n❌ Error: Need at least 5 valid pairs for either stage")
        return None
    
    # Prepare pairs with renamed fields for calibrator
    calibration_pairs = []
    for p in pairs:
        cp = {
            "prompt": p["prompt"],
            "ideal_ir": p.get("ideal_ir"),
            "actual_ir": p.get("actual_ir"),
            "ideal_code": p.get("good_code"),  # Rename from good_code to ideal_code
            "actual_code": p.get("actual_code"),
            "behavior_type": p.get("behavior_type", "general")
        }
        calibration_pairs.append(cp)
    
    # Import calibrator
    from ir_calibration import IRCalibrator
    
    print(f"\n  Initializing calibrator with local embedding API...")
    print(f"    URL: {LOCAL_EMBEDDING_URL}")
    print(f"    Model: {LOCAL_EMBEDDING_MODEL}")
    print(f"    Batch size: {EMBEDDING_BATCH_SIZE}")
    
    calibrator = IRCalibrator(
        model_name=LOCAL_LLM_MODEL,
        embedding_url=LOCAL_EMBEDDING_URL,
        embedding_model=LOCAL_EMBEDDING_MODEL,
        batch_size=EMBEDDING_BATCH_SIZE
    )
    
    # Add ALL pairs at once with batch embedding for BOTH IR and Code
    print(f"\n{'='*70}")
    print("BATCH EMBEDDING ALL PAIRS (IR + Code)")
    print(f"{'='*70}")
    
    calibrator.add_pairs_batch(calibration_pairs, embed_in_parallel=True)
    
    # Compute calibration for both stages
    print(f"\n  Computing calibration vectors...")
    result = calibrator.compute_calibration()
    
    # Save calibrator
    output_path = os.path.join(output_dir, "calibration_offsets.json")
    calibrator.save(output_path)
    
    # Print results
    print(f"\n{'='*70}")
    print("CALIBRATION COMPLETE (IR + Code)")
    print(f"{'='*70}")
    
    # IR Stage results
    if result.get("ir"):
        ir = result["ir"]
        print(f"\n  IR Stage Calibration:")
        print(f"    Pairs: {ir.get('num_pairs', 0)}")
        print(f"    Mean offset magnitude: {ir.get('mean_offset_magnitude', 0):.4f}")
        print(f"    Max offset magnitude: {ir.get('max_offset_magnitude', 0):.4f}")
        print(f"    Min offset magnitude: {ir.get('min_offset_magnitude', 0):.4f}")
    
    # Code Stage results
    if result.get("code"):
        code = result["code"]
        print(f"\n  Code Stage Calibration:")
        print(f"    Pairs: {code.get('num_pairs', 0)}")
        print(f"    Mean offset magnitude: {code.get('mean_offset_magnitude', 0):.4f}")
        print(f"    Max offset magnitude: {code.get('max_offset_magnitude', 0):.4f}")
        print(f"    Min offset magnitude: {code.get('min_offset_magnitude', 0):.4f}")
    
    print(f"\n  Output: {output_path}")
    
    return output_path


# ============================================================================
# STEP 4: INTEGRATE WITH IR SYSTEM
# ============================================================================

def step_4_integrate(offsets_path: str, output_dir: str):
    """Integrate calibration for both IR and Code stages"""
    
    print("\n" + "="*70)
    print("STEP 4: INTEGRATE CALIBRATION (IR + Code)")
    print("="*70)
    
    if not os.path.exists(offsets_path):
        print(f"\n❌ Error: Offsets data not found: {offsets_path}")
        return
    
    # Load calibration
    from ir_calibration import IRCalibrator
    
    calibrator = IRCalibrator(
        model_name=LOCAL_LLM_MODEL,
        embedding_url=LOCAL_EMBEDDING_URL,
        embedding_model=LOCAL_EMBEDDING_MODEL,
        batch_size=EMBEDDING_BATCH_SIZE
    )
    calibrator.load(offsets_path)
    
    # ─────────────────────────────────────────────────────────────────────
    # TEST IR EVALUATION
    # ─────────────────────────────────────────────────────────────────────
    print(f"\n  Testing IR calibration with sample...")
    
    # Sample bad IR (with code leaks)
    bad_ir = {
        "class_name": "TestBehavior",
        "behaviors": [{
            "trigger": "Update()",  # Code leak
            "actions": [
                {"action": "transform.position += Vector3.up * speed"}  # Code leak
            ]
        }]
    }
    
    ir_result = calibrator.evaluate_ir(bad_ir)
    
    print(f"\n  IR Stage Evaluation:")
    print(f"    Distance to ideal cluster: {ir_result.distance_to_ideal_cluster:.4f}")
    print(f"    Needs correction: {ir_result.needs_correction}")
    print(f"    Confidence: {ir_result.confidence:.2f}")
    
    if ir_result.nearest_calibration_pair:
        print(f"    Nearest example: {ir_result.nearest_calibration_pair.prompt[:50]}...")
    
    # ─────────────────────────────────────────────────────────────────────
    # TEST CODE EVALUATION
    # ─────────────────────────────────────────────────────────────────────
    print(f"\n  Testing Code calibration with sample...")
    
    # Sample problematic code
    bad_code = '''
using UnityEngine;
public class TestBehavior : MonoBehaviour {
    void Update() {
        transform.position += Vector3.up;  // Missing deltaTime, wrong pattern
    }
}
'''
    
    code_result = calibrator.evaluate_code(bad_code)
    
    print(f"\n  Code Stage Evaluation:")
    print(f"    Distance to ideal cluster: {code_result.distance_to_ideal_cluster:.4f}")
    print(f"    Needs correction: {code_result.needs_correction}")
    print(f"    Confidence: {code_result.confidence:.2f}")
    
    if code_result.nearest_calibration_pair:
        print(f"    Nearest example: {code_result.nearest_calibration_pair.prompt[:50]}...")
    
    # Create integration snippet
    integration_code = f'''
# ============================================================================
# INTEGRATION CODE - Dual-Stage Calibration for IR and Code
# ============================================================================

from ir_calibration import IRCalibrator

# Load calibration (do once at startup)
_calibrator = IRCalibrator(
    embedding_url="{LOCAL_EMBEDDING_URL}",
    embedding_model="{LOCAL_EMBEDDING_MODEL}",
    batch_size={EMBEDDING_BATCH_SIZE}
)
_calibrator.load("{offsets_path}")

def check_ir_quality(ir_json: dict, behavior_type: str = None) -> bool:
    """Check if generated IR is close to ideal distribution"""
    result = _calibrator.evaluate_ir(ir_json, behavior_type)
    return not result.needs_correction

def check_code_quality(code: str, behavior_type: str = None) -> bool:
    """Check if generated C# code is close to ideal distribution"""
    result = _calibrator.evaluate_code(code, behavior_type)
    return not result.needs_correction

def evaluate_full_pipeline(ir_json: dict, code: str) -> dict:
    """Evaluate both IR and Code stages together"""
    return _calibrator.evaluate(ir_json=ir_json, code=code)

# In your generation loop:
# ir = generate_ir(prompt)
# if not check_ir_quality(ir):
#     ir = regenerate_ir(prompt, lower_temperature=True)
#
# code = assemble_code(ir)
# if not check_code_quality(code):
#     code = regenerate_code(ir, lower_temperature=True)
'''
    
    integration_path = os.path.join(output_dir, "integration_snippet.py")
    with open(integration_path, 'w') as f:
        f.write(integration_code)
    
    print(f"\n{'='*70}")
    print("INTEGRATION READY (IR + Code)")
    print(f"{'='*70}")
    print(f"\n  Integration snippet saved to: {integration_path}")
    print(f"\n  Usage:")
    print(f"    from ir_calibration import IRCalibrator")
    print(f"    calibrator = IRCalibrator()")
    print(f"    calibrator.load('{offsets_path}')")
    print(f"")
    print(f"    # Evaluate IR stage")
    print(f"    ir_result = calibrator.evaluate_ir(your_ir_json)")
    print(f"")
    print(f"    # Evaluate Code stage")
    print(f"    code_result = calibrator.evaluate_code(your_csharp_code)")
    print(f"")
    print(f"    # Or evaluate both at once")
    print(f"    results = calibrator.evaluate(ir_json=..., code=...)")
    print(f"    if result.needs_correction:")
    print(f"        # Handle correction")


# ============================================================================
# MAIN PIPELINE
# ============================================================================

def run_pipeline(
    n_examples: int = N_EXAMPLES,
    output_dir: str = OUTPUT_DIR,
    steps: List[int] = None
):
    """Run the full calibration pipeline"""
    
    # Determine which steps to run
    run_step_1 = steps is None or 1 in steps
    run_step_2 = steps is None or 2 in steps
    run_step_3 = steps is None or 3 in steps
    run_step_4 = steps is None or 4 in steps
    
    print("="*70)
    print("CALIBRATION PIPELINE")
    print("="*70)
    print(f"\nConfiguration:")
    print(f"  Examples: {n_examples}")
    print(f"  Output directory: {output_dir}")
    print(f"  Anthropic API Key: {'✓ Set' if ANTHROPIC_API_KEY else '✗ Missing'}")
    print(f"  Local LLM: {LOCAL_LLM_URL}")
    print(f"  Timestamp: {TIMESTAMP}")
    
    print(f"\nPipeline Steps:")
    print(f"  1. Generate Ideals (Claude):  {'✓ Enabled' if run_step_1 else '✗ Skipped'}")
    print(f"  2. Generate Actuals (Local):  {'✓ Enabled' if run_step_2 else '✗ Skipped'}")
    print(f"  3. Compute Offsets:           {'✓ Enabled' if run_step_3 else '✗ Skipped'}")
    print(f"  4. Integrate:                 {'✓ Enabled' if run_step_4 else '✗ Skipped'}")
    
    # Track outputs
    calibration_path = os.path.join(output_dir, "calibration_data.json")
    actuals_path = os.path.join(output_dir, "calibration_with_actuals.json")
    offsets_path = os.path.join(output_dir, "calibration_offsets.json")
    
    # Step 1: Generate ideal examples
    if run_step_1:
        calibration_path = asyncio.run(step_1_generate_ideals(n_examples, output_dir))
        if not calibration_path:
            print("\n❌ Step 1 failed, stopping pipeline")
            return
    else:
        if not os.path.exists(calibration_path):
            print(f"\n⚠ Skipping step 1 but {calibration_path} not found")
    
    # Step 2: Generate actual outputs
    if run_step_2:
        if os.path.exists(calibration_path):
            actuals_path = step_2_generate_actuals(calibration_path, output_dir)
            if not actuals_path:
                print("\n❌ Step 2 failed, stopping pipeline")
                return
        else:
            print(f"\n⚠ Cannot run step 2: {calibration_path} not found")
    else:
        if not os.path.exists(actuals_path):
            print(f"\n⚠ Skipping step 2 but {actuals_path} not found")
    
    # Step 3: Compute offsets
    if run_step_3:
        if os.path.exists(actuals_path):
            offsets_path = step_3_compute_offsets(actuals_path, output_dir)
            if not offsets_path:
                print("\n❌ Step 3 failed, stopping pipeline")
                return
        else:
            print(f"\n⚠ Cannot run step 3: {actuals_path} not found")
    else:
        if not os.path.exists(offsets_path):
            print(f"\n⚠ Skipping step 3 but {offsets_path} not found")
    
    # Step 4: Integrate
    if run_step_4:
        if os.path.exists(offsets_path):
            step_4_integrate(offsets_path, output_dir)
        else:
            print(f"\n⚠ Cannot run step 4: {offsets_path} not found")
    
    # Final summary
    print("\n" + "="*70)
    print("PIPELINE COMPLETE")
    print("="*70)
    
    print(f"\nOutputs:")
    if os.path.exists(calibration_path):
        print(f"  ✓ Ideal examples: {calibration_path}")
    if os.path.exists(actuals_path):
        print(f"  ✓ Actual outputs: {actuals_path}")
    if os.path.exists(offsets_path):
        print(f"  ✓ Calibration offsets: {offsets_path}")
    
    print(f"\nNext steps:")
    print(f"  1. Review calibration_offsets.json")
    print(f"  2. Integrate into unity_ir_inference.py using the snippet")
    print(f"  3. Run compare mode to verify improvement:")
    print(f"     python unity_full_pipeline_v2.py --compare \"your prompt\"")


# ============================================================================
# CLI
# ============================================================================

def print_usage():
    print("="*70)
    print("CALIBRATION PIPELINE RUNNER")
    print("="*70)
    print("\nUsage:")
    print("  python Calibration_Pipeline_Runner.py [options]")
    print("\nOptions:")
    print("  --count N        Number of examples to generate (default: 100)")
    print("  --step N         Run only step N (1, 2, 3, or 4)")
    print("  --steps 1,2,3    Run specific steps")
    print("  --output DIR     Output directory (default: calibration_data)")
    print("  --help           Show this help message")
    print("\nSteps:")
    print("  1. Generate ideal examples using Claude Haiku")
    print("  2. Generate actual outputs from local model")
    print("  3. Compute offset vectors")
    print("  4. Integrate calibration into IR system")
    print("\nExamples:")
    print("  python Calibration_Pipeline_Runner.py                    # Full pipeline")
    print("  python Calibration_Pipeline_Runner.py --count 20         # Quick test")
    print("  python Calibration_Pipeline_Runner.py --step 2           # Only step 2")
    print("  python Calibration_Pipeline_Runner.py --steps 2,3,4      # Skip ideals")


if __name__ == "__main__":
    import argparse
    
    parser = argparse.ArgumentParser(add_help=False)
    parser.add_argument("--count", type=int, default=N_EXAMPLES)
    parser.add_argument("--step", type=int, default=None)
    parser.add_argument("--steps", type=str, default=None)
    parser.add_argument("--output", type=str, default=OUTPUT_DIR)
    parser.add_argument("--help", "-h", action="store_true")
    
    args = parser.parse_args()
    
    if args.help:
        print_usage()
        sys.exit(0)
    
    # Parse steps
    steps = None
    if args.step:
        steps = [args.step]
    elif args.steps:
        steps = [int(s.strip()) for s in args.steps.split(",")]
    
    try:
        run_pipeline(
            n_examples=args.count,
            output_dir=args.output,
            steps=steps
        )
    except KeyboardInterrupt:
        print("\n\n⚠ Pipeline interrupted!")
        print("  Run again to resume (progress is saved)")

