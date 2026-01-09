#!/usr/bin/env python3
"""
Unity Full Pipeline with RAG
============================
End-to-end Unity script generation:
  User Description -> IR JSON (with code leak steering) -> C# Script (via RAG)

Combines:
- unity_ir_inference.py: Natural language to IR JSON (with attractor steering)
- unity_rag_query.py: RAG-based documentation retrieval
- LLM code generation with RAG context

RAG Modes:
- monolithic: Single query for entire IR (~10 docs, faster)
- per_behavior: Query per behavior then combine (~20-30 docs, more comprehensive)

Usage:
    python unity_full_pipeline_rag.py --interactive
    python unity_full_pipeline_rag.py "player that collects coins and gains score"
    python unity_full_pipeline_rag.py --rag-mode per_behavior "enemy AI"
"""

import json
import argparse
import requests
import re
from typing import Optional, Dict, List, Tuple
from dataclasses import dataclass, field

# IR Generator with code leak steering
from unity_ir_inference import UnityIRGenerator, GenerationResult

# RAG system
from unity_rag_query import UnityRAG, RetrievalResult

# Note: Hallucination steering removed - found to be counterproductive

# ============================================================================
# CONFIGURATION
# ============================================================================

import os
LLM_URL = "http://localhost:1234/v1/chat/completions"
RAG_DB_PATH = os.path.join(os.path.dirname(__file__), "unity_rag_db")
DEFAULT_TEMPERATURE = 0.4

# RAG retrieval modes
RAG_MODE_MONOLITHIC = "monolithic"  # Single query for entire IR (faster, ~10 docs)
RAG_MODE_PER_BEHAVIOR = "per_behavior"  # Query per behavior (more docs, ~20-30)

# Per-behavior RAG config
DOCS_PER_BEHAVIOR = 6

# ============================================================================
# PROMPTS
# ============================================================================

