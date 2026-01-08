"""
Unity RAG Database Builder
Indexes Unity API markdown documentation, generates embeddings, and builds search indices.

Usage:
    python build_rag_database.py --input <docs_path> --output <db_path>

Prerequisites:
    - LM Studio running with nomic-embed-text-v1.5 model loaded
    - Unity documentation markdown files extracted to input path
"""

import argparse
import json
import numpy as np
import os
import requests
import time
from pathlib import Path
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass


@dataclass
class Document:
    """A single Unity API documentation entry"""
    id: int
    namespace: str
    api_name: str
    file_path: str
    title: str
    content: str = ""


class UnityRAGBuilder:
    """Builds the RAG database from Unity markdown documentation"""
    
    def __init__(self,
                 input_path: str,
                 output_path: str,
                 embedding_url: str = "http://127.0.0.1:1234/v1/embeddings",
                 model_name: str = "text-embedding-nomic-embed-text-v1.5",
                 batch_size: int = 300,
                 verbose: bool = True):
        """
        Initialize the database builder.
        
        Args:
            input_path: Path to Unity markdown docs (ScriptReference folder)
            output_path: Output path for database files
            embedding_url: URL of embedding server (LM Studio)
            model_name: Name of embedding model
            batch_size: Documents per embedding request
            verbose: Print progress information
        """
        self.input_path = Path(input_path)
        self.output_path = Path(output_path)
        self.embedding_url = embedding_url
        self.model_name = model_name
        self.batch_size = batch_size
        self.verbose = verbose
        
        # Data storage
        self.documents: List[Document] = []
        self.namespaces: Dict[str, Dict] = {}
        self.type_to_namespace: Dict[str, str] = {}
        self.embeddings: Optional[np.ndarray] = None
    
    def scan_documents(self) -> int:
        """
        Scan input directory for markdown files and build document index.
        
        Returns:
            Number of documents found
        """
        if self.verbose:
            print(f"Scanning {self.input_path} for markdown files...")
        
        doc_id = 0
        
        # Walk through all subdirectories (each is a namespace)
        for namespace_dir in sorted(self.input_path.iterdir()):
            if not namespace_dir.is_dir():
                continue
            
            namespace_name = namespace_dir.name
            namespace_docs = []
            api_names = []
            
            # Find all markdown files in this namespace
            for md_file in namespace_dir.glob("*.md"):
                # Parse API name from filename
                api_name = md_file.stem
                
                # Read title from file (first line after #)
                title = api_name
                try:
                    with open(md_file, 'r', encoding='utf-8') as f:
                        first_lines = f.read(500)
                        for line in first_lines.split('\n'):
                            if line.startswith('# '):
                                title = line[2:].strip()
                                break
                except Exception:
                    pass
                
                # Create document entry
                doc = Document(
                    id=doc_id,
                    namespace=namespace_name,
                    api_name=api_name,
                    file_path=str(md_file.relative_to(self.input_path)),
                    title=title
                )
                
                self.documents.append(doc)
                namespace_docs.append(doc_id)
                api_names.append(api_name)
                doc_id += 1
            
            if namespace_docs:
                self.namespaces[namespace_name] = {
                    "name": namespace_name,
                    "doc_ids": namespace_docs,
                    "doc_count": len(namespace_docs),
                    "api_names": api_names
                }
                
                # Build type-to-namespace mapping
                self.type_to_namespace[namespace_name] = namespace_name
        
        if self.verbose:
            print(f"  Found {len(self.documents)} documents in {len(self.namespaces)} namespaces")
        
        return len(self.documents)
    
    def load_document_content(self, doc: Document) -> str:
        """Load the full markdown content for a document"""
        full_path = self.input_path / doc.file_path
        
        try:
            with open(full_path, 'r', encoding='utf-8') as f:
                return f.read()
        except Exception as e:
            if self.verbose:
                print(f"  Warning: Could not read {doc.file_path}: {e}")
            return ""
    
    def _get_embeddings_batch(self, texts: List[str]) -> np.ndarray:
        """Get embeddings for a batch of texts"""
        response = requests.post(
            self.embedding_url,
            json={
                "model": self.model_name,
                "input": texts
            },
            timeout=300  # 5 minutes for large batches
        )
        
        if response.status_code != 200:
            raise Exception(f"Embedding API error: {response.status_code} - {response.text}")
        
        data = response.json()
        embeddings = [item["embedding"] for item in data["data"]]
        return np.array(embeddings, dtype=np.float32)
    
    def generate_embeddings(self) -> np.ndarray:
        """
        Generate embeddings for all documents.
        
        Returns:
            Numpy array of embeddings (N x dim)
        """
        if self.verbose:
            print(f"\nGenerating embeddings for {len(self.documents)} documents...")
            print(f"  Batch size: {self.batch_size}")
            print(f"  Model: {self.model_name}")
        
        all_embeddings = []
        total_batches = (len(self.documents) + self.batch_size - 1) // self.batch_size
        
        for batch_idx in range(total_batches):
            start = batch_idx * self.batch_size
            end = min(start + self.batch_size, len(self.documents))
            batch_docs = self.documents[start:end]
            
            # Build text for embedding (title + api_name + namespace)
            texts = []
            for doc in batch_docs:
                # Use title and API name for semantic matching
                text = f"{doc.title}. {doc.api_name}. Namespace: {doc.namespace}"
                texts.append(text)
            
            if self.verbose:
                print(f"  Batch {batch_idx + 1}/{total_batches}: docs {start}-{end-1}...", end="", flush=True)
            
            start_time = time.time()
            batch_embeddings = self._get_embeddings_batch(texts)
            elapsed = time.time() - start_time
            
            all_embeddings.append(batch_embeddings)
            
            if self.verbose:
                print(f" done ({elapsed:.1f}s)")
        
        self.embeddings = np.vstack(all_embeddings)
        
        if self.verbose:
            print(f"  Embeddings shape: {self.embeddings.shape}")
        
        return self.embeddings
    
    def save_database(self, include_content: bool = True):
        """Save all database components to output directory
        
        Args:
            include_content: If True, embed full markdown content in documents.json
                           (larger file ~35MB but self-contained, no need for source files)
        """
        if self.verbose:
            print(f"\nSaving database to {self.output_path}...")
            if include_content:
                print(f"  Including full document content (self-contained mode)")
        
        # Create output directory
        self.output_path.mkdir(parents=True, exist_ok=True)
        
        # Save documents with optional content
        docs_data = []
        content_chars = 0
        
        for doc in self.documents:
            doc_entry = {
                "id": doc.id,
                "namespace": doc.namespace,
                "api_name": doc.api_name,
                "file_path": doc.file_path,
                "title": doc.title
            }
            
            if include_content:
                content = self.load_document_content(doc)
                if content:
                    doc_entry["content"] = content
                    content_chars += len(content)
            
            docs_data.append(doc_entry)
        
        docs_path = self.output_path / "documents.json"
        with open(docs_path, 'w', encoding='utf-8') as f:
            json.dump(docs_data, f, indent=2)
        
        if self.verbose:
            size_mb = docs_path.stat().st_size / (1024 * 1024)
            print(f"  Saved documents.json ({len(docs_data)} docs, {size_mb:.1f} MB)")
            if include_content:
                print(f"  Total content: {content_chars:,} chars embedded")
        
        # Save namespace index
        ns_data = {
            "namespaces": self.namespaces,
            "type_to_namespace": self.type_to_namespace
        }
        
        ns_path = self.output_path / "namespace_index.json"
        with open(ns_path, 'w', encoding='utf-8') as f:
            json.dump(ns_data, f, indent=2)
        
        if self.verbose:
            print(f"  Saved namespace_index.json ({len(self.namespaces)} namespaces)")
        
        # Save embeddings
        emb_path = self.output_path / "embeddings.npy"
        np.save(emb_path, self.embeddings)
        
        if self.verbose:
            print(f"  Saved embeddings.npy {self.embeddings.shape}")
        
        # Save config
        config = {
            "total_documents": len(self.documents),
            "total_namespaces": len(self.namespaces),
            "embedding_dim": self.embeddings.shape[1] if self.embeddings is not None else 0,
            "has_faiss_index": False,
            "model_name": self.model_name,
            "server_url": self.embedding_url
        }
        
        config_path = self.output_path / "config.json"
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2)
        
        if self.verbose:
            print(f"  Saved config.json")
        
        # Optionally build FAISS index
        try:
            import faiss
            
            if self.verbose:
                print(f"  Building FAISS index...")
            
            # Normalize embeddings for cosine similarity
            norms = np.linalg.norm(self.embeddings, axis=1, keepdims=True)
            norms[norms == 0] = 1
            normed_embs = self.embeddings / norms
            
            # Build index
            index = faiss.IndexFlatIP(self.embeddings.shape[1])
            index.add(normed_embs.astype(np.float32))
            
            faiss_path = self.output_path / "faiss_index.bin"
            faiss.write_index(index, str(faiss_path))
            
            # Update config
            config["has_faiss_index"] = True
            with open(config_path, 'w', encoding='utf-8') as f:
                json.dump(config, f, indent=2)
            
            if self.verbose:
                print(f"  Saved faiss_index.bin")
                
        except ImportError:
            if self.verbose:
                print(f"  FAISS not available, skipping index build")
    
    def build(self, include_content: bool = True):
        """Run the complete database build process
        
        Args:
            include_content: Embed full markdown content in documents.json
                           (self-contained, no need for source files at runtime)
        """
        print(f"Unity RAG Database Builder")
        print(f"{'='*50}")
        
        # Step 1: Scan documents
        doc_count = self.scan_documents()
        
        if doc_count == 0:
            print("Error: No documents found!")
            return False
        
        # Step 2: Generate embeddings
        self.generate_embeddings()
        
        # Step 3: Save database
        self.save_database(include_content=include_content)
        
        print(f"\n{'='*50}")
        print(f"Database build complete!")
        print(f"  Documents: {len(self.documents)}")
        print(f"  Namespaces: {len(self.namespaces)}")
        print(f"  Content embedded: {include_content}")
        print(f"  Output: {self.output_path}")
        
        return True


