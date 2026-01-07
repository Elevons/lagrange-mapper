#!/usr/bin/env python3
"""
Unity Full Pipeline
===================
End-to-end Unity script generation:
  User Description → IR JSON (steered) → C# Script (via RAG)

Combines:
- unity_ir_inference.py: Natural language to IR JSON (with attractor steering)
- unity_script_assembler.py: IR JSON to C# script (via WaveformRAG)

Usage:
    python unity_full_pipeline.py --interactive
    python unity_full_pipeline.py "player that collects coins and gains score"
"""

import json
import argparse
from typing import Optional
from dataclasses import dataclass

from unity_ir_inference import UnityIRGenerator, GenerationResult
from unity_script_assembler import UnityScriptAssembler, AssemblyResult

# ============================================================================
# PIPELINE RESULT
# ============================================================================

@dataclass
class PipelineResult:
    """Complete pipeline result"""
    success: bool
    description: str
    
    # Stage 1: IR Generation
    ir_result: Optional[GenerationResult] = None
    ir_json: Optional[dict] = None
    
    # Stage 2: Script Assembly
    assembly_result: Optional[AssemblyResult] = None
    csharp_code: Optional[str] = None
    
    error: Optional[str] = None
    
    def __str__(self):
        if not self.success:
            return f"PipelineResult(success=False, error={self.error})"
        
        ir_status = "steered" if self.ir_result and self.ir_result.was_steered else "clean"
        code_lines = len(self.csharp_code.split('\n')) if self.csharp_code else 0
        
        return f"PipelineResult(success=True, ir={ir_status}, code_lines={code_lines})"

# ============================================================================
# PIPELINE CLASS
# ============================================================================

class UnityPipeline:
    """
    Full Unity script generation pipeline.
    
    Stage 1: Natural language → IR JSON (with attractor steering)
    Stage 2: IR JSON → C# script (via WaveformRAG with two-stage retrieval)
    
    Two-stage retrieval:
    1. Coarse: Detect domain from IR (ui, physics, audio, etc.)
    2. Fine: Search only within that domain's tools (~500 vs 25,000)
    """
    
    def __init__(
        self,
        use_steering: bool = True,
        intensity: float = 0.5,
        verbose: bool = False,
        skip_voting: bool = False,
        use_domain_filter: bool = True  # Enable two-stage domain filtering
    ):
        self.verbose = verbose
        self.skip_voting = skip_voting
        self.use_domain_filter = use_domain_filter
        
        # Initialize IR generator
        if verbose:
            print("Initializing IR Generator...")
        self.ir_generator = UnityIRGenerator(
            use_steering=use_steering,
            intensity=intensity,
            verbose=verbose
        )
        
        # Initialize script assembler (loads WaveformRAG)
        if verbose:
            print("Initializing Script Assembler...")
        self.assembler = UnityScriptAssembler(
            verbose=verbose, 
            skip_voting=skip_voting,
            use_domain_filter=use_domain_filter
        )
        
        if verbose:
            mode_str = "domain-filtered" if use_domain_filter else "full search"
            print(f"Pipeline ready! (RAG mode: {mode_str})\n")
    
    def generate(self, description: str) -> PipelineResult:
        """
        Generate a complete Unity C# script from a natural language description.
        
        Args:
            description: Natural language behavior description
            
        Returns:
            PipelineResult with IR JSON and C# code
        """
        result = PipelineResult(success=False, description=description)
        
        # Stage 1: Generate IR JSON
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 1: Natural Language → IR JSON")
            print('='*60)
        
        ir_result = self.ir_generator.generate(description)
        result.ir_result = ir_result
        
        if not ir_result.success:
            result.error = f"IR generation failed: {ir_result.error}"
            return result
        
        result.ir_json = ir_result.parsed
        
        if self.verbose:
            print(f"\nIR JSON generated ({ir_result.attempts} attempts)")
            if ir_result.was_steered:
                print(f"  Steering applied: {ir_result.initial_detection.triggered_attractors if ir_result.initial_detection else []}")
        
        # Stage 2: Assemble C# script
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 2: IR JSON → C# Script")
            print('='*60)
        
        assembly_result = self.assembler.assemble(result.ir_json)
        result.assembly_result = assembly_result
        
        if not assembly_result.success:
            result.error = f"Script assembly failed: {assembly_result.error}"
            return result
        
        result.csharp_code = assembly_result.code
        result.success = True
        
        if self.verbose:
            print(f"\nC# script generated: {assembly_result.class_name}")
            print(f"  Fields: {len(assembly_result.fields)}")
            print(f"  Behaviors: {len(assembly_result.behaviors)}")
            print(f"  Imports: {', '.join(assembly_result.imports)}")
        
        return result

# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(pipeline: UnityPipeline):
    """Interactive generation session"""
    print("="*60)
    print("UNITY FULL PIPELINE - Interactive Mode")
    print("="*60)
    print("Generate complete Unity C# scripts from natural language.")
    print("Commands:")
    print("  quit, verbose, quiet")
    print("  ir (show IR only), code (show code only), both")
    print("  novote/vote - toggle MAKER voting")
    print("  filter/nofilter - toggle two-stage domain filtering")
    print("="*60)
    
    show_ir = True
    show_code = True
    
    while True:
        try:
            prompt = input("\n> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nExiting.")
            break
        
        if not prompt:
            continue
        
        cmd = prompt.lower()
        if cmd == "quit":
            break
        elif cmd == "verbose":
            pipeline.verbose = True
            pipeline.ir_generator.verbose = True
            pipeline.assembler.verbose = True
            print("Verbose mode ON")
            continue
        elif cmd == "quiet":
            pipeline.verbose = False
            pipeline.ir_generator.verbose = False
            pipeline.assembler.verbose = False
            print("Verbose mode OFF")
            continue
        elif cmd == "ir":
            show_ir = True
            show_code = False
            print("Showing IR JSON only")
            continue
        elif cmd == "code":
            show_ir = False
            show_code = True
            print("Showing C# code only")
            continue
        elif cmd == "both":
            show_ir = True
            show_code = True
            print("Showing both IR and code")
            continue
        elif cmd == "novote":
            # Recreate assembler without voting (preserve domain filter setting)
            pipeline.assembler = UnityScriptAssembler(
                verbose=pipeline.verbose,
                skip_voting=True,
                use_domain_filter=pipeline.use_domain_filter
            )
            pipeline.skip_voting = True
            print("MAKER voting DISABLED - using raw RAG scores")
            continue
        elif cmd == "vote":
            # Recreate assembler with voting (preserve domain filter setting)
            pipeline.assembler = UnityScriptAssembler(
                verbose=pipeline.verbose,
                skip_voting=False,
                use_domain_filter=pipeline.use_domain_filter
            )
            pipeline.skip_voting = False
            print("MAKER voting ENABLED")
            continue
        elif cmd == "filter":
            # Enable two-stage domain filtering
            pipeline.use_domain_filter = True
            pipeline.assembler = UnityScriptAssembler(
                verbose=pipeline.verbose,
                skip_voting=pipeline.skip_voting,
                use_domain_filter=True
            )
            print("Two-stage domain filtering ENABLED")
            continue
        elif cmd == "nofilter":
            # Disable domain filtering (full search)
            pipeline.use_domain_filter = False
            pipeline.assembler = UnityScriptAssembler(
                verbose=pipeline.verbose,
                skip_voting=pipeline.skip_voting,
                use_domain_filter=False
            )
            print("Domain filtering DISABLED - searching all tools")
            continue
        elif cmd == "status":
            # Show current settings
            voting_status = "OFF (raw RAG)" if pipeline.skip_voting else "ON"
            filter_status = "ON (two-stage)" if pipeline.use_domain_filter else "OFF (full search)"
            verbose_status = "ON" if pipeline.verbose else "OFF"
            print(f"  Voting: {voting_status}")
            print(f"  Domain filter: {filter_status}")
            print(f"  Verbose: {verbose_status}")
            continue
        
        # Generate
        result = pipeline.generate(prompt)
        
        print(f"\n{result}")
        
        if result.success:
            if show_ir and result.ir_json:
                print(f"\n{'─'*40}")
                print("IR JSON:")
                print('─'*40)
                print(json.dumps(result.ir_json, indent=2))
            
            if show_code and result.csharp_code:
                print(f"\n{'─'*40}")
                print("C# Script:")
                print('─'*40)
                print(result.csharp_code)
        else:
            print(f"\nError: {result.error}")

# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Unity Full Pipeline: Natural Language → IR JSON → C# Script"
    )
    parser.add_argument("description", nargs="*", help="Behavior description")
    parser.add_argument("-i", "--interactive", action="store_true")
    parser.add_argument("--no-steering", action="store_true", help="Disable attractor steering")
    parser.add_argument("--intensity", type=float, default=0.5, help="Steering intensity 0-1")
    parser.add_argument("-v", "--verbose", action="store_true")
    
    args = parser.parse_args()
    
    pipeline = UnityPipeline(
        use_steering=not args.no_steering,
        intensity=args.intensity,
        verbose=args.verbose
    )
    
    if args.interactive or not args.description:
        interactive_mode(pipeline)
    else:
        description = " ".join(args.description)
        result = pipeline.generate(description)
        
        if result.success:
            print("\n" + "="*60)
            print("Generated C# Script:")
            print("="*60)
            print(result.csharp_code)
        else:
            print(f"Error: {result.error}")


if __name__ == "__main__":
    main()

