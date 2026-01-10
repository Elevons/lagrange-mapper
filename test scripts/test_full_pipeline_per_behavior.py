#!/usr/bin/env python3
"""
Test Full Pipeline with Per-Behavior RAG
==========================================
Tests the full pipeline (UnityPipelineRAG) with per-behavior RAG lookup mode.
Grades results using LLM evaluation (no comparison with other approaches).

Usage:
    # Test single prompt
    python test_full_pipeline_per_behavior.py "disco ball with lights"
    
    python test_full_pipeline_per_behavior.py "particle fountain" --verbose
    
    # Test multiple prompts from file
    python test_full_pipeline_per_behavior.py --batch crazy_test_prompts.txt --limit 5
    
    # Interactive mode
    python test_full_pipeline_per_behavior.py --interactive
    
    # Disable steering
    python test_full_pipeline_per_behavior.py "enemy AI" --no-steering
"""

import json
import argparse
import sys
import os
import time
import requests
from pathlib import Path
from typing import Optional, List, Dict
from dataclasses import dataclass, field
from datetime import datetime
from dotenv import load_dotenv

# Add parent directory to path to import from code generation pipeline
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'code generation pipeline'))

# Import pipeline
from unity_full_pipeline_rag import UnityPipelineRAG, PipelineResult, RAG_MODE_PER_BEHAVIOR

# Load environment variables
load_dotenv()

# ============================================================================
# CONFIGURATION
# ============================================================================

ANTHROPIC_API_KEY = os.getenv("ANTHROPIC_API_KEY", "")
ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages"
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Output directory for results
RESULTS_DIR = os.path.join(os.path.dirname(__file__), "prompt_test_results")
os.makedirs(RESULTS_DIR, exist_ok=True)


# ============================================================================
# TEST RESULT
# ============================================================================

@dataclass
class TestResult:
    """Result of a test run"""
    prompt: str
    success: bool
    
    # Generation
    generated_code: Optional[str] = None
    ir_json: Optional[Dict] = None
    generation_time: float = 0.0
    rag_docs_used: int = 0
    was_steered: bool = False
    
    # Grading
    grade: Optional[Dict] = None
    grade_time: float = 0.0
    
    error: Optional[str] = None
    
    def summary(self) -> str:
        if not self.success:
            return f"❌ FAILED: {self.error}"
        
        lines = [
            f"✓ Generated {len(self.generated_code)} chars in {self.generation_time:.1f}s",
            f"  RAG docs: {self.rag_docs_used}",
            f"  Steered: {'Yes' if self.was_steered else 'No'}",
        ]
        
        if self.grade:
            score = self.grade.get('overall_score', 0)
            lines.append(f"  Grade: {score}/10")
        
        return "\n".join(lines)


# ============================================================================
# GRADING
# ============================================================================

