// Prompt: charge attack with hold
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class ChargeAttack : MonoBehaviour
{
    [Header("Charge Settings")]
    [SerializeField] private KeyCode _chargeKey = KeyCode.Mouse0;
    [SerializeField] private float _minChargeTime = 0.5f;
    [SerializeField] private float _maxChargeTime = 3.0f;
    [SerializeField] private float _minDamage = 10f;
    [SerializeField] private float _maxDamage = 50f;
    [SerializeField] private float _minRange = 2f;
    [SerializeField] private float _maxRange = 8f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _chargingEffect;
    [SerializeField] private GameObject _attackEffect;
    [SerializeField] private LineRenderer _rangeIndicator;
    [SerializeField] private Transform _attackPoint;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Events")]
    public UnityEvent<float> OnChargeStarted;
    public UnityEvent<float> OnChargeUpdated;
    public UnityEvent<float, float> OnAttackReleased;
    
    private bool _isCharging = false;
    private float _chargeStartTime;
    private float _currentChargeTime;
    private Camera _playerCamera;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_rangeIndicator != null)
        {
            _rangeIndicator.enabled = false;
            _rangeIndicator.positionCount = 2;
        }
        
        if (_chargingEffect != null)
            _chargingEffect.SetActive(false);
    }
    
    private void Update()
    {
        HandleInput();
        
        if (_isCharging)
        {
            UpdateCharge();
            UpdateVisualEffects();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_chargeKey))
        {
            StartCharge();
        }
        else if (Input.GetKeyUp(_chargeKey) && _isCharging)
        {
            ReleaseAttack();
        }
    }
    
    private void StartCharge()
    {
        _isCharging = true;
        _chargeStartTime = Time.time;
        _currentChargeTime = 0f;
        
        if (_chargingEffect != null)
            _chargingEffect.SetActive(true);
            
        if (_audioSource != null && _chargeSound != null)
            _audioSource.PlayOneShot(_chargeSound);
            
        OnChargeStarted.Invoke(0f);
    }
    
    private void UpdateCharge()
    {
        _currentChargeTime = Time.time - _chargeStartTime;
        _currentChargeTime = Mathf.Clamp(_currentChargeTime, 0f, _maxChargeTime);
        
        float chargePercent = Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
        OnChargeUpdated.Invoke(chargePercent);
        
        if (_currentChargeTime >= _maxChargeTime)
        {
            ReleaseAttack();
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_rangeIndicator != null && _attackPoint != null)
        {
            _rangeIndicator.enabled = true;
            float currentRange = Mathf.Lerp(_minRange, _maxRange, _currentChargeTime / _maxChargeTime);
            
            Vector3 startPos = _attackPoint.position;
            Vector3 endPos = startPos + _attackPoint.forward * currentRange;
            
            _rangeIndicator.SetPosition(0, startPos);
            _rangeIndicator.SetPosition(1, endPos);
        }
    }
    
    private void ReleaseAttack()
    {
        if (!_isCharging) return;
        
        _isCharging = false;
        
        if (_chargingEffect != null)
            _chargingEffect.SetActive(false);
            
        if (_rangeIndicator != null)
            _rangeIndicator.enabled = false;
        
        if (_currentChargeTime >= _minChargeTime)
        {
            ExecuteAttack();
        }
    }
    
    private void ExecuteAttack()
    {
        float chargePercent = Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
        float damage = Mathf.Lerp(_minDamage, _maxDamage, chargePercent);
        float range = Mathf.Lerp(_minRange, _maxRange, chargePercent);
        
        if (_attackEffect != null)
        {
            GameObject effect = Instantiate(_attackEffect, _attackPoint.position, _attackPoint.rotation);
            Destroy(effect, 2f);
        }
        
        if (_audioSource != null && _releaseSound != null)
            _audioSource.PlayOneShot(_releaseSound);
        
        PerformAttack(damage, range);
        OnAttackReleased.Invoke(damage, range);
    }
    
    private void PerformAttack(float damage, float range)
    {
        Vector3 attackOrigin = _attackPoint != null ? _attackPoint.position : transform.position;
        Vector3 attackDirection = _attackPoint != null ? _attackPoint.forward : transform.forward;
        
        RaycastHit[] hits = Physics.SphereCastAll(attackOrigin, 1f, attackDirection, range);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(damage);
            }
            
            Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 knockbackDirection = (hit.point - attackOrigin).normalized;
                rb.AddForce(knockbackDirection * damage * 10f, ForceMode.Impulse);
            }
        }
    }
    
    public float GetChargePercent()
    {
        if (!_isCharging) return 0f;
        return Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
    }
    
    public bool IsCharging()
    {
        return _isCharging;
    }
    
    public void CancelCharge()
    {
        if (_isCharging)
        {
            _isCharging = false;
            
            if (_chargingEffect != null)
                _chargingEffect.SetActive(false);
                
            if (_rangeIndicator != null)
                _rangeIndicator.enabled = false;
        }
    }
}

public interface IDamageable
{
    void TakeDamage(float damage);
}