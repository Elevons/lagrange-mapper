#!/usr/bin/env python3
"""
Unity Full Pipeline with RAG
============================
End-to-end Unity script generation:
  User Description -> IR JSON (with code leak steering) -> C# Script (via current RAG)

Combines:
- unity_ir_inference.py: Natural language to IR JSON (with attractor steering)
- unity_rag_query.py: RAG-based documentation retrieval
- LLM code generation with RAG context

Usage:
    python unity_full_pipeline_rag.py --interactive
    python unity_full_pipeline_rag.py "player that collects coins and gains score"
    python unity_full_pipeline_rag.py --no-steering "enemy AI"
"""

import json
import argparse
import requests
import re
from typing import Optional, Dict, List, Tuple
from dataclasses import dataclass, field

# IR Generator with code leak steering
from unity_ir_inference import UnityIRGenerator, GenerationResult

# RAG system
from unity_rag_query import UnityRAG, RetrievalResult

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
RAG_DB_PATH = "unity_rag_db"
DEFAULT_TEMPERATURE = 0.4

# ============================================================================
# PROMPTS
# ============================================================================

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert the behavior specification into a complete MonoBehaviour script.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, etc.)
2. Use correct Unity APIs from the documentation provided
3. Declare all fields as public or [SerializeField]
4. Add required using statements
5. Make the code production-ready and compilable
6. SELF-CONTAINED: Do NOT reference classes that aren't defined in this script or Unity's standard API
7. If you need a helper class, DEFINE IT in the same file

Output ONLY the C# code. No markdown, no explanations."""

STEERING_PROMPT_TEMPLATE = """The following C# code has INVALID Unity APIs that need to be fixed.

INVALID APIS DETECTED (must be fixed):
{invalid_apis}

UNITY DOCUMENTATION FOR REFERENCE:
{rag_context}

ORIGINAL SPECIFICATION:
{ir_json}

CODE TO FIX:
```csharp
{code}
```

Fix EACH invalid API listed above. Use the suggested alternatives or find the correct Unity API.
Output ONLY the corrected C# code, no explanations:"""

# ============================================================================
# PIPELINE RESULT
# ============================================================================

@dataclass
class PipelineResult:
    """Complete pipeline result"""
    success: bool
    description: str
    
    # Stage 1: IR Generation (with steering)
    ir_result: Optional[GenerationResult] = None
    ir_json: Optional[dict] = None
    
    # Stage 2: Code Generation
    csharp_code: Optional[str] = None
    was_steered: bool = False
    rag_docs_used: int = 0
    
    error: Optional[str] = None
    
    def __str__(self):
        if not self.success:
            return f"PipelineResult(success=False, error={self.error})"
        
        ir_status = "steered" if self.ir_result and self.ir_result.was_steered else "clean"
        code_lines = len(self.csharp_code.split('\n')) if self.csharp_code else 0
        
        return f"PipelineResult(success=True, ir={ir_status}, code_steered={self.was_steered}, rag_docs={self.rag_docs_used}, lines={code_lines})"

# ============================================================================
# PIPELINE CLASS
# ============================================================================

