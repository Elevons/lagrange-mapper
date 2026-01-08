// Prompt: health bar that updates visually
// Type: pickup

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class HealthBar : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    [Header("Visual Components")]
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _backgroundImage;
    
    [Header("Color Settings")]
    [SerializeField] private Color _healthyColor = Color.green;
    [SerializeField] private Color _warningColor = Color.yellow;
    [SerializeField] private Color _criticalColor = Color.red;
    [SerializeField] private float _warningThreshold = 0.6f;
    [SerializeField] private float _criticalThreshold = 0.3f;
    
    [Header("Animation Settings")]
    [SerializeField] private bool _useSmoothing = true;
    [SerializeField] private float _smoothSpeed = 5f;
    [SerializeField] private bool _usePulseEffect = true;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private float _pulseIntensity = 0.3f;
    
    [Header("Events")]
    public UnityEvent OnHealthChanged;
    public UnityEvent OnHealthDepleted;
    public UnityEvent OnHealthCritical;
    
    private float _targetHealth;
    private bool _isInitialized = false;
    private float _originalAlpha;
    
    void Start()
    {
        InitializeHealthBar();
    }
    
    void Update()
    {
        if (!_isInitialized) return;
        
        UpdateHealthBarVisual();
        UpdateColorBasedOnHealth();
        
        if (_usePulseEffect && GetHealthPercentage() <= _criticalThreshold)
        {
            ApplyPulseEffect();
        }
    }
    
    private void InitializeHealthBar()
    {
        _currentHealth = _maxHealth;
        _targetHealth = _maxHealth;
        
        if (_healthSlider == null)
            _healthSlider = GetComponent<Slider>();
            
        if (_fillImage == null && _healthSlider != null)
            _fillImage = _healthSlider.fillRect?.GetComponent<Image>();
            
        if (_backgroundImage == null && _healthSlider != null)
            _backgroundImage = _healthSlider.GetComponent<Image>();
        
        if (_healthSlider != null)
        {
            _healthSlider.maxValue = _maxHealth;
            _healthSlider.value = _currentHealth;
        }
        
        if (_fillImage != null)
        {
            _originalAlpha = _fillImage.color.a;
        }
        
        _isInitialized = true;
    }
    
    private void UpdateHealthBarVisual()
    {
        if (_healthSlider == null) return;
        
        if (_useSmoothing)
        {
            _healthSlider.value = Mathf.Lerp(_healthSlider.value, _targetHealth, Time.deltaTime * _smoothSpeed);
        }
        else
        {
            _healthSlider.value = _targetHealth;
        }
    }
    
    private void UpdateColorBasedOnHealth()
    {
        if (_fillImage == null) return;
        
        float healthPercentage = GetHealthPercentage();
        Color targetColor;
        
        if (healthPercentage > _warningThreshold)
        {
            targetColor = _healthyColor;
        }
        else if (healthPercentage > _criticalThreshold)
        {
            targetColor = _warningColor;
        }
        else
        {
            targetColor = _criticalColor;
        }
        
        _fillImage.color = targetColor;
    }
    
    private void ApplyPulseEffect()
    {
        if (_fillImage == null) return;
        
        float pulse = Mathf.Sin(Time.time * _pulseSpeed) * _pulseIntensity;
        float alpha = _originalAlpha + pulse;
        alpha = Mathf.Clamp01(alpha);
        
        Color currentColor = _fillImage.color;
        currentColor.a = alpha;
        _fillImage.color = currentColor;
    }
    
    public void SetMaxHealth(float maxHealth)
    {
        _maxHealth = Mathf.Max(1f, maxHealth);
        _currentHealth = Mathf.Min(_currentHealth, _maxHealth);
        _targetHealth = _currentHealth;
        
        if (_healthSlider != null)
        {
            _healthSlider.maxValue = _maxHealth;
        }
        
        OnHealthChanged?.Invoke();
    }
    
    public void SetHealth(float health)
    {
        float previousHealth = _currentHealth;
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        _targetHealth = _currentHealth;
        
        if (previousHealth != _currentHealth)
        {
            OnHealthChanged?.Invoke();
            
            if (_currentHealth <= 0f && previousHealth > 0f)
            {
                OnHealthDepleted?.Invoke();
            }
            else if (_currentHealth <= _maxHealth * _criticalThreshold && previousHealth > _maxHealth * _criticalThreshold)
            {
                OnHealthCritical?.Invoke();
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        SetHealth(_currentHealth - damage);
    }
    
    public void Heal(float healAmount)
    {
        SetHealth(_currentHealth + healAmount);
    }
    
    public void RestoreToFull()
    {
        SetHealth(_maxHealth);
    }
    
    public float GetCurrentHealth()
    {
        return _currentHealth;
    }
    
    public float GetMaxHealth()
    {
        return _maxHealth;
    }
    
    public float GetHealthPercentage()
    {
        return _maxHealth > 0 ? _currentHealth / _maxHealth : 0f;
    }
    
    public bool IsHealthFull()
    {
        return _currentHealth >= _maxHealth;
    }
    
    public bool IsHealthEmpty()
    {
        return _currentHealth <= 0f;
    }
    
    public bool IsHealthCritical()
    {
        return GetHealthPercentage() <= _criticalThreshold;
    }
    
    void OnValidate()
    {
        _maxHealth = Mathf.Max(1f, _maxHealth);
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        _warningThreshold = Mathf.Clamp01(_warningThreshold);
        _criticalThreshold = Mathf.Clamp01(_criticalThreshold);
        _smoothSpeed = Mathf.Max(0.1f, _smoothSpeed);
        _pulseSpeed = Mathf.Max(0.1f, _pulseSpeed);
        _pulseIntensity = Mathf.Clamp01(_pulseIntensity);
        
        if (_criticalThreshold > _warningThreshold)
        {
            _criticalThreshold = _warningThreshold;
        }
    }
}