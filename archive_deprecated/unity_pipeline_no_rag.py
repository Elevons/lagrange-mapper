"""
Minimal Unity Pipeline - IR to C# without RAG
Just the core two-step generation: Prompt -> IR JSON -> C# Code
"""

import json
import requests
from dataclasses import dataclass
from typing import Optional, Dict

DEFAULT_LLM_URL = "http://localhost:1234/v1/chat/completions"
DEFAULT_TEMPERATURE = 0.7

# === PROMPTS ===

IR_SYSTEM_PROMPT = """You are a helpful assistant. Output only valid JSON."""

CODE_SYSTEM_PROMPT = """You are a Unity C# expert. Generate clean, working MonoBehaviour scripts.

Rules:
1. Output ONLY valid C# code - no markdown, no explanation
2. Use proper Unity patterns (GetComponent in Start, null checks)
3. Use instance references (rb.velocity) not static (Rigidbody.velocity)
4. Include [RequireComponent] for required components
5. Use [SerializeField] for inspector-exposed private fields
6. Keep code simple and readable
7. IMPORTANT: Do NOT reference classes that aren't defined in this script or Unity's API
8. All functionality must be SELF-CONTAINED - define any helper classes you need"""


@dataclass
class PipelineResult:
    """Result of the minimal pipeline"""
    prompt: str
    ir_json: Optional[Dict] = None
    code: Optional[str] = None
    error: Optional[str] = None


class UnityPipelineNoRAG:
    """Minimal two-step pipeline: Prompt -> IR -> C#"""
    
    def __init__(self, llm_url: str = DEFAULT_LLM_URL, verbose: bool = False):
        self.llm_url = llm_url
        self.verbose = verbose
    
    def generate(self, prompt: str) -> PipelineResult:
        """Run the complete pipeline"""
        result = PipelineResult(prompt=prompt)
        
        # Step 1: Generate IR
        if self.verbose:
            print("Step 1: Generating IR...")
        
        ir_json = self._generate_ir(prompt)
        if not ir_json:
            result.error = "Failed to generate IR"
            return result
        result.ir_json = ir_json
        
        if self.verbose:
            print(f"  Class: {ir_json.get('class_name', 'unknown')}")
            print(f"  Behaviors: {len(ir_json.get('behaviors', []))}")
        
        # Step 2: Generate C# from IR
        if self.verbose:
            print("Step 2: Generating C#...")
        
        code = self._generate_code(ir_json)
        if not code:
            result.error = "Failed to generate code"
            return result
        result.code = code
        
        if self.verbose:
            print(f"  Generated {len(code)} chars")
        
        return result
    
    def _generate_ir(self, prompt: str) -> Optional[Dict]:
        """Generate IR JSON from natural language prompt"""
        try:
            user_content = f"""Describe this Unity behavior as JSON with class_name, components, fields, and behaviors.

Request: {prompt}

Output format:
{{
  "class_name": "BehaviorName",
  "components": ["Rigidbody", "AudioSource"],
  "fields": [{{"name": "speed", "type": "float", "default": 5.0}}],
  "behaviors": [{{"name": "movement", "trigger": "when key pressed", "actions": [{{"action": "move forward"}}]}}]
}}

JSON:"""

            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": IR_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 1500
                },
                timeout=60
            )
            
            if response.status_code != 200:
                return None
            
            content = response.json()["choices"][0]["message"]["content"]
            content = self._clean_json(content)
            return json.loads(content)
            
        except Exception as e:
            if self.verbose:
                print(f"  IR error: {e}")
            return None
    
    def _generate_code(self, ir_json: Dict) -> Optional[str]:
        """Generate C# code from IR JSON"""
        try:
            user_content = f"""Generate a Unity C# MonoBehaviour based on this specification:

Class: {ir_json.get('class_name', 'GeneratedBehavior')}
Components: {', '.join(ir_json.get('components', [])) or 'None'}

Fields:
{json.dumps(ir_json.get('fields', []), indent=2)}

Behaviors:
{json.dumps(ir_json.get('behaviors', []), indent=2)}

Generate complete, working C# code. No markdown."""

            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": CODE_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 2500
                },
                timeout=60
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"  Code error: {e}")
            return None
    
    def _clean_json(self, text: str) -> str:
        """Extract JSON from response"""
        text = text.strip()
        if text.startswith("```"):
            lines = text.split("\n")
            lines = [l for l in lines if not l.startswith("```")]
            text = "\n".join(lines)
        # Find JSON bounds
        start = text.find("{")
        end = text.rfind("}") + 1
        if start >= 0 and end > start:
            return text[start:end]
        return text
    
    def _clean_code(self, text: str) -> str:
        """Extract C# code from response"""
        text = text.strip()
        if text.startswith("```"):
            lines = text.split("\n")
            # Remove first and last code fence lines
            if lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].startswith("```"):
                lines = lines[:-1]
            text = "\n".join(lines)
        return text


def main():
    """Test the minimal pipeline"""
    import sys
    
    # Default test prompt
    prompt = "Create a script that makes an object spin and change color over time"
    
    if len(sys.argv) > 1:
        prompt = " ".join(sys.argv[1:])
    
    print(f"Prompt: {prompt}\n")
    print("=" * 60)
    
    pipeline = UnityPipelineNoRAG(verbose=True)
    result = pipeline.generate(prompt)
    
    if result.error:
        print(f"\nError: {result.error}")
        return
    
    print("\n" + "=" * 60)
    print("IR JSON:")
    print("=" * 60)
    print(json.dumps(result.ir_json, indent=2))
    
    print("\n" + "=" * 60)
    print("C# CODE:")
    print("=" * 60)
    print(result.code)


if __name__ == "__main__":
    main()


