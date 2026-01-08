// Prompt: object that checks if current time is between 2 AM and 4 AM, if true makes object glow, play spooky sounds, and move erratically - also checks if exactly 7 other Rigidbody objects exist in scene
// Type: movement

using UnityEngine;
using System;
using System.Collections;

public class SpookyTimeObject : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField] private int _startHour = 2;
    [SerializeField] private int _endHour = 4;
    [SerializeField] private bool _useSystemTime = true;
    [SerializeField] private float _simulatedTimeSpeed = 1f;
    
    [Header("Glow Effect")]
    [SerializeField] private Material _glowMaterial;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Color _glowColor = Color.red;
    [SerializeField] private float _glowIntensity = 2f;
    [SerializeField] private float _glowPulseSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] _spookySounds;
    [SerializeField] private float _soundInterval = 3f;
    [SerializeField] private float _soundVolume = 0.5f;
    
    [Header("Movement")]
    [SerializeField] private float _movementIntensity = 2f;
    [SerializeField] private float _movementSpeed = 1f;
    [SerializeField] private Vector3 _movementRange = new Vector3(5f, 2f, 5f);
    
    [Header("Rigidbody Check")]
    [SerializeField] private int _requiredRigidbodyCount = 7;
    [SerializeField] private float _rigidbodyCheckInterval = 1f;
    
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Vector3 _originalPosition;
    private bool _isSpookyTime = false;
    private bool _hasCorrectRigidbodyCount = false;
    private bool _isActive = false;
    private float _simulatedTime = 0f;
    private Coroutine _movementCoroutine;
    private Coroutine _soundCoroutine;
    private Coroutine _rigidbodyCheckCoroutine;
    private Material _originalMaterial;
    
    void Start()
    {
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.volume = _soundVolume;
        _audioSource.playOnAwake = false;
        
        _originalPosition = transform.position;
        
        if (_renderer != null && _normalMaterial != null)
        {
            _originalMaterial = _renderer.material;
        }
        
        _simulatedTime = (float)(DateTime.Now.Hour * 3600 + DateTime.Now.Minute * 60 + DateTime.Now.Second);
        
        StartCoroutine(TimeCheckCoroutine());
        _rigidbodyCheckCoroutine = StartCoroutine(RigidbodyCheckCoroutine());
    }
    
    void Update()
    {
        if (!_useSystemTime)
        {
            _simulatedTime += Time.deltaTime * _simulatedTimeSpeed * 3600f;
            if (_simulatedTime >= 86400f)
            {
                _simulatedTime -= 86400f;
            }
        }
        
        UpdateGlowEffect();
    }
    
    IEnumerator TimeCheckCoroutine()
    {
        while (true)
        {
            CheckSpookyTime();
            yield return new WaitForSeconds(1f);
        }
    }
    
    IEnumerator RigidbodyCheckCoroutine()
    {
        while (true)
        {
            CheckRigidbodyCount();
            yield return new WaitForSeconds(_rigidbodyCheckInterval);
        }
    }
    
    void CheckSpookyTime()
    {
        int currentHour;
        
        if (_useSystemTime)
        {
            currentHour = DateTime.Now.Hour;
        }
        else
        {
            currentHour = Mathf.FloorToInt(_simulatedTime / 3600f) % 24;
        }
        
        bool wasSpookyTime = _isSpookyTime;
        _isSpookyTime = (currentHour >= _startHour && currentHour < _endHour);
        
        if (_isSpookyTime != wasSpookyTime)
        {
            UpdateActiveState();
        }
    }
    
    void CheckRigidbodyCount()
    {
        Rigidbody[] allRigidbodies = FindObjectsOfType<Rigidbody>();
        int count = 0;
        
        foreach (Rigidbody rb in allRigidbodies)
        {
            if (rb.gameObject != gameObject)
            {
                count++;
            }
        }
        
        bool wasCorrect = _hasCorrectRigidbodyCount;
        _hasCorrectRigidbodyCount = (count == _requiredRigidbodyCount);
        
        if (_hasCorrectRigidbodyCount != wasCorrect)
        {
            UpdateActiveState();
        }
    }
    
    void UpdateActiveState()
    {
        bool shouldBeActive = _isSpookyTime && _hasCorrectRigidbodyCount;
        
        if (shouldBeActive && !_isActive)
        {
            ActivateSpookyBehavior();
        }
        else if (!shouldBeActive && _isActive)
        {
            DeactivateSpookyBehavior();
        }
        
        _isActive = shouldBeActive;
    }
    
    void ActivateSpookyBehavior()
    {
        if (_movementCoroutine != null)
        {
            StopCoroutine(_movementCoroutine);
        }
        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
        }
        
        _movementCoroutine = StartCoroutine(ErraticMovementCoroutine());
        _soundCoroutine = StartCoroutine(SpookySoundCoroutine());
        
        SetGlowMaterial(true);
    }
    
    void DeactivateSpookyBehavior()
    {
        if (_movementCoroutine != null)
        {
            StopCoroutine(_movementCoroutine);
            _movementCoroutine = null;
        }
        
        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
            _soundCoroutine = null;
        }
        
        if (_audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        transform.position = _originalPosition;
        SetGlowMaterial(false);
    }
    
    void SetGlowMaterial(bool useGlow)
    {
        if (_renderer == null) return;
        
        if (useGlow && _glowMaterial != null)
        {
            _renderer.material = _glowMaterial;
        }
        else if (!useGlow)
        {
            if (_normalMaterial != null)
            {
                _renderer.material = _normalMaterial;
            }
            else if (_originalMaterial != null)
            {
                _renderer.material = _originalMaterial;
            }
        }
    }
    
    void UpdateGlowEffect()
    {
        if (!_isActive || _renderer == null) return;
        
        float pulse = Mathf.Sin(Time.time * _glowPulseSpeed) * 0.5f + 0.5f;
        Color currentColor = _glowColor * (_glowIntensity * pulse);
        
        if (_renderer.material.HasProperty("_EmissionColor"))
        {
            _renderer.material.SetColor("_EmissionColor", currentColor);
        }
        else if (_renderer.material.HasProperty("_Color"))
        {
            _renderer.material.color = Color.Lerp(Color.white, _glowColor, pulse);
        }
    }
    
    IEnumerator ErraticMovementCoroutine()
    {
        while (_isActive)
        {
            Vector3 randomOffset = new Vector3(
                UnityEngine.Random.Range(-_movementRange.x, _movementRange.x),
                UnityEngine.Random.Range(-_movementRange.y, _movementRange.y),
                UnityEngine.Random.Range(-_movementRange.z, _movementRange.z)
            ) * _movementIntensity;
            
            Vector3 targetPosition = _originalPosition + randomOffset;
            
            float duration = UnityEngine.Random.Range(0.5f, 2f) / _movementSpeed;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            
            while (elapsed < duration && _isActive)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                t = Mathf.SmoothStep(0f, 1f, t);
                
                transform.position = Vector3.Lerp(startPos, targetPosition, t);
                yield return null;
            }
            
            yield return new WaitForSeconds(UnityEngine.Random.Range(0.1f, 0.5f));
        }
    }
    
    IEnumerator SpookySoundCoroutine()
    {
        while (_isActive)
        {
            if (_spookySounds != null && _spookySounds.Length > 0)
            {
                AudioClip randomClip = _spookySounds[UnityEngine.Random.Range(0, _spookySounds.Length)];
                if (randomClip != null)
                {
                    _audioSource.clip = randomClip;
                    _audioSource.Play();
                }
            }
            
            yield return new WaitForSeconds(_soundInterval + UnityEngine.Random.Range(-1f, 1f));
        }
    }
    
    void OnDestroy()
    {
        if (_movementCoroutine != null)
        {
            StopCoroutine(_movementCoroutine);
        }
        if (_soundCoroutine != null)
        {
            StopCoroutine(_soundCoroutine);
        }
        if (_rigidbodyCheckCoroutine != null)
        {
            StopCoroutine(_rigidbodyCheckCoroutine);
        }
    }
}