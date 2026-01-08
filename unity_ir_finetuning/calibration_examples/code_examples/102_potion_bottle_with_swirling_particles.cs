// Prompt: potion bottle with swirling particles
// Type: general

using UnityEngine;
using System.Collections;

public class PotionBottle : MonoBehaviour
{
    [Header("Potion Settings")]
    [SerializeField] private Color _potionColor = Color.red;
    [SerializeField] private float _swirlingSpeed = 2f;
    [SerializeField] private float _swirlingRadius = 0.5f;
    [SerializeField] private int _particleCount = 20;
    
    [Header("Bottle Components")]
    [SerializeField] private Transform _liquidTransform;
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private Light _potionLight;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Effects")]
    [SerializeField] private float _glowIntensity = 1f;
    [SerializeField] private float _bobHeight = 0.1f;
    [SerializeField] private float _bobSpeed = 1f;
    [SerializeField] private AudioClip _bubbleSound;
    [SerializeField] private bool _playAmbientSound = true;
    
    private Vector3 _initialPosition;
    private float _timeOffset;
    private ParticleSystem.Particle[] _particles;
    private float[] _particleAngles;
    private float[] _particleHeights;
    private Renderer _liquidRenderer;
    
    private void Start()
    {
        _initialPosition = transform.position;
        _timeOffset = Random.Range(0f, 2f * Mathf.PI);
        
        InitializeParticleSystem();
        InitializeLiquid();
        InitializeLight();
        InitializeAudio();
    }
    
    private void InitializeParticleSystem()
    {
        if (_particleSystem == null)
        {
            GameObject particleObj = new GameObject("PotionParticles");
            particleObj.transform.SetParent(transform);
            particleObj.transform.localPosition = Vector3.zero;
            _particleSystem = particleObj.AddComponent<ParticleSystem>();
        }
        
        var main = _particleSystem.main;
        main.startLifetime = float.MaxValue;
        main.startSpeed = 0f;
        main.startSize = 0.05f;
        main.startColor = _potionColor;
        main.maxParticles = _particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        
        var emission = _particleSystem.emission;
        emission.enabled = false;
        
        var shape = _particleSystem.shape;
        shape.enabled = false;
        
        _particles = new ParticleSystem.Particle[_particleCount];
        _particleAngles = new float[_particleCount];
        _particleHeights = new float[_particleCount];
        
        for (int i = 0; i < _particleCount; i++)
        {
            _particleAngles[i] = (float)i / _particleCount * 2f * Mathf.PI;
            _particleHeights[i] = Random.Range(-0.8f, 0.8f);
            
            _particles[i].position = CalculateParticlePosition(i, 0f);
            _particles[i].startColor = Color.Lerp(_potionColor, Color.white, Random.Range(0f, 0.3f));
            _particles[i].startSize = Random.Range(0.03f, 0.08f);
        }
        
        _particleSystem.SetParticles(_particles, _particleCount);
    }
    
    private void InitializeLiquid()
    {
        if (_liquidTransform != null)
        {
            _liquidRenderer = _liquidTransform.GetComponent<Renderer>();
            if (_liquidRenderer != null)
            {
                _liquidRenderer.material.color = _potionColor;
                if (_liquidRenderer.material.HasProperty("_EmissionColor"))
                {
                    _liquidRenderer.material.SetColor("_EmissionColor", _potionColor * _glowIntensity);
                }
            }
        }
    }
    
    private void InitializeLight()
    {
        if (_potionLight == null)
        {
            GameObject lightObj = new GameObject("PotionLight");
            lightObj.transform.SetParent(transform);
            lightObj.transform.localPosition = Vector3.zero;
            _potionLight = lightObj.AddComponent<Light>();
        }
        
        _potionLight.type = LightType.Point;
        _potionLight.color = _potionColor;
        _potionLight.intensity = _glowIntensity;
        _potionLight.range = 3f;
    }
    
    private void InitializeAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _audioSource.clip = _bubbleSound;
        _audioSource.loop = true;
        _audioSource.volume = 0.3f;
        _audioSource.pitch = Random.Range(0.8f, 1.2f);
        
        if (_playAmbientSound && _bubbleSound != null)
        {
            _audioSource.Play();
        }
    }
    
    private void Update()
    {
        UpdateBottlePosition();
        UpdateParticles();
        UpdateLightEffect();
    }
    
    private void UpdateBottlePosition()
    {
        float bobOffset = Mathf.Sin(Time.time * _bobSpeed + _timeOffset) * _bobHeight;
        transform.position = _initialPosition + Vector3.up * bobOffset;
    }
    
    private void UpdateParticles()
    {
        if (_particleSystem == null || _particles == null) return;
        
        float time = Time.time * _swirlingSpeed;
        
        for (int i = 0; i < _particleCount; i++)
        {
            _particles[i].position = CalculateParticlePosition(i, time);
            
            // Add some random movement
            Vector3 randomOffset = new Vector3(
                Mathf.Sin(time * 2f + i) * 0.02f,
                Mathf.Cos(time * 1.5f + i) * 0.01f,
                Mathf.Sin(time * 1.8f + i) * 0.02f
            );
            
            _particles[i].position += randomOffset;
        }
        
        _particleSystem.SetParticles(_particles, _particleCount);
    }
    
    private Vector3 CalculateParticlePosition(int index, float time)
    {
        float angle = _particleAngles[index] + time;
        float radius = _swirlingRadius * (0.5f + 0.5f * Mathf.Sin(time * 0.5f + index));
        
        float x = Mathf.Cos(angle) * radius;
        float z = Mathf.Sin(angle) * radius;
        float y = _particleHeights[index] + Mathf.Sin(time + index) * 0.1f;
        
        return new Vector3(x, y, z);
    }
    
    private void UpdateLightEffect()
    {
        if (_potionLight != null)
        {
            float flicker = 1f + Mathf.Sin(Time.time * 3f + _timeOffset) * 0.1f;
            _potionLight.intensity = _glowIntensity * flicker;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(ConsumePotion());
        }
    }
    
    private IEnumerator ConsumePotion()
    {
        // Increase swirling speed
        float originalSpeed = _swirlingSpeed;
        _swirlingSpeed *= 3f;
        
        // Play consumption sound
        if (_audioSource != null && _bubbleSound != null)
        {
            _audioSource.pitch = 1.5f;
            _audioSource.volume = 0.6f;
        }
        
        // Shrink effect
        Vector3 originalScale = transform.localScale;
        float shrinkTime = 1f;
        float elapsed = 0f;
        
        while (elapsed < shrinkTime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / shrinkTime;
            
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            
            if (_potionLight != null)
            {
                _potionLight.intensity = Mathf.Lerp(_glowIntensity, 0f, progress);
            }
            
            yield return null;
        }
        
        gameObject.SetActive(false);
    }
    
    public void SetPotionColor(Color newColor)
    {
        _potionColor = newColor;
        
        if (_liquidRenderer != null)
        {
            _liquidRenderer.material.color = _potionColor;
            if (_liquidRenderer.material.HasProperty("_EmissionColor"))
            {
                _liquidRenderer.material.SetColor("_EmissionColor", _potionColor * _glowIntensity);
            }
        }
        
        if (_potionLight != null)
        {
            _potionLight.color = _potionColor;
        }
        
        if (_particles != null)
        {
            for (int i = 0; i < _particles.Length; i++)
            {
                _particles[i].startColor = Color.Lerp(_potionColor, Color.white, Random.Range(0f, 0.3f));
            }
            _particleSystem.SetParticles(_particles, _particleCount);
        }
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            SetPotionColor(_potionColor);
        }
    }
}