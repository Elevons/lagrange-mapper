"""
Unity Full Pipeline V2 - Simplified Direct LLM Architecture
Natural Language → IR JSON → C# Code (no RAG!)
"""
import sys
import json
from unity_ir_inference import UnityIRGenerator
from unity_script_assembler_v2 import UnityScriptAssemblerV2
from unity_code_validator import UnityAPIValidator


class UnityPipelineV2:
    """End-to-end pipeline: Natural Language → IR JSON → C# Code"""
    
    def __init__(self, verbose: bool = False, validate: bool = True):
        self.verbose = verbose
        self.validate = validate
        
        # Stage 1: IR Generator
        self.ir_generator = UnityIRGenerator(
            intensity=0.5,
            use_steering=True,
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
        ir_result = self.ir_generator.generate(prompt)
        
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


def interactive_mode():
    """Interactive mode for testing the pipeline"""
    
    print("="*60)
    print("UNITY PIPELINE V2 - Interactive Mode")
    print("="*60)
    print("Generate Unity C# scripts from natural language.")
    print("Commands:")
    print("  quit - Exit")
    print("  verbose - Toggle verbose output")
    print("  quiet - Disable verbose output")
    print("  ir - Show IR only")
    print("  code - Show code only")
    print("  both - Show both IR and code (default)")
    print("="*60)
    
    pipeline = UnityPipelineV2(verbose=False)
    show_ir = True
    show_code = True
    
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
            
            # Generate script
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
    if len(sys.argv) > 1 and sys.argv[1] in ["--interactive", "-i"]:
        interactive_mode()
    else:
        # Single test
        test_prompt = "create a script that pushes an object forward continuously using physics"
        
        print("Testing Unity Pipeline V2")
        print(f"Prompt: {test_prompt}")
        
        pipeline = UnityPipelineV2(verbose=True)
        ir_json, code, success = pipeline.generate(test_prompt)
        
        if success:
            print("\n✓ Test passed!")
        else:
            print("\n✗ Test failed")


if __name__ == "__main__":
    main()

