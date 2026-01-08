// Prompt: dust particles in light
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class DustParticlesInLight : MonoBehaviour
{
    [Header("Particle Settings")]
    [SerializeField] private int _maxParticles = 100;
    [SerializeField] private float _particleSize = 0.02f;
    [SerializeField] private float _particleSizeVariation = 0.01f;
    [SerializeField] private Material _dustMaterial;
    
    [Header("Movement")]
    [SerializeField] private float _driftSpeed = 0.5f;
    [SerializeField] private float _driftVariation = 0.3f;
    [SerializeField] private float _floatAmplitude = 0.1f;
    [SerializeField] private float _floatFrequency = 1f;
    
    [Header("Light Detection")]
    [SerializeField] private LayerMask _lightLayerMask = -1;
    [SerializeField] private float _lightDetectionRadius = 10f;
    [SerializeField] private float _fadeDistance = 2f;
    
    [Header("Spawn Area")]
    [SerializeField] private Vector3 _spawnAreaSize = new Vector3(5f, 5f, 5f);
    [SerializeField] private float _respawnHeight = 3f;
    
    private List<DustParticle> _particles = new List<DustParticle>();
    private Light[] _nearbyLights;
    private Camera _mainCamera;
    
    [System.Serializable]
    private class DustParticle
    {
        public GameObject gameObject;
        public Renderer renderer;
        public Vector3 velocity;
        public float floatOffset;
        public float baseAlpha;
        public Vector3 startPosition;
        public float lifetime;
        public float maxLifetime;
        
        public DustParticle(GameObject go, Renderer rend)
        {
            gameObject = go;
            renderer = rend;
            floatOffset = Random.Range(0f, Mathf.PI * 2f);
            baseAlpha = Random.Range(0.1f, 0.3f);
            maxLifetime = Random.Range(10f, 30f);
            lifetime = 0f;
        }
    }
    
    void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        if (_dustMaterial == null)
        {
            _dustMaterial = CreateDefaultDustMaterial();
        }
        
        InitializeParticles();
        InvokeRepeating(nameof(UpdateLightSources), 0f, 1f);
    }
    
    void Update()
    {
        UpdateParticles();
        UpdateParticleVisibility();
    }
    
    private Material CreateDefaultDustMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.color = new Color(1f, 1f, 1f, 0.2f);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }
    
    private void InitializeParticles()
    {
        for (int i = 0; i < _maxParticles; i++)
        {
            CreateParticle();
        }
    }
    
    private void CreateParticle()
    {
        GameObject particleGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        particleGO.name = "DustParticle";
        particleGO.transform.SetParent(transform);
        
        DestroyImmediate(particleGO.GetComponent<Collider>());
        
        Renderer renderer = particleGO.GetComponent<Renderer>();
        renderer.material = _dustMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        
        float size = _particleSize + Random.Range(-_particleSizeVariation, _particleSizeVariation);
        particleGO.transform.localScale = Vector3.one * size;
        
        Vector3 spawnPos = GetRandomSpawnPosition();
        particleGO.transform.position = spawnPos;
        
        DustParticle particle = new DustParticle(particleGO, renderer);
        particle.startPosition = spawnPos;
        particle.velocity = new Vector3(
            Random.Range(-_driftVariation, _driftVariation),
            Random.Range(-_driftSpeed * 0.5f, _driftSpeed * 0.5f),
            Random.Range(-_driftVariation, _driftVariation)
        );
        
        _particles.Add(particle);
    }
    
    private Vector3 GetRandomSpawnPosition()
    {
        return transform.position + new Vector3(
            Random.Range(-_spawnAreaSize.x * 0.5f, _spawnAreaSize.x * 0.5f),
            Random.Range(-_spawnAreaSize.y * 0.5f, _spawnAreaSize.y * 0.5f),
            Random.Range(-_spawnAreaSize.z * 0.5f, _spawnAreaSize.z * 0.5f)
        );
    }
    
    private void UpdateParticles()
    {
        for (int i = 0; i < _particles.Count; i++)
        {
            DustParticle particle = _particles[i];
            if (particle.gameObject == null) continue;
            
            particle.lifetime += Time.deltaTime;
            
            Vector3 floatMotion = new Vector3(
                Mathf.Sin(Time.time * _floatFrequency + particle.floatOffset) * _floatAmplitude,
                Mathf.Cos(Time.time * _floatFrequency * 0.7f + particle.floatOffset) * _floatAmplitude * 0.5f,
                Mathf.Sin(Time.time * _floatFrequency * 1.3f + particle.floatOffset) * _floatAmplitude * 0.3f
            );
            
            Vector3 movement = (particle.velocity * _driftSpeed + floatMotion) * Time.deltaTime;
            particle.gameObject.transform.position += movement;
            
            if (_mainCamera != null)
            {
                Vector3 dirToCamera = _mainCamera.transform.position - particle.gameObject.transform.position;
                particle.gameObject.transform.rotation = Quaternion.LookRotation(dirToCamera);
            }
            
            if (particle.gameObject.transform.position.y > transform.position.y + _respawnHeight ||
                particle.lifetime > particle.maxLifetime ||
                Vector3.Distance(particle.gameObject.transform.position, transform.position) > _spawnAreaSize.magnitude)
            {
                RespawnParticle(particle);
            }
        }
    }
    
    private void RespawnParticle(DustParticle particle)
    {
        Vector3 newPos = GetRandomSpawnPosition();
        newPos.y = transform.position.y - _spawnAreaSize.y * 0.5f;
        particle.gameObject.transform.position = newPos;
        particle.startPosition = newPos;
        particle.lifetime = 0f;
        particle.maxLifetime = Random.Range(10f, 30f);
        
        particle.velocity = new Vector3(
            Random.Range(-_driftVariation, _driftVariation),
            Random.Range(-_driftSpeed * 0.5f, _driftSpeed * 0.5f),
            Random.Range(-_driftVariation, _driftVariation)
        );
    }
    
    private void UpdateParticleVisibility()
    {
        if (_nearbyLights == null) return;
        
        foreach (DustParticle particle in _particles)
        {
            if (particle.gameObject == null) continue;
            
            float lightInfluence = CalculateLightInfluence(particle.gameObject.transform.position);
            float alpha = particle.baseAlpha * lightInfluence;
            
            Color currentColor = particle.renderer.material.color;
            currentColor.a = alpha;
            particle.renderer.material.color = currentColor;
            
            particle.renderer.enabled = alpha > 0.01f;
        }
    }
    
    private float CalculateLightInfluence(Vector3 position)
    {
        float totalInfluence = 0f;
        
        foreach (Light light in _nearbyLights)
        {
            if (light == null || !light.enabled) continue;
            
            float distance = Vector3.Distance(position, light.transform.position);
            float lightRange = light.range;
            
            if (distance > lightRange) continue;
            
            float influence = 1f - (distance / lightRange);
            influence = Mathf.Pow(influence, 2f);
            
            if (light.type == LightType.Spot)
            {
                Vector3 dirToParticle = (position - light.transform.position).normalized;
                float angle = Vector3.Angle(light.transform.forward, dirToParticle);
                float spotAngle = light.spotAngle * 0.5f;
                
                if (angle > spotAngle)
                {
                    influence *= Mathf.Max(0f, 1f - (angle - spotAngle) / _fadeDistance);
                }
            }
            
            influence *= light.intensity;
            totalInfluence += influence;
        }
        
        return Mathf.Clamp01(totalInfluence);
    }
    
    private void UpdateLightSources()
    {
        Collider[] lightColliders = Physics.OverlapSphere(transform.position, _lightDetectionRadius, _lightLayerMask);
        List<Light> lights = new List<Light>();
        
        foreach (Collider col in lightColliders)
        {
            Light light = col.GetComponent<Light>();
            if (light != null)
            {
                lights.Add(light);
            }
        }
        
        Light[] allLights = FindObjectsOfType<Light>();
        foreach (Light light in allLights)
        {
            if (Vector3.Distance(light.transform.position, transform.position) <= _lightDetectionRadius)
            {
                if (!lights.Contains(light))
                {
                    lights.Add(light);
                }
            }
        }
        
        _nearbyLights = lights.ToArray();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, _spawnAreaSize);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _lightDetectionRadius);
    }
    
    void OnDestroy()
    {
        foreach (DustParticle particle in _particles)
        {
            if (particle.gameObject != null)
            {
                DestroyImmediate(particle.gameObject);
            }
        }
    }
}