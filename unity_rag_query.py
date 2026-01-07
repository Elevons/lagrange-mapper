"""
Unity RAG Query Interface
Two-tier retrieval system for Unity API documentation:
- Tier 1: Namespace selection via direct type lookup from IR JSON
- Tier 2: Semantic search within selected namespaces using embeddings
"""

import json
import numpy as np
import os
import requests
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Tuple, Set


@dataclass
class RetrievedDoc:
    """A single retrieved document from the RAG system"""
    id: int
    namespace: str
    api_name: str
    file_path: str
    title: str
    score: float = 0.0
    content: Optional[str] = None


@dataclass
class RetrievalResult:
    """Result of a retrieval operation"""
    query: str
    selected_namespaces: List[str]
    documents: List[RetrievedDoc]
    total_searched: int
    
    def __repr__(self):
        return (f"RetrievalResult(namespaces={len(self.selected_namespaces)}, "
                f"docs={len(self.documents)}, searched={self.total_searched})")


class UnityRAG:
    """
    Unity Documentation RAG System
    
    Two-tier retrieval:
    1. Select relevant namespaces from IR JSON types
    2. Semantic search within those namespaces
    """
    
    def __init__(self, 
                 db_path: str = "unity_rag_db",
                 docs_source_path: Optional[str] = None,
                 embedding_url: str = "http://127.0.0.1:1234/v1/embeddings",
                 verbose: bool = False):
        """
        Initialize the RAG system.
        
        Args:
            db_path: Path to unity_rag_db folder
            docs_source_path: Path to Unity markdown docs (for loading content)
            embedding_url: URL of embedding server
            verbose: Print debug information
        """
        self.db_path = db_path
        self.docs_source_path = docs_source_path
        self.embedding_url = embedding_url
        self.verbose = verbose
        
        # Load database components
        self._load_database()
        
    def _load_database(self):
        """Load all database components"""
        if self.verbose:
            print(f"Loading RAG database from {self.db_path}...")
        
        # Load config
        config_path = os.path.join(self.db_path, "config.json")
        with open(config_path, 'r', encoding='utf-8') as f:
            self.config = json.load(f)
        
        # Load documents metadata (may include content if built with include_content=True)
        docs_path = os.path.join(self.db_path, "documents.json")
        with open(docs_path, 'r', encoding='utf-8') as f:
            self.documents = json.load(f)
        
        # Check if content is embedded in documents.json
        self.has_embedded_content = len(self.documents) > 0 and "content" in self.documents[0]
        
        # Load namespace index
        ns_path = os.path.join(self.db_path, "namespace_index.json")
        with open(ns_path, 'r', encoding='utf-8') as f:
            ns_data = json.load(f)
            self.namespaces = ns_data.get("namespaces", {})
            self.type_to_namespace = ns_data.get("type_to_namespace", {})
        
        # Load embeddings
        emb_path = os.path.join(self.db_path, "embeddings.npy")
        self.embeddings = np.load(emb_path)
        
        # Build type map for property chain validation
        self.type_map = self._build_type_map()
        
        if self.verbose:
            print(f"  Loaded {len(self.documents)} documents")
            print(f"  Loaded {len(self.namespaces)} namespaces")
            print(f"  Embeddings shape: {self.embeddings.shape}")
            print(f"  Content embedded: {self.has_embedded_content}")
            print(f"  Type map: {len(self.type_map)} entries")
            print(f"  Model: {self.config.get('model_name', 'unknown')}")
    
    def _build_type_map(self) -> Dict[str, str]:
        """
        Build a complete API -> ReturnType map from ALL docs.
        Used for property chain validation.
        
        Returns:
            Dict mapping "ClassName.member" -> "ReturnType"
        """
        import re
        type_map = {}
        
        for doc in self.documents:
            api_name = doc.get("api_name", "")
            content = doc.get("content", "")
            
            if not api_name or not content:
                continue
            
            return_type = None
            
            # Pattern 1: Method declaration - "public ReturnType MethodName("
            # Handles: public void Play(), public AudioClip GetClip(), public static Vector3 Lerp()
            decl_match = re.search(
                r'public\s+(?:static\s+)?(?:override\s+)?(\w+(?:\[\])?)\s+\w+\s*[\(\<]', 
                content
            )
            if decl_match:
                return_type = decl_match.group(1)
            
            # Pattern 2: Property declaration - "public ReturnType propertyName { get"
            if not return_type:
                prop_match = re.search(
                    r'public\s+(?:static\s+)?(\w+(?:\[\])?)\s+\w+\s*\{', 
                    content
                )
                if prop_match:
                    return_type = prop_match.group(1)
            
            # Pattern 3: Returns section - "### Returns\n\n**TypeName**"
            if not return_type:
                returns_match = re.search(
                    r'### Returns\s*\n+\*\*(\w+(?:\[\])?)\*\*', 
                    content
                )
                if returns_match:
                    return_type = returns_match.group(1)
            
            # Pattern 4: First linked type in description - "[TypeName](TypeName.html)"
            # Fallback for properties without explicit declaration
            if not return_type:
                link_match = re.search(r'\[(\w+)\]\(\w+\.html\)', content)
                if link_match:
                    return_type = link_match.group(1)
            
            # Store if we found a non-void type
            if return_type and return_type.lower() not in ('void', 'null', 'true', 'false'):
                # Normalize api_name: handle both "Class.method" and "Class-property" formats
                normalized = api_name.replace('-', '.')
                type_map[normalized] = return_type
                # Also store the hyphenated version for lookups
                type_map[api_name] = return_type
        
        return type_map
    
    def resolve_chain_type(self, base_type: str, chain: List[str]) -> Tuple[str, List[Dict]]:
        """
        Resolve a property/method chain to validate each step.
        
        Args:
            base_type: Starting type (e.g., "AudioSource")
            chain: List of properties/methods (e.g., ["clip", "Play"])
            
        Returns:
            Tuple of (final_type, list of invalid steps with details)
        """
        current_type = base_type
        invalid_steps = []
        
        for i, step in enumerate(chain):
            # Try various API name formats
            api_variants = [
                f"{current_type}.{step}",
                f"{current_type}-{step}",
                f"{current_type}.{step.lower()}",
                f"{current_type}-{step.lower()}",
            ]
            
            next_type = None
            for variant in api_variants:
                if variant in self.type_map:
                    next_type = self.type_map[variant]
                    break
            
            if next_type:
                current_type = next_type
            else:
                # Check if this API exists at all (search in documents)
                exists = False
                for variant in api_variants:
                    for doc in self.documents:
                        doc_api = doc.get("api_name", "").replace('-', '.')
                        if doc_api.lower() == variant.lower():
                            exists = True
                            break
                    if exists:
                        break
                
                if not exists:
                    invalid_steps.append({
                        "api": f"{current_type}.{step}",
                        "step_index": i,
                        "base_type": base_type,
                        "chain": chain
                    })
                
                # Can't continue chain if we don't know the type
                # But keep going to find more errors
                current_type = "Unknown"
        
        return current_type, invalid_steps
    
    def get_return_type(self, api: str) -> Optional[str]:
        """Get return type for an API, if known."""
        return self.type_map.get(api) or self.type_map.get(api.replace('.', '-'))
    
    def select_namespaces(self, ir_json: Dict) -> List[str]:
        """
        Extract Unity types from IR JSON and map to namespaces.
        
        Extracts types from:
        - fields[].type
        - components[]
        - Any other type references
        
        Args:
            ir_json: The intermediate representation JSON
            
        Returns:
            List of namespace names that contain relevant documentation
        """
        types_found: Set[str] = set()
        
        # Extract from fields
        for field in ir_json.get("fields", []):
            field_type = field.get("type", "")
            if field_type and field_type not in ["float", "int", "string", "bool", "double"]:
                types_found.add(field_type)
        
        # Extract from components
        for component in ir_json.get("components", []):
            if isinstance(component, str):
                types_found.add(component)
            elif isinstance(component, dict):
                types_found.add(component.get("name", ""))
        
        # Extract from behaviors (look for Unity types in action descriptions)
        for behavior in ir_json.get("behaviors", []):
            # Check trigger for type hints
            trigger = behavior.get("trigger", "")
            self._extract_types_from_text(trigger, types_found)
            
            # Check actions
            for action in behavior.get("actions", []):
                if isinstance(action, dict):
                    action_text = action.get("action", "")
                    self._extract_types_from_text(action_text, types_found)
        
        # Map types to namespaces
        namespaces = []
        for type_name in types_found:
            if type_name in self.type_to_namespace:
                ns = self.type_to_namespace[type_name]
                if ns not in namespaces:
                    namespaces.append(ns)
        
        # Add common namespaces based on patterns
        self._add_inferred_namespaces(ir_json, namespaces)
        
        if self.verbose:
            print(f"  Types found: {types_found}")
            print(f"  Namespaces selected: {namespaces}")
        
        return namespaces
    
    def _extract_types_from_text(self, text: str, types_found: Set[str]):
        """Extract potential Unity type names from text"""
        # Guard against non-string input (LLM might return bool/int instead of string)
        if not isinstance(text, str):
            return
        
        # Common Unity types to look for
        unity_types = [
            "Rigidbody", "Rigidbody2D", "Collider", "Collider2D",
            "Transform", "GameObject", "Component",
            "AudioSource", "AudioClip", "AudioListener",
            "Camera", "Light", "Renderer", "MeshRenderer",
            "Animator", "Animation", "ParticleSystem",
            "Physics", "Physics2D", "Vector3", "Vector2", "Quaternion",
            "CharacterController", "NavMeshAgent",
            "Canvas", "RectTransform", "Button", "Text", "Image"
        ]
        
        for type_name in unity_types:
            if type_name.lower() in text.lower():
                types_found.add(type_name)
    
    def _add_inferred_namespaces(self, ir_json: Dict, namespaces: List[str]):
        """Add namespaces inferred from behavior patterns using dynamic lookup"""
        behaviors_text = json.dumps(ir_json.get("behaviors", []))
        class_name = ir_json.get("class_name", "").lower()
        components_text = json.dumps(ir_json.get("components", [])).lower()
        full_text = f"{behaviors_text} {class_name} {components_text}".lower()
        
        # Keyword to namespace mapping - look up in available namespaces
        keyword_hints = {
            # Physics
            "force": ["Physics", "Rigidbody"],
            "physics": ["Physics"],
            "collision": ["Physics", "Collider"],
            "gravity": ["Physics", "Rigidbody"],
            "raycast": ["Physics"],
            # Audio
            "audio": ["AudioSource", "AudioClip"],
            "sound": ["AudioSource", "AudioClip"],
            "music": ["AudioSource"],
            # Instantiation
            "spawn": ["Object"],
            "instantiate": ["Object"],
            "prefab": ["Object"],
            # Animation
            "animate": ["Animator", "Animation"],
            "shake": ["Animator", "Animation"],
            # UI (will only add if namespace exists in database)
            "text": ["Text"],
            "button": ["Button"],
            "slider": ["Slider"],
            "canvas": ["Canvas"],
            "image": ["Image"],
            "ui": ["Canvas", "RectTransform"],
            "health": ["Slider", "Image"],
            "bar": ["Slider", "Image"],
            # Transform
            "move": ["Transform"],
            "rotate": ["Transform"],
            "position": ["Transform"],
        }
        
        for keyword, potential_namespaces in keyword_hints.items():
            if keyword in full_text:
                for ns in potential_namespaces:
                    # Only add if namespace actually exists in our database
                    if ns in self.namespaces and ns not in namespaces:
                        namespaces.append(ns)
    
    def _get_embedding(self, text: str) -> np.ndarray:
        """Get embedding for a single text query"""
        response = requests.post(
            self.embedding_url,
            json={
                "model": self.config.get("model_name", "text-embedding-nomic-embed-text-v1.5"),
                "input": text
            },
            timeout=30
        )
        
        if response.status_code != 200:
            raise Exception(f"Embedding API error: {response.status_code}")
        
        data = response.json()
        return np.array(data["data"][0]["embedding"], dtype=np.float32)
    
    def _build_query_from_ir(self, ir_json: Dict) -> str:
        """Build a natural language query from IR JSON for semantic search"""
        parts = []
        
        # Class purpose
        class_name = ir_json.get("class_name", "")
        if class_name:
            parts.append(f"Unity script: {class_name}")
        
        # Components needed
        components = ir_json.get("components", [])
        if components:
            parts.append(f"Uses: {', '.join(components)}")
        
        # Behavior descriptions
        for behavior in ir_json.get("behaviors", []):
            trigger = behavior.get("trigger", "")
            if trigger:
                parts.append(trigger)
            
            for action in behavior.get("actions", []):
                if isinstance(action, dict):
                    action_text = action.get("action", "")
                    if action_text:
                        parts.append(action_text)
        
        return " ".join(parts)
    
    def search(self, 
               query: str, 
               namespaces: Optional[List[str]] = None,
               threshold: float = 0.5,
               top_k: int = 10) -> List[RetrievedDoc]:
        """
        Semantic search within specified namespaces.
        
        Args:
            query: Natural language search query
            namespaces: Limit search to these namespaces (None = all)
            threshold: Minimum similarity score
            top_k: Maximum results to return
            
        Returns:
            List of matching documents sorted by score
        """
        # Get query embedding
        query_emb = self._get_embedding(query)
        query_emb = query_emb / np.linalg.norm(query_emb)
        
        # Get document indices to search
        if namespaces:
            doc_indices = []
            for ns in namespaces:
                if ns in self.namespaces:
                    doc_indices.extend(self.namespaces[ns].get("doc_ids", []))
            doc_indices = list(set(doc_indices))
        else:
            doc_indices = list(range(len(self.documents)))
        
        if not doc_indices:
            return []
        
        # Get embeddings for these docs
        doc_embs = self.embeddings[doc_indices]
        
        # Normalize embeddings
        norms = np.linalg.norm(doc_embs, axis=1, keepdims=True)
        norms[norms == 0] = 1  # Avoid division by zero
        doc_embs = doc_embs / norms
        
        # Compute cosine similarities
        similarities = np.dot(doc_embs, query_emb)
        
        # Filter and sort
        results = []
        for i, sim in enumerate(similarities):
            if sim >= threshold:
                doc_idx = doc_indices[i]
                doc = self.documents[doc_idx]
                results.append(RetrievedDoc(
                    id=doc["id"],
                    namespace=doc["namespace"],
                    api_name=doc["api_name"],
                    file_path=doc["file_path"],
                    title=doc["title"],
                    score=float(sim)
                ))
        
        # Sort by score descending
        results.sort(key=lambda x: x.score, reverse=True)
        
        return results[:top_k]
    
    def retrieve_for_ir(self,
                        ir_json: Dict,
                        threshold: float = 0.6,
                        top_k_per_namespace: int = 3,
                        top_k_total: int = 10,
                        include_content: bool = False) -> RetrievalResult:
        """
        Main retrieval method for pipeline integration.
        
        1. Extracts types from IR JSON
        2. Maps to relevant namespaces
        3. Performs semantic search within those namespaces
        
        Args:
            ir_json: The intermediate representation JSON
            threshold: Minimum similarity score
            top_k_per_namespace: Max docs per namespace
            top_k_total: Max total docs returned
            include_content: Load full markdown content
            
        Returns:
            RetrievalResult with documents and metadata
        """
        if self.verbose:
            print(f"\nRetrieving docs for: {ir_json.get('class_name', 'unknown')}")
        
        # Tier 1: Select namespaces
        selected_ns = self.select_namespaces(ir_json)
        
        if not selected_ns:
            if self.verbose:
                print("  No namespaces selected, using broad search")
            # Fallback to common Unity namespaces
            selected_ns = ["MonoBehaviour", "GameObject", "Transform", "Physics"]
        
        # Build query from IR
        query = self._build_query_from_ir(ir_json)
        
        if self.verbose:
            print(f"  Query: {query[:100]}...")
        
        # Tier 2: Search per namespace
        all_results: List[RetrievedDoc] = []
        total_searched = 0
        
        for ns in selected_ns:
            if ns not in self.namespaces:
                continue
                
            ns_doc_count = self.namespaces[ns].get("doc_count", 0)
            total_searched += ns_doc_count
            
            results = self.search(
                query=query,
                namespaces=[ns],
                threshold=threshold,
                top_k=top_k_per_namespace
            )
            
            all_results.extend(results)
            
            if self.verbose and results:
                print(f"  {ns}: {len(results)} docs (best={results[0].score:.2f})")
        
        # Deduplicate and sort by score
        seen_ids = set()
        unique_results = []
        for doc in sorted(all_results, key=lambda x: x.score, reverse=True):
            if doc.id not in seen_ids:
                seen_ids.add(doc.id)
                unique_results.append(doc)
        
        # Limit total
        final_docs = unique_results[:top_k_total]
        
        # Load content if requested (from embedded data or files)
        if include_content:
            for doc in final_docs:
                if doc.content is None:
                    doc.content = self._load_doc_content(doc.file_path, doc.id)
        
        return RetrievalResult(
            query=query,
            selected_namespaces=selected_ns,
            documents=final_docs,
            total_searched=total_searched
        )
    
    def _load_doc_content(self, file_path: str, doc_id: int = None) -> Optional[str]:
        """Load markdown content - from embedded data or source files
        
        Args:
            file_path: Path to the document file
            doc_id: Optional document ID to look up embedded content
            
        Returns:
            Document content string or None
        """
        # First try embedded content in documents.json
        if self.has_embedded_content and doc_id is not None:
            if 0 <= doc_id < len(self.documents):
                content = self.documents[doc_id].get("content")
                if content:
                    return content
        
        # Fallback to file system if docs_source_path is set
        if self.docs_source_path:
            full_path = os.path.join(self.docs_source_path, file_path)
            try:
                with open(full_path, 'r', encoding='utf-8') as f:
                    return f.read()
            except Exception as e:
                if self.verbose:
                    print(f"  Could not load {file_path}: {e}")
        
        return None
    
    def get_doc_content(self, doc: 'RetrievedDoc') -> Optional[str]:
        """Get content for a retrieved document"""
        return self._load_doc_content(doc.file_path, doc.id)
    
    def validate_api(self, api_name: str, threshold: float = 0.8) -> Tuple[bool, List[Tuple[str, float]]]:
        """
        Validate that an API name exists in Unity documentation.
        
        Args:
            api_name: API name to validate (e.g., "Rigidbody.AddForce")
            threshold: Similarity threshold for suggestions
            
        Returns:
            (is_valid, suggestions) where suggestions are (api_name, score) tuples
        """
        # Check exact match first
        for doc in self.documents:
            if doc["api_name"] == api_name or doc["api_name"].endswith(f".{api_name}"):
                return True, [(doc["api_name"], 1.0)]
        
        # Search for similar APIs
        query_emb = self._get_embedding(api_name)
        query_emb = query_emb / np.linalg.norm(query_emb)
        
        # Normalize all embeddings
        norms = np.linalg.norm(self.embeddings, axis=1, keepdims=True)
        norms[norms == 0] = 1
        normed_embs = self.embeddings / norms
        
        # Compute similarities
        similarities = np.dot(normed_embs, query_emb)
        
        # Get top suggestions
        top_indices = np.argsort(similarities)[::-1][:5]
        suggestions = [
            (self.documents[i]["api_name"], float(similarities[i]))
            for i in top_indices
            if similarities[i] >= threshold
        ]
        
        # Check if top match is close enough to be valid
        is_valid = len(suggestions) > 0 and suggestions[0][1] >= 0.95
        
        return is_valid, suggestions
    
    def format_context_for_prompt(self, 
                                   documents: List[RetrievedDoc],
                                   max_tokens: int = 2000,
                                   include_content: bool = True) -> str:
        """
        Format retrieved documents for LLM prompt injection.
        
        Args:
            documents: List of retrieved documents
            max_tokens: Approximate max token limit
            include_content: Include full doc content if available
            
        Returns:
            Formatted context string
        """
        if not documents:
            return ""
        
        lines = ["RELEVANT UNITY API DOCUMENTATION:", ""]
        char_count = 0
        approx_chars_per_token = 4
        max_chars = max_tokens * approx_chars_per_token
        
        for doc in documents:
            # Header
            header = f"### {doc.api_name} (score: {doc.score:.2f})"
            
            if char_count + len(header) > max_chars:
                break
            
            lines.append(header)
            char_count += len(header)
            
            # Content
            if include_content and doc.content:
                # Truncate content if needed
                remaining = max_chars - char_count
                content = doc.content[:remaining] if len(doc.content) > remaining else doc.content
                lines.append(content)
                lines.append("")
                char_count += len(content)
            else:
                lines.append(f"API: {doc.title}")
                lines.append(f"Namespace: {doc.namespace}")
                lines.append("")
                char_count += len(doc.title) + len(doc.namespace) + 20
        
        lines.append("---")
        return "\n".join(lines)
    
    def extract_apis_from_code(self, code: str) -> List[str]:
        """
        Extract Unity API calls from C# code.
        Looks for patterns like ClassName.MethodName, GetComponent<T>, etc.
        
        Args:
            code: C# source code
            
        Returns:
            List of API names found (e.g., ["Rigidbody.AddForce", "Physics.OverlapSphere"])
        """
        import re
        
        apis_found = []
        
        # Pattern 1: Static method calls like Physics.OverlapSphere, Mathf.Lerp
        static_pattern = r'\b(Physics|Mathf|Vector3|Quaternion|Time|Input|Debug|GameObject|Object|Resources|Application)\s*\.\s*(\w+)'
        for match in re.finditer(static_pattern, code):
            api = f"{match.group(1)}.{match.group(2)}"
            if api not in apis_found:
                apis_found.append(api)
        
        # Pattern 2: Instance method calls on common Unity types
        instance_pattern = r'\b(rb|rigidbody|transform|audioSource|animator|collider|renderer)\s*\.\s*(\w+)'
        type_map = {
            'rb': 'Rigidbody', 'rigidbody': 'Rigidbody',
            'transform': 'Transform', 'audioSource': 'AudioSource',
            'animator': 'Animator', 'collider': 'Collider', 'renderer': 'Renderer'
        }
        for match in re.finditer(instance_pattern, code, re.IGNORECASE):
            var_name = match.group(1).lower()
            method = match.group(2)
            if var_name in type_map:
                api = f"{type_map[var_name]}.{method}"
                if api not in apis_found:
                    apis_found.append(api)
        
        # Pattern 3: GetComponent<T> patterns
        getcomp_pattern = r'GetComponent\s*<\s*(\w+)\s*>'
        for match in re.finditer(getcomp_pattern, code):
            component = match.group(1)
            if component not in apis_found:
                apis_found.append(component)
        
        # Pattern 4: Unity lifecycle methods
        lifecycle_pattern = r'\b(Start|Update|FixedUpdate|LateUpdate|Awake|OnEnable|OnDisable|OnDestroy|OnTriggerEnter|OnTriggerExit|OnCollisionEnter|OnCollisionExit)\s*\('
        for match in re.finditer(lifecycle_pattern, code):
            method = f"MonoBehaviour.{match.group(1)}"
            if method not in apis_found:
                apis_found.append(method)
        
        # Pattern 5: Common Unity methods with specific signatures
        common_apis = [
            ('AddForce', 'Rigidbody.AddForce'),
            ('AddExplosionForce', 'Rigidbody.AddExplosionForce'),
            ('AddTorque', 'Rigidbody.AddTorque'),
            ('PlayOneShot', 'AudioSource.PlayOneShot'),
            ('Play()', 'AudioSource.Play'),
            ('Instantiate', 'Object.Instantiate'),
            ('Destroy', 'Object.Destroy'),
            ('OverlapSphere', 'Physics.OverlapSphere'),
            ('Raycast', 'Physics.Raycast'),
            ('Lerp', 'Vector3.Lerp'),
            ('MoveTowards', 'Vector3.MoveTowards'),
            ('LookAt', 'Transform.LookAt'),
            ('Rotate', 'Transform.Rotate'),
            ('Translate', 'Transform.Translate'),
        ]
        for keyword, api_name in common_apis:
            if keyword in code and api_name not in apis_found:
                apis_found.append(api_name)
        
        return apis_found
    
    def retrieve_for_code_steering(self, 
                                   code: str,
                                   threshold: float = 0.6,
                                   top_k: int = 8) -> RetrievalResult:
        """
        Retrieve Unity docs relevant to APIs used in generated code.
        Used for RAG-based contrastive steering.
        
        Args:
            code: Generated C# code to analyze
            threshold: Minimum similarity for results
            top_k: Max docs to retrieve
            
        Returns:
            RetrievalResult with relevant documentation
        """
        # Extract APIs from code
        apis = self.extract_apis_from_code(code)
        
        if self.verbose:
            print(f"  Extracted APIs from code: {apis[:10]}{'...' if len(apis) > 10 else ''}")
        
        # Map APIs to namespaces
        namespaces = []
        for api in apis:
            # Try direct namespace lookup
            base_type = api.split('.')[0] if '.' in api else api
            if base_type in self.type_to_namespace:
                ns = self.type_to_namespace[base_type]
                if ns not in namespaces:
                    namespaces.append(ns)
        
        if not namespaces:
            namespaces = ["MonoBehaviour", "GameObject", "Transform"]
        
        # Build query from API list
        query = f"Unity API usage: {', '.join(apis[:15])}"
        
        # Search
        all_results = []
        total_searched = 0
        
        for ns in namespaces[:8]:  # Limit namespace search
            if ns not in self.namespaces:
                continue
            
            ns_docs = self.namespaces[ns].get("doc_count", 0)
            total_searched += ns_docs
            
            results = self.search(query, namespaces=[ns], threshold=threshold, top_k=3)
            all_results.extend(results)
        
        # Also do targeted search for specific API names
        for api in apis[:5]:
            targeted = self.search(api, threshold=0.7, top_k=2)
            all_results.extend(targeted)
        
        # Dedupe and sort
        seen = set()
        unique = []
        for doc in sorted(all_results, key=lambda x: x.score, reverse=True):
            if doc.id not in seen:
                seen.add(doc.id)
                unique.append(doc)
        
        return RetrievalResult(
            query=query,
            selected_namespaces=namespaces,
            documents=unique[:top_k],
            total_searched=total_searched
        )
    
    def build_steering_prompt(self, 
                              problematic_code: str, 
                              ir_json: Dict,
                              docs: List[RetrievedDoc]) -> str:
        """
        Build a RAG-based steering prompt using Unity documentation.
        
        Instead of showing good/bad code pairs from calibration,
        shows official Unity API documentation as the ground truth.
        
        Args:
            problematic_code: Code that needs correction
            ir_json: Original IR specification
            docs: Retrieved Unity documentation
            
        Returns:
            Steering prompt for LLM
        """
        if not docs:
            return None
        
        prompt_parts = [
            "The following C# code may have Unity API issues. Here is the official Unity documentation for the APIs being used:\n"
        ]
        
        # Add relevant documentation
        prompt_parts.append("=== UNITY API REFERENCE ===\n")
        
        for doc in docs[:6]:
            prompt_parts.append(f"### {doc.api_name}")
            if doc.content:
                # Truncate content to key info
                content = doc.content[:800]
                prompt_parts.append(content)
            else:
                prompt_parts.append(f"API: {doc.title}")
                prompt_parts.append(f"Namespace: {doc.namespace}")
            prompt_parts.append("")
        
        prompt_parts.append("=== END REFERENCE ===\n")
        
        # Show the IR specification
        prompt_parts.append("=== BEHAVIOR SPECIFICATION ===")
        prompt_parts.append(json.dumps(ir_json, indent=2))
        prompt_parts.append("=== END SPECIFICATION ===\n")
        
        # Show the problematic code
        prompt_parts.append("=== CODE TO IMPROVE ===")
        prompt_parts.append(f"```csharp\n{problematic_code}\n```")
        prompt_parts.append("=== END CODE ===\n")
        
        # Instructions
        prompt_parts.append("""Review the code against the Unity API documentation above and fix any issues:
1. Ensure method signatures match the official Unity API
2. Use correct parameter types and orders
3. Fix any non-existent methods or properties
4. Follow Unity best practices shown in the documentation

Generate the corrected C# code (no markdown, no explanations):""")
        
        return "\n".join(prompt_parts)


def test_rag():
    """Test the RAG system"""
    print("Testing Unity RAG System\n")
    
    # Initialize
    rag = UnityRAG(verbose=True)
    
    # Test IR
    test_ir = {
        "class_name": "ExplosionTrigger",
        "components": ["BoxCollider", "Rigidbody"],
        "fields": [
            {"name": "explosionForce", "type": "float", "default": 50},
            {"name": "radiusMeters", "type": "float", "default": 10},
            {"name": "explosionSound", "type": "AudioClip", "default": None}
        ],
        "behaviors": [
            {
                "name": "apply_explosion",
                "trigger": "detect collision with trigger zone",
                "actions": [
                    {"action": "get all rigidbodies in a sphere within radiusMeters"},
                    {"action": "apply explosion force to each rigidbody"},
                    {"action": "play audio clip explosionSound"}
                ]
            }
        ]
    }
    
    # Retrieve
    result = rag.retrieve_for_ir(test_ir, threshold=0.5, top_k_total=5)
    
    print(f"\nResult: {result}")
    print(f"\nDocuments:")
    for doc in result.documents:
        print(f"  - {doc.api_name} ({doc.score:.2f})")
    
    # Test API validation
    print("\nValidating APIs:")
    for api in ["Rigidbody.AddForce", "Rigidbody.AddExplosionForce", "FakeAPI.DoStuff"]:
        valid, suggestions = rag.validate_api(api)
        print(f"  {api}: valid={valid}, suggestions={suggestions[:2]}")
    
    # Format context
    context = rag.format_context_for_prompt(result.documents)
    print(f"\nContext (first 500 chars):\n{context[:500]}")


if __name__ == "__main__":
    test_rag()