class UnityPipelineRAG:
    """
    Full Unity script generation pipeline with RAG.
    
    Stage 1: Natural language -> IR JSON (with code leak steering)
    Stage 2: IR JSON -> RAG retrieval -> C# code generation
    """
    
    def __init__(
        self,
        use_steering: bool = True,
        intensity: float = 0.5,
        verbose: bool = False,
        steer_code: bool = True,  # Enable API validation/steering for code
    ):
        self.verbose = verbose
        self.steer_code = steer_code
        self.llm_url = LLM_URL
        
        # Initialize IR generator with code leak steering
        if verbose:
            print("Initializing IR Generator (with code leak steering)...")
        self.ir_generator = UnityIRGenerator(
            use_steering=use_steering,
            intensity=intensity,
            verbose=verbose
        )
        
        # Initialize RAG
        if verbose:
            print("Loading RAG database...")
        self.rag = UnityRAG(db_path=RAG_DB_PATH, verbose=verbose)
        
        if verbose:
            print(f"Pipeline ready! RAG: {len(self.rag.documents)} docs\n")
    
    def generate(self, description: str) -> PipelineResult:
        """Generate complete Unity C# script from natural language description."""
        result = PipelineResult(success=False, description=description)
        
        # ===== STAGE 1: Generate IR JSON (with code leak steering) =====
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 1: Natural Language -> IR JSON (with steering)")
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
                triggered = ir_result.initial_detection.triggered_attractors if ir_result.initial_detection else []
                print(f"  Code leak steering applied: {triggered}")
            print(f"  Class: {result.ir_json.get('class_name', 'unknown')}")
            print(f"  Components: {result.ir_json.get('components', [])}")
            print(f"  Behaviors: {len(result.ir_json.get('behaviors', []))}")
        
        # ===== STAGE 2: RAG Retrieval =====
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 2: RAG Retrieval")
            print('='*60)
        
        rag_result = self.rag.retrieve_for_ir(result.ir_json, include_content=True)
        rag_context = self.rag.format_context_for_prompt(rag_result.documents) if rag_result else ""
        result.rag_docs_used = len(rag_result.documents) if rag_result else 0
        
        if self.verbose:
            if rag_result and rag_result.documents:
                print(f"  Retrieved {len(rag_result.documents)} docs from {len(rag_result.selected_namespaces)} namespaces")
            else:
                print("  No RAG context retrieved")
        
        # ===== STAGE 3: Generate C# Code =====
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 3: Generate C# Code")
            print('='*60)
        
        code = self._generate_code(result.ir_json, rag_context)
        
        if not code:
            result.error = "Failed to generate C# code"
            return result
        
        if self.verbose:
            print(f"  Generated {len(code)} chars")
        
        # ===== STAGE 4: API Validation & Steering (optional) =====
        if self.steer_code:
            if self.verbose:
                print(f"\n{'='*60}")
                print("STAGE 4: API Validation & Steering")
                print('='*60)
            
            code, was_steered = self._validate_and_steer(code, result.ir_json, rag_context)
            result.was_steered = was_steered
            
            if self.verbose:
                if was_steered:
                    print("  Code was steered to fix invalid APIs")
                else:
                    print("  All APIs validated [OK]")
        
        result.csharp_code = code
        result.success = True
        
        return result
    
    def _generate_code(self, ir_json: Dict, rag_context: str) -> Optional[str]:
        """Generate C# code from IR JSON with RAG context."""
        try:
            user_content = ""
            if rag_context:
                user_content += f"UNITY API DOCUMENTATION:\n{rag_context}\n\n"
            
            user_content += f"BEHAVIOR SPECIFICATION:\n{json.dumps(ir_json, indent=2)}\n\n"
            user_content += "Generate the complete Unity C# MonoBehaviour script:"
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": CODE_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"  Code generation error: {e}")
            return None
    
    def _validate_and_steer(self, code: str, ir_json: Dict, rag_context: str) -> Tuple[str, bool]:
        """Validate APIs and steer if needed."""
        # Extract APIs from generated code
        apis_found = self.rag.extract_apis_from_code(code)
        
        if self.verbose:
            print(f"  Extracted APIs from code: {apis_found[:10]}...")
        
        # Validate each API
        suspicious = []
        for api in apis_found:
            is_valid, suggestions = self.rag.validate_api(api, threshold=0.7)
            if not is_valid:
                nearest = suggestions[0] if suggestions else None
                suspicious.append({
                    'api': api,
                    'nearest': nearest[0] if nearest else None,
                    'score': nearest[1] if nearest else 0
                })
        
        if not suspicious:
            return code, False
        
        if self.verbose:
            print(f"  Found {len(suspicious)} suspicious APIs:")
            for s in suspicious[:5]:
                if s.get('nearest'):
                    print(f"    [!] {s['api']} -> {s['nearest']} ({s['score']:.2f})")
                else:
                    print(f"    [!] {s['api']} -> not found")
        
        # Get additional RAG context for steering
        steering_result = self.rag.retrieve_for_code_steering(code, threshold=0.5)
        steering_context = self.rag.format_context_for_prompt(steering_result.documents) if steering_result else rag_context
        
        # Build steering prompt
        invalid_apis_text = ""
        for s in suspicious:
            if s.get('nearest'):
                invalid_apis_text += f"- {s['api']} -> Try using: {s['nearest']}\n"
            else:
                invalid_apis_text += f"- {s['api']} -> Does not exist in Unity, remove or replace\n"
        
        steering_prompt = STEERING_PROMPT_TEMPLATE.format(
            invalid_apis=invalid_apis_text,
            rag_context=steering_context[:3000],
            ir_json=json.dumps(ir_json, indent=2),
            code=code
        )
        
        try:
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": "You fix Unity C# code by replacing invalid APIs with correct ones."},
                        {"role": "user", "content": steering_prompt}
                    ],
                    "temperature": 0.2,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code == 200:
                steered = response.json()["choices"][0]["message"]["content"]
                return self._clean_code(steered), True
                
        except Exception as e:
            if self.verbose:
                print(f"  Steering error: {e}")
        
        return code, False
    
    def _clean_code(self, code: str) -> str:
        """Clean C# code from markdown formatting."""
        code = code.strip()
        if code.startswith("```"):
            lines = code.split("\n")
            if lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip() == "```":
                lines = lines[:-1]
            code = "\n".join(lines)
        return code.strip()

# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(pipeline: UnityPipelineRAG):
    """Interactive generation session"""
    print("="*60)
    print("UNITY FULL PIPELINE (RAG) - Interactive Mode")
    print("="*60)
    print("Generate complete Unity C# scripts from natural language.")
    print("With code leak steering + RAG-based code generation")
    print("Commands: quit, verbose, quiet, ir, code, both")
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
            print("Verbose mode ON")
            continue
        elif cmd == "quiet":
            pipeline.verbose = False
            pipeline.ir_generator.verbose = False
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
        
        # Generate
        result = pipeline.generate(prompt)
        
        print(f"\n{result}")
        
        if result.success:
            if show_ir and result.ir_json:
                print(f"\n{'-'*40}")
                print("IR JSON:")
                print('-'*40)
                print(json.dumps(result.ir_json, indent=2))
            
            if show_code and result.csharp_code:
                print(f"\n{'-'*40}")
                print("C# Script:")
                print('-'*40)
                print(result.csharp_code)
        else:
            print(f"\nError: {result.error}")

# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Unity Full Pipeline (RAG): NL -> IR (steered) -> C# (RAG)"
    )
    parser.add_argument("description", nargs="*", help="Behavior description")
    parser.add_argument("-i", "--interactive", action="store_true")
    parser.add_argument("--no-steering", action="store_true", help="Disable code leak steering for IR")
    parser.add_argument("--no-code-steer", action="store_true", help="Disable API validation steering for code")
    parser.add_argument("--intensity", type=float, default=0.5, help="Steering intensity 0-1")
    parser.add_argument("-v", "--verbose", action="store_true")
    
    args = parser.parse_args()
    
    pipeline = UnityPipelineRAG(
        use_steering=not args.no_steering,
        intensity=args.intensity,
        verbose=args.verbose,
        steer_code=not args.no_code_steer
    )
    
    if args.interactive or not args.description:
        interactive_mode(pipeline)
    else:
        description = " ".join(args.description)
        result = pipeline.generate(description)
        
        if result.success:
            print("\n" + "="*60)
            print("IR JSON:")
            print("="*60)
            print(json.dumps(result.ir_json, indent=2))
            
            print("\n" + "="*60)
            print("Generated C# Script:")
            print("="*60)
            print(result.csharp_code)
        else:
            print(f"Error: {result.error}")


if __name__ == "__main__":
    main()

