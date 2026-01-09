#!/usr/bin/env python3
"""
End-to-End Validation Test
==========================
Tests the complete pipeline:
1. Generate code from a prompt using the full pipeline
2. Validate the generated code against Unity API ground truth
3. Report hallucinations found
4. Optionally attempt to fix and re-validate

Usage:
    # Test single prompt
    python test_validation_pipeline.py "disco ball with lights"
    
    # Test with verbose output
    python test_validation_pipeline.py "particle fountain" --verbose
    
    # Test multiple prompts from file
    python test_validation_pipeline.py --batch crazy_test_prompts.txt --limit 5
    
    # Interactive mode
    python test_validation_pipeline.py --interactive
    
    # Test and attempt fix
    python test_validation_pipeline.py "enemy AI" --fix
"""

import json
import argparse
import sys
from pathlib import Path
from typing import Optional, List, Dict
from dataclasses import dataclass, field
import time

# Add parent directory to path to import from code generation pipeline
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))

# Import pipeline
from code_generation_pipeline.unity_full_pipeline_rag import UnityPipelineRAG, PipelineResult

# Import validators
from code_generation_pipeline.unity_api_validator import UnityAPIValidator
from code_generation_pipeline.unity_api_validator_v2 import UnityAPIValidatorV2, ValidationIssue

# Import fixer (optional)
try:
    from code_generation_pipeline.unity_script_fixer import UnityScriptFixer
    HAS_FIXER = True
except ImportError:
    HAS_FIXER = False


# ============================================================================
# TEST RESULT
# ============================================================================

@dataclass
class ValidationTestResult:
    """Result of a validation test"""
    prompt: str
    success: bool
    
    # Generation
    generated_code: Optional[str] = None
    generation_time: float = 0.0
    ir_json: Optional[Dict] = None
    
    # Validation (pattern-based)
    pattern_issues: List = field(default_factory=list)
    pattern_issue_count: int = 0
    
    # Validation (RAG-verified)
    rag_issues: List = field(default_factory=list)
    rag_issue_count: int = 0
    confirmed_hallucinations: int = 0
    
    # Fix attempt
    fix_attempted: bool = False
    fixed_code: Optional[str] = None
    issues_after_fix: int = 0
    
    error: Optional[str] = None
    
    def summary(self) -> str:
        if not self.success:
            return f"❌ FAILED: {self.error}"
        
        lines = [
            f"✓ Generated {len(self.generated_code)} chars in {self.generation_time:.1f}s",
            f"  Pattern validator: {self.pattern_issue_count} issues",
            f"  RAG validator: {self.rag_issue_count} issues ({self.confirmed_hallucinations} confirmed)",
        ]
        
        if self.fix_attempted:
            lines.append(f"  After fix: {self.issues_after_fix} issues remaining")
        
        return "\n".join(lines)


# ============================================================================
# TEST RUNNER
# ============================================================================

