#!/usr/bin/env python3
"""
Unity Script Assembler
======================
Transforms Unity IR JSON into complete C# scripts using WaveformRAG
for API selection and code pattern retrieval.

Flow:
1. Parse Unity IR JSON (natural language behavior descriptions)
2. For each trigger/action, query WaveformRAG to find matching Unity APIs
3. Assemble code patterns into a complete MonoBehaviour script

Usage:
    from unity_script_assembler import UnityScriptAssembler
    
    assembler = UnityScriptAssembler()
    script = assembler.assemble(ir_json)
    print(script.code)
"""

import json
import re
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple, Any
from pathlib import Path

# Import WaveformRAG
import sys
sys.path.insert(0, str(Path(__file__).parent / "WaveformRAG"))
from WaveformRAG import WaveformEngine, set_language

# ============================================================================
# DATA CLASSES
# ============================================================================

@dataclass
class ResolvedAction:
    """An action resolved to a Unity API"""
    original: Dict  # Original action from IR
    query: str  # Query sent to WaveformRAG
    api_key: str  # Selected API key
    code_pattern: str  # Code pattern from RAG
    imports: List[str]  # Required imports
    confidence: float  # Match confidence
    rag_tool: Optional[Dict] = None  # Full RAG tool data with args_meta
    
@dataclass 
class ResolvedBehavior:
    """A behavior with resolved trigger and actions"""
    name: str
    trigger_type: str  # "update", "start", "coroutine", "event", etc.
    trigger_code: str  # Code for the trigger condition
    actions: List[ResolvedAction]
    condition: Optional[str] = None

@dataclass
class AssemblyResult:
    """Result of script assembly"""
    success: bool
    code: str = ""
    class_name: str = ""
    imports: List[str] = field(default_factory=list)
    fields: List[Dict] = field(default_factory=list)
    behaviors: List[ResolvedBehavior] = field(default_factory=list)
    warnings: List[str] = field(default_factory=list)
    error: Optional[str] = None

# ============================================================================
# TYPE MAPPING
# ============================================================================

# Maps IR types to C# types
TYPE_MAP = {
    "string": "string",
    "float": "float",
    "number": "float",
    "decimal": "float",
    "integer": "int",
    "int": "int",
    "whole number": "int",
    "boolean": "bool",
    "bool": "bool",
    "vector": "Vector3",
    "position": "Vector3",
    "rotation": "Quaternion",
    "object": "GameObject",
    "gameobject": "GameObject",
    "prefab": "GameObject",
    "transform": "Transform",
    "component": "Component",
}

def map_type(ir_type: str) -> str:
    """Map IR type description to C# type"""
    ir_lower = ir_type.lower()
    for key, csharp_type in TYPE_MAP.items():
        if key in ir_lower:
            return csharp_type
    return "object"  # Fallback


# ============================================================================
# LLM-BASED QUERY REFORMULATION
# ============================================================================
# Uses the same approach as dataset.py to reformulate queries into
# task_pattern style that matches the dataset embeddings.

import requests

