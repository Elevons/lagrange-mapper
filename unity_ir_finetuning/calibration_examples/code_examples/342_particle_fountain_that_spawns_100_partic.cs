// Prompt: particle fountain that spawns 100 particles per second each with own physics, unique color based on spawn time - each particle tracks nearest other particle and creates visual line if within 2 units
// Type: combat

using UnityEngine;
using System.Collections.Generic;

public class ParticleFountain : MonoBehaviour
{
    [Header("Fountain Settings")]
    [SerializeField] private float _spawnRate = 100f;
    [SerializeField] private float _fountainForce = 10f;
    [SerializeField] private float _spawnRadius = 0.5f;
    [SerializeField] private float _particleLifetime = 5f;
    
    [Header("Connection Settings")]
    [SerializeField] private float _connectionDistance = 2f;
    [SerializeField] private Material _lineMaterial;
    [SerializeField] private float _lineWidth = 0.02f;
    
    [Header("Particle Prefab")]
    [SerializeField] private GameObject _particlePrefab;
    
    private List<FountainParticle> _activeParticles = new List<FountainParticle>();
    private float _lastSpawnTime;
    private float _spawnInterval;
    private LineRenderer _lineRenderer;
    
    [System.Serializable]
    public class FountainParticle
    {
        public GameObject gameObject;
        public Rigidbody rigidbody;
        public Renderer renderer;
        public float spawnTime;
        public float lifetime;
        public FountainParticle nearestParticle;
        public float distanceToNearest;
        
        public FountainParticle(GameObject go, float time)
        {
            gameObject = go;
            rigidbody = go.GetComponent<Rigidbody>();
            renderer = go.GetComponent<Renderer>();
            spawnTime = time;
            lifetime = 0f;
            distanceToNearest = float.MaxValue;
        }
        
        public bool IsValid()
        {
            return gameObject != null;
        }
        
        public Vector3 Position
        {
            get { return gameObject != null ? gameObject.transform.position : Vector3.zero; }
        }
    }
    
    void Start()
    {
        _spawnInterval = 1f / _spawnRate;
        _lastSpawnTime = Time.time;
        
        SetupLineRenderer();
        CreateParticlePrefabIfNeeded();
    }
    
    void Update()
    {
        SpawnParticles();
        UpdateParticles();
        FindNearestParticles();
        DrawConnections();
        CleanupDeadParticles();
    }
    
    void SetupLineRenderer()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        if (_lineRenderer == null)
        {
            _lineRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        _lineRenderer.material = _lineMaterial != null ? _lineMaterial : CreateDefaultLineMaterial();
        _lineRenderer.startWidth = _lineWidth;
        _lineRenderer.endWidth = _lineWidth;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.positionCount = 0;
    }
    
    Material CreateDefaultLineMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = Color.white;
        return mat;
    }
    
    void CreateParticlePrefabIfNeeded()
    {
        if (_particlePrefab == null)
        {
            _particlePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _particlePrefab.transform.localScale = Vector3.one * 0.1f;
            
            Rigidbody rb = _particlePrefab.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = _particlePrefab.AddComponent<Rigidbody>();
            }
            rb.mass = 0.1f;
            rb.drag = 0.5f;
            
            _particlePrefab.SetActive(false);
        }
    }
    
    void SpawnParticles()
    {
        while (Time.time - _lastSpawnTime >= _spawnInterval)
        {
            SpawnSingleParticle();
            _lastSpawnTime += _spawnInterval;
        }
    }
    
    void SpawnSingleParticle()
    {
        Vector3 spawnPosition = transform.position + Random.insideUnitSphere * _spawnRadius;
        GameObject particle = Instantiate(_particlePrefab, spawnPosition, Random.rotation);
        particle.SetActive(true);
        
        Rigidbody rb = particle.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force = (Vector3.up + Random.insideUnitSphere * 0.3f).normalized * _fountainForce;
            rb.AddForce(force, ForceMode.Impulse);
        }
        
        FountainParticle fountainParticle = new FountainParticle(particle, Time.time);
        SetParticleColor(fountainParticle);
        _activeParticles.Add(fountainParticle);
    }
    
    void SetParticleColor(FountainParticle particle)
    {
        if (particle.renderer != null)
        {
            float hue = (particle.spawnTime * 0.1f) % 1f;
            Color color = Color.HSVToRGB(hue, 0.8f, 1f);
            particle.renderer.material.color = color;
        }
    }
    
    void UpdateParticles()
    {
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            if (_activeParticles[i].IsValid())
            {
                _activeParticles[i].lifetime = Time.time - _activeParticles[i].spawnTime;
            }
        }
    }
    
    void FindNearestParticles()
    {
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            FountainParticle current = _activeParticles[i];
            if (!current.IsValid()) continue;
            
            current.nearestParticle = null;
            current.distanceToNearest = float.MaxValue;
            
            for (int j = 0; j < _activeParticles.Count; j++)
            {
                if (i == j) continue;
                
                FountainParticle other = _activeParticles[j];
                if (!other.IsValid()) continue;
                
                float distance = Vector3.Distance(current.Position, other.Position);
                
                if (distance < current.distanceToNearest && distance <= _connectionDistance)
                {
                    current.distanceToNearest = distance;
                    current.nearestParticle = other;
                }
            }
        }
    }
    
    void DrawConnections()
    {
        List<Vector3> linePositions = new List<Vector3>();
        
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            FountainParticle particle = _activeParticles[i];
            if (!particle.IsValid() || particle.nearestParticle == null || !particle.nearestParticle.IsValid())
                continue;
            
            linePositions.Add(particle.Position);
            linePositions.Add(particle.nearestParticle.Position);
        }
        
        _lineRenderer.positionCount = linePositions.Count;
        if (linePositions.Count > 0)
        {
            _lineRenderer.SetPositions(linePositions.ToArray());
        }
    }
    
    void CleanupDeadParticles()
    {
        for (int i = _activeParticles.Count - 1; i >= 0; i--)
        {
            FountainParticle particle = _activeParticles[i];
            
            if (!particle.IsValid() || particle.lifetime > _particleLifetime)
            {
                if (particle.IsValid())
                {
                    DestroyImmediate(particle.gameObject);
                }
                _activeParticles.RemoveAt(i);
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _spawnRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _connectionDistance);
    }
}