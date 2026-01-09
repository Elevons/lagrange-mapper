#!/usr/bin/env python3
"""
Generate Calibration Examples using Claude Haiku

Creates 100 high-quality (prompt, ideal_ir, good_code) triplets for IR calibration.
Uses 10 parallel requests to Claude Haiku for speed.

Usage:
    python generate_calibration_examples.py
    python generate_calibration_examples.py --count 50  # Generate 50 examples
    python generate_calibration_examples.py --resume    # Resume from saved progress
"""

import os
import json
import asyncio
import aiohttp
from pathlib import Path
from datetime import datetime
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass, asdict
import argparse

# Load .env file
try:
    from dotenv import load_dotenv
    load_dotenv()
    HAS_DOTENV = True
except ImportError:
    HAS_DOTENV = False
    print("Note: python-dotenv not installed. Install with: pip install python-dotenv")
    print("      Or set ANTHROPIC_API_KEY environment variable directly.")

# ============================================================================
# CONFIGURATION
# ============================================================================

ANTHROPIC_API_KEY = os.getenv("ANTHROPIC_API_KEY", "")
ANTHROPIC_API_URL = "https://api.anthropic.com/v1/messages"
CLAUDE_MODEL = "claude-3-5-haiku-20241022"

# Concurrency
MAX_CONCURRENT_REQUESTS = 10
REQUEST_DELAY = 0.1  # Small delay between batches to avoid rate limits

# Output
OUTPUT_DIR = "calibration_examples"
PROGRESS_FILE = "calibration_progress.json"

# ============================================================================
# UNITY BEHAVIOR PROMPTS (100 diverse examples)
# ============================================================================

BEHAVIOR_PROMPTS = [
    # === PICKUPS & COLLECTIBLES (10) ===
    "rotating coin that adds score when collected",
    "health pickup that heals player on touch",
    "ammo box that refills weapon ammunition",
    "power-up that grants temporary invincibility",
    "key that unlocks corresponding door",
    "gem that floats and sparkles",
    "fuel canister for vehicle refueling",
    "experience orb that grants XP",
    "shield pickup that adds armor",
    "speed boost pickup that increases movement",
    
    # === ENEMIES & AI (15) ===
    "enemy that patrols between waypoints",
    "turret that tracks and shoots at player",
    "enemy that chases player when spotted",
    "boss with multiple attack phases",
    "flying enemy that circles overhead",
    "enemy that explodes on death",
    "spawner that creates enemies periodically",
    "enemy with ranged and melee attacks",
    "stealth enemy that ambushes player",
    "minion that follows a leader",
    "enemy that retreats when health is low",
    "homing missile that tracks target",
    "guard that alerts others when attacked",
    "enemy that shields nearby allies",
    "swarm AI that coordinates movement",
    
    # === ENVIRONMENT & HAZARDS (15) ===
    "spike trap that damages on contact",
    "pressure plate that opens nearby door",
    "moving platform between two points",
    "rotating saw blade hazard",
    "lava floor that damages over time",
    "wind zone that pushes objects",
    "teleporter between two locations",
    "breakable wall that shatters on impact",
    "falling boulder triggered by proximity",
    "laser beam that toggles on and off",
    "conveyor belt that moves objects",
    "electrified floor that pulses",
    "poison gas cloud that damages",
    "ice floor that reduces friction",
    "crumbling platform that falls after stepping",
    
    # === WEAPONS & COMBAT (12) ===
    "gun that fires projectiles",
    "sword that swings with animation",
    "grenade that explodes after delay",
    "shotgun with spread pattern",
    "laser weapon with continuous beam",
    "rocket launcher with splash damage",
    "melee weapon with combo attacks",
    "bow that charges for power shots",
    "flamethrower with particle effects",
    "shield that blocks incoming damage",
    "mine that detonates when stepped on",
    "boomerang that returns to thrower",
    
    # === PLAYER MECHANICS (12) ===
    "player jump with variable height",
    "double jump ability",
    "wall jump and slide",
    "dash ability with cooldown",
    "grappling hook movement",
    "climbing on marked surfaces",
    "swimming with oxygen meter",
    "gliding with parachute",
    "ground pound attack",
    "sprint with stamina cost",
    "crouch and crawl movement",
    "ledge grab and climb up",
    
    # === UI & FEEDBACK (8) ===
    "health bar that updates visually",
    "damage numbers that float up",
    "minimap marker for objectives",
    "compass pointing to target",
    "screen shake on impact",
    "hit flash effect on damage",
    "crosshair that changes on hover",
    "tooltip on hover over item",
    
    # === VEHICLES & MOUNTS (8) ===
    "car with acceleration and steering",
    "boat that floats on water",
    "helicopter with altitude control",
    "horse that player can mount",
    "tank with turret rotation",
    "motorcycle with leaning turns",
    "spaceship with thrust controls",
    "minecart on rails",
    
    # === INTERACTABLES (10) ===
    "door that opens on approach",
    "chest that contains random loot",
    "lever that activates mechanism",
    "button that triggers event once",
    "NPC with dialogue options",
    "shop that displays purchasable items",
    "save point that checkpoints progress",
    "sign that displays message",
    "terminal with hackable interface",
    "campfire that restores health over time",
    
    # === AUDIO & VISUAL (5) ===
    "footstep sounds based on surface",
    "ambient sound zone",
    "music that changes by area",
    "light that flickers randomly",
    "particle emitter on trigger",
    
    # === PUZZLES & LOGIC (5) ===
    "color matching puzzle blocks",
    "pressure plates requiring weight",
    "rotating puzzle ring",
    "sequence memory game",
    "sliding tile puzzle",
]

