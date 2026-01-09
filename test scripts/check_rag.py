#!/usr/bin/env python3
"""Quick check of RAG database coverage"""
import sys
import os
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..'))
from code_generation_pipeline.unity_rag_query import UnityRAG

# Force output
sys.stdout.reconfigure(line_buffering=True)

rag = UnityRAG(verbose=False)

output = []
output.append(f"Total docs: {len(rag.documents)}")

# Check ParticleSystem coverage
ps_docs = [d for d in rag.documents if 'Particle' in d.get('api_name', '')]
output.append(f"ParticleSystem docs: {len(ps_docs)}")

if ps_docs:
    output.append("Sample ParticleSystem APIs:")
    for d in ps_docs[:10]:
        output.append(f"  - {d['api_name']}")
else:
    output.append("No ParticleSystem docs found!")

# Check for emission, main module
emission_docs = [d for d in rag.documents if 'emission' in d.get('api_name', '').lower()]
output.append(f"\nEmission-related docs: {len(emission_docs)}")
for d in emission_docs[:5]:
    output.append(f"  - {d['api_name']}")

# Write to file
with open("rag_check_output.txt", "w") as f:
    f.write("\n".join(output))

print("\n".join(output))

