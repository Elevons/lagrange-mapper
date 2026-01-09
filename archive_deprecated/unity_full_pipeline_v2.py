"""
Unity Full Pipeline V2 - Simplified Direct LLM Architecture
Natural Language → IR JSON → C# Code (no RAG!)

Compare mode available to see oneshot vs IR pipeline output.
"""
import sys
import json
import requests
from unity_ir_inference import UnityIRGenerator
from unity_script_assembler_v2 import UnityScriptAssemblerV2
from unity_code_validator import UnityAPIValidator

# LLM Configuration
LLM_URL = "http://localhost:1234/v1/chat/completions"
LLM_MODEL = "local-model"

# ============================================================================
# ONESHOT GENERATOR (Direct NL → C#)
# ============================================================================

ONESHOT_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert natural language descriptions into complete, working Unity MonoBehaviour scripts.

REQUIREMENTS:
1. Create a MonoBehaviour class with an appropriate name based on the description
2. Use proper Unity lifecycle methods (Start, Update, FixedUpdate, etc.)
3. Handle collisions with OnCollisionEnter, OnTriggerEnter as appropriate
4. Use correct Unity APIs: Rigidbody.AddForce, Physics.OverlapSphere, etc.
5. Declare serialized public fields for configurable values
6. Add necessary using statements
7. Make the code production-ready and compilable
8. Add helpful comments for clarity

OUTPUT only the complete C# script, no markdown, no explanations."""


def generate_oneshot(prompt: str, temperature: float = 0.3, verbose: bool = False) -> tuple:
    """
    Generate Unity C# code directly from natural language (no IR step).
    
    Args:
        prompt: Natural language description
        temperature: LLM temperature
        verbose: Print debug info
        
    Returns:
        tuple: (code, success, error)
    """
    if verbose:
        print(f"Oneshot generation for: {prompt[:50]}...")
    
    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": LLM_MODEL,
                "messages": [
                    {"role": "system", "content": ONESHOT_SYSTEM_PROMPT},
                    {"role": "user", "content": prompt}
                ],
                "temperature": temperature,
                "max_tokens": 4000
            },
            timeout=120
        )
        response.raise_for_status()
        
        code = response.json()["choices"][0]["message"]["content"].strip()
        
        # Clean up markdown if present
        if "```" in code:
            lines = code.split('\n')
            in_code_block = False
            clean_lines = []
            
            for line in lines:
                if line.strip().startswith('```'):
                    in_code_block = not in_code_block
                    continue
                if in_code_block or not line.strip().startswith('```'):
                    clean_lines.append(line)
            
            code = '\n'.join(clean_lines).strip()
        
        return code, True, None
        
    except Exception as e:
        return None, False, str(e)


# ============================================================================
# PIPELINE CLASS
# ============================================================================

