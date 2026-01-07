"""
IR Calibration System - Contrastive Embedding Offsets

Learns the systematic bias between ideal IR and model-generated IR,
then uses that offset to detect/correct future generations.

Workflow:
1. Create ground truth: 100 good C# scripts → reverse to ideal IR JSON
2. Generate: Run same prompts through model to get actual IR
3. Compute offsets: embed(ideal) - embed(actual) for each pair
4. Cluster offsets by behavior type (optional)
5. At inference: use offset to detect if output is drifting toward bad patterns
"""

import json
import numpy as np
import requests
from pathlib import Path
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple
import hashlib

# ============================================================================
# EMBEDDING CONFIGURATION
# ============================================================================

# Local embedding API (same as attractor_mapper.py)
LOCAL_EMBEDDING_URL = "http://localhost:1234/v1/embeddings"
LOCAL_EMBEDDING_MODEL = "text-embedding-nomic-embed-text-v1.5"

# Batch size for parallel embedding requests
EMBEDDING_BATCH_SIZE = 300


@dataclass
class CalibrationPair:
    """A single calibration example with both IR and Code stages"""
    prompt: str
    
    # IR Stage: NL → JSON
    ideal_ir: Dict
    actual_ir: Optional[Dict] = None
    ideal_ir_embedding: Optional[np.ndarray] = None
    actual_ir_embedding: Optional[np.ndarray] = None
    
    # Code Stage: JSON → C#
    ideal_code: Optional[str] = None  # Claude's C# output
    actual_code: Optional[str] = None  # Local model's C# output
    ideal_code_embedding: Optional[np.ndarray] = None
    actual_code_embedding: Optional[np.ndarray] = None
    
    # Metadata
    behavior_type: str = "general"  # e.g., "movement", "combat", "audio", "pickup"
    
    # Legacy compatibility
    @property
    def ideal_embedding(self) -> Optional[np.ndarray]:
        return self.ideal_ir_embedding
    
    @ideal_embedding.setter
    def ideal_embedding(self, value):
        self.ideal_ir_embedding = value
    
    @property
    def actual_embedding(self) -> Optional[np.ndarray]:
        return self.actual_ir_embedding
    
    @actual_embedding.setter
    def actual_embedding(self, value):
        self.actual_ir_embedding = value
    
    @property
    def source_script(self) -> Optional[str]:
        return self.ideal_code
    
    @source_script.setter
    def source_script(self, value):
        self.ideal_code = value
    
    # IR Stage offset
    @property
    def ir_offset(self) -> Optional[np.ndarray]:
        """Compute ideal - actual offset for IR stage"""
        if self.ideal_ir_embedding is not None and self.actual_ir_embedding is not None:
            return self.ideal_ir_embedding - self.actual_ir_embedding
        return None
    
    @property
    def ir_offset_magnitude(self) -> float:
        """How far off was the model in IR stage?"""
        if self.ir_offset is not None:
            return float(np.linalg.norm(self.ir_offset))
        return 0.0
    
    # Code Stage offset
    @property
    def code_offset(self) -> Optional[np.ndarray]:
        """Compute ideal - actual offset for Code stage"""
        if self.ideal_code_embedding is not None and self.actual_code_embedding is not None:
            return self.ideal_code_embedding - self.actual_code_embedding
        return None
    
    @property
    def code_offset_magnitude(self) -> float:
        """How far off was the model in Code stage?"""
        if self.code_offset is not None:
            return float(np.linalg.norm(self.code_offset))
        return 0.0
    
    # Legacy compatibility
    @property
    def offset(self) -> Optional[np.ndarray]:
        return self.ir_offset
    
    @property
    def offset_magnitude(self) -> float:
        return self.ir_offset_magnitude


@dataclass
class CalibrationResult:
    """Result of applying calibration to a new generation"""
    # What was evaluated
    stage: str = "ir"  # "ir" or "code"
    content: Optional[any] = None  # IR dict or code string
    embedding: Optional[np.ndarray] = None
    
    # Distance metrics
    distance_to_ideal_cluster: float = 0.0
    nearest_calibration_pair: Optional[CalibrationPair] = None
    
    # Correction info
    suggested_offset: Optional[np.ndarray] = None
    confidence: float = 0.0  # How confident are we in the correction?
    
    # Thresholds (tunable) - lower = stricter quality requirements
    ir_threshold: float = 0.4
    code_threshold: float = 0.4  # Lowered to trigger more steering
    
    @property
    def needs_correction(self) -> bool:
        """Should we try to correct this output?"""
        threshold = self.code_threshold if self.stage == "code" else self.ir_threshold
        return self.distance_to_ideal_cluster > threshold
    
    # Legacy compatibility
    @property
    def ir_json(self) -> Optional[Dict]:
        return self.content if self.stage == "ir" else None


