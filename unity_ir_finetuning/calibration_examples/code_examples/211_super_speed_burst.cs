// Prompt: super speed burst
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SuperSpeedBurst : MonoBehaviour
{
    [Header("Speed Settings")]
    [SerializeField] private float _speedMultiplier = 3f;
    [SerializeField] private float _burstDuration = 2f;
    [SerializeField] private float _cooldownTime = 5f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _activationKey = KeyCode.LeftShift;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _speedTrailEffect;
    [SerializeField] private GameObject _speedAura;
    [SerializeField] private Color _speedTintColor = Color.cyan;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    
    [Header("Events")]
    public UnityEvent OnSpeedBurstActivated;
    public UnityEvent OnSpeedBurstDeactivated;
    public UnityEvent OnCooldownComplete;
    
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private float _originalSpeed;
    private bool _isSpeedActive = false;
    private bool _isOnCooldown = false;
    private float _burstTimer = 0f;
    private float _cooldownTimer = 0f;
    private Renderer _renderer;
    private Color _originalColor;
    private Camera _playerCamera;
    private float _originalFOV;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        _renderer = GetComponent<Renderer>();
        _playerCamera = Camera.main;
        
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_renderer != null)
            _originalColor = _renderer.material.color;
        
        if (_playerCamera != null)
            _originalFOV = _playerCamera.fieldOfView;
        
        if (_speedAura != null)
            _speedAura.SetActive(false);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateTimers();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_activationKey) && !_isSpeedActive && !_isOnCooldown)
        {
            ActivateSpeedBurst();
        }
    }
    
    private void UpdateTimers()
    {
        if (_isSpeedActive)
        {
            _burstTimer -= Time.deltaTime;
            if (_burstTimer <= 0f)
            {
                DeactivateSpeedBurst();
            }
        }
        
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                OnCooldownComplete?.Invoke();
            }
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_isSpeedActive && _renderer != null)
        {
            float lerpValue = Mathf.PingPong(Time.time * 2f, 1f);
            _renderer.material.color = Color.Lerp(_originalColor, _speedTintColor, lerpValue * 0.5f);
        }
    }
    
    public void ActivateSpeedBurst()
    {
        if (_isSpeedActive || _isOnCooldown) return;
        
        _isSpeedActive = true;
        _burstTimer = _burstDuration;
        
        ApplySpeedBoost();
        ActivateVisualEffects();
        PlayActivationSound();
        
        OnSpeedBurstActivated?.Invoke();
    }
    
    private void DeactivateSpeedBurst()
    {
        if (!_isSpeedActive) return;
        
        _isSpeedActive = false;
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
        
        RemoveSpeedBoost();
        DeactivateVisualEffects();
        PlayDeactivationSound();
        
        OnSpeedBurstDeactivated?.Invoke();
    }
    
    private void ApplySpeedBoost()
    {
        if (_characterController != null)
        {
            // For CharacterController, we'll modify movement in a separate movement script
            // This component will provide the speed multiplier
        }
        
        if (_rigidbody != null)
        {
            _rigidbody.drag *= 0.5f; // Reduce drag for smoother movement
        }
        
        // Increase camera FOV for speed effect
        if (_playerCamera != null)
        {
            StartCoroutine(LerpCameraFOV(_originalFOV + 10f, 0.2f));
        }
    }
    
    private void RemoveSpeedBoost()
    {
        if (_rigidbody != null)
        {
            _rigidbody.drag /= 0.5f; // Restore original drag
        }
        
        // Restore camera FOV
        if (_playerCamera != null)
        {
            StartCoroutine(LerpCameraFOV(_originalFOV, 0.3f));
        }
    }
    
    private void ActivateVisualEffects()
    {
        if (_speedTrailEffect != null)
            _speedTrailEffect.Play();
        
        if (_speedAura != null)
            _speedAura.SetActive(true);
    }
    
    private void DeactivateVisualEffects()
    {
        if (_speedTrailEffect != null)
            _speedTrailEffect.Stop();
        
        if (_speedAura != null)
            _speedAura.SetActive(false);
        
        if (_renderer != null)
            _renderer.material.color = _originalColor;
    }
    
    private void PlayActivationSound()
    {
        if (_audioSource != null && _activationSound != null)
        {
            _audioSource.pitch = Random.Range(0.9f, 1.1f);
            _audioSource.PlayOneShot(_activationSound);
        }
    }
    
    private void PlayDeactivationSound()
    {
        if (_audioSource != null && _deactivationSound != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(_deactivationSound);
        }
    }
    
    private System.Collections.IEnumerator LerpCameraFOV(float targetFOV, float duration)
    {
        if (_playerCamera == null) yield break;
        
        float startFOV = _playerCamera.fieldOfView;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _playerCamera.fieldOfView = Mathf.Lerp(startFOV, targetFOV, t);
            yield return null;
        }
        
        _playerCamera.fieldOfView = targetFOV;
    }
    
    public float GetSpeedMultiplier()
    {
        return _isSpeedActive ? _speedMultiplier : 1f;
    }
    
    public bool IsSpeedActive()
    {
        return _isSpeedActive;
    }
    
    public bool IsOnCooldown()
    {
        return _isOnCooldown;
    }
    
    public float GetCooldownProgress()
    {
        if (!_isOnCooldown) return 1f;
        return 1f - (_cooldownTimer / _cooldownTime);
    }
    
    public float GetBurstProgress()
    {
        if (!_isSpeedActive) return 0f;
        return _burstTimer / _burstDuration;
    }
    
    private void OnDisable()
    {
        if (_isSpeedActive)
        {
            DeactivateSpeedBurst();
        }
    }
}