// Prompt: score multiplier indicator
// Type: general

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ScoreMultiplierIndicator : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _multiplierText;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _fillImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Multiplier Settings")]
    [SerializeField] private float _baseMultiplier = 1.0f;
    [SerializeField] private float _maxMultiplier = 10.0f;
    [SerializeField] private float _multiplierDecayRate = 0.5f;
    [SerializeField] private float _multiplierIncrement = 0.5f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _lowMultiplierColor = Color.white;
    [SerializeField] private Color _highMultiplierColor = Color.red;
    [SerializeField] private AnimationCurve _pulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 1.2f);
    [SerializeField] private float _pulseDuration = 0.3f;
    
    [Header("Animation Settings")]
    [SerializeField] private float _fadeInDuration = 0.5f;
    [SerializeField] private float _fadeOutDuration = 1.0f;
    [SerializeField] private float _hideDelay = 2.0f;
    
    private float _currentMultiplier;
    private float _multiplierTimer;
    private bool _isVisible;
    private Coroutine _hideCoroutine;
    private Coroutine _pulseCoroutine;
    private Vector3 _originalScale;
    
    public float CurrentMultiplier => _currentMultiplier;
    
    private void Start()
    {
        InitializeComponents();
        _currentMultiplier = _baseMultiplier;
        _originalScale = transform.localScale;
        UpdateDisplay();
        SetVisibility(false, true);
    }
    
    private void Update()
    {
        UpdateMultiplierDecay();
    }
    
    private void InitializeComponents()
    {
        if (_multiplierText == null)
            _multiplierText = GetComponentInChildren<TextMeshProUGUI>();
            
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
            
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    private void UpdateMultiplierDecay()
    {
        if (_currentMultiplier > _baseMultiplier)
        {
            _multiplierTimer += Time.deltaTime;
            
            if (_multiplierTimer >= 1.0f)
            {
                _multiplierTimer = 0f;
                _currentMultiplier = Mathf.Max(_baseMultiplier, _currentMultiplier - _multiplierDecayRate);
                UpdateDisplay();
                
                if (_currentMultiplier <= _baseMultiplier)
                {
                    HideIndicator();
                }
            }
        }
    }
    
    public void AddMultiplier()
    {
        AddMultiplier(_multiplierIncrement);
    }
    
    public void AddMultiplier(float amount)
    {
        _currentMultiplier = Mathf.Min(_maxMultiplier, _currentMultiplier + amount);
        _multiplierTimer = 0f;
        
        UpdateDisplay();
        ShowIndicator();
        TriggerPulseEffect();
    }
    
    public void ResetMultiplier()
    {
        _currentMultiplier = _baseMultiplier;
        _multiplierTimer = 0f;
        UpdateDisplay();
        HideIndicator();
    }
    
    private void UpdateDisplay()
    {
        if (_multiplierText != null)
        {
            _multiplierText.text = $"x{_currentMultiplier:F1}";
        }
        
        UpdateColors();
        UpdateFillAmount();
    }
    
    private void UpdateColors()
    {
        float normalizedMultiplier = (_currentMultiplier - _baseMultiplier) / (_maxMultiplier - _baseMultiplier);
        Color currentColor = Color.Lerp(_lowMultiplierColor, _highMultiplierColor, normalizedMultiplier);
        
        if (_multiplierText != null)
            _multiplierText.color = currentColor;
            
        if (_backgroundImage != null)
            _backgroundImage.color = new Color(currentColor.r, currentColor.g, currentColor.b, 0.3f);
    }
    
    private void UpdateFillAmount()
    {
        if (_fillImage != null)
        {
            float fillAmount = (_currentMultiplier - _baseMultiplier) / (_maxMultiplier - _baseMultiplier);
            _fillImage.fillAmount = fillAmount;
        }
    }
    
    private void ShowIndicator()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
        
        if (!_isVisible)
        {
            StartCoroutine(FadeIn());
        }
        
        _hideCoroutine = StartCoroutine(HideAfterDelay());
    }
    
    private void HideIndicator()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }
        
        if (_isVisible)
        {
            StartCoroutine(FadeOut());
        }
    }
    
    private IEnumerator FadeIn()
    {
        _isVisible = true;
        float elapsedTime = 0f;
        float startAlpha = _canvasGroup.alpha;
        
        while (elapsedTime < _fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _fadeInDuration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
            yield return null;
        }
        
        _canvasGroup.alpha = 1f;
    }
    
    private IEnumerator FadeOut()
    {
        float elapsedTime = 0f;
        float startAlpha = _canvasGroup.alpha;
        
        while (elapsedTime < _fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _fadeOutDuration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }
        
        _canvasGroup.alpha = 0f;
        _isVisible = false;
    }
    
    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(_hideDelay);
        
        if (_currentMultiplier <= _baseMultiplier)
        {
            HideIndicator();
        }
    }
    
    private void SetVisibility(bool visible, bool immediate = false)
    {
        _isVisible = visible;
        
        if (immediate)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
        }
    }
    
    private void TriggerPulseEffect()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
        }
        
        _pulseCoroutine = StartCoroutine(PulseAnimation());
    }
    
    private IEnumerator PulseAnimation()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _pulseDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _pulseDuration;
            float scaleMultiplier = _pulseCurve.Evaluate(progress);
            transform.localScale = _originalScale * scaleMultiplier;
            yield return null;
        }
        
        transform.localScale = _originalScale;
        _pulseCoroutine = null;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            AddMultiplier();
        }
    }
    
    private void OnDestroy()
    {
        if (_hideCoroutine != null)
            StopCoroutine(_hideCoroutine);
            
        if (_pulseCoroutine != null)
            StopCoroutine(_pulseCoroutine);
    }
}