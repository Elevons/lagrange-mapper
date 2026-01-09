"""
Test Script: Tool-Based RAG vs Doc-Based RAG
Tests whether pre-built code patterns work better than API documentation
"""

import json
import os
import numpy as np
import requests
from dataclasses import dataclass
from typing import List, Dict, Optional
from sentence_transformers import SentenceTransformer
from dotenv import load_dotenv

load_dotenv()

# Config
LLM_URL = "http://localhost:1234/v1/chat/completions"
CLAUDE_API_KEY = os.environ.get("ANTHROPIC_API_KEY", "")

# Sample tools extracted from WaveformRAG
TOOLS = {
    "TRANSFORM_ROTATE": {
        "code": "transform.Rotate(Vector3.up * speed * Time.deltaTime);",
        "description": "Rotates the Transform of a GameObject by the specified Euler angles.",
        "patterns": ["rotate", "spin", "turn", "twist", "orientation"]
    },
    "TRANSFORM_TRANSLATE": {
        "code": "transform.Translate(direction * speed * Time.deltaTime);",
        "description": "Moves the transform in the direction and distance of translation.",
        "patterns": ["move", "shift", "translate", "position", "displace"]
    },
    "RIGIDBODY_ADDFORCE": {
        "code": "rb.AddForce(direction * force, ForceMode.Impulse);",
        "description": "Applies a force to a Rigidbody, causing it to accelerate.",
        "patterns": ["push", "force", "launch", "thrust", "propel", "jump"]
    },
    "RIGIDBODY_VELOCITY": {
        "code": "rb.velocity = new Vector3(x, y, z);",
        "description": "The velocity vector of the rigidbody.",
        "patterns": ["velocity", "speed", "movement", "fast", "slow"]
    },
    "VECTOR3_LERP": {
        "code": "Vector3.Lerp(start, end, t)",
        "description": "Linearly interpolates between two 3D positions.",
        "patterns": ["smooth", "interpolate", "lerp", "gradual", "ease", "transition"]
    },
    "COLOR_LERP": {
        "code": "Color.Lerp(startColor, endColor, t)",
        "description": "Linearly interpolates between two colors.",
        "patterns": ["color", "fade", "blend", "tint", "hue", "gradient"]
    },
    "AUDIOSOURCE_PLAY": {
        "code": "audioSource.Play();",
        "description": "Plays the clip assigned to the AudioSource.",
        "patterns": ["play", "sound", "audio", "music", "noise", "hear"]
    },
    "AUDIOSOURCE_VOLUME": {
        "code": "audioSource.volume = volumeLevel;",
        "description": "The volume of the audio source (0.0 to 1.0).",
        "patterns": ["volume", "loud", "quiet", "louder", "softer", "mute"]
    },
    "OBJECT_INSTANTIATE": {
        "code": "Instantiate(prefab, position, rotation);",
        "description": "Clones the object and returns the clone.",
        "patterns": ["spawn", "create", "instantiate", "clone", "duplicate", "generate"]
    },
    "OBJECT_DESTROY": {
        "code": "Destroy(gameObject);",
        "description": "Removes a GameObject from the scene.",
        "patterns": ["destroy", "remove", "delete", "kill", "dispose", "explode"]
    },
    "TIME_DELTATIME": {
        "code": "Time.deltaTime",
        "description": "The interval in seconds from the last frame to the current one.",
        "patterns": ["time", "frame", "delta", "elapsed", "duration"]
    },
    "MATERIAL_COLOR": {
        "code": "material.color = newColor;",
        "description": "Sets the main color of the Material.",
        "patterns": ["material", "color", "tint", "appearance", "surface"]
    },
    "GAMEOBJECT_GETCOMPONENT": {
        "code": "GetComponent<ComponentType>();",
        "description": "Gets a reference to a component attached to a GameObject.",
        "patterns": ["component", "get", "access", "reference", "attached"]
    }
}


@dataclass
class ToolMatch:
    key: str
    code: str
    description: str
    score: float


class ToolRAG:
    """Simple tool-based RAG using sentence embeddings"""
    
    def __init__(self):
        print("Loading embedding model...")
        self.model = SentenceTransformer('all-MiniLM-L6-v2')
        self.tools = TOOLS
        self._build_index()
    
    def _build_index(self):
        """Pre-compute embeddings for all tool patterns"""
        self.tool_texts = []
        self.tool_keys = []
        
        for key, tool in self.tools.items():
            # Combine patterns and description for matching
            text = tool["description"] + " " + " ".join(tool["patterns"])
            self.tool_texts.append(text)
            self.tool_keys.append(key)
        
        self.embeddings = self.model.encode(self.tool_texts, convert_to_numpy=True)
        print(f"Indexed {len(self.tools)} tools")
    
    def search(self, query: str, top_k: int = 5) -> List[ToolMatch]:
        """Find tools matching the query"""
        query_emb = self.model.encode(query, convert_to_numpy=True)
        
        # Cosine similarity
        similarities = np.dot(self.embeddings, query_emb)
        
        # Get top matches
        indices = np.argsort(similarities)[::-1][:top_k]
        
        results = []
        for idx in indices:
            key = self.tool_keys[idx]
            tool = self.tools[key]
            results.append(ToolMatch(
                key=key,
                code=tool["code"],
                description=tool["description"],
                score=float(similarities[idx])
            ))
        
        return results


def generate_with_tools(prompt: str, tools: List[ToolMatch]) -> Optional[str]:
    """Generate code using matched tools"""
    
    tools_context = "\n".join([
        f"- {t.key}: `{t.code}` - {t.description}"
        for t in tools
    ])
    
    system_prompt = """You are a Unity C# expert. Generate MonoBehaviour scripts using ONLY the provided tools.

CRITICAL RULES:
1. Use ONLY the code patterns shown below - do not invent APIs
2. Adapt the patterns to fit the task (change variable names, combine patterns)
3. Output complete, valid C# code
4. No markdown code blocks"""

    user_content = f"""Generate a Unity script for: {prompt}

AVAILABLE TOOLS (use ONLY these):
{tools_context}

Generate the complete MonoBehaviour script:"""

    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": "local-model",
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": user_content}
                ],
                "temperature": 0.7,
                "max_tokens": 2000
            },
            timeout=60
        )
        
        if response.status_code != 200:
            return None
        
        code = response.json()["choices"][0]["message"]["content"]
        # Clean markdown if present
        if code.startswith("```"):
            lines = code.split("\n")
            lines = [l for l in lines if not l.startswith("```")]
            code = "\n".join(lines)
        return code
        
    except Exception as e:
        print(f"Error: {e}")
        return None


def generate_oneshot(prompt: str) -> Optional[str]:
    """Generate code without tools (baseline)"""
    
    system_prompt = """You are a Unity C# expert. Generate complete MonoBehaviour scripts.
Output ONLY valid C# code - no markdown, no explanations."""

    try:
        response = requests.post(
            LLM_URL,
            json={
                "model": "local-model",
                "messages": [
                    {"role": "system", "content": system_prompt},
                    {"role": "user", "content": f"Create a Unity script that: {prompt}"}
                ],
                "temperature": 0.7,
                "max_tokens": 2000
            },
            timeout=60
        )
        
        if response.status_code != 200:
            return None
        
        code = response.json()["choices"][0]["message"]["content"]
        if code.startswith("```"):
            lines = code.split("\n")
            lines = [l for l in lines if not l.startswith("```")]
            code = "\n".join(lines)
        return code
        
    except Exception as e:
        print(f"Error: {e}")
        return None


def grade_with_claude(prompt: str, oneshot_code: str, tool_code: str, tools_used: List[ToolMatch]) -> Optional[Dict]:
    """Grade both approaches"""
    if not CLAUDE_API_KEY:
        print("No API key - skipping grading")
        return None
    
    tools_list = ", ".join([t.key for t in tools_used])
    
    grading_prompt = f"""Compare two Unity C# scripts generated for this task:

TASK: {prompt}

=== ONESHOT (no guidance) ===
{oneshot_code}

=== TOOL-BASED (given these tools: {tools_list}) ===
{tool_code}

Rate each on:
1. correctness: Will it compile and work? Are APIs real? (1-10)
2. hallucination: Does it invent non-existent APIs? (1-10, higher = less hallucination)
3. completeness: Does it implement the full requirement? (1-10)

Respond with ONLY this JSON:
{{
  "oneshot": {{"correctness": X, "hallucination": X, "completeness": X, "total": X}},
  "tool_based": {{"correctness": X, "hallucination": X, "completeness": X, "total": X}},
  "winner": "oneshot" or "tool_based",
  "analysis": "One sentence on which approach produced more valid code"
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
                "max_tokens": 500,
                "messages": [{"role": "user", "content": grading_prompt}]
            },
            timeout=30
        )
        
        if response.status_code != 200:
            return None
        
        content = response.json()["content"][0]["text"]
        start = content.find("{")
        end = content.rfind("}") + 1
        if start >= 0 and end > start:
            return json.loads(content[start:end])
        return None
        
    except Exception as e:
        print(f"Grading error: {e}")
        return None


def test_prompt(prompt: str, rag: ToolRAG):
    """Test a single prompt"""
    print(f"\n{'='*60}")
    print(f"PROMPT: {prompt}")
    print("="*60)
    
    # Find matching tools
    print("\n[1] Finding matching tools...")
    tools = rag.search(prompt, top_k=5)
    for t in tools:
        print(f"  {t.key}: {t.score:.2f}")
    
    # Generate with tools
    print("\n[2] Generating with TOOLS...")
    tool_code = generate_with_tools(prompt, tools)
    if not tool_code:
        print("  Failed!")
        return None
    print(f"  Generated {len(tool_code)} chars")
    
    # Generate oneshot
    print("\n[3] Generating ONESHOT...")
    oneshot_code = generate_oneshot(prompt)
    if not oneshot_code:
        print("  Failed!")
        return None
    print(f"  Generated {len(oneshot_code)} chars")
    
    # Grade
    print("\n[4] Grading with Claude...")
    grade = grade_with_claude(prompt, oneshot_code, tool_code, tools)
    
    if grade:
        winner = grade.get("winner", "?")
        os_total = grade.get("oneshot", {}).get("total", "?")
        tb_total = grade.get("tool_based", {}).get("total", "?")
        print(f"\n  WINNER: {winner.upper()}")
        print(f"  Oneshot: {os_total}/30")
        print(f"  Tool-based: {tb_total}/30")
        print(f"  Analysis: {grade.get('analysis', '')}")
    
    return {
        "prompt": prompt,
        "tools_matched": [{"key": t.key, "score": t.score} for t in tools],
        "oneshot_code": oneshot_code,
        "tool_code": tool_code,
        "grade": grade
    }


def main():
    """Run tests"""
    test_prompts = [
        "Make an object spin continuously",
        "Create something that moves toward the player and plays a sound",
        "Build a script that changes color over time and spawns particles",
    ]
    
    rag = ToolRAG()
    results = []
    
    for prompt in test_prompts:
        result = test_prompt(prompt, rag)
        if result:
            results.append(result)
    
    # Summary
    print("\n" + "="*60)
    print("SUMMARY")
    print("="*60)
    
    graded = [r for r in results if r.get("grade")]
    if graded:
        oneshot_wins = sum(1 for r in graded if r["grade"].get("winner") == "oneshot")
        tool_wins = sum(1 for r in graded if r["grade"].get("winner") == "tool_based")
        
        print(f"Oneshot wins: {oneshot_wins}")
        print(f"Tool-based wins: {tool_wins}")
        
        # Average hallucination scores
        os_hall = [r["grade"]["oneshot"]["hallucination"] for r in graded if r["grade"].get("oneshot")]
        tb_hall = [r["grade"]["tool_based"]["hallucination"] for r in graded if r["grade"].get("tool_based")]
        
        if os_hall and tb_hall:
            print(f"\nAvg Hallucination Score (higher = better):")
            print(f"  Oneshot: {sum(os_hall)/len(os_hall):.1f}/10")
            print(f"  Tool-based: {sum(tb_hall)/len(tb_hall):.1f}/10")
    
    # Save results
    results_dir = os.path.join(os.path.dirname(__file__), "prompt_test_results")
    os.makedirs(results_dir, exist_ok=True)
    results_path = os.path.join(results_dir, "tool_rag_results.json")
    with open(results_path, "w") as f:
        json.dump(results, f, indent=2)
    print(f"\nResults saved to {results_path}")


if __name__ == "__main__":
    main()