# ============================================================================
# PROMPTS FOR CLAUDE
# ============================================================================

GENERATE_CODE_SYSTEM = """You are an expert Unity C# developer. Generate production-quality MonoBehaviour scripts.

Requirements:
1. Use proper Unity lifecycle methods (Start, Update, FixedUpdate, etc.)
2. Use correct Unity APIs and patterns
3. Include [SerializeField] or public fields for configuration
4. Add clear comments explaining the code
5. Handle edge cases and null checks
6. Use proper naming conventions

Output ONLY the C# code, no markdown, no explanations."""

GENERATE_IR_SYSTEM = """You generate Unity behavior specifications in clean JSON format.

The IR (Intermediate Representation) uses NATURAL LANGUAGE, not code.

Structure:
{
  "class_name": "ClassName",
  "components": ["Component1", "Component2"],
  "fields": [{"name": "fieldName", "type": "float", "default": 10}],
  "behaviors": [{
    "name": "behavior_name",
    "trigger": "natural language trigger description",
    "actions": [
      {"action": "natural language action description"}
    ]
  }]
}

CRITICAL RULES:
- Use NATURAL LANGUAGE for triggers and actions
- NO operators (==, <, >, +, -)
- NO Unity API calls (Vector3.up, Time.deltaTime)
- NO function syntax (distance(), normalize())
- NO code expressions

Good: "move toward player at walking speed"
Bad: "transform.position += direction * speed * Time.deltaTime"

Good: "when player is within detection range"
Bad: "if (distance < detectionRadius)"

Output ONLY valid JSON, no markdown."""

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class CalibrationExample:
    """A single calibration example with prompt, IR, and code"""
    prompt: str
    ideal_ir: Optional[Dict] = None
    good_code: Optional[str] = None
    behavior_type: str = "general"
    generated_at: str = ""
    error: Optional[str] = None
    
    def is_complete(self) -> bool:
        return self.ideal_ir is not None and self.good_code is not None


# ============================================================================
# ASYNC CLAUDE API
# ============================================================================

