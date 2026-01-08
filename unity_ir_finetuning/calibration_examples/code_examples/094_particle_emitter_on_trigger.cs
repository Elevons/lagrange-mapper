// Prompt: particle emitter on trigger
// Type: general

using UnityEngine;

public class ParticleEmitterTrigger : MonoBehaviour
{
    [Header("Particle System")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private bool _createParticleSystemIfNull = true;
    
    [Header("Trigger Settings")]
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private bool _emitOnEnter = true;
    [SerializeField] private bool _emitOnExit = false;
    [SerializeField] private bool _emitOnStay = false;
    [SerializeField] private float _stayEmissionInterval = 0.5f;
    
    [Header("Emission Settings")]
    [SerializeField] private int _particlesToEmit = 50;
    [SerializeField] private bool _useCustomEmissionRate = false;
    [SerializeField] private float _customEmissionRate = 100f;
    [SerializeField] private float _emissionDuration = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _emissionSound;
    [SerializeField] private bool _playAudioOnEmit = false;
    
    private float _lastStayEmissionTime;
    private bool _isEmitting;
    private float _emissionStartTime;
    private float _originalEmissionRate;
    
    private void Start()
    {
        if (_particleSystem == null)
        {
            _particleSystem = GetComponent<ParticleSystem>();
            
            if (_particleSystem == null && _createParticleSystemIfNull)
            {
                _particleSystem = gameObject.AddComponent<ParticleSystem>();
                ConfigureDefaultParticleSystem();
            }
        }
        
        if (_particleSystem != null)
        {
            var emission = _particleSystem.emission;
            _originalEmissionRate = emission.rateOverTime.constant;
            emission.enabled = false;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        Collider triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (_isEmitting && _useCustomEmissionRate)
        {
            if (Time.time - _emissionStartTime >= _emissionDuration)
            {
                StopEmission();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_targetTag) && _emitOnEnter)
        {
            EmitParticles();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_targetTag) && _emitOnExit)
        {
            EmitParticles();
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag(_targetTag) && _emitOnStay)
        {
            if (Time.time - _lastStayEmissionTime >= _stayEmissionInterval)
            {
                EmitParticles();
                _lastStayEmissionTime = Time.time;
            }
        }
    }
    
    private void EmitParticles()
    {
        if (_particleSystem == null) return;
        
        if (_useCustomEmissionRate)
        {
            StartCustomEmission();
        }
        else
        {
            _particleSystem.Emit(_particlesToEmit);
        }
        
        PlayEmissionAudio();
    }
    
    private void StartCustomEmission()
    {
        var emission = _particleSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = _customEmissionRate;
        
        _isEmitting = true;
        _emissionStartTime = Time.time;
    }
    
    private void StopEmission()
    {
        if (_particleSystem == null) return;
        
        var emission = _particleSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = _originalEmissionRate;
        
        _isEmitting = false;
    }
    
    private void PlayEmissionAudio()
    {
        if (_playAudioOnEmit && _audioSource != null && _emissionSound != null)
        {
            _audioSource.PlayOneShot(_emissionSound);
        }
    }
    
    private void ConfigureDefaultParticleSystem()
    {
        if (_particleSystem == null) return;
        
        var main = _particleSystem.main;
        main.startLifetime = 2f;
        main.startSpeed = 5f;
        main.startSize = 0.1f;
        main.startColor = Color.white;
        main.maxParticles = 1000;
        
        var emission = _particleSystem.emission;
        emission.enabled = false;
        emission.rateOverTime = 50f;
        
        var shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 1f;
        
        var velocityOverLifetime = _particleSystem.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.radial = new ParticleSystem.MinMaxCurve(2f);
    }
    
    public void ForceEmit()
    {
        EmitParticles();
    }
    
    public void ForceEmit(int particleCount)
    {
        if (_particleSystem != null)
        {
            _particleSystem.Emit(particleCount);
            PlayEmissionAudio();
        }
    }
    
    public void SetTargetTag(string newTag)
    {
        _targetTag = newTag;
    }
    
    public void SetParticleCount(int count)
    {
        _particlesToEmit = Mathf.Max(0, count);
    }
    
    private void OnValidate()
    {
        _particlesToEmit = Mathf.Max(0, _particlesToEmit);
        _stayEmissionInterval = Mathf.Max(0.1f, _stayEmissionInterval);
        _emissionDuration = Mathf.Max(0.1f, _emissionDuration);
        _customEmissionRate = Mathf.Max(0f, _customEmissionRate);
    }
}