"""
Unity Script Assembler V2 - Direct LLM Generation
Converts IR JSON to Unity C# code using direct LLM prompting (no RAG)
"""
import json
import requests
from typing import Dict, Optional
from dataclasses import dataclass


@dataclass
class AssemblyResult:
    """Result of C# code assembly"""
    success: bool
    code: str
    class_name: str
    error: Optional[str] = None


class UnityScriptAssemblerV2:
    """Converts IR JSON to Unity C# code using direct LLM generation"""
    
    def __init__(self, llm_url: str = "http://localhost:1234/v1/chat/completions", 
                 verbose: bool = False, temperature: float = 0.3):
        self.llm_url = llm_url
        self.verbose = verbose
        self.temperature = temperature
    
    def assemble(self, ir_json: Dict) -> AssemblyResult:
        """
        Convert IR JSON to complete Unity C# MonoBehaviour script.
        
        Args:
            ir_json: The intermediate representation JSON
            
        Returns:
            AssemblyResult with generated C# code
        """
        class_name = ir_json.get("class_name", "UnityBehavior")
        
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"Generating C# for: {class_name}")
            print(f"{'='*60}")
        
        # Build the prompt
        prompt = self._build_prompt(ir_json)
        
        if self.verbose:
            print(f"\nPrompt length: {len(prompt)} chars")
        
        # Call LLM
        try:
            code = self._call_llm(prompt)
            
            if self.verbose:
                print(f"Generated {len(code)} chars of code")
            
            return AssemblyResult(
                success=True,
                code=code,
                class_name=class_name
            )
            
        except Exception as e:
            return AssemblyResult(
                success=False,
                code="",
                class_name=class_name,
                error=str(e)
            )
    
    def _build_prompt(self, ir_json: Dict) -> str:
        """Build the LLM prompt from IR JSON"""
        
        # Format IR nicely
        ir_str = json.dumps(ir_json, indent=2)
        
        prompt = f"""You are a Unity C# code generator. Convert this behavior specification into a complete, working Unity MonoBehaviour script.

EXAMPLES OF CORRECT PATTERNS:

Example 1 - Audio Field:
IR: {{"name": "explosionSound", "type": "AudioClip", "default": null}}
C#: public AudioClip explosionSound;
Usage: audioSource.PlayOneShot(explosionSound);

Example 2 - Prefab Field:
IR: {{"name": "enemyPrefab", "type": "GameObject", "default": null}}
C#: public GameObject enemyPrefab;
Usage: Instantiate(enemyPrefab, position, Quaternion.identity);

Example 3 - Collision Detection:
IR: {{"trigger": "detect collision with another object"}}
C#: void OnCollisionEnter(Collision collision) {{ ... }}

Example 4 - Audio Action:
IR: {{"action": "play an audio clip", "target": "explosionSound"}}
C#: GetComponent<AudioSource>().PlayOneShot(explosionSound);

Example 5 - Trigger Detection:
IR: {{"trigger": "detect player entering trigger zone"}}
C#: void OnTriggerEnter(Collider other) {{ if (other.CompareTag("Player")) {{ ... }} }}

INPUT JSON:
{ir_str}

INSTRUCTIONS:
1. Create a MonoBehaviour class with the specified "class_name"
2. Add comments listing required components from "components" array
3. Declare all "fields" as public variables with their types and default values
4. For each behavior in "behaviors":
   - Map the "trigger" to the appropriate Unity lifecycle method or event:
     * "detect collision with trigger zone" → OnTriggerEnter(Collider other)
     * "detect collision with another object" → OnCollisionEnter(Collision collision)
     * "execute logic every frame" → Update()
     * "execute logic on start" → Start()
     * "call repeatedly at a fixed interval" → InvokeRepeating() or coroutine
   - Implement each "action" using the correct Unity API:
     * "get all rigidbodies in a sphere" → Physics.OverlapSphere()
     * "apply force" / "apply explosion force" → Rigidbody.AddForce() or AddExplosionForce()
     * "play audio" / "play sound" → AudioSource.Play() or PlayOneShot()
     * "spawn" / "create instance" → Instantiate()
     * "enable/disable component" → component.enabled = true/false
     * "destroy object" → Destroy()
     * "find object" → GameObject.Find() or FindObjectOfType()
5. Use proper C# syntax, Unity conventions, and add necessary using statements
6. Make the code production-ready and compilable
7. Add helpful comments for clarity

CATEGORICAL RULES - Match intent patterns to Unity paradigms:

1. TIME-BASED MOTION (keywords: "over X seconds", "for duration", "smoothly", "gradually"):
   → ALWAYS use Coroutine + Vector3.Lerp or DOTween
   → NEVER use AddForce for timed/smooth movement
   → Pattern: StartCoroutine(MoveOverTime()); with while loop and Time.deltaTime

2. INSTANT MOTION (keywords: "push", "launch", "explode", "impulse"):
   → Use Rigidbody.AddForce with ForceMode.Impulse or ForceMode.Force
   → Physics-based, not time-controlled

3. MATERIAL/VISUAL CHANGES (keywords: "change color", "set material", "tint"):
   → Use: GetComponent<Renderer>().material.color = Color.green;
   → NEVER use .tint (doesn't exist)
   → Modify existing material, don't create new ones unless necessary

4. PROPERTY TRANSITIONS (keywords: "fade", "animate", "transition over time"):
   → Use Coroutine with Mathf.Lerp for floats, Color.Lerp for colors
   → Or use animation/animator if complex

5. ONE-SHOT vs CONTINUOUS:
   → "when X happens" / "on trigger" → Single execution in event handler
   → "while X" / "continuously" → Update() loop or Coroutine

IMPORTANT:
- Follow the patterns shown in the examples above
- Fields with type "AudioClip" → declare as AudioClip, use with PlayOneShot()
- Fields with type "GameObject" → declare as GameObject, use with Instantiate()
- Fields with type "string" → keep as string (for names/IDs only)
- Use GetComponent<T>() when accessing components
- Handle null checks where appropriate
- Use proper Unity types: Vector3, Quaternion, ForceMode, etc.
- If playing audio but AudioSource not in components, declare: private AudioSource audioSource; and initialize in Start()
- If using physics but Rigidbody not in components, declare: private Rigidbody rb; and initialize in Start()

OUTPUT only the complete C# script, no markdown, no explanations:"""
        
        return prompt
    
    def _call_llm(self, prompt: str) -> str:
        """Call the LLM API"""
        response = requests.post(
            self.llm_url,
            json={
                "model": "local-model",
                "messages": [{"role": "user", "content": prompt}],
                "temperature": self.temperature,
                "max_tokens": 4000,
                "stream": False
            },
            timeout=120
        )
        
        if response.status_code != 200:
            raise Exception(f"LLM API error: {response.status_code} - {response.text}")
        
        result = response.json()
        code = result['choices'][0]['message']['content']
        
        # Clean up markdown if present
        if "```" in code:
            # Extract code from markdown blocks
            lines = code.split('\n')
            in_code_block = False
            clean_lines = []
            
            for line in lines:
                if line.strip().startswith('```'):
                    in_code_block = not in_code_block
                    continue
                if in_code_block or (not any(line.strip().startswith(x) for x in ['```'])):
                    if not line.strip().startswith('```'):
                        clean_lines.append(line)
            
            code = '\n'.join(clean_lines).strip()
        
        return code


def test_assembler():
    """Test the assembler with sample IR JSON"""
    
    # Sample IR from the explosion trigger test
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
                    {"action": "for each rigidbody apply force to its transform using explosionForce"},
                    {"action": "play the explosion sound using soundPrefab at this object's position"},
                    {"action": "spawn particle effects using particlePrefab centered at this object's position"}
                ]
            }
        ]
    }
    
    assembler = UnityScriptAssemblerV2(verbose=True)
    result = assembler.assemble(ir_json)
    
    if result.success:
        print(f"\n{'='*60}")
        print("GENERATED C# CODE:")
        print(f"{'='*60}")
        print(result.code)
        print(f"{'='*60}")
    else:
        print(f"Error: {result.error}")


if __name__ == "__main__":
    test_assembler()