CODE_SYSTEM_PROMPT = """You are a Unity C# code generator. Convert the behavior specification into a complete MonoBehaviour script.

CRITICAL: TEMPORAL EXECUTION MODEL

Unity executes code frame-by-frame (~60 times per second). Actions must execute at the correct frequency based on their "temporal" classification:

1. EVENT ACTIONS (temporal: "event" - once per trigger):
   - Execute in: OnTriggerEnter, OnCollisionEnter, button callbacks, state entry methods, condition change handlers
   - Examples: PlayOneShot(), Instantiate(), Destroy(), SetTrigger(), one-time color changes
   - NEVER call in Update() or FixedUpdate() unless protected by state change detection or cooldowns

2. CONTINUOUS ACTIONS (temporal: "continuous" - every frame while active):
   - Execute in: Update(), FixedUpdate(), LateUpdate(), state update methods
   - Examples: transform.position +=, Rigidbody.AddForce(ForceMode.Force), Vector3.Lerp()
   - Must be called repeatedly for smooth motion/rotation
   - Always multiply by Time.deltaTime for frame-rate independence

3. CONDITIONAL ACTIONS (temporal: "conditional" - check once per frame, trigger events):
   - Execute in: Update() with state tracking to prevent re-triggering
   - Pattern: if (condition && !wasTrue) { ExecuteEvent(); wasTrue = true; }

CRITICAL RULES - FOLLOW EXACTLY:

1. CLASS NAME: Use EXACTLY the "class_name" from IR - no changes, no underscores, no prefixes
2. FIELDS: Declare EXACTLY the fields from "fields" array:
   - Use the EXACT field name from IR (e.g., if IR says "chaseTarget", use "chaseTarget" not "_chaseTarget" or "target")
   - Use the EXACT type from IR (e.g., if IR says "GameObject", use GameObject not Transform)
   - Use the EXACT default value from IR
   - Declare as [SerializeField] private
   - DO NOT add extra fields not in the IR
3. COMPONENTS: For each component in "components":
   - Add GetComponent<T>() call in Start() using the EXACT component name
   - Store in a private field (e.g., Rigidbody _rigidbody)
   - If IR says "Rigidbody", use Rigidbody NOT Rigidbody2D
4. BEHAVIORS: Implement EXACTLY each behavior from "behaviors":
   - Check each action's "temporal" field
   - Map "temporal": "event" → Execute once in appropriate event handler
   - Map "temporal": "continuous" → Execute every frame in Update/FixedUpdate
   - Map "temporal": "conditional" → Check condition, trigger events on change
   - DO NOT skip behaviors or add extra behaviors

FIELD NAMING RULES:
- If IR field is "chaseTarget", use "chaseTarget" (not "_chaseTarget", not "target", not "_target")
- If IR field is "detectionRadius", use "detectionRadius" (not "_detectionRadius", not "detectionRange")
- Preserve exact spelling and casing from IR

REQUIREMENTS:
1. Use proper Unity lifecycle methods (Start, Update, FixedUpdate, OnTriggerEnter, OnTriggerExit, etc.)
2. Use correct Unity APIs from the documentation provided
3. Add required using statements (UnityEngine, System.Collections, etc.)
4. Make the code production-ready and compilable
5. SELF-CONTAINED: Do NOT reference classes that aren't defined in this script or Unity's standard API
6. DO NOT add extra fields, methods, or behaviors not in the IR specification
7. Use the exact component types from IR (Rigidbody vs Rigidbody2D, Collider vs Collider2D)

IMPLEMENTATION PATTERNS:

Pattern 1: TRIGGER-BASED BEHAVIORS
For behaviors with discrete triggers (collision, button press, detection enter/exit):
```csharp
private void OnTriggerEnter(Collider other) {
    if (other.CompareTag("Player")) {
        // EVENT actions execute here (temporal: "event")
        audioSource.PlayOneShot(collectSound);  // ✓ Once per trigger
        Destroy(gameObject);
    }
}
```

Pattern 2: STATE-BASED BEHAVIORS  
For behaviors with persistent states (idle, chase, attack):
```csharp
private enum State { Idle, Chase, Attack }
private State _currentState = State.Idle;
private State _previousState;

void Update() {
    State nextState = DetermineNextState();
    
    // Detect state changes
    if (nextState != _currentState) {
        ExitState(_currentState);
        _previousState = _currentState;
        _currentState = nextState;
        EnterState(_currentState);  // ✓ EVENT actions here (temporal: "event")
    }
    
    UpdateState(_currentState);  // ✓ CONTINUOUS actions here (temporal: "continuous")
}

private void EnterState(State state) {
    // Execute EVENT actions from IR (temporal: "event")
    switch(state) {
        case State.Attack:
            audioSource.PlayOneShot(attackSound);  // ✓ Once on entry
            GetComponent<Renderer>().material.color = attackColor;
            animator.SetTrigger("Attack");
            break;
    }
}

private void UpdateState(State state) {
    // Execute CONTINUOUS actions from IR (temporal: "continuous")
    switch(state) {
        case State.Chase:
            Vector3 dir = (target.position - transform.position).normalized;
            transform.position += dir * chaseSpeed * Time.deltaTime;  // ✓ Every frame
            break;
    }
}
```

Pattern 3: COOLDOWN-BASED BEHAVIORS
For behaviors with timing constraints (fire rate, ability cooldowns):
```csharp
private float _lastFireTime = 0f;
[SerializeField] private float fireRate = 0.5f;

void Update() {
    if (Input.GetButton("Fire") && Time.time - _lastFireTime >= fireRate) {
        Fire();  // ✓ EVENT action with rate limiting
        _lastFireTime = Time.time;
    }
}
```

Pattern 4: CONDITION-BASED BEHAVIORS
For behaviors that trigger on condition changes:
```csharp
private bool _wasInRange = false;

void Update() {
    bool inRange = Vector3.Distance(transform.position, player.position) < detectionRange;
    
    // Detect condition change (temporal: "conditional")
    if (inRange && !_wasInRange) {
        OnEnterRange();  // ✓ EVENT actions on condition becoming true
    } else if (!inRange && _wasInRange) {
        OnExitRange();   // ✓ EVENT actions on condition becoming false
    }
    
    if (inRange) {
        TrackPlayer();  // ✓ CONTINUOUS actions while condition is true (temporal: "continuous")
    }
    
    _wasInRange = inRange;
}
```

Pattern 5: SIMPLE CONTINUOUS BEHAVIORS
For behaviors that run every frame:
```csharp
void Update() {
    if (chaseTarget != null) {
        // CONTINUOUS actions (temporal: "continuous")
        Vector3 direction = (chaseTarget.transform.position - transform.position).normalized;
        transform.position += direction * moveSpeed * Time.deltaTime;
        transform.rotation = Quaternion.LookRotation(direction);
    }
}
```

ACTION MAPPING BY TEMPORAL TYPE:

EVENT (temporal: "event" - call once):
- "play audio clip" → audioSource.PlayOneShot(clip)
- "spawn prefab" → Instantiate(prefab, position, rotation)
- "destroy object" → Destroy(gameObject)
- "set color" → renderer.material.color = color
- "set material" → renderer.material = material
- "set animation trigger" → animator.SetTrigger(trigger)
- "apply impulse" → rigidbody.AddForce(force, ForceMode.Impulse)
- "enable component" → component.enabled = true
- "disable component" → component.enabled = false
- "send message" → target.SendMessage(methodName)
- "show UI" → uiObject.SetActive(true)
- "hide UI" → uiObject.SetActive(false)

CONTINUOUS (temporal: "continuous" - call every frame):
- "move toward" → transform.position += direction * speed * Time.deltaTime
- "move away from" → transform.position -= direction * speed * Time.deltaTime
- "rotate toward" → transform.rotation = Quaternion.RotateTowards(current, target, speed * Time.deltaTime)
- "look at" → transform.LookAt(target)  [if continuous tracking needed]
- "apply force" → rigidbody.AddForce(direction * force, ForceMode.Force)  [in FixedUpdate]
- "apply torque" → rigidbody.AddTorque(torque, ForceMode.Force)  [in FixedUpdate]
- "lerp to position" → transform.position = Vector3.Lerp(current, target, t * Time.deltaTime)
- "smooth rotation" → transform.rotation = Quaternion.Slerp(current, target, t * Time.deltaTime)
- "orbit around" → Calculate orbit position, update transform.position
- "bob up and down" → transform.position.y += Mathf.Sin(Time.time) * amplitude

CONDITIONAL (temporal: "conditional" - check then trigger):
- "check distance to X" → float dist = Vector3.Distance(a, b); if (dist < threshold && !wasInRange) { OnEnterRange(); }
- "if health < X" → if (health < threshold && !wasLowHealth) { OnLowHealth(); wasLowHealth = true; }
- "if X exists" → if (target != null && !wasExisting) { OnTargetFound(); wasExisting = true; }

FORBIDDEN PATTERNS (CRITICAL BUGS):
❌ audioSource.PlayOneShot() in Update() without state change detection or cooldown
❌ Instantiate() in Update() without cooldown/limiting
❌ animator.SetTrigger() in Update() without state change detection
❌ Destroy() in Update() without protection
❌ One-time color/material changes in Update()
❌ Continuous actions without Time.deltaTime multiplier
❌ Physics continuous forces in Update() instead of FixedUpdate()

REQUIRED PATTERNS:
✓ Event actions (temporal: "event") in OnTrigger/OnCollision/State entry/Condition change
✓ Continuous actions (temporal: "continuous") in Update/FixedUpdate with Time.deltaTime
✓ State change detection before event actions
✓ Cooldowns for repeatable events
✓ Condition change tracking for conditional actions
✓ Physics continuous actions in FixedUpdate()

COMPONENT-SPECIFIC RULES:
- AudioSource: 
  * PlayOneShot() = EVENT (temporal: "event")
  * Play()/Stop() = EVENT (temporal: "event")
  * Looping audio: Play() in EnterState, Stop() in ExitState
  
- Rigidbody: 
  * AddForce with Impulse/VelocityChange = EVENT (temporal: "event")
  * AddForce with Force/Acceleration = CONTINUOUS (temporal: "continuous") in FixedUpdate
  
- Animator: 
  * SetTrigger/Play = EVENT (temporal: "event")
  * SetFloat/SetBool for blending = CONTINUOUS (temporal: "continuous")
  
- Transform: 
  * position/rotation assignment with lerp = CONTINUOUS (temporal: "continuous")
  * position/rotation direct assignment = EVENT (temporal: "event") if one-time, CONTINUOUS if every frame

DEPENDENCY HANDLING (CRITICAL - prevents undefined references):
- DO NOT reference external singleton classes (PlayerManager, GameManager, ScoreManager, etc.)
- DO NOT assume static classes exist unless they're Unity built-ins
- For player references, use: GameObject.FindGameObjectWithTag("Player")
- Cache in Start():
  ```csharp
  private Transform _player;
  void Start() {
      GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
      if (playerObj != null) {
          _player = playerObj.transform;
      }
  }
  ```
- ALWAYS add null checks: if (_player == null) return;
- For other game objects, use tags or FindGameObjectWithTag()
- NEVER use: PlayerManager.instance, GameManager.Instance, etc. (these don't exist)

STRUCTURE:
1. using statements
2. public class [EXACT_CLASS_NAME] : MonoBehaviour
3. [SerializeField] private fields (EXACT names from IR)
4. private component references (Rigidbody, Collider, etc.)
5. State enum and state tracking fields (if state machine)
6. Start() - GetComponent calls, initialization, cache player references
7. Update() - State machine logic, continuous behaviors
8. FixedUpdate() - Physics-based continuous behaviors (if needed)
9. Unity event methods (OnTriggerEnter, etc.) for event-based behaviors
10. EnterState() / UpdateState() / ExitState() / DetermineNextState() (if state machine)
11. Behavior implementation methods
12. Helper methods if needed

GENERAL ALGORITHM:
1. Parse IR behavior's trigger type:
   - "collision", "trigger enter", "button press" → OnTrigger/OnCollision method
   - "state is X" → State machine pattern
   - "update", "each frame" → Update pattern with action classification
   - "timer expires", "cooldown ready" → Cooldown pattern

2. For each action in behavior, check its "temporal" field:
   - "event" → Place in state entry/trigger callback/condition change
   - "continuous" → Place in Update/FixedUpdate/state update
   - "conditional" → Add condition check with state tracking

3. Group actions by temporal type within each behavior

4. Generate appropriate Unity lifecycle methods

VALIDATION:
Before generating code, verify:
- No EVENT actions in continuous execution paths (Update loop) without protection
- All CONTINUOUS actions have Time.deltaTime multiplier
- All physics CONTINUOUS actions in FixedUpdate
- State changes properly detected before EVENT actions
- Cooldowns on repeatable EVENTs

Output ONLY the C# code. No markdown, no explanations."""

