// Prompt: respawn countdown
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class RespawnCountdown : MonoBehaviour
{
    [Header("Countdown Settings")]
    [SerializeField] private float _countdownDuration = 5f;
    [SerializeField] private bool _startOnAwake = false;
    [SerializeField] private bool _hideUIWhenNotActive = true;
    
    [Header("UI References")]
    [SerializeField] private Text _countdownText;
    [SerializeField] private Image _countdownFillImage;
    [SerializeField] private GameObject _countdownPanel;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Visual Effects")]
    [SerializeField] private bool _enablePulseEffect = true;
    [SerializeField] private AnimationCurve _pulseCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 1.2f);
    [SerializeField] private Color _startColor = Color.red;
    [SerializeField] private Color _endColor = Color.green;
    [SerializeField] private bool _enableColorTransition = true;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _tickSound;
    [SerializeField] private AudioClip _completeSound;
    [SerializeField] private bool _playTickOnEachSecond = true;
    
    [Header("Events")]
    public UnityEvent OnCountdownStart;
    public UnityEvent OnCountdownTick;
    public UnityEvent OnCountdownComplete;
    public UnityEvent OnCountdownCancelled;
    
    private float _currentTime;
    private bool _isCountingDown = false;
    private Coroutine _countdownCoroutine;
    private Vector3 _originalScale;
    private int _lastSecond = -1;
    
    private void Awake()
    {
        if (_countdownText != null)
            _originalScale = _countdownText.transform.localScale;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetUIVisibility(false);
    }
    
    private void Start()
    {
        if (_startOnAwake)
            StartCountdown();
    }
    
    public void StartCountdown()
    {
        StartCountdown(_countdownDuration);
    }
    
    public void StartCountdown(float duration)
    {
        if (_isCountingDown)
            StopCountdown();
            
        _countdownDuration = duration;
        _currentTime = _countdownDuration;
        _isCountingDown = true;
        _lastSecond = -1;
        
        SetUIVisibility(true);
        OnCountdownStart?.Invoke();
        
        _countdownCoroutine = StartCoroutine(CountdownRoutine());
    }
    
    public void StopCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
        
        _isCountingDown = false;
        SetUIVisibility(false);
        OnCountdownCancelled?.Invoke();
    }
    
    public void PauseCountdown()
    {
        if (_countdownCoroutine != null)
        {
            StopCoroutine(_countdownCoroutine);
            _countdownCoroutine = null;
        }
    }
    
    public void ResumeCountdown()
    {
        if (_isCountingDown && _countdownCoroutine == null)
        {
            _countdownCoroutine = StartCoroutine(CountdownRoutine());
        }
    }
    
    private IEnumerator CountdownRoutine()
    {
        while (_currentTime > 0f)
        {
            UpdateUI();
            CheckForSecondTick();
            
            _currentTime -= Time.deltaTime;
            yield return null;
        }
        
        _currentTime = 0f;
        _isCountingDown = false;
        UpdateUI();
        
        PlaySound(_completeSound);
        OnCountdownComplete?.Invoke();
        
        if (_hideUIWhenNotActive)
        {
            yield return new WaitForSeconds(0.5f);
            SetUIVisibility(false);
        }
    }
    
    private void UpdateUI()
    {
        float normalizedTime = _currentTime / _countdownDuration;
        
        if (_countdownText != null)
        {
            int displaySeconds = Mathf.CeilToInt(_currentTime);
            _countdownText.text = displaySeconds.ToString();
            
            if (_enableColorTransition)
            {
                _countdownText.color = Color.Lerp(_endColor, _startColor, normalizedTime);
            }
            
            if (_enablePulseEffect)
            {
                float pulseValue = _pulseCurve.Evaluate(Time.time % 1f);
                _countdownText.transform.localScale = _originalScale * pulseValue;
            }
        }
        
        if (_countdownFillImage != null)
        {
            _countdownFillImage.fillAmount = normalizedTime;
            
            if (_enableColorTransition)
            {
                _countdownFillImage.color = Color.Lerp(_endColor, _startColor, normalizedTime);
            }
        }
        
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.Lerp(0.5f, 1f, normalizedTime);
        }
    }
    
    private void CheckForSecondTick()
    {
        int currentSecond = Mathf.CeilToInt(_currentTime);
        
        if (currentSecond != _lastSecond && currentSecond > 0)
        {
            _lastSecond = currentSecond;
            OnCountdownTick?.Invoke();
            
            if (_playTickOnEachSecond)
                PlaySound(_tickSound);
        }
    }
    
    private void SetUIVisibility(bool visible)
    {
        if (_countdownPanel != null)
            _countdownPanel.SetActive(visible);
        else if (_countdownText != null)
            _countdownText.gameObject.SetActive(visible);
            
        if (_canvasGroup != null)
            _canvasGroup.alpha = visible ? 1f : 0f;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public float GetRemainingTime()
    {
        return _currentTime;
    }
    
    public float GetNormalizedTime()
    {
        return _countdownDuration > 0 ? _currentTime / _countdownDuration : 0f;
    }
    
    public bool IsCountingDown()
    {
        return _isCountingDown;
    }
    
    public void AddTime(float additionalTime)
    {
        _currentTime += additionalTime;
        _currentTime = Mathf.Max(0f, _currentTime);
    }
    
    public void SetRemainingTime(float time)
    {
        _currentTime = Mathf.Max(0f, time);
    }
    
    private void OnValidate()
    {
        _countdownDuration = Mathf.Max(0.1f, _countdownDuration);
    }
}