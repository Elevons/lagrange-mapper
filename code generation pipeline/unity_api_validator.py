#!/usr/bin/env python3
"""
Unity API Validator
====================
Strict validation system to catch LLM hallucinations.

The key insight: LLMs hallucinate plausible-sounding APIs that don't exist.
This validator catches common patterns like:
- ParticleSystem.GetEmission() -> should be .emission (property access)
- transform.distance() -> should be Vector3.Distance()
- Light.beamHeight -> doesn't exist
- new AudioSource() -> components can't be constructed

Usage:
    from unity_api_validator import UnityAPIValidator
    validator = UnityAPIValidator()
    issues = validator.validate_code(code)
"""

import re
from typing import List, Dict, Set, Tuple, Optional
from dataclasses import dataclass, field


@dataclass
class ValidationIssue:
    """A detected API issue"""
    line_num: int
    code_snippet: str
    issue_type: str  # "invalid_method", "wrong_accessor", "invalid_constructor", etc.
    invalid_api: str
    suggested_fix: str
    confidence: float = 1.0
    
    def __str__(self):
        return f"Line {self.line_num}: {self.issue_type} - {self.invalid_api} -> {self.suggested_fix}"


class UnityAPIValidator:
    """
    Strict Unity API validator with pattern-based hallucination detection.
    
    Uses three tiers of validation:
    1. Known hallucination patterns (high confidence)
    2. Invalid accessor patterns (method vs property)
    3. Component constructor validation
    """
    
    def __init__(self, verbose: bool = False):
        self.verbose = verbose
        self._build_validation_rules()
    
    def _build_validation_rules(self):
        """Build comprehensive validation rules"""
        
        # ==================================================================
        # TIER 1: KNOWN HALLUCINATION PATTERNS
        # LLMs consistently invent these - immediate rejection
        # ==================================================================
        
        self.hallucination_patterns = [
            # ParticleSystem - most common hallucinations
            (r'ParticleSystem\.GetEmission\s*\(', 
             "ParticleSystem.GetEmission()", 
             "Use particleSystem.emission (it's a property, not a method)"),
            
            (r'ParticleSystem\.GetVelocity\s*\(', 
             "ParticleSystem.GetVelocity()", 
             "Use particleSystem.velocityOverLifetime (property access)"),
            
            (r'ParticleSystem\.GetColor\s*\(', 
             "ParticleSystem.GetColor()", 
             "Use particleSystem.colorOverLifetime (property access)"),
            
            (r'ParticleSystem\.GetMain\s*\(', 
             "ParticleSystem.GetMain()", 
             "Use particleSystem.main (property access)"),
            
            (r'ParticleSystem\.GetLifetime\s*\(', 
             "ParticleSystem.GetLifetime()", 
             "Use particleSystem.main.startLifetime or .lifetimeByEmitterSpeed"),
            
            (r'ParticleSystem\.GetScale\s*\(', 
             "ParticleSystem.GetScale()", 
             "Use particleSystem.main.startSize for size settings"),
            
            (r'ParticleSystem\.GetDrift\s*\(', 
             "ParticleSystem.GetDrift()", 
             "No drift module - use velocityOverLifetime or forceOverLifetime"),
            
            (r'ParticleSystem\.GetAudio\s*\(', 
             "ParticleSystem.GetAudio()", 
             "ParticleSystem has no audio module - use separate AudioSource"),
            
            (r'ParticleSystem\.GetForce\s*\(', 
             "ParticleSystem.GetForce()", 
             "Use particleSystem.forceOverLifetime (property access)"),
            
            (r'ParticleSystemVelocity(?!Module)\b', 
             "ParticleSystemVelocity", 
             "Use ParticleSystem.VelocityOverLifetimeModule"),
            
            (r'ParticleSystemLifetimeConfig', 
             "ParticleSystemLifetimeConfig", 
             "Use ParticleSystem.MainModule.startLifetime (MinMaxCurve)"),
            
            (r'ParticleSystemScaleConfig', 
             "ParticleSystemScaleConfig", 
             "Use ParticleSystem.MainModule.startSize"),
            
            (r'ParticleSystemColorConfig', 
             "ParticleSystemColorConfig", 
             "Use ParticleSystem.ColorOverLifetimeModule"),
            
            (r'ParticleSystemDriftConfig', 
             "ParticleSystemDriftConfig", 
             "No such class - use VelocityOverLifetimeModule or ForceOverLifetimeModule"),
            
            (r'ParticleSystemAudioConfig', 
             "ParticleSystemAudioConfig", 
             "No such class - use AudioSource component instead"),
            
            (r'ParticleSystem\.EmissionConfig', 
             "ParticleSystem.EmissionConfig", 
             "Use ParticleSystem.EmissionModule (access via .emission property)"),
            
            (r'\.startParticles\b', 
             "startParticles", 
             "Use ParticleSystem.main.maxParticles or .particleCount"),
            
            (r'emission\.startPos\[', 
             "emission.startPos[]", 
             "EmissionModule doesn't have startPos - use ShapeModule for spawn positions"),
            
            (r'emission\.maxParticles\s*=', 
             "emission.maxParticles", 
             "Use particleSystem.main.maxParticles (it's on MainModule)"),
            
            (r'velocity\.startSpeed\[', 
             "velocity.startSpeed[]", 
             "VelocityOverLifetimeModule uses .x/.y/.z MinMaxCurves, not arrays"),
            
            # Light hallucinations
            (r'\.beamHeight\b', 
             "Light.beamHeight", 
             "Light has no beamHeight property - use spotAngle for cone lights"),
            
            (r'\.beamDirection\b', 
             "Light.beamDirection", 
             "Light has no beamDirection - use transform.forward for direction"),
            
            (r'Light\.target\b', 
             "Light.target", 
             "Light has no target property - use LookAt() on transform"),
            
            (r'Light\.position\b', 
             "Light.position", 
             "Use light.transform.position (Light inherits from Component)"),
            
            # Transform hallucinations  
            (r'transform\.distance\s*\(', 
             "transform.distance()", 
             "Use Vector3.Distance(transform.position, other.position)"),
            
            (r'transform\.distanceTo\s*\(', 
             "transform.distanceTo()", 
             "Use Vector3.Distance(transform.position, other.position)"),
            
            (r'\.position\.distanceTo\s*\(', 
             "position.distanceTo()", 
             "Use Vector3.Distance(pos1, pos2)"),
            
            (r'transform\.enabled\b', 
             "Transform.enabled", 
             "Transform has no enabled - use gameObject.SetActive(bool)"),
            
            (r'transform\.isPlaying\b', 
             "Transform.isPlaying", 
             "Transform has no isPlaying - check specific component like AudioSource or ParticleSystem"),
            
            (r'transform\.color\b', 
             "Transform.color", 
             "Transform has no color - use Renderer.material.color"),
            
            # Quaternion hallucinations
            (r'Quaternion\.Rotation\b', 
             "Quaternion.Rotation", 
             "Use Quaternion.Euler() or transform.rotation"),
            
            (r'\.Rotation\s*\*\s*', 
             ".Rotation *", 
             "Quaternion is a struct, not a property - multiply quaternions directly"),
            
            # AudioSource hallucinations
            (r'AudioSource\.minVolume\b', 
             "AudioSource.minVolume", 
             "AudioSource has no minVolume - just use .volume (0-1 range)"),
            
            (r'AudioSource\.maxVolume\b', 
             "AudioSource.maxVolume", 
             "AudioSource has no maxVolume - just use .volume (0-1 range)"),
            
            (r'audioSource\.clip\.Length\b', 
             "AudioClip.Length", 
             "Use audioSource.clip.length (lowercase 'l')"),
            
            (r'new\s+AudioClip\s*\(', 
             "new AudioClip()", 
             "Can't construct AudioClip - load via Resources.Load<AudioClip>() or assign in Inspector"),
            
            # Collider hallucinations
            (r'collider\.activeSelf\b', 
             "Collider.activeSelf", 
             "Collider has no activeSelf - use collider.enabled or gameObject.activeSelf"),
            
            # AnimationCurve hallucinations
            (r'AnimationCurve\.length\b', 
             "AnimationCurve.length", 
             "Use animationCurve.keys.Length for keyframe count"),
            
            # Mathf/MathF confusion
            (r'\bMathF\s*\.', 
             "MathF.Method", 
             "Use Mathf (Unity's math class), not MathF (System namespace)"),
            
            (r'\bMathf\s+[A-Z]', 
             "Mathf Method (missing dot)", 
             "Missing dot - use Mathf.Method() not Mathf Method"),
            
            # Random hallucinations
            (r'\bRandom\s+audios\b', 
             "Random audios", 
             "Invalid syntax - use Random.Range() or create AudioClip array"),
            
            # Rigidbody hallucinations  
            (r'Rigidbody\.mass\s*=.*_gravityScale', 
             "Setting mass for gravity", 
             "For gravity scaling, use Rigidbody.useGravity + Physics.gravity or custom force"),
        ]
        
        # ==================================================================
        # TIER 2: INVALID CONSTRUCTORS
        # Unity components can't be created with 'new'
        # ==================================================================
        
        self.no_constructor_types = {
            # Core components
            'AudioSource', 'AudioClip', 'AudioListener',
            'Rigidbody', 'Rigidbody2D',
            'Collider', 'BoxCollider', 'SphereCollider', 'CapsuleCollider', 'MeshCollider',
            'Collider2D', 'BoxCollider2D', 'CircleCollider2D', 'PolygonCollider2D',
            'ParticleSystem', 'ParticleSystemRenderer',
            'Light', 'Camera',
            'Animator', 'Animation',
            'Renderer', 'MeshRenderer', 'SkinnedMeshRenderer', 'SpriteRenderer', 'LineRenderer', 'TrailRenderer',
            'Canvas', 'CanvasRenderer', 'CanvasGroup',
            'RectTransform',
            'NavMeshAgent', 'NavMeshObstacle',
            'CharacterController',
            'Terrain', 'TerrainCollider',
            'WindZone',
            'Joint', 'HingeJoint', 'FixedJoint', 'SpringJoint', 'ConfigurableJoint',
            'ConstantForce',
            'Cloth',
            'WheelCollider',
            # UI components
            'Text', 'Image', 'Button', 'Toggle', 'Slider', 'Scrollbar', 'Dropdown', 'InputField',
            'ScrollRect', 'Mask', 'RawImage',
        }
        
        # ==================================================================
        # TIER 3: PARTICLE SYSTEM MODULE ACCESS PATTERNS
        # These are properties, not methods
        # ==================================================================
        
        self.particle_modules = {
            'main': 'MainModule',
            'emission': 'EmissionModule', 
            'shape': 'ShapeModule',
            'velocityOverLifetime': 'VelocityOverLifetimeModule',
            'limitVelocityOverLifetime': 'LimitVelocityOverLifetimeModule',
            'inheritVelocity': 'InheritVelocityModule',
            'forceOverLifetime': 'ForceOverLifetimeModule',
            'colorOverLifetime': 'ColorOverLifetimeModule',
            'colorBySpeed': 'ColorBySpeedModule',
            'sizeOverLifetime': 'SizeOverLifetimeModule',
            'sizeBySpeed': 'SizeBySpeedModule',
            'rotationOverLifetime': 'RotationOverLifetimeModule',
            'rotationBySpeed': 'RotationBySpeedModule',
            'externalForces': 'ExternalForcesModule',
            'noise': 'NoiseModule',
            'collision': 'CollisionModule',
            'trigger': 'TriggerModule',
            'subEmitters': 'SubEmittersModule',
            'textureSheetAnimation': 'TextureSheetAnimationModule',
            'lights': 'LightsModule',
            'trails': 'TrailModule',
            'customData': 'CustomDataModule',
        }
        
        # ==================================================================
        # TIER 4: CORRECT API PATTERNS (for positive validation)
        # ==================================================================
        
        self.valid_static_calls = {
            # Physics
            'Physics.Raycast', 'Physics.RaycastAll', 'Physics.RaycastNonAlloc',
            'Physics.SphereCast', 'Physics.SphereCastAll', 'Physics.SphereCastNonAlloc',
            'Physics.BoxCast', 'Physics.BoxCastAll',
            'Physics.OverlapSphere', 'Physics.OverlapSphereNonAlloc',
            'Physics.OverlapBox', 'Physics.OverlapBoxNonAlloc',
            'Physics.CheckSphere', 'Physics.CheckBox', 'Physics.CheckCapsule',
            'Physics.Linecast', 'Physics.IgnoreCollision',
            # Mathf
            'Mathf.Sin', 'Mathf.Cos', 'Mathf.Tan', 'Mathf.Asin', 'Mathf.Acos', 'Mathf.Atan', 'Mathf.Atan2',
            'Mathf.Sqrt', 'Mathf.Abs', 'Mathf.Pow', 'Mathf.Exp', 'Mathf.Log', 'Mathf.Log10',
            'Mathf.Min', 'Mathf.Max', 'Mathf.Clamp', 'Mathf.Clamp01',
            'Mathf.Lerp', 'Mathf.LerpUnclamped', 'Mathf.LerpAngle', 'Mathf.InverseLerp',
            'Mathf.MoveTowards', 'Mathf.MoveTowardsAngle', 'Mathf.SmoothStep', 'Mathf.SmoothDamp', 'Mathf.SmoothDampAngle',
            'Mathf.Round', 'Mathf.Floor', 'Mathf.Ceil', 'Mathf.RoundToInt', 'Mathf.FloorToInt', 'Mathf.CeilToInt',
            'Mathf.Sign', 'Mathf.PingPong', 'Mathf.Repeat', 'Mathf.DeltaAngle',
            'Mathf.PerlinNoise', 'Mathf.PerlinNoise1D',
            # Vector3
            'Vector3.Lerp', 'Vector3.LerpUnclamped', 'Vector3.Slerp', 'Vector3.SlerpUnclamped',
            'Vector3.MoveTowards', 'Vector3.RotateTowards', 'Vector3.SmoothDamp',
            'Vector3.Distance', 'Vector3.Magnitude', 'Vector3.SqrMagnitude',
            'Vector3.Dot', 'Vector3.Cross', 'Vector3.Angle', 'Vector3.SignedAngle',
            'Vector3.Normalize', 'Vector3.Project', 'Vector3.ProjectOnPlane', 'Vector3.Reflect',
            'Vector3.Scale', 'Vector3.ClampMagnitude', 'Vector3.Min', 'Vector3.Max',
            # Quaternion
            'Quaternion.Euler', 'Quaternion.AngleAxis', 'Quaternion.LookRotation', 'Quaternion.FromToRotation',
            'Quaternion.Lerp', 'Quaternion.LerpUnclamped', 'Quaternion.Slerp', 'Quaternion.SlerpUnclamped',
            'Quaternion.RotateTowards', 'Quaternion.Inverse', 'Quaternion.Angle', 'Quaternion.Dot',
            # Object (Instantiate/Destroy)
            'Object.Instantiate', 'Object.Destroy', 'Object.DestroyImmediate', 'Object.DontDestroyOnLoad',
            'Object.FindObjectOfType', 'Object.FindObjectsOfType',
            'Instantiate', 'Destroy',  # Often called without Object. prefix
            # Color
            'Color.Lerp', 'Color.LerpUnclamped', 'Color.HSVToRGB', 'Color.RGBToHSV',
            # Random
            'Random.Range', 'Random.value', 'Random.insideUnitSphere', 'Random.insideUnitCircle',
            'Random.onUnitSphere', 'Random.rotation', 'Random.rotationUniform', 'Random.ColorHSV',
            # Time (properties, not methods)
            'Time.deltaTime', 'Time.fixedDeltaTime', 'Time.time', 'Time.fixedTime',
            'Time.unscaledDeltaTime', 'Time.unscaledTime', 'Time.timeScale', 'Time.frameCount',
            # Debug
            'Debug.Log', 'Debug.LogWarning', 'Debug.LogError', 'Debug.DrawLine', 'Debug.DrawRay',
            # Input
            'Input.GetKey', 'Input.GetKeyDown', 'Input.GetKeyUp',
            'Input.GetButton', 'Input.GetButtonDown', 'Input.GetButtonUp',
            'Input.GetAxis', 'Input.GetAxisRaw', 'Input.GetMouseButton', 'Input.GetMouseButtonDown',
            'Input.mousePosition', 'Input.GetTouch',
            # Application
            'Application.Quit', 'Application.LoadLevel',
            # Resources
            'Resources.Load', 'Resources.LoadAll', 'Resources.UnloadAsset',
            # GameObject
            'GameObject.Find', 'GameObject.FindWithTag', 'GameObject.FindGameObjectWithTag',
            'GameObject.FindGameObjectsWithTag', 'GameObject.CreatePrimitive',
            # Gizmos
            'Gizmos.DrawLine', 'Gizmos.DrawRay', 'Gizmos.DrawSphere', 'Gizmos.DrawWireSphere',
            'Gizmos.DrawCube', 'Gizmos.DrawWireCube',
        }
        
        # ==================================================================
        # TIER 5: LOWERCASE METHOD CORRECTIONS
        # Unity uses PascalCase for methods
        # ==================================================================
        
        self.lowercase_methods = {
            'play': 'Play', 'stop': 'Stop', 'pause': 'Pause',
            'emit': 'Emit', 'clear': 'Clear', 'simulate': 'Simulate',
            'setActive': 'SetActive', 'getComponent': 'GetComponent',
            'addComponent': 'AddComponent', 'sendMessage': 'SendMessage',
            'invoke': 'Invoke', 'invokeRepeating': 'InvokeRepeating',
            'cancelInvoke': 'CancelInvoke', 'destroy': 'Destroy',
            'instantiate': 'Instantiate', 'dontDestroyOnLoad': 'DontDestroyOnLoad',
            'lookAt': 'LookAt', 'rotate': 'Rotate', 'translate': 'Translate',
            'addForce': 'AddForce', 'addTorque': 'AddTorque',
            'movePosition': 'MovePosition', 'moveRotation': 'MoveRotation',
        }
    
    def validate_code(self, code: str) -> List[ValidationIssue]:
        """
        Validate C# code for Unity API issues.
        
        Returns list of ValidationIssue objects sorted by line number.
        """
        issues = []
        lines = code.split('\n')
        
        for line_num, line in enumerate(lines, 1):
            # Skip comments
            stripped = line.strip()
            if stripped.startswith('//') or stripped.startswith('/*') or stripped.startswith('*'):
                continue
            
            # Tier 1: Check hallucination patterns
            issues.extend(self._check_hallucinations(line_num, line))
            
            # Tier 2: Check invalid constructors
            issues.extend(self._check_constructors(line_num, line))
            
            # Tier 3: Check ParticleSystem accessor patterns
            issues.extend(self._check_particle_accessors(line_num, line))
            
            # Tier 4: Check lowercase methods
            issues.extend(self._check_lowercase_methods(line_num, line))
            
            # Tier 5: Check other common issues
            issues.extend(self._check_common_issues(line_num, line))
        
        return sorted(issues, key=lambda x: x.line_num)
    
    def _check_hallucinations(self, line_num: int, line: str) -> List[ValidationIssue]:
        """Check for known hallucination patterns"""
        issues = []
        
        for pattern, invalid_api, fix in self.hallucination_patterns:
            if re.search(pattern, line):
                issues.append(ValidationIssue(
                    line_num=line_num,
                    code_snippet=line.strip(),
                    issue_type="hallucinated_api",
                    invalid_api=invalid_api,
                    suggested_fix=fix,
                    confidence=1.0
                ))
        
        return issues
    
    def _check_constructors(self, line_num: int, line: str) -> List[ValidationIssue]:
        """Check for invalid component constructors"""
        issues = []
        
        for comp_type in self.no_constructor_types:
            pattern = rf'new\s+{comp_type}\s*\('
            if re.search(pattern, line):
                issues.append(ValidationIssue(
                    line_num=line_num,
                    code_snippet=line.strip(),
                    issue_type="invalid_constructor",
                    invalid_api=f"new {comp_type}()",
                    suggested_fix=f"Use gameObject.AddComponent<{comp_type}>() or assign via Inspector",
                    confidence=1.0
                ))
        
        return issues
    
    def _check_particle_accessors(self, line_num: int, line: str) -> List[ValidationIssue]:
        """Check for incorrect ParticleSystem module access patterns"""
        issues = []
        
        for module_prop, module_class in self.particle_modules.items():
            # Check for method call pattern (wrong): ps.GetEmission(), ps.emission()
            method_pattern = rf'\.{module_prop}\s*\('
            if re.search(method_pattern, line):
                # Make sure it's not a valid method like main.startLifetime.Evaluate()
                if f'.{module_prop}(' in line and module_prop != 'main':
                    issues.append(ValidationIssue(
                        line_num=line_num,
                        code_snippet=line.strip(),
                        issue_type="wrong_accessor",
                        invalid_api=f".{module_prop}()",
                        suggested_fix=f".{module_prop} is a property, not a method - use without parentheses",
                        confidence=0.9
                    ))
        
        return issues
    
    def _check_lowercase_methods(self, line_num: int, line: str) -> List[ValidationIssue]:
        """Check for lowercase Unity methods (should be PascalCase)"""
        issues = []
        
        for wrong, correct in self.lowercase_methods.items():
            pattern = rf'\.{wrong}\s*\('
            if re.search(pattern, line):
                issues.append(ValidationIssue(
                    line_num=line_num,
                    code_snippet=line.strip(),
                    issue_type="wrong_case",
                    invalid_api=f".{wrong}()",
                    suggested_fix=f"Use .{correct}() - Unity methods use PascalCase",
                    confidence=1.0
                ))
        
        return issues
    
    def _check_common_issues(self, line_num: int, line: str) -> List[ValidationIssue]:
        """Check for other common issues"""
        issues = []
        
        # Double dots
        if '..' in line and '...' not in line:  # Allow ... for params
            issues.append(ValidationIssue(
                line_num=line_num,
                code_snippet=line.strip(),
                issue_type="syntax_error",
                invalid_api="..",
                suggested_fix="Double dot syntax error - use single dot for member access",
                confidence=1.0
            ))
        
        # Space in identifiers (common LLM error)
        # e.g., "_pitch TransitionCoroutine" instead of "_pitchTransitionCoroutine"
        space_pattern = r'_\w+\s+\w+(?=\s*[=;,\)])'
        match = re.search(space_pattern, line)
        if match:
            issues.append(ValidationIssue(
                line_num=line_num,
                code_snippet=line.strip(),
                issue_type="syntax_error",
                invalid_api=match.group(),
                suggested_fix="Remove space in identifier - C# identifiers cannot contain spaces",
                confidence=1.0
            ))
        
        # Missing IEnumerator return type for coroutines
        if 'yield return' in line:
            # Check if we're inside a void method (likely error)
            pass  # Would need more context for this check
        
        # Nullable reference types (?) - often not supported in Unity
        nullable_pattern = r'\b(AudioSource|GameObject|Transform|Rigidbody|Collider|Light|Camera)\?\s+\w+'
        if re.search(nullable_pattern, line) and '#nullable' not in '\n'.join([line]):
            issues.append(ValidationIssue(
                line_num=line_num,
                code_snippet=line.strip(),
                issue_type="compatibility",
                invalid_api="Type? nullable syntax",
                suggested_fix="Unity often doesn't support nullable reference types - remove the ?",
                confidence=0.7
            ))
        
        return issues
    
    def get_fix_context(self, issues: List[ValidationIssue]) -> str:
        """
        Generate a context string for LLM to fix the issues.
        Grouped by issue type with clear instructions.
        """
        if not issues:
            return ""
        
        lines = ["## UNITY API ERRORS TO FIX\n"]
        
        # Group by issue type
        by_type: Dict[str, List[ValidationIssue]] = {}
        for issue in issues:
            if issue.issue_type not in by_type:
                by_type[issue.issue_type] = []
            by_type[issue.issue_type].append(issue)
        
        for issue_type, type_issues in by_type.items():
            lines.append(f"### {issue_type.upper().replace('_', ' ')}")
            for issue in type_issues:
                lines.append(f"- Line {issue.line_num}: `{issue.invalid_api}` → {issue.suggested_fix}")
            lines.append("")
        
        lines.append("Fix ALL issues listed above. Use ONLY the suggested corrections.\n")
        
        return "\n".join(lines)
    
    def quick_validate(self, code: str) -> Tuple[bool, int, List[str]]:
        """
        Quick validation returning (is_valid, error_count, summary_list)
        Useful for checking if code needs fixing.
        """
        issues = self.validate_code(code)
        summaries = [str(i) for i in issues[:10]]  # Limit to first 10
        return len(issues) == 0, len(issues), summaries


