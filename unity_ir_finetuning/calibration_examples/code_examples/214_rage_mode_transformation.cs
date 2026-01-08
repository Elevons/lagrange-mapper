// Prompt: rage mode transformation
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class RageModeTransformation : MonoBehaviour
{
    [Header("Rage Mode Settings")]
    [SerializeField] private float _rageDuration = 10f;
    [SerializeField] private float _rageActivationThreshold = 0.3f;
    [SerializeField] private bool _autoActivateOnLowHealth = true;
    [SerializeField] private KeyCode _manualActivationKey = KeyCode.R;
    
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth = 100f;
    
    [Header("Transformation Effects")]
    [SerializeField] private float _damageMultiplier = 2f;
    [SerializeField] private float _speedMultiplier = 1.5f;
    [SerializeField] private float _sizeMultiplier = 1.2f;
    [SerializeField] private Color _rageColor = Color.red;
    [SerializeField] private float _colorIntensity = 2f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _rageParticles;
    [SerializeField] private GameObject _rageAura;
    [SerializeField] private Light _rageLight;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _transformationSound;
    [SerializeField] private AudioClip _rageLoopSound;
    
    [Header("Screen Effects")]
    [SerializeField] private bool _enableScreenShake = true;
    [SerializeField] private float _shakeIntensity = 0.5f;
    [SerializeField] private float _shakeDuration = 1f;
    
    [Header("Events")]
    public UnityEvent OnRageModeActivated;
    public UnityEvent OnRageModeDeactivated;
    public UnityEvent<float> OnHealthChanged;
    
    private bool _isInRageMode = false;
    private float _rageTimer = 0f;
    private Vector3 _originalScale;
    private Color _originalColor;
    private Renderer _renderer;
    private Material _originalMaterial;
    private Material _rageMaterial;
    private Camera _mainCamera;
    private Vector3 _originalCameraPosition;
    private Coroutine _shakeCoroutine;
    
    // Cached components
    private Rigidbody _rigidbody;
    private Collider _collider;
    
    // Original values for restoration
    private float _originalMass;
    private float _originalDrag;
    
    private void Start()
    {
        InitializeComponents();
        SetupMaterials();
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }
    
    private void InitializeComponents()
    {
        _renderer = GetComponent<Renderer>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _mainCamera = Camera.main;
        
        _originalScale = transform.localScale;
        
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }
        
        if (_rigidbody != null)
        {
            _originalMass = _rigidbody.mass;
            _originalDrag = _rigidbody.drag;
        }
        
        if (_mainCamera != null)
        {
            _originalCameraPosition = _mainCamera.transform.localPosition;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }
    
    private void SetupMaterials()
    {
        if (_renderer != null)
        {
            _originalMaterial = _renderer.material;
            _rageMaterial = new Material(_originalMaterial);
            _rageMaterial.color = _rageColor;
            _rageMaterial.EnableKeyword("_EMISSION");
            _rageMaterial.SetColor("_EmissionColor", _rageColor * _colorIntensity);
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateRageMode();
        CheckAutoActivation();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_manualActivationKey) && !_isInRageMode)
        {
            ActivateRageMode();
        }
    }
    
    private void UpdateRageMode()
    {
        if (_isInRageMode)
        {
            _rageTimer -= Time.deltaTime;
            
            if (_rageTimer <= 0f)
            {
                DeactivateRageMode();
            }
        }
    }
    
    private void CheckAutoActivation()
    {
        if (_autoActivateOnLowHealth && !_isInRageMode)
        {
            float healthPercentage = _currentHealth / _maxHealth;
            if (healthPercentage <= _rageActivationThreshold)
            {
                ActivateRageMode();
            }
        }
    }
    
    public void ActivateRageMode()
    {
        if (_isInRageMode) return;
        
        _isInRageMode = true;
        _rageTimer = _rageDuration;
        
        ApplyTransformationEffects();
        PlayTransformationEffects();
        
        OnRageModeActivated?.Invoke();
    }
    
    public void DeactivateRageMode()
    {
        if (!_isInRageMode) return;
        
        _isInRageMode = false;
        _rageTimer = 0f;
        
        RemoveTransformationEffects();
        StopTransformationEffects();
        
        OnRageModeDeactivated?.Invoke();
    }
    
    private void ApplyTransformationEffects()
    {
        // Scale transformation
        transform.localScale = _originalScale * _sizeMultiplier;
        
        // Material transformation
        if (_renderer != null && _rageMaterial != null)
        {
            _renderer.material = _rageMaterial;
        }
        
        // Physics modifications
        if (_rigidbody != null)
        {
            _rigidbody.mass = _originalMass * _damageMultiplier;
            _rigidbody.drag = _originalDrag * 0.5f;
        }
        
        // Light effects
        if (_rageLight != null)
        {
            _rageLight.enabled = true;
            _rageLight.color = _rageColor;
            _rageLight.intensity = _colorIntensity;
        }
    }
    
    private void RemoveTransformationEffects()
    {
        // Restore scale
        transform.localScale = _originalScale;
        
        // Restore material
        if (_renderer != null && _originalMaterial != null)
        {
            _renderer.material = _originalMaterial;
        }
        
        // Restore physics
        if (_rigidbody != null)
        {
            _rigidbody.mass = _originalMass;
            _rigidbody.drag = _originalDrag;
        }
        
        // Disable light
        if (_rageLight != null)
        {
            _rageLight.enabled = false;
        }
    }
    
    private void PlayTransformationEffects()
    {
        // Particle effects
        if (_rageParticles != null)
        {
            _rageParticles.Play();
        }
        
        // Aura effects
        if (_rageAura != null)
        {
            _rageAura.SetActive(true);
        }
        
        // Sound effects
        if (_audioSource != null)
        {
            if (_transformationSound != null)
            {
                _audioSource.PlayOneShot(_transformationSound);
            }
            
            if (_rageLoopSound != null)
            {
                _audioSource.clip = _rageLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        
        // Screen shake
        if (_enableScreenShake && _mainCamera != null)
        {
            if (_shakeCoroutine != null)
            {
                StopCoroutine(_shakeCoroutine);
            }
            _shakeCoroutine = StartCoroutine(ScreenShake());
        }
    }
    
    private void StopTransformationEffects()
    {
        // Stop particles
        if (_rageParticles != null)
        {
            _rageParticles.Stop();
        }
        
        // Disable aura
        if (_rageAura != null)
        {
            _rageAura.SetActive(false);
        }
        
        // Stop sounds
        if (_audioSource != null)
        {
            _audioSource.Stop();
            _audioSource.loop = false;
        }
        
        // Stop screen shake
        if (_shakeCoroutine != null)
        {
            StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = null;
            
            if (_mainCamera != null)
            {
                _mainCamera.transform.localPosition = _originalCameraPosition;
            }
        }
    }
    
    private IEnumerator ScreenShake()
    {
        float elapsed = 0f;
        
        while (elapsed < _shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * _shakeIntensity;
            float y = Random.Range(-1f, 1f) * _shakeIntensity;
            
            _mainCamera.transform.localPosition = _originalCameraPosition + new Vector3(x, y, 0f);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _mainCamera.transform.localPosition = _originalCameraPosition;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }
    
    public void Heal(float healAmount)
    {
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healAmount);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }
    
    public float GetDamageMultiplier()
    {
        return _isInRageMode ? _damageMultiplier : 1f;
    }
    
    public float GetSpeedMultiplier()
    {
        return _isInRageMode ? _speedMultiplier : 1f;
    }
    
    public bool IsInRageMode()
    {
        return _isInRageMode;
    }
    
    public float GetRageTimeRemaining()
    {
        return _rageTimer;
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
    
    private void OnDestroy()
    {
        if (_rageMaterial != null)
        {
            DestroyImmediate(_rageMaterial);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isInRageMode && other.CompareTag("Enemy"))
        {
            // Apply rage damage to enemies
            var enemyHealth = other.GetComponent<RageModeTransformation>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(10f * _damageMultiplier);
            }
        }
    }
}