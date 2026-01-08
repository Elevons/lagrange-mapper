// Prompt: leaves falling from trees
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class FallingLeaves : MonoBehaviour
{
    [Header("Leaf Settings")]
    [SerializeField] private GameObject[] _leafPrefabs;
    [SerializeField] private int _maxLeaves = 50;
    [SerializeField] private float _spawnRate = 2f;
    [SerializeField] private float _leafLifetime = 10f;
    
    [Header("Spawn Area")]
    [SerializeField] private Vector3 _spawnAreaSize = new Vector3(20f, 5f, 20f);
    [SerializeField] private float _spawnHeight = 10f;
    
    [Header("Physics")]
    [SerializeField] private float _fallSpeed = 2f;
    [SerializeField] private float _swayAmount = 1f;
    [SerializeField] private float _swaySpeed = 1f;
    [SerializeField] private float _rotationSpeed = 30f;
    [SerializeField] private Vector3 _windDirection = Vector3.right;
    [SerializeField] private float _windStrength = 0.5f;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    
    private List<LeafController> _activeLeaves = new List<LeafController>();
    private float _nextSpawnTime;
    
    [System.Serializable]
    public class LeafController
    {
        public GameObject leafObject;
        public Vector3 startPosition;
        public float swayOffset;
        public float spawnTime;
        public bool hasLanded;
        public Vector3 rotationAxis;
        
        public LeafController(GameObject leaf, Vector3 startPos, float time)
        {
            leafObject = leaf;
            startPosition = startPos;
            swayOffset = Random.Range(0f, Mathf.PI * 2f);
            spawnTime = time;
            hasLanded = false;
            rotationAxis = Random.onUnitSphere;
        }
    }
    
    void Start()
    {
        if (_leafPrefabs == null || _leafPrefabs.Length == 0)
        {
            Debug.LogWarning("No leaf prefabs assigned to FallingLeaves component!");
            return;
        }
        
        _nextSpawnTime = Time.time + (1f / _spawnRate);
    }
    
    void Update()
    {
        if (_leafPrefabs != null && _leafPrefabs.Length > 0)
        {
            SpawnLeaves();
        }
        
        UpdateLeaves();
        CleanupLeaves();
    }
    
    void SpawnLeaves()
    {
        if (Time.time >= _nextSpawnTime && _activeLeaves.Count < _maxLeaves)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();
            GameObject leafPrefab = _leafPrefabs[Random.Range(0, _leafPrefabs.Length)];
            
            if (leafPrefab != null)
            {
                GameObject newLeaf = Instantiate(leafPrefab, spawnPosition, Random.rotation);
                
                Rigidbody rb = newLeaf.GetComponent<Rigidbody>();
                if (rb == null)
                {
                    rb = newLeaf.AddComponent<Rigidbody>();
                }
                
                rb.useGravity = false;
                rb.drag = 2f;
                rb.angularDrag = 5f;
                
                Collider col = newLeaf.GetComponent<Collider>();
                if (col == null)
                {
                    col = newLeaf.AddComponent<BoxCollider>();
                }
                col.isTrigger = false;
                
                LeafController leafController = new LeafController(newLeaf, spawnPosition, Time.time);
                _activeLeaves.Add(leafController);
            }
            
            _nextSpawnTime = Time.time + (1f / _spawnRate) + Random.Range(-0.2f, 0.2f);
        }
    }
    
    Vector3 GetRandomSpawnPosition()
    {
        Vector3 basePosition = transform.position;
        float x = Random.Range(-_spawnAreaSize.x * 0.5f, _spawnAreaSize.x * 0.5f);
        float y = _spawnHeight + Random.Range(0f, _spawnAreaSize.y);
        float z = Random.Range(-_spawnAreaSize.z * 0.5f, _spawnAreaSize.z * 0.5f);
        
        return basePosition + new Vector3(x, y, z);
    }
    
    void UpdateLeaves()
    {
        for (int i = _activeLeaves.Count - 1; i >= 0; i--)
        {
            LeafController leaf = _activeLeaves[i];
            
            if (leaf.leafObject == null)
            {
                _activeLeaves.RemoveAt(i);
                continue;
            }
            
            if (!leaf.hasLanded)
            {
                UpdateFallingLeaf(leaf);
                CheckGroundCollision(leaf);
            }
            else
            {
                UpdateGroundedLeaf(leaf);
            }
        }
    }
    
    void UpdateFallingLeaf(LeafController leaf)
    {
        Transform leafTransform = leaf.leafObject.transform;
        
        float swayX = Mathf.Sin((Time.time + leaf.swayOffset) * _swaySpeed) * _swayAmount;
        float swayZ = Mathf.Cos((Time.time + leaf.swayOffset) * _swaySpeed * 0.7f) * _swayAmount * 0.5f;
        
        Vector3 movement = new Vector3(swayX, -_fallSpeed, swayZ) * Time.deltaTime;
        movement += _windDirection.normalized * _windStrength * Time.deltaTime;
        
        leafTransform.position += movement;
        
        leafTransform.Rotate(leaf.rotationAxis * _rotationSpeed * Time.deltaTime);
        
        Rigidbody rb = leaf.leafObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = movement / Time.deltaTime;
        }
    }
    
    void CheckGroundCollision(LeafController leaf)
    {
        Transform leafTransform = leaf.leafObject.transform;
        
        if (Physics.Raycast(leafTransform.position, Vector3.down, _groundCheckDistance, _groundLayer))
        {
            leaf.hasLanded = true;
            
            Rigidbody rb = leaf.leafObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.useGravity = true;
                rb.drag = 10f;
            }
        }
    }
    
    void UpdateGroundedLeaf(LeafController leaf)
    {
        Vector3 windForce = _windDirection.normalized * _windStrength * 0.1f;
        
        Rigidbody rb = leaf.leafObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(windForce, ForceMode.Force);
        }
    }
    
    void CleanupLeaves()
    {
        for (int i = _activeLeaves.Count - 1; i >= 0; i--)
        {
            LeafController leaf = _activeLeaves[i];
            
            if (leaf.leafObject == null || Time.time - leaf.spawnTime > _leafLifetime)
            {
                if (leaf.leafObject != null)
                {
                    DestroyImmediate(leaf.leafObject);
                }
                _activeLeaves.RemoveAt(i);
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * (_spawnHeight + _spawnAreaSize.y * 0.5f), _spawnAreaSize);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, _windDirection.normalized * 3f);
    }
    
    void OnDestroy()
    {
        for (int i = 0; i < _activeLeaves.Count; i++)
        {
            if (_activeLeaves[i].leafObject != null)
            {
                DestroyImmediate(_activeLeaves[i].leafObject);
            }
        }
        _activeLeaves.Clear();
    }
}