def test_validator():
    """Test the validator with common hallucination examples"""
    test_code = '''
using UnityEngine;

public class TestScript : MonoBehaviour
{
    private ParticleSystem ps;
    private Light light;
    private AudioSource audio;
    
    void Start()
    {
        // Hallucinations to detect:
        ParticleSystem.EmissionConfig emission = ps.GetEmission();  // Wrong
        emission.maxParticles = 100;  // Wrong location
        
        ParticleSystemVelocity velocity = ps.GetVelocity();  // Wrong class name
        velocity.startSpeed[0] = 5f;  // Wrong accessor
        
        light.beamHeight = 10f;  // Doesn't exist
        light.position = Vector3.zero;  // Should be transform.position
        
        float dist = transform.distance(other.position);  // Wrong
        transform.enabled = false;  // Wrong
        
        AudioSource clip = new AudioSource();  // Can't construct
        
        ps.play();  // Wrong case
        
        float angle = MathF.Sin(0.5f);  // Wrong class
        float value = Mathf Sin(0.5f);  // Missing dot
    }
}
'''
    
    print("=" * 60)
    print("Unity API Validator Test")
    print("=" * 60)
    
    validator = UnityAPIValidator(verbose=True)
    issues = validator.validate_code(test_code)
    
    print(f"\nFound {len(issues)} issues:\n")
    for issue in issues:
        print(f"  {issue}")
    
    print("\n" + "=" * 60)
    print("Fix Context for LLM:")
    print("=" * 60)
    print(validator.get_fix_context(issues))


if __name__ == "__main__":
    test_validator()

