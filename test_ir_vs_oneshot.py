"""
Test Script: IR Pipeline (no RAG) vs Oneshot
Compares the value of the intermediate representation without RAG complexity
"""

import json
import os
import sys
import requests
from datetime import datetime
from typing import List, Dict, Optional
from dotenv import load_dotenv

from unity_pipeline_no_rag import UnityPipelineNoRAG

# Load .env file
load_dotenv()

# Config
LLM_URL = "http://localhost:1234/v1/chat/completions"
CLAUDE_API_KEY = os.environ.get("ANTHROPIC_API_KEY", "")
PROMPTS_FILE = "crazy_test_prompts.txt"
OUTPUT_DIR = "prompt_test_results"

# Oneshot system prompt (same as other tests for fair comparison)
ONESHOT_SYSTEM_PROMPT = """You are a Unity C# expert. Generate complete, working MonoBehaviour scripts.

Rules:
1. Output ONLY valid C# code - no markdown, no explanations
2. Use proper Unity patterns and best practices
3. Include all necessary using statements
4. Use [SerializeField] for inspector-exposed fields
5. Add [RequireComponent] where appropriate"""


def load_prompts(file_path: str) -> List[str]:
    """Load prompts from file"""
    prompts = []
    with open(file_path, 'r', encoding='utf-8') as f:
        for line in f:
            line = line.strip()
            if line and not line.startswith('#'):
                prompts.append(line)
    return prompts


def generate_oneshot(prompt: str) -> Optional[str]:
    """Generate code directly from prompt (no IR)"""
    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": "local-model",
                "messages": [
                    {"role": "system", "content": ONESHOT_SYSTEM_PROMPT},
                    {"role": "user", "content": f"Create a Unity MonoBehaviour script that:\n\n{prompt}"}
                ],
                "temperature": 0.7,
                "max_tokens": 2500
            },
            timeout=60
        )
        
        if response.status_code != 200:
            return None
        
        code = response.json()["choices"][0]["message"]["content"]
        return clean_code(code)
        
    except Exception as e:
        print(f"  Oneshot error: {e}")
        return None


def clean_code(text: str) -> str:
    """Remove markdown from code"""
    text = text.strip()
    if text.startswith("```"):
        lines = text.split("\n")
        if lines[0].startswith("```"):
            lines = lines[1:]
        if lines and lines[-1].startswith("```"):
            lines = lines[:-1]
        text = "\n".join(lines)
    return text


def grade_with_claude(prompt: str, oneshot_code: str, ir_code: str, ir_json: Dict) -> Optional[Dict]:
    """Grade both approaches using Claude Haiku"""
    if not CLAUDE_API_KEY:
        print("  No ANTHROPIC_API_KEY - skipping grading")
        return None
    
    grading_prompt = f"""You are evaluating Unity C# code quality. Compare two approaches for generating this script:

PROMPT: {prompt}

=== APPROACH 1: ONESHOT (Direct generation) ===
{oneshot_code}

=== APPROACH 2: IR PIPELINE (Two-step: prompt→IR→code, NO RAG) ===
IR Specification:
{json.dumps(ir_json, indent=2)}

Generated Code:
{ir_code}

Rate EACH approach on these criteria (1-10 scale):
1. architecture: Code organization, modularity, separation of concerns
2. fixability: How easy to debug and maintain
3. unity_patterns: Proper use of Unity conventions (GetComponent, SerializeField, etc.)
4. correctness: Will it work? Are APIs used correctly?
5. completeness: Does it fully implement the requirements?

Weights: architecture=3, fixability=2, unity_patterns=1, correctness=2, completeness=2

Respond with ONLY this JSON:
{{
  "oneshot": {{
    "architecture": X,
    "fixability": X,
    "unity_patterns": X,
    "correctness": X,
    "completeness": X,
    "weighted_total": X,
    "justification": "one sentence"
  }},
  "ir_pipeline": {{
    "architecture": X,
    "fixability": X,
    "unity_patterns": X,
    "correctness": X,
    "completeness": X,
    "weighted_total": X,
    "justification": "one sentence"
  }},
  "winner": "oneshot" or "ir_pipeline",
  "key_insight": "What does this comparison reveal about the value of IR without RAG?"
}}"""

    try:
        response = requests.post(
            "https://api.anthropic.com/v1/messages",
            headers={
                "x-api-key": CLAUDE_API_KEY,
                "anthropic-version": "2023-06-01",
                "content-type": "application/json"
            },
            json={
                "model": "claude-3-haiku-20240307",
                "max_tokens": 1000,
                "messages": [{"role": "user", "content": grading_prompt}]
            },
            timeout=30
        )
        
        if response.status_code != 200:
            print(f"  Claude error: {response.status_code}")
            return None
        
        content = response.json()["content"][0]["text"]
        # Extract JSON
        start = content.find("{")
        end = content.rfind("}") + 1
        if start >= 0 and end > start:
            return json.loads(content[start:end])
        return None
        
    except Exception as e:
        print(f"  Grading error: {e}")
        return None


