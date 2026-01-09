#!/usr/bin/env python3
"""Test Unity IR steering against code leak patterns"""

import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from code_generation_pipeline.attractor_steering import load_steering

# Test cases: natural language (should pass) vs code leak (should flag)
TEST_CASES = [
    {
        "name": "clean_natural_language",
        "json": '''{
            "trigger": "player touches this",
            "condition": "player is within chaseRange",
            "actions": [{"type": "set_state", "params": {"state": "Chase"}}]
        }''',
        "expected_flag": False
    },
    {
        "name": "code_leak_condition",
        "json": '''{
            "trigger": "on_trigger_enter:Player",
            "condition": "distance(player) < chaseRange",
            "actions": [{"type": "set_state", "params": {"state": "Chase"}}]
        }''',
        "expected_flag": True
    },
    {
        "name": "code_leak_template",
        "json": '''{
            "trigger": "update",
            "actions": [{"type": "rotate", "params": {"y": "{{360 * Time.deltaTime}}"}}]
        }''',
        "expected_flag": True
    },
    {
        "name": "code_leak_operators",
        "json": '''{
            "condition": "key == 'W' || key == 'Up'",
            "actions": [{"type": "set_value", "params": {"value": "full" if key == 'W' else "idle"}}]
        }''',
        "expected_flag": True
    },
    {
        "name": "code_leak_unity_api",
        "json": '''{
            "trigger": "start",
            "actions": [{"type": "set_position", "params": {"position": "Vector3.up"}}]
        }''',
        "expected_flag": True
    },
    {
        "name": "natural_language_complex",
        "json": '''{
            "trigger": "player enters room",
            "condition": "room is not cleared and player has key",
            "actions": [
                {"type": "spawn_enemy", "params": {"enemy_type": "guard", "count": 3}},
                {"type": "play_sound", "params": {"sound": "alert"}},
                {"type": "set_state", "params": {"state": "combat"}}
            ]
        }''',
        "expected_flag": False
    },
    {
        "name": "code_leak_function_call",
        "json": '''{
            "trigger": "update",
            "condition": "normalize(direction) != Vector3.zero",
            "actions": [{"type": "move", "params": {"direction": "normalize(direction)"}}]
        }''',
        "expected_flag": True
    },
    {
        "name": "mixed_natural_with_leak",
        "json": '''{
            "trigger": "player touches this",
            "condition": "player health > 50",
            "actions": [{"type": "heal", "params": {"amount": "max(100 - health, 0)"}}]
        }''',
        "expected_flag": True
    }
]

def main():
    try:
        config_path = os.path.join(os.path.dirname(__file__), "..", "code generation pipeline", "unity_ir_filter_configs")
        steering = load_steering("local-model-unity-ir", config_path)
    except FileNotFoundError:
        print("="*70)
        print("UNITY IR CODE LEAK DETECTION TEST")
        print("="*70)
        print("\n⚠ Filter config not found!")
        print("  Run the pipeline first with PROBE_MODE='unity_ir'")
        print(f"  Expected config at: {os.path.join('code generation pipeline', 'unity_ir_filter_configs', 'local-model-unity-ir', 'filter_config.json')}")
        return
    
    print("="*70)
    print("UNITY IR CODE LEAK DETECTION TEST")
    print("="*70)
    
    passed = 0
    failed = 0
    
    for test in TEST_CASES:
        result = steering.detect_code_leak(test["json"], intensity=0.5)
        
        if result.is_attracted == test["expected_flag"]:
            status = "✓ PASS"
            passed += 1
        else:
            status = "✗ FAIL"
            failed += 1
        
        print(f"\n{status}: {test['name']}")
        print(f"  Expected flag: {test['expected_flag']}")
        print(f"  Got flag: {result.is_attracted}")
        if result.flagged_keywords:
            print(f"  Flagged patterns: {', '.join(result.flagged_keywords[:5])}")
        if result.embedding_score > 0:
            print(f"  Embedding similarity: {result.embedding_score:.3f}")
        if result.triggered_attractors:
            print(f"  Triggered attractors: {', '.join(result.triggered_attractors)}")
    
    print(f"\n{'='*70}")
    print(f"Results: {passed}/{passed+failed} passed")
    
    if failed == 0:
        print("\n✓ All tests passed! Code leak detection is working correctly.")
    else:
        print(f"\n⚠ {failed} test(s) failed. Review the detection logic.")

if __name__ == "__main__":
    main()


