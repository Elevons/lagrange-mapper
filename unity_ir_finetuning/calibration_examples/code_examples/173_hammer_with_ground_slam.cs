// Prompt: hammer with ground slam
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Hammer : MonoBehaviour
{
    [Header("Hammer Settings")]
    [SerializeField] private float _slamForce = 20f;
    [SerializeField] private float _slamRadius = 5f;
    [SerializeField] private float _slamDamage = 50f;
    [SerializeField] private float _slamCooldown = 2f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Animation")]
    [SerializeField] private float _raiseHeight = 2f;
    [SerializeField] private float _raiseSpeed = 5f;
    [SerializeField] private float _slamSpeed = 15f;
    
    [Header("Effects")]
    [SerializeField] private GameObject _slamEffectPrefab;
    [SerializeField] private AudioClip _slamSound;
    [SerializeField] private float _cameraShakeIntensity = 0.5f;
    [SerializeField] private float _cameraShakeDuration = 0.3f;
    
    [Header("Events")]
    public UnityEvent OnSlamStart;
    public UnityEvent OnSlamHit;
    public UnityEvent<float> OnCooldownChanged;
    
    private Vector3 _originalPosition;
    private bool _isSlaming = false;
    private bool _isOnCooldown = false;
    private float _cooldownTimer = 0f;
    private AudioSource _audioSource;
    private Camera _mainCamera;
    
    private enum HammerState
    {
        Idle,
        Raising,
        Slamming,
        Cooldown
    }
    
    private HammerState _currentState = HammerState.Idle;
    
    void Start()
    {
        _originalPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
    }
    
    void Update()
    {
        HandleCooldown();
        HandleInput();
        UpdateHammerMovement();
    }
    
    private void HandleCooldown()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            OnCooldownChanged?.Invoke(_cooldownTimer / _slamCooldown);
            
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                _currentState = HammerState.Idle;
            }
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanSlam())
        {
            StartSlam();
        }
    }
    
    private bool CanSlam()
    {
        return _currentState == HammerState.Idle && !_isOnCooldown;
    }
    
    private void StartSlam()
    {
        _currentState = HammerState.Raising;
        _isSlaming = true;
        OnSlamStart?.Invoke();
    }
    
    private void UpdateHammerMovement()
    {
        switch (_currentState)
        {
            case HammerState.Raising:
                HandleRaising();
                break;
            case HammerState.Slamming:
                HandleSlamming();
                break;
        }
    }
    
    private void HandleRaising()
    {
        Vector3 targetPosition = _originalPosition + Vector3.up * _raiseHeight;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, _raiseSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            _currentState = HammerState.Slamming;
        }
    }
    
    private void HandleSlamming()
    {
        transform.position = Vector3.MoveTowards(transform.position, _originalPosition, _slamSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, _originalPosition) < 0.1f)
        {
            ExecuteSlam();
            transform.position = _originalPosition;
            _currentState = HammerState.Cooldown;
            _isSlaming = false;
            _isOnCooldown = true;
            _cooldownTimer = _slamCooldown;
        }
    }
    
    private void ExecuteSlam()
    {
        // Create slam effect
        if (_slamEffectPrefab != null)
        {
            Instantiate(_slamEffectPrefab, _originalPosition, Quaternion.identity);
        }
        
        // Play slam sound
        if (_slamSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_slamSound);
        }
        
        // Camera shake
        if (_mainCamera != null)
        {
            StartCoroutine(CameraShake());
        }
        
        // Find and damage targets
        Collider[] hitColliders = Physics.OverlapSphere(_originalPosition, _slamRadius, _targetLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            // Apply damage to objects with health
            var healthComponent = hitCollider.GetComponent<IDamageable>();
            if (healthComponent != null)
            {
                healthComponent.TakeDamage(_slamDamage);
            }
            
            // Apply knockback force
            Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 forceDirection = (hitCollider.transform.position - _originalPosition).normalized;
                forceDirection.y = 0.5f; // Add upward force
                rb.AddForce(forceDirection * _slamForce, ForceMode.Impulse);
            }
            
            // Trigger player-specific effects
            if (hitCollider.CompareTag("Player"))
            {
                // Additional player effects can be added here
                OnSlamHit?.Invoke();
            }
        }
        
        OnSlamHit?.Invoke();
    }
    
    private System.Collections.IEnumerator CameraShake()
    {
        Vector3 originalPosition = _mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < _cameraShakeDuration)
        {
            float x = Random.Range(-1f, 1f) * _cameraShakeIntensity;
            float y = Random.Range(-1f, 1f) * _cameraShakeIntensity;
            
            _mainCamera.transform.position = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _mainCamera.transform.position = originalPosition;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw slam radius
        Gizmos.color = Color.red;
        Vector3 center = Application.isPlaying ? _originalPosition : transform.position;
        Gizmos.DrawWireSphere(center, _slamRadius);
        
        // Draw raise height
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(center, center + Vector3.up * _raiseHeight);
    }
    
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
    
    public bool IsOnCooldown => _isOnCooldown;
    public float CooldownProgress => _isOnCooldown ? _cooldownTimer / _slamCooldown : 0f;
    public bool IsSlaming => _isSlaming;
}