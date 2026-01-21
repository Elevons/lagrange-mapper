"""
Unity Pipeline Simple - Streamlined NL -> IR -> C# Generation

Simplified flow:
1. User prompt -> IR JSON
2. IR JSON -> RAG retrieval  
3. RAG context + IR -> C# code generation
4. RAG-based steering (if needed)
5. Output code

No attractor detection, no calibration, just clean generation with RAG support.
"""

import json
import requests
import sys
import os
from typing import Dict, List, Optional
from dataclasses import dataclass

# Add code generation pipeline to path for imports
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'code generation pipeline'))
from unity_ir_inference import extract_json_from_response

# ============================================================================
# CONFIGURATION
# ============================================================================

LLM_URL = "http://localhost:1234/v1/chat/completions"
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "code generation pipeline", "unity_rag_db")
DEFAULT_TEMPERATURE = 0.4

# ============================================================================
# PROMPTS
# ============================================================================

IR_SYSTEM_PROMPT = """You are a helpful assistant. Output only valid JSON."""

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert the behavior specification into a complete MonoBehaviour script.

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, OnTriggerEnter, etc.)
2. Use correct Unity APIs from the documentation provided
3. Declare all fields as public or [SerializeField]
4. Add required using statements
5. Make the code production-ready and compilable
6. SELF-CONTAINED: Do NOT reference classes that aren't defined in this script or Unity's standard API
7. If you need a helper class, DEFINE IT in the same file

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
    rag_doc_names: Optional[List[str]] = None  # List of "APIName (score)" strings


@dataclass
class CompareResult:
    """Result of oneshot vs IR comparison"""
    prompt: str
    oneshot_code: Optional[str] = None
    ir_json: Optional[Dict] = None
    ir_code: Optional[str] = None
    ir_steered: bool = False
    ir_rag_docs: int = 0
    ir_rag_doc_names: Optional[List[str]] = None  # List of "APIName (score)" strings


# ============================================================================
# SIMPLE PIPELINE CLASS
# ============================================================================

