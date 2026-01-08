// Prompt: ground pound attack
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class GroundPoundAttack : MonoBehaviour
{
    [Header("Ground Pound Settings")]
    [SerializeField] private float _poundForce = 20f;
    [SerializeField] private float _poundRadius = 5f;
    [SerializeField] private float _poundDamage = 50f;
    [SerializeField] private float _chargeTime = 0.5f;
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private LayerMask _enemyLayer = -1;
    
    [Header("Input")]
    [SerializeField] private KeyCode _poundKey = KeyCode.Q;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _chargingEffect;
    [SerializeField] private GameObject _impactEffect;
    [SerializeField] private ParticleSystem _dustParticles;
    [SerializeField] private float _cameraShakeIntensity = 0.5f;
    [SerializeField] private float _cameraShakeDuration = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _impactSound;
    [SerializeField] private float _audioVolume = 1f;
    
    [Header("Events")]
    public UnityEvent OnGroundPoundStart;
    public UnityEvent OnGroundPoundImpact;
    public UnityEvent<float> OnDamageDealt;
    
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Camera _mainCamera;
    private bool _isCharging = false;
    private bool _isPounding = false;
    private bool _isOnCooldown = false;
    private float _chargeTimer = 0f;
    private float _cooldownTimer = 0f;
    private Vector3 _originalCameraPosition;
    private float _cameraShakeTimer = 0f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        _mainCamera = Camera.main;
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_mainCamera != null)
        {
            _originalCameraPosition = _mainCamera.transform.localPosition;
        }
        
        if (_chargingEffect != null)
        {
            _chargingEffect.SetActive(false);
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCharging();
        UpdateCooldown();
        UpdateCameraShake();
    }
    
    private void HandleInput()
    {
        if (_isOnCooldown || _isPounding) return;
        
        if (Input.GetKeyDown(_poundKey) && !_isCharging && IsGrounded())
        {
            StartCharging();
        }
        
        if (Input.GetKeyUp(_poundKey) && _isCharging)
        {
            if (_chargeTimer >= _chargeTime)
            {
                ExecuteGroundPound();
            }
            else
            {
                CancelCharging();
            }
        }
    }
    
    private void UpdateCharging()
    {
        if (!_isCharging) return;
        
        _chargeTimer += Time.deltaTime;
        
        if (Input.GetKey(_poundKey))
        {
            if (_chargeTimer >= _chargeTime && !_isPounding)
            {
                ExecuteGroundPound();
            }
        }
    }
    
    private void UpdateCooldown()
    {
        if (!_isOnCooldown) return;
        
        _cooldownTimer -= Time.deltaTime;
        if (_cooldownTimer <= 0f)
        {
            _isOnCooldown = false;
        }
    }
    
    private void UpdateCameraShake()
    {
        if (_cameraShakeTimer <= 0f || _mainCamera == null) return;
        
        _cameraShakeTimer -= Time.deltaTime;
        
        if (_cameraShakeTimer > 0f)
        {
            Vector3 shakeOffset = Random.insideUnitSphere * _cameraShakeIntensity;
            _mainCamera.transform.localPosition = _originalCameraPosition + shakeOffset;
        }
        else
        {
            _mainCamera.transform.localPosition = _originalCameraPosition;
        }
    }
    
    private void StartCharging()
    {
        _isCharging = true;
        _chargeTimer = 0f;
        
        if (_chargingEffect != null)
        {
            _chargingEffect.SetActive(true);
        }
        
        PlaySound(_chargeSound);
        OnGroundPoundStart?.Invoke();
    }
    
    private void CancelCharging()
    {
        _isCharging = false;
        _chargeTimer = 0f;
        
        if (_chargingEffect != null)
        {
            _chargingEffect.SetActive(false);
        }
    }
    
    private void ExecuteGroundPound()
    {
        _isCharging = false;
        _isPounding = true;
        
        if (_chargingEffect != null)
        {
            _chargingEffect.SetActive(false);
        }
        
        _rigidbody.velocity = Vector3.down * _poundForce;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isPounding) return;
        
        if (IsInLayerMask(collision.gameObject.layer, _groundLayer))
        {
            PerformGroundImpact();
        }
    }
    
    private void PerformGroundImpact()
    {
        _isPounding = false;
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
        
        Vector3 impactPosition = transform.position;
        
        // Create impact effect
        if (_impactEffect != null)
        {
            Instantiate(_impactEffect, impactPosition, Quaternion.identity);
        }
        
        // Play dust particles
        if (_dustParticles != null)
        {
            _dustParticles.transform.position = impactPosition;
            _dustParticles.Play();
        }
        
        // Camera shake
        if (_mainCamera != null)
        {
            _cameraShakeTimer = _cameraShakeDuration;
        }
        
        // Play impact sound
        PlaySound(_impactSound);
        
        // Deal damage to enemies in radius
        DealDamageInRadius(impactPosition);
        
        // Apply knockback to rigidbodies in radius
        ApplyKnockbackInRadius(impactPosition);
        
        OnGroundPoundImpact?.Invoke();
    }
    
    private void DealDamageInRadius(Vector3 center)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, _poundRadius, _enemyLayer);
        float totalDamage = 0f;
        
        foreach (Collider hitCollider in hitColliders)
        {
            // Try to find health component or similar
            var healthComponent = hitCollider.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                // Send damage message if the object can receive it
                hitCollider.SendMessage("TakeDamage", _poundDamage, SendMessageOptions.DontRequireReceiver);
                totalDamage += _poundDamage;
            }
            
            // Alternative: destroy objects with specific tags
            if (hitCollider.CompareTag("Enemy") || hitCollider.CompareTag("Destructible"))
            {
                Destroy(hitCollider.gameObject);
                totalDamage += _poundDamage;
            }
        }
        
        if (totalDamage > 0f)
        {
            OnDamageDealt?.Invoke(totalDamage);
        }
    }
    
    private void ApplyKnockbackInRadius(Vector3 center)
    {
        Collider[] hitColliders = Physics.OverlapSphere(center, _poundRadius);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;
            
            Rigidbody hitRigidbody = hitCollider.GetComponent<Rigidbody>();
            if (hitRigidbody != null)
            {
                Vector3 direction = (hitCollider.transform.position - center).normalized;
                float distance = Vector3.Distance(center, hitCollider.transform.position);
                float knockbackForce = Mathf.Lerp(_poundForce, 0f, distance / _poundRadius);
                
                hitRigidbody.AddForce(direction * knockbackForce, ForceMode.Impulse);
            }
        }
    }
    
    private bool IsGrounded()
    {
        float rayDistance = 1.1f;
        return Physics.Raycast(transform.position, Vector3.down, rayDistance, _groundLayer);
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip, _audioVolume);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _poundRadius);
        
        if (_isCharging)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _poundRadius * 0.5f);
        }
    }
    
    public bool IsOnCooldown => _isOnCooldown;
    public bool IsCharging => _isCharging;
    public bool IsPounding => _isPounding;
    public float CooldownProgress => _isOnCooldown ? (1f - _cooldownTimer / _cooldownTime) : 1f;
    public float ChargeProgress => _isCharging ? Mathf.Clamp01(_chargeTimer / _chargeTime) : 0f;
}