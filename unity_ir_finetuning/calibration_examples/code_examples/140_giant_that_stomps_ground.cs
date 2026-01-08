// Prompt: giant that stomps ground
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class GiantStomp : MonoBehaviour
{
    [Header("Stomp Settings")]
    [SerializeField] private float _stompInterval = 3f;
    [SerializeField] private float _stompForce = 1000f;
    [SerializeField] private float _stompRadius = 10f;
    [SerializeField] private float _stompDamage = 50f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Animation")]
    [SerializeField] private float _liftHeight = 2f;
    [SerializeField] private float _liftDuration = 1f;
    [SerializeField] private float _stompDuration = 0.3f;
    [SerializeField] private Transform _footTransform;
    
    [Header("Effects")]
    [SerializeField] private GameObject _stompEffectPrefab;
    [SerializeField] private AudioClip _stompSound;
    [SerializeField] private AudioClip _liftSound;
    [SerializeField] private float _cameraShakeIntensity = 2f;
    [SerializeField] private float _cameraShakeDuration = 0.5f;
    
    [Header("Events")]
    public UnityEvent OnStompStart;
    public UnityEvent OnStompImpact;
    public UnityEvent OnStompComplete;
    
    private AudioSource _audioSource;
    private Vector3 _originalFootPosition;
    private bool _isStomping = false;
    private float _stompTimer = 0f;
    private Camera _mainCamera;
    private Vector3 _originalCameraPosition;
    private float _shakeTimer = 0f;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_footTransform == null)
            _footTransform = transform;
            
        _originalFootPosition = _footTransform.position;
        _mainCamera = Camera.main;
        if (_mainCamera != null)
            _originalCameraPosition = _mainCamera.transform.position;
            
        _stompTimer = _stompInterval;
    }
    
    private void Update()
    {
        if (!_isStomping)
        {
            _stompTimer -= Time.deltaTime;
            if (_stompTimer <= 0f)
            {
                StartStomp();
                _stompTimer = _stompInterval;
            }
        }
        
        UpdateCameraShake();
    }
    
    private void StartStomp()
    {
        if (_isStomping) return;
        
        _isStomping = true;
        OnStompStart?.Invoke();
        
        if (_liftSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_liftSound);
            
        StartCoroutine(StompSequence());
    }
    
    private System.Collections.IEnumerator StompSequence()
    {
        // Lift foot
        Vector3 startPos = _originalFootPosition;
        Vector3 liftPos = _originalFootPosition + Vector3.up * _liftHeight;
        
        float elapsedTime = 0f;
        while (elapsedTime < _liftDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _liftDuration;
            t = Mathf.SmoothStep(0f, 1f, t);
            _footTransform.position = Vector3.Lerp(startPos, liftPos, t);
            yield return null;
        }
        
        // Stomp down
        elapsedTime = 0f;
        while (elapsedTime < _stompDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / _stompDuration;
            t = t * t; // Accelerate downward
            _footTransform.position = Vector3.Lerp(liftPos, _originalFootPosition, t);
            yield return null;
        }
        
        _footTransform.position = _originalFootPosition;
        
        // Impact
        PerformStompImpact();
        OnStompImpact?.Invoke();
        
        yield return new WaitForSeconds(0.2f);
        
        _isStomping = false;
        OnStompComplete?.Invoke();
    }
    
    private void PerformStompImpact()
    {
        Vector3 stompPosition = _footTransform.position;
        
        // Play sound
        if (_stompSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_stompSound);
            
        // Spawn effect
        if (_stompEffectPrefab != null)
            Instantiate(_stompEffectPrefab, stompPosition, Quaternion.identity);
            
        // Camera shake
        StartCameraShake();
        
        // Find affected objects
        Collider[] hitColliders = Physics.OverlapSphere(stompPosition, _stompRadius, _affectedLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            // Apply force to rigidbodies
            Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = (hitCollider.transform.position - stompPosition).normalized;
                float distance = Vector3.Distance(hitCollider.transform.position, stompPosition);
                float forceMagnitude = _stompForce * (1f - distance / _stompRadius);
                
                rb.AddForce(direction * forceMagnitude + Vector3.up * forceMagnitude * 0.5f, ForceMode.Impulse);
            }
            
            // Damage players
            if (hitCollider.CompareTag("Player"))
            {
                // Send damage message if the player has a method to receive it
                hitCollider.SendMessage("TakeDamage", _stompDamage, SendMessageOptions.DontRequireReceiver);
                
                // Alternative: Use a simple health component if it exists
                var healthComponent = hitCollider.GetComponent<SimpleHealth>();
                if (healthComponent != null)
                {
                    healthComponent.TakeDamage(_stompDamage);
                }
            }
            
            // Destroy destructible objects
            if (hitCollider.CompareTag("Destructible"))
            {
                Destroy(hitCollider.gameObject);
            }
        }
    }
    
    private void StartCameraShake()
    {
        if (_mainCamera != null)
        {
            _shakeTimer = _cameraShakeDuration;
        }
    }
    
    private void UpdateCameraShake()
    {
        if (_shakeTimer > 0f && _mainCamera != null)
        {
            _shakeTimer -= Time.deltaTime;
            
            float shakeAmount = _cameraShakeIntensity * (_shakeTimer / _cameraShakeDuration);
            Vector3 shakeOffset = Random.insideUnitSphere * shakeAmount;
            shakeOffset.z = 0f; // Keep camera on same Z plane
            
            _mainCamera.transform.position = _originalCameraPosition + shakeOffset;
            
            if (_shakeTimer <= 0f)
            {
                _mainCamera.transform.position = _originalCameraPosition;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Vector3 center = _footTransform != null ? _footTransform.position : transform.position;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, _stompRadius);
        
        if (_footTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center + Vector3.up * _liftHeight, Vector3.one * 0.5f);
        }
    }
}

// Simple health component that can be used with the stomp system
public class SimpleHealth : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth);
        
        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
            Destroy(gameObject);
        }
    }
    
    public void Heal(float amount)
    {
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}