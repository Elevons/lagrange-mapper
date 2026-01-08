// Prompt: fire crackling with embers
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class FireCrackling : MonoBehaviour
{
    [Header("Fire Settings")]
    [SerializeField] private ParticleSystem _fireParticles;
    [SerializeField] private ParticleSystem _smokeParticles;
    [SerializeField] private ParticleSystem _emberParticles;
    [SerializeField] private Light _fireLight;
    [SerializeField] private AudioSource _crackleAudioSource;
    
    [Header("Crackling Audio")]
    [SerializeField] private AudioClip[] _crackleClips;
    [SerializeField] private float _minCrackleInterval = 1f;
    [SerializeField] private float _maxCrackleInterval = 4f;
    [SerializeField] private float _crackleVolumeMin = 0.3f;
    [SerializeField] private float _crackleVolumeMax = 0.8f;
    
    [Header("Fire Light Animation")]
    [SerializeField] private float _baseLightIntensity = 2f;
    [SerializeField] private float _lightFlickerAmount = 0.5f;
    [SerializeField] private float _lightFlickerSpeed = 8f;
    [SerializeField] private Color _baseLightColor = Color.yellow;
    [SerializeField] private Color _flickerLightColor = new Color(1f, 0.6f, 0.2f);
    
    [Header("Ember Settings")]
    [SerializeField] private int _maxEmbers = 20;
    [SerializeField] private float _emberSpawnRate = 2f;
    [SerializeField] private float _emberLifetime = 3f;
    [SerializeField] private Vector3 _emberSpawnArea = new Vector3(1f, 0.5f, 1f);
    [SerializeField] private float _emberUpwardForce = 2f;
    [SerializeField] private float _emberRandomForce = 1f;
    
    [Header("Fire Intensity")]
    [SerializeField] private float _fireIntensity = 1f;
    [SerializeField] private bool _isLit = true;
    
    private float _nextCrackleTime;
    private float _nextEmberSpawnTime;
    private List<EmberParticle> _activeEmbers = new List<EmberParticle>();
    private float _lightFlickerOffset;
    
    [System.Serializable]
    private class EmberParticle
    {
        public GameObject gameObject;
        public Rigidbody rigidbody;
        public Light light;
        public float lifetime;
        public float maxLifetime;
        public Color startColor;
        
        public EmberParticle(GameObject go, float life)
        {
            gameObject = go;
            rigidbody = go.GetComponent<Rigidbody>();
            light = go.GetComponent<Light>();
            maxLifetime = life;
            lifetime = life;
            startColor = light ? light.color : Color.red;
        }
    }
    
    private void Start()
    {
        InitializeComponents();
        _lightFlickerOffset = Random.Range(0f, 100f);
        _nextCrackleTime = Time.time + Random.Range(_minCrackleInterval, _maxCrackleInterval);
        _nextEmberSpawnTime = Time.time + (1f / _emberSpawnRate);
    }
    
    private void InitializeComponents()
    {
        if (_fireLight == null)
            _fireLight = GetComponent<Light>();
            
        if (_crackleAudioSource == null)
            _crackleAudioSource = GetComponent<AudioSource>();
            
        if (_fireParticles == null)
            _fireParticles = GetComponentInChildren<ParticleSystem>();
    }
    
    private void Update()
    {
        if (!_isLit) return;
        
        UpdateFireLight();
        HandleCrackling();
        HandleEmberSpawning();
        UpdateEmbers();
        UpdateParticleIntensity();
    }
    
    private void UpdateFireLight()
    {
        if (_fireLight == null) return;
        
        float flicker = Mathf.PerlinNoise(Time.time * _lightFlickerSpeed, _lightFlickerOffset);
        float intensity = _baseLightIntensity + (flicker - 0.5f) * _lightFlickerAmount;
        intensity *= _fireIntensity;
        
        _fireLight.intensity = Mathf.Max(0f, intensity);
        _fireLight.color = Color.Lerp(_baseLightColor, _flickerLightColor, flicker * 0.5f);
    }
    
    private void HandleCrackling()
    {
        if (Time.time >= _nextCrackleTime && _crackleClips.Length > 0 && _crackleAudioSource != null)
        {
            PlayCrackleSound();
            _nextCrackleTime = Time.time + Random.Range(_minCrackleInterval, _maxCrackleInterval);
        }
    }
    
    private void PlayCrackleSound()
    {
        AudioClip clip = _crackleClips[Random.Range(0, _crackleClips.Length)];
        float volume = Random.Range(_crackleVolumeMin, _crackleVolumeMax) * _fireIntensity;
        float pitch = Random.Range(0.8f, 1.2f);
        
        _crackleAudioSource.pitch = pitch;
        _crackleAudioSource.PlayOneShot(clip, volume);
    }
    
    private void HandleEmberSpawning()
    {
        if (Time.time >= _nextEmberSpawnTime && _activeEmbers.Count < _maxEmbers)
        {
            SpawnEmber();
            _nextEmberSpawnTime = Time.time + (1f / (_emberSpawnRate * _fireIntensity));
        }
    }
    
    private void SpawnEmber()
    {
        GameObject ember = CreateEmberGameObject();
        EmberParticle emberParticle = new EmberParticle(ember, _emberLifetime);
        
        Vector3 spawnPos = transform.position + new Vector3(
            Random.Range(-_emberSpawnArea.x * 0.5f, _emberSpawnArea.x * 0.5f),
            Random.Range(0f, _emberSpawnArea.y),
            Random.Range(-_emberSpawnArea.z * 0.5f, _emberSpawnArea.z * 0.5f)
        );
        
        ember.transform.position = spawnPos;
        
        Vector3 force = Vector3.up * _emberUpwardForce + Random.insideUnitSphere * _emberRandomForce;
        emberParticle.rigidbody.AddForce(force, ForceMode.Impulse);
        
        _activeEmbers.Add(emberParticle);
    }
    
    private GameObject CreateEmberGameObject()
    {
        GameObject ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        ember.name = "Ember";
        ember.transform.localScale = Vector3.one * Random.Range(0.02f, 0.05f);
        
        Destroy(ember.GetComponent<Collider>());
        
        Rigidbody rb = ember.AddComponent<Rigidbody>();
        rb.mass = 0.01f;
        rb.drag = 2f;
        rb.angularDrag = 5f;
        
        Light emberLight = ember.AddComponent<Light>();
        emberLight.type = LightType.Point;
        emberLight.color = new Color(1f, 0.4f, 0.1f);
        emberLight.intensity = Random.Range(0.5f, 1.5f);
        emberLight.range = Random.Range(0.5f, 1.5f);
        
        Renderer renderer = ember.GetComponent<Renderer>();
        Material mat = new Material(Shader.Find("Standard"));
        mat.SetColor("_Color", Color.red);
        mat.SetColor("_EmissionColor", Color.red * 2f);
        mat.EnableKeyword("_EMISSION");
        renderer.material = mat;
        
        return ember;
    }
    
    private void UpdateEmbers()
    {
        for (int i = _activeEmbers.Count - 1; i >= 0; i--)
        {
            EmberParticle ember = _activeEmbers[i];
            ember.lifetime -= Time.deltaTime;
            
            if (ember.lifetime <= 0f || ember.gameObject == null)
            {
                if (ember.gameObject != null)
                    Destroy(ember.gameObject);
                _activeEmbers.RemoveAt(i);
                continue;
            }
            
            float lifetimeRatio = ember.lifetime / ember.maxLifetime;
            
            if (ember.light != null)
            {
                ember.light.intensity *= lifetimeRatio;
                ember.light.color = Color.Lerp(Color.black, ember.startColor, lifetimeRatio);
            }
            
            if (ember.gameObject != null)
            {
                Renderer renderer = ember.gameObject.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    Color emberColor = Color.Lerp(Color.black, Color.red, lifetimeRatio);
                    renderer.material.SetColor("_Color", emberColor);
                    renderer.material.SetColor("_EmissionColor", emberColor * lifetimeRatio * 2f);
                }
            }
        }
    }
    
    private void UpdateParticleIntensity()
    {
        UpdateParticleSystem(_fireParticles, _fireIntensity);
        UpdateParticleSystem(_smokeParticles, _fireIntensity * 0.8f);
        UpdateParticleSystem(_emberParticles, _fireIntensity);
    }
    
    private void UpdateParticleSystem(ParticleSystem particles, float intensity)
    {
        if (particles == null) return;
        
        var emission = particles.emission;
        var main = particles.main;
        
        emission.rateOverTime = emission.rateOverTime.constant * intensity;
        main.startLifetime = main.startLifetime.constant * (0.5f + intensity * 0.5f);
    }
    
    public void SetFireIntensity(float intensity)
    {
        _fireIntensity = Mathf.Clamp01(intensity);
    }
    
    public void ExtinguishFire()
    {
        _isLit = false;
        _fireIntensity = 0f;
        
        if (_fireParticles != null) _fireParticles.Stop();
        if (_emberParticles != null) _emberParticles.Stop();
        if (_fireLight != null) _fireLight.intensity = 0f;
        
        foreach (var ember in _activeEmbers)
        {
            if (ember.gameObject != null)
                Destroy(ember.gameObject);
        }
        _activeEmbers.Clear();
    }
    
    public void LightFire()
    {
        _isLit = true;
        _fireIntensity = 1f;
        
        if (_fireParticles != null) _fireParticles.Play();
        if (_emberParticles != null) _emberParticles.Play();
        if (_smokeParticles != null) _smokeParticles.Play();
    }
    
    private void OnDestroy()
    {
        foreach (var ember in _activeEmbers)
        {
            if (ember.gameObject != null)
                Destroy(ember.gameObject);
        }
        _activeEmbers.Clear();
    }
}