def _call_llm_reformulate(query: str, query_type: str = "action") -> str:
    """
    Use LLM to reformulate a natural language query into task_pattern style.
    
    This matches the synthesis approach used in dataset.py:
    - Task patterns are verb-first imperative phrases
    - They describe what the user wants to accomplish
    - Examples: "set the value of a slider", "execute logic every frame"
    
    The examples are taken directly from unity_textbook.json task_patterns.
    """
    # Real task_patterns from unity_textbook.json
    if query_type == "trigger/event":
        samples = [
            ("every frame, update, continuously", "execute logic every frame"),
            ("start, begin, initialize", "initialize a script when first enabled"),
            ("timer, interval, repeating", "call a method repeatedly at a fixed interval"),
            ("collision, hit, impact", "detect when a rigidbody collides with another object"),
            ("trigger, enter zone", "respond to trigger enter event"),
            ("physics, fixed update", "perform physics calculations at a fixed time step"),
            ("health changed, value changed", "respond to a value change event"),
            ("player takes damage", "detect when an object takes damage"),
        ]
    else:
        # UI/Slider patterns - from actual dataset
        samples = [
            ("slider, value, set", "set the value of a slider"),
            ("slider, update, health", "set the progress value of a UI progress bar"),
            ("slider, maximum, high", "set the maximum value of a slider"),
            ("slider, minimum, low", "set the minimum value of a slider"),
            ("image, sprite, set", "set the sprite displayed in an image"),
            ("text, display, show", "set the text content of a UI label"),
            # Transform patterns
            ("move, translate, position", "set the position of a transform"),
            ("rotate, spin, turn", "rotate an object around an axis"),
            ("scale, resize, size", "set the local scale of a transform"),
            # Object patterns
            ("spawn, instantiate, create", "create a new instance of a prefab"),
            ("destroy, remove, delete", "destroy a game object"),
            # Physics patterns
            ("force, push, velocity", "apply force to a rigidbody"),
            ("velocity, speed, move", "set the velocity of a rigidbody"),
            # Audio patterns
            ("play, sound, audio", "play an audio clip"),
            # Variable operations - these map to generic code, not APIs
            ("subtract, minus, reduce", "subtract a value from a variable"),
            ("add, plus, increase", "add a value to a variable"),
            ("set, assign, value", "set a value"),
        ]
    
    examples = "\n".join([f'- "{keywords}" → "{pattern}"' for keywords, pattern in samples])
    
    prompt = f"""You are converting Unity script actions into API search queries.

The database contains patterns like:
{examples}

Convert this into a matching search pattern:
Input: "{query}"

Rules:
1. Output a verb-first phrase like "set the value of a slider"
2. Focus on the Unity API being used, not variable names
3. If it mentions Slider/Image/UI, include that in the pattern
4. Output ONLY the pattern, nothing else."""

    try:
        response = requests.post(
            "http://localhost:11434/api/generate",
            json={
                "model": "qwen2.5:14b",
                "prompt": prompt,
                "stream": False,
                "options": {"temperature": 0.1, "num_predict": 25}
            },
            timeout=10
        )
        if response.status_code == 200:
            result = response.json().get("response", "").strip()
            # Clean up: remove quotes, newlines, extra text
            result = result.strip('"\'').split('\n')[0].strip()
            # Remove any leading dash or bullet
            result = re.sub(r'^[-•*]\s*', '', result)
            if result and len(result) > 5 and len(result) < 100:
                return result
    except Exception:
        pass
    
    # Fallback: return original query
    return query


def build_action_query(action: dict, components: list = None, use_llm: bool = True) -> str:
    """
    Build a natural language query from action data.
    
    The IR now outputs actions in task_pattern style directly:
    - {"action": "set the value of a slider", "target": "healthSlider"}
    
    This format matches the dataset embeddings, so minimal reformulation needed.
    
    Args:
        action: Action dict from IR
        components: List of component names from IR (e.g., ["Slider", "Image"])
        use_llm: Whether to use LLM for reformulation (usually not needed now)
    """
    # New format: action field contains the task pattern directly
    if "action" in action:
        query = action.get("action", "")
        # Add target context if present
        target = action.get("target", "")
        if target and target.lower() not in query.lower():
            query = f"{query} {target}"
        return query
    
    # Legacy format fallback: type + params
    parts = []
    
    action_type = action.get("type", "")
    if action_type:
        parts.append(action_type.replace("_", " "))
    
    description = action.get("description", action.get("details", ""))
    if description and isinstance(description, str):
        parts.append(description)
    
    # Add param values for context
    params = action.get("params", {})
    if isinstance(params, dict):
        for key, val in params.items():
            if isinstance(val, str) and len(val) > 1:
                parts.append(val)
    
    # Add component context if provided
    if components:
        for comp in components:
            if isinstance(comp, str):
                parts.append(comp)
    
    raw_query = " ".join(parts).strip()
    
    # Use LLM reformulation for legacy format
    if use_llm and raw_query:
        return _call_llm_reformulate(raw_query, "action")
    return raw_query