def grade_with_claude(prompt: str, code: str, ir_json: Optional[Dict] = None) -> Optional[Dict]:
    """
    Grade the generated code using Claude Haiku.
    
    Returns a dict with scores and feedback, or None if grading fails.
    """
    if not ANTHROPIC_API_KEY:
        print("[WARN] ANTHROPIC_API_KEY not set, skipping Claude grading")
        return None
    
    grading_prompt = f"""You are evaluating Unity C# code generated from a natural language description.

USER REQUEST:
{prompt}

IR SPECIFICATION (intermediate representation):
{json.dumps(ir_json, indent=2) if ir_json else "Not available"}

GENERATED CODE:
```csharp
{code[:4000] if code else "[FAILED TO GENERATE]"}
```

Evaluate this code on the following criteria:

1. **Correctness** (0-10): Does the code correctly implement the user's request?
   - Does it match the IR specification?
   - Are the behaviors implemented correctly?
   - Are the Unity APIs used correctly?

2. **Code Quality** (0-10): Is the code well-structured and maintainable?
   - Proper Unity lifecycle methods?
   - Good variable naming?
   - Appropriate use of components?
   - TODO comments for complex sections?

3. **Completeness** (0-10): Does the code implement all required features?
   - All behaviors from IR?
   - All fields and components?
   - Edge cases handled (or marked with TODOs)?

4. **Unity Best Practices** (0-10): Does it follow Unity conventions?
   - Proper component usage?
   - Correct physics handling?
   - Appropriate use of SerializeField?
   - Safe null checks?

Return a JSON object with:
{{
  "overall_score": <0-10 average of all scores>,
  "correctness": <0-10>,
  "code_quality": <0-10>,
  "completeness": <0-10>,
  "unity_best_practices": <0-10>,
  "strengths": ["strength1", "strength2", ...],
  "weaknesses": ["weakness1", "weakness2", ...],
  "suggestions": ["suggestion1", "suggestion2", ...]
}}"""
    
    try:
        response = requests.post(
            ANTHROPIC_API_URL,
            headers={
                "x-api-key": ANTHROPIC_API_KEY,
                "anthropic-version": "2023-06-01",
                "content-type": "application/json"
            },
            json={
                "model": CLAUDE_MODEL,
                "max_tokens": 2000,
                "messages": [
                    {
                        "role": "user",
                        "content": grading_prompt
                    }
                ]
            },
            timeout=60
        )
        
        if response.status_code != 200:
            print(f"[ERROR] Claude API error: {response.status_code}")
            try:
                print(f"  Detail: {response.json()}")
            except:
                pass
            return None
        
        content = response.json()["content"][0]["text"]
        
        # Extract JSON from response
        import re
        json_match = re.search(r'\{[\s\S]*\}', content)
        if json_match:
            grade_json = json.loads(json_match.group(0))
            return grade_json
        else:
            print(f"[WARN] Could not parse grade JSON from Claude response")
            return None
            
    except Exception as e:
        print(f"[ERROR] Claude grading failed: {e}")
        return None


# ============================================================================
# TEST RUNNER
# ============================================================================