async def call_claude_async(
    session: aiohttp.ClientSession,
    system_prompt: str,
    user_prompt: str,
    semaphore: asyncio.Semaphore,
    max_tokens: int = 4000,
    temperature: float = 0.3
) -> Optional[str]:
    """Make async request to Claude API with semaphore for rate limiting"""
    
    async with semaphore:
        headers = {
            "x-api-key": ANTHROPIC_API_KEY,
            "anthropic-version": "2023-06-01",
            "Content-Type": "application/json"
        }
        
        payload = {
            "model": CLAUDE_MODEL,
            "max_tokens": max_tokens,
            "temperature": temperature,
            "system": system_prompt,
            "messages": [{"role": "user", "content": user_prompt}]
        }
        
        try:
            async with session.post(
                ANTHROPIC_API_URL,
                headers=headers,
                json=payload,
                timeout=aiohttp.ClientTimeout(total=120)
            ) as response:
                if response.status == 200:
                    data = await response.json()
                    return data['content'][0]['text'].strip()
                else:
                    error_text = await response.text()
                    print(f"    API error {response.status}: {error_text[:100]}")
                    return None
        except Exception as e:
            print(f"    Request error: {e}")
            return None


async def generate_example_async(
    session: aiohttp.ClientSession,
    prompt: str,
    index: int,
    semaphore: asyncio.Semaphore
) -> CalibrationExample:
    """Generate both IR and code for a single prompt"""
    
    example = CalibrationExample(
        prompt=prompt,
        generated_at=datetime.now().isoformat()
    )
    
    # Categorize by prompt content
    prompt_lower = prompt.lower()
    if any(w in prompt_lower for w in ["enemy", "ai", "patrol", "chase", "attack"]):
        example.behavior_type = "combat"
    elif any(w in prompt_lower for w in ["pickup", "collect", "health", "ammo", "coin"]):
        example.behavior_type = "pickup"
    elif any(w in prompt_lower for w in ["platform", "door", "trap", "hazard"]):
        example.behavior_type = "environment"
    elif any(w in prompt_lower for w in ["player", "jump", "dash", "move"]):
        example.behavior_type = "movement"
    elif any(w in prompt_lower for w in ["gun", "weapon", "sword", "shoot"]):
        example.behavior_type = "combat"
    else:
        example.behavior_type = "general"
    
    print(f"  [{index+1:3d}] {prompt[:50]}...", end=" ", flush=True)
    
    # Generate both in parallel
    ir_task = call_claude_async(
        session, 
        GENERATE_IR_SYSTEM,
        f"Generate IR JSON for: {prompt}",
        semaphore,
        max_tokens=2000,
        temperature=0.3
    )
    
    code_task = call_claude_async(
        session,
        GENERATE_CODE_SYSTEM,
        f"Generate Unity C# MonoBehaviour for: {prompt}",
        semaphore,
        max_tokens=4000,
        temperature=0.3
    )
    
    ir_result, code_result = await asyncio.gather(ir_task, code_task)
    
    # Parse IR
    if ir_result:
        try:
            # Extract JSON from response
            import re
            json_match = re.search(r'\{[\s\S]*\}', ir_result)
            if json_match:
                example.ideal_ir = json.loads(json_match.group(0))
        except json.JSONDecodeError as e:
            example.error = f"IR parse error: {e}"
    
    # Store code
    if code_result:
        # Clean markdown if present
        if "```" in code_result:
            lines = code_result.split('\n')
            clean_lines = []
            in_block = False
            for line in lines:
                if line.strip().startswith('```'):
                    in_block = not in_block
                    continue
                if in_block or not line.strip().startswith('```'):
                    clean_lines.append(line)
            example.good_code = '\n'.join(clean_lines).strip()
        else:
            example.good_code = code_result
    
    status = "✓" if example.is_complete() else "✗"
    print(status)
    
    return example