def build_trigger_query(trigger: str, use_llm: bool = True) -> str:
    """
    Build a natural language query from trigger text.
    
    The IR now outputs triggers in task_pattern style directly:
    - "execute logic every frame"
    - "respond to trigger enter event"
    
    This format matches the dataset embeddings, so usually no reformulation needed.
    """
    if not trigger:
        return ""
    
    raw_query = trigger.strip()
    
    # If the trigger is already in task_pattern style (verb-first), use it directly
    # Examples: "execute", "detect", "respond", "initialize", "call"
    task_verbs = ["execute", "detect", "respond", "initialize", "call", "apply", "set", "create", "destroy"]
    if any(raw_query.lower().startswith(v) for v in task_verbs):
        return raw_query
    
    # Otherwise, use LLM to reformulate
    if use_llm and raw_query:
        return _call_llm_reformulate(raw_query, "trigger/event")
    return raw_query

# ============================================================================
# ASSEMBLER CLASS
# ============================================================================

class UnityScriptAssembler:
    """
    Assembles Unity C# scripts from natural language IR JSON.
    Uses WaveformRAG to find appropriate Unity APIs for each action.
    
    Two-stage retrieval:
    1. Coarse: Detect domain from IR (ui, physics, audio, etc.) 
    2. Fine: Search only within that domain's tools
    """
    
    def __init__(self, verbose: bool = False, skip_voting: bool = False, 
                 use_domain_filter: bool = True):
        self.verbose = verbose
        self.skip_voting = skip_voting  # If True, use raw RAG scores instead of MAKER voting
        self.use_domain_filter = use_domain_filter  # If True, use two-stage retrieval
        self.current_domain: Optional[str] = None  # Detected domain for current IR
        self.current_components: List[str] = []  # Components from current IR
        self.engine: Optional[WaveformEngine] = None
        self._init_engine()
    
    def _init_engine(self):
        """Initialize WaveformRAG engine"""
        try:
            set_language("unity")
            self.engine = WaveformEngine()
            # Disable voting if requested
            if self.skip_voting and hasattr(self.engine, 'llm'):
                self.engine.llm.num_votes = 0  # This will skip voting
            if self.verbose:
                mode = "RAG-only (no voting)" if self.skip_voting else "with MAKER voting"
                filter_mode = "domain-filtered" if self.use_domain_filter else "full search"
                print(f"WaveformRAG initialized {mode}, {filter_mode} ({len(self.engine.tools)} tools)")
        except Exception as e:
            print(f"Warning: Failed to initialize WaveformRAG: {e}")
            self.engine = None
    
    def _classify_ir_domain(self, ir_json: Dict) -> Optional[str]:
        """
        Classify the overall domain of an IR document.
        
        Uses multiple signals:
        1. Component names (e.g., "Slider", "Rigidbody")
        2. Behavior triggers and actions
        3. Field types
        4. Explicit domain markers
        
        Returns the most likely domain (ui_runtime, physics, audio, etc.)
        """
        if not self.engine:
            return None
        
        # Collect text from IR for classification
        text_signals = []
        
        # Components
        for comp in ir_json.get("components", []):
            if isinstance(comp, str):
                text_signals.append(comp.lower())
        
        # Class name
        class_name = ir_json.get("class_name", "")
        if class_name:
            text_signals.append(class_name.lower())
        
        # Behaviors - triggers and actions
        for behavior in ir_json.get("behaviors", ir_json.get("actions", [])):
            if isinstance(behavior, dict):
                trigger = behavior.get("trigger", "")
                if trigger:
                    text_signals.append(trigger.lower())
                for action in behavior.get("actions", []):
                    if isinstance(action, dict):
                        action_type = action.get("type", "")
                        if action_type:
                            text_signals.append(action_type.lower())
                        params = action.get("params", {})
                        if isinstance(params, dict):
                            text_signals.extend(str(v).lower() for v in params.values() if v)
        
        # Fields
        for field in ir_json.get("fields", []):
            if isinstance(field, dict):
                field_type = field.get("type", "")
                field_name = field.get("name", "")
                if field_type:
                    text_signals.append(field_type.lower())
                if field_name:
                    text_signals.append(field_name.lower())
        
        # Combine signals into a query
        combined_text = " ".join(text_signals)
        if not combined_text.strip():
            return None
        
        # Use engine's domain detection
        try:
            domain, score = self.engine.detect_domain(combined_text, verbose=self.verbose)
            if self.verbose and domain:
                print(f"  IR Domain: {domain} (score: {score:.2f})")
            return domain if score > 0.1 else None
        except Exception as e:
            if self.verbose:
                print(f"  Domain detection error: {e}")
            return None
    
    def assemble(self, ir_json: Dict | str) -> AssemblyResult:
        """
        Assemble a complete C# script from Unity IR JSON.
        
        Uses two-stage retrieval:
        1. Detect domain from IR (coarse classification)
        2. Search only within domain's tools (filtered search)
        
        Args:
            ir_json: Unity IR as dict or JSON string
            
        Returns:
            AssemblyResult with generated code
        """
        # Parse if string
        if isinstance(ir_json, str):
            try:
                ir_json = json.loads(ir_json)
            except json.JSONDecodeError as e:
                return AssemblyResult(success=False, error=f"Invalid JSON: {e}")
        
        result = AssemblyResult(success=True)
        result.class_name = self._sanitize_class_name(ir_json.get("class_name", "GeneratedBehavior"))
        
        # Stage 1: Coarse domain classification
        self.current_domain = None
        self.current_components = ir_json.get("components", [])  # Store components for action queries
        
        if self.use_domain_filter:
            self.current_domain = self._classify_ir_domain(ir_json)
            if self.verbose and self.current_domain:
                # Get tool count for this domain
                if self.engine and hasattr(self.engine, 'get_domain_tools'):
                    domain_tool_count = len(self.engine.get_domain_tools(self.current_domain))
                    print(f"  Two-stage search: {domain_tool_count} tools in '{self.current_domain}' domain")
        
        # Process fields
        result.fields = self._process_fields(ir_json.get("fields", []))
        
        # Process behaviors (Stage 2: domain-filtered RAG searches)
        behaviors = ir_json.get("behaviors", ir_json.get("actions", []))
        for behavior in behaviors:
            resolved = self._resolve_behavior(behavior)
            if resolved:
                result.behaviors.append(resolved)
                # Collect imports
                for action in resolved.actions:
                    result.imports.extend(action.imports)
        
        # Deduplicate imports
        result.imports = list(set(result.imports))
        if not result.imports:
            result.imports = ["UnityEngine"]
        
        # Generate code
        result.code = self._generate_code(result)
        
        # Reset state for next assembly
        self.current_domain = None
        self.current_components = []
        
        return result
    
    def _sanitize_class_name(self, name: str) -> str:
        """Convert name to valid C# class name"""
        # Remove non-alphanumeric, capitalize words
        words = re.findall(r'[a-zA-Z0-9]+', name)
        return ''.join(w.capitalize() for w in words) or "GeneratedBehavior"
    
    def _process_fields(self, fields: List[Dict]) -> List[Dict]:
        """Process IR fields into C# field definitions"""
        processed = []
        for f in fields:
            if not isinstance(f, dict):
                continue
            name = f.get("name", "field")
            # Sanitize field name
            name = re.sub(r'[^a-zA-Z0-9_]', '', name.replace(' ', '_'))
            if name and name[0].isdigit():
                name = '_' + name
            
            ir_type = f.get("type", "float")
            csharp_type = map_type(ir_type)
            
            default = f.get("default", "")
            default_value = self._parse_default_value(default, csharp_type)
            
            processed.append({
                "name": name or "field",
                "type": csharp_type,
                "default": default_value,
                "original": f
            })
        return processed
    
    def _parse_default_value(self, default: Any, csharp_type: str) -> str:
        """Parse default value from IR to C# literal"""
        if default is None or default == "":
            # Return type-appropriate default
            defaults = {
                "float": "0f",
                "int": "0",
                "bool": "false",
                "string": '""',
                "Vector3": "Vector3.zero",
                "Quaternion": "Quaternion.identity",
                "GameObject": "null",
            }
            return defaults.get(csharp_type, "null")
        
        # Handle numeric
        if isinstance(default, (int, float)):
            if csharp_type == "float":
                return f"{default}f"
            return str(default)
        
        # Parse natural language numbers
        if isinstance(default, str):
            # Extract numbers from text
            numbers = re.findall(r'[-+]?\d*\.?\d+', default)
            if numbers:
                val = float(numbers[0])
                if csharp_type == "float":
                    return f"{val}f"
                elif csharp_type == "int":
                    return str(int(val))
                return str(val)
            
            # Boolean
            if csharp_type == "bool":
                return "true" if any(w in default.lower() for w in ["true", "yes", "on"]) else "false"
            
            # String
            if csharp_type == "string":
                return f'"{default}"'
        
        return "null"
    
    def _resolve_behavior(self, behavior: Dict) -> Optional[ResolvedBehavior]:
        """Resolve a behavior's trigger and actions to Unity APIs"""
        name = behavior.get("name", "behavior")
        trigger = behavior.get("trigger", "")
        condition = behavior.get("condition")
        actions = behavior.get("actions", behavior.get("what_happens", []))
        
        # Wrap single action
        if isinstance(actions, str):
            actions = [{"type": "action", "details": actions}]
        elif isinstance(actions, dict):
            actions = [actions]
        
        # Determine trigger type
        trigger_type, trigger_code = self._resolve_trigger(trigger)
        
        # Resolve each action (without trigger context - actions are independent)
        resolved_actions = []
        for action in actions:
            resolved = self._resolve_action(action)
            if resolved:
                resolved_actions.append(resolved)
        
        return ResolvedBehavior(
            name=self._sanitize_method_name(name),
            trigger_type=trigger_type,
            trigger_code=trigger_code,
            actions=resolved_actions,
            condition=condition
        )
    
    def _resolve_trigger(self, trigger: str) -> Tuple[str, str]:
        """Map natural language trigger to Unity method using RAG"""
        if not trigger:
            return "lifecycle", "Update"
        
        # Use LLM to reformulate into task_pattern style
        raw_query = trigger.strip()
        query = build_trigger_query(trigger, use_llm=True)
        
        if self.verbose:
            if query != raw_query:
                print(f"  Trigger: \"{raw_query[:40]}...\" → \"{query[:40]}...\"")
            print(f"  Trigger RAG Query: {query[:50]}...")
        
        # Query RAG for the trigger
        rag_result = self._query_rag(query)
        
        if rag_result:
            api_key = rag_result.api_key.lower()
            # Determine type based on API
            if "start" in api_key or "awake" in api_key:
                return "lifecycle", "Start"
            elif "enable" in api_key:
                return "lifecycle", "OnEnable"
            elif "update" in api_key:
                if "fixed" in api_key:
                    return "lifecycle", "FixedUpdate"
                elif "late" in api_key:
                    return "lifecycle", "LateUpdate"
                return "lifecycle", "Update"
            elif "collision" in api_key:
                return "event", "OnCollisionEnter"
            elif "trigger" in api_key:
                if "exit" in api_key:
                    return "event", "OnTriggerExit"
                return "event", "OnTriggerEnter"
            elif "invoke" in api_key or "repeating" in api_key:
                return "coroutine", "InvokeRepeating"
            elif "coroutine" in api_key:
                return "coroutine", "StartCoroutine"
        
        # Default to Update if RAG didn't match
        return "lifecycle", "Update"
    
    def _resolve_action(self, action: Dict, context: str = "") -> Optional[ResolvedAction]:
        """Resolve an action to a Unity API using WaveformRAG"""
        # Build raw query first
        raw_parts = []
        action_type = action.get("type", "")
        if action_type:
            raw_parts.append(action_type.replace("_", " "))
        description = action.get("description", action.get("details", ""))
        if description and isinstance(description, str):
            raw_parts.append(description)
        raw_query = " ".join(raw_parts).strip()
        
        # Use LLM to reformulate into task_pattern style
        # Pass component context from IR for better API matching
        components = getattr(self, 'current_components', [])
        query = build_action_query(action, components=components, use_llm=True)
        
        if not query:
            return None
        
        if self.verbose:
            if query != raw_query and raw_query:
                print(f"  Action: \"{raw_query[:40]}...\" → \"{query[:40]}...\"")
            print(f"  Action RAG Query: {query[:60]}...")
        
        # Query RAG
        rag_result = self._query_rag(query)
        if rag_result:
            return rag_result
        
        # Fallback - generate placeholder
        action_type = action.get("type", "action")
        return ResolvedAction(
            original=action,
            query=query,
            api_key="Placeholder",
            code_pattern=f"// TODO: {action_type}",
            imports=["UnityEngine"],
            confidence=0.0
        )
    
    def _query_rag(self, query: str, domain_override: Optional[str] = None) -> Optional[ResolvedAction]:
        """
        Query RAG and return the top result.
        
        Uses two-stage retrieval if domain filtering is enabled:
        - If self.current_domain is set, searches only within that domain
        - Falls back to full search if no domain detected
        
        Args:
            query: The search query
            domain_override: Explicit domain to filter by (overrides self.current_domain)
        """
        if not self.engine or not query:
            return None
        
        try:
            # Determine which domain to filter by
            domain_filter = domain_override or self.current_domain
            
            # Use filtered search if domain filtering is enabled and we have a domain
            if self.use_domain_filter and domain_filter:
                if hasattr(self.engine, 'find_tool_filtered'):
                    result = self.engine.find_tool_filtered(
                        query, 
                        domain_filter=domain_filter,
                        verbose=self.verbose, 
                        deep_search=True,
                        auto_detect_domain=False  # We already detected domain from IR
                    )
                else:
                    result = self.engine.find_tool(query, verbose=self.verbose, deep_search=True)
            else:
                # Full search across all tools
                result = self.engine.find_tool(query, verbose=self.verbose, deep_search=True)
            
            if result and result.get("top_tools"):
                best_tool = result["top_tools"][0]
                
                if self.verbose:
                    domain_info = f" [domain: {domain_filter}]" if domain_filter else ""
                    print(f"    RAG: {best_tool.get('key', 'Unknown')} (score: {best_tool.get('score', 0.0):.2f}){domain_info}")
                
                return ResolvedAction(
                    original={},
                    query=query,
                    api_key=best_tool.get("key", "Unknown"),
                    code_pattern=best_tool.get("code", "// TODO: Implement"),
                    imports=best_tool.get("using", ["UnityEngine"]),
                    confidence=best_tool.get("score", 0.0),
                    rag_tool=best_tool  # Store full tool for LLM parameter filling
                )
        except Exception as e:
            if self.verbose:
                print(f"  RAG error: {e}")
        
        return None
    
    def _sanitize_method_name(self, name: str) -> str:
        """Convert name to valid C# method name"""
        words = re.findall(r'[a-zA-Z0-9]+', name)
        if not words:
            return "DoAction"
        return ''.join(w.capitalize() for w in words)
    
    def _generate_code(self, result: AssemblyResult) -> str:
        """Generate the final C# script"""
        lines = []
        
        # Collect field names for reference
        field_names = {f['name'] for f in result.fields}
        interval_field = self._find_interval_field(result.fields)
        prefab_field = self._find_prefab_field(result.fields)
        
        # Imports - ensure UnityEngine is always included
        imports = set(result.imports)
        imports.add("UnityEngine")
        
        for imp in sorted(imports):
            lines.append(f"using {imp};")
        lines.append("")
        
        # Class declaration
        lines.append(f"public class {result.class_name} : MonoBehaviour")
        lines.append("{")
        
        # Fields
        if result.fields:
            lines.append("    // === Fields ===")
            for f in result.fields:
                lines.append(f"    public {f['type']} {f['name']} = {f['default']};")
            lines.append("")
        
        # Group behaviors by trigger type
        lifecycle_behaviors = {}  # method_name -> [behaviors]
        coroutine_behaviors = []
        
        for behavior in result.behaviors:
            if behavior.trigger_type in ["lifecycle", "conditional", "input", "event"]:
                method = behavior.trigger_code
                if method not in lifecycle_behaviors:
                    lifecycle_behaviors[method] = []
                lifecycle_behaviors[method].append(behavior)
            elif behavior.trigger_type == "coroutine":
                coroutine_behaviors.append(behavior)
        
        # Generate Start if we have coroutines to initialize
        if coroutine_behaviors and "Start" not in lifecycle_behaviors:
            lifecycle_behaviors["Start"] = []
        
        # Generate lifecycle methods
        for method_name, behaviors in lifecycle_behaviors.items():
            lines.append(f"    void {method_name}()")
            lines.append("    {")
            
            # Start coroutines in Start
            if method_name == "Start":
                for cb in coroutine_behaviors:
                    # Use actual interval field name
                    interval_var = interval_field or "1f"
                    lines.append(f"        InvokeRepeating(nameof({cb.name}), 0f, {interval_var});")
            
            for behavior in behaviors:
                if behavior.condition:
                    lines.append(f"        // {behavior.name}")
                    lines.append(f"        // Condition: {behavior.condition}")
                
                for action in behavior.actions:
                    code = self._format_action_code(action, field_names, prefab_field)
                    for code_line in code.split('\n'):
                        lines.append(f"        {code_line}")
            
            if not behaviors and method_name != "Start":
                lines.append("        // No actions")
            
            lines.append("    }")
            lines.append("")
        
        # Generate coroutine/repeating methods
        for behavior in coroutine_behaviors:
            lines.append(f"    void {behavior.name}()")
            lines.append("    {")
            for action in behavior.actions:
                code = self._format_action_code(action, field_names, prefab_field)
                for code_line in code.split('\n'):
                    lines.append(f"        {code_line}")
            lines.append("    }")
            lines.append("")
        
        # Generate event methods
        event_behaviors = [b for b in result.behaviors if b.trigger_type == "event"]
        for behavior in event_behaviors:
            if behavior.trigger_code not in lifecycle_behaviors:
                param = "Collision other" if "Collision" in behavior.trigger_code else "Collider other"
                lines.append(f"    void {behavior.trigger_code}({param})")
                lines.append("    {")
                for action in behavior.actions:
                    code = self._format_action_code(action, field_names, prefab_field)
                    for code_line in code.split('\n'):
                        lines.append(f"        {code_line}")
                lines.append("    }")
                lines.append("")
        
        lines.append("}")
        
        return '\n'.join(lines)
    
    def _find_interval_field(self, fields: List[Dict]) -> Optional[str]:
        """Find a field that looks like an interval/timer"""
        for f in fields:
            name = f.get("name", "").lower()
            if any(kw in name for kw in ["interval", "delay", "rate", "period", "time"]):
                return f["name"]
        return None
    
    def _find_prefab_field(self, fields: List[Dict]) -> Optional[str]:
        """Find a field that looks like a prefab reference"""
        for f in fields:
            name = f.get("name", "").lower()
            ftype = f.get("type", "").lower()
            if "prefab" in name or ftype == "gameobject":
                return f["name"]
        return None
    
    def _format_action_code(self, action: ResolvedAction, field_names: set = None, prefab_field: str = None) -> str:
        """Format action code pattern with context using LLM parameter filling"""
        
        # Check if we have the RAG tool data with args_meta
        if hasattr(action, 'rag_tool') and action.rag_tool:
            tool = action.rag_tool
            args_meta = tool.get('args_meta', [])
            
            # If we have parameter metadata, use LLM to fill them
            if args_meta and self.engine and hasattr(self.engine, 'llm'):
                # Build IR data from action and available fields
                ir_data = {
                    'fields': [{'name': fn, 'type': 'auto'} for fn in (field_names or [])],
                    'action': action.original if hasattr(action, 'original') else {}
                }
                
                # Add prefab field if available
                if prefab_field:
                    ir_data['action']['prefab'] = prefab_field
                
                # Try LLM-based parameter filling
                query = action.query if hasattr(action, 'query') else str(action.original)
                filled_code = self.engine.llm.generate_api_call(query, tool, ir_data, timeout=10)
                
                if filled_code and filled_code.strip():
                    code = filled_code.strip()
                    # Ensure semicolon
                    if not code.endswith(';') and not code.endswith('}'):
                        code += ';'
                    return code
        
        # Fallback to deterministic placeholder replacement
        code = action.code_pattern
        
        # Use prefab field if available
        if prefab_field:
            code = code.replace("prefab", prefab_field)
        
        # Replace numbered placeholders with meaningful defaults
        code = re.sub(r'\{0\}', prefab_field or 'target', code)
        code = re.sub(r'\{1\}', 'transform.position', code)
        code = re.sub(r'\{2\}', 'Quaternion.identity', code)
        code = re.sub(r'\{\d+\}', 'value', code)
        
        # Replace angle bracket placeholders from RAG patterns
        code = re.sub(r'<[^>]+prefab[^>]*>', prefab_field or 'prefab', code, flags=re.IGNORECASE)
        code = re.sub(r'<[^>]+position[^>]*>', 'transform.position', code, flags=re.IGNORECASE)
        code = re.sub(r'<[^>]+rotation[^>]*>', 'Quaternion.identity', code, flags=re.IGNORECASE)
        code = re.sub(r'<[^>]+parent[^>]*>', 'transform', code, flags=re.IGNORECASE)
        code = re.sub(r'<[^>]+force[^>]*>', 'Vector3.forward * 10f', code, flags=re.IGNORECASE)
        code = re.sub(r'<[^>]+>', 'value', code)  # Catch remaining
        
        # Ensure it ends with semicolon if it looks like a statement
        code = code.strip()
        if code and not code.endswith(';') and not code.endswith('}') and not code.startswith('//'):
            # Check if it looks like an expression/statement
            if '(' in code or '=' in code or '.' in code:
                code += ';'
        
        # Add comment if low confidence
        if action.confidence < 0.5:
            code = f"// API: {action.api_key} (confidence: {action.confidence:.2f})\n{code}"
        
        return code


