// Prompt: enemy health bar above head
// Type: combat

using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    [Header("UI Components")]
    [SerializeField] private Canvas _healthBarCanvas;
    [SerializeField] private Slider _healthSlider;
    [SerializeField] private Image _fillImage;
    [SerializeField] private Image _backgroundImage;
    
    [Header("Visual Settings")]
    [SerializeField] private Vector3 _offset = new Vector3(0, 2f, 0);
    [SerializeField] private Color _fullHealthColor = Color.green;
    [SerializeField] private Color _lowHealthColor = Color.red;
    [SerializeField] private float _lowHealthThreshold = 0.3f;
    [SerializeField] private bool _hideWhenFull = true;
    [SerializeField] private bool _alwaysFaceCamera = true;
    
    [Header("Animation")]
    [SerializeField] private float _smoothTime = 0.1f;
    [SerializeField] private AnimationCurve _damageCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Camera _mainCamera;
    private float _targetHealthPercentage;
    private float _displayHealthPercentage;
    private float _velocity;
    private bool _isAnimating;
    
    private void Awake()
    {
        _currentHealth = _maxHealth;
        _targetHealthPercentage = 1f;
        _displayHealthPercentage = 1f;
        
        CreateHealthBarUI();
    }
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        UpdateHealthBar();
    }
    
    private void Update()
    {
        if (_healthBarCanvas != null)
        {
            UpdatePosition();
            UpdateRotation();
            AnimateHealthBar();
        }
    }
    
    private void CreateHealthBarUI()
    {
        if (_healthBarCanvas != null) return;
        
        GameObject canvasGO = new GameObject("HealthBarCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = _offset;
        
        _healthBarCanvas = canvasGO.AddComponent<Canvas>();
        _healthBarCanvas.renderMode = RenderMode.WorldSpace;
        _healthBarCanvas.sortingOrder = 10;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantWorldSize;
        scaler.referencePixelsPerUnit = 100;
        
        RectTransform canvasRect = _healthBarCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(2f, 0.3f);
        
        GameObject sliderGO = new GameObject("HealthSlider");
        sliderGO.transform.SetParent(canvasGO.transform, false);
        
        _healthSlider = sliderGO.AddComponent<Slider>();
        _healthSlider.minValue = 0f;
        _healthSlider.maxValue = 1f;
        _healthSlider.value = 1f;
        
        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = Vector2.zero;
        sliderRect.anchorMax = Vector2.one;
        sliderRect.sizeDelta = Vector2.zero;
        sliderRect.anchoredPosition = Vector2.zero;
        
        GameObject backgroundGO = new GameObject("Background");
        backgroundGO.transform.SetParent(sliderGO.transform, false);
        _backgroundImage = backgroundGO.AddComponent<Image>();
        _backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        
        RectTransform bgRect = backgroundGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        bgRect.anchoredPosition = Vector2.zero;
        
        GameObject fillAreaGO = new GameObject("Fill Area");
        fillAreaGO.transform.SetParent(sliderGO.transform, false);
        
        RectTransform fillAreaRect = fillAreaGO.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.sizeDelta = Vector2.zero;
        fillAreaRect.anchoredPosition = Vector2.zero;
        
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(fillAreaGO.transform, false);
        _fillImage = fillGO.AddComponent<Image>();
        _fillImage.color = _fullHealthColor;
        _fillImage.type = Image.Type.Filled;
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        fillRect.anchoredPosition = Vector2.zero;
        
        _healthSlider.fillRect = fillRect;
    }
    
    private void UpdatePosition()
    {
        if (_healthBarCanvas != null)
        {
            _healthBarCanvas.transform.position = transform.position + _offset;
        }
    }
    
    private void UpdateRotation()
    {
        if (_alwaysFaceCamera && _mainCamera != null && _healthBarCanvas != null)
        {
            Vector3 directionToCamera = _mainCamera.transform.position - _healthBarCanvas.transform.position;
            _healthBarCanvas.transform.rotation = Quaternion.LookRotation(directionToCamera);
        }
    }
    
    private void AnimateHealthBar()
    {
        if (Mathf.Abs(_displayHealthPercentage - _targetHealthPercentage) > 0.01f)
        {
            _displayHealthPercentage = Mathf.SmoothDamp(_displayHealthPercentage, _targetHealthPercentage, ref _velocity, _smoothTime);
            
            if (_healthSlider != null)
                _healthSlider.value = _displayHealthPercentage;
                
            UpdateHealthBarColor();
        }
    }
    
    private void UpdateHealthBar()
    {
        _targetHealthPercentage = _currentHealth / _maxHealth;
        
        UpdateHealthBarColor();
        UpdateVisibility();
    }
    
    private void UpdateHealthBarColor()
    {
        if (_fillImage != null)
        {
            float healthPercentage = _displayHealthPercentage;
            
            if (healthPercentage <= _lowHealthThreshold)
            {
                float t = healthPercentage / _lowHealthThreshold;
                _fillImage.color = Color.Lerp(_lowHealthColor, _fullHealthColor, t);
            }
            else
            {
                _fillImage.color = _fullHealthColor;
            }
        }
    }
    
    private void UpdateVisibility()
    {
        if (_healthBarCanvas != null)
        {
            bool shouldShow = !_hideWhenFull || _currentHealth < _maxHealth;
            _healthBarCanvas.gameObject.SetActive(shouldShow);
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (damage <= 0) return;
        
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        UpdateHealthBar();
        
        if (_currentHealth <= 0)
        {
            OnDeath();
        }
    }
    
    public void Heal(float healAmount)
    {
        if (healAmount <= 0) return;
        
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healAmount);
        UpdateHealthBar();
    }
    
    public void SetHealth(float newHealth)
    {
        _currentHealth = Mathf.Clamp(newHealth, 0, _maxHealth);
        UpdateHealthBar();
        
        if (_currentHealth <= 0)
        {
            OnDeath();
        }
    }
    
    public void SetMaxHealth(float newMaxHealth)
    {
        float healthPercentage = _currentHealth / _maxHealth;
        _maxHealth = Mathf.Max(1f, newMaxHealth);
        _currentHealth = _maxHealth * healthPercentage;
        UpdateHealthBar();
    }
    
    private void OnDeath()
    {
        if (_healthBarCanvas != null)
        {
            _healthBarCanvas.gameObject.SetActive(false);
        }
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
        return _currentHealth / _maxHealth;
    }
    
    public bool IsAlive()
    {
        return _currentHealth > 0;
    }
    
    private void OnValidate()
    {
        if (_maxHealth <= 0)
            _maxHealth = 1f;
            
        _currentHealth = Mathf.Clamp(_currentHealth, 0, _maxHealth);
        
        if (Application.isPlaying)
            UpdateHealthBar();
    }
}