class ValidationTestRunner:
    """Runs validation tests on generated code"""
    
    def __init__(self, verbose: bool = False, use_steering: bool = True):
        self.verbose = verbose
        
        print("Initializing test runner...")
        
        # Initialize pipeline
        self.pipeline = UnityPipelineRAG(
            use_steering=use_steering,
            verbose=verbose,
            steer_code=True
        )
        
        # Initialize validators
        print("Loading validators...")
        self.pattern_validator = UnityAPIValidator(verbose=False)
        self.rag_validator = UnityAPIValidatorV2(verbose=verbose)
        
        # Initialize fixer (optional)
        self.fixer = None
        if HAS_FIXER:
            try:
                self.fixer = UnityScriptFixer(verbose=verbose)
                print("Fixer loaded")
            except Exception as e:
                print(f"Fixer not available: {e}")
        
        print("Test runner ready!\n")
    
    def test_prompt(
        self, 
        prompt: str, 
        attempt_fix: bool = False
    ) -> ValidationTestResult:
        """Test a single prompt through the full pipeline"""
        
        result = ValidationTestResult(prompt=prompt, success=False)
        
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"TESTING: {prompt[:60]}...")
            print('='*60)
        
        # ================================================================
        # STEP 1: Generate code
        # ================================================================
        if self.verbose:
            print("\n[1/3] Generating code...")
        
        start_time = time.time()
        
        try:
            pipeline_result = self.pipeline.generate(prompt)
        except Exception as e:
            result.error = f"Pipeline error: {e}"
            return result
        
        result.generation_time = time.time() - start_time
        
        if not pipeline_result.success or not pipeline_result.csharp_code:
            result.error = f"Generation failed: {pipeline_result.error}"
            return result
        
        result.generated_code = pipeline_result.csharp_code
        result.ir_json = pipeline_result.ir_json
        
        if self.verbose:
            print(f"  Generated {len(result.generated_code)} chars in {result.generation_time:.1f}s")
            if pipeline_result.was_steered:
                print("  (Code was steered during generation)")
        
        # ================================================================
        # STEP 2: Validate with pattern-based validator
        # ================================================================
        if self.verbose:
            print("\n[2/3] Pattern-based validation...")
        
        try:
            pattern_issues = self.pattern_validator.validate_code(result.generated_code)
            result.pattern_issues = [str(i) for i in pattern_issues]
            result.pattern_issue_count = len(pattern_issues)
            
            if self.verbose:
                if pattern_issues:
                    print(f"  Found {len(pattern_issues)} issues:")
                    for issue in pattern_issues[:5]:
                        print(f"    - {issue}")
                    if len(pattern_issues) > 5:
                        print(f"    ... and {len(pattern_issues) - 5} more")
                else:
                    print("  No issues found")
        except Exception as e:
            if self.verbose:
                print(f"  Pattern validation error: {e}")
        
        # ================================================================
        # STEP 3: Validate with RAG-verified validator
        # ================================================================
        if self.verbose:
            print("\n[3/3] RAG-verified validation...")
        
        try:
            rag_issues = self.rag_validator.validate_code(result.generated_code)
            result.rag_issues = [str(i) for i in rag_issues]
            result.rag_issue_count = len(rag_issues)
            result.confirmed_hallucinations = sum(1 for i in rag_issues if i.verified_invalid)
            
            if self.verbose:
                if rag_issues:
                    print(f"  Found {len(rag_issues)} issues ({result.confirmed_hallucinations} confirmed):")
                    for issue in rag_issues[:5]:
                        status = "✓ CONFIRMED" if issue.verified_invalid else "? PATTERN"
                        print(f"    [{status}] {issue.invalid_api}")
                        if issue.nearest_valid_api:
                            print(f"      → Nearest: {issue.nearest_valid_api} ({issue.similarity_score:.0%})")
                    if len(rag_issues) > 5:
                        print(f"    ... and {len(rag_issues) - 5} more")
                else:
                    print("  No issues found - code is clean!")
        except Exception as e:
            if self.verbose:
                print(f"  RAG validation error: {e}")
        
        # ================================================================
        # STEP 4: Attempt fix (optional)
        # ================================================================
        if attempt_fix and self.fixer and (result.pattern_issue_count > 0 or result.rag_issue_count > 0):
            if self.verbose:
                print("\n[4/4] Attempting fix...")
            
            result.fix_attempted = True
            
            try:
                # Build error string from issues
                errors = "\n".join(result.pattern_issues[:10] + result.rag_issues[:10])
                
                fix_result = self.fixer.fix(result.generated_code, errors)
                
                if fix_result.success and fix_result.fixed_code:
                    result.fixed_code = fix_result.fixed_code
                    
                    # Re-validate
                    fixed_issues = self.rag_validator.validate_code(result.fixed_code)
                    result.issues_after_fix = len(fixed_issues)
                    
                    if self.verbose:
                        improvement = result.rag_issue_count - result.issues_after_fix
                        print(f"  Fix result: {result.issues_after_fix} issues remaining (improved by {improvement})")
                        
            except Exception as e:
                if self.verbose:
                    print(f"  Fix error: {e}")
        
        result.success = True
        return result
    
    def test_batch(
        self, 
        prompts: List[str], 
        attempt_fix: bool = False
    ) -> List[ValidationTestResult]:
        """Test multiple prompts"""
        
        results = []
        
        for i, prompt in enumerate(prompts):
            print(f"\n[{i+1}/{len(prompts)}] {prompt[:50]}...")
            result = self.test_prompt(prompt, attempt_fix)
            results.append(result)
            
            # Brief summary
            if result.success:
                print(f"  → {result.rag_issue_count} issues ({result.confirmed_hallucinations} confirmed)")
            else:
                print(f"  → FAILED: {result.error}")
        
        return results
    
    def print_summary(self, results: List[ValidationTestResult]):
        """Print summary of all test results"""
        
        print("\n" + "=" * 60)
        print("TEST SUMMARY")
        print("=" * 60)
        
        total = len(results)
        successful = sum(1 for r in results if r.success)
        total_pattern_issues = sum(r.pattern_issue_count for r in results if r.success)
        total_rag_issues = sum(r.rag_issue_count for r in results if r.success)
        total_confirmed = sum(r.confirmed_hallucinations for r in results if r.success)
        
        print(f"\nTests: {successful}/{total} successful")
        print(f"Pattern validator: {total_pattern_issues} total issues")
        print(f"RAG validator: {total_rag_issues} total issues ({total_confirmed} confirmed hallucinations)")
        
        if successful > 0:
            avg_pattern = total_pattern_issues / successful
            avg_rag = total_rag_issues / successful
            avg_confirmed = total_confirmed / successful
            
            print(f"\nPer-generation averages:")
            print(f"  Pattern issues: {avg_pattern:.1f}")
            print(f"  RAG issues: {avg_rag:.1f}")
            print(f"  Confirmed hallucinations: {avg_confirmed:.1f}")
        
        # Most common issues
        all_issues = []
        for r in results:
            if r.success:
                all_issues.extend(r.rag_issues)
        
        if all_issues:
            # Count by issue type (extract from string representation)
            from collections import Counter
            issue_types = Counter()
            for issue_str in all_issues:
                # Extract the API name
                if " - " in issue_str:
                    api = issue_str.split(" - ")[1].split(" ->")[0]
                    issue_types[api] += 1
            
            print(f"\nMost common hallucinations:")
            for api, count in issue_types.most_common(10):
                print(f"  {count}x {api}")
        
        # Results with most issues
        problematic = sorted(
            [r for r in results if r.success],
            key=lambda r: r.confirmed_hallucinations,
            reverse=True
        )[:5]
        
        if problematic and problematic[0].confirmed_hallucinations > 0:
            print(f"\nMost problematic prompts:")
            for r in problematic:
                if r.confirmed_hallucinations > 0:
                    print(f"  {r.confirmed_hallucinations} hallucinations: {r.prompt[:50]}...")


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

def interactive_mode(runner: ValidationTestRunner):
    """Interactive testing session"""
    
    print("=" * 60)
    print("VALIDATION TEST - Interactive Mode")
    print("=" * 60)
    print("Enter prompts to test. Commands: quit, fix, verbose, quiet")
    print("=" * 60)
    
    attempt_fix = False
    
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
        elif cmd == "fix":
            attempt_fix = not attempt_fix
            print(f"Auto-fix: {'ON' if attempt_fix else 'OFF'}")
            continue
        elif cmd == "verbose":
            runner.verbose = True
            print("Verbose ON")
            continue
        elif cmd == "quiet":
            runner.verbose = False
            print("Verbose OFF")
            continue
        
        result = runner.test_prompt(prompt, attempt_fix=attempt_fix)
        
        print(f"\n{result.summary()}")
        
        if result.success and result.generated_code:
            print(f"\n--- Generated Code ({len(result.generated_code)} chars) ---")
            # Show first 50 lines
            lines = result.generated_code.split('\n')[:50]
            print('\n'.join(lines))
            if len(result.generated_code.split('\n')) > 50:
                print(f"... ({len(result.generated_code.split(chr(10))) - 50} more lines)")


# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Test the validation pipeline end-to-end"
    )
    parser.add_argument("prompt", nargs="?", help="Single prompt to test")
    parser.add_argument("--batch", "-b", help="File with prompts (one per line)")
    parser.add_argument("--limit", "-l", type=int, help="Limit number of prompts")
    parser.add_argument("--interactive", "-i", action="store_true")
    parser.add_argument("--fix", "-f", action="store_true", help="Attempt to fix issues")
    parser.add_argument("--verbose", "-v", action="store_true")
    parser.add_argument("--no-steering", action="store_true", help="Disable pipeline steering")
    parser.add_argument("--output", "-o", help="Save results to JSON file")
    
    args = parser.parse_args()
    
    # Initialize runner
    runner = ValidationTestRunner(
        verbose=args.verbose,
        use_steering=not args.no_steering
    )
    
    results = []
    
    if args.interactive:
        interactive_mode(runner)
    elif args.batch:
        prompts = load_prompts_from_file(args.batch, args.limit)
        print(f"Loaded {len(prompts)} prompts from {args.batch}")
        results = runner.test_batch(prompts, attempt_fix=args.fix)
        runner.print_summary(results)
    elif args.prompt:
        result = runner.test_prompt(args.prompt, attempt_fix=args.fix)
        print(f"\n{result.summary()}")
        results = [result]
        
        if result.success and args.verbose and result.generated_code:
            print(f"\n--- Generated Code ---")
            print(result.generated_code)
    else:
        # Default: test with a sample prompt
        sample_prompt = "disco ball that rotates and projects colored light beams with particle effects"
        print(f"Testing with sample prompt: {sample_prompt}")
        result = runner.test_prompt(sample_prompt, attempt_fix=args.fix)
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
                "pattern_issues": r.pattern_issue_count,
                "rag_issues": r.rag_issue_count,
                "confirmed_hallucinations": r.confirmed_hallucinations,
                "issues_after_fix": r.issues_after_fix if r.fix_attempted else None,
                "error": r.error,
            })
        
        with open(args.output, 'w', encoding='utf-8') as f:
            json.dump(output_data, f, indent=2)
        print(f"\nResults saved to {args.output}")


if __name__ == "__main__":
    main()