class IRCalibrator:
    """
    Learns systematic biases in IR generation and provides correction vectors.
    
    Uses local embedding API (LM Studio) for fast batch embedding.
    
    Usage:
        calibrator = IRCalibrator()
        
        # Add calibration pairs (ideal IR from good scripts)
        calibrator.add_pair(prompt, ideal_ir, behavior_type="movement")
        
        # Generate actual IR and let calibrator learn the offset
        calibrator.record_actual(prompt, actual_ir)
        
        # At inference time, check if new output needs correction
        result = calibrator.evaluate(new_ir_json)
        if result.needs_correction:
            # Either regenerate or apply steering
            pass
    """
    
    def __init__(
        self, 
        model_name: str = "local-model",
        embedding_url: str = LOCAL_EMBEDDING_URL,
        embedding_model: str = LOCAL_EMBEDDING_MODEL,
        batch_size: int = EMBEDDING_BATCH_SIZE
    ):
        self.model_name = model_name
        self.pairs: List[CalibrationPair] = []
        self.embedding_url = embedding_url
        self.embedding_model = embedding_model
        self.batch_size = batch_size
        
        # Computed calibration data (IR stage)
        self._ideal_centroid: Optional[np.ndarray] = None
        self._ir_ideal_centroid: Optional[np.ndarray] = None
        self._offset_by_type: Dict[str, np.ndarray] = {}
        self._ir_offset_by_type: Dict[str, np.ndarray] = {}
        self._mean_offset: Optional[np.ndarray] = None
        self._ir_mean_offset: Optional[np.ndarray] = None
        
        # Computed calibration data (Code stage)
        self._code_ideal_centroid: Optional[np.ndarray] = None
        self._code_offset_by_type: Dict[str, np.ndarray] = {}
        self._code_mean_offset: Optional[np.ndarray] = None
        
        print(f"IRCalibrator initialized:")
        print(f"  Embedding URL: {embedding_url}")
        print(f"  Embedding Model: {embedding_model}")
        print(f"  Batch Size: {batch_size}")
    
    def _embed_single(self, text: str) -> Optional[np.ndarray]:
        """Embed a single text string"""
        try:
            response = requests.post(
                self.embedding_url,
                json={
                    "model": self.embedding_model,
                    "input": text
                },
                timeout=30
            )
            
            if response.status_code == 200:
                embedding = response.json()['data'][0]['embedding']
                vec = np.array(embedding, dtype=float)
                # Normalize
                norm = np.linalg.norm(vec)
                if norm > 0:
                    vec = vec / norm
                return vec
            else:
                print(f"  Warning: Embedding failed with status {response.status_code}")
                return None
                
        except Exception as e:
            print(f"  Error getting embedding: {e}")
            return None
    
    def _embed(self, ir_json: Dict) -> np.ndarray:
        """Embed an IR JSON as a vector"""
        ir_str = json.dumps(ir_json, indent=2, sort_keys=True)
        return self._embed_single(ir_str)
    
    def _batch_embed(self, ir_jsons: List[Dict], show_progress: bool = True) -> List[np.ndarray]:
        """
        Batch embed multiple IR JSONs at once (up to 300 at a time).
        
        Uses local embedding API with true batching for maximum speed.
        
        Args:
            ir_jsons: List of IR JSON dicts to embed
            show_progress: Print progress updates
            
        Returns:
            List of embedding vectors
        """
        if not ir_jsons:
            return []
        
        # Serialize all JSONs to strings
        ir_strings = [
            json.dumps(ir, indent=2, sort_keys=True) 
            for ir in ir_jsons
        ]
        
        all_embeddings = [None] * len(ir_strings)
        
        # Process in batches of up to EMBEDDING_BATCH_SIZE (300)
        n_batches = (len(ir_strings) + self.batch_size - 1) // self.batch_size
        
        for batch_idx in range(n_batches):
            start_idx = batch_idx * self.batch_size
            end_idx = min(start_idx + self.batch_size, len(ir_strings))
            batch_texts = ir_strings[start_idx:end_idx]
            
            if show_progress:
                print(f"    Embedding batch {batch_idx + 1}/{n_batches} ({len(batch_texts)} items)...")
            
            try:
                response = requests.post(
                    self.embedding_url,
                    json={
                        "model": self.embedding_model,
                        "input": batch_texts  # Send all texts at once
                    },
                    timeout=120  # Longer timeout for large batches
                )
                
                if response.status_code == 200:
                    data = response.json()['data']
                    # Sort by index to maintain order
                    sorted_data = sorted(data, key=lambda x: x.get('index', 0))
                    
                    for i, item in enumerate(sorted_data):
                        embedding = item['embedding']
                        vec = np.array(embedding, dtype=float)
                        # Normalize
                        norm = np.linalg.norm(vec)
                        if norm > 0:
                            vec = vec / norm
                        all_embeddings[start_idx + i] = vec
                else:
                    print(f"    Warning: Batch embedding failed with status {response.status_code}")
                    # Fall back to sequential for this batch
                    for i, text in enumerate(batch_texts):
                        emb = self._embed_single(text)
                        all_embeddings[start_idx + i] = emb
                        
            except Exception as e:
                print(f"    Error in batch embedding: {e}")
                # Fall back to sequential for this batch
                for i, text in enumerate(batch_texts):
                    emb = self._embed_single(text)
                    all_embeddings[start_idx + i] = emb
        
        if show_progress:
            valid_count = sum(1 for e in all_embeddings if e is not None)
            print(f"  ✓ Embedded {valid_count}/{len(ir_strings)} items")
        
        return all_embeddings
    
    def _batch_embed_texts(self, texts: List[str], show_progress: bool = True) -> List[np.ndarray]:
        """
        Batch embed multiple text strings (for C# code).
        
        Same as _batch_embed but takes raw strings instead of dicts.
        """
        if not texts:
            return []
        
        all_embeddings = [None] * len(texts)
        
        # Process in batches of up to EMBEDDING_BATCH_SIZE (300)
        n_batches = (len(texts) + self.batch_size - 1) // self.batch_size
        
        for batch_idx in range(n_batches):
            start_idx = batch_idx * self.batch_size
            end_idx = min(start_idx + self.batch_size, len(texts))
            batch_texts = texts[start_idx:end_idx]
            
            if show_progress and n_batches > 1:
                print(f"    Embedding batch {batch_idx + 1}/{n_batches} ({len(batch_texts)} items)...")
            
            try:
                response = requests.post(
                    self.embedding_url,
                    json={
                        "model": self.embedding_model,
                        "input": batch_texts
                    },
                    timeout=120
                )
                
                if response.status_code == 200:
                    data = response.json()['data']
                    sorted_data = sorted(data, key=lambda x: x.get('index', 0))
                    
                    for i, item in enumerate(sorted_data):
                        embedding = item['embedding']
                        vec = np.array(embedding, dtype=float)
                        norm = np.linalg.norm(vec)
                        if norm > 0:
                            vec = vec / norm
                        all_embeddings[start_idx + i] = vec
                else:
                    print(f"    Warning: Batch embedding failed with status {response.status_code}")
                    for i, text in enumerate(batch_texts):
                        emb = self._embed_single(text)
                        all_embeddings[start_idx + i] = emb
                        
            except Exception as e:
                print(f"    Error in batch embedding: {e}")
                for i, text in enumerate(batch_texts):
                    emb = self._embed_single(text)
                    all_embeddings[start_idx + i] = emb
        
        if show_progress:
            valid_count = sum(1 for e in all_embeddings if e is not None)
            print(f"  ✓ Embedded {valid_count}/{len(texts)} texts")
        
        return all_embeddings
    
    def _prompt_hash(self, prompt: str) -> str:
        """Hash prompt for lookup"""
        return hashlib.md5(prompt.lower().strip().encode()).hexdigest()[:12]
    
    # =========================================================================
    # ADDING CALIBRATION DATA
    # =========================================================================
    
    def add_pair(
        self,
        prompt: str,
        ideal_ir: Dict,
        behavior_type: str = "general",
        source_script: Optional[str] = None
    ) -> CalibrationPair:
        """
        Add an ideal IR (from a known-good script) for calibration.
        
        Args:
            prompt: The natural language prompt
            ideal_ir: The correct IR JSON (reverse-engineered from good code)
            behavior_type: Category for clustering offsets
            source_script: Original C# code if available
        """
        pair = CalibrationPair(
            prompt=prompt,
            ideal_ir=ideal_ir,
            behavior_type=behavior_type,
            source_script=source_script
        )
        
        # Compute ideal embedding
        if self.embedder:
            pair.ideal_embedding = self._embed(ideal_ir)
        
        self.pairs.append(pair)
        self._invalidate_cache()
        
        return pair
    
    def record_actual(self, prompt: str, actual_ir: Dict) -> Optional[CalibrationPair]:
        """
        Record what the model actually generated for a prompt.
        Call this after add_pair() to compute the offset.
        """
        # Find matching pair by prompt
        prompt_hash = self._prompt_hash(prompt)
        
        for pair in self.pairs:
            if self._prompt_hash(pair.prompt) == prompt_hash:
                pair.actual_ir = actual_ir
                if self.embedder:
                    pair.actual_embedding = self._embed(actual_ir)
                self._invalidate_cache()
                return pair
    
    def add_pairs_batch(
        self,
        pairs_data: List[Dict],
        embed_in_parallel: bool = True
    ) -> int:
        """
        Add multiple calibration pairs with batch embedding for BOTH IR and Code stages.
        
        Args:
            pairs_data: List of dicts with keys:
                - prompt: str
                - ideal_ir: Dict
                - actual_ir: Dict (optional)
                - ideal_code / good_code: str (optional) - Claude's C# code
                - actual_code: str (optional) - Local model's C# code
                - behavior_type: str (optional)
            embed_in_parallel: Whether to batch embed (default True)
            
        Returns:
            Number of pairs added
        """
        if not pairs_data:
            return 0
        
        print(f"\n  Adding {len(pairs_data)} calibration pairs (IR + Code)...")
        
        # Create pair objects without embeddings first
        new_pairs = []
        for p in pairs_data:
            pair = CalibrationPair(
                prompt=p["prompt"],
                ideal_ir=p["ideal_ir"],
                actual_ir=p.get("actual_ir"),
                ideal_code=p.get("ideal_code") or p.get("good_code"),
                actual_code=p.get("actual_code"),
                behavior_type=p.get("behavior_type", "general")
            )
            new_pairs.append(pair)
        
        # Batch embed if enabled
        if embed_in_parallel:
            # ─────────────────────────────────────────────────────────────────
            # IR STAGE EMBEDDINGS
            # ─────────────────────────────────────────────────────────────────
            ideal_irs = [p.ideal_ir for p in new_pairs if p.ideal_ir]
            actual_irs = [p.actual_ir for p in new_pairs if p.actual_ir]
            
            if ideal_irs:
                print(f"  Embedding {len(ideal_irs)} ideal IRs...")
                ideal_ir_embeddings = self._batch_embed(ideal_irs)
                
                idx = 0
                for pair in new_pairs:
                    if pair.ideal_ir:
                        pair.ideal_ir_embedding = ideal_ir_embeddings[idx]
                        idx += 1
            
            if actual_irs:
                print(f"  Embedding {len(actual_irs)} actual IRs...")
                actual_ir_embeddings = self._batch_embed(actual_irs)
                
                idx = 0
                for pair in new_pairs:
                    if pair.actual_ir:
                        pair.actual_ir_embedding = actual_ir_embeddings[idx]
                        idx += 1
            
            # ─────────────────────────────────────────────────────────────────
            # CODE STAGE EMBEDDINGS
            # ─────────────────────────────────────────────────────────────────
            ideal_codes = [p.ideal_code for p in new_pairs if p.ideal_code]
            actual_codes = [p.actual_code for p in new_pairs if p.actual_code]
            
            if ideal_codes:
                print(f"  Embedding {len(ideal_codes)} ideal C# scripts...")
                ideal_code_embeddings = self._batch_embed_texts(ideal_codes)
                
                idx = 0
                for pair in new_pairs:
                    if pair.ideal_code:
                        pair.ideal_code_embedding = ideal_code_embeddings[idx]
                        idx += 1
            
            if actual_codes:
                print(f"  Embedding {len(actual_codes)} actual C# scripts...")
                actual_code_embeddings = self._batch_embed_texts(actual_codes)
                
                idx = 0
                for pair in new_pairs:
                    if pair.actual_code:
                        pair.actual_code_embedding = actual_code_embeddings[idx]
                        idx += 1
        
        # Add to pairs list
        self.pairs.extend(new_pairs)
        self._invalidate_cache()
        
        ir_count = sum(1 for p in new_pairs if p.ideal_ir)
        code_count = sum(1 for p in new_pairs if p.ideal_code)
        print(f"  ✓ Added {len(new_pairs)} pairs ({ir_count} with IR, {code_count} with Code)")
        return len(new_pairs)
    
    def embed_all_actuals_batch(self):
        """
        Batch embed all actual IRs that don't have embeddings yet.
        Call this after loading pairs that have actual_ir but no actual_embedding.
        """
        if not self.embedder:
            raise RuntimeError("Embeddings not available")
        
        # Find pairs that need embedding
        pairs_to_embed = [
            p for p in self.pairs 
            if p.actual_ir and p.actual_embedding is None
        ]
        
        if not pairs_to_embed:
            print("  No actuals need embedding")
            return
        
        print(f"  Batch embedding {len(pairs_to_embed)} actual IRs...")
        
        actual_irs = [p.actual_ir for p in pairs_to_embed]
        embeddings = self._batch_embed(actual_irs)
        
        for pair, emb in zip(pairs_to_embed, embeddings):
            pair.actual_embedding = emb
        
        self._invalidate_cache()
        print(f"  ✓ Embedded {len(pairs_to_embed)} actuals")
        
        return None
    
    def _invalidate_cache(self):
        """Clear computed calibration data when pairs change"""
        self._ideal_centroid = None
        self._offset_by_type = {}
        self._mean_offset = None
    
    # =========================================================================
    # COMPUTING CALIBRATION VECTORS
    # =========================================================================
    
    def compute_calibration(self) -> Dict[str, any]:
        """
        Compute calibration vectors from all pairs for BOTH IR and Code stages.
        
        Returns dict with:
            - ir: IR stage calibration data
            - code: Code stage calibration data
            - statistics: Summary stats
        """
        # =====================================================================
        # IR STAGE CALIBRATION
        # =====================================================================
        ir_pairs = [p for p in self.pairs if p.ir_offset is not None]
        
        ir_result = {}
        if ir_pairs:
            # Compute mean IR offset
            ir_offsets = np.array([p.ir_offset for p in ir_pairs])
            self._mean_offset = np.mean(ir_offsets, axis=0)
            self._ir_mean_offset = self._mean_offset
            
            # Compute IR offset by behavior type
            ir_by_type = {}
            for pair in ir_pairs:
                if pair.behavior_type not in ir_by_type:
                    ir_by_type[pair.behavior_type] = []
                ir_by_type[pair.behavior_type].append(pair.ir_offset)
            
            self._offset_by_type = {
                btype: np.mean(np.array(offs), axis=0)
                for btype, offs in ir_by_type.items()
            }
            self._ir_offset_by_type = self._offset_by_type
            
            # Compute ideal IR centroid
            ir_ideal_embeddings = np.array([
                p.ideal_ir_embedding for p in self.pairs 
                if p.ideal_ir_embedding is not None
            ])
            if len(ir_ideal_embeddings) > 0:
                self._ideal_centroid = np.mean(ir_ideal_embeddings, axis=0)
                self._ir_ideal_centroid = self._ideal_centroid
            
            ir_magnitudes = [p.ir_offset_magnitude for p in ir_pairs]
            ir_result = {
                "mean_offset": self._ir_mean_offset,
                "offset_by_type": {k: v.tolist() for k, v in self._ir_offset_by_type.items()},
                "ideal_centroid_shape": self._ir_ideal_centroid.shape if self._ir_ideal_centroid is not None else None,
                "num_pairs": len(ir_pairs),
                "mean_offset_magnitude": float(np.mean(ir_magnitudes)),
                "max_offset_magnitude": float(np.max(ir_magnitudes)),
                "min_offset_magnitude": float(np.min(ir_magnitudes)),
            }
            
            print(f"\n  IR Stage Calibration:")
            print(f"    Pairs: {len(ir_pairs)}")
            print(f"    Mean offset magnitude: {ir_result['mean_offset_magnitude']:.4f}")
        
        # =====================================================================
        # CODE STAGE CALIBRATION
        # =====================================================================
        code_pairs = [p for p in self.pairs if p.code_offset is not None]
        
        code_result = {}
        if code_pairs:
            # Compute mean Code offset
            code_offsets = np.array([p.code_offset for p in code_pairs])
            self._code_mean_offset = np.mean(code_offsets, axis=0)
            
            # Compute Code offset by behavior type
            code_by_type = {}
            for pair in code_pairs:
                if pair.behavior_type not in code_by_type:
                    code_by_type[pair.behavior_type] = []
                code_by_type[pair.behavior_type].append(pair.code_offset)
            
            self._code_offset_by_type = {
                btype: np.mean(np.array(offs), axis=0)
                for btype, offs in code_by_type.items()
            }
            
            # Compute ideal Code centroid
            code_ideal_embeddings = np.array([
                p.ideal_code_embedding for p in self.pairs 
                if p.ideal_code_embedding is not None
            ])
            if len(code_ideal_embeddings) > 0:
                self._code_ideal_centroid = np.mean(code_ideal_embeddings, axis=0)
            
            code_magnitudes = [p.code_offset_magnitude for p in code_pairs]
            code_result = {
                "mean_offset": self._code_mean_offset,
                "offset_by_type": {k: v.tolist() for k, v in self._code_offset_by_type.items()},
                "ideal_centroid_shape": self._code_ideal_centroid.shape if hasattr(self, '_code_ideal_centroid') and self._code_ideal_centroid is not None else None,
                "num_pairs": len(code_pairs),
                "mean_offset_magnitude": float(np.mean(code_magnitudes)),
                "max_offset_magnitude": float(np.max(code_magnitudes)),
                "min_offset_magnitude": float(np.min(code_magnitudes)),
            }
            
            print(f"\n  Code Stage Calibration:")
            print(f"    Pairs: {len(code_pairs)}")
            print(f"    Mean offset magnitude: {code_result['mean_offset_magnitude']:.4f}")
        
        # Combined statistics
        return {
            "ir": ir_result,
            "code": code_result,
            "statistics": {
                "ir_pairs": len(ir_pairs),
                "code_pairs": len(code_pairs),
                "behavior_types": list(set(
                    list(self._offset_by_type.keys() if hasattr(self, '_offset_by_type') else []) +
                    list(self._code_offset_by_type.keys() if hasattr(self, '_code_offset_by_type') else [])
                ))
            }
        }
    
    # =========================================================================
    # INFERENCE-TIME EVALUATION
    # =========================================================================
    
    def evaluate_ir(
        self,
        ir_json: Dict,
        behavior_type: Optional[str] = None
    ) -> CalibrationResult:
        """
        Evaluate a new IR generation against IR calibration data.
        
        Args:
            ir_json: The model's generated IR
            behavior_type: If known, use type-specific offset
            
        Returns:
            CalibrationResult with distance metrics and suggested correction
        """
        # Ensure calibration is computed
        if self._ideal_centroid is None:
            self.compute_calibration()
        
        # Embed the new IR
        embedding = self._embed(ir_json)
        
        # Distance to ideal IR centroid
        if hasattr(self, '_ir_ideal_centroid') and self._ir_ideal_centroid is not None:
            dist_to_ideal = float(np.linalg.norm(embedding - self._ir_ideal_centroid))
        elif self._ideal_centroid is not None:
            dist_to_ideal = float(np.linalg.norm(embedding - self._ideal_centroid))
        else:
            dist_to_ideal = float('inf')
        
        # Find nearest calibration pair (by IR)
        nearest = None
        min_dist = float('inf')
        for pair in self.pairs:
            if pair.ideal_ir_embedding is not None:
                d = float(np.linalg.norm(embedding - pair.ideal_ir_embedding))
                if d < min_dist:
                    min_dist = d
                    nearest = pair
        
        # Get appropriate offset
        if behavior_type and hasattr(self, '_ir_offset_by_type') and behavior_type in self._ir_offset_by_type:
            suggested_offset = self._ir_offset_by_type[behavior_type]
        elif hasattr(self, '_ir_mean_offset'):
            suggested_offset = self._ir_mean_offset
        else:
            suggested_offset = self._mean_offset
        
        # Confidence based on IR pairs
        ir_pairs = [p for p in self.pairs if p.ir_offset is not None]
        confidence = min(1.0, len(ir_pairs) / 50.0)
        
        return CalibrationResult(
            stage="ir",
            content=ir_json,
            embedding=embedding,
            distance_to_ideal_cluster=dist_to_ideal,
            nearest_calibration_pair=nearest,
            suggested_offset=suggested_offset,
            confidence=confidence
        )
    
    def evaluate_code(
        self,
        code: str,
        behavior_type: Optional[str] = None
    ) -> CalibrationResult:
        """
        Evaluate generated C# code against Code calibration data.
        
        Args:
            code: The model's generated C# code
            behavior_type: If known, use type-specific offset
            
        Returns:
            CalibrationResult with distance metrics and suggested correction
        """
        # Ensure calibration is computed
        if not hasattr(self, '_code_ideal_centroid'):
            self.compute_calibration()
        
        # Embed the code
        embedding = self._embed_single(code)
        
        if embedding is None:
            return CalibrationResult(
                stage="code",
                content=code,
                embedding=None,
                distance_to_ideal_cluster=float('inf'),
                confidence=0.0
            )
        
        # Distance to ideal code centroid
        if hasattr(self, '_code_ideal_centroid') and self._code_ideal_centroid is not None:
            dist_to_ideal = float(np.linalg.norm(embedding - self._code_ideal_centroid))
        else:
            dist_to_ideal = float('inf')
        
        # Find nearest calibration pair (by code)
        nearest = None
        min_dist = float('inf')
        for pair in self.pairs:
            if pair.ideal_code_embedding is not None:
                d = float(np.linalg.norm(embedding - pair.ideal_code_embedding))
                if d < min_dist:
                    min_dist = d
                    nearest = pair
        
        # Get appropriate offset
        if behavior_type and hasattr(self, '_code_offset_by_type') and behavior_type in self._code_offset_by_type:
            suggested_offset = self._code_offset_by_type[behavior_type]
        elif hasattr(self, '_code_mean_offset'):
            suggested_offset = self._code_mean_offset
        else:
            suggested_offset = None
        
        # Confidence based on code pairs
        code_pairs = [p for p in self.pairs if p.code_offset is not None]
        confidence = min(1.0, len(code_pairs) / 50.0)
        
        return CalibrationResult(
            stage="code",
            content=code,
            embedding=embedding,
            distance_to_ideal_cluster=dist_to_ideal,
            nearest_calibration_pair=nearest,
            suggested_offset=suggested_offset,
            confidence=confidence
        )
    
    def evaluate(
        self,
        ir_json: Dict = None,
        code: str = None,
        behavior_type: Optional[str] = None
    ) -> Dict[str, CalibrationResult]:
        """
        Evaluate both IR and Code stages (convenience method).
        
        Args:
            ir_json: The model's generated IR (optional)
            code: The model's generated C# code (optional)
            behavior_type: If known, use type-specific offset
            
        Returns:
            Dict with 'ir' and/or 'code' CalibrationResults
        """
        results = {}
        
        if ir_json is not None:
            results['ir'] = self.evaluate_ir(ir_json, behavior_type)
        
        if code is not None:
            results['code'] = self.evaluate_code(code, behavior_type)
        
        return results
    
    def apply_offset(self, embedding: np.ndarray, offset: np.ndarray) -> np.ndarray:
        """Apply correction offset to an embedding"""
        return embedding + offset
    
    # =========================================================================
    # STEERING BY EXAMPLE - Use calibration to guide regeneration
    # =========================================================================
    
    def get_steering_examples_for_ir(
        self, 
        problematic_ir: Dict, 
        n_examples: int = 2,
        max_distance: float = 0.7  # Only use examples within this distance
    ) -> List[Dict]:
        """
        Find calibration pairs whose actual IR is closest to the problematic IR,
        then return their ideal IRs as steering examples.
        
        This enables "steering by example" - showing the model what good output
        looks like for similar inputs.
        
        GENERALIZATION SAFEGUARD: Only returns examples within max_distance.
        If the problematic IR is too different from all calibration examples,
        returns empty list (caller should fall back to other methods).
        
        Args:
            problematic_ir: The IR that needs correction
            n_examples: Number of examples to return
            max_distance: Maximum embedding distance for an example to be considered relevant
            
        Returns:
            List of dicts with 'prompt', 'bad_ir', 'good_ir' for few-shot prompting
            Empty list if no sufficiently similar examples found
        """
        # Embed the problematic IR
        prob_embedding = self._embed(problematic_ir)
        if prob_embedding is None:
            return []
        
        # Find pairs with actual IR closest to problematic IR
        distances = []
        for pair in self.pairs:
            if pair.actual_ir_embedding is not None and pair.ideal_ir is not None:
                dist = float(np.linalg.norm(prob_embedding - pair.actual_ir_embedding))
                # Only consider if within max_distance (generalization check)
                if dist <= max_distance:
                    distances.append((dist, pair))
        
        if not distances:
            # No similar examples found - this prompt is outside our calibration coverage
            return []
        
        # Sort by distance and take top N
        distances.sort(key=lambda x: x[0])
        
        examples = []
        for dist, pair in distances[:n_examples]:
            examples.append({
                "prompt": pair.prompt,
                "bad_ir": pair.actual_ir,  # What the model generated (similar to our problem)
                "good_ir": pair.ideal_ir,  # What it should have generated
                "distance": dist,
                "similarity": 1.0 - (dist / max_distance)  # 0-1 similarity score
            })
        
        return examples
    
    def get_steering_examples_for_code(
        self, 
        problematic_code: str, 
        n_examples: int = 2,
        max_distance: float = 0.8  # Code has more variance, allow slightly higher
    ) -> List[Dict]:
        """
        Find calibration pairs whose actual code is closest to the problematic code,
        then return their ideal code as steering examples.
        
        GENERALIZATION SAFEGUARD: Only returns examples within max_distance.
        
        Args:
            problematic_code: The C# code that needs correction
            n_examples: Number of examples to return
            max_distance: Maximum embedding distance for relevance
            
        Returns:
            List of dicts with 'prompt', 'bad_code', 'good_code' for few-shot prompting
            Empty list if no sufficiently similar examples found
        """
        # Embed the problematic code
        prob_embedding = self._embed_single(problematic_code)
        if prob_embedding is None:
            return []
        
        # Find pairs with actual code closest to problematic code
        distances = []
        for pair in self.pairs:
            if pair.actual_code_embedding is not None and pair.ideal_code is not None:
                dist = float(np.linalg.norm(prob_embedding - pair.actual_code_embedding))
                if dist <= max_distance:
                    distances.append((dist, pair))
        
        if not distances:
            return []
        
        # Sort by distance and take top N
        distances.sort(key=lambda x: x[0])
        
        examples = []
        for dist, pair in distances[:n_examples]:
            examples.append({
                "prompt": pair.prompt,
                "bad_code": pair.actual_code,
                "good_code": pair.ideal_code,
                "distance": dist,
                "similarity": 1.0 - (dist / max_distance)
            })
        
        return examples
    
    def get_generalization_info(self, ir_json: Dict = None, code: str = None) -> Dict:
        """
        Check how well new content generalizes to calibration data.
        
        Returns info about whether the content is within the calibration distribution
        or is an out-of-distribution example that needs different handling.
        """
        result = {
            "ir_in_distribution": False,
            "code_in_distribution": False,
            "ir_nearest_distance": None,
            "code_nearest_distance": None,
            "ir_nearest_prompt": None,
            "code_nearest_prompt": None,
            "recommendation": "unknown"
        }
        
        if ir_json:
            ir_examples = self.get_steering_examples_for_ir(ir_json, n_examples=1)
            if ir_examples:
                result["ir_in_distribution"] = True
                result["ir_nearest_distance"] = ir_examples[0]["distance"]
                result["ir_nearest_prompt"] = ir_examples[0]["prompt"]
            else:
                # Check what the actual nearest distance is
                prob_emb = self._embed(ir_json)
                if prob_emb is not None:
                    min_dist = float('inf')
                    nearest_prompt = None
                    for pair in self.pairs:
                        if pair.actual_ir_embedding is not None:
                            d = float(np.linalg.norm(prob_emb - pair.actual_ir_embedding))
                            if d < min_dist:
                                min_dist = d
                                nearest_prompt = pair.prompt
                    result["ir_nearest_distance"] = min_dist
                    result["ir_nearest_prompt"] = nearest_prompt
        
        if code:
            code_examples = self.get_steering_examples_for_code(code, n_examples=1)
            if code_examples:
                result["code_in_distribution"] = True
                result["code_nearest_distance"] = code_examples[0]["distance"]
                result["code_nearest_prompt"] = code_examples[0]["prompt"]
            else:
                prob_emb = self._embed_single(code)
                if prob_emb is not None:
                    min_dist = float('inf')
                    nearest_prompt = None
                    for pair in self.pairs:
                        if pair.actual_code_embedding is not None:
                            d = float(np.linalg.norm(prob_emb - pair.actual_code_embedding))
                            if d < min_dist:
                                min_dist = d
                                nearest_prompt = pair.prompt
                    result["code_nearest_distance"] = min_dist
                    result["code_nearest_prompt"] = nearest_prompt
        
        # Recommendation
        if result["ir_in_distribution"] or result["code_in_distribution"]:
            result["recommendation"] = "use_contrastive_steering"
        elif result["ir_nearest_distance"] and result["ir_nearest_distance"] < 1.0:
            result["recommendation"] = "use_mean_offset_with_caution"
        else:
            result["recommendation"] = "out_of_distribution_needs_more_calibration_data"
        
        return result
    
    def build_contrastive_prompt_ir(self, problematic_ir: Dict, original_prompt: str) -> str:
        """
        Build a contrastive regeneration prompt that shows:
        1. Similar bad examples
        2. Their corrected versions
        3. The problematic IR to fix
        
        This uses the calibration offset direction implicitly through examples.
        """
        examples = self.get_steering_examples_for_ir(problematic_ir, n_examples=2)
        
        if not examples:
            return None
        
        prompt_parts = [
            "The following IR JSON has issues. Here are examples of similar problems and their corrections:\n"
        ]
        
        for i, ex in enumerate(examples, 1):
            prompt_parts.append(f"\n--- Example {i} ---")
            prompt_parts.append(f"Original prompt: {ex['prompt']}")
            prompt_parts.append(f"\nProblematic output:\n{json.dumps(ex['bad_ir'], indent=2)}")
            prompt_parts.append(f"\nCorrected output:\n{json.dumps(ex['good_ir'], indent=2)}")
        
        prompt_parts.append(f"\n--- Your task ---")
        prompt_parts.append(f"Original prompt: {original_prompt}")
        prompt_parts.append(f"\nProblematic IR to fix:\n{json.dumps(problematic_ir, indent=2)}")
        prompt_parts.append(f"\nGenerate the corrected IR JSON following the pattern shown above:")
        
        return "\n".join(prompt_parts)
    
    def build_contrastive_prompt_code(self, problematic_code: str, ir_json: Dict) -> str:
        """
        Build a contrastive regeneration prompt for code that shows:
        1. Similar bad code examples
        2. Their corrected versions
        3. The problematic code to fix
        """
        examples = self.get_steering_examples_for_code(problematic_code, n_examples=1)
        
        if not examples:
            return None
        
        prompt_parts = [
            "The following C# code has issues. Here's an example of similar problems and corrections:\n"
        ]
        
        for i, ex in enumerate(examples, 1):
            prompt_parts.append(f"\n--- Example {i}: {ex['prompt'][:50]}... ---")
            prompt_parts.append(f"\nProblematic code:\n```csharp\n{ex['bad_code'][:1500]}...\n```")
            prompt_parts.append(f"\nCorrected code:\n```csharp\n{ex['good_code'][:1500]}...\n```")
        
        prompt_parts.append(f"\n--- Your task ---")
        prompt_parts.append(f"IR specification:\n{json.dumps(ir_json, indent=2)}")
        prompt_parts.append(f"\nProblematic code to fix:\n```csharp\n{problematic_code}\n```")
        prompt_parts.append(f"\nGenerate corrected C# code following Unity best practices:")
        
        return "\n".join(prompt_parts)
    
    # =========================================================================
    # PERSISTENCE
    # =========================================================================
    
    def save(self, path: str):
        """Save calibration data to disk (both IR and Code stages)"""
        data = {
            "model_name": self.model_name,
            "embedding_model": self.embedding_model,
            "pairs": [
                {
                    "prompt": p.prompt,
                    "ideal_ir": p.ideal_ir,
                    "actual_ir": p.actual_ir,
                    "ideal_code": p.ideal_code,
                    "actual_code": p.actual_code,
                    "behavior_type": p.behavior_type,
                    # IR embeddings
                    "ideal_ir_embedding": p.ideal_ir_embedding.tolist() if p.ideal_ir_embedding is not None else None,
                    "actual_ir_embedding": p.actual_ir_embedding.tolist() if p.actual_ir_embedding is not None else None,
                    # Code embeddings
                    "ideal_code_embedding": p.ideal_code_embedding.tolist() if p.ideal_code_embedding is not None else None,
                    "actual_code_embedding": p.actual_code_embedding.tolist() if p.actual_code_embedding is not None else None,
                }
                for p in self.pairs
            ],
            # Calibration vectors
            "ir_mean_offset": self._ir_mean_offset.tolist() if self._ir_mean_offset is not None else None,
            "code_mean_offset": self._code_mean_offset.tolist() if self._code_mean_offset is not None else None,
            "ir_ideal_centroid": self._ir_ideal_centroid.tolist() if self._ir_ideal_centroid is not None else None,
            "code_ideal_centroid": self._code_ideal_centroid.tolist() if self._code_ideal_centroid is not None else None,
        }
        
        with open(path, 'w') as f:
            json.dump(data, f, indent=2)
        
        ir_count = sum(1 for p in self.pairs if p.ideal_ir_embedding is not None)
        code_count = sum(1 for p in self.pairs if p.ideal_code_embedding is not None)
        print(f"Saved {len(self.pairs)} calibration pairs to {path}")
        print(f"  IR pairs: {ir_count}, Code pairs: {code_count}")
    
    def load(self, path: str):
        """Load calibration data from disk (both IR and Code stages)"""
        with open(path) as f:
            data = json.load(f)
        
        self.model_name = data.get("model_name", "unknown")
        
        self.pairs = []
        for p in data["pairs"]:
            pair = CalibrationPair(
                prompt=p["prompt"],
                ideal_ir=p["ideal_ir"],
                actual_ir=p.get("actual_ir"),
                ideal_code=p.get("ideal_code"),
                actual_code=p.get("actual_code"),
                behavior_type=p.get("behavior_type", "general")
            )
            # IR embeddings (new format)
            if p.get("ideal_ir_embedding"):
                pair.ideal_ir_embedding = np.array(p["ideal_ir_embedding"])
            elif p.get("ideal_embedding"):  # Legacy format
                pair.ideal_ir_embedding = np.array(p["ideal_embedding"])
                
            if p.get("actual_ir_embedding"):
                pair.actual_ir_embedding = np.array(p["actual_ir_embedding"])
            elif p.get("actual_embedding"):  # Legacy format
                pair.actual_ir_embedding = np.array(p["actual_embedding"])
            
            # Code embeddings
            if p.get("ideal_code_embedding"):
                pair.ideal_code_embedding = np.array(p["ideal_code_embedding"])
            if p.get("actual_code_embedding"):
                pair.actual_code_embedding = np.array(p["actual_code_embedding"])
                
            self.pairs.append(pair)
        
        # Load calibration vectors
        if data.get("ir_mean_offset"):
            self._ir_mean_offset = np.array(data["ir_mean_offset"])
            self._mean_offset = self._ir_mean_offset
        if data.get("code_mean_offset"):
            self._code_mean_offset = np.array(data["code_mean_offset"])
        if data.get("ir_ideal_centroid"):
            self._ir_ideal_centroid = np.array(data["ir_ideal_centroid"])
            self._ideal_centroid = self._ir_ideal_centroid
        if data.get("code_ideal_centroid"):
            self._code_ideal_centroid = np.array(data["code_ideal_centroid"])
        
        self._invalidate_cache()
        
        ir_count = sum(1 for p in self.pairs if p.ideal_ir_embedding is not None)
        code_count = sum(1 for p in self.pairs if p.ideal_code_embedding is not None)
        print(f"Loaded {len(self.pairs)} calibration pairs from {path}")
        print(f"  IR pairs: {ir_count}, Code pairs: {code_count}")
    
    def load_from_examples(self, path: str, embed_ideals: bool = True):
        """
        Load calibration data from generate_calibration_examples.py output.
        
        Args:
            path: Path to calibration_data.json
            embed_ideals: Whether to compute embeddings for ideal IRs
        """
        with open(path) as f:
            data = json.load(f)
        
        pairs_data = data.get("pairs", [])
        print(f"Loading {len(pairs_data)} examples from {path}...")
        
        self.pairs = []
        
        for p in pairs_data:
            pair = CalibrationPair(
                prompt=p["prompt"],
                ideal_ir=p["ideal_ir"],
                behavior_type=p.get("behavior_type", "general"),
                source_script=p.get("good_code")
            )
            self.pairs.append(pair)
        
        # Batch embed all ideal IRs
        if embed_ideals and self.embedder and self.pairs:
            print(f"Computing embeddings for {len(self.pairs)} ideal IRs...")
            
            ir_strings = [
                json.dumps(p.ideal_ir, indent=2, sort_keys=True) 
                for p in self.pairs
            ]
            
            # Batch embed (process in chunks of 50)
            batch_size = 50
            for i in range(0, len(ir_strings), batch_size):
                batch = ir_strings[i:i+batch_size]
                embeddings = self.embedder.encode(batch, convert_to_numpy=True)
                
                for j, emb in enumerate(embeddings):
                    self.pairs[i + j].ideal_embedding = emb
                
                print(f"  Embedded {min(i + batch_size, len(ir_strings))}/{len(ir_strings)}")
        
        self._invalidate_cache()
        
        # Show summary by type
        by_type = {}
        for p in self.pairs:
            by_type[p.behavior_type] = by_type.get(p.behavior_type, 0) + 1
        
        print(f"\nLoaded {len(self.pairs)} calibration examples:")
        for btype, count in sorted(by_type.items()):
            print(f"  {btype}: {count}")
    
    def generate_actuals_from_model(
        self,
        generator_func,
        max_examples: int = None,
        verbose: bool = True
    ):
        """
        Generate actual IR outputs from your local model for all calibration pairs.
        
        Args:
            generator_func: Function that takes a prompt and returns IR JSON dict
                           e.g., lambda prompt: ir_generator.generate(prompt).parsed
            max_examples: Limit number of examples to process (None = all)
            verbose: Print progress
        
        This populates actual_ir and actual_embedding for each pair,
        allowing compute_calibration() to calculate offsets.
        
        Uses batch embedding at the end for speed.
        """
        pairs_to_process = self.pairs[:max_examples] if max_examples else self.pairs
        
        print(f"\nGenerating actual outputs for {len(pairs_to_process)} examples...")
        
        successful = 0
        failed = 0
        successful_pairs = []
        
        # Generate all actuals (no embedding yet)
        for i, pair in enumerate(pairs_to_process):
            if verbose and (i + 1) % 10 == 0:
                print(f"  Progress: {i + 1}/{len(pairs_to_process)}")
            
            try:
                actual_ir = generator_func(pair.prompt)
                
                if actual_ir and isinstance(actual_ir, dict):
                    pair.actual_ir = actual_ir
                    successful_pairs.append(pair)
                    successful += 1
                else:
                    failed += 1
                    
            except Exception as e:
                if verbose:
                    print(f"    Error on '{pair.prompt[:30]}...': {e}")
                failed += 1
        
        print(f"\nGeneration complete:")
        print(f"  Successful: {successful}")
        print(f"  Failed: {failed}")
        
        # Batch embed all actuals at once (much faster!)
        if successful_pairs and self.embedder:
            print(f"\nBatch embedding {len(successful_pairs)} actual IRs...")
            actual_irs = [p.actual_ir for p in successful_pairs]
            embeddings = self._batch_embed(actual_irs, show_progress=verbose)
            
            for pair, emb in zip(successful_pairs, embeddings):
                pair.actual_embedding = emb
            
            print(f"  ✓ Embedded {len(successful_pairs)} actuals")
        
        # Compute calibration now that we have actuals
        if successful > 0:
            self.compute_calibration()
        
        return successful, failed


