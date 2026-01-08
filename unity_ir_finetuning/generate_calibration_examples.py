#!/usr/bin/env python3
"""
Generate Calibration Examples using Claude Sonnet

Creates 300 high-quality (prompt, ideal_ir, good_code) triplets for IR calibration.
Uses 10 parallel requests to Claude Sonnet for speed and quality.

Usage:
    python generate_calibration_examples.py
    python generate_calibration_examples.py --count 300  # Generate 300 examples
    python generate_calibration_examples.py --resume     # Resume from saved progress
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
CLAUDE_MODEL = "claude-sonnet-4-20250514"  # Sonnet for better quality

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
    
    # === ADDITIONAL PICKUPS (20) ===
    "coin that spins and plays sound when collected",
    "treasure chest that opens with key",
    "potion bottle with swirling particles",
    "magnet that attracts nearby coins",
    "star collectible that bounces",
    "crystal that glows when near player",
    "food item that restores stamina",
    "armor piece that increases defense",
    "scroll that teaches new ability",
    "rare drop with sparkle effects",
    "bouncing gem that moves randomly",
    "floating heart health pickup",
    "weapon upgrade pickup",
    "skill point orb",
    "currency bag with gold coins",
    "mushroom power-up",
    "flower that grants special power",
    "feather that enables floating",
    "boots that increase speed",
    "ring that provides protection",
    
    # === ADDITIONAL ENEMIES (25) ===
    "slime that splits when killed",
    "ghost that phases through walls",
    "skeleton that reassembles after death",
    "bat that hangs from ceiling",
    "spider that drops from above",
    "zombie that walks slowly toward player",
    "wolf that attacks in packs",
    "archer enemy that keeps distance",
    "mage enemy that casts spells",
    "knight with shield blocking",
    "dragon that breathes fire",
    "golem that throws rocks",
    "ninja that teleports behind player",
    "robot with laser attacks",
    "alien with tentacle attacks",
    "demon that summons minions",
    "vampire that drains health",
    "werewolf that transforms at night",
    "pirate with sword and pistol",
    "wizard with elemental attacks",
    "giant that stomps ground",
    "snake that slithers and bites",
    "scorpion with poison tail",
    "crab that moves sideways",
    "fish that jumps from water",
    
    # === ADDITIONAL ENVIRONMENT (25) ===
    "torch that lights up area",
    "water current that pushes player",
    "quicksand that slows movement",
    "bounce pad that launches player",
    "zipline for fast travel",
    "ladder for climbing",
    "rope swing",
    "destructible barrel",
    "explosive crate",
    "rolling boulder trap",
    "arrow trap that shoots from wall",
    "flame jet trap",
    "pit fall trap with spikes",
    "swinging axe pendulum",
    "rising water level",
    "collapsing bridge",
    "rotating blade obstacle",
    "steam vent that damages",
    "acid pool hazard",
    "magnetic field zone",
    "gravity flip zone",
    "time slow zone",
    "darkness zone that limits vision",
    "slippery slope",
    "wind tunnel",
    
    # === ADDITIONAL WEAPONS (20) ===
    "crossbow with bolt travel time",
    "throwing knife with arc",
    "whip with long range",
    "hammer with ground slam",
    "spear with thrust attack",
    "axe with spin attack",
    "staff with magic projectile",
    "scythe with sweeping attack",
    "dual daggers with fast combo",
    "katana with quick draw",
    "mace with stun effect",
    "trident with water affinity",
    "chakram that returns",
    "slingshot with stones",
    "blowgun with darts",
    "net launcher for capturing",
    "ice wand that freezes",
    "fire staff that burns",
    "lightning rod that chains",
    "gravity gun that pulls objects",
    
    # === ADDITIONAL PLAYER MECHANICS (25) ===
    "roll dodge with invincibility frames",
    "parry that reflects attacks",
    "block with shield",
    "charge attack with hold",
    "aerial combo attacks",
    "grab and throw enemy",
    "stealth crouch with visibility meter",
    "lock-on targeting system",
    "quick turn around",
    "slide under obstacles",
    "pole vault jump",
    "rope climbing",
    "swimming dive",
    "underwater breath timer",
    "jetpack flight",
    "wing glide",
    "telekinesis grab",
    "time rewind ability",
    "clone ability",
    "size change ability",
    "invisibility toggle",
    "super speed burst",
    "power punch charge",
    "healing meditation",
    "rage mode transformation",
    
    # === ADDITIONAL UI & FEEDBACK (15) ===
    "inventory grid system",
    "quest tracker display",
    "enemy health bar above head",
    "stamina bar with recovery",
    "mana bar for abilities",
    "experience bar with level up",
    "notification popup",
    "achievement unlock banner",
    "countdown timer display",
    "score multiplier indicator",
    "combo counter",
    "directional damage indicator",
    "low health warning flash",
    "item pickup notification",
    "buff and debuff icons",
    
    # === ADDITIONAL VEHICLES (15) ===
    "skateboard with tricks",
    "surfboard on waves",
    "snowboard down slopes",
    "bicycle with pedaling",
    "go-kart racing",
    "fighter jet with missiles",
    "submarine underwater",
    "hoverboard floating",
    "dragon mount flying",
    "wolf mount running",
    "mine cart on tracks",
    "hot air balloon floating",
    "glider with wind currents",
    "rocket ship launch",
    "portal travel between points",
    
    # === ADDITIONAL INTERACTABLES (20) ===
    "vending machine with selection",
    "computer terminal with text",
    "radio that plays music",
    "mirror that reflects",
    "painting that reveals secret",
    "bookshelf with hidden switch",
    "fireplace with warmth zone",
    "bed for sleeping",
    "cooking pot with recipes",
    "forge for crafting",
    "fishing spot",
    "garden patch for planting",
    "mailbox with letters",
    "phone with conversation",
    "elevator with floor selection",
    "bridge that extends",
    "gate that requires key",
    "alarm that triggers on detection",
    "camera security system",
    "turntable DJ mixer",
    
    # === ADDITIONAL AUDIO & VISUAL (15) ===
    "day night cycle",
    "weather rain effect",
    "thunder and lightning",
    "fog rolling in",
    "leaves falling from trees",
    "dust particles in light",
    "underwater bubble effects",
    "fire crackling with embers",
    "waterfall with mist",
    "aurora borealis sky effect",
    "lens flare from sun",
    "motion blur on speed",
    "depth of field focus",
    "chromatic aberration effect",
    "vignette darkness at edges",
    
    # === GAME SYSTEMS (20) ===
    "save and load system",
    "pause menu with options",
    "difficulty selection",
    "tutorial message system",
    "checkpoint respawn",
    "game over screen",
    "victory celebration",
    "score submission to leaderboard",
    "achievement tracking",
    "statistics tracking",
    "settings preferences save",
    "audio volume controls",
    "graphics quality settings",
    "control remapping",
    "language localization",
    "credits scroll",
    "main menu navigation",
    "loading screen with progress",
    "splash screen display",
    "input prompt icons",
    
    # === MULTIPLAYER & NETWORKING (20) ===
    "player name tag above head",
    "chat message bubble",
    "emote animation system",
    "team color indicator",
    "voice chat proximity",
    "lobby room system",
    "matchmaking queue",
    "player ready state",
    "sync transform over network",
    "interpolate remote player",
    "spawn point assignment",
    "kill death score tracking",
    "respawn countdown",
    "spectator camera",
    "vote kick system",
    "friend list display",
    "party invite system",
    "ping indicator",
    "server browser list",
    "host migration handling",
    
    # === CRAZY COMPLEX BEHAVIORS (26) ===
    # Multi-component chaos
    "object that spins faster and faster while playing a sound that gets louder, and when it reaches maximum speed it explodes into 10 smaller pieces that each bounce around randomly while playing their own unique sound effects, and the original object's material color shifts from blue to red during the spin-up phase",
    "gravity well that pulls nearby rigidbodies toward it with increasing force based on distance, plays a low-frequency rumble that gets louder as objects get closer, changes the light intensity based on how many objects are currently being pulled, and creates particle effects at the point where objects collide with the center",
    "object that becomes a musical instrument - when you collide with it, it plays a note based on where you hit it (top plays high notes, bottom plays low notes), the object's color shifts to match the note's frequency, it vibrates with the sound, and leaves a trail of particles that fade out",
    
    # State machine madness
    "creature with 5 states: idle (sits still, plays ambient breathing), curious (slowly approaches player if within 10 units, plays questioning sound), scared (runs away from player, plays panic sound, color turns red), aggressive (chases player, plays roar, color turns dark), and exhausted (stops moving, plays tired sound, color fades to gray)",
    "door with locked, unlocking, open, and closing states - when locked it shakes slightly, when unlocking it plays key-turning sound for 3 seconds, when open it stays for 10 seconds then auto-closes, the door's material emission color changes with each state",
    
    # Physics nightmares
    "bouncy ball that every time it hits a surface bounces higher than the previous bounce, plays a pitch that increases with each bounce, changes color based on bounce count, and leaves a temporary trail - after 10 bounces it explodes into confetti particles",
    "object that floats upward when player is near (within 5 units) but falls faster when player is far away, plays a humming sound that gets higher pitched as it rises and lower as it falls, rotates opposite to its vertical movement direction",
    "physics-based pendulum that swings naturally but when it reaches the bottom of its swing it applies an explosive force to any rigidbody it touches, plays a bell sound, and creates a shockwave particle effect",
    
    # Time-based complexity
    "time bomb that beeps every second with the beep frequency increasing as time runs down, the object's color pulses red in sync with beeps and shakes more violently as time decreases, after 10 seconds it explodes with massive force affecting all rigidbodies in a 20-unit radius",
    "object that gradually transforms over 30 seconds: smoothly rotates 360 degrees, scales from 1x to 2x and back, changes color through the rainbow spectrum, moves in a figure-8 pattern, and plays a continuous musical note that shifts pitch based on transformation phase",
    
    # Audio-visual symphony
    "visualizer that reacts to audio: object's scale increases proportionally to audio volume, color shifts based on audio frequency (low red, mid green, high blue), rotates based on audio waveform, spawns particles that move outward in sync with beat",
    "singing crystal that when touched plays a sequence of 5 musical notes in a chord progression, each note lights up a different part of the crystal, the entire crystal pulses with the rhythm, after the sequence completes it creates a burst of light particles",
    
    # Conditional logic chains
    "pressure plate system: when weight is applied it plays click sound, changes color to green, starts 5-second timer - if weight stays for 5 seconds it plays success sound and spawns reward object, each pressure plate remembers how many times it's been activated",
    "combo system: hitting object once makes it glow yellow and play low note, second hit within 2 seconds makes it glow orange and play higher note, third hit makes it glow red, fourth hit creates explosion effect and spawns 5 bonus objects - missing the 2-second window resets combo",
    
    # Unusual combinations
    "portal that when entered plays whoosh sound, rotates player 180 degrees instantly, reverses velocity, changes color to random hue, scales to 0.5x size, and teleports to random location within 50 units while creating visual distortion effect",
    "mimic chest that looks normal until player gets within 3 units, then opens its lid, reveals glowing eyes - if touched it snaps shut, applies strong force pushing player away, and spawns 3 smaller mimic objects that chase player",
    
    # Environmental interactions
    "weather controller that when activated spawns rain particles falling downward, plays rain sound effects, makes all objects dampen (reduce bounce), changes ambient lighting to darker, and after 10 seconds spawns lightning bolts with thunder sounds",
    "gravity field that inverts gravity for all objects within its radius, plays space-warp sound, creates visual distortion effect - objects entering should smoothly transition gravity direction over 1 second",
    
    # Meta/reflexive behaviors
    "script that counts its own Update calls and every 1000 calls plays a different sound, changes its material, and spawns a clone of itself - each generation plays slightly higher-pitched sound than parent",
    "self-modifying object that every collision increases its mass by 10%, plays growth sound, scales up by 5%, adds random color tint - after 10 collisions it splits into two objects with half the accumulated properties",
    
    # Extreme edge cases
    "object that checks if current time is between 2 AM and 4 AM, if true makes object glow, play spooky sounds, and move erratically - also checks if exactly 7 other Rigidbody objects exist in scene",
    "quantum object that when observed (player looking at it via raycast) collapses to one state (visible, plays observation sound), when not observed exists in superposition (flickers between positions, plays quantum hum, translucent material)",
    
    # Performance stress test
    "particle fountain that spawns 100 particles per second each with own physics, unique color based on spawn time - each particle tracks nearest other particle and creates visual line if within 2 units",
    "chain reaction system: touching object A explodes and spawns 5 objects of type B in circle, each B when touched spawns 3 objects of type C, each C spawns 2 of type D, each D spawns 1 of type E which plays victory sound",
    
    # Absolutely bonkers
    "recursive reality object that contains miniature version of entire scene inside it as texture on material, when zooming in it reveals the miniature scene also contains same object creating infinite recursion, each level plays different musical note",
    "chaos engine that randomly selects from 20 different behaviors every 3 seconds (spin, bounce, scale pulse, color shift, audio play, particle burst, force application, teleport, rotation etc) - never repeats same behavior twice in a row, after 60 seconds settles into calm meditative state",
]

# ============================================================================
# PROMPTS FOR CLAUDE
# ============================================================================

GENERATE_CODE_SYSTEM = """You are an expert Unity C# developer. Generate production-quality MonoBehaviour scripts.

