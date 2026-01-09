"""Test LLM's ability to generate Unity C# directly from IR JSON"""
import requests
import json

# LLM endpoint
LLM_URL = "http://localhost:1234/v1/chat/completions"

# The IR JSON from your test (the good one!)
ir_json = {
  "class_name": "ExplosionTrigger",
  "components": ["BoxCollider", "Rigidbody"],
  "fields": [
    {"name": "explosionForce", "type": "float", "default": 50},
    {"name": "radiusMeters", "type": "float", "default": 10},
    {"name": "soundPrefab", "type": "string", "default": ""},
    {"name": "particlePrefab", "type": "string", "default": ""}
  ],
  "behaviors": [
    {
      "name": "detect_player_in_area",
      "trigger": "detect collision with trigger zone",
      "condition": None,
      "actions": [
        {"action": "check if the player is within a radius of this object", "target": None}
      ]
    },
    {
      "name": "apply_explosion_if_triggered",
      "trigger": "after detect_player_in_area detected true",
      "condition": None,
      "actions": [
        {"action": "get all rigidbodies in a sphere within radiusMeters around center", "target": None},
        {"action": "for each rigidbody apply force to its transform using explosionForce for 0.1 second"},
        {"action": "play the explosion sound using soundPrefab at this object's position"},
        {"action": "spawn particle effects using particlePrefab centered at this object's position"}
      ]
    }
  ]
}

# Prompt for the LLM
prompt = f"""You are a Unity C# code generator. Convert this behavior specification into a complete, working Unity MonoBehaviour script.

INPUT JSON:
{json.dumps(ir_json, indent=2)}

INSTRUCTIONS:
1. Create a MonoBehaviour class with the specified name
2. Add the required Unity components (as comments or GetComponent calls)
3. Declare all fields as public with their default values
4. Map each "trigger" to the appropriate Unity lifecycle method:
   - "detect collision with trigger zone" → OnTriggerEnter(Collider other)
   - "every frame" → Update()
   - "on start" → Start()
5. Map each "action" to the correct Unity API calls:
   - "get all rigidbodies in a sphere" → Physics.OverlapSphere()
   - "apply force" → Rigidbody.AddExplosionForce() or AddForce()
   - "play the explosion sound" → AudioSource.PlayOneShot() or similar
   - "spawn particle effects" → Instantiate()
6. Use proper C# syntax, types, and Unity conventions
7. Add necessary using statements
8. Make the code production-ready and compilable

OUTPUT only the complete C# script, no explanations or markdown:"""

print("="*60)
print("TESTING LLM DIRECT CODE GENERATION")
print("="*60)
print(f"\nIR JSON:\n{json.dumps(ir_json, indent=2)}\n")
print("="*60)
print("Calling LLM...")
print("="*60)

# Call LLM
response = requests.post(
    LLM_URL,
    json={
        "model": "local-model",
        "messages": [{"role": "user", "content": prompt}],
        "temperature": 0.3,
        "max_tokens": 2000,
        "stream": False
    },
    timeout=60
)

if response.status_code == 200:
    result = response.json()
    generated_code = result['choices'][0]['message']['content']
    
    print("\nGENERATED C# CODE:")
    print("="*60)
    print(generated_code)
    print("="*60)
    
    # Quick analysis
    print("\n\nQUICK ANALYSIS:")
    print("="*60)
    
    checks = {
        "OnTriggerEnter": "OnTriggerEnter" in generated_code,
        "Physics.OverlapSphere": "OverlapSphere" in generated_code,
        "AddExplosionForce": "AddExplosionForce" in generated_code or "AddForce" in generated_code,
        "AudioSource": "AudioSource" in generated_code or "audio" in generated_code.lower(),
        "Instantiate": "Instantiate" in generated_code,
        "using UnityEngine": "using UnityEngine" in generated_code,
        "MonoBehaviour": "MonoBehaviour" in generated_code,
        "public float explosionForce": "explosionForce" in generated_code,
    }
    
    for check, passed in checks.items():
        status = "✓" if passed else "✗"
        print(f"{status} {check}")
    
    passed_count = sum(checks.values())
    total_count = len(checks)
    score = (passed_count / total_count) * 100
    
    print(f"\nScore: {passed_count}/{total_count} ({score:.0f}%)")
    
    if score >= 75:
        print("\n🎉 LLM generated good Unity code!")
        print("   → Problem is the RAG dataset, not the LLM")
    elif score >= 50:
        print("\n⚠️ LLM generated partial Unity code")
        print("   → LLM knows Unity but needs better prompting")
    else:
        print("\n❌ LLM failed to generate valid Unity code")
        print("   → LLM doesn't know Unity well enough")
        
else:
    print(f"Error: {response.status_code}")
    print(response.text)