# ============================================================================
# PIPELINE RESULT
# ============================================================================

@dataclass
class PipelineResult:
    """Complete pipeline result"""
    success: bool
    description: str
    
    # Stage 1: IR Generation (with steering)
    ir_result: Optional[GenerationResult] = None
    ir_json: Optional[dict] = None
    
    # Stage 2: Code Generation
    csharp_code: Optional[str] = None
    rag_docs_used: int = 0
    
    error: Optional[str] = None
    
    def __str__(self):
        if not self.success:
            return f"PipelineResult(success=False, error={self.error})"
        
        ir_status = "steered" if self.ir_result and self.ir_result.was_steered else "clean"
        code_lines = len(self.csharp_code.split('\n')) if self.csharp_code else 0
        
        return f"PipelineResult(success=True, ir={ir_status}, rag_docs={self.rag_docs_used}, lines={code_lines})"

# ============================================================================
# PIPELINE CLASS
# ============================================================================

class UnityPipelineRAG:
    """
    Full Unity script generation pipeline with RAG.
    
    Stage 1: Natural language -> IR JSON (with code leak steering)
    Stage 2: RAG retrieval (monolithic or per-behavior)
    Stage 3: C# code generation with RAG context
    
    RAG modes:
    - monolithic: Single query for entire IR (default, faster)
    - per_behavior: Query per behavior then combine (more docs, better coverage)
    """
    
    def __init__(
        self,
        use_steering: bool = True,
        intensity: float = 0.5,
        verbose: bool = False,
        rag_mode: str = RAG_MODE_MONOLITHIC,  # "monolithic" or "per_behavior"
    ):
        self.verbose = verbose
        self.rag_mode = rag_mode
        self.llm_url = LLM_URL
        self.use_steering = use_steering
        self.steering_intensity = intensity
        
        # Initialize IR generator with code leak steering
        if verbose:
            steering_status = "enabled" if use_steering else "disabled"
            print(f"Initializing IR Generator (code leak steering: {steering_status})...")
        self.ir_generator = UnityIRGenerator(
            use_steering=use_steering,
            intensity=intensity,
            verbose=verbose
        )
        
        # Initialize RAG
        if verbose:
            print("Loading RAG database...")
        self.rag = UnityRAG(db_path=RAG_DB_PATH, verbose=verbose)
        
        if verbose:
            steering_status = "enabled" if use_steering else "disabled"
            print(f"Pipeline ready! RAG: {len(self.rag.documents)} docs, mode: {rag_mode}, steering: {steering_status}\n")
    
    def set_steering(self, enabled: bool, intensity: Optional[float] = None):
        """Enable or disable code leak steering."""
        self.use_steering = enabled
        if intensity is not None:
            self.steering_intensity = intensity
        
        # Reinitialize IR generator with new steering settings
        self.ir_generator = UnityIRGenerator(
            use_steering=enabled,
            intensity=self.steering_intensity,
            verbose=self.verbose
        )
        
        if self.verbose:
            status = "enabled" if enabled else "disabled"
            print(f"Code leak steering {status} (intensity: {self.steering_intensity})")
    
    def generate(self, description: str) -> PipelineResult:
        """Generate complete Unity C# script from natural language description."""
        result = PipelineResult(success=False, description=description)
        
        # ===== STAGE 1: Generate IR JSON =====
        if self.verbose:
            steering_text = "with steering" if self.use_steering else "without steering"
            print(f"\n{'='*60}")
            print(f"STAGE 1: Natural Language -> IR JSON ({steering_text})")
            print('='*60)
        
        ir_result = self.ir_generator.generate(description)
        result.ir_result = ir_result
        
        if not ir_result.success:
            result.error = f"IR generation failed: {ir_result.error}"
            return result
        
        # Validate that parsed result is a dict, not a list
        if not isinstance(ir_result.parsed, dict):
            result.error = f"IR generation returned invalid structure: expected dict, got {type(ir_result.parsed).__name__}"
            if self.verbose:
                print(f"  Error: {result.error}")
                if ir_result.parsed is not None:
                    print(f"  Parsed value: {ir_result.parsed}")
            return result
        
        result.ir_json = ir_result.parsed
        
        if self.verbose:
            print(f"\nIR JSON generated ({ir_result.attempts} attempts)")
            if ir_result.was_steered:
                triggered = ir_result.initial_detection.triggered_attractors if ir_result.initial_detection else []
                print(f"  Code leak steering applied: {triggered}")
            print(f"  Class: {result.ir_json.get('class_name', 'unknown')}")
            print(f"  Components: {result.ir_json.get('components', [])}")
            print(f"  Behaviors: {len(result.ir_json.get('behaviors', []))}")
        
        # ===== STAGE 2: RAG Retrieval =====
        if self.verbose:
            print(f"\n{'='*60}")
            print(f"STAGE 2: RAG Retrieval ({self.rag_mode})")
            print('='*60)
        
        if self.rag_mode == RAG_MODE_PER_BEHAVIOR:
            rag_context, total_docs = self._retrieve_per_behavior(result.ir_json)
            result.rag_docs_used = total_docs
        else:
            # Monolithic mode (default)
            rag_result = self.rag.retrieve_for_ir(result.ir_json, include_content=True)
            rag_context = self.rag.format_context_for_prompt(rag_result.documents) if rag_result else ""
            result.rag_docs_used = len(rag_result.documents) if rag_result else 0
        
        if self.verbose:
            if result.rag_docs_used > 0:
                print(f"  Retrieved {result.rag_docs_used} docs")
            else:
                print("  No RAG context retrieved")
        
        # ===== STAGE 3: Generate C# Code =====
        if self.verbose:
            print(f"\n{'='*60}")
            print("STAGE 3: Generate C# Code")
            print('='*60)
        
        code = self._generate_code(result.ir_json, rag_context)
        
        if not code:
            result.error = "Failed to generate C# code"
            return result
        
        if self.verbose:
            print(f"  Generated {len(code)} chars")
        
        result.csharp_code = code
        result.success = True
        
        return result
    
    def _generate_code(self, ir_json: Dict, rag_context: str) -> Optional[str]:
        """Generate C# code from IR JSON with RAG context, following IR structure closely."""
        try:
            # Build structured prompt that emphasizes IR mapping
            user_content = self._build_structured_prompt(ir_json, rag_context)
            
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
            code = self._clean_code(code)
            
            # Validate and fix issues with targeted regeneration
            validation = self._validate_code_against_ir(code, ir_json)
            
            if validation:
                if self.verbose:
                    print(f"  Validation: {validation}")
                    print(f"  Applying targeted fixes...")
                
                # Apply targeted fixes
                fixed_code = self._apply_targeted_fixes(code, ir_json, validation, rag_context)
                if fixed_code:
                    code = fixed_code
                    # Re-validate after fixes
                    final_validation = self._validate_code_against_ir(code, ir_json)
                    if self.verbose:
                        if final_validation:
                            print(f"  After fixes: {final_validation}")
                        else:
                            print(f"  After fixes: ✓ Passed")
                else:
                    if self.verbose:
                        print(f"  Targeted fixes failed, using original code")
            else:
                if self.verbose:
                    print(f"  Validation: ✓ Passed")
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Code generation error: {e}")
            return None
    
    def _apply_targeted_fixes(self, code: str, ir_json: Dict, validation_issues: str, rag_context: str) -> Optional[str]:
        """Apply targeted fixes to specific issues in the code."""
        try:
            # Parse validation issues to identify what needs fixing
            issues = validation_issues.split("; ")
            missing_fields = []
            wrong_component_types = []
            
            for issue in issues:
                if "Field '" in issue and "not found" in issue:
                    # Extract field name
                    field_match = issue.split("Field '")[1].split("'")[0]
                    missing_fields.append(field_match)
                elif "Component '" in issue:
                    # Extract component name
                    comp_match = issue.split("Component '")[1].split("'")[0]
                    wrong_component_types.append(comp_match)
            
            if not missing_fields and not wrong_component_types:
                return None  # Can't fix unknown issues
            
            # Try programmatic fixes first (for missing fields)
            if missing_fields:
                fixed_code = self._inject_missing_fields_programmatically(code, ir_json, missing_fields)
                if fixed_code:
                    code = fixed_code
                    if self.verbose:
                        print(f"  Injected {len(missing_fields)} missing fields programmatically")
            
            # Always ensure field names match IR exactly (rename if needed)
            code = self._rename_fields_to_match_ir(code, ir_json)
            
            # For component type fixes, use LLM
            if wrong_component_types:
                fix_prompt = self._build_fix_prompt(code, ir_json, [], wrong_component_types, rag_context)
                
                response = requests.post(
                    self.llm_url,
                    json={
                        "model": "local-model",
                        "messages": [
                            {"role": "system", "content": "You are a Unity C# code fixer. Fix ONLY the component type issues. Preserve all other code unchanged."},
                            {"role": "user", "content": fix_prompt}
                        ],
                        "temperature": DEFAULT_TEMPERATURE * 0.5,
                        "max_tokens": 4000
                    },
                    timeout=120
                )
                
                if response.status_code == 200:
                    fixed_code = response.json()["choices"][0]["message"]["content"]
                    code = self._clean_code(fixed_code)
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Targeted fix error: {e}")
            return None
    
    def _inject_missing_fields_programmatically(self, code: str, ir_json: Dict, missing_field_names: List[str]) -> Optional[str]:
        """Programmatically inject missing fields into the code."""
        try:
            # Get field definitions from IR
            fields = ir_json.get("fields", [])
            fields_to_add = [f for f in fields if f.get("name", "") in missing_field_names]
            
            if not fields_to_add:
                return None
            
            # Find the class declaration and opening brace
            lines = code.split('\n')
            class_line_idx = None
            class_brace_idx = None
            first_field_idx = None
            
            for i, line in enumerate(lines):
                # Find class declaration
                if class_line_idx is None and ('class ' in line or 'public class' in line):
                    class_line_idx = i
                    # Check if opening brace is on same line
                    if '{' in line:
                        class_brace_idx = i
                    continue
                
                # Find opening brace if not on same line as class
                if class_line_idx is not None and class_brace_idx is None:
                    if '{' in line:
                        class_brace_idx = i
                        continue
                
                # After finding the class body, look for first field or method
                if class_brace_idx is not None and first_field_idx is None:
                    stripped = line.strip()
                    # Skip empty lines and closing braces
                    if not stripped or stripped == '}':
                        continue
                    # Find first field declaration (with [SerializeField] or private/public)
                    if '[SerializeField]' in line or (stripped.startswith('private ') or stripped.startswith('public ')):
                        # Make sure it's not a method (has parentheses or is void)
                        if '(' not in line and 'void' not in line and '{' not in line:
                            first_field_idx = i
                            break
                    # Or find first method
                    elif 'void ' in line or stripped.startswith('private ') or stripped.startswith('public '):
                        if '(' in line:  # It's a method
                            first_field_idx = i
                            break
            
            if class_line_idx is None:
                return None  # Can't find class
            
            if class_brace_idx is None:
                # Try to find opening brace after class line
                for i in range(class_line_idx + 1, min(class_line_idx + 5, len(lines))):
                    if '{' in lines[i]:
                        class_brace_idx = i
                        break
                
                if class_brace_idx is None:
                    return None  # Can't find class body
            
            # If no fields found, insert right after opening brace
            if first_field_idx is None:
                first_field_idx = class_brace_idx + 1
            
            # Make sure we're inserting inside the class (after the brace)
            if first_field_idx <= class_brace_idx:
                first_field_idx = class_brace_idx + 1
            
            # Build field declarations using EXACT names from IR
            field_declarations = []
            for field in fields_to_add:
                name = field.get("name", "")
                ftype = field.get("type", "")
                default = field.get("default", "")
                
                # Format default value
                if default is None:
                    default_str = "null"
                elif isinstance(default, str):
                    default_str = f'"{default}"'
                elif isinstance(default, bool):
                    default_str = "true" if default else "false"
                else:
                    default_str = str(default)
                
                # Handle special types
                if ftype.endswith("[]"):
                    default_str = "null"
                elif ftype in ["Vector3", "Vector2", "Quaternion", "Color"]:
                    if default is None or default == "null":
                        default_str = f"{ftype}.zero" if ftype != "Color" else "Color.white"
                    else:
                        default_str = str(default)
                
                # Use EXACT field name from IR (no underscore prefix)
                field_decl = f"    [SerializeField] private {ftype} {name} = {default_str};"
                field_declarations.append(field_decl)
            
            # Insert fields
            if field_declarations:
                # Add blank line before if needed
                if first_field_idx > 0 and lines[first_field_idx - 1].strip() and not lines[first_field_idx - 1].strip().startswith('['):
                    field_declarations.insert(0, "")
                
                # Insert fields
                lines = lines[:first_field_idx] + field_declarations + lines[first_field_idx:]
                
                return '\n'.join(lines)
            
            return None
            
        except Exception as e:
            if self.verbose:
                print(f"  Programmatic field injection error: {e}")
            return None
    
    def _rename_fields_to_match_ir(self, code: str, ir_json: Dict) -> str:
        """Rename fields in code to match IR field names exactly."""
        try:
            import re
            fields = ir_json.get("fields", [])
            if not fields:
                return code
            
            # Build mapping of variations to IR field names
            field_mappings = {}
            for field in fields:
                ir_name = field.get("name", "")
                if not ir_name:
                    continue
                
                # Always check for underscore prefix variation (most common)
                underscore_variation = f"_{ir_name}"
                
                # Check if underscore variation exists in code
                if underscore_variation in code:
                    # Check if it's actually used as a field (not just a substring)
                    # Look for field declaration patterns
                    field_patterns = [
                        rf'\bprivate\s+\w+\s+{re.escape(underscore_variation)}\b',
                        rf'\[SerializeField\]\s+private\s+\w+\s+{re.escape(underscore_variation)}\b',
                        rf'\bpublic\s+\w+\s+{re.escape(underscore_variation)}\b',
                    ]
                    
                    is_field = False
                    for pattern in field_patterns:
                        if re.search(pattern, code):
                            is_field = True
                            break
                    
                    # Also check if it's used as a variable (with word boundaries)
                    if not is_field:
                        if re.search(rf'\b{re.escape(underscore_variation)}\b', code):
                            is_field = True
                    
                    if is_field:
                        field_mappings[underscore_variation] = ir_name
            
            # Perform replacements (order matters - do declarations first, then usage)
            if field_mappings:
                for old_name, new_name in field_mappings.items():
                    # Replace field declarations (most specific patterns first)
                    code = re.sub(
                        rf'(\[SerializeField\]\s+private\s+\w+\s+){re.escape(old_name)}(\s*[=;])',
                        rf'\1{new_name}\2',
                        code
                    )
                    code = re.sub(
                        rf'\bprivate\s+\w+\s+{re.escape(old_name)}(\s*[=;])',
                        lambda m: m.group(0).replace(old_name, new_name),
                        code
                    )
                    code = re.sub(
                        rf'\bpublic\s+\w+\s+{re.escape(old_name)}(\s*[=;])',
                        lambda m: m.group(0).replace(old_name, new_name),
                        code
                    )
                    
                    # Replace all other usages (with word boundaries to avoid partial matches)
                    code = re.sub(rf'\b{re.escape(old_name)}\b', new_name, code)
                    
                    if self.verbose:
                        print(f"  Renamed field '{old_name}' -> '{new_name}' to match IR")
            
            return code
            
        except Exception as e:
            if self.verbose:
                print(f"  Field renaming error: {e}")
            return code
    
    def _build_fix_prompt(self, code: str, ir_json: Dict, missing_fields: List[str], wrong_components: List[str], rag_context: str) -> str:
        """Build a prompt for targeted fixes."""
        parts = []
        
        parts.append("=== TARGETED CODE FIX ===")
        parts.append("")
        parts.append("Fix ONLY the specific issues below. Keep ALL other code unchanged.")
        parts.append("")
        
        if missing_fields:
            parts.append("=== MISSING FIELDS ===")
            parts.append("Add these fields to the class using EXACT names from IR:")
            fields = ir_json.get("fields", [])
            for field in fields:
                field_name = field.get("name", "")
                if field_name in missing_fields:
                    ftype = field.get("type", "")
                    default = field.get("default", "")
                    parts.append(f"  - {field_name} ({ftype}) = {default}")
                    parts.append(f"    → Add: [SerializeField] private {ftype} {field_name} = {default};")
            parts.append("")
            parts.append("CRITICAL: Use the EXACT field names above. Do NOT rename them.")
            parts.append("")
        
        if wrong_components:
            parts.append("=== COMPONENT FIXES ===")
            parts.append("Ensure these components are referenced correctly:")
            for comp in wrong_components:
                parts.append(f"  - Use GetComponent<{comp}>() (not {comp}2D)")
            parts.append("")
        
        if rag_context:
            parts.append("=== UNITY API DOCUMENTATION ===")
            parts.append(rag_context[:1000])  # Limit context for fix prompt
            parts.append("")
        
        parts.append("=== CURRENT CODE (fix the issues above) ===")
        parts.append("```csharp")
        parts.append(code)
        parts.append("```")
        parts.append("")
        parts.append("Output ONLY the fixed C# code with the missing fields added and components corrected.")
        parts.append("Keep all other code exactly as it is.")
        
        return "\n".join(parts)
    
    def _build_structured_prompt(self, ir_json: Dict, rag_context: str) -> str:
        """Build a structured prompt that emphasizes IR mapping with temporal classification."""
        parts = []
        
        if rag_context:
            parts.append("=== UNITY API DOCUMENTATION ===")
            parts.append(rag_context)
            parts.append("")
        
        parts.append("=== BEHAVIOR SPECIFICATION (MUST FOLLOW EXACTLY) ===")
        parts.append("")
        
        # Class name
        class_name = ir_json.get("class_name", "Behavior")
        parts.append(f"CLASS NAME: {class_name}")
        parts.append("")
        
        # Components
        components = ir_json.get("components", [])
        if components:
            parts.append("COMPONENTS (get in Start()):")
            for comp in components:
                parts.append(f"  - {comp}")
            parts.append("")
        
        # Fields
        fields = ir_json.get("fields", [])
        if fields:
            parts.append("FIELDS (declare exactly as shown):")
            for field in fields:
                name = field.get("name", "")
                ftype = field.get("type", "")
                default = field.get("default", "")
                parts.append(f"  - {name} ({ftype}) = {default}")
            parts.append("")
        
        # State Machine (if present)
        state_machine = ir_json.get("state_machine", {})
        if isinstance(state_machine, dict) and state_machine.get("has_state_machine", False):
            parts.append("STATE MACHINE (IMPLEMENT WITH ENUM AND SWITCH):")
            initial_state = state_machine.get("initial_state", "")
            states = state_machine.get("states", [])
            
            parts.append(f"  Initial State: {initial_state}")
            parts.append("  States:")
            for state in states:
                state_name = state.get("name", "")
                actions = state.get("actions", [])
                state_transitions = state.get("transitions", [])
                
                # Group actions by temporal type
                event_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "event"]
                continuous_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "continuous"]
                conditional_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "conditional"]
                
                parts.append(f"    - {state_name}:")
                if event_actions:
                    parts.append(f"        EVENT actions ({len(event_actions)}): Execute ONCE in EnterState()")
                    for action in event_actions:
                        action_text = action.get("action", str(action))
                        parts.append(f"          • {action_text}")
                if continuous_actions:
                    parts.append(f"        CONTINUOUS actions ({len(continuous_actions)}): Execute EVERY FRAME in UpdateState()")
                    for action in continuous_actions:
                        action_text = action.get("action", str(action))
                        parts.append(f"          • {action_text}")
                if conditional_actions:
                    parts.append(f"        CONDITIONAL actions ({len(conditional_actions)}): Check in UpdateState(), trigger events on change")
                    for action in conditional_actions:
                        action_text = action.get("action", str(action))
                        parts.append(f"          • {action_text}")
                
                if state_transitions:
                    parts.append(f"        Transitions:")
                    for trans in state_transitions:
                        to_state = trans.get("to", "")
                        condition = trans.get("condition", "")
                        parts.append(f"          → {to_state} (when {condition})")
            
            parts.append("")
            parts.append("  IMPLEMENTATION REQUIREMENTS:")
            parts.append("  - Create State enum with all state names")
            parts.append("  - Track _currentState and _previousState")
            parts.append("  - EnterState() → Execute EVENT actions (temporal: \"event\") ONCE")
            parts.append("  - UpdateState() → Execute CONTINUOUS actions (temporal: \"continuous\") EVERY FRAME")
            parts.append("  - DetermineNextState() → Check transition conditions")
            parts.append("  - CRITICAL: Audio PlayOneShot() ONLY in EnterState(), NEVER in UpdateState()")
            parts.append("")
        
        # Behaviors (simple behaviors, not state-based)
        behaviors = ir_json.get("behaviors", [])
        if behaviors:
            parts.append("BEHAVIORS (implement each one with correct execution timing):")
            for i, behavior in enumerate(behaviors, 1):
                if isinstance(behavior, dict):
                    name = behavior.get("name", f"behavior_{i}")
                    trigger = behavior.get("trigger", "")
                    actions = behavior.get("actions", [])
                    
                    # Group actions by temporal type
                    event_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "event"]
                    continuous_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "continuous"]
                    conditional_actions = [a for a in actions if isinstance(a, dict) and a.get("temporal") == "conditional"]
                    legacy_actions = [a for a in actions if not isinstance(a, dict) or "temporal" not in a]
                    
                    parts.append(f"  {i}. {name}")
                    parts.append(f"     Trigger: {trigger}")
                    
                    if event_actions:
                        parts.append(f"     EVENT actions ({len(event_actions)} - execute ONCE):")
                        for action in event_actions:
                            action_text = action.get("action", str(action))
                            parts.append(f"       - {action_text}")
                        parts.append(f"     → IMPLEMENT IN: EnterState() or one-time event method (OnTriggerEnter, etc.)")
                        parts.append(f"     → CRITICAL: Audio PlayOneShot() ONLY here, NEVER in Update()")
                    
                    if continuous_actions:
                        parts.append(f"     CONTINUOUS actions ({len(continuous_actions)} - execute EVERY FRAME):")
                        for action in continuous_actions:
                            action_text = action.get("action", str(action))
                            parts.append(f"       - {action_text}")
                        parts.append(f"     → IMPLEMENT IN: Update() or FixedUpdate() with Time.deltaTime")
                    
                    if conditional_actions:
                        parts.append(f"     CONDITIONAL actions ({len(conditional_actions)} - check then trigger):")
                        for action in conditional_actions:
                            action_text = action.get("action", str(action))
                            parts.append(f"       - {action_text}")
                        parts.append(f"     → IMPLEMENT IN: Update() with state tracking to prevent re-triggering")
                    
                    if legacy_actions:
                        parts.append(f"     Actions (legacy format - no temporal specified):")
                        for action in legacy_actions:
                            if isinstance(action, dict):
                                action_text = action.get("action", str(action))
                            else:
                                action_text = str(action)
                            parts.append(f"       - {action_text}")
                        parts.append(f"     → IMPLEMENT IN: Infer from action type (audio/spawn = event, movement = continuous)")
                else:
                    parts.append(f"  {i}. {behavior}")
            parts.append("")
        
        # Dependency warnings
        parts.append("=== DEPENDENCY HANDLING ===")
        parts.append("DO NOT reference undefined classes:")
        parts.append("  - NO PlayerManager.instance, GameManager.Instance, etc.")
        parts.append("  - Use GameObject.FindGameObjectWithTag(\"Player\") for player references")
        parts.append("  - Cache in Start() and add null checks")
        parts.append("")
        
        # Full JSON for reference
        parts.append("=== FULL IR JSON (for reference) ===")
        parts.append(json.dumps(ir_json, indent=2))
        parts.append("")
        parts.append("Generate the complete Unity C# MonoBehaviour script following the specification above.")
        parts.append("")
        parts.append("CRITICAL REMINDERS:")
        parts.append("- EVENT actions (temporal: \"event\") → Execute ONCE (EnterState() or one-time events)")
        parts.append("- CONTINUOUS actions (temporal: \"continuous\") → Execute EVERY FRAME (Update() or FixedUpdate())")
        parts.append("- CONDITIONAL actions (temporal: \"conditional\") → Check in Update(), trigger events on change")
        parts.append("- Audio PlayOneShot() → ONLY in EnterState() or one-time events, NEVER in Update()")
        parts.append("- State machines → Use enum, switch statements, EnterState/UpdateState pattern")
        parts.append("- Dependencies → Use FindGameObjectWithTag(), never assume singletons exist")
        parts.append("- All continuous actions MUST use Time.deltaTime for frame-rate independence")
        parts.append("- Physics continuous forces MUST be in FixedUpdate()")
        
        return "\n".join(parts)
    
    def _validate_code_against_ir(self, code: str, ir_json: Dict) -> Optional[str]:
        """Validate that code follows IR structure exactly."""
        issues = []
        
        # Check class name
        class_name = ir_json.get("class_name", "")
        if class_name:
            # Check for exact class name (allowing for "public class ClassName")
            if f"class {class_name}" not in code and f"class {class_name} " not in code:
                issues.append(f"Class name '{class_name}' not found exactly")
        
        # Check fields - must use exact names (strict checking)
        fields = ir_json.get("fields", [])
        for field in fields:
            field_name = field.get("name", "")
            if not field_name:
                continue
            
            # Check for exact field name (prefer exact match, but allow underscore prefix as warning)
            # Look for exact field_name first
            exact_patterns = [
                f" {field_name} ",  # Exact match with spaces
                f" {field_name}=",  # As assignment
                f"[SerializeField] private {field_name}",  # Serialized field
                f"private {field_name}",  # Private field
                f"public {field_name}",  # Public field
            ]
            
            found_exact = False
            for pattern in exact_patterns:
                if pattern in code:
                    found_exact = True
                    break
            
            # Also check if it's used as a variable
            if not found_exact:
                usage_patterns = [
                    f"{field_name}.",
                    f"{field_name} =",
                ]
                for pattern in usage_patterns:
                    if pattern in code:
                        found_exact = True
                        break
            
            # If exact not found, check for underscore variant (but flag as issue)
            if not found_exact:
                underscore_patterns = [
                    f"_{field_name} ",
                    f"_{field_name}=",
                    f"[SerializeField] private _{field_name}",
                    f"private _{field_name}",
                ]
                found_underscore = False
                for pattern in underscore_patterns:
                    if pattern in code:
                        found_underscore = True
                        break
                
                if not found_underscore:
                    # Check usage with underscore
                    if f"_{field_name}." in code or f"_{field_name} =" in code:
                        found_underscore = True
                
                if found_underscore:
                    issues.append(f"Field '{field_name}' found as '_{field_name}' (should use exact IR name)")
                else:
                    issues.append(f"Field '{field_name}' not found (check exact name)")
        
        # Check components - must use exact types
        components = ir_json.get("components", [])
        for comp in components:
            # Check for GetComponent<ComponentType> or component reference
            if f"GetComponent<{comp}>" not in code and f"GetComponent<{comp}2D>" not in code:
                # Also check if it's referenced as a variable
                comp_lower = comp.lower()
                if comp_lower not in code.lower() and comp not in code:
                    issues.append(f"Component '{comp}' not referenced (check exact type)")
        
        # Check behaviors - must implement all
        behaviors = ir_json.get("behaviors", [])
        found_behaviors = 0
        for behavior in behaviors:
            if isinstance(behavior, dict):
                trigger = behavior.get("trigger", "")
                name = behavior.get("name", "")
                actions = behavior.get("actions", [])
                
                # Check if trigger keywords appear
                trigger_keywords = [w for w in trigger.split() if len(w) > 3][:3]
                trigger_found = any(kw.lower() in code.lower() for kw in trigger_keywords)
                
                # Check if behavior name appears
                name_found = name.lower() in code.lower() if name else False
                
                # Check if action keywords appear
                action_found = False
                for action in actions:
                    if isinstance(action, dict):
                        action_text = action.get("action", "")
                    else:
                        action_text = str(action)
                    action_keywords = [w for w in action_text.split() if len(w) > 3][:2]
                    if any(kw.lower() in code.lower() for kw in action_keywords):
                        action_found = True
                        break
                
                if trigger_found or name_found or action_found:
                    found_behaviors += 1
            else:
                if str(behavior).lower()[:20] in code.lower():
                    found_behaviors += 1
        
        if found_behaviors < len(behaviors) * 0.6:  # At least 60% should be found
            issues.append(f"Only {found_behaviors}/{len(behaviors)} behaviors detected")
        
        # Check for duplicate variable declarations
        duplicate_issues = self._check_duplicate_declarations(code)
        issues.extend(duplicate_issues)
        
        if issues:
            return "; ".join(issues)
        return None
    
    def _check_duplicate_declarations(self, code: str) -> List[str]:
        """
        Check for duplicate variable declarations in C# code.
        Returns list of issue messages.
        """
        issues = []
        lines = code.split('\n')
        
        # Track field declarations (class-level variables)
        field_declarations = {}  # var_name -> list of line_nums
        
        # Track method parameters
        method_params = {}  # method_name -> list of param_names
        
        for line_num, line in enumerate(lines, 1):
            stripped = line.strip()
            
            # Skip comments
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
                continue
            
            # Check for field declarations (class-level)
            # Pattern: [attributes] access_modifier type var_name [= value];
            field_patterns = [
                r'\[(?:SerializeField|Header)[^\]]*\]\s*(?:private|public|protected|internal)?\s+\w+\s+(\w+)\s*[=;]',
                r'(?:private|public|protected|internal)\s+\w+\s+(\w+)\s*[=;]',
            ]
            
            for pattern in field_patterns:
                matches = re.finditer(pattern, line)
                for match in matches:
                    var_name = match.group(1)
                    if var_name not in field_declarations:
                        field_declarations[var_name] = []
                    field_declarations[var_name].append(line_num)
            
            # Check for method parameters (simplified - just check for duplicate params in same signature)
            method_match = re.search(r'\b(?:private|public|protected|internal)?\s*(?:static)?\s*\w+\s+(\w+)\s*\(([^)]*)\)', line)
            if method_match:
                method_name = method_match.group(1)
                params_str = method_match.group(2)
                # Extract parameter names
                param_names = []
                for param in params_str.split(','):
                    param = param.strip()
                    if param:
                        # Extract name (last word before = or end)
                        param_match = re.search(r'(\w+)(?:\s*=\s*[^,]+)?$', param)
                        if param_match:
                            param_names.append(param_match.group(1))
                # Check for duplicate parameter names in same method
                seen_params = set()
                for param_name in param_names:
                    if param_name in seen_params:
                        issues.append(f"Line {line_num}: Duplicate parameter '{param_name}' in method '{method_name}'")
                    seen_params.add(param_name)
        
        # Check for duplicate field declarations
        for var_name, line_nums in field_declarations.items():
            if len(line_nums) > 1:
                issues.append(f"Duplicate field declaration '{var_name}' at lines {', '.join(map(str, line_nums))}")
        
        return issues
    
    def _retrieve_per_behavior(self, ir_json: Dict) -> Tuple[str, int]:
        """
        Retrieve RAG docs per behavior, then combine.
        
        Returns: (combined_rag_context, total_unique_docs)
        """
        behaviors = ir_json.get("behaviors", [])
        all_docs = {}  # api_name -> doc (for deduplication)
        
        for i, behavior in enumerate(behaviors):
            # Handle string or dict format
            if isinstance(behavior, str):
                trigger = "Update"
                actions = [behavior]
            elif isinstance(behavior, dict):
                trigger = behavior.get("trigger", "")
                actions = behavior.get("actions", [])
                # Normalize actions to strings
                actions = [a if isinstance(a, str) else str(a) for a in actions]
            else:
                continue
            
            # Build focused query for this behavior
            query = self._build_behavior_query(trigger, actions)
            
            if self.verbose:
                print(f"  Behavior {i+1}: {query[:60]}...")
            
            # Retrieve docs for this behavior
            try:
                docs = self.rag.search(
                    query=query,
                    namespaces=None,
                    threshold=0.45,
                    top_k=DOCS_PER_BEHAVIOR
                )
                
                # Deduplicate by api_name
                for doc in docs:
                    if doc.api_name not in all_docs:
                        all_docs[doc.api_name] = doc
                        if self.verbose:
                            print(f"    + {doc.api_name} ({doc.score:.2f})")
            except Exception as e:
                if self.verbose:
                    print(f"    RAG error: {e}")
        
        # Format combined context
        unique_docs = list(all_docs.values())
        if unique_docs:
            rag_context = self.rag.format_context_for_prompt(unique_docs, max_tokens=4000, include_content=True)
        else:
            rag_context = ""
        
        return rag_context, len(unique_docs)
    
    def _build_behavior_query(self, trigger: str, actions: List[str]) -> str:
        """Build a focused search query from behavior trigger and actions."""
        parts = [trigger] + actions
        text = " ".join(parts)
        
        # Extract Unity API terms (class names, method names)
        api_terms = []
        unity_types = {
            'Rigidbody', 'Transform', 'GameObject', 'AudioSource', 'AudioClip',
            'ParticleSystem', 'Light', 'Camera', 'Collider', 'Animator',
            'Vector3', 'Quaternion', 'Time', 'Input', 'Physics', 'Mathf'
        }
        
        words = re.findall(r'\b[A-Z][a-zA-Z0-9]+\b', text)
        for word in words:
            if word in unity_types or len(word) > 4:
                api_terms.append(word)
        
        # Combine into query
        if api_terms:
            query = f"Unity {', '.join(api_terms[:5])} {text[:100]}"
        else:
            query = f"Unity {text[:150]}"
        
        return query
    
    def _clean_code(self, code: str) -> str:
        """Clean C# code from markdown formatting."""
        code = code.strip()
        if code.startswith("```"):
            lines = code.split("\n")
            if lines[0].startswith("```"):
                lines = lines[1:]
            if lines and lines[-1].strip() == "```":
                lines = lines[:-1]
            code = "\n".join(lines)
        return code.strip()