class FullPipelineTester:
    """Tests the full pipeline with per-behavior RAG"""
    
    def __init__(self, verbose: bool = False, use_steering: bool = True):
        self.verbose = verbose
        
        print("Initializing test runner...")
        
        # Initialize pipeline with per-behavior mode
        self.pipeline = UnityPipelineRAG(
            use_steering=use_steering,
            verbose=verbose,
            rag_mode=RAG_MODE_PER_BEHAVIOR  # Use per-behavior RAG lookup
        )
        
        print("Test runner ready!\n")
    
    def test_prompt(self, prompt: str, grade: bool = True) -> TestResult:
        """Test a single prompt through the full pipeline"""
        
        result = TestResult(prompt=prompt, success=False)
        
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"TESTING: {prompt[:60]}...")
            print('='*60)
        
        # ================================================================
        # STEP 1: Generate code
        # ================================================================
        if self.verbose:
            print("\n[1/2] Generating code with per-behavior RAG...")
        
        start_time = time.time()
        
        try:
            pipeline_result = self.pipeline.generate(prompt)
        except Exception as e:
            result.error = f"Pipeline error: {e}"
            if self.verbose:
                print(f"  ERROR: {result.error}")
            return result
        
        result.generation_time = time.time() - start_time
        
        if not pipeline_result.success or not pipeline_result.csharp_code:
            result.error = f"Generation failed: {pipeline_result.error}"
            if self.verbose:
                print(f"  ERROR: {result.error}")
            return result
        
        result.generated_code = pipeline_result.csharp_code
        result.ir_json = pipeline_result.ir_json
        result.rag_docs_used = pipeline_result.rag_docs_used
        result.was_steered = pipeline_result.ir_result.was_steered if pipeline_result.ir_result else False
        
        if self.verbose:
            print(f"  Generated {len(result.generated_code)} chars in {result.generation_time:.1f}s")
            print(f"  RAG docs used: {result.rag_docs_used}")
            if result.was_steered:
                print("  (Code was steered during generation)")
        
        # ================================================================
        # STEP 2: Grade with Claude
        # ================================================================
        if grade:
            if self.verbose:
                print("\n[2/2] Grading with Claude...")
            
            start_time = time.time()
            result.grade = grade_with_claude(prompt, result.generated_code, result.ir_json)
            result.grade_time = time.time() - start_time
            
            if result.grade:
                if self.verbose:
                    score = result.grade.get('overall_score', 0)
                    print(f"  Grade: {score}/10")
                    print(f"  Correctness: {result.grade.get('correctness', 0)}/10")
                    print(f"  Code Quality: {result.grade.get('code_quality', 0)}/10")
                    print(f"  Completeness: {result.grade.get('completeness', 0)}/10")
                    print(f"  Unity Best Practices: {result.grade.get('unity_best_practices', 0)}/10")
            else:
                if self.verbose:
                    print("  Grading failed or skipped")
        
        result.success = True
        return result
    
    def test_batch(self, prompts: List[str], grade: bool = True) -> List[TestResult]:
        """Test multiple prompts"""
        
        results = []
        
        for i, prompt in enumerate(prompts):
            print(f"\n[{i+1}/{len(prompts)}] {prompt[:50]}...")
            result = self.test_prompt(prompt, grade=grade)
            results.append(result)
            
            # Brief summary
            if result.success:
                if result.grade:
                    score = result.grade.get('overall_score', 0)
                    print(f"  → {score}/10 ({result.rag_docs_used} docs)")
                else:
                    print(f"  → Generated ({result.rag_docs_used} docs)")
            else:
                print(f"  → FAILED: {result.error}")
        
        return results
    
    def print_summary(self, results: List[TestResult]):
        """Print summary of all test results"""
        
        print("\n" + "=" * 60)
        print("TEST SUMMARY")
        print("=" * 60)
        
        total = len(results)
        successful = sum(1 for r in results if r.success)
        total_docs = sum(r.rag_docs_used for r in results if r.success)
        graded = sum(1 for r in results if r.success and r.grade)
        
        print(f"\nTests: {successful}/{total} successful")
        print(f"Graded: {graded}/{successful}")
        
        if successful > 0:
            avg_docs = total_docs / successful
            print(f"Average RAG docs per generation: {avg_docs:.1f}")
        
        if graded > 0:
            scores = [r.grade.get('overall_score', 0) for r in results if r.success and r.grade]
            avg_score = sum(scores) / len(scores)
            print(f"Average grade: {avg_score:.1f}/10")
            
            # Score distribution
            excellent = sum(1 for s in scores if s >= 8)
            good = sum(1 for s in scores if 6 <= s < 8)
            fair = sum(1 for s in scores if 4 <= s < 6)
            poor = sum(1 for s in scores if s < 4)
            
            print(f"\nScore distribution:")
            print(f"  Excellent (8-10): {excellent}")
            print(f"  Good (6-8): {good}")
            print(f"  Fair (4-6): {fair}")
            print(f"  Poor (0-4): {poor}")
        
        # Best and worst
        if graded > 0:
            graded_results = [r for r in results if r.success and r.grade]
            best = max(graded_results, key=lambda r: r.grade.get('overall_score', 0))
            worst = min(graded_results, key=lambda r: r.grade.get('overall_score', 0))
            
            print(f"\nBest: {best.grade.get('overall_score', 0)}/10 - {best.prompt[:50]}...")
            print(f"Worst: {worst.grade.get('overall_score', 0)}/10 - {worst.prompt[:50]}...")


# ============================================================================
# LOAD PROMPTS
# ============================================================================

