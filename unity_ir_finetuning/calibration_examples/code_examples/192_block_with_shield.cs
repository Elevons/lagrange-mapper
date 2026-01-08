// Prompt: block with shield
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ShieldBlock : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private float _shieldHealth = 100f;
    [SerializeField] private float _maxShieldHealth = 100f;
    [SerializeField] private float _regenRate = 10f;
    [SerializeField] private float _regenDelay = 2f;
    [SerializeField] private bool _canRegenerate = true;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _shieldEffect;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Material _damagedMaterial;
    [SerializeField] private Color _shieldColor = Color.blue;
    [SerializeField] private AnimationCurve _damageFeedbackCurve = AnimationCurve.EaseInOut(0f, 1f, 0.3f, 0f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _breakSound;
    [SerializeField] private AudioClip _regenSound;
    [SerializeField] private float _audioVolume = 1f;
    
    [Header("Events")]
    public UnityEvent OnShieldHit;
    public UnityEvent OnShieldBroken;
    public UnityEvent OnShieldRestored;
    
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Collider _collider;
    private float _lastDamageTime;
    private bool _isShieldActive = true;
    private float _damageEffectTime;
    private Color _originalColor;
    private bool _isRegenerating;
    
    private void Start()
    {
        InitializeComponents();
        SetupShield();
    }
    
    private void InitializeComponents()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.volume = _audioVolume;
        _audioSource.playOnAwake = false;
        
        if (_renderer != null && _renderer.material != null)
        {
            _originalColor = _renderer.material.color;
        }
    }
    
    private void SetupShield()
    {
        _shieldHealth = _maxShieldHealth;
        UpdateShieldVisuals();
        
        if (_shieldEffect != null)
        {
            _shieldEffect.SetActive(_isShieldActive);
        }
    }
    
    private void Update()
    {
        HandleShieldRegeneration();
        HandleDamageEffect();
    }
    
    private void HandleShieldRegeneration()
    {
        if (!_canRegenerate || _isShieldActive) return;
        
        if (Time.time - _lastDamageTime >= _regenDelay)
        {
            if (!_isRegenerating)
            {
                _isRegenerating = true;
                PlaySound(_regenSound);
            }
            
            _shieldHealth += _regenRate * Time.deltaTime;
            _shieldHealth = Mathf.Clamp(_shieldHealth, 0f, _maxShieldHealth);
            
            if (_shieldHealth >= _maxShieldHealth)
            {
                RestoreShield();
            }
            
            UpdateShieldVisuals();
        }
    }
    
    private void HandleDamageEffect()
    {
        if (_damageEffectTime > 0f)
        {
            _damageEffectTime -= Time.deltaTime;
            
            if (_renderer != null && _damagedMaterial != null)
            {
                float effectStrength = _damageFeedbackCurve.Evaluate(1f - (_damageEffectTime / 0.3f));
                Color currentColor = Color.Lerp(_originalColor, Color.red, effectStrength);
                _renderer.material.color = currentColor;
            }
            
            if (_damageEffectTime <= 0f)
            {
                ResetVisualEffects();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.collider);
    }
    
    private void HandleCollision(Collider other)
    {
        if (!_isShieldActive) return;
        
        float damage = CalculateDamage(other);
        if (damage > 0f)
        {
            TakeDamage(damage);
        }
    }
    
    private float CalculateDamage(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            return 0f; // Don't damage shield from player contact
        }
        
        if (other.CompareTag("Enemy"))
        {
            return 25f;
        }
        
        // Check for projectiles or other damaging objects
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null)
        {
            float velocity = rb.velocity.magnitude;
            return Mathf.Clamp(velocity * 2f, 5f, 50f);
        }
        
        return 10f; // Default damage
    }
    
    public void TakeDamage(float damage)
    {
        if (!_isShieldActive) return;
        
        _shieldHealth -= damage;
        _shieldHealth = Mathf.Max(_shieldHealth, 0f);
        _lastDamageTime = Time.time;
        _isRegenerating = false;
        
        TriggerDamageEffect();
        PlaySound(_hitSound);
        OnShieldHit?.Invoke();
        
        if (_shieldHealth <= 0f)
        {
            BreakShield();
        }
        else
        {
            UpdateShieldVisuals();
        }
    }
    
    private void TriggerDamageEffect()
    {
        _damageEffectTime = 0.3f;
    }
    
    private void BreakShield()
    {
        _isShieldActive = false;
        _isRegenerating = false;
        
        UpdateShieldVisuals();
        PlaySound(_breakSound);
        OnShieldBroken?.Invoke();
        
        if (_shieldEffect != null)
        {
            _shieldEffect.SetActive(false);
        }
        
        // Disable collision while shield is down
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
    
    private void RestoreShield()
    {
        _isShieldActive = true;
        _isRegenerating = false;
        _shieldHealth = _maxShieldHealth;
        
        UpdateShieldVisuals();
        OnShieldRestored?.Invoke();
        
        if (_shieldEffect != null)
        {
            _shieldEffect.SetActive(true);
        }
        
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
    
    private void UpdateShieldVisuals()
    {
        if (_renderer == null) return;
        
        if (_isShieldActive)
        {
            float healthPercent = _shieldHealth / _maxShieldHealth;
            Color shieldColor = Color.Lerp(Color.red, _shieldColor, healthPercent);
            shieldColor.a = Mathf.Lerp(0.3f, 0.8f, healthPercent);
            
            if (_normalMaterial != null)
            {
                _renderer.material = _normalMaterial;
                _renderer.material.color = shieldColor;
            }
        }
        else
        {
            if (_damagedMaterial != null)
            {
                _renderer.material = _damagedMaterial;
            }
            else if (_renderer.material != null)
            {
                Color brokenColor = _originalColor;
                brokenColor.a = 0.2f;
                _renderer.material.color = brokenColor;
            }
        }
    }
    
    private void ResetVisualEffects()
    {
        if (_renderer != null)
        {
            if (_isShieldActive && _normalMaterial != null)
            {
                _renderer.material = _normalMaterial;
                UpdateShieldVisuals();
            }
            else
            {
                _renderer.material.color = _originalColor;
            }
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip, _audioVolume);
        }
    }
    
    public void SetShieldHealth(float health)
    {
        _shieldHealth = Mathf.Clamp(health, 0f, _maxShieldHealth);
        
        if (_shieldHealth > 0f && !_isShieldActive)
        {
            RestoreShield();
        }
        else if (_shieldHealth <= 0f && _isShieldActive)
        {
            BreakShield();
        }
        else
        {
            UpdateShieldVisuals();
        }
    }
    
    public float GetShieldHealth()
    {
        return _shieldHealth;
    }
    
    public float GetShieldHealthPercent()
    {
        return _shieldHealth / _maxShieldHealth;
    }
    
    public bool IsShieldActive()
    {
        return _isShieldActive;
    }
    
    public void ForceBreakShield()
    {
        _shieldHealth = 0f;
        BreakShield();
    }
    
    public void ForceRestoreShield()
    {
        RestoreShield();
    }
}