# ============================================================================
# INTERACTIVE MODE
# ============================================================================

def interactive_mode(pipeline: UnityPipelineRAG):
    """Interactive generation session"""
    print("="*60)
    print("UNITY FULL PIPELINE (RAG) - Interactive Mode")
    print("="*60)
    print("Generate complete Unity C# scripts from natural language.")
    print("With code leak steering + RAG-based code generation")
    print("Commands: quit, verbose, quiet, ir, code, both, steering-on, steering-off")
    print("="*60)
    
    # Show initial steering status
    steering_status = "enabled" if pipeline.use_steering else "disabled"
    print(f"Code leak steering: {steering_status}")
    
    show_ir = True
    show_code = True
    
    while True:
        try:
            prompt = input("\n> ").strip()
        except (EOFError, KeyboardInterrupt):
            print("\nExiting.")
            break
        
        if not prompt:
            continue
        
        cmd = prompt.lower()
        if cmd == "quit":
            break
        elif cmd == "verbose":
            pipeline.verbose = True
            pipeline.ir_generator.verbose = True
            print("Verbose mode ON")
            continue
        elif cmd == "quiet":
            pipeline.verbose = False
            pipeline.ir_generator.verbose = False
            print("Verbose mode OFF")
            continue
        elif cmd == "ir":
            show_ir = True
            show_code = False
            print("Showing IR JSON only")
            continue
        elif cmd == "code":
            show_ir = False
            show_code = True
            print("Showing C# code only")
            continue
        elif cmd == "both":
            show_ir = True
            show_code = True
            print("Showing both IR and code")
            continue
        elif cmd == "steering-on":
            pipeline.set_steering(True)
            continue
        elif cmd == "steering-off":
            pipeline.set_steering(False)
            continue
        
        # Generate
        result = pipeline.generate(prompt)
        
        print(f"\n{result}")
        
        if result.success:
            if show_ir and result.ir_json:
                print(f"\n{'-'*40}")
                print("IR JSON:")
                print('-'*40)
                print(json.dumps(result.ir_json, indent=2))
            
            if show_code and result.csharp_code:
                print(f"\n{'-'*40}")
                print("C# Script:")
                print('-'*40)
                print(result.csharp_code)
        else:
            print(f"\nError: {result.error}")

