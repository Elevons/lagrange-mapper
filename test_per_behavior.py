"""Quick test to compare monolithic vs per-behavior pipelines"""

from unity_pipeline_per_behavior import compare_approaches

test_prompt = "Create a gravity well that pulls nearby rigidbodies toward it and plays a humming sound"

results = compare_approaches(test_prompt, verbose=False)

print("="*70)
print("MONOLITHIC CODE:")
print("="*70)
if results['monolithic']['code']:
    print(results['monolithic']['code'][:2500])
else:
    print("None")

print()
print("="*70)
print("PER-BEHAVIOR CODE:")
print("="*70)
if results['per_behavior']['code']:
    print(results['per_behavior']['code'][:2500])
else:
    print("None")

print()
print("="*70)
print("SUMMARY")
print("="*70)
print(f"Monolithic docs: {results['monolithic']['docs_used']}")
print(f"Per-behavior docs: {results['per_behavior']['docs_used']}")
print(f"Coverage increase: {results['per_behavior']['docs_used'] / max(1, results['monolithic']['docs_used']):.1f}x")


