"""
Direct model test - see exactly what the model outputs.
"""

import requests
import json

LLM_URL = "http://localhost:1234/v1/chat/completions"

IR_SYSTEM_PROMPT = """You are a Unity behavior specification generator. Output structured JSON only.

STRUCTURE:
{
  "class_name": "BehaviorName",
  "components": ["Rigidbody", "AudioSource", ...],
  "fields": [{"name": "speed", "type": "float", "default": 5.0}],
  "behaviors": [{"name": "...", "trigger": "...", "actions": [...]}]
}

RULES:
1. Components must be exact Unity class names: Rigidbody, AudioSource, Collider, Animator, Text, Image, Canvas, RectTransform
2. Field types must be C# types: float, int, bool, string, Vector3, AudioClip, GameObject, Transform
3. Default values must be actual values: 10, null, true, 0.5 (not descriptions)
4. Actions are verb phrases: "play audio", "apply force", "destroy object"

Output ONLY valid JSON. No markdown, no explanations."""

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Generate a complete MonoBehaviour script.
Output ONLY the C# code. No markdown, no explanations."""

def test_prompt(system_prompt, user_prompt, label):
    """Test a single prompt and show raw response."""
    print("")
    print("=" * 70)
    print("TEST: " + label)
    print("=" * 70)
    print("System: " + system_prompt[:100] + "...")
    print("User: " + user_prompt[:100] + "...")
    print("-" * 70)
    
    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": "local-model",
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_prompt}
                ],
                "temperature": 0.4,
                "max_tokens": 2000
            },
            timeout=120
        )
        
        if response.status_code != 200:
            print("[X] HTTP Error: " + str(response.status_code))
            print(response.text[:500])
            return
        
        content = response.json()["choices"][0]["message"]["content"]
        
        print("")
        print("RAW RESPONSE (" + str(len(content)) + " chars):")
        print("-" * 70)
        # Encode safely for Windows console
        safe_content = content.encode('ascii', 'replace').decode('ascii')
        print(safe_content)
        print("-" * 70)
        
        # Try to detect what type of output it is
        if content.strip().startswith("{"):
            print("[OK] Looks like JSON")
        elif "class " in content and "MonoBehaviour" in content:
            print("[!!] Looks like C# code (not JSON!)")
        elif content.strip().startswith("```"):
            print("[!!] Looks like markdown code block")
        else:
            print("[?] Unknown format")
            
    except Exception as e:
        print("[X] Error: " + str(e))


def main():
    test_prompt_text = "Create a spinning coin that adds points when collected"
    
    print("")
    print("=" * 70)
    print("DIRECT MODEL TEST")
    print("=" * 70)
    print("")
    print("LLM URL: " + LLM_URL)
    print("Test prompt: " + test_prompt_text)
    
    # Test 1: IR JSON generation
    test_prompt(
        IR_SYSTEM_PROMPT,
        test_prompt_text,
        "IR JSON Generation"
    )
    
    # Test 2: Code generation
    test_prompt(
        CODE_SYSTEM_PROMPT,
        test_prompt_text,
        "Code Generation"
    )
    
    # Test 3: Just ask for JSON directly
    test_prompt(
        "You are a helpful assistant. Output only valid JSON.",
        "Describe this Unity behavior as JSON with class_name, components, fields, and behaviors: " + test_prompt_text,
        "Simple JSON Request"
    )
    
    # Test 4: No system prompt
    test_prompt(
        "",
        "Output a JSON object describing a Unity script for: " + test_prompt_text + "\n\nJSON:",
        "No System Prompt"
    )
    
    print("")
    print("=" * 70)
    print("TESTS COMPLETE")
    print("=" * 70)


if __name__ == "__main__":
    main()
