// Prompt: time slow zone
// Type: general

using UnityEngine;
using System.Collections;

public class TimeSlowZone : MonoBehaviour
{
    [Header("Time Control")]
    [SerializeField] private float _slowTimeScale = 0.3f;
    [SerializeField] private float _transitionDuration = 0.5f;
    [SerializeField] private bool _affectFixedTimeScale = true;
    
    [Header("Zone Settings")]
    [SerializeField] private bool _triggerOnEnter = true;
    [SerializeField] private bool _resetOnExit = true;
    [SerializeField] private string _targetTag = "Player";
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _slowEffectParticles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _enterSound;
    [SerializeField] private AudioClip _exitSound;
    
    [Header("Zone Indicator")]
    [SerializeField] private GameObject _zoneVisual;
    [SerializeField] private Color _zoneColor = Color.cyan;
    [SerializeField] private bool _pulseEffect = true;
    [SerializeField] private float _pulseSpeed = 2f;
    
    private float _originalTimeScale;
    private float _originalFixedTimeScale;
    private bool _isSlowActive = false;
    private Coroutine _transitionCoroutine;
    private Renderer _zoneRenderer;
    private Material _zoneMaterial;
    private Color _originalZoneColor;
    
    private void Start()
    {
        _originalTimeScale = Time.timeScale;
        _originalFixedTimeScale = Time.fixedDeltaTime;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetupZoneVisual();
        
        if (_slowEffectParticles != null)
        {
            _slowEffectParticles.Stop();
        }
    }
    
    private void SetupZoneVisual()
    {
        if (_zoneVisual != null)
        {
            _zoneRenderer = _zoneVisual.GetComponent<Renderer>();
            if (_zoneRenderer != null)
            {
                _zoneMaterial = _zoneRenderer.material;
                _originalZoneColor = _zoneMaterial.color;
                _zoneMaterial.color = _zoneColor;
            }
        }
        else
        {
            _zoneRenderer = GetComponent<Renderer>();
            if (_zoneRenderer != null)
            {
                _zoneMaterial = _zoneRenderer.material;
                _originalZoneColor = _zoneMaterial.color;
                _zoneMaterial.color = _zoneColor;
            }
        }
    }
    
    private void Update()
    {
        if (_pulseEffect && _zoneMaterial != null)
        {
            float alpha = Mathf.Lerp(0.3f, 0.8f, (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f);
            Color currentColor = _zoneMaterial.color;
            currentColor.a = alpha;
            _zoneMaterial.color = currentColor;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_triggerOnEnter) return;
        
        if (string.IsNullOrEmpty(_targetTag) || other.CompareTag(_targetTag))
        {
            ActivateSlowTime();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!_resetOnExit) return;
        
        if (string.IsNullOrEmpty(_targetTag) || other.CompareTag(_targetTag))
        {
            DeactivateSlowTime();
        }
    }
    
    public void ActivateSlowTime()
    {
        if (_isSlowActive) return;
        
        _isSlowActive = true;
        
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
            
        _transitionCoroutine = StartCoroutine(TransitionTimeScale(_slowTimeScale));
        
        if (_slowEffectParticles != null)
            _slowEffectParticles.Play();
            
        PlaySound(_enterSound);
    }
    
    public void DeactivateSlowTime()
    {
        if (!_isSlowActive) return;
        
        _isSlowActive = false;
        
        if (_transitionCoroutine != null)
            StopCoroutine(_transitionCoroutine);
            
        _transitionCoroutine = StartCoroutine(TransitionTimeScale(_originalTimeScale));
        
        if (_slowEffectParticles != null)
            _slowEffectParticles.Stop();
            
        PlaySound(_exitSound);
    }
    
    private IEnumerator TransitionTimeScale(float targetScale)
    {
        float startTimeScale = Time.timeScale;
        float startFixedTimeScale = Time.fixedDeltaTime;
        float targetFixedTimeScale = _originalFixedTimeScale * targetScale;
        
        float elapsed = 0f;
        
        while (elapsed < _transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / _transitionDuration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            Time.timeScale = Mathf.Lerp(startTimeScale, targetScale, t);
            
            if (_affectFixedTimeScale)
            {
                Time.fixedDeltaTime = Mathf.Lerp(startFixedTimeScale, targetFixedTimeScale, t);
            }
            
            yield return null;
        }
        
        Time.timeScale = targetScale;
        if (_affectFixedTimeScale)
        {
            Time.fixedDeltaTime = targetFixedTimeScale;
        }
        
        _transitionCoroutine = null;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.pitch = Time.timeScale;
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void ToggleSlowTime()
    {
        if (_isSlowActive)
            DeactivateSlowTime();
        else
            ActivateSlowTime();
    }
    
    public void SetSlowTimeScale(float newScale)
    {
        _slowTimeScale = Mathf.Clamp(newScale, 0.01f, 1f);
        
        if (_isSlowActive)
        {
            if (_transitionCoroutine != null)
                StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = StartCoroutine(TransitionTimeScale(_slowTimeScale));
        }
    }
    
    private void OnDisable()
    {
        if (_isSlowActive)
        {
            Time.timeScale = _originalTimeScale;
            if (_affectFixedTimeScale)
            {
                Time.fixedDeltaTime = _originalFixedTimeScale;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (_zoneMaterial != null && _originalZoneColor != Color.clear)
        {
            _zoneMaterial.color = _originalZoneColor;
        }
    }
}