class UnityPipelineV2:
    """End-to-end pipeline: Natural Language → IR JSON → C# Code"""
    
    def __init__(self, verbose: bool = False, validate: bool = True, 
                 fast_mode: bool = False, max_corrections: int = 1):
        """
        Initialize the pipeline.
        
        Args:
            verbose: Print detailed output
            validate: Run C# validation (Stage 3)
            fast_mode: Skip IR steering corrections (2x faster)
            max_corrections: Max IR correction attempts (1-3, default 3)
        """
        self.verbose = verbose
        self.validate = validate
        self.fast_mode = fast_mode
        self.max_corrections = max_corrections if not fast_mode else 0
        
        # Stage 1: IR Generator
        self.ir_generator = UnityIRGenerator(
            intensity=0.5,
            use_steering=not fast_mode,  # Disable steering in fast mode
            verbose=verbose
        )
        
        # Stage 2: Script Assembler (direct LLM, no RAG)
        self.assembler = UnityScriptAssemblerV2(verbose=verbose)
        
        # Stage 3: Code Validator (Lagrange clustering)
        self.validator = None
        if validate:
            if verbose:
                print("Loading Unity API validator...")
            self.validator = UnityAPIValidator()
        
        if verbose:
            mode = "with validation" if validate else "no validation"
            print(f"Pipeline V2 initialized (Direct LLM mode, {mode})")
    
    def generate(self, prompt: str, show_ir: bool = True, show_code: bool = True):
        """
        Generate a Unity C# script from natural language.
        
        Args:
            prompt: Natural language description of desired behavior
            show_ir: Whether to display the IR JSON
            show_code: Whether to display the generated C# code
            
        Returns:
            tuple: (ir_json, c#_code, success)
        """
        if self.verbose or show_ir or show_code:
            print(f"\n{'='*60}")
            print(f"STAGE 1: Natural Language → IR JSON")
            print(f"{'='*60}")
        
        # Stage 1: Generate IR JSON
        ir_result = self.ir_generator.generate(prompt, max_attempts=self.max_corrections)
        
        if not ir_result.success:
            print(f"❌ IR generation failed: {ir_result.error}")
            return None, None, False
        
        ir_json = ir_result.parsed
        
        if show_ir:
            print(f"\nIR JSON generated ({ir_result.attempts} attempts):")
            if ir_result.was_steered:
                triggered = ir_result.initial_detection.triggered_attractors if ir_result.initial_detection else []
                print(f"  Steering applied: {triggered}")
            print(f"\n{'-'*60}")
            print(json.dumps(ir_json, indent=2))
            print(f"{'-'*60}")
        
        # Stage 2: Generate C# code
        if self.verbose or show_code:
            print(f"\n{'='*60}")
            print(f"STAGE 2: IR JSON → C# Script")
            print(f"{'='*60}")
        
        asm_result = self.assembler.assemble(ir_json)
        
        if not asm_result.success:
            print(f"❌ Code assembly failed: {asm_result.error}")
            return ir_json, None, False
        
        final_code = asm_result.code
        
        # Stage 3: Validate and correct (optional)
        if self.validator:
            if self.verbose or show_code:
                print(f"\n{'='*60}")
                print(f"STAGE 3: C# Code Validation")
                print(f"{'='*60}")
            
            validation = self.validator.validate_code(asm_result.code, auto_correct=True)
            
            if not validation.is_valid:
                if self.verbose:
                    print(f"\n⚠️ Validation found issues:")
                    for issue in validation.invalid_apis:
                        print(f"  Line {issue['line']}: {issue['api']} → {issue['suggestion']}")
                
                if validation.corrected_code:
                    final_code = validation.corrected_code
                    if self.verbose:
                        print(f"✓ Auto-corrected {len(validation.invalid_apis)} issue(s)")
            elif self.verbose:
                print(f"✓ Code validation passed")
        
        if show_code:
            print(f"\nC# script generated: {asm_result.class_name}")
            if self.validator and not validation.is_valid:
                print(f"  (auto-corrected {len(validation.invalid_apis)} invalid APIs)")
            print(f"\n{'-'*60}")
            print(final_code)
            print(f"{'-'*60}")
        
        return ir_json, final_code, True
    
    def compare(self, prompt: str, show_diff: bool = True):
        """
        Compare oneshot generation vs IR pipeline output.
        
        Args:
            prompt: Natural language description
            show_diff: Show detailed comparison
            
        Returns:
            dict with both outputs and comparison data
        """
        print(f"\n{'='*70}")
        print(f"COMPARE MODE: Oneshot vs IR Pipeline")
        print(f"{'='*70}")
        print(f"Prompt: {prompt}")
        print(f"{'='*70}")
        
        results = {
            "prompt": prompt,
            "oneshot": {"code": None, "success": False, "error": None},
            "ir_pipeline": {"ir_json": None, "code": None, "success": False, "error": None, "steered": False}
        }
        
        # ─────────────────────────────────────────────────────────────────────
        # ONESHOT: Natural Language → C# (direct)
        # ─────────────────────────────────────────────────────────────────────
        print(f"\n{'─'*70}")
        print("│ ONESHOT (NL → C# direct)")
        print(f"{'─'*70}")
        
        oneshot_code, oneshot_success, oneshot_error = generate_oneshot(
            prompt, verbose=self.verbose
        )
        
        results["oneshot"]["success"] = oneshot_success
        
        if oneshot_success:
            results["oneshot"]["code"] = oneshot_code
            print(oneshot_code)
        else:
            results["oneshot"]["error"] = oneshot_error
            print(f"❌ Oneshot failed: {oneshot_error}")
        
        # ─────────────────────────────────────────────────────────────────────
        # IR PIPELINE: Natural Language → IR JSON → C#
        # ─────────────────────────────────────────────────────────────────────
        print(f"\n{'─'*70}")
        print("│ IR PIPELINE (NL → IR → C#)")
        print(f"{'─'*70}")
        
        ir_json, ir_code, ir_success = self.generate(
            prompt, show_ir=True, show_code=False  # We'll print code ourselves
        )
        
        results["ir_pipeline"]["success"] = ir_success
        
        if ir_success:
            results["ir_pipeline"]["ir_json"] = ir_json
            results["ir_pipeline"]["code"] = ir_code
            
            # Check if steering was applied
            if hasattr(self.ir_generator, '_last_result'):
                results["ir_pipeline"]["steered"] = self.ir_generator._last_result.was_steered
            
            print(f"\n{'-'*40}")
            print("Generated C# Code:")
            print(f"{'-'*40}")
            print(ir_code)
        else:
            results["ir_pipeline"]["error"] = "IR pipeline failed"
            print(f"❌ IR pipeline failed")
        
        # ─────────────────────────────────────────────────────────────────────
        # COMPARISON SUMMARY
        # ─────────────────────────────────────────────────────────────────────
        if show_diff and oneshot_success and ir_success:
            print(f"\n{'='*70}")
            print("│ COMPARISON")
            print(f"{'='*70}")
            
            oneshot_lines = len(oneshot_code.split('\n')) if oneshot_code else 0
            ir_lines = len(ir_code.split('\n')) if ir_code else 0
            oneshot_chars = len(oneshot_code) if oneshot_code else 0
            ir_chars = len(ir_code) if ir_code else 0
            
            print(f"  Oneshot:     {oneshot_lines:3d} lines, {oneshot_chars:5d} chars")
            print(f"  IR Pipeline: {ir_lines:3d} lines, {ir_chars:5d} chars")
            
            # Basic structural comparison
            if oneshot_code and ir_code:
                oneshot_has_class = "class " in oneshot_code
                ir_has_class = "class " in ir_code
                oneshot_has_mono = "MonoBehaviour" in oneshot_code
                ir_has_mono = "MonoBehaviour" in ir_code
                
                print(f"\n  Structure:")
                print(f"    Oneshot:     class={oneshot_has_class}, MonoBehaviour={oneshot_has_mono}")
                print(f"    IR Pipeline: class={ir_has_class}, MonoBehaviour={ir_has_mono}")
                
                # Count Unity-specific patterns
                patterns = ["Update()", "Start()", "OnCollision", "OnTrigger", "Rigidbody", "AddForce"]
                print(f"\n  Unity Patterns Found:")
                for pattern in patterns:
                    in_oneshot = pattern in oneshot_code
                    in_ir = pattern in ir_code
                    if in_oneshot or in_ir:
                        oneshot_mark = "✓" if in_oneshot else "✗"
                        ir_mark = "✓" if in_ir else "✗"
                        print(f"    {pattern:20s}  Oneshot: {oneshot_mark}  IR: {ir_mark}")
        
        print(f"\n{'='*70}")
        print("Compare complete.")
        print(f"{'='*70}")
        
        return results