def test_prompt(prompt_num: int, prompt: str, grade: bool = True) -> Dict:
    """Test a single prompt with both approaches"""
    result = {
        "prompt_num": prompt_num,
        "prompt": prompt,
        "timestamp": datetime.now().isoformat()
    }
    
    # Generate oneshot
    print("  Generating ONESHOT...")
    oneshot_code = generate_oneshot(prompt)
    result["oneshot_code"] = oneshot_code
    
    if not oneshot_code:
        result["error"] = "Oneshot generation failed"
        return result
    
    # Generate IR pipeline
    print("  Generating IR PIPELINE (no RAG)...")
    pipeline = UnityPipelineNoRAG(verbose=False)
    ir_result = pipeline.generate(prompt)
    
    result["ir_json"] = ir_result.ir_json
    result["ir_code"] = ir_result.code
    
    if ir_result.error:
        result["error"] = ir_result.error
        return result
    
    # Grade if requested
    if grade and oneshot_code and ir_result.code:
        print("  Grading with Claude...")
        grade_result = grade_with_claude(prompt, oneshot_code, ir_result.code, ir_result.ir_json)
        result["grade"] = grade_result
        
        if grade_result:
            winner = grade_result.get("winner", "unknown")
            os_score = grade_result.get("oneshot", {}).get("weighted_total", "?")
            ir_score = grade_result.get("ir_pipeline", {}).get("weighted_total", "?")
            print(f"  Winner: {winner.upper()} (oneshot={os_score}, ir={ir_score})")
    
    return result


def print_summary(results: List[Dict]):
    """Print summary statistics"""
    print("\n" + "=" * 70)
    print("SUMMARY: IR PIPELINE (no RAG) vs ONESHOT")
    print("=" * 70)
    
    graded = [r for r in results if r.get("grade")]
    
    if not graded:
        print("No graded results available.")
        return
    
    # Count wins
    oneshot_wins = 0
    ir_wins = 0
    ties = 0
    
    oneshot_scores = []
    ir_scores = []
    
    for r in graded:
        g = r.get("grade", {})
        winner = g.get("winner", "")
        
        if winner == "oneshot":
            oneshot_wins += 1
        elif winner == "ir_pipeline":
            ir_wins += 1
        else:
            ties += 1
        
        os_data = g.get("oneshot", {})
        ir_data = g.get("ir_pipeline", {})
        
        if isinstance(os_data, dict) and os_data.get("weighted_total"):
            oneshot_scores.append(os_data["weighted_total"])
        if isinstance(ir_data, dict) and ir_data.get("weighted_total"):
            ir_scores.append(ir_data["weighted_total"])
    
    print(f"\nTotal Graded: {len(graded)}")
    print(f"\nWins:")
    print(f"  Oneshot:     {oneshot_wins} ({100*oneshot_wins/len(graded):.0f}%)")
    print(f"  IR Pipeline: {ir_wins} ({100*ir_wins/len(graded):.0f}%)")
    if ties:
        print(f"  Ties:        {ties}")
    
    if oneshot_scores and ir_scores:
        print(f"\nAverage Scores (out of 80):")
        print(f"  Oneshot:     {sum(oneshot_scores)/len(oneshot_scores):.1f}")
        print(f"  IR Pipeline: {sum(ir_scores)/len(ir_scores):.1f}")
    
    # Dimension breakdown
    print("\nAverage by Dimension:")
    dims = ["architecture", "fixability", "unity_patterns", "correctness", "completeness"]
    
    for dim in dims:
        os_vals = [r["grade"]["oneshot"].get(dim, 0) for r in graded 
                   if isinstance(r.get("grade", {}).get("oneshot"), dict)]
        ir_vals = [r["grade"]["ir_pipeline"].get(dim, 0) for r in graded 
                   if isinstance(r.get("grade", {}).get("ir_pipeline"), dict)]
        
        if os_vals and ir_vals:
            os_avg = sum(os_vals) / len(os_vals)
            ir_avg = sum(ir_vals) / len(ir_vals)
            diff = ir_avg - os_avg
            arrow = "↑" if diff > 0.3 else "↓" if diff < -0.3 else "="
            print(f"  {dim:15} oneshot={os_avg:.1f}  ir={ir_avg:.1f}  {arrow}")
    
    print("\n" + "=" * 70)


def main():
    """Main entry point"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Test IR Pipeline vs Oneshot")
    parser.add_argument("prompts", nargs="*", type=int, help="Prompt numbers to test (1-indexed)")
    parser.add_argument("--all", action="store_true", help="Test all prompts")
    parser.add_argument("--no-grade", action="store_true", help="Skip Claude grading")
    args = parser.parse_args()
    
    # Load prompts
    prompts = load_prompts(PROMPTS_FILE)
    print(f"Loaded {len(prompts)} prompts from {PROMPTS_FILE}\n")
    
    # Determine which to test
    if args.all:
        to_test = list(range(1, len(prompts) + 1))
    elif args.prompts:
        to_test = args.prompts
    else:
        to_test = [1]  # Default to first prompt
    
    # Create output dir
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    
    results = []
    
    for num in to_test:
        if num < 1 or num > len(prompts):
            print(f"Skipping invalid prompt #{num}")
            continue
        
        prompt = prompts[num - 1]
        
        print("=" * 70)
        print(f"[{num}/{len(prompts)}] TESTING PROMPT #{num}")
        print("=" * 70)
        print(f"{prompt[:100]}..." if len(prompt) > 100 else prompt)
        print()
        
        try:
            result = test_prompt(num, prompt, grade=not args.no_grade)
            results.append(result)
            
            # Save incrementally
            out_file = os.path.join(OUTPUT_DIR, "ir_vs_oneshot_results.json")
            with open(out_file, 'w', encoding='utf-8') as f:
                json.dump(results, f, indent=2)
            
        except Exception as e:
            print(f"  Error: {e}")
            results.append({"prompt_num": num, "error": str(e)})
        
        print()
    
    # Print summary
    print_summary(results)
    
    # Final save
    out_file = os.path.join(OUTPUT_DIR, "ir_vs_oneshot_results.json")
    with open(out_file, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2)
    print(f"\nResults saved to: {out_file}")


if __name__ == "__main__":
    main()

