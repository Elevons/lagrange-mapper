// Prompt: low health warning flash
// Type: pickup

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class LowHealthWarning : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth = 100f;
    [SerializeField] private float _lowHealthThreshold = 25f;
    
    [Header("Flash Settings")]
    [SerializeField] private Image _warningOverlay;
    [SerializeField] private Color _flashColor = new Color(1f, 0f, 0f, 0.3f);
    [SerializeField] private float _flashSpeed = 2f;
    [SerializeField] private AnimationCurve _flashCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _warningSound;
    [SerializeField] private float _soundInterval = 2f;
    
    [Header("Events")]
    public UnityEvent OnLowHealthStart;
    public UnityEvent OnLowHealthEnd;
    public UnityEvent OnHealthChanged;
    
    private bool _isLowHealth = false;
    private float _flashTimer = 0f;
    private float _soundTimer = 0f;
    private Color _originalColor;
    private Canvas _warningCanvas;
    
    private void Start()
    {
        InitializeComponents();
        UpdateHealthState();
    }
    
    private void Update()
    {
        if (_isLowHealth)
        {
            UpdateFlashEffect();
            UpdateWarningSound();
        }
    }
    
    private void InitializeComponents()
    {
        if (_warningOverlay == null)
        {
            CreateWarningOverlay();
        }
        
        _originalColor = _warningOverlay.color;
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
    }
    
    private void CreateWarningOverlay()
    {
        GameObject canvasGO = new GameObject("LowHealthWarningCanvas");
        canvasGO.transform.SetParent(transform);
        
        _warningCanvas = canvasGO.AddComponent<Canvas>();
        _warningCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _warningCanvas.sortingOrder = 100;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        
        GameObject overlayGO = new GameObject("WarningOverlay");
        overlayGO.transform.SetParent(canvasGO.transform, false);
        
        _warningOverlay = overlayGO.AddComponent<Image>();
        _warningOverlay.color = Color.clear;
        
        RectTransform rectTransform = _warningOverlay.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }
    
    private void UpdateFlashEffect()
    {
        _flashTimer += Time.deltaTime * _flashSpeed;
        
        float curveValue = _flashCurve.Evaluate(Mathf.PingPong(_flashTimer, 1f));
        Color targetColor = Color.Lerp(Color.clear, _flashColor, curveValue);
        
        _warningOverlay.color = targetColor;
    }
    
    private void UpdateWarningSound()
    {
        if (_warningSound == null || _audioSource == null) return;
        
        _soundTimer += Time.deltaTime;
        
        if (_soundTimer >= _soundInterval)
        {
            _audioSource.PlayOneShot(_warningSound);
            _soundTimer = 0f;
        }
    }
    
    private void UpdateHealthState()
    {
        bool shouldShowWarning = _currentHealth <= _lowHealthThreshold && _currentHealth > 0f;
        
        if (shouldShowWarning && !_isLowHealth)
        {
            StartLowHealthWarning();
        }
        else if (!shouldShowWarning && _isLowHealth)
        {
            StopLowHealthWarning();
        }
        
        OnHealthChanged?.Invoke();
    }
    
    private void StartLowHealthWarning()
    {
        _isLowHealth = true;
        _flashTimer = 0f;
        _soundTimer = 0f;
        
        if (_warningCanvas != null)
        {
            _warningCanvas.gameObject.SetActive(true);
        }
        
        OnLowHealthStart?.Invoke();
    }
    
    private void StopLowHealthWarning()
    {
        _isLowHealth = false;
        
        if (_warningOverlay != null)
        {
            _warningOverlay.color = Color.clear;
        }
        
        if (_warningCanvas != null)
        {
            _warningCanvas.gameObject.SetActive(false);
        }
        
        OnLowHealthEnd?.Invoke();
    }
    
    public void TakeDamage(float damage)
    {
        if (damage < 0f) return;
        
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        UpdateHealthState();
    }
    
    public void Heal(float healAmount)
    {
        if (healAmount < 0f) return;
        
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healAmount);
        UpdateHealthState();
    }
    
    public void SetHealth(float health)
    {
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        UpdateHealthState();
    }
    
    public void SetMaxHealth(float maxHealth)
    {
        if (maxHealth <= 0f) return;
        
        float healthPercentage = _currentHealth / _maxHealth;
        _maxHealth = maxHealth;
        _currentHealth = _maxHealth * healthPercentage;
        UpdateHealthState();
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
        return _maxHealth > 0f ? _currentHealth / _maxHealth : 0f;
    }
    
    public bool IsLowHealth()
    {
        return _isLowHealth;
    }
    
    private void OnValidate()
    {
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        _lowHealthThreshold = Mathf.Clamp(_lowHealthThreshold, 0f, _maxHealth);
        _flashSpeed = Mathf.Max(0.1f, _flashSpeed);
        _soundInterval = Mathf.Max(0.1f, _soundInterval);
    }
}