def interactive_mode():
    """Interactive mode for testing the pipeline"""
    
    print("="*60)
    print("UNITY PIPELINE V2 - Interactive Mode")
    print("="*60)
    print("Generate Unity C# scripts from natural language.")
    print("Commands:")
    print("  quit      - Exit")
    print("  verbose   - Toggle verbose output")
    print("  quiet     - Disable verbose output")
    print("  ir        - Show IR only")
    print("  code      - Show code only")
    print("  both      - Show both IR and code (default)")
    print("  fast      - Enable fast mode (skip IR corrections)")
    print("  slow      - Disable fast mode (enable IR corrections)")
    print("  compare   - Enter compare mode (oneshot vs IR)")
    print("  normal    - Exit compare mode")
    print("="*60)
    
    pipeline = UnityPipelineV2(verbose=False)
    show_ir = True
    show_code = True
    compare_mode = False
    
    while True:
        try:
            user_input = input("\n> ").strip()
            
            if not user_input:
                continue
            
            # Handle commands
            if user_input.lower() == "quit":
                print("Exiting.")
                break
            
            elif user_input.lower() == "verbose":
                pipeline.verbose = True
                pipeline.ir_generator.verbose = True
                pipeline.assembler.verbose = True
                print("Verbose mode ON")
                continue
            
            elif user_input.lower() == "quiet":
                pipeline.verbose = False
                pipeline.ir_generator.verbose = False
                pipeline.assembler.verbose = False
                print("Verbose mode OFF")
                continue
            
            elif user_input.lower() == "ir":
                show_ir = True
                show_code = False
                print("Mode: Show IR only")
                continue
            
            elif user_input.lower() == "code":
                show_ir = False
                show_code = True
                print("Mode: Show code only")
                continue
            
            elif user_input.lower() == "both":
                show_ir = True
                show_code = True
                print("Mode: Show both IR and code")
                continue
            
            elif user_input.lower() == "fast":
                pipeline.fast_mode = True
                pipeline.max_corrections = 0
                pipeline.ir_generator.use_steering = False
                print("Fast mode ON (IR corrections disabled)")
                continue
            
            elif user_input.lower() == "slow":
                pipeline.fast_mode = False
                pipeline.max_corrections = 3
                pipeline.ir_generator.use_steering = True
                print("Fast mode OFF (IR corrections enabled)")
                continue
            
            elif user_input.lower() == "compare":
                compare_mode = True
                print("Compare mode ON - will show oneshot vs IR pipeline")
                continue
            
            elif user_input.lower() == "normal":
                compare_mode = False
                print("Compare mode OFF - normal IR pipeline mode")
                continue
            
            # Generate script
            if compare_mode:
                # Compare mode: show both oneshot and IR pipeline output
                results = pipeline.compare(user_input)
                if results["oneshot"]["success"] and results["ir_pipeline"]["success"]:
                    print(f"\n✓ Compare complete")
                else:
                    print(f"\n⚠ Compare completed with errors")
            else:
                # Normal mode: just IR pipeline
                ir_json, code, success = pipeline.generate(
                    user_input, 
                    show_ir=show_ir, 
                    show_code=show_code
                )
                
                if success:
                    print(f"\n✓ Pipeline complete")
                else:
                    print(f"\n✗ Pipeline failed")
        
        except KeyboardInterrupt:
            print("\nExiting.")
            break
        except Exception as e:
            print(f"Error: {e}")
            if pipeline.verbose:
                import traceback
                traceback.print_exc()