class UnityPipelineSimple:
    """
    Simplified Unity code generation pipeline.
    
    Flow: Prompt -> IR JSON -> RAG -> C# Code -> (optional steering) -> Output
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
            print(f"  -> Class: {ir_json.get('class_name')}")
            print(f"  -> Components: {ir_json.get('components', [])}")
            print(f"  -> Fields: {len(ir_json.get('fields', []))}")
            print(f"  -> Behaviors: {len(ir_json.get('behaviors', []))}")
        
        # Step 2: RAG retrieval based on IR
        rag_context = ""
        rag_docs_used = 0
        rag_doc_names = []
        
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
                rag_doc_names = [f"{doc.api_name} ({doc.score:.2f})" for doc in rag_result.documents]
                rag_context = self.rag.format_context_for_prompt(
                    rag_result.documents, 
                    max_tokens=2500
                )
                
                if self.verbose:
                    print(f"  -> Retrieved {rag_docs_used} docs from {len(rag_result.selected_namespaces)} namespaces")
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
            print(f"  -> Generated {len(code)} chars")
        
        # Step 3.5: Verify IR fields are in generated code
        missing_fields = self._verify_ir_fields_in_code(code, ir_json)
        if missing_fields:
            if self.verbose:
                print(f"  -> {len(missing_fields)} IR fields missing, injecting...")
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
                    print(f"  -> Chain validation found {len(chain_suspicious)} issues")
                suspicious.extend(chain_suspicious)
            
            if suspicious:
                if self.verbose:
                    print(f"  -> {len(suspicious)} suspicious APIs found:")
                    for s in suspicious[:5]:
                        if s["nearest"]:
                            print(f"     [!] {s['api']} -> nearest: {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"     [!] {s['api']} -> not found in Unity docs")
                
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
                            print(f"  -> Steering applied (fixing {len(suspicious)} APIs)")
            else:
                if self.verbose:
                    print(f"  -> All APIs validated [OK]")
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
            rag_docs_used=rag_docs_used,
            rag_doc_names=rag_doc_names
        )
    
    def _generate_ir(self, prompt: str) -> Optional[Dict]:
        """Generate IR JSON from prompt"""
        try:
            # Build user message with embedded schema (model responds better to this)
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
                    "max_tokens": 2000
                },
                timeout=60
            )
            
            if response.status_code != 200:
                if self.verbose:
                    print(f"  IR error: HTTP {response.status_code}")
                    print(f"  Response: {response.text[:500]}")
                return None
            
            content = response.json()["choices"][0]["message"]["content"]
            
            # DEBUG: Print raw response if verbose
            if self.verbose:
                print(f"  Raw IR response ({len(content)} chars):")
                print(f"  {content[:300]}...")
            
            # Use robust JSON parsing (json-repair, json5, regex fallback)
            _, parsed = extract_json_from_response(content)
            
            # DEBUG: If parsing failed, show why
            if parsed is None and self.verbose:
                print(f"  [WARN] JSON parsing failed!")
                print(f"  Full response:\n{content}")
            
            # Validate that parsed result is a dict, not a list
            if parsed is not None and not isinstance(parsed, dict):
                if self.verbose:
                    print(f"  IR error: Expected dict, got {type(parsed).__name__}")
                return None
            
            return parsed
            
        except Exception as e:
            if self.verbose:
                print(f"  IR generation error: {e}")
                import traceback
                traceback.print_exc()
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
                        invalid_apis_text += f"- {s['api']} -> Try using: {s['nearest']}\n"
                    else:
                        invalid_apis_text += f"- {s['api']} -> Does not exist in Unity, remove or replace\n"
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
    
    def _validate_apis_against_rag(self, code: str, ir_json: Dict = None, threshold: float = 0.85) -> list:
        """
        Validate ALL method calls in code against RAG database.
        
        Uses a two-tier approach:
        1. EXACT MATCH: Check against API whitelist from RAG index (fast, reliable)
        2. SEMANTIC FALLBACK: If not in whitelist, use embedding similarity (slower, catches variations)
        
        Also validates:
        - Attributes: [AttributeName]
        - Constructors: new ClassName(...)
        
        Args:
            code: Generated C# code
            ir_json: Optional IR JSON for type context
            threshold: Minimum similarity score for semantic fallback (default 0.85)
            
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
            raw_components = ir_json.get("components", [])
            # Normalize: handle both string and dict formats
            for c in raw_components:
                if isinstance(c, str):
                    ir_components.append(c)
                elif isinstance(c, dict):
                    # Extract from {"component_type": "Rigidbody"} or {"name": "Rigidbody"}
                    comp_name = c.get("component_type") or c.get("name") or c.get("type", "")
                    if comp_name:
                        ir_components.append(comp_name)
        
        # Common properties/methods to skip (always valid on most Unity objects)
        skip_members = {
            'name', 'tag', 'gameObject', 'transform', 'enabled', 'position', 
            'rotation', 'localPosition', 'localRotation', 'localScale', 'parent',
            'GetComponent', 'GetComponents', 'GetComponentInChildren', 'GetComponentsInChildren',
            'AddComponent', 'Destroy', 'Instantiate', 'DontDestroyOnLoad',
        }
        
        # Common C# types to skip entirely
        skip_classes = {
            'System', 'List', 'Dictionary', 'Math', 'String', 'Array', 'Console', 
            'IEnumerator', 'IEnumerable', 'Action', 'Func', 'Task', 'File', 'Path',
            'Exception', 'Type', 'Object', 'Convert', 'Int32', 'Float', 'Boolean',
            'StringBuilder', 'Regex', 'Match', 'Group', 'Collections',
        }
        
        # Common C# methods to skip
        skip_methods = {'Length', 'Count', 'ToString', 'GetType', 'Equals', 'GetHashCode', 'CompareTo'}
        
        def check_api(api: str) -> dict:
            """Check a single API against whitelist, then semantic fallback."""
            # TIER 1: Exact match against whitelist
            if hasattr(self.rag, 'is_valid_api') and self.rag.is_valid_api(api):
                return None  # Valid, no issue
            
            # TIER 2: Semantic search fallback
            results = self.rag.search(query=api, top_k=1, threshold=0.0)
            if results:
                if results[0].score >= threshold:
                    return None  # Close enough match
                else:
                    return {
                        "api": api,
                        "nearest": results[0].api_name,
                        "score": results[0].score
                    }
            else:
                return {"api": api, "nearest": None, "score": 0.0}
        
        # =====================================================================
        # PATTERN 0: Validate attributes [AttributeName]
        # =====================================================================
        attr_pattern = r'\[(\w+)(?:\([^\)]*\))?\]'
        for match in re.finditer(attr_pattern, code):
            attr_name = match.group(1)
            
            if attr_name in checked:
                continue
            checked.add(attr_name)
            
            # Check if valid attribute
            if hasattr(self.rag, 'is_valid_attribute') and self.rag.is_valid_attribute(attr_name):
                continue
            
            # Also check whitelist for the attribute class itself
            if hasattr(self.rag, 'is_valid_api'):
                if self.rag.is_valid_api(attr_name) or self.rag.is_valid_api(f"{attr_name}Attribute"):
                    continue
            
            suspicious.append({
                "api": f"[{attr_name}]",
                "nearest": None,
                "score": 0.0,
                "type": "attribute"
            })
        
        # =====================================================================
        # PATTERN 1: Type.Method calls using IR components
        # e.g., animator.SetState -> check "Animator.SetState"
        # =====================================================================
        for component in ir_components:
            var_pattern = rf'\b(\w*{re.escape(component.lower())}\w*)\s*\.\s*(\w+)'
            
            for match in re.finditer(var_pattern, code, re.IGNORECASE):
                method = match.group(2)
                if method in skip_members or method in skip_methods:
                    continue
                    
                api = f"{component}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                issue = check_api(api)
                if issue:
                    suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 2: Common Unity base types (always check these)
        # =====================================================================
        base_types = [
            ("transform", "Transform"),
            ("gameObject", "GameObject"),
            ("rb", "Rigidbody"),
            ("rigidbody", "Rigidbody"),
            ("collider", "Collider"),
            ("renderer", "Renderer"),
            ("camera", "Camera"),
            ("light", "Light"),
            ("audio", "AudioSource"),
            ("animator", "Animator"),
        ]
        
        for var_hint, type_name in base_types:
            pattern = rf'\b{re.escape(var_hint)}\s*\.\s*(\w+)'
            for match in re.finditer(pattern, code, re.IGNORECASE):
                method = match.group(1)
                if method in skip_members or method in skip_methods:
                    continue
                    
                api = f"{type_name}.{method}"
                if api in checked:
                    continue
                checked.add(api)
                
                issue = check_api(api)
                if issue:
                    suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 3: ALL Class.member patterns (methods AND properties)
        # Catches: Time.deltaTime, Physics.Raycast(), Vector3.zero, etc.
        # =====================================================================
        static_pattern = r'\b([A-Z][a-zA-Z0-9]+)\s*\.\s*([a-zA-Z][a-zA-Z0-9]*)'
        
        for match in re.finditer(static_pattern, code):
            class_name = match.group(1)
            member = match.group(2)
            
            if class_name in skip_classes:
                continue
            
            if member in skip_methods:
                continue
            
            api = f"{class_name}.{member}"
            if api in checked:
                continue
            checked.add(api)
            
            issue = check_api(api)
            if issue:
                suspicious.append(issue)
        
        # =====================================================================
        # PATTERN 4: Constructor calls - new ClassName(...)
        # Catches hallucinated constructors like: new AudioClip("path")
        # =====================================================================
        ctor_pattern = r'\bnew\s+([A-Z][a-zA-Z0-9]+)\s*\('
        
        # Known Unity types that CAN be constructed with new
        constructable_types = {
            'Vector2', 'Vector3', 'Vector4', 'Quaternion', 'Color', 'Rect', 'Bounds',
            'Ray', 'RaycastHit', 'ContactPoint', 'WaitForSeconds', 'WaitForSecondsRealtime',
            'WaitUntil', 'WaitWhile', 'WaitForEndOfFrame', 'WaitForFixedUpdate',
            'Material', 'Mesh', 'Texture2D', 'RenderTexture', 'AnimationCurve',
            'GUIStyle', 'GUIContent', 'GUILayoutOption',
        }
        
        # Types that should NOT be constructed with new (use factory methods or assignment)
        non_constructable_types = {
            'AudioClip', 'AudioSource', 'GameObject', 'Transform', 'Component',
            'Rigidbody', 'Collider', 'Renderer', 'Camera', 'Light', 'Animator',
            'MonoBehaviour', 'ScriptableObject', 'Sprite', 'Font',
        }
        
        for match in re.finditer(ctor_pattern, code):
            type_name = match.group(1)
            
            ctor_key = f"new {type_name}"
            if ctor_key in checked:
                continue
            checked.add(ctor_key)
            
            # Flag if trying to construct a non-constructable type
            if type_name in non_constructable_types:
                suspicious.append({
                    "api": f"new {type_name}()",
                    "nearest": f"Use GetComponent<{type_name}>() or Resources.Load<{type_name}>()",
                    "score": 0.0,
                    "type": "invalid_constructor"
                })
        
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
                # Handle both string and dict formats from LLM
                if isinstance(comp, dict):
                    comp_name = comp.get("name", "")
                elif isinstance(comp, str):
                    comp_name = comp
                else:
                    continue
                
                if not comp_name:
                    continue
                
                # Map common variable patterns to component types
                components[comp_name.lower()] = comp_name
                # Common abbreviations
                if comp_name == "Rigidbody":
                    components["rb"] = comp_name
                if comp_name == "AudioSource":
                    components["audio"] = comp_name
                    components["source"] = comp_name
        
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
            # Handle case where field is not a dict
            if isinstance(field, str):
                field_name = field
                field_type = "float"
                field_default = None
            elif isinstance(field, dict):
                field_name = field.get("name", "")
                field_type = field.get("type", "float")
                field_default = field.get("default")
            else:
                continue
            
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
            print("│ ONESHOT (NL -> C# direct)")
            print(f"{'─'*70}")
        
        result.oneshot_code = self.generate_oneshot(prompt)
        
        if self.verbose and result.oneshot_code:
            print(result.oneshot_code)
        elif self.verbose:
            print("  [Oneshot failed]")
        
        # Generate via IR pipeline
        if self.verbose:
            print(f"\n{'─'*70}")
            print("│ IR PIPELINE (NL -> IR -> C#)")
            print(f"{'─'*70}")
        
        ir_result = self.generate(prompt)
        
        if ir_result.success:
            result.ir_json = ir_result.ir_json
            result.ir_code = ir_result.code
            result.ir_steered = ir_result.was_steered
            result.ir_rag_docs = ir_result.rag_docs_used
            result.ir_rag_doc_names = ir_result.rag_doc_names
            
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
            oneshot_has = "[OK]" if result.oneshot_code and pattern in result.oneshot_code else "[X]"
            ir_has = "[OK]" if result.ir_code and pattern in result.ir_code else "[X]"
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
                            print(f"      [!] {s['api']} -> {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      [!] {s['api']} -> not found")
                else:
                    print(f"    Oneshot: All APIs validated [OK]")
            
            # Validate IR pipeline code (with IR context for better type inference)
            if result.ir_code:
                ir_suspicious = self._validate_apis_against_rag(result.ir_code, ir_json=result.ir_json, threshold=0.70)
                if ir_suspicious:
                    print(f"    IR:      {len(ir_suspicious)} suspicious APIs")
                    for s in ir_suspicious[:3]:
                        if s["nearest"]:
                            print(f"      [!] {s['api']} -> {s['nearest']} ({s['score']:.2f})")
                        else:
                            print(f"      [!] {s['api']} -> not found")
                else:
                    print(f"    IR:      All APIs validated [OK]")
        
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