# ============================================================================
# MAIN
# ============================================================================

def main():
    parser = argparse.ArgumentParser(
        description="Unity Full Pipeline (RAG): NL -> IR (steered) -> C# (RAG)"
    )
    parser.add_argument("description", nargs="*", help="Behavior description")
    parser.add_argument("-i", "--interactive", action="store_true")
    parser.add_argument("--no-steering", "--disable-steering", action="store_true", 
                       dest="no_steering", help="Disable code leak steering for IR generation")
    parser.add_argument("--intensity", type=float, default=0.5, help="Steering intensity 0-1 (only used when steering is enabled)")
    parser.add_argument("--rag-mode", choices=["monolithic", "per_behavior"], default="monolithic",
                       help="RAG retrieval mode: monolithic (single query) or per_behavior (query per behavior)")
    parser.add_argument("-v", "--verbose", action="store_true")
    
    args = parser.parse_args()
    
    pipeline = UnityPipelineRAG(
        use_steering=not args.no_steering,
        intensity=args.intensity,
        verbose=args.verbose,
        rag_mode=args.rag_mode
    )
    
    if args.interactive or not args.description:
        interactive_mode(pipeline)
    else:
        description = " ".join(args.description)
        result = pipeline.generate(description)
        
        if result.success:
            print("\n" + "="*60)
            print("IR JSON:")
            print("="*60)
            print(json.dumps(result.ir_json, indent=2))
            
            print("\n" + "="*60)
            print("Generated C# Script:")
            print("="*60)
            print(result.csharp_code)
        else:
            print(f"Error: {result.error}")


if __name__ == "__main__":
    main()

