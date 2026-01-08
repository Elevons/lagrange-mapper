// Prompt: fog rolling in
// Type: general

using UnityEngine;
using UnityEngine.Rendering;

public class FogRoller : MonoBehaviour
{
    [Header("Fog Settings")]
    [SerializeField] private bool _enableFog = true;
    [SerializeField] private Color _fogColor = Color.gray;
    [SerializeField] private FogMode _fogMode = FogMode.ExponentialSquared;
    
    [Header("Rolling Animation")]
    [SerializeField] private float _rollDuration = 30f;
    [SerializeField] private AnimationCurve _rollCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool _rollOnStart = true;
    [SerializeField] private bool _loop = false;
    
    [Header("Fog Density")]
    [SerializeField] private float _startDensity = 0f;
    [SerializeField] private float _endDensity = 0.02f;
    
    [Header("Fog Distance (Linear Mode)")]
    [SerializeField] private float _startDistance = 300f;
    [SerializeField] private float _endDistance = 50f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _fogParticles;
    [SerializeField] private Light _ambientLight;
    [SerializeField] private Color _startLightColor = Color.white;
    [SerializeField] private Color _endLightColor = Color.gray;
    [SerializeField] private float _startLightIntensity = 1f;
    [SerializeField] private float _endLightIntensity = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fogRollingSound;
    [SerializeField] private float _audioFadeInDuration = 5f;
    
    private bool _isRolling = false;
    private float _rollTimer = 0f;
    private bool _originalFogEnabled;
    private Color _originalFogColor;
    private FogMode _originalFogMode;
    private float _originalFogDensity;
    private float _originalFogStartDistance;
    private float _originalFogEndDistance;
    private Color _originalLightColor;
    private float _originalLightIntensity;
    private float _targetAudioVolume;
    
    private void Start()
    {
        StoreOriginalSettings();
        
        if (_audioSource != null)
        {
            _targetAudioVolume = _audioSource.volume;
            _audioSource.volume = 0f;
        }
        
        if (_rollOnStart)
        {
            StartFogRoll();
        }
    }
    
    private void Update()
    {
        if (_isRolling)
        {
            UpdateFogRoll();
        }
    }
    
    private void StoreOriginalSettings()
    {
        _originalFogEnabled = RenderSettings.fog;
        _originalFogColor = RenderSettings.fogColor;
        _originalFogMode = RenderSettings.fogMode;
        _originalFogDensity = RenderSettings.fogDensity;
        _originalFogStartDistance = RenderSettings.fogStartDistance;
        _originalFogEndDistance = RenderSettings.fogEndDistance;
        
        if (_ambientLight != null)
        {
            _originalLightColor = _ambientLight.color;
            _originalLightIntensity = _ambientLight.intensity;
        }
    }
    
    public void StartFogRoll()
    {
        if (_isRolling) return;
        
        _isRolling = true;
        _rollTimer = 0f;
        
        RenderSettings.fog = _enableFog;
        RenderSettings.fogMode = _fogMode;
        RenderSettings.fogColor = _fogColor;
        
        if (_fogParticles != null && !_fogParticles.isPlaying)
        {
            _fogParticles.Play();
        }
        
        if (_audioSource != null && _fogRollingSound != null)
        {
            _audioSource.clip = _fogRollingSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    public void StopFogRoll()
    {
        if (!_isRolling) return;
        
        _isRolling = false;
        RestoreOriginalSettings();
        
        if (_fogParticles != null && _fogParticles.isPlaying)
        {
            _fogParticles.Stop();
        }
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    private void UpdateFogRoll()
    {
        _rollTimer += Time.deltaTime;
        float normalizedTime = Mathf.Clamp01(_rollTimer / _rollDuration);
        float curveValue = _rollCurve.Evaluate(normalizedTime);
        
        // Update fog density
        if (_fogMode == FogMode.Exponential || _fogMode == FogMode.ExponentialSquared)
        {
            RenderSettings.fogDensity = Mathf.Lerp(_startDensity, _endDensity, curveValue);
        }
        else if (_fogMode == FogMode.Linear)
        {
            RenderSettings.fogStartDistance = Mathf.Lerp(_startDistance, _endDistance, curveValue);
            RenderSettings.fogEndDistance = RenderSettings.fogStartDistance * 2f;
        }
        
        // Update ambient light
        if (_ambientLight != null)
        {
            _ambientLight.color = Color.Lerp(_startLightColor, _endLightColor, curveValue);
            _ambientLight.intensity = Mathf.Lerp(_startLightIntensity, _endLightIntensity, curveValue);
        }
        
        // Update audio volume
        if (_audioSource != null && _audioSource.isPlaying)
        {
            float audioFadeProgress = Mathf.Clamp01(_rollTimer / _audioFadeInDuration);
            _audioSource.volume = Mathf.Lerp(0f, _targetAudioVolume, audioFadeProgress);
        }
        
        // Check if roll is complete
        if (normalizedTime >= 1f)
        {
            if (_loop)
            {
                _rollTimer = 0f;
            }
            else
            {
                _isRolling = false;
                
                if (_audioSource != null && _audioSource.isPlaying)
                {
                    _audioSource.Stop();
                }
            }
        }
    }
    
    private void RestoreOriginalSettings()
    {
        RenderSettings.fog = _originalFogEnabled;
        RenderSettings.fogColor = _originalFogColor;
        RenderSettings.fogMode = _originalFogMode;
        RenderSettings.fogDensity = _originalFogDensity;
        RenderSettings.fogStartDistance = _originalFogStartDistance;
        RenderSettings.fogEndDistance = _originalFogEndDistance;
        
        if (_ambientLight != null)
        {
            _ambientLight.color = _originalLightColor;
            _ambientLight.intensity = _originalLightIntensity;
        }
    }
    
    public void ToggleFogRoll()
    {
        if (_isRolling)
        {
            StopFogRoll();
        }
        else
        {
            StartFogRoll();
        }
    }
    
    public bool IsRolling => _isRolling;
    
    public float RollProgress => _isRolling ? Mathf.Clamp01(_rollTimer / _rollDuration) : 0f;
    
    private void OnDestroy()
    {
        if (_isRolling)
        {
            RestoreOriginalSettings();
        }
    }
    
    private void OnValidate()
    {
        _rollDuration = Mathf.Max(0.1f, _rollDuration);
        _startDensity = Mathf.Max(0f, _startDensity);
        _endDensity = Mathf.Max(0f, _endDensity);
        _startDistance = Mathf.Max(0.1f, _startDistance);
        _endDistance = Mathf.Max(0.1f, _endDistance);
        _audioFadeInDuration = Mathf.Max(0.1f, _audioFadeInDuration);
        _startLightIntensity = Mathf.Max(0f, _startLightIntensity);
        _endLightIntensity = Mathf.Max(0f, _endLightIntensity);
    }
}