# ============================================================================
# CLI / TESTING
# ============================================================================

def main():
    """Test the assembler with sample IR"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Unity Script Assembler")
    parser.add_argument("input", nargs="?", help="IR JSON file or string")
    parser.add_argument("-v", "--verbose", action="store_true")
    parser.add_argument("--test", action="store_true", help="Run with test IR")
    
    args = parser.parse_args()
    
    assembler = UnityScriptAssembler(verbose=args.verbose)
    
    if args.test or not args.input:
        # Test IR
        test_ir = {
            "class_name": "SpawnIntervalManager",
            "fields": [
                {"name": "spawnPrefab", "type": "prefab", "default": ""},
                {"name": "interval", "type": "float", "default": 2.0},
                {"name": "spawnCount", "type": "integer", "default": 10}
            ],
            "behaviors": [
                {
                    "name": "spawn prefab",
                    "trigger": "timer reaches interval",
                    "actions": [
                        {"type": "instantiate", "details": "create copy of spawnPrefab at current position"}
                    ]
                },
                {
                    "name": "decrement counter",
                    "trigger": "after spawning",
                    "actions": [
                        {"type": "decrease", "details": "reduce spawnCount by one"}
                    ]
                }
            ]
        }
        print("Testing with sample IR:")
        print(json.dumps(test_ir, indent=2))
        print("\n" + "="*60 + "\n")
        
        result = assembler.assemble(test_ir)
    else:
        # Load from file or string
        if args.input.endswith('.json'):
            with open(args.input) as f:
                ir_json = json.load(f)
        else:
            ir_json = args.input
        
        result = assembler.assemble(ir_json)
    
    if result.success:
        print("Generated C# Script:")
        print("="*60)
        print(result.code)
        print("="*60)
        if result.warnings:
            print("\nWarnings:")
            for w in result.warnings:
                print(f"  - {w}")
    else:
        print(f"Assembly failed: {result.error}")


if __name__ == "__main__":
    main()

