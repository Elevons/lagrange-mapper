// Prompt: mana bar for abilities
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ManaBar : MonoBehaviour
{
    [Header("Mana Settings")]
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _currentMana = 100f;
    [SerializeField] private float _manaRegenRate = 5f;
    [SerializeField] private float _manaRegenDelay = 2f;
    
    [Header("UI Components")]
    [SerializeField] private Slider _manaSlider;
    [SerializeField] private Image _manaFillImage;
    [SerializeField] private Text _manaText;
    [SerializeField] private Image _backgroundImage;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _fullManaColor = Color.blue;
    [SerializeField] private Color _lowManaColor = Color.red;
    [SerializeField] private Color _emptyManaColor = Color.gray;
    [SerializeField] private float _lowManaThreshold = 0.25f;
    [SerializeField] private bool _showManaText = true;
    [SerializeField] private bool _hideWhenFull = false;
    
    [Header("Animation")]
    [SerializeField] private bool _smoothTransition = true;
    [SerializeField] private float _transitionSpeed = 5f;
    [SerializeField] private bool _pulseOnLowMana = true;
    [SerializeField] private float _pulseSpeed = 2f;
    
    [Header("Events")]
    public UnityEvent OnManaEmpty;
    public UnityEvent OnManaFull;
    public UnityEvent<float> OnManaChanged;
    
    private float _targetManaValue;
    private float _lastManaUseTime;
    private bool _wasEmpty;
    private bool _wasFull;
    private CanvasGroup _canvasGroup;
    
    public float CurrentMana => _currentMana;
    public float MaxMana => _maxMana;
    public float ManaPercentage => _currentMana / _maxMana;
    public bool HasMana => _currentMana > 0f;
    public bool IsFullMana => _currentMana >= _maxMana;
    
    private void Awake()
    {
        InitializeComponents();
        _targetManaValue = _currentMana;
        _lastManaUseTime = -_manaRegenDelay;
    }
    
    private void Start()
    {
        UpdateManaDisplay();
        UpdateVisibility();
    }
    
    private void Update()
    {
        HandleManaRegeneration();
        HandleSmoothTransition();
        HandleLowManaPulse();
        UpdateManaDisplay();
        UpdateVisibility();
        CheckManaEvents();
    }
    
    private void InitializeComponents()
    {
        if (_manaSlider == null)
            _manaSlider = GetComponentInChildren<Slider>();
            
        if (_manaFillImage == null && _manaSlider != null)
            _manaFillImage = _manaSlider.fillRect?.GetComponent<Image>();
            
        if (_manaText == null)
            _manaText = GetComponentInChildren<Text>();
            
        if (_backgroundImage == null)
            _backgroundImage = GetComponent<Image>();
            
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null && _hideWhenFull)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        if (_manaSlider != null)
        {
            _manaSlider.minValue = 0f;
            _manaSlider.maxValue = _maxMana;
            _manaSlider.wholeNumbers = false;
        }
    }
    
    private void HandleManaRegeneration()
    {
        if (_currentMana < _maxMana && Time.time >= _lastManaUseTime + _manaRegenDelay)
        {
            _currentMana += _manaRegenRate * Time.deltaTime;
            _currentMana = Mathf.Clamp(_currentMana, 0f, _maxMana);
            _targetManaValue = _currentMana;
        }
    }
    
    private void HandleSmoothTransition()
    {
        if (_smoothTransition && _manaSlider != null)
        {
            float currentSliderValue = _manaSlider.value;
            float targetValue = _smoothTransition ? _targetManaValue : _currentMana;
            
            if (Mathf.Abs(currentSliderValue - targetValue) > 0.01f)
            {
                _manaSlider.value = Mathf.Lerp(currentSliderValue, targetValue, _transitionSpeed * Time.deltaTime);
            }
            else
            {
                _manaSlider.value = targetValue;
            }
        }
        else if (_manaSlider != null)
        {
            _manaSlider.value = _currentMana;
        }
    }
    
    private void HandleLowManaPulse()
    {
        if (_pulseOnLowMana && ManaPercentage <= _lowManaThreshold && _manaFillImage != null)
        {
            float alpha = 0.5f + 0.5f * Mathf.Sin(Time.time * _pulseSpeed);
            Color currentColor = _manaFillImage.color;
            currentColor.a = alpha;
            _manaFillImage.color = currentColor;
        }
        else if (_manaFillImage != null)
        {
            Color currentColor = _manaFillImage.color;
            currentColor.a = 1f;
            _manaFillImage.color = currentColor;
        }
    }
    
    private void UpdateManaDisplay()
    {
        UpdateManaColor();
        UpdateManaText();
    }
    
    private void UpdateManaColor()
    {
        if (_manaFillImage == null) return;
        
        Color targetColor;
        float percentage = ManaPercentage;
        
        if (percentage <= 0f)
        {
            targetColor = _emptyManaColor;
        }
        else if (percentage <= _lowManaThreshold)
        {
            targetColor = Color.Lerp(_emptyManaColor, _lowManaColor, percentage / _lowManaThreshold);
        }
        else
        {
            float normalizedHigh = (percentage - _lowManaThreshold) / (1f - _lowManaThreshold);
            targetColor = Color.Lerp(_lowManaColor, _fullManaColor, normalizedHigh);
        }
        
        if (!_pulseOnLowMana || percentage > _lowManaThreshold)
        {
            _manaFillImage.color = targetColor;
        }
        else
        {
            Color pulseColor = targetColor;
            pulseColor.a = _manaFillImage.color.a;
            _manaFillImage.color = pulseColor;
        }
    }
    
    private void UpdateManaText()
    {
        if (_manaText != null && _showManaText)
        {
            _manaText.text = $"{Mathf.Ceil(_currentMana)}/{_maxMana}";
        }
        else if (_manaText != null)
        {
            _manaText.text = "";
        }
    }
    
    private void UpdateVisibility()
    {
        if (_canvasGroup != null && _hideWhenFull)
        {
            float targetAlpha = IsFullMana ? 0f : 1f;
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, Time.deltaTime * 3f);
        }
    }
    
    private void CheckManaEvents()
    {
        bool isEmpty = _currentMana <= 0f;
        bool isFull = _currentMana >= _maxMana;
        
        if (isEmpty && !_wasEmpty)
        {
            OnManaEmpty?.Invoke();
            _wasEmpty = true;
        }
        else if (!isEmpty)
        {
            _wasEmpty = false;
        }
        
        if (isFull && !_wasFull)
        {
            OnManaFull?.Invoke();
            _wasFull = true;
        }
        else if (!isFull)
        {
            _wasFull = false;
        }
        
        OnManaChanged?.Invoke(_currentMana);
    }
    
    public bool TryUseMana(float amount)
    {
        if (_currentMana >= amount)
        {
            UseMana(amount);
            return true;
        }
        return false;
    }
    
    public void UseMana(float amount)
    {
        _currentMana -= amount;
        _currentMana = Mathf.Clamp(_currentMana, 0f, _maxMana);
        _targetManaValue = _currentMana;
        _lastManaUseTime = Time.time;
    }
    
    public void RestoreMana(float amount)
    {
        _currentMana += amount;
        _currentMana = Mathf.Clamp(_currentMana, 0f, _maxMana);
        _targetManaValue = _currentMana;
    }
    
    public void SetMana(float amount)
    {
        _currentMana = Mathf.Clamp(amount, 0f, _maxMana);
        _targetManaValue = _currentMana;
    }
    
    public void SetMaxMana(float newMaxMana)
    {
        float percentage = ManaPercentage;
        _maxMana = Mathf.Max(1f, newMaxMana);
        _currentMana = _maxMana * percentage;
        _targetManaValue = _currentMana;
        
        if (_manaSlider != null)
        {
            _manaSlider.maxValue = _maxMana;
        }
    }
    
    public void RefillMana()
    {
        _currentMana = _maxMana;
        _targetManaValue = _currentMana;
    }
    
    public void EmptyMana()
    {
        _currentMana = 0f;
        _targetManaValue = _currentMana;
        _lastManaUseTime = Time.time;
    }
}