async def generate_all_examples(
    prompts: List[str],
    start_index: int = 0
) -> List[CalibrationExample]:
    """Generate all examples with concurrent requests"""
    
    semaphore = asyncio.Semaphore(MAX_CONCURRENT_REQUESTS)
    
    async with aiohttp.ClientSession() as session:
        tasks = []
        for i, prompt in enumerate(prompts[start_index:], start=start_index):
            task = generate_example_async(session, prompt, i, semaphore)
            tasks.append(task)
            
            # Small delay between task creation to avoid burst
            if len(tasks) % MAX_CONCURRENT_REQUESTS == 0:
                await asyncio.sleep(REQUEST_DELAY)
        
        results = await asyncio.gather(*tasks)
    
    return list(results)


# ============================================================================
# MAIN FUNCTIONS
# ============================================================================

def save_progress(examples: List[CalibrationExample], output_dir: str):
    """Save current progress to file"""
    os.makedirs(output_dir, exist_ok=True)
    
    progress_path = os.path.join(output_dir, PROGRESS_FILE)
    
    data = {
        "generated_at": datetime.now().isoformat(),
        "total_count": len(examples),
        "complete_count": sum(1 for e in examples if e.is_complete()),
        "examples": [asdict(e) for e in examples]
    }
    
    with open(progress_path, 'w') as f:
        json.dump(data, f, indent=2, default=str)
    
    print(f"\n  Saved progress to {progress_path}")


def load_progress(output_dir: str) -> Tuple[List[CalibrationExample], int]:
    """Load previous progress if exists"""
    progress_path = os.path.join(output_dir, PROGRESS_FILE)
    
    if not os.path.exists(progress_path):
        return [], 0
    
    try:
        with open(progress_path) as f:
            data = json.load(f)
        
        examples = []
        for e in data.get("examples", []):
            example = CalibrationExample(
                prompt=e["prompt"],
                ideal_ir=e.get("ideal_ir"),
                good_code=e.get("good_code"),
                behavior_type=e.get("behavior_type", "general"),
                generated_at=e.get("generated_at", ""),
                error=e.get("error")
            )
            examples.append(example)
        
        complete_count = sum(1 for e in examples if e.is_complete())
        print(f"  Loaded {len(examples)} examples ({complete_count} complete)")
        
        return examples, len(examples)
        
    except Exception as e:
        print(f"  Warning: Could not load progress: {e}")
        return [], 0


def save_final_output(examples: List[CalibrationExample], output_dir: str):
    """Save final calibration data for use with ir_calibration.py"""
    os.makedirs(output_dir, exist_ok=True)
    
    # Save as calibration pairs
    calibration_data = {
        "generated_at": datetime.now().isoformat(),
        "model": CLAUDE_MODEL,
        "total_examples": len(examples),
        "complete_examples": sum(1 for e in examples if e.is_complete()),
        "by_type": {},
        "pairs": []
    }
    
    # Count by type
    type_counts = {}
    for e in examples:
        type_counts[e.behavior_type] = type_counts.get(e.behavior_type, 0) + 1
    calibration_data["by_type"] = type_counts
    
    # Build pairs
    for e in examples:
        if e.is_complete():
            calibration_data["pairs"].append({
                "prompt": e.prompt,
                "ideal_ir": e.ideal_ir,
                "good_code": e.good_code,
                "behavior_type": e.behavior_type
            })
    
    # Save main calibration file
    output_path = os.path.join(output_dir, "calibration_data.json")
    with open(output_path, 'w') as f:
        json.dump(calibration_data, f, indent=2)
    
    print(f"\n{'='*60}")
    print("CALIBRATION DATA SAVED")
    print(f"{'='*60}")
    print(f"  Output: {output_path}")
    print(f"  Total: {calibration_data['total_examples']} examples")
    print(f"  Complete: {calibration_data['complete_examples']} examples")
    print(f"\n  By type:")
    for btype, count in sorted(type_counts.items()):
        print(f"    {btype}: {count}")
    
    # Also save individual code files for reference
    code_dir = os.path.join(output_dir, "code_examples")
    os.makedirs(code_dir, exist_ok=True)
    
    for i, e in enumerate(examples):
        if e.good_code:
            # Create safe filename
            safe_name = "".join(c if c.isalnum() else "_" for c in e.prompt[:40])
            filename = f"{i:03d}_{safe_name}.cs"
            
            with open(os.path.join(code_dir, filename), 'w') as f:
                f.write(f"// Prompt: {e.prompt}\n")
                f.write(f"// Type: {e.behavior_type}\n\n")
                f.write(e.good_code)
    
    print(f"\n  Code examples saved to: {code_dir}/")
    
    return output_path


