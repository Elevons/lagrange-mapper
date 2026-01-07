"""
Unity Pipeline Simple - Streamlined NL → IR → C# Generation

Simplified flow:
1. User prompt → IR JSON
2. IR JSON → RAG retrieval  
3. RAG context + IR → C# code generation
4. RAG-based steering (if needed)
5. Output code

No attractor detection, no calibration, just clean generation with RAG support.
"""

import json
import requests
import sys
from typing import Dict, Optional
from dataclasses import dataclass
from unity_ir_inference import extract_json_from_response

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
RAG_DB_PATH = "unity_rag_db"
DEFAULT_TEMPERATURE = 0.4

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
1. Components must be exact Unity class names: Rigidbody, AudioSource, Collider, Animator, Text, Image, Canvas, RectTransform
2. Field types must be C# types: float, int, bool, string, Vector3, AudioClip, GameObject, Transform
3. Default values must be actual values: 10, null, true, 0.5 (not descriptions)
4. Actions are verb phrases: "play audio", "apply force", "destroy object"

Output ONLY valid JSON. No markdown, no explanations."""

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert the behavior specification into a complete MonoBehaviour script.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, etc.)
2. Use correct Unity APIs from the documentation provided
3. Declare all fields as public or [SerializeField]
4. Add required using statements
5. Make the code production-ready and compilable

Output ONLY the C# code. No markdown, no explanations."""

STEERING_PROMPT_TEMPLATE = """The following C# code has INVALID Unity APIs that need to be fixed.

INVALID APIS DETECTED (must be fixed):
{invalid_apis}

UNITY DOCUMENTATION FOR REFERENCE:
{rag_context}

ORIGINAL SPECIFICATION:
{ir_json}

CODE TO FIX:
```csharp
{code}
```

Fix EACH invalid API listed above. Use the suggested alternatives or find the correct Unity API.
Output ONLY the corrected C# code, no explanations:"""

ONESHOT_SYSTEM_PROMPT = """You are a Unity C# code generator. Generate a complete MonoBehaviour script from the description.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, etc.)
2. Use correct Unity APIs
3. Declare all fields as public or [SerializeField]
4. Add required using statements
5. Make the code production-ready and compilable

Output ONLY the C# code. No markdown, no explanations."""

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class PipelineResult:
    """Result of the pipeline"""
    success: bool
    ir_json: Optional[Dict] = None
    code: Optional[str] = None
    error: Optional[str] = None
    was_steered: bool = False
    rag_docs_used: int = 0


@dataclass
class CompareResult:
    """Result of oneshot vs IR comparison"""
    prompt: str
    oneshot_code: Optional[str] = None
    ir_json: Optional[Dict] = None
    ir_code: Optional[str] = None
    ir_steered: bool = False
    ir_rag_docs: int = 0


# ============================================================================
# SIMPLE PIPELINE CLASS
# ============================================================================