# =============================================================================
# EXAMPLE USAGE
# =============================================================================

def example_calibration_workflow():
    """Example of how to use the calibration system"""
    
    print("=" * 60)
    print("IR CALIBRATION EXAMPLE")
    print("=" * 60)
    
    calibrator = IRCalibrator()
    
    # Step 1: Add ideal IR (reverse-engineered from good scripts)
    # In practice, you'd have 100 of these from your good code examples
    
    ideal_pressure_plate = {
        "class_name": "PressurePlate",
        "components": ["Collider", "AudioSource"],
        "fields": [
            {"name": "activationSound", "type": "AudioClip", "default": None},
            {"name": "door", "type": "Transform", "default": None},
            {"name": "openDuration", "type": "float", "default": 2.0}
        ],
        "behaviors": [{
            "name": "on_activate",
            "trigger": "detect object entering trigger zone",
            "actions": [
                {"action": "play activation sound"},
                {"action": "change material color to green"},
                {"action": "move door upward over openDuration seconds"}  # Natural language!
            ]
        }]
    }
    
    calibrator.add_pair(
        prompt="pressure plate that opens a door",
        ideal_ir=ideal_pressure_plate,
        behavior_type="trigger"
    )
    
    # Step 2: Record what the model actually generates
    # This would come from running the prompt through your IR generator
    
    actual_pressure_plate = {
        "class_name": "PressurePlate",
        "components": ["Collider", "AudioSource"],
        "fields": [
            {"name": "activationSound", "type": "AudioClip", "default": None},
            {"name": "door", "type": "Transform", "default": None}
        ],
        "behaviors": [{
            "name": "on_activate",
            "trigger": "OnTriggerEnter",  # Code leak!
            "actions": [
                {"action": "audioSource.PlayOneShot(activationSound)"},  # Code leak!
                {"action": "door.position += Vector3.up * 2f"}  # Code leak!
            ]
        }]
    }
    
    calibrator.record_actual(
        prompt="pressure plate that opens a door",
        actual_ir=actual_pressure_plate
    )
    
    # Step 3: Compute calibration
    result = calibrator.compute_calibration()
    
    print(f"\nCalibration computed:")
    print(f"  Pairs: {result['statistics']['num_pairs']}")
    print(f"  Mean offset magnitude: {result['statistics']['mean_offset_magnitude']:.3f}")
    print(f"  Behavior types: {result['statistics']['behavior_types']}")
    
    # Step 4: Evaluate a new generation
    new_ir = {
        "class_name": "Turret",
        "behaviors": [{
            "trigger": "Update()",  # Might be code leak
            "actions": [{"action": "transform.LookAt(target)"}]  # Code leak!
        }]
    }
    
    eval_result = calibrator.evaluate(new_ir)
    
    print(f"\nEvaluation of new IR:")
    print(f"  Distance to ideal cluster: {eval_result.distance_to_ideal_cluster:.3f}")
    print(f"  Needs correction: {eval_result.needs_correction}")
    print(f"  Confidence: {eval_result.confidence:.2f}")
    
    if eval_result.nearest_calibration_pair:
        print(f"  Nearest example: {eval_result.nearest_calibration_pair.prompt}")


if __name__ == "__main__":
    example_calibration_workflow()