def main():
    parser = argparse.ArgumentParser(description="Generate calibration examples using Claude Haiku")
    parser.add_argument("--count", type=int, default=100, help="Number of examples to generate")
    parser.add_argument("--resume", action="store_true", help="Resume from previous progress")
    parser.add_argument("--output", default=OUTPUT_DIR, help="Output directory")
    args = parser.parse_args()
    
    print("="*60)
    print("CALIBRATION EXAMPLE GENERATOR")
    print("="*60)
    
    # Check API key
    if not ANTHROPIC_API_KEY:
        print("\n❌ Error: ANTHROPIC_API_KEY not set!")
        print("\nTo fix:")
        print("  1. Create a .env file with: ANTHROPIC_API_KEY=sk-ant-...")
        print("  2. Or set environment variable: export ANTHROPIC_API_KEY=sk-ant-...")
        print("  3. Install python-dotenv: pip install python-dotenv")
        return
    
    print(f"\nConfiguration:")
    print(f"  Model: {CLAUDE_MODEL}")
    print(f"  Target count: {args.count}")
    print(f"  Concurrent requests: {MAX_CONCURRENT_REQUESTS}")
    print(f"  Output directory: {args.output}")
    
    # Select prompts
    prompts = BEHAVIOR_PROMPTS[:args.count]
    if len(prompts) < args.count:
        print(f"\n  Note: Only {len(prompts)} prompts available")
    
    # Check for resume
    start_index = 0
    existing_examples = []
    
    if args.resume:
        existing_examples, start_index = load_progress(args.output)
        if start_index > 0:
            print(f"\n  Resuming from example {start_index + 1}")
    
    remaining = len(prompts) - start_index
    if remaining <= 0:
        print(f"\n✓ All {len(prompts)} examples already generated!")
        # Just save final output
        save_final_output(existing_examples, args.output)
        return
    
    print(f"\n{'='*60}")
    print(f"GENERATING {remaining} EXAMPLES ({MAX_CONCURRENT_REQUESTS} parallel)")
    print(f"{'='*60}\n")
    
    # Run async generation
    try:
        new_examples = asyncio.run(generate_all_examples(prompts, start_index))
        
        # Combine with existing
        all_examples = existing_examples + new_examples
        
        # Save progress
        save_progress(all_examples, args.output)
        
        # Save final output
        save_final_output(all_examples, args.output)
        
        # Summary
        complete = sum(1 for e in all_examples if e.is_complete())
        failed = len(all_examples) - complete
        
        print(f"\n{'='*60}")
        print("GENERATION COMPLETE")
        print(f"{'='*60}")
        print(f"  Complete: {complete}")
        print(f"  Failed: {failed}")
        
        if failed > 0:
            print(f"\n  Failed examples:")
            for e in all_examples:
                if not e.is_complete():
                    print(f"    - {e.prompt[:50]}... ({e.error or 'unknown error'})")
        
        print(f"\n  Next step: Use with ir_calibration.py")
        print(f"    from ir_calibration import IRCalibrator")
        print(f"    calibrator = IRCalibrator()")
        print(f"    calibrator.load_from_examples('{args.output}/calibration_data.json')")
        
    except KeyboardInterrupt:
        print("\n\n⚠ Interrupted! Saving progress...")
        if 'new_examples' in dir():
            save_progress(existing_examples + new_examples, args.output)
        print("  Run with --resume to continue")


if __name__ == "__main__":
    main()



