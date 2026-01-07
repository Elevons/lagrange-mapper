"""
Quick script to test crazy prompts from crazy_test_prompts.txt
Tests all prompts and grades oneshot vs IR versions using Claude Haiku
"""

import sys
import json
import time
import requests
from datetime import datetime
from typing import Dict, List, Optional
from pathlib import Path
from unity_pipeline_simple import UnityPipelineSimple

# ============================================================================
# CONFIGURATION - SET YOUR API KEY HERE
# ============================================================================
ANTHROPIC_API_KEY = ""  # Set your Anthropic API key here, e.g. "sk-ant-..."
ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages"
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Output directory for results
RESULTS_DIR = "prompt_test_results"

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

def grade_with_claude(prompt: str, oneshot_code: Optional[str], ir_code: Optional[str], ir_json: Optional[Dict]) -> Optional[Dict]:
    """
    Send both code versions to Claude Haiku for grading.
    
    Returns a dict with scores and feedback, or None if grading fails.
    """
    if not ANTHROPIC_API_KEY:
        print("⚠️  Warning: ANTHROPIC_API_KEY not set, skipping Claude grading")
        return None
    
    grading_prompt = f"""You are evaluating two Unity C# code generation approaches for the same user request.

USER REQUEST:
{prompt}

APPROACH 1 - ONESHOT (Direct NL → C#):
```csharp
{oneshot_code[:4000] if oneshot_code else "[FAILED TO GENERATE]"}
```

APPROACH 2 - IR PIPELINE (NL → IR → C#):
Intermediate Representation (IR):
{json.dumps(ir_json, indent=2) if ir_json else "[NO IR]"}

Generated C# Code:
```csharp
{ir_code[:4000] if ir_code else "[FAILED TO GENERATE]"}
```

EVALUATION CRITERIA:
1. Correctness: Does the code correctly implement the requested behavior?
2. Unity API Usage: Are Unity APIs used correctly and appropriately?
3. Code Quality: Is the code well-structured, readable, and maintainable?
4. Completeness: Does it handle all aspects of the request?
5. Best Practices: Does it follow Unity and C# best practices?

For each approach, provide:
- A score from 1-10 for each criterion
- Brief justification for each score
- Overall winner (oneshot or IR)
- Key differences and advantages of the winning approach

Format your response as JSON:
{{
  "oneshot": {{
    "correctness": <score>,
    "api_usage": <score>,
    "code_quality": <score>,
    "completeness": <score>,
    "best_practices": <score>,
    "total": <sum>,
    "justification": "<brief explanation>"
  }},
  "ir": {{
    "correctness": <score>,
    "api_usage": <score>,
    "code_quality": <score>,
    "completeness": <score>,
    "best_practices": <score>,
    "total": <sum>,
    "justification": "<brief explanation>"
  }},
  "winner": "oneshot" | "ir" | "tie",
  "key_differences": "<explanation of main differences>",
  "advantages": "<what makes the winner better>"
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
            print(f"⚠️  Claude API error: {response.status_code} - {response.text}")
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
        print(f"⚠️  Error calling Claude API: {e}")
        return None

def test_prompt(prompt_num: int, prompt: str, verbose: bool = True, compare: bool = False, grade: bool = False):
    """Test a single prompt"""
    print(f"\n{'='*80}")
    print(f"TESTING PROMPT #{prompt_num}")
    print(f"{'='*80}")
    print(f"\n{prompt}\n")
    print(f"{'='*80}\n")
    
    pipeline = UnityPipelineSimple(verbose=verbose)
    
    result_data = {
        "prompt_num": prompt_num,
        "prompt": prompt,
        "timestamp": datetime.now().isoformat(),
        "oneshot_code": None,
        "ir_json": None,
        "ir_code": None,
        "ir_steered": False,
        "ir_rag_docs": 0,
        "grade": None,
        "error": None
    }
    
    if compare or grade:
        # Generate both versions for comparison
        result = pipeline.compare(prompt)
        result_data["oneshot_code"] = result.oneshot_code
        result_data["ir_json"] = result.ir_json
        result_data["ir_code"] = result.ir_code
        result_data["ir_steered"] = result.ir_steered
        result_data["ir_rag_docs"] = result.ir_rag_docs
        
        # Grade with Claude if requested
        if grade and result.oneshot_code and result.ir_code:
            print(f"\n{'─'*80}")
            print("GRADING WITH CLAUDE HAIKU...")
            print(f"{'─'*80}")
            grade_result = grade_with_claude(prompt, result.oneshot_code, result.ir_code, result.ir_json)
            result_data["grade"] = grade_result
            
            if grade_result and "winner" in grade_result:
                print(f"\n🏆 WINNER: {grade_result['winner'].upper()}")
                if "oneshot" in grade_result and "ir" in grade_result:
                    oneshot_total = grade_result["oneshot"].get("total", 0)
                    ir_total = grade_result["ir"].get("total", 0)
                    print(f"   Oneshot Score: {oneshot_total}/50")
                    print(f"   IR Score: {ir_total}/50")
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
                print(f"\n{'─'*80}")
                print("IR JSON:")
                print(f"{'─'*80}")
                print(json.dumps(result.ir_json, indent=2))
                
                print(f"\n{'─'*80}")
                print(f"C# CODE: (steered={result.was_steered}, rag_docs={result.rag_docs_used})")
                print(f"{'─'*80}")
                print(result.code)
        else:
            result_data["error"] = result.error
            print(f"❌ ERROR: {result.error}")
    
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
    
    print(f"\n💾 Results saved to: {filepath}")
    return filepath

def print_summary(results: List[Dict]):
    """Print summary statistics"""
    print(f"\n{'='*80}")
    print("SUMMARY STATISTICS")
    print(f"{'='*80}")
    
    total = len(results)
    successful = sum(1 for r in results if r.get("ir_code") or r.get("oneshot_code"))
    graded = sum(1 for r in results if r.get("grade") and "winner" in r["grade"])
    
    print(f"Total Prompts: {total}")
    print(f"Successful: {successful}")
    print(f"Graded: {graded}")
    
    if graded > 0:
        oneshot_wins = sum(1 for r in results if r.get("grade", {}).get("winner") == "oneshot")
        ir_wins = sum(1 for r in results if r.get("grade", {}).get("winner") == "ir")
        ties = sum(1 for r in results if r.get("grade", {}).get("winner") == "tie")
        
        print(f"\n🏆 Winners:")
        print(f"   Oneshot: {oneshot_wins}")
        print(f"   IR Pipeline: {ir_wins}")
        print(f"   Ties: {ties}")
        
        # Calculate average scores
        oneshot_scores = [r["grade"]["oneshot"]["total"] for r in results 
                         if r.get("grade") and "oneshot" in r["grade"] and "total" in r["grade"]["oneshot"]]
        ir_scores = [r["grade"]["ir"]["total"] for r in results 
                    if r.get("grade") and "ir" in r["grade"] and "total" in r["grade"]["ir"]]
        
        if oneshot_scores:
            print(f"\n📊 Average Scores:")
            print(f"   Oneshot: {sum(oneshot_scores)/len(oneshot_scores):.1f}/50")
        if ir_scores:
            print(f"   IR Pipeline: {sum(ir_scores)/len(ir_scores):.1f}/50")
    
    # Count errors
    errors = [r for r in results if r.get("error")]
    if errors:
        print(f"\n❌ Errors: {len(errors)}")
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
        print("\n⚠️  Note: Set ANTHROPIC_API_KEY at the top of this script for grading")
        return
    
    # Check API key if grading
    if "--grade" in sys.argv and not ANTHROPIC_API_KEY:
        print("⚠️  Warning: ANTHROPIC_API_KEY not set!")
        print("   Set it at the top of test_crazy_prompts.py to enable grading")
        print("   Continuing without grading...\n")
    
    try:
        prompts = load_prompts()
        print(f"Loaded {len(prompts)} prompts from crazy_test_prompts.txt\n")
    except FileNotFoundError:
        print("❌ Error: crazy_test_prompts.txt not found!")
        return
    except Exception as e:
        print(f"❌ Error loading prompts: {e}")
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
            print("⚠️  Grading enabled - this will make API calls to Claude Haiku")
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
                print(f"💾 Progress saved ({i}/{len(prompts)} prompts completed)")
                
                # Small delay to avoid rate limits
                if grade and i < len(prompts):
                    time.sleep(1)
                    
            except KeyboardInterrupt:
                print("\n\n⏸️  Stopped by user.")
                # Save partial results before exiting
                if results:
                    partial_filename = f"test_results_partial_{datetime.now().strftime('%Y%m%d_%H%M%S')}.json"
                    save_results(results, partial_filename)
                    print(f"💾 Partial results saved to: {partial_filename}")
                break
            except Exception as e:
                print(f"\n❌ Error testing prompt {i}: {e}\n")
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
        print(f"✅ Completed {len(results)} prompts in {elapsed:.1f} seconds")
        print(f"{'='*80}")
        
        # Save final results with timestamp
        final_file = save_results(results)
        
        # Keep in_progress file as backup, but also create final timestamped version
        print(f"💾 Final results saved (in_progress file also available as backup)")
        
        # Print summary
        print_summary(results)
        
        return
    
    # Test specific prompt number
    try:
        prompt_num = int(sys.argv[1])
        if prompt_num < 1 or prompt_num > len(prompts):
            print(f"❌ Error: Prompt number must be between 1 and {len(prompts)}")
            return
        
        compare = "--compare" in sys.argv or "--grade" in sys.argv
        grade = "--grade" in sys.argv
        verbose = "--verbose" in sys.argv or "-v" in sys.argv
        
        prompt = prompts[prompt_num - 1]
        result_data = test_prompt(prompt_num, prompt, verbose=verbose, compare=compare, grade=grade)
        
        # Save single result
        save_results([result_data], f"prompt_{prompt_num}_result.json")
        
    except ValueError:
        print(f"❌ Error: '{sys.argv[1]}' is not a valid prompt number")
        print("Use --list to see available prompts")
    except Exception as e:
        print(f"❌ Error: {e}")

if __name__ == "__main__":
    main()