def main():
    parser = argparse.ArgumentParser(
        description="Build Unity RAG database from markdown documentation"
    )
    parser.add_argument(
        "--input", "-i",
        required=True,
        help="Path to Unity markdown docs (ScriptReference folder)"
    )
    parser.add_argument(
        "--output", "-o",
        default="unity_rag_db",
        help="Output path for database files (default: unity_rag_db)"
    )
    parser.add_argument(
        "--embedding-url",
        default="http://127.0.0.1:1234/v1/embeddings",
        help="Embedding server URL (default: LM Studio on localhost)"
    )
    parser.add_argument(
        "--model",
        default="text-embedding-nomic-embed-text-v1.5",
        help="Embedding model name"
    )
    parser.add_argument(
        "--batch-size",
        type=int,
        default=300,
        help="Documents per embedding batch (default: 300)"
    )
    parser.add_argument(
        "--quiet", "-q",
        action="store_true",
        help="Suppress progress output"
    )
    parser.add_argument(
        "--no-content",
        action="store_true",
        help="Don't embed document content (smaller database, requires source files at runtime)"
    )
    
    args = parser.parse_args()
    
    builder = UnityRAGBuilder(
        input_path=args.input,
        output_path=args.output,
        embedding_url=args.embedding_url,
        model_name=args.model,
        batch_size=args.batch_size,
        verbose=not args.quiet
    )
    
    success = builder.build(include_content=not args.no_content)
    return 0 if success else 1


if __name__ == "__main__":
    exit(main())