CRITICAL REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, FixedUpdate, OnTriggerEnter, etc.)
2. Use ONLY Unity's standard API - no custom classes like PlayerHealth, GameManager, etc.
3. Code must be COMPLETELY SELF-CONTAINED - define any helper classes within the file
4. Include [SerializeField] for inspector-exposed private fields
5. Add [Header("...")] attributes to organize inspector
6. Handle null checks and edge cases properly
7. Use proper naming conventions (PascalCase for public, _camelCase for private)

FORBIDDEN - Do NOT reference these undefined classes:
- PlayerHealth, PlayerController, PlayerMovement, PlayerInventory
- GameManager, ScoreManager, UIManager
- Any class not defined in this script or Unity's API

If you need player interaction, use tags: other.CompareTag("Player")
If you need events, use UnityEvents or delegates defined in the script.

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
    parser = argparse.ArgumentParser(description="Generate calibration examples using Claude Sonnet")
    parser.add_argument("--count", type=int, default=0, help="Number of examples (0 = all available prompts)")
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
    
    # Select prompts (0 = all)
    count = args.count if args.count > 0 else len(BEHAVIOR_PROMPTS)
    prompts = BEHAVIOR_PROMPTS[:count]
    print(f"\n  Total prompts available: {len(BEHAVIOR_PROMPTS)}")
    if count > len(BEHAVIOR_PROMPTS):
        print(f"  Note: Requested {count}, using all {len(BEHAVIOR_PROMPTS)} available")
    
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


