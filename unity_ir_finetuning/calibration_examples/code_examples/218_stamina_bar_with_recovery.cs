// Prompt: stamina bar with recovery
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class StaminaBar : MonoBehaviour
{
    [Header("Stamina Settings")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _currentStamina;
    [SerializeField] private float _staminaRecoveryRate = 20f;
    [SerializeField] private float _staminaRecoveryDelay = 2f;
    [SerializeField] private float _lowStaminaThreshold = 20f;
    
    [Header("UI References")]
    [SerializeField] private Slider _staminaSlider;
    [SerializeField] private Image _staminaFillImage;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _normalColor = Color.green;
    [SerializeField] private Color _lowStaminaColor = Color.red;
    [SerializeField] private Color _depletedColor = Color.gray;
    [SerializeField] private bool _hideWhenFull = true;
    [SerializeField] private float _fadeSpeed = 2f;
    
    [Header("Animation")]
    [SerializeField] private bool _enablePulseEffect = true;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private float _pulseIntensity = 0.3f;
    
    [Header("Events")]
    public UnityEvent OnStaminaDepleted;
    public UnityEvent OnStaminaLow;
    public UnityEvent OnStaminaRecovered;
    
    private float _lastStaminaUseTime;
    private bool _isRecovering;
    private bool _wasLowStamina;
    private bool _wasDepleted;
    private float _targetAlpha = 1f;
    private float _pulseTimer;
    
    private void Start()
    {
        InitializeStamina();
        SetupUI();
    }
    
    private void Update()
    {
        HandleStaminaRecovery();
        UpdateUI();
        HandleVisualEffects();
    }
    
    private void InitializeStamina()
    {
        _currentStamina = _maxStamina;
        _lastStaminaUseTime = Time.time;
        _isRecovering = false;
        _wasLowStamina = false;
        _wasDepleted = false;
    }
    
    private void SetupUI()
    {
        if (_staminaSlider != null)
        {
            _staminaSlider.maxValue = _maxStamina;
            _staminaSlider.value = _currentStamina;
        }
        
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
            
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    private void HandleStaminaRecovery()
    {
        bool shouldRecover = Time.time - _lastStaminaUseTime >= _staminaRecoveryDelay;
        
        if (shouldRecover && _currentStamina < _maxStamina)
        {
            if (!_isRecovering)
            {
                _isRecovering = true;
            }
            
            _currentStamina += _staminaRecoveryRate * Time.deltaTime;
            _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
            
            if (_currentStamina >= _maxStamina)
            {
                _currentStamina = _maxStamina;
                _isRecovering = false;
                OnStaminaRecovered?.Invoke();
            }
        }
        else if (!shouldRecover)
        {
            _isRecovering = false;
        }
    }
    
    private void UpdateUI()
    {
        if (_staminaSlider != null)
        {
            _staminaSlider.value = _currentStamina;
        }
        
        UpdateStaminaColor();
        UpdateVisibility();
        CheckStaminaThresholds();
    }
    
    private void UpdateStaminaColor()
    {
        if (_staminaFillImage == null) return;
        
        Color targetColor;
        
        if (_currentStamina <= 0f)
        {
            targetColor = _depletedColor;
        }
        else if (_currentStamina <= _lowStaminaThreshold)
        {
            targetColor = _lowStaminaColor;
        }
        else
        {
            targetColor = _normalColor;
        }
        
        _staminaFillImage.color = targetColor;
    }
    
    private void UpdateVisibility()
    {
        if (!_hideWhenFull)
        {
            _targetAlpha = 1f;
        }
        else
        {
            _targetAlpha = (_currentStamina >= _maxStamina && !_isRecovering) ? 0f : 1f;
        }
        
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, _targetAlpha, _fadeSpeed * Time.deltaTime);
        }
    }
    
    private void HandleVisualEffects()
    {
        if (!_enablePulseEffect || _staminaFillImage == null) return;
        
        if (_currentStamina <= _lowStaminaThreshold && _currentStamina > 0f)
        {
            _pulseTimer += Time.deltaTime * _pulseSpeed;
            float pulseValue = Mathf.Sin(_pulseTimer) * _pulseIntensity;
            Color currentColor = _staminaFillImage.color;
            currentColor.a = Mathf.Clamp01(1f + pulseValue);
            _staminaFillImage.color = currentColor;
        }
        else
        {
            _pulseTimer = 0f;
            if (_staminaFillImage.color.a != 1f)
            {
                Color currentColor = _staminaFillImage.color;
                currentColor.a = 1f;
                _staminaFillImage.color = currentColor;
            }
        }
    }
    
    private void CheckStaminaThresholds()
    {
        bool isLowStamina = _currentStamina <= _lowStaminaThreshold && _currentStamina > 0f;
        bool isDepleted = _currentStamina <= 0f;
        
        if (isDepleted && !_wasDepleted)
        {
            OnStaminaDepleted?.Invoke();
            _wasDepleted = true;
        }
        else if (!isDepleted)
        {
            _wasDepleted = false;
        }
        
        if (isLowStamina && !_wasLowStamina)
        {
            OnStaminaLow?.Invoke();
            _wasLowStamina = true;
        }
        else if (!isLowStamina)
        {
            _wasLowStamina = false;
        }
    }
    
    public void UseStamina(float amount)
    {
        if (amount <= 0f) return;
        
        _currentStamina -= amount;
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
        _lastStaminaUseTime = Time.time;
        _isRecovering = false;
    }
    
    public void RestoreStamina(float amount)
    {
        if (amount <= 0f) return;
        
        _currentStamina += amount;
        _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
    }
    
    public void SetMaxStamina(float newMaxStamina)
    {
        if (newMaxStamina <= 0f) return;
        
        float ratio = _currentStamina / _maxStamina;
        _maxStamina = newMaxStamina;
        _currentStamina = _maxStamina * ratio;
        
        if (_staminaSlider != null)
        {
            _staminaSlider.maxValue = _maxStamina;
        }
    }
    
    public bool HasStamina(float amount)
    {
        return _currentStamina >= amount;
    }
    
    public bool CanUseStamina(float amount)
    {
        return _currentStamina >= amount;
    }
    
    public float GetStaminaPercentage()
    {
        return _maxStamina > 0f ? _currentStamina / _maxStamina : 0f;
    }
    
    public float GetCurrentStamina()
    {
        return _currentStamina;
    }
    
    public float GetMaxStamina()
    {
        return _maxStamina;
    }
    
    public bool IsRecovering()
    {
        return _isRecovering;
    }
    
    public bool IsEmpty()
    {
        return _currentStamina <= 0f;
    }
    
    public bool IsFull()
    {
        return _currentStamina >= _maxStamina;
    }
    
    public bool IsLow()
    {
        return _currentStamina <= _lowStaminaThreshold;
    }
}