"""
Unity Pipeline Per-Behavior - Multi-Query RAG with Single-Shot Generation

Key insight: Instead of retrieving 8 docs for the entire script, retrieve
docs PER BEHAVIOR using focused queries. Then combine ALL docs and generate
the complete script in ONE LLM call.

Flow:
1. Prompt → IR JSON (same as before)
2. For each behavior in IR:
   a. Build focused query from trigger + actions
   b. Retrieve 6 docs specific to that behavior
   c. Collect all docs (deduplicated)
3. Combine all retrieved docs into rich context
4. Generate COMPLETE C# script in one LLM call with:
   - The IR JSON
   - All collected RAG docs
   - Clear instructions

Benefits:
- 4 behaviors × 6 docs = ~24 docs total (vs 8 monolithic)
- Each query is focused, so docs are more relevant
- LLM sees full class context during generation
- No fragile assembly step
- Natural code flow between methods
"""

import json
import re
import requests
import os
import sys
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass, field

# Add code generation pipeline to path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'code generation pipeline'))

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "code generation pipeline", "unity_rag_db")
DEFAULT_TEMPERATURE = 0.3  # Lower for more deterministic code
DOCS_PER_BEHAVIOR = 6

# ============================================================================
# PROMPTS
# ============================================================================

IR_SYSTEM_PROMPT = """You are a Unity behavior specification generator. Output structured JSON only.

STRUCTURE:
{
  "class_name": "BehaviorName",
  "components": ["Rigidbody", "AudioSource", ...],
  "fields": [{"name": "speed", "type": "float", "default": 5.0}],
  "behaviors": [{"name": "...", "trigger": "...", "actions": [...]}]
}

RULES:
1. Components must be exact Unity class names: Rigidbody, AudioSource, Collider, Animator, ParticleSystem, Light, Renderer
2. Field types must be C# types: float, int, bool, string, Vector3, Color, AudioClip, GameObject, Transform, Material
3. Default values must be actual values: 10, null, true, 0.5 (not descriptions)
4. Each behavior should be a distinct logical unit with clear trigger and actions
5. Actions are verb phrases describing what Unity APIs to use

Output ONLY valid JSON. No markdown, no explanations."""




CODE_GENERATION_PROMPT = """You are a Unity C# script generator. Generate a COMPLETE, working MonoBehaviour script.

You will receive:
1. An IR specification (class name, components, fields, behaviors)
2. Unity API documentation for relevant APIs

OUTPUT RULES:
1. Output ONLY valid C# code - no markdown, no explanations
2. Use the APIs shown in the documentation - if an API isn't documented, use a TODO comment
3. Each behavior becomes a private method called from Update()
4. Use proper Unity patterns: GetComponent in Start(), null checks, Time.deltaTime
5. Use instance variables (rb.velocity) not class references (Rigidbody.velocity)

STRUCTURE:
```
using UnityEngine;

[RequireComponent(typeof(...))]
public class ClassName : MonoBehaviour
{
    // Public fields for inspector
    public float speed = 5f;
    
    // Private component references
    private Rigidbody rb;
    private AudioSource audioSource;
    
    void Start()
    {
        rb = GetComponent<Rigidbody>();
        audioSource = GetComponent<AudioSource>();
    }
    
    void Update()
    {
        BehaviorOne();
        BehaviorTwo();
    }
    
    void BehaviorOne()
    {
        // Implementation using the documented APIs
    }
    
    void BehaviorTwo()
    {
        // Implementation
    }
}
```"""


# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class BehaviorContext:
    """Context for generating a single behavior method"""
    name: str
    trigger: str
    actions: List[str]
    rag_docs: List[any] = field(default_factory=list)
    rag_context: str = ""
    generated_code: Optional[str] = None
    

@dataclass
class PerBehaviorResult:
    """Result of the per-behavior pipeline"""
    success: bool
    ir_json: Optional[Dict] = None
    code: Optional[str] = None
    error: Optional[str] = None
    behaviors_generated: int = 0
    total_docs_retrieved: int = 0
    methods: List[BehaviorContext] = field(default_factory=list)
    rag_doc_names_by_behavior: Optional[Dict[str, List[str]]] = None  # behavior_name -> list of doc names


# ============================================================================
# PIPELINE CLASS
# ============================================================================

class UnityPipelinePerBehavior:
    """
    Per-behavior Unity code generation pipeline.
    
    Key difference from simple pipeline: retrieves RAG docs separately
    for each behavior block, then generates methods individually.
    """
    
    def __init__(self, 
                 llm_url: str = LLM_URL,
                 rag_db_path: str = RAG_DB_PATH,
                 docs_per_behavior: int = DOCS_PER_BEHAVIOR,
                 verbose: bool = False):
        self.llm_url = llm_url
        self.docs_per_behavior = docs_per_behavior
        self.verbose = verbose
        self.rag = None
        
        # Load RAG system
        try:
            from unity_rag_query import UnityRAG
            self.rag = UnityRAG(db_path=rag_db_path, verbose=verbose)
            if verbose:
                print(f"RAG loaded: {len(self.rag.documents)} docs")
        except Exception as e:
            if verbose:
                print(f"RAG not available: {e}")
    
    def generate(self, prompt: str) -> PerBehaviorResult:
        """
        Generate Unity C# code using multi-query RAG with single-shot generation.
        
        1. Generate IR JSON
        2. For each behavior: retrieve focused RAG docs
        3. Combine all docs (deduplicated)
        4. Generate complete script in one LLM call
        """
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"MULTI-QUERY RAG PIPELINE")
            print(f"{'='*60}")
            print(f"Prompt: {prompt[:80]}...")
        
        # Step 1: Generate IR JSON
        if self.verbose:
            print("\n[1/3] Generating IR JSON...")
        
        ir_json = self._generate_ir(prompt)
        if ir_json is None:
            return PerBehaviorResult(success=False, error="Failed to generate IR JSON")
        
        behaviors = ir_json.get("behaviors", [])
        if self.verbose:
            print(f"  → Class: {ir_json.get('class_name')}")
            print(f"  → {len(behaviors)} behaviors to process")
        
        # Step 2: Collect RAG docs for each behavior
        if self.verbose:
            print(f"\n[2/3] Multi-query RAG retrieval ({self.docs_per_behavior} docs per behavior)...")
        
        behavior_contexts = []
        all_docs = {}  # api_name -> doc (for deduplication)
        doc_names_by_behavior = {}
        
        for i, behavior in enumerate(behaviors):
            ctx = self._process_behavior(behavior, ir_json.get("fields", []), i + 1)
            behavior_contexts.append(ctx)
            
            # Collect docs (deduplicate by api_name)
            doc_names_by_behavior[ctx.name] = []
            for doc in ctx.rag_docs:
                doc_names_by_behavior[ctx.name].append(f"{doc.api_name} ({doc.score:.2f})")
                if doc.api_name not in all_docs:
                    all_docs[doc.api_name] = doc
            
            if self.verbose:
                print(f"  → [{i+1}] {ctx.name}: {len(ctx.rag_docs)} docs")
                for doc in ctx.rag_docs[:2]:
                    print(f"       - {doc.api_name} ({doc.score:.2f})")
        
        total_docs = len(all_docs)
        unique_docs = list(all_docs.values())
        
        if self.verbose:
            print(f"  → Total unique docs: {total_docs} (vs 8 in monolithic)")
        
        # Step 3: Generate complete script with combined context
        if self.verbose:
            print("\n[3/3] Generating complete script...")
        
        final_code = self._generate_complete_script(ir_json, unique_docs, behavior_contexts)
        
        if final_code is None:
            return PerBehaviorResult(
                success=False,
                ir_json=ir_json,
                error="Failed to generate script",
                behaviors_generated=0,
                total_docs_retrieved=total_docs
            )
        
        if self.verbose:
            print(f"  → Final script: {len(final_code)} chars")
            print(f"\n{'='*60}")
            print("COMPLETE")
            print(f"{'='*60}")
        
        return PerBehaviorResult(
            success=True,
            ir_json=ir_json,
            code=final_code,
            behaviors_generated=len(behaviors),
            total_docs_retrieved=total_docs,
            methods=behavior_contexts,
            rag_doc_names_by_behavior=doc_names_by_behavior
        )
    
    def _generate_complete_script(self, ir_json: Dict, docs: List, behavior_contexts: List[BehaviorContext]) -> Optional[str]:
        """
        Generate the complete C# script in one LLM call.
        
        Args:
            ir_json: The IR specification
            docs: All retrieved RAG docs (deduplicated)
            behavior_contexts: List of behavior contexts with names/triggers/actions
        """
        try:
            # Build the RAG context
            if docs and self.rag:
                rag_context = self.rag.format_context_for_prompt(docs, max_tokens=4000, include_content=True)
            else:
                rag_context = "(No Unity API documentation available)"
            
            # Build behavior descriptions
            behavior_desc = []
            for ctx in behavior_contexts:
                actions_str = ", ".join(ctx.actions) if ctx.actions else "no specific actions"
                behavior_desc.append(f"- {ctx.name}: When {ctx.trigger}, do: {actions_str}")
            
            # Build the user prompt
            user_content = f"""Generate a complete Unity C# script based on this specification:

=== UNITY API REFERENCE ===
{rag_context}

=== SCRIPT SPECIFICATION ===
Class Name: {ir_json.get('class_name', 'GeneratedBehavior')}
Required Components: {', '.join(ir_json.get('components', [])) or 'None'}

Fields:
{json.dumps(ir_json.get('fields', []), indent=2)}

Behaviors to implement:
{chr(10).join(behavior_desc)}

=== INSTRUCTIONS ===
1. Generate a COMPLETE, syntactically valid C# MonoBehaviour
2. Use ONLY the APIs shown in the reference above
3. If an API isn't in the reference, write: // TODO: [what's needed]
4. Use instance variables (rb.velocity, audioSource.Play()) not class names (Rigidbody.velocity)
5. Each behavior becomes a private method called from Update()
6. Include proper null checks and Unity best practices

Output ONLY the C# code, no markdown code blocks, no explanations."""

            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": CODE_GENERATION_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 3000
                },
                timeout=900
            )
            
            if response.status_code != 200:
                if self.verbose:
                    print(f"  LLM error: {response.status_code}")
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            
            # Clean up the code (remove markdown if present)
            code = self._clean_generated_script(code)
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Script generation error: {e}")
            return None
    
    def _clean_generated_script(self, code: str) -> str:
        """Clean the generated script - just remove markdown wrappers"""
        # Remove markdown code blocks
        code = re.sub(r'```csharp\n?', '', code)
        code = re.sub(r'```\w*\n?', '', code)
        code = re.sub(r'```', '', code)
        return code.strip()
    
    def _generate_ir(self, prompt: str) -> Optional[Dict]:
        """Generate IR JSON from prompt"""
        try:
            from unity_ir_inference import extract_json_from_response
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": IR_SYSTEM_PROMPT},
                        {"role": "user", "content": prompt}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 2000
                },
                timeout=900
            )
            
            if response.status_code != 200:
                return None
            
            content = response.json()["choices"][0]["message"]["content"]
            _, parsed = extract_json_from_response(content)
            
            # Validate that parsed result is a dict, not a list
            if parsed is not None and not isinstance(parsed, dict):
                if self.verbose:
                    print(f"  IR error: Expected dict, got {type(parsed).__name__}")
                return None
            
            return parsed
            
        except Exception as e:
            if self.verbose:
                print(f"  IR error: {e}")
            return None
    
    def _process_behavior(self, behavior, fields: List[Dict], index: int) -> BehaviorContext:
        """
        Process a single behavior: build query, retrieve docs.
        """
        # Handle case where behavior is a string instead of dict
        if isinstance(behavior, str):
            return BehaviorContext(
                name=f"Behavior{index}",
                trigger="Update",
                actions=[behavior]
            )
        
        if not isinstance(behavior, dict):
            return BehaviorContext(
                name=f"Behavior{index}",
                trigger="Update",
                actions=[str(behavior)]
            )
        
        name = behavior.get("name", f"Behavior{index}")
        trigger = behavior.get("trigger", "")
        actions = behavior.get("actions", [])
        
        # Normalize actions to strings
        action_strings = []
        for action in actions:
            if isinstance(action, str):
                action_strings.append(action)
            elif isinstance(action, dict):
                action_strings.append(action.get("name", "") or str(action))
        
        ctx = BehaviorContext(
            name=name,
            trigger=trigger,
            actions=action_strings
        )
        
        if not self.rag:
            return ctx
        
        # Build focused query for this behavior
        query = self._build_behavior_query(trigger, action_strings)
        
        if self.verbose:
            print(f"       Query: {query[:60]}...")
        
        # Retrieve docs specific to this behavior using semantic search
        try:
            # Use the search method - it returns List[RetrievedDoc] directly
            docs = self.rag.search(
                query=query,
                namespaces=None,  # Search all namespaces for best coverage
                threshold=0.45,
                top_k=self.docs_per_behavior
            )
            
            if docs:
                # Load content for retrieved docs
                for doc in docs:
                    if not doc.content and self.rag.has_embedded_content:
                        # Content is in the document metadata
                        doc_data = self.rag.documents[doc.id]
                        doc.content = doc_data.get("content", "")
                
                ctx.rag_docs = docs
                ctx.rag_context = self.rag.format_context_for_prompt(
                    docs,
                    max_tokens=1500,
                    include_content=True
                )
        except Exception as e:
            if self.verbose:
                print(f"       RAG error: {e}")
        
        return ctx
    
    def _build_behavior_query(self, trigger: str, actions: List[str]) -> str:
        """
        Build a focused search query from behavior trigger and actions.
        
        Strategy: Construct a query that matches how the RAG database entries are named.
        Unity API docs are typically named like "ClassName.MethodName" or "ClassName-propertyName".
        """
        parts = [trigger] + actions
        text = " ".join(parts)
        
        # Collect API-style terms that will match the doc naming convention
        api_terms = []
        
        # Direct component/class names (highest priority - repeat for emphasis)
        component_pattern = r'\b(Rigidbody|AudioSource|Transform|Material|Renderer|Collider|ParticleSystem|Light|Camera|Animator|MeshRenderer|SpriteRenderer|LineRenderer)\b'
        for match in re.findall(component_pattern, text, re.IGNORECASE):
            api_terms.append(match)
            api_terms.append(match)  # Repeat for emphasis in semantic search
        
        # Method names with their typical class context
        verb_to_api = {
            "apply force": "Rigidbody.AddForce ForceMode",
            "add force": "Rigidbody.AddForce",
            "add torque": "Rigidbody.AddTorque",
            "play sound": "AudioSource.Play AudioSource.PlayOneShot",
            "play audio": "AudioSource.Play AudioSource.PlayOneShot AudioClip",
            "stop sound": "AudioSource.Stop",
            "set volume": "AudioSource.volume",
            "increase volume": "AudioSource.volume",
            "change color": "Material.color Renderer.material Color.Lerp",
            "set color": "Material.color Renderer.material",
            "shift color": "Color.Lerp Material.color",
            "lerp": "Mathf.Lerp Vector3.Lerp Color.Lerp Quaternion.Slerp",
            "spawn": "Object.Instantiate GameObject.Instantiate",
            "instantiate": "Object.Instantiate",
            "create": "Object.Instantiate GameObject",
            "destroy": "Object.Destroy",
            "rotate": "Transform.Rotate Transform.rotation Quaternion",
            "spin": "Transform.Rotate Transform.rotation",
            "move": "Transform.Translate Transform.position Vector3",
            "position": "Transform.position Vector3",
            "scale": "Transform localScale",
            "detect": "Physics OverlapSphere Raycast",
            "velocity": "Rigidbody.velocity Vector3",
            "speed": "Rigidbody.velocity Time.deltaTime",
            "collision": "OnCollisionEnter Collision Collider",
            "trigger enter": "OnTriggerEnter Collider",
            "particle": "ParticleSystem.Play ParticleSystem.Emit",
            "emit": "ParticleSystem.Emit",
            "light": "Light.intensity Light.color",
            "intensity": "Light.intensity",
            "animate": "Animator.SetTrigger Animator.SetBool Animator.SetFloat",
            "raycast": "Physics.Raycast RaycastHit",
            "overlap": "Physics.OverlapSphere",
            "random": "Random.Range Random.insideUnitSphere",
            "time": "Time.deltaTime Time.time",
            "input": "Input.GetKey Input.GetAxis Input.GetMouseButton",
            "explosion": "Rigidbody.AddExplosionForce",
            "bounce": "PhysicMaterial.bounciness Rigidbody",
        }
        
        text_lower = text.lower()
        for phrase, api_names in verb_to_api.items():
            if phrase in text_lower:
                api_terms.append(api_names)
        
        # Build the final query: prioritize API terms, include original context
        if api_terms:
            # Put API terms first for better semantic matching
            query = " ".join(api_terms) + " " + text
        else:
            query = text
        
        # Truncate if too long (keep more room for API terms)
        return query[:600]


# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(verbose: bool = True):
    """Interactive mode for per-behavior RAG pipeline"""
    print("="*60)
    print("UNITY PIPELINE PER-BEHAVIOR - Interactive Mode")
    print("="*60)
    print("Multi-query RAG: retrieves docs separately for each behavior")
    print("Commands:")
    print("  quit    - Exit")
    print("  verbose - Enable verbose output")
    print("  quiet   - Disable verbose output")
    print("="*60)
    
    pipeline = UnityPipelinePerBehavior(verbose=verbose)
    
    while True:
        try:
            user_input = input(f"\n> ").strip()
            
            if not user_input:
                continue
            if user_input.lower() == "quit":
                break
            if user_input.lower() == "verbose":
                pipeline.verbose = True
                print("Verbose ON")
                continue
            if user_input.lower() == "quiet":
                pipeline.verbose = False
                print("Verbose OFF")
                continue
            
            # Generate code
            result = pipeline.generate(user_input)
            
            if result.success:
                print(f"\n{'─'*60}")
                print("IR JSON:")
                print(f"{'─'*60}")
                print(json.dumps(result.ir_json, indent=2))
                
                print(f"\n{'─'*60}")
                print(f"C# CODE: ({result.behaviors_generated} behaviors, {result.total_docs_retrieved} docs)")
                print(f"{'─'*60}")
                print(result.code)
                
                if result.rag_doc_names_by_behavior and verbose:
                    print(f"\n{'─'*60}")
                    print("RAG Docs by Behavior:")
                    print(f"{'─'*60}")
                    for behavior_name, doc_names in result.rag_doc_names_by_behavior.items():
                        print(f"  {behavior_name}:")
                        for doc_name in doc_names[:5]:  # Show first 5
                            print(f"    - {doc_name}")
            else:
                print(f"Error: {result.error}")
                
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"Error: {e}")
            import traceback
            traceback.print_exc()
    
    print("Exiting.")


# ============================================================================
# COMPARISON FUNCTION
# ============================================================================

def compare_approaches(prompt: str, verbose: bool = True) -> Dict:
    """
    Compare monolithic vs per-behavior pipeline on the same prompt.
    """
    from unity_pipeline_simple import UnityPipelineSimple
    
    results = {
        "prompt": prompt,
        "monolithic": None,
        "per_behavior": None
    }
    
    # Run monolithic pipeline
    if verbose:
        print("\n" + "="*70)
        print("MONOLITHIC PIPELINE")
        print("="*70)
    
    mono_pipeline = UnityPipelineSimple(verbose=verbose)
    mono_result = mono_pipeline.generate(prompt, steer=False)
    
    results["monolithic"] = {
        "success": mono_result.success,
        "docs_used": mono_result.rag_docs_used,
        "code_length": len(mono_result.code) if mono_result.code else 0,
        "code": mono_result.code
    }
    
    # Run per-behavior pipeline
    if verbose:
        print("\n" + "="*70)
        print("PER-BEHAVIOR PIPELINE")
        print("="*70)
    
    per_behavior_pipeline = UnityPipelinePerBehavior(verbose=verbose)
    pb_result = per_behavior_pipeline.generate(prompt)
    
    results["per_behavior"] = {
        "success": pb_result.success,
        "docs_used": pb_result.total_docs_retrieved,
        "behaviors": pb_result.behaviors_generated,
        "code_length": len(pb_result.code) if pb_result.code else 0,
        "code": pb_result.code
    }
    
    # Summary
    if verbose:
        print("\n" + "="*70)
        print("COMPARISON SUMMARY")
        print("="*70)
        print(f"                    Monolithic    Per-Behavior")
        print(f"Docs Retrieved:     {results['monolithic']['docs_used']:>10}    {results['per_behavior']['docs_used']:>12}")
        print(f"Code Length:        {results['monolithic']['code_length']:>10}    {results['per_behavior']['code_length']:>12}")
        print(f"Coverage Increase:  {'-':>10}    {results['per_behavior']['docs_used'] / max(1, results['monolithic']['docs_used']):.1f}x")
    
    return results


# ============================================================================
# CLI
# ============================================================================

if __name__ == "__main__":
    import sys
    
    test_prompt = """Create a spinning object that plays a sound when clicked, 
    changes color based on spin speed, and explodes into particles after 10 seconds."""
    
    if len(sys.argv) > 1:
        if sys.argv[1] in ["--interactive", "-i"]:
            verbose = "--verbose" in sys.argv or "-v" in sys.argv
            interactive_mode(verbose=verbose)
        elif sys.argv[1] == "--compare":
            results = compare_approaches(test_prompt, verbose=True)
        else:
            test_prompt = " ".join(sys.argv[1:])
            pipeline = UnityPipelinePerBehavior(verbose=True)
            result = pipeline.generate(test_prompt)
            if result.success:
                print("\n" + "="*60)
                print("GENERATED CODE:")
                print("="*60)
                print(result.code)
    else:
        # Default: run per-behavior pipeline
        pipeline = UnityPipelinePerBehavior(verbose=True)
        result = pipeline.generate(test_prompt)
        if result.success:
            print("\n" + "="*60)
            print("GENERATED CODE:")
            print("="*60)
            print(result.code)

