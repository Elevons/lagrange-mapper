#!/usr/bin/env python3
"""Quick test to verify all imports work"""

try:
    from attractor_steering import (
        load_steering,
        AttractorSteering,
        DetectionResult,
        build_natural_language_prompt,
    )
    print("✓ attractor_steering imports OK")
except ImportError as e:
    print(f"✗ attractor_steering import failed: {e}")
    exit(1)

try:
    from attractor_mapper import (
        detect_code_markers,
        CODE_LEAK_PATTERNS,
    )
    print("✓ attractor_mapper imports OK")
except ImportError as e:
    print(f"✗ attractor_mapper import failed: {e}")
    exit(1)

try:
    from unity_ir_inference import UnityIRGenerator
    print("✓ unity_ir_inference import OK")
except ImportError as e:
    print(f"✗ unity_ir_inference import failed: {e}")
    exit(1)

print("\n✓ All imports successful!")