def load_prompts_from_file(filepath: str, limit: Optional[int] = None) -> List[str]:
    """Load prompts from a file (one per line, # for comments)"""
    prompts = []
    
    with open(filepath, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                prompts.append(line)
    
    if limit:
        prompts = prompts[:limit]
    
    return prompts


# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(tester: FullPipelineTester):
    """Interactive testing session"""
    
    print("=" * 60)
    print("FULL PIPELINE TEST - Interactive Mode")
    print("=" * 60)
    print("Enter prompts to test. Commands: quit, verbose, quiet, grade, no-grade")
    print("=" * 60)
    
    grade = True
    
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
            tester.verbose = True
            print("Verbose ON")
            continue
        elif cmd == "quiet":
            tester.verbose = False
            print("Verbose OFF")
            continue
        elif cmd == "grade":
            grade = True
            print("Grading ON")
            continue
        elif cmd == "no-grade":
            grade = False
            print("Grading OFF")
            continue
        
        result = tester.test_prompt(prompt, grade=grade)
        
        print(f"\n{result.summary()}")
        
        if result.success and result.generated_code and tester.verbose:
            print(f"\n--- Generated Code ({len(result.generated_code)} chars) ---")
            # Show first 50 lines
            lines = result.generated_code.split('\n')[:50]
            print('\n'.join(lines))
            if len(result.generated_code.split('\n')) > 50:
                print(f"... ({len(result.generated_code.split('\n')) - 50} more lines)")


# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Test the full pipeline with per-behavior RAG and grade results"
    )
    parser.add_argument("prompt", nargs="?", help="Single prompt to test")
    parser.add_argument("--batch", "-b", help="File with prompts (one per line)")
    parser.add_argument("--limit", "-l", type=int, help="Limit number of prompts")
    parser.add_argument("--interactive", "-i", action="store_true")
    parser.add_argument("--verbose", "-v", action="store_true")
    parser.add_argument("--no-steering", action="store_true", help="Disable pipeline steering")
    parser.add_argument("--no-grade", action="store_true", help="Skip LLM grading")
    parser.add_argument("--output", "-o", help="Save results to JSON file")
    
    args = parser.parse_args()
    
    # Initialize tester
    tester = FullPipelineTester(
        verbose=args.verbose,
        use_steering=not args.no_steering
    )
    
    results = []
    
    if args.interactive:
        interactive_mode(tester)
    elif args.batch:
        prompts = load_prompts_from_file(args.batch, args.limit)
        print(f"Loaded {len(prompts)} prompts from {args.batch}")
        results = tester.test_batch(prompts, grade=not args.no_grade)
        tester.print_summary(results)
    elif args.prompt:
        result = tester.test_prompt(args.prompt, grade=not args.no_grade)
        print(f"\n{result.summary()}")
        results = [result]
        
        if result.success and args.verbose and result.generated_code:
            print(f"\n--- Generated Code ---")
            print(result.generated_code)
    else:
        # Default: test with a sample prompt
        sample_prompt = "disco ball that rotates and projects colored light beams with particle effects"
        print(f"Testing with sample prompt: {sample_prompt}")
        result = tester.test_prompt(sample_prompt, grade=not args.no_grade)
        print(f"\n{result.summary()}")
        results = [result]
    
    # Save results
    if args.output and results:
        output_data = []
        for r in results:
            output_data.append({
                "prompt": r.prompt,
                "success": r.success,
                "generation_time": r.generation_time,
                "rag_docs_used": r.rag_docs_used,
                "was_steered": r.was_steered,
                "generated_code": r.generated_code,
                "ir_json": r.ir_json,
                "grade": r.grade,
                "grade_time": r.grade_time,
                "error": r.error,
            })
        
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, indent=2)
        print(f"\nResults saved to {args.output}")
    elif results:
        # Auto-save to timestamped file
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        output_file = os.path.join(RESULTS_DIR, f"full_pipeline_per_behavior_{timestamp}.json")
        
        output_data = []
        for r in results:
            output_data.append({
                "prompt": r.prompt,
                "success": r.success,
                "generation_time": r.generation_time,
                "rag_docs_used": r.rag_docs_used,
                "was_steered": r.was_steered,
                "generated_code": r.generated_code,
                "ir_json": r.ir_json,
                "grade": r.grade,
                "grade_time": r.grade_time,
                "error": r.error,
            })
        
        with open(output_file, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, indent=2)
        print(f"\nResults auto-saved to {output_file}")


if __name__ == "__main__":
    main()

