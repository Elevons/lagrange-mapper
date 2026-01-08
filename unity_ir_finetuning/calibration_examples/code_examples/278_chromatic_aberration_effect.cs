// Prompt: chromatic aberration effect
// Type: general

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable]
public class ChromaticAberrationSettings
{
    [Range(0f, 1f)]
    public float intensity = 0.1f;
    [Range(0f, 5f)]
    public float speed = 1f;
    public bool useDistanceFromCenter = true;
    public AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
}

public class ChromaticAberrationEffect : MonoBehaviour
{
    [Header("Chromatic Aberration Settings")]
    [SerializeField] private ChromaticAberrationSettings _settings = new ChromaticAberrationSettings();
    
    [Header("Animation")]
    [SerializeField] private bool _animateIntensity = false;
    [SerializeField] private float _animationSpeed = 1f;
    [SerializeField] private float _minIntensity = 0f;
    [SerializeField] private float _maxIntensity = 1f;
    
    [Header("Trigger Settings")]
    [SerializeField] private bool _triggerOnPlayerEnter = false;
    [SerializeField] private float _triggerDuration = 2f;
    [SerializeField] private AnimationCurve _triggerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Distance-Based Effect")]
    [SerializeField] private bool _useDistanceEffect = false;
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private float _maxDistance = 10f;
    [SerializeField] private AnimationCurve _distanceCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    
    private Volume _postProcessVolume;
    private ChromaticAberration _chromaticAberration;
    private float _baseIntensity;
    private float _currentTriggerTime;
    private bool _isTriggered;
    private Camera _mainCamera;
    
    private void Start()
    {
        InitializeComponents();
        SetupChromaticAberration();
        _baseIntensity = _settings.intensity;
        
        if (_targetTransform == null)
            _targetTransform = transform;
            
        if (_mainCamera == null)
            _mainCamera = Camera.main;
    }
    
    private void InitializeComponents()
    {
        _postProcessVolume = GetComponent<Volume>();
        
        if (_postProcessVolume == null)
        {
            _postProcessVolume = gameObject.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
        }
        
        if (_postProcessVolume.profile == null)
        {
            _postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }
    }
    
    private void SetupChromaticAberration()
    {
        if (!_postProcessVolume.profile.TryGet<ChromaticAberration>(out _chromaticAberration))
        {
            _chromaticAberration = _postProcessVolume.profile.Add<ChromaticAberration>(false);
        }
        
        _chromaticAberration.intensity.overrideState = true;
        _chromaticAberration.intensity.value = _settings.intensity;
        _chromaticAberration.active = true;
    }
    
    private void Update()
    {
        if (_chromaticAberration == null) return;
        
        float finalIntensity = CalculateFinalIntensity();
        _chromaticAberration.intensity.value = finalIntensity;
        
        UpdateTriggerEffect();
    }
    
    private float CalculateFinalIntensity()
    {
        float intensity = _baseIntensity;
        
        if (_animateIntensity)
        {
            float animatedValue = Mathf.Lerp(_minIntensity, _maxIntensity, 
                (Mathf.Sin(Time.time * _animationSpeed) + 1f) * 0.5f);
            intensity = animatedValue;
        }
        
        if (_useDistanceEffect && _mainCamera != null)
        {
            float distance = Vector3.Distance(_mainCamera.transform.position, _targetTransform.position);
            float normalizedDistance = Mathf.Clamp01(distance / _maxDistance);
            float distanceMultiplier = _distanceCurve.Evaluate(normalizedDistance);
            intensity *= distanceMultiplier;
        }
        
        if (_isTriggered)
        {
            float triggerMultiplier = _triggerCurve.Evaluate(_currentTriggerTime / _triggerDuration);
            intensity *= triggerMultiplier;
        }
        
        return Mathf.Clamp01(intensity);
    }
    
    private void UpdateTriggerEffect()
    {
        if (_isTriggered)
        {
            _currentTriggerTime += Time.deltaTime;
            
            if (_currentTriggerTime >= _triggerDuration)
            {
                _isTriggered = false;
                _currentTriggerTime = 0f;
            }
        }
    }
    
    public void TriggerEffect()
    {
        _isTriggered = true;
        _currentTriggerTime = 0f;
    }
    
    public void SetIntensity(float intensity)
    {
        _baseIntensity = Mathf.Clamp01(intensity);
        _settings.intensity = _baseIntensity;
    }
    
    public void SetAnimationEnabled(bool enabled)
    {
        _animateIntensity = enabled;
    }
    
    public void SetAnimationSpeed(float speed)
    {
        _animationSpeed = speed;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_triggerOnPlayerEnter && other.CompareTag("Player"))
        {
            TriggerEffect();
        }
    }
    
    private void OnValidate()
    {
        if (_settings != null)
        {
            _settings.intensity = Mathf.Clamp01(_settings.intensity);
            _settings.speed = Mathf.Max(0f, _settings.speed);
        }
        
        _minIntensity = Mathf.Clamp01(_minIntensity);
        _maxIntensity = Mathf.Clamp01(_maxIntensity);
        _triggerDuration = Mathf.Max(0.1f, _triggerDuration);
        _maxDistance = Mathf.Max(0.1f, _maxDistance);
    }
    
    private void OnDestroy()
    {
        if (_postProcessVolume != null && _postProcessVolume.profile != null)
        {
            if (_chromaticAberration != null)
            {
                _postProcessVolume.profile.Remove<ChromaticAberration>();
            }
        }
    }
}