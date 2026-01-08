// Prompt: invisibility toggle
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class InvisibilityToggle : MonoBehaviour
{
    [Header("Invisibility Settings")]
    [SerializeField] private KeyCode _toggleKey = KeyCode.I;
    [SerializeField] private float _invisibilityDuration = 5f;
    [SerializeField] private bool _hasLimitedDuration = true;
    [SerializeField] private float _cooldownTime = 3f;
    
    [Header("Visual Effects")]
    [SerializeField] private float _visibleAlpha = 1f;
    [SerializeField] private float _invisibleAlpha = 0.1f;
    [SerializeField] private float _fadeSpeed = 5f;
    [SerializeField] private bool _disableColliders = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _activateSound;
    [SerializeField] private AudioClip _deactivateSound;
    [SerializeField] private float _audioVolume = 1f;
    
    [Header("Events")]
    public UnityEvent OnInvisibilityActivated;
    public UnityEvent OnInvisibilityDeactivated;
    public UnityEvent OnCooldownStarted;
    public UnityEvent OnCooldownEnded;
    
    private bool _isInvisible = false;
    private bool _isOnCooldown = false;
    private float _invisibilityTimer = 0f;
    private float _cooldownTimer = 0f;
    private float _currentAlpha;
    
    private Renderer[] _renderers;
    private Collider[] _colliders;
    private AudioSource _audioSource;
    private Material[] _originalMaterials;
    private Material[] _transparentMaterials;
    
    private void Start()
    {
        InitializeComponents();
        SetupMaterials();
        _currentAlpha = _visibleAlpha;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateTimers();
        UpdateVisualEffects();
    }
    
    private void InitializeComponents()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _colliders = GetComponentsInChildren<Collider>();
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.volume = _audioVolume;
    }
    
    private void SetupMaterials()
    {
        if (_renderers == null || _renderers.Length == 0) return;
        
        _originalMaterials = new Material[_renderers.Length];
        _transparentMaterials = new Material[_renderers.Length];
        
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null)
            {
                _originalMaterials[i] = _renderers[i].material;
                _transparentMaterials[i] = new Material(_originalMaterials[i]);
                
                // Enable transparency
                _transparentMaterials[i].SetFloat("_Mode", 3);
                _transparentMaterials[i].SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _transparentMaterials[i].SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _transparentMaterials[i].SetInt("_ZWrite", 0);
                _transparentMaterials[i].DisableKeyword("_ALPHATEST_ON");
                _transparentMaterials[i].EnableKeyword("_ALPHABLEND_ON");
                _transparentMaterials[i].DisableKeyword("_ALPHAPREMULTIPLY_ON");
                _transparentMaterials[i].renderQueue = 3000;
            }
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_toggleKey) && !_isOnCooldown)
        {
            ToggleInvisibility();
        }
    }
    
    private void UpdateTimers()
    {
        if (_isInvisible && _hasLimitedDuration)
        {
            _invisibilityTimer -= Time.deltaTime;
            if (_invisibilityTimer <= 0f)
            {
                DeactivateInvisibility();
            }
        }
        
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                OnCooldownEnded?.Invoke();
            }
        }
    }
    
    private void UpdateVisualEffects()
    {
        float targetAlpha = _isInvisible ? _invisibleAlpha : _visibleAlpha;
        _currentAlpha = Mathf.Lerp(_currentAlpha, targetAlpha, _fadeSpeed * Time.deltaTime);
        
        ApplyAlphaToMaterials(_currentAlpha);
    }
    
    private void ApplyAlphaToMaterials(float alpha)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _transparentMaterials[i] != null)
            {
                Color color = _transparentMaterials[i].color;
                color.a = alpha;
                _transparentMaterials[i].color = color;
                
                if (_renderers[i].material != _transparentMaterials[i])
                {
                    _renderers[i].material = _transparentMaterials[i];
                }
            }
        }
    }
    
    public void ToggleInvisibility()
    {
        if (_isInvisible)
        {
            DeactivateInvisibility();
        }
        else
        {
            ActivateInvisibility();
        }
    }
    
    public void ActivateInvisibility()
    {
        if (_isOnCooldown) return;
        
        _isInvisible = true;
        
        if (_hasLimitedDuration)
        {
            _invisibilityTimer = _invisibilityDuration;
        }
        
        if (_disableColliders)
        {
            SetCollidersEnabled(false);
        }
        
        PlaySound(_activateSound);
        OnInvisibilityActivated?.Invoke();
    }
    
    public void DeactivateInvisibility()
    {
        _isInvisible = false;
        _invisibilityTimer = 0f;
        
        if (_disableColliders)
        {
            SetCollidersEnabled(true);
        }
        
        if (_cooldownTime > 0f)
        {
            StartCooldown();
        }
        
        PlaySound(_deactivateSound);
        OnInvisibilityDeactivated?.Invoke();
    }
    
    private void StartCooldown()
    {
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
        OnCooldownStarted?.Invoke();
    }
    
    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider col in _colliders)
        {
            if (col != null)
            {
                col.enabled = enabled;
            }
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip, _audioVolume);
        }
    }
    
    private void OnDestroy()
    {
        // Clean up transparent materials
        if (_transparentMaterials != null)
        {
            for (int i = 0; i < _transparentMaterials.Length; i++)
            {
                if (_transparentMaterials[i] != null)
                {
                    DestroyImmediate(_transparentMaterials[i]);
                }
            }
        }
        
        // Restore original materials
        if (_originalMaterials != null && _renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _originalMaterials[i] != null)
                {
                    _renderers[i].material = _originalMaterials[i];
                }
            }
        }
    }
    
    public bool IsInvisible => _isInvisible;
    public bool IsOnCooldown => _isOnCooldown;
    public float RemainingInvisibilityTime => _invisibilityTimer;
    public float RemainingCooldownTime => _cooldownTimer;
    public float InvisibilityProgress => _hasLimitedDuration ? (_invisibilityDuration - _invisibilityTimer) / _invisibilityDuration : 0f;
    public float CooldownProgress => _cooldownTime > 0f ? (_cooldownTime - _cooldownTimer) / _cooldownTime : 1f;
}