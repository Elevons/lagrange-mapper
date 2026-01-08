// Prompt: shield that blocks incoming damage
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Shield : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private float _maxShieldHealth = 100f;
    [SerializeField] private float _rechargeDelay = 3f;
    [SerializeField] private float _rechargeRate = 20f;
    [SerializeField] private bool _blockAllDamage = true;
    [SerializeField] private float _damageReduction = 0.8f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _shieldVisual;
    [SerializeField] private ParticleSystem _hitEffect;
    [SerializeField] private ParticleSystem _rechargeEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _breakSound;
    [SerializeField] private AudioClip _rechargeSound;
    
    [Header("Events")]
    public UnityEvent<float> OnShieldDamaged;
    public UnityEvent OnShieldBroken;
    public UnityEvent OnShieldRestored;
    public UnityEvent<float, float> OnShieldHealthChanged;
    
    private float _currentShieldHealth;
    private bool _isRecharging;
    private float _lastDamageTime;
    private Renderer _shieldRenderer;
    private Color _originalColor;
    private bool _isShieldActive;
    
    private void Start()
    {
        _currentShieldHealth = _maxShieldHealth;
        _isShieldActive = true;
        
        if (_shieldVisual != null)
        {
            _shieldRenderer = _shieldVisual.GetComponent<Renderer>();
            if (_shieldRenderer != null)
            {
                _originalColor = _shieldRenderer.material.color;
            }
        }
        
        UpdateShieldVisual();
        OnShieldHealthChanged?.Invoke(_currentShieldHealth, _maxShieldHealth);
    }
    
    private void Update()
    {
        if (!_isShieldActive && !_isRecharging && Time.time - _lastDamageTime >= _rechargeDelay)
        {
            StartRecharge();
        }
        
        if (_isRecharging)
        {
            RechargeShield();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Projectile") || other.CompareTag("Enemy"))
        {
            HandleIncomingDamage(other);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Projectile") || collision.gameObject.CompareTag("Enemy"))
        {
            HandleIncomingDamage(collision.collider);
        }
    }
    
    private void HandleIncomingDamage(Collider damageSource)
    {
        if (!_isShieldActive) return;
        
        float damage = GetDamageFromSource(damageSource);
        
        if (_blockAllDamage)
        {
            BlockDamage(damage, damageSource);
        }
        else
        {
            float reducedDamage = damage * (1f - _damageReduction);
            float shieldDamage = damage * _damageReduction;
            BlockDamage(shieldDamage, damageSource);
            
            // Pass reduced damage to player if they have a health component
            var playerHealth = GetComponentInParent<MonoBehaviour>();
            if (playerHealth != null)
            {
                playerHealth.SendMessage("TakeDamage", reducedDamage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    private float GetDamageFromSource(Collider source)
    {
        // Try to get damage from various common damage components
        var damageComponent = source.GetComponent<MonoBehaviour>();
        if (damageComponent != null)
        {
            // Try to get damage value through reflection or common field names
            var damageField = damageComponent.GetType().GetField("damage");
            if (damageField != null && damageField.FieldType == typeof(float))
            {
                return (float)damageField.GetValue(damageComponent);
            }
            
            var damageProperty = damageComponent.GetType().GetProperty("Damage");
            if (damageProperty != null && damageProperty.PropertyType == typeof(float))
            {
                return (float)damageProperty.GetValue(damageComponent);
            }
        }
        
        // Default damage if no damage component found
        return 25f;
    }
    
    private void BlockDamage(float damage, Collider source)
    {
        _currentShieldHealth -= damage;
        _lastDamageTime = Time.time;
        _isRecharging = false;
        
        PlayHitEffect();
        PlaySound(_hitSound);
        OnShieldDamaged?.Invoke(damage);
        OnShieldHealthChanged?.Invoke(_currentShieldHealth, _maxShieldHealth);
        
        if (_currentShieldHealth <= 0)
        {
            BreakShield();
        }
        
        // Destroy or disable the damage source
        if (source.CompareTag("Projectile"))
        {
            Destroy(source.gameObject);
        }
        
        UpdateShieldVisual();
    }
    
    private void BreakShield()
    {
        _isShieldActive = false;
        _currentShieldHealth = 0;
        
        PlaySound(_breakSound);
        OnShieldBroken?.Invoke();
        UpdateShieldVisual();
        
        if (_hitEffect != null)
        {
            _hitEffect.Stop();
        }
    }
    
    private void StartRecharge()
    {
        _isRecharging = true;
        PlaySound(_rechargeSound);
        
        if (_rechargeEffect != null)
        {
            _rechargeEffect.Play();
        }
    }
    
    private void RechargeShield()
    {
        _currentShieldHealth += _rechargeRate * Time.deltaTime;
        
        if (_currentShieldHealth >= _maxShieldHealth)
        {
            _currentShieldHealth = _maxShieldHealth;
            _isRecharging = false;
            _isShieldActive = true;
            
            OnShieldRestored?.Invoke();
            
            if (_rechargeEffect != null)
            {
                _rechargeEffect.Stop();
            }
        }
        
        OnShieldHealthChanged?.Invoke(_currentShieldHealth, _maxShieldHealth);
        UpdateShieldVisual();
    }
    
    private void UpdateShieldVisual()
    {
        if (_shieldVisual != null)
        {
            _shieldVisual.SetActive(_isShieldActive);
            
            if (_shieldRenderer != null)
            {
                float healthPercent = _currentShieldHealth / _maxShieldHealth;
                Color shieldColor = Color.Lerp(Color.red, _originalColor, healthPercent);
                shieldColor.a = _originalColor.a * healthPercent;
                _shieldRenderer.material.color = shieldColor;
            }
        }
    }
    
    private void PlayHitEffect()
    {
        if (_hitEffect != null)
        {
            _hitEffect.Play();
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void SetShieldHealth(float health)
    {
        _currentShieldHealth = Mathf.Clamp(health, 0, _maxShieldHealth);
        _isShieldActive = _currentShieldHealth > 0;
        UpdateShieldVisual();
        OnShieldHealthChanged?.Invoke(_currentShieldHealth, _maxShieldHealth);
    }
    
    public void RepairShield(float amount)
    {
        float oldHealth = _currentShieldHealth;
        _currentShieldHealth = Mathf.Clamp(_currentShieldHealth + amount, 0, _maxShieldHealth);
        
        if (oldHealth <= 0 && _currentShieldHealth > 0)
        {
            _isShieldActive = true;
            OnShieldRestored?.Invoke();
        }
        
        UpdateShieldVisual();
        OnShieldHealthChanged?.Invoke(_currentShieldHealth, _maxShieldHealth);
    }
    
    public float GetShieldHealthPercent()
    {
        return _currentShieldHealth / _maxShieldHealth;
    }
    
    public bool IsShieldActive()
    {
        return _isShieldActive;
    }
    
    public void ToggleShield(bool active)
    {
        _isShieldActive = active && _currentShieldHealth > 0;
        UpdateShieldVisual();
    }
}