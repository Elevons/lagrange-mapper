// Prompt: waterfall with mist
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class Waterfall : MonoBehaviour
{
    [Header("Water Settings")]
    [SerializeField] private ParticleSystem _waterParticles;
    [SerializeField] private float _waterFlowRate = 100f;
    [SerializeField] private float _waterSpeed = 5f;
    [SerializeField] private Color _waterColor = Color.cyan;
    
    [Header("Mist Settings")]
    [SerializeField] private ParticleSystem _mistParticles;
    [SerializeField] private float _mistDensity = 50f;
    [SerializeField] private float _mistSpread = 2f;
    [SerializeField] private Color _mistColor = Color.white;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _waterfallSound;
    [SerializeField] private float _audioVolume = 0.7f;
    
    [Header("Physics")]
    [SerializeField] private BoxCollider _splashZone;
    [SerializeField] private float _splashForce = 10f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private Light _mistLight;
    [SerializeField] private float _lightIntensity = 0.5f;
    [SerializeField] private Color _lightColor = Color.white;
    [SerializeField] private AnimationCurve _intensityCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);
    
    private float _timeOffset;
    private Vector3 _originalLightPosition;
    private List<Rigidbody> _objectsInSplashZone = new List<Rigidbody>();
    
    void Start()
    {
        _timeOffset = Random.Range(0f, 100f);
        SetupWaterParticles();
        SetupMistParticles();
        SetupAudio();
        SetupSplashZone();
        SetupLighting();
    }
    
    void SetupWaterParticles()
    {
        if (_waterParticles == null)
        {
            GameObject waterGO = new GameObject("WaterParticles");
            waterGO.transform.SetParent(transform);
            waterGO.transform.localPosition = Vector3.zero;
            _waterParticles = waterGO.AddComponent<ParticleSystem>();
        }
        
        var main = _waterParticles.main;
        main.startLifetime = 3f;
        main.startSpeed = _waterSpeed;
        main.startSize = 0.1f;
        main.startColor = _waterColor;
        main.maxParticles = (int)_waterFlowRate;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = _waterParticles.emission;
        emission.rateOverTime = _waterFlowRate;
        
        var shape = _waterParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(1f, 0.1f, 0.5f);
        
        var velocityOverLifetime = _waterParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(-_waterSpeed);
        
        var collision = _waterParticles.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.dampen = 0.3f;
        collision.bounce = 0.1f;
    }
    
    void SetupMistParticles()
    {
        if (_mistParticles == null)
        {
            GameObject mistGO = new GameObject("MistParticles");
            mistGO.transform.SetParent(transform);
            mistGO.transform.localPosition = new Vector3(0f, -2f, 0f);
            _mistParticles = mistGO.AddComponent<ParticleSystem>();
        }
        
        var main = _mistParticles.main;
        main.startLifetime = 5f;
        main.startSpeed = 0.5f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 2f);
        main.startColor = _mistColor;
        main.maxParticles = (int)_mistDensity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = _mistParticles.emission;
        emission.rateOverTime = _mistDensity;
        
        var shape = _mistParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_mistSpread, 0.5f, _mistSpread);
        
        var velocityOverLifetime = _mistParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0.2f, 1f);
        
        var sizeOverLifetime = _mistParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.2f, 1f, 1f));
        
        var colorOverLifetime = _mistParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(_mistColor, 0f), new GradientColorKey(_mistColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.3f), new GradientAlphaKey(0f, 1f) }
        );
        colorOverLifetime.color = gradient;
    }
    
    void SetupAudio()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        
        if (_waterfallSound != null)
        {
            _audioSource.clip = _waterfallSound;
            _audioSource.loop = true;
            _audioSource.volume = _audioVolume;
            _audioSource.spatialBlend = 1f;
            _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
            _audioSource.maxDistance = 20f;
            _audioSource.Play();
        }
    }
    
    void SetupSplashZone()
    {
        if (_splashZone == null)
        {
            _splashZone = gameObject.AddComponent<BoxCollider>();
            _splashZone.isTrigger = true;
            _splashZone.size = new Vector3(2f, 1f, 2f);
            _splashZone.center = new Vector3(0f, -2.5f, 0f);
        }
    }
    
    void SetupLighting()
    {
        if (_mistLight == null)
        {
            GameObject lightGO = new GameObject("MistLight");
            lightGO.transform.SetParent(transform);
            lightGO.transform.localPosition = new Vector3(0f, -1f, 1f);
            _mistLight = lightGO.AddComponent<Light>();
        }
        
        _mistLight.type = LightType.Point;
        _mistLight.color = _lightColor;
        _mistLight.intensity = _lightIntensity;
        _mistLight.range = 5f;
        _mistLight.shadows = LightShadows.Soft;
        
        _originalLightPosition = _mistLight.transform.localPosition;
    }
    
    void Update()
    {
        UpdateLighting();
        UpdateParticleEffects();
        ApplySplashForces();
    }
    
    void UpdateLighting()
    {
        if (_mistLight != null)
        {
            float time = Time.time + _timeOffset;
            float intensity = _intensityCurve.Evaluate((Mathf.Sin(time * 0.5f) + 1f) * 0.5f);
            _mistLight.intensity = _lightIntensity * intensity;
            
            Vector3 offset = new Vector3(
                Mathf.Sin(time * 0.3f) * 0.2f,
                Mathf.Sin(time * 0.7f) * 0.1f,
                Mathf.Cos(time * 0.4f) * 0.15f
            );
            _mistLight.transform.localPosition = _originalLightPosition + offset;
        }
    }
    
    void UpdateParticleEffects()
    {
        if (_waterParticles != null)
        {
            var emission = _waterParticles.emission;
            emission.rateOverTime = _waterFlowRate;
            
            var main = _waterParticles.main;
            main.startSpeed = _waterSpeed;
            main.startColor = _waterColor;
        }
        
        if (_mistParticles != null)
        {
            var emission = _mistParticles.emission;
            emission.rateOverTime = _mistDensity;
            
            var main = _mistParticles.main;
            main.startColor = _mistColor;
            
            var shape = _mistParticles.shape;
            shape.scale = new Vector3(_mistSpread, 0.5f, _mistSpread);
        }
    }
    
    void ApplySplashForces()
    {
        for (int i = _objectsInSplashZone.Count - 1; i >= 0; i--)
        {
            if (_objectsInSplashZone[i] == null)
            {
                _objectsInSplashZone.RemoveAt(i);
                continue;
            }
            
            Rigidbody rb = _objectsInSplashZone[i];
            Vector3 splashDirection = Vector3.up + Random.insideUnitSphere * 0.3f;
            rb.AddForce(splashDirection * _splashForce * Time.deltaTime, ForceMode.Force);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & _affectedLayers) != 0)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !_objectsInSplashZone.Contains(rb))
            {
                _objectsInSplashZone.Add(rb);
                
                // Create splash effect
                CreateSplashEffect(other.transform.position);
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null && _objectsInSplashZone.Contains(rb))
        {
            _objectsInSplashZone.Remove(rb);
        }
    }
    
    void CreateSplashEffect(Vector3 position)
    {
        GameObject splashGO = new GameObject("SplashEffect");
        splashGO.transform.position = position;
        
        ParticleSystem splash = splashGO.AddComponent<ParticleSystem>();
        
        var main = splash.main;
        main.startLifetime = 1f;
        main.startSpeed = 3f;
        main.startSize = 0.2f;
        main.startColor = _waterColor;
        main.maxParticles = 20;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = splash.emission;
        emission.SetBursts(new ParticleSystem.Burst[] {
            new ParticleSystem.Burst(0f, 20)
        });
        
        var shape = splash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.5f;
        
        Destroy(splashGO, 2f);
    }
    
    void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateParticleEffects();
            
            if (_audioSource != null)
                _audioSource.volume = _audioVolume;
            
            if (_mistLight != null)
            {
                _mistLight.color = _lightColor;
                _mistLight.intensity = _lightIntensity;
            }
        }
    }
}