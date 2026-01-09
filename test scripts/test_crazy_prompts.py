"""
Quick script to test crazy prompts from crazy_test_prompts.txt
Tests all prompts and grades oneshot vs IR (monolithic) vs IR (per-behavior) using Claude Haiku
"""

import sys
import os
import json
import time
import requests
from datetime import datetime
from typing import Dict, List, Optional
from pathlib import Path
from dotenv import load_dotenv

# Add parent directory to path to import from code generation pipeline
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from code_generation_pipeline.unity_pipeline_simple import UnityPipelineSimple
from code_generation_pipeline.unity_pipeline_per_behavior import UnityPipelinePerBehavior

# Load environment variables from .env file
load_dotenv()

# ============================================================================
# CONFIGURATION - API KEY LOADED FROM .env FILE
# ============================================================================
ANTHROPIC_API_KEY = os.getenv("ANTHROPIC_API_KEY", "")
ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages"
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Output directory for results
RESULTS_DIR = os.path.join(os.path.dirname(__file__), "prompt_test_results")

def load_prompts(filename="crazy_test_prompts.txt"):
    """Load prompts from the text file"""
    prompts = []
    current_prompt = []
    in_prompt = False
    
    with open(filename, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            
            # Skip comments and empty lines
            if not line or line.startswith('#'):
                continue
            
            # Check if this is a numbered prompt
            if line and (line[0].isdigit() or line.startswith('"')):
                if current_prompt:
                    prompts.append(' '.join(current_prompt))
                    current_prompt = []
                
                # Remove leading number and period if present
                if line[0].isdigit():
                    parts = line.split('.', 1)
                    if len(parts) > 1:
                        line = parts[1].strip()
                
                # Remove quotes if present
                if line.startswith('"') and line.endswith('"'):
                    line = line[1:-1]
                
                current_prompt.append(line)
                in_prompt = True
            elif in_prompt:
                # Continuation of current prompt
                if line.startswith('"') and line.endswith('"'):
                    line = line[1:-1]
                current_prompt.append(line)
    
    # Add last prompt
    if current_prompt:
        prompts.append(' '.join(current_prompt))
    
    return prompts

def grade_with_claude(prompt: str, oneshot_code: Optional[str], ir_code: Optional[str], ir_json: Optional[Dict], 
                       per_behavior_code: Optional[str] = None, per_behavior_docs: int = 0) -> Optional[Dict]:
    """
    Send all code versions to Claude Haiku for grading.
    
    Returns a dict with scores and feedback, or None if grading fails.
    """
    if not ANTHROPIC_API_KEY:
        print("[WARN]  Warning: ANTHROPIC_API_KEY not set, skipping Claude grading")
        return None
    
    # Build grading prompt based on which approaches we have
    has_per_behavior = per_behavior_code is not None
    
    grading_prompt = f"""You are evaluating {"three" if has_per_behavior else "two"} Unity C# code generation approaches for the same user request.

USER REQUEST:
{prompt}

APPROACH 1 - ONESHOT (Direct NL -> C#, no RAG):
```csharp
{oneshot_code[:3500] if oneshot_code else "[FAILED TO GENERATE]"}
```

APPROACH 2 - IR MONOLITHIC (NL -> IR -> RAG (8 docs) -> C#):
Intermediate Representation (IR):
{json.dumps(ir_json, indent=2)[:1000] if ir_json else "[NO IR]"}

Generated C# Code:
```csharp
{ir_code[:3500] if ir_code else "[FAILED TO GENERATE]"}
```"""

    if has_per_behavior:
        grading_prompt += f"""

APPROACH 3 - IR PER-BEHAVIOR (NL -> IR -> RAG per behavior ({per_behavior_docs} docs total) -> C#):
This approach retrieves separate documentation for each behavior block, then generates methods individually.
```csharp
{per_behavior_code[:3500] if per_behavior_code else "[FAILED TO GENERATE]"}
```"""

    grading_prompt += f"""

EVALUATION CRITERIA (weighted by importance):

1. Architecture & Structure (MOST IMPORTANT - 3x weight):
   - Is the code organized with clear separation of concerns?
   - Are there proper methods/functions for distinct behaviors?
   - Could a developer easily understand and extend this code?
   - Is it modular rather than monolithic?

2. Fixability & Maintainability (2x weight):
   - If something is wrong, how easy would it be to fix?
   - Are there clear extension points for missing features?
   - Is the code defensive without being over-engineered?
   - Would TODO comments or stub methods be easy to fill in?

3. Unity Patterns (1.5x weight):
   - Does it use appropriate Unity lifecycle methods?
   - Are serialized fields used correctly for inspector configuration?
   - Does it follow Unity component patterns?

4. Correctness (1x weight):
   - Does the implemented portion work correctly?
   - Note: Partial but correct is BETTER than complete but broken

5. Completeness (0.5x weight - LEAST IMPORTANT):
   - How much of the request is addressed?
   - Note: DO NOT reward code that tries everything but is messy
   - Clean incomplete code beats messy complete code

PHILOSOPHY: We want code that a junior developer could pick up, understand, and finish implementing. Spaghetti code that "works" is worse than clean code with TODOs.

For each approach, provide:
- A score from 1-10 for each criterion (before weighting)
- The weighted total will be: (architecture*3) + (fixability*2) + (unity_patterns*1.5) + (correctness*1) + (completeness*0.5)
- Max possible weighted score: 80

Format your response as JSON:
{{
  "oneshot": {{
    "architecture": <score>,
    "fixability": <score>,
    "unity_patterns": <score>,
    "correctness": <score>,
    "completeness": <score>,
    "weighted_total": <calculated>,
    "justification": "<brief explanation>"
  }},
  "ir_monolithic": {{
    "architecture": <score>,
    "fixability": <score>,
    "unity_patterns": <score>,
    "correctness": <score>,
    "completeness": <score>,
    "weighted_total": <calculated>,
    "justification": "<brief explanation>"
  }},"""
    
    if has_per_behavior:
        grading_prompt += """
  "ir_per_behavior": {
    "architecture": <score>,
    "fixability": <score>,
    "unity_patterns": <score>,
    "correctness": <score>,
    "completeness": <score>,
    "weighted_total": <calculated>,
    "justification": "<brief explanation>"
  },
  "winner": "oneshot" | "ir_monolithic" | "ir_per_behavior","""
    else:
        grading_prompt += """
  "winner": "oneshot" | "ir_monolithic","""
    
    grading_prompt += """
  "key_differences": "<explanation of main differences>",
  "advantages": "<what makes the winner better>"
}"""

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
            print(f"[WARN]  Claude API error: {response.status_code} - {response.text}")
            return None
        
        content = response.json()["content"][0]["text"]
        
        # Try to extract JSON from response
        try:
            # Look for JSON in the response
            import re
            json_match = re.search(r'\{.*\}', content, re.DOTALL)
            if json_match:
                grade_data = json.loads(json_match.group())
                return grade_data
            else:
                # Fallback: return raw content
                return {"raw_response": content}
        except json.JSONDecodeError:
            return {"raw_response": content}
            
    except Exception as e:
        print(f"[WARN]  Error calling Claude API: {e}")
        return None

def test_prompt(prompt_num: int, prompt: str, verbose: bool = True, compare: bool = False, grade: bool = False):
    """Test a single prompt with all three approaches"""
    print(f"\n{'='*80}")
    print(f"TESTING PROMPT #{prompt_num}")
    print(f"{'='*80}")
    print(f"\n{prompt}\n")
    print(f"{'='*80}\n")
    
    pipeline = UnityPipelineSimple(verbose=verbose)
    per_behavior_pipeline = UnityPipelinePerBehavior(verbose=verbose)
    
    result_data = {
        "prompt_num": prompt_num,
        "prompt": prompt,
        "timestamp": datetime.now().isoformat(),
        "oneshot_code": None,
        "ir_json": None,
        "ir_code": None,
        "ir_steered": False,
        "ir_rag_docs": 0,
        "ir_rag_doc_names": None,
        "per_behavior_code": None,
        "per_behavior_docs": 0,
        "per_behavior_methods": 0,
        "per_behavior_doc_names": None,
        "grade": None,
        "error": None
    }
    
    if compare or grade:
        # Generate oneshot and IR monolithic versions
        result = pipeline.compare(prompt)
        result_data["oneshot_code"] = result.oneshot_code
        result_data["ir_json"] = result.ir_json
        result_data["ir_code"] = result.ir_code
        result_data["ir_steered"] = result.ir_steered
        result_data["ir_rag_docs"] = result.ir_rag_docs
        result_data["ir_rag_doc_names"] = result.ir_rag_doc_names
        
        # Generate per-behavior version
        print(f"\n{'-'*80}")
        print("GENERATING PER-BEHAVIOR VERSION...")
        print(f"{'-'*80}")
        try:
            pb_result = per_behavior_pipeline.generate(prompt)
            if pb_result.success:
                result_data["per_behavior_code"] = pb_result.code
                result_data["per_behavior_docs"] = pb_result.total_docs_retrieved
                result_data["per_behavior_methods"] = pb_result.behaviors_generated
                result_data["per_behavior_doc_names"] = pb_result.rag_doc_names_by_behavior
                print(f"   Per-behavior: {pb_result.total_docs_retrieved} docs, {pb_result.behaviors_generated} methods")
            else:
                print(f"   Per-behavior: FAILED - {pb_result.error}")
        except Exception as e:
            print(f"   Per-behavior: ERROR - {e}")
        
        # Grade with Claude if requested
        if grade and result.oneshot_code and result.ir_code:
            print(f"\n{'-'*80}")
            print("GRADING WITH CLAUDE HAIKU...")
            print(f"{'-'*80}")
            grade_result = grade_with_claude(
                prompt, 
                result.oneshot_code, 
                result.ir_code, 
                result.ir_json,
                result_data.get("per_behavior_code"),
                result_data.get("per_behavior_docs", 0)
            )
            result_data["grade"] = grade_result
            
            if grade_result and "winner" in grade_result:
                print(f"\n🏆 WINNER: {grade_result['winner'].upper()}")
                
                # Show scores for all approaches
                for approach in ["oneshot", "ir_monolithic", "ir_per_behavior"]:
                    if approach in grade_result and isinstance(grade_result[approach], dict):
                        total = grade_result[approach].get("weighted_total", 0)
                        arch = grade_result[approach].get("architecture", "?")
                        fix = grade_result[approach].get("fixability", "?")
                        print(f"   {approach}: {total}/80 (arch={arch}, fix={fix})")
                
                if "key_differences" in grade_result:
                    print(f"\n📊 Key Differences:")
                    print(f"   {grade_result['key_differences']}")
    else:
        # Just generate IR version
        result = pipeline.generate(prompt, steer=True)
        
        if result.success:
            result_data["ir_json"] = result.ir_json
            result_data["ir_code"] = result.code
            result_data["ir_steered"] = result.was_steered
            result_data["ir_rag_docs"] = result.rag_docs_used
            
            if verbose:
                print(f"\n{'-'*80}")
                print("IR JSON:")
                print(f"{'-'*80}")
                print(json.dumps(result.ir_json, indent=2))
                
                print(f"\n{'-'*80}")
                print(f"C# CODE: (steered={result.was_steered}, rag_docs={result.rag_docs_used})")
                print(f"{'-'*80}")
                print(result.code)
        else:
            result_data["error"] = result.error
            print(f"[X] ERROR: {result.error}")
    
    return result_data

def save_results(results: List[Dict], filename: Optional[str] = None):
    """Save test results to JSON file"""
    Path(RESULTS_DIR).mkdir(exist_ok=True)
    
    if filename is None:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        filename = f"test_results_{timestamp}.json"
    
    filepath = Path(RESULTS_DIR) / filename
    
    with open(filepath, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False)
    
    print(f"\n[SAVED] Results saved to: {filepath}")
    return filepath

def print_summary(results: List[Dict]):
    """Print summary statistics for all three approaches"""
    print(f"\n{'='*80}")
    print("SUMMARY STATISTICS")
    print(f"{'='*80}")
    
    total = len(results)
    successful = sum(1 for r in results if r.get("ir_code") or r.get("oneshot_code"))
    per_behavior_success = sum(1 for r in results if r.get("per_behavior_code"))
    graded = sum(1 for r in results if r.get("grade") and "winner" in r["grade"])
    
    print(f"Total Prompts: {total}")
    print(f"Successful (oneshot/mono): {successful}")
    print(f"Successful (per-behavior): {per_behavior_success}")
    print(f"Graded: {graded}")
    
    if graded > 0:
        # Safely get winner - handle None grade values
        def get_winner(r):
            grade = r.get("grade")
            if grade is None:
                return None
            return grade.get("winner")
        
        oneshot_wins = sum(1 for r in results if get_winner(r) == "oneshot")
        ir_mono_wins = sum(1 for r in results if get_winner(r) in ["ir", "ir_monolithic"])
        ir_pb_wins = sum(1 for r in results if get_winner(r) == "ir_per_behavior")
        ties = sum(1 for r in results if get_winner(r) == "tie")
        
        print(f"\n🏆 Winners:")
        print(f"   Oneshot:           {oneshot_wins}")
        print(f"   IR Monolithic:     {ir_mono_wins}")
        print(f"   IR Per-Behavior:   {ir_pb_wins}")
        print(f"   Ties:              {ties}")
        
        # Helper to extract scores for an approach
        def get_scores(approach_key, field):
            scores = []
            for r in results:
                grade = r.get("grade")
                if grade is None:
                    continue
                # Handle both old "ir" and new "ir_monolithic" keys
                if approach_key == "ir_monolithic" and "ir" in grade and approach_key not in grade:
                    approach = grade.get("ir")
                else:
                    approach = grade.get(approach_key)
                # Ensure approach is a dict before accessing fields
                if isinstance(approach, dict) and field in approach:
                    scores.append(approach[field])
            return scores
        
        # Calculate average scores for all approaches
        approaches = [
            ("Oneshot", "oneshot"),
            ("IR Monolithic", "ir_monolithic"),
            ("IR Per-Behavior", "ir_per_behavior")
        ]
        
        print(f"\n📊 Average Weighted Scores (max 80):")
        for name, key in approaches:
            scores = get_scores(key, "weighted_total")
            if scores:
                print(f"   {name:18}: {sum(scores)/len(scores):.1f}/80")
        
        print(f"\n🏗️  Average Architecture Scores (3x weight):")
        for name, key in approaches:
            scores = get_scores(key, "architecture")
            if scores:
                print(f"   {name:18}: {sum(scores)/len(scores):.1f}/10")
        
        print(f"\n🔧 Average Fixability Scores (2x weight):")
        for name, key in approaches:
            scores = get_scores(key, "fixability")
            if scores:
                print(f"   {name:18}: {sum(scores)/len(scores):.1f}/10")
        
        # RAG docs comparison
        mono_docs = [r.get("ir_rag_docs", 0) for r in results if r.get("ir_rag_docs")]
        pb_docs = [r.get("per_behavior_docs", 0) for r in results if r.get("per_behavior_docs")]
        
        if mono_docs or pb_docs:
            print(f"\n📚 Average RAG Docs Retrieved:")
            if mono_docs:
                print(f"   IR Monolithic:     {sum(mono_docs)/len(mono_docs):.1f} docs")
            if pb_docs:
                print(f"   IR Per-Behavior:   {sum(pb_docs)/len(pb_docs):.1f} docs")
                if mono_docs:
                    ratio = (sum(pb_docs)/len(pb_docs)) / (sum(mono_docs)/len(mono_docs))
                    print(f"   Coverage Increase: {ratio:.1f}x")
    
    # Count errors
    errors = [r for r in results if r.get("error")]
    if errors:
        print(f"\n[X] Errors: {len(errors)}")
        for r in errors[:5]:  # Show first 5
            print(f"   Prompt {r['prompt_num']}: {r['error']}")

def main():
    """Main function"""
    if len(sys.argv) < 2:
        print("Usage:")
        print("  python test_crazy_prompts.py <prompt_number>")
        print("  python test_crazy_prompts.py <prompt_number> --compare")
        print("  python test_crazy_prompts.py <prompt_number> --grade")
        print("  python test_crazy_prompts.py --list")
        print("  python test_crazy_prompts.py --all [--grade] [--compare]")
        print("\nOptions:")
        print("  --grade    : Grade oneshot vs IR with Claude Haiku (requires API key)")
        print("  --compare  : Generate both oneshot and IR versions")
        print("  --verbose  : Show detailed output")
        print("\nExamples:")
        print("  python test_crazy_prompts.py 1")
        print("  python test_crazy_prompts.py 5 --compare --grade")
        print("  python test_crazy_prompts.py --all --grade")
        print("\n[WARN]  Note: Set ANTHROPIC_API_KEY in .env file for grading")
        return
    
    # Check API key if grading
    if "--grade" in sys.argv and not ANTHROPIC_API_KEY:
        print("[WARN]  Warning: ANTHROPIC_API_KEY not set!")
        print("   Create a .env file with: ANTHROPIC_API_KEY=sk-ant-...")
        print("   Continuing without grading...\n")
    
    try:
        prompts = load_prompts()
        print(f"Loaded {len(prompts)} prompts from crazy_test_prompts.txt\n")
    except FileNotFoundError:
        print("[X] Error: crazy_test_prompts.txt not found!")
        return
    except Exception as e:
        print(f"[X] Error loading prompts: {e}")
        return
    
    if sys.argv[1] == "--list":
        print("Available prompts:")
        for i, prompt in enumerate(prompts, 1):
            preview = prompt[:60] + "..." if len(prompt) > 60 else prompt
            print(f"  {i:2d}. {preview}")
        return
    
    if sys.argv[1] == "--all":
        compare = "--compare" in sys.argv or "--grade" in sys.argv
        grade = "--grade" in sys.argv
        verbose = "--verbose" in sys.argv or "-v" in sys.argv
        
        print(f"Testing ALL {len(prompts)} prompts...")
        if grade:
            print("[WARN]  Grading enabled - this will make API calls to Claude Haiku")
        print("Press Ctrl+C to stop\n")
        
        results = []
        start_time = time.time()
        
        for i, prompt in enumerate(prompts, 1):
            try:
                print(f"\n[{i}/{len(prompts)}] Processing prompt {i}...")
                result_data = test_prompt(i, prompt, verbose=verbose, compare=compare, grade=grade)
                results.append(result_data)
                
                # Save incrementally after each prompt
                save_results(results, "test_results_in_progress.json")
                print(f"[SAVED] Progress saved ({i}/{len(prompts)} prompts completed)")
                
                # Small delay to avoid rate limits
                if grade and i < len(prompts):
                    time.sleep(1)
                    
            except KeyboardInterrupt:
                print("\n\n⏸️  Stopped by user.")
                # Save partial results before exiting
                if results:
                    partial_filename = f"test_results_partial_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
                    save_results(results, partial_filename)
                    print(f"[SAVED] Partial results saved to: {partial_filename}")
                break
            except Exception as e:
                print(f"\n[X] Error testing prompt {i}: {e}\n")
                results.append({
                    "prompt_num": i,
                    "prompt": prompt,
                    "error": str(e),
                    "timestamp": datetime.now().isoformat()
                })
                # Save even on error
                save_results(results, "test_results_in_progress.json")
                continue
        
        elapsed = time.time() - start_time
        print(f"\n{'='*80}")
        print(f"[OK] Completed {len(results)} prompts in {elapsed:.1f} seconds")
        print(f"{'='*80}")
        
        # Save final results with timestamp
        final_file = save_results(results)
        
        # Keep in_progress file as backup, but also create final timestamped version
        print(f"[SAVED] Final results saved (in_progress file also available as backup)")
        
        # Print summary
        print_summary(results)
        
        return
    
    # Test specific prompt number
    try:
        prompt_num = int(sys.argv[1])
        if prompt_num < 1 or prompt_num > len(prompts):
            print(f"[X] Error: Prompt number must be between 1 and {len(prompts)}")
            return
        
        compare = "--compare" in sys.argv or "--grade" in sys.argv
        grade = "--grade" in sys.argv
        verbose = "--verbose" in sys.argv or "-v" in sys.argv
        
        prompt = prompts[prompt_num - 1]
        result_data = test_prompt(prompt_num, prompt, verbose=verbose, compare=compare, grade=grade)
        
        # Save single result
        save_results([result_data], f"prompt_{prompt_num}_result.json")
        
    except ValueError:
        print(f"[X] Error: '{sys.argv[1]}' is not a valid prompt number")
        print("Use --list to see available prompts")
    except Exception as e:
        print(f"[X] Error: {e}")

if __name__ == "__main__":
    main()