def main():
    """Main entry point"""
    # Parse args
    args = sys.argv[1:]
    
    if not args or args[0] in ["--help", "-h"]:
        print("Unity Pipeline V2 - Natural Language to Unity C# Generator")
        print()
        print("Usage:")
        print("  python unity_full_pipeline_v2.py --interactive     Interactive mode")
        print("  python unity_full_pipeline_v2.py --compare <prompt>  Compare oneshot vs IR")
        print("  python unity_full_pipeline_v2.py <prompt>            Generate with IR pipeline")
        print()
        print("Interactive Commands:")
        print("  compare  - Enter compare mode")
        print("  normal   - Exit compare mode")
        print("  verbose  - Enable verbose output")
        print("  quiet    - Disable verbose output")
        print("  fast     - Skip IR corrections")
        print("  slow     - Enable IR corrections")
        print("  quit     - Exit")
        return
    
    if args[0] in ["--interactive", "-i"]:
        interactive_mode()
    
    elif args[0] in ["--compare", "-c"]:
        # Compare mode from command line
        if len(args) < 2:
            print("Error: --compare requires a prompt")
            print("Usage: python unity_full_pipeline_v2.py --compare \"your prompt here\"")
            return
        
        prompt = " ".join(args[1:])
        pipeline = UnityPipelineV2(verbose=False)
        pipeline.compare(prompt)
    
    elif args[0].startswith("-"):
        print(f"Unknown option: {args[0]}")
        print("Use --help for usage information")
    
    else:
        # Single prompt with IR pipeline
        prompt = " ".join(args)
        
        print("Unity Pipeline V2 - IR Mode")
        print(f"Prompt: {prompt}")
        
        pipeline = UnityPipelineV2(verbose=True)
        ir_json, code, success = pipeline.generate(prompt)
        
        if success:
            print("\n✓ Pipeline complete!")
        else:
            print("\n✗ Pipeline failed")


if __name__ == "__main__":
    main()