class UnityPipelineSimple:
    """
    Simplified Unity code generation pipeline.
    
    Flow: Prompt → IR JSON → RAG → C# Code → (optional steering) → Output
    """
    
    def __init__(self, 
                 llm_url: str = LLM_URL,
                 rag_db_path: str = RAG_DB_PATH,
                 verbose: bool = False):
        self.llm_url = llm_url
        self.verbose = verbose
        self.rag = None
        
        # Load RAG system
        try:
            from unity_rag_query import UnityRAG
            self.rag = UnityRAG(db_path=rag_db_path, verbose=verbose)
            if verbose:
                print(f"RAG loaded: {len(self.rag.documents)} docs, content={self.rag.has_embedded_content}")
        except Exception as e:
            if verbose:
                print(f"RAG not available: {e}")
    
    def generate(self, prompt: str, steer: bool = True) -> PipelineResult:
        """
        Generate Unity C# code from a natural language prompt.
        
        Args:
            prompt: Natural language description of desired behavior
            steer: Whether to apply RAG-based steering if code looks problematic
            
        Returns:
            PipelineResult with IR JSON and generated code
        """
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"PROMPT: {prompt[:80]}...")
            print(f"{'='*60}")
        
        # Step 1: Generate IR JSON
        if self.verbose:
            print("\n[1/4] Generating IR JSON...")
        
        ir_json = self._generate_ir(prompt)
        if ir_json is None:
            return PipelineResult(success=False, error="Failed to generate IR JSON")
        
        if self.verbose:
            print(f"  → Class: {ir_json.get('class_name')}")
            print(f"  → Components: {ir_json.get('components', [])}")
            print(f"  → Fields: {len(ir_json.get('fields', []))}")
            print(f"  → Behaviors: {len(ir_json.get('behaviors', []))}")
        
        # Step 2: RAG retrieval based on IR
        rag_context = ""
        rag_docs_used = 0
        
        if self.rag:
            if self.verbose:
                print("\n[2/4] Retrieving Unity documentation...")
            
            rag_result = self.rag.retrieve_for_ir(
                ir_json, 
                threshold=0.5, 
                top_k_total=8,
                include_content=True
            )
            
            if rag_result.documents:
                rag_docs_used = len(rag_result.documents)
                rag_context = self.rag.format_context_for_prompt(
                    rag_result.documents, 
                    max_tokens=2500
                )
                
                if self.verbose:
                    print(f"  → Retrieved {rag_docs_used} docs from {len(rag_result.selected_namespaces)} namespaces")
                    for doc in rag_result.documents[:3]:
                        print(f"     - {doc.api_name} ({doc.score:.2f})")
        else:
            if self.verbose:
                print("\n[2/4] RAG not available, skipping...")
        
        # Step 3: Generate C# code
        if self.verbose:
            print("\n[3/4] Generating C# code...")
        
        code = self._generate_code(ir_json, rag_context)
        if code is None:
            return PipelineResult(
                success=False, 
                ir_json=ir_json,
                error="Failed to generate code"
            )
        
        if self.verbose:
            print(f"  → Generated {len(code)} chars")
        
        # Step 3.5: Verify IR fields are in generated code
        missing_fields = self._verify_ir_fields_in_code(code, ir_json)
        if missing_fields:
            if self.verbose:
                print(f"  → {len(missing_fields)} IR fields missing, injecting...")
            code = self._inject_missing_fields(code, missing_fields)
            if self.verbose:
                for f in missing_fields:
                    print(f"     + {f['type']} {f['name']}")
        
        # Step 4: RAG-based API validation and steering
        was_steered = False
        
        if steer and self.rag:
            if self.verbose:
                print("\n[4/4] Validating APIs against RAG database...")
            
            # Validate all extracted APIs against RAG database (use IR for type context)
            suspicious = self._validate_apis_against_rag(code, ir_json=ir_json, threshold=0.70)
            
            # Also validate property chains (e.g., audioSource.clip.Play())
            chain_suspicious = self._validate_property_chains(code, ir_json)
            if chain_suspicious:
                if self.verbose:
                    print(f"  → Chain validation found {len(chain_suspicious)} issues")
                suspicious.extend(chain_suspicious)
            
            if suspicious:
                if self.verbose:
                    print(f"  → {len(suspicious)} suspicious APIs found:")
                    for s in suspicious[:5]:
                        if s["nearest"]:
                            print(f"     ⚠ {s['api']} → nearest: {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"     ⚠ {s['api']} → not found in Unity docs")
                
                # Get RAG docs for the suspicious APIs to help with steering
                steering_result = self.rag.retrieve_for_code_steering(code, threshold=0.5)
                
                if steering_result.documents:
                    steering_context = self.rag.format_context_for_prompt(
                        steering_result.documents,
                        max_tokens=2000
                    )
                    
                    # Pass the suspicious APIs to steering so LLM knows exactly what to fix
                    steered_code = self._steer_code(code, ir_json, steering_context, suspicious=suspicious)
                    if steered_code and steered_code != code:
                        code = steered_code
                        was_steered = True
                        if self.verbose:
                            print(f"  → Steering applied (fixing {len(suspicious)} APIs)")
            else:
                if self.verbose:
                    print(f"  → All APIs validated ✓")
        else:
            if self.verbose:
                print("\n[4/4] Validation skipped")
        
        if self.verbose:
            print(f"\n{'='*60}")
            print("COMPLETE")
            print(f"{'='*60}")
        
        return PipelineResult(
            success=True,
            ir_json=ir_json,
            code=code,
            was_steered=was_steered,
            rag_docs_used=rag_docs_used
        )
    
    def _generate_ir(self, prompt: str) -> Optional[Dict]:
        """Generate IR JSON from prompt"""
        try:
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
                timeout=60
            )
            
            if response.status_code != 200:
                return None
            
            content = response.json()["choices"][0]["message"]["content"]
            
            # Use robust JSON parsing (json-repair, json5, regex fallback)
            _, parsed = extract_json_from_response(content)
            return parsed
            
        except Exception as e:
            if self.verbose:
                print(f"  IR generation error: {e}")
            return None
    
    def _generate_code(self, ir_json: Dict, rag_context: str) -> Optional[str]:
        """Generate C# code from IR JSON with RAG context"""
        try:
            # Build prompt
            user_content = ""
            if rag_context:
                user_content += f"{rag_context}\n\n"
            
            user_content += f"BEHAVIOR SPECIFICATION:\n{json.dumps(ir_json, indent=2)}\n\n"
            user_content += "Generate the complete Unity C# MonoBehaviour script:"
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": CODE_SYSTEM_PROMPT},
                        {"role": "user", "content": user_content}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"  Code generation error: {e}")
            return None
    
    def _steer_code(self, code: str, ir_json: Dict, rag_context: str, suspicious: list = None) -> Optional[str]:
        """Apply RAG-based steering to fix code issues"""
        try:
            # Format the invalid APIs list for the prompt
            invalid_apis_text = ""
            if suspicious:
                for s in suspicious:
                    if s["nearest"]:
                        invalid_apis_text += f"- {s['api']} → Try using: {s['nearest']}\n"
                    else:
                        invalid_apis_text += f"- {s['api']} → Does not exist in Unity, remove or replace\n"
            else:
                invalid_apis_text = "- General API issues detected\n"
            
            prompt = STEERING_PROMPT_TEMPLATE.format(
                invalid_apis=invalid_apis_text,
                rag_context=rag_context,
                ir_json=json.dumps(ir_json, indent=2),
                code=code
            )
            
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [{"role": "user", "content": prompt}],
                    "temperature": 0.2,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            steered = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(steered)
            
        except Exception as e:
            if self.verbose:
                print(f"  Steering error: {e}")
            return None
    
    def _validate_apis_against_rag(self, code: str, ir_json: Dict = None, threshold: float = 0.70) -> list:
        """
        Validate ALL method calls in code against RAG database.
        
        Uses universal pattern matching (no hardcoded API lists) plus 
        IR-driven type context for better accuracy.
        
        Args:
            code: Generated C# code
            ir_json: Optional IR JSON for type context
            threshold: Minimum similarity score to consider valid
            
        Returns:
            List of suspicious APIs that don't match known Unity APIs
        """
        import re
        
        if not self.rag:
            return []
        
        suspicious = []
        checked = set()  # Avoid duplicate checks
        
        # Get component types from IR for context
        ir_components = []
        if ir_json:
            ir_components = ir_json.get("components", [])
        
        # =====================================================================
        # PATTERN 1: Type.Method calls using IR components
        # e.g., animator.SetState -> check "Animator.SetState"
        # =====================================================================
        for component in ir_components:
            # Find variables that might be this component type
            # Matches: animator, _animator, myAnimator, etc.
            var_pattern = rf'\b(\w*{re.escape(component.lower())}\w*)\s*\.\s*(\w+)'
            
            for match in re.finditer(var_pattern, code, re.IGNORECASE):
                method = match.group(2)
                # Skip common non-API properties
                if method in ('name', 'tag', 'gameObject', 'transform', 'enabled'):
                    continue
                    
                api = f"{component}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                # Check against RAG
                results = self.rag.search(query=api, top_k=1, threshold=0.0)
                if results:
                    if results[0].score < threshold:
                        suspicious.append({
                            "api": api,
                            "nearest": results[0].api_name,
                            "score": results[0].score
                        })
                else:
                    suspicious.append({"api": api, "nearest": None, "score": 0.0})
        
        # =====================================================================
        # PATTERN 2: Common Unity base types (always check these)
        # Transform, GameObject, Rigidbody, etc.
        # =====================================================================
        base_types = [
            ("transform", "Transform"),
            ("gameObject", "GameObject"),
            ("rb", "Rigidbody"),
            ("rigidbody", "Rigidbody"),
            ("collider", "Collider"),
        ]
        
        for var_hint, type_name in base_types:
            pattern = rf'\b{re.escape(var_hint)}\s*\.\s*(\w+)'
            for match in re.finditer(pattern, code, re.IGNORECASE):
                method = match.group(1)
                if method in ('name', 'tag', 'gameObject', 'transform', 'enabled', 'position', 'rotation'):
                    continue
                    
                api = f"{type_name}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                results = self.rag.search(query=api, top_k=1, threshold=0.0)
                if results:
                    if results[0].score < threshold:
                        suspicious.append({
                            "api": api,
                            "nearest": results[0].api_name,
                            "score": results[0].score
                        })
                else:
                    suspicious.append({"api": api, "nearest": None, "score": 0.0})
        
        # =====================================================================
        # PATTERN 3: ALL Class.member patterns (methods AND properties)
        # Catches: Time.deltaTime, Physics.Raycast(), Vector3.zero, etc.
        # =====================================================================
        # Match ClassName.memberName (both method calls and property access)
        static_pattern = r'\b([A-Z][a-zA-Z0-9]+)\s*\.\s*([a-zA-Z][a-zA-Z0-9]*)'
        
        for match in re.finditer(static_pattern, code):
            class_name = match.group(1)
            member = match.group(2)
            
            # Skip common non-Unity C# patterns
            if class_name in ('System', 'List', 'Dictionary', 'Math', 'String', 'Array', 'Console', 
                              'IEnumerator', 'IEnumerable', 'Action', 'Func', 'Task', 'File', 'Path'):
                continue
            
            # Skip if member is a common C# thing, not Unity API
            if member in ('Length', 'Count', 'ToString', 'GetType', 'Equals', 'GetHashCode'):
                continue
            
            api = f"{class_name}.{member}"
            if api in checked:
                continue
            checked.add(api)
            
            results = self.rag.search(query=api, top_k=1, threshold=0.0)
            if results:
                if results[0].score < threshold:
                    suspicious.append({
                        "api": api,
                        "nearest": results[0].api_name,
                        "score": results[0].score
                    })
            else:
                suspicious.append({"api": api, "nearest": None, "score": 0.0})
        
        return suspicious
    
    def _validate_property_chains(self, code: str, ir_json: Dict) -> list:
        """
        Validate property chains like audioSource.clip.Play().
        
        Uses the RAG type map to resolve each step of the chain
        and validate that the final method/property exists.
        
        Args:
            code: Generated C# code
            ir_json: IR JSON for type context
            
        Returns:
            List of suspicious chain APIs
        """
        import re
        
        if not self.rag or not hasattr(self.rag, 'type_map'):
            return []
        
        suspicious = []
        checked_chains = set()
        
        # Get component types from IR for base type inference
        components = {}
        if ir_json:
            for comp in ir_json.get("components", []):
                # Map common variable patterns to component types
                components[comp.lower()] = comp
                # Common abbreviations
                if comp == "Rigidbody":
                    components["rb"] = comp
                if comp == "AudioSource":
                    components["audio"] = comp
                    components["source"] = comp
        
        # Add common Unity types always present
        common_types = {
            "transform": "Transform",
            "gameobject": "GameObject",
            "renderer": "Renderer",
            "collider": "Collider",
        }
        components.update(common_types)
        
        # Find property chains: variable.prop1.prop2 or variable.prop1.method()
        # Match chains with 2+ segments
        chain_pattern = r'\b(\w+)((?:\.\w+){2,})'
        
        for match in re.finditer(chain_pattern, code):
            var_name = match.group(1).lower()
            chain_str = match.group(2)  # ".clip.Play" or ".transform.position.x"
            
            # Skip if we've already checked this exact chain
            chain_key = f"{var_name}{chain_str}"
            if chain_key in checked_chains:
                continue
            checked_chains.add(chain_key)
            
            # Determine base type from variable name
            base_type = None
            for pattern, type_name in components.items():
                if pattern in var_name:
                    base_type = type_name
                    break
            
            if not base_type:
                continue
            
            # Parse chain: ".clip.Play()" -> ["clip", "Play"]
            chain_parts = [s.rstrip('()[]') for s in chain_str.split('.') if s]
            
            if len(chain_parts) < 2:
                continue
            
            # Resolve and validate the chain
            final_type, invalid_steps = self.rag.resolve_chain_type(base_type, chain_parts)
            
            for inv in invalid_steps:
                suspicious.append({
                    "api": inv["api"],
                    "nearest": None,
                    "score": 0.0,
                    "reason": f"chain: {base_type}.{'.'.join(chain_parts)}"
                })
        
        return suspicious
    
    def _verify_ir_fields_in_code(self, code: str, ir_json: Dict) -> list:
        """
        Verify that all fields from the IR JSON are declared in the generated code.
        
        Args:
            code: Generated C# code
            ir_json: IR JSON specification
            
        Returns:
            List of missing fields (each with name, type, default)
        """
        import re
        
        if not ir_json:
            return []
        
        ir_fields = ir_json.get("fields", [])
        if not ir_fields:
            return []
        
        missing = []
        
        for field in ir_fields:
            field_name = field.get("name", "")
            field_type = field.get("type", "float")
            field_default = field.get("default")
            
            if not field_name:
                continue
            
            # Check if field is declared in code
            # Patterns: "public float fieldName", "private float fieldName", "[SerializeField] float fieldName"
            patterns = [
                rf'\b(public|private|protected)?\s*{re.escape(field_type)}\s+{re.escape(field_name)}\b',
                rf'\[SerializeField\]\s*(private|public)?\s*{re.escape(field_type)}\s+{re.escape(field_name)}\b',
                rf'\bpublic\s+{re.escape(field_type)}\s+{re.escape(field_name)}\s*[=;]',
            ]
            
            found = False
            for pattern in patterns:
                if re.search(pattern, code, re.IGNORECASE):
                    found = True
                    break
            
            if not found:
                missing.append({
                    "name": field_name,
                    "type": field_type,
                    "default": field_default
                })
        
        return missing
    
    def _inject_missing_fields(self, code: str, missing_fields: list) -> str:
        """
        Inject missing fields into the generated code.
        
        Adds fields after the class declaration opening brace.
        """
        import re
        
        if not missing_fields:
            return code
        
        # Build field declarations
        field_lines = []
        for field in missing_fields:
            name = field["name"]
            ftype = field["type"]
            default = field.get("default")
            
            if default is not None:
                if isinstance(default, str):
                    field_lines.append(f'    public {ftype} {name} = "{default}";')
                elif isinstance(default, bool):
                    field_lines.append(f'    public {ftype} {name} = {str(default).lower()};')
                else:
                    field_lines.append(f'    public {ftype} {name} = {default}f;' if ftype == "float" else f'    public {ftype} {name} = {default};')
            else:
                field_lines.append(f'    public {ftype} {name};')
        
        fields_block = "\n".join(field_lines)
        
        # Find the class opening and insert after it
        # Pattern: "class ClassName : MonoBehaviour {" or "class ClassName {"
        class_pattern = r'(class\s+\w+[^{]*\{)'
        
        match = re.search(class_pattern, code)
        if match:
            insert_pos = match.end()
            code = code[:insert_pos] + f"\n    // IR-specified fields (auto-injected)\n{fields_block}\n" + code[insert_pos:]
        
        return code
    
    def _clean_code(self, code: str) -> str:
        """Clean C# code from markdown formatting"""
        if "```csharp" in code:
            code = code.split("```csharp")[1].split("```")[0]
        elif "```cs" in code:
            code = code.split("```cs")[1].split("```")[0]
        elif "```" in code:
            parts = code.split("```")
            if len(parts) >= 2:
                code = parts[1]
        return code.strip()
    
    def generate_oneshot(self, prompt: str) -> Optional[str]:
        """
        Generate Unity C# code directly from prompt (no IR step).
        
        Args:
            prompt: Natural language description
            
        Returns:
            Generated C# code or None on failure
        """
        try:
            response = requests.post(
                self.llm_url,
                json={
                    "model": "local-model",
                    "messages": [
                        {"role": "system", "content": ONESHOT_SYSTEM_PROMPT},
                        {"role": "user", "content": prompt}
                    ],
                    "temperature": DEFAULT_TEMPERATURE,
                    "max_tokens": 4000
                },
                timeout=120
            )
            
            if response.status_code != 200:
                return None
            
            code = response.json()["choices"][0]["message"]["content"]
            return self._clean_code(code)
            
        except Exception as e:
            if self.verbose:
                print(f"  Oneshot generation error: {e}")
            return None
    
    def compare(self, prompt: str) -> CompareResult:
        """
        Compare oneshot generation vs IR pipeline.
        
        Args:
            prompt: Natural language description
            
        Returns:
            CompareResult with both outputs
        """
        result = CompareResult(prompt=prompt)
        
        # Generate oneshot
        if self.verbose:
            print(f"\n{'='*70}")
            print("COMPARE MODE: Oneshot vs IR Pipeline")
            print(f"{'='*70}")
            print(f"Prompt: {prompt[:70]}...")
            print(f"{'='*70}")
            print(f"\n{'─'*70}")
            print("│ ONESHOT (NL → C# direct)")
            print(f"{'─'*70}")
        
        result.oneshot_code = self.generate_oneshot(prompt)
        
        if self.verbose and result.oneshot_code:
            print(result.oneshot_code)
        elif self.verbose:
            print("  [Oneshot failed]")
        
        # Generate via IR pipeline
        if self.verbose:
            print(f"\n{'─'*70}")
            print("│ IR PIPELINE (NL → IR → C#)")
            print(f"{'─'*70}")
        
        ir_result = self.generate(prompt)
        
        if ir_result.success:
            result.ir_json = ir_result.ir_json
            result.ir_code = ir_result.code
            result.ir_steered = ir_result.was_steered
            result.ir_rag_docs = ir_result.rag_docs_used
            
            # Print IR JSON
            if self.verbose:
                print(f"\n{'─'*40}")
                print("IR JSON:")
                print(f"{'─'*40}")
                print(json.dumps(ir_result.ir_json, indent=2))
            
            # Print generated code
            if self.verbose:
                print(f"\n{'─'*40}")
                print("Generated C# Code:")
                print(f"{'─'*40}")
                print(ir_result.code)
        elif self.verbose:
            print("  [IR Pipeline failed]")
        
        # Print comparison summary
        if self.verbose:
            self._print_comparison(result)
        
        return result
    
    def _print_comparison(self, result: CompareResult):
        """Print comparison summary"""
        print(f"\n{'='*70}")
        print("│ COMPARISON")
        print(f"{'='*70}")
        
        oneshot_lines = len(result.oneshot_code.split('\n')) if result.oneshot_code else 0
        oneshot_chars = len(result.oneshot_code) if result.oneshot_code else 0
        ir_lines = len(result.ir_code.split('\n')) if result.ir_code else 0
        ir_chars = len(result.ir_code) if result.ir_code else 0
        
        print(f"  Oneshot:     {oneshot_lines} lines, {oneshot_chars:>5} chars")
        print(f"  IR Pipeline: {ir_lines} lines, {ir_chars:>5} chars (RAG: {result.ir_rag_docs} docs, steered: {result.ir_steered})")
        
        # Check for Unity patterns
        patterns = [
            ("Update()", "Update"),
            ("Start()", "Start"),
            ("OnCollision", "OnCollision"),
            ("OnTrigger", "OnTrigger"),
            ("Rigidbody", "Rigidbody"),
            ("AddForce", "AddForce"),
            ("AudioSource", "AudioSource"),
            ("GetComponent", "GetComponent"),
        ]
        
        print(f"\n  Unity Patterns Found:")
        for name, pattern in patterns:
            oneshot_has = "✓" if result.oneshot_code and pattern in result.oneshot_code else "✗"
            ir_has = "✓" if result.ir_code and pattern in result.ir_code else "✗"
            print(f"    {name:<20} Oneshot: {oneshot_has}  IR: {ir_has}")
        
        # RAG-based API validation for both outputs
        if self.rag:
            print(f"\n  API Validation (RAG-based):")
            
            # Validate oneshot code
            if result.oneshot_code:
                oneshot_suspicious = self._validate_apis_against_rag(result.oneshot_code, threshold=0.70)
                if oneshot_suspicious:
                    print(f"    Oneshot: {len(oneshot_suspicious)} suspicious APIs")
                    for s in oneshot_suspicious[:3]:
                        if s["nearest"]:
                            print(f"      ⚠ {s['api']} → {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      ⚠ {s['api']} → not found")
                else:
                    print(f"    Oneshot: All APIs validated ✓")
            
            # Validate IR pipeline code (with IR context for better type inference)
            if result.ir_code:
                ir_suspicious = self._validate_apis_against_rag(result.ir_code, ir_json=result.ir_json, threshold=0.70)
                if ir_suspicious:
                    print(f"    IR:      {len(ir_suspicious)} suspicious APIs")
                    for s in ir_suspicious[:3]:
                        if s["nearest"]:
                            print(f"      ⚠ {s['api']} → {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      ⚠ {s['api']} → not found")
                else:
                    print(f"    IR:      All APIs validated ✓")
        
        print(f"\n{'='*70}")
        print("Compare complete.")
        print(f"{'='*70}")


# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(verbose: bool = True):
    """Interactive mode for testing"""
    print("="*60)
    print("UNITY PIPELINE SIMPLE - Interactive Mode")
    print("="*60)
    print("Commands:")
    print("  quit    - Exit")
    print("  verbose - Enable verbose output")
    print("  quiet   - Disable verbose output")
    print("  compare - Enter compare mode (oneshot vs IR)")
    print("  normal  - Exit compare mode")
    print("="*60)
    
    pipeline = UnityPipelineSimple(verbose=verbose)
    compare_mode = False
    
    while True:
        try:
            mode_indicator = "[compare] " if compare_mode else ""
            user_input = input(f"\n{mode_indicator}> ").strip()
            
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
            if user_input.lower() == "compare":
                compare_mode = True
                print("Compare mode ON - will show oneshot vs IR pipeline")
                continue
            if user_input.lower() == "normal":
                compare_mode = False
                print("Compare mode OFF - normal IR pipeline only")
                continue
            
            if compare_mode:
                # Compare mode: show both oneshot and IR
                pipeline.compare(user_input)
            else:
                # Normal mode: IR pipeline only
                result = pipeline.generate(user_input)
                
                if result.success:
                    print(f"\n{'─'*60}")
                    print("IR JSON:")
                    print(f"{'─'*60}")
                    print(json.dumps(result.ir_json, indent=2))
                    
                    print(f"\n{'─'*60}")
                    print(f"C# CODE: (steered={result.was_steered}, rag_docs={result.rag_docs_used})")
                    print(f"{'─'*60}")
                    print(result.code)
                else:
                    print(f"Error: {result.error}")
                
        except KeyboardInterrupt:
            break
        except Exception as e:
            print(f"Error: {e}")
    
    print("Exiting.")


def main():
    args = sys.argv[1:]
    
    if not args or args[0] in ["--help", "-h"]:
        print("Unity Pipeline Simple")
        print()
        print("Usage:")
        print("  python unity_pipeline_simple.py --interactive")
        print("  python unity_pipeline_simple.py \"your prompt here\"")
        return
    
    if args[0] in ["--interactive", "-i"]:
        verbose = "--verbose" in args or "-v" in args
        interactive_mode(verbose=verbose)
    else:
        prompt = " ".join(args)
        pipeline = UnityPipelineSimple(verbose=True)
        result = pipeline.generate(prompt)
        
        if result.success:
            print("\n" + "="*60)
            print("GENERATED CODE:")
            print("="*60)
            print(result.code)
        else:
            print(f"Error: {result.error}")


if __name__ == "__main__":
    main()

