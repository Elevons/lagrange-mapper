// Prompt: countdown timer display
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

public class CountdownTimer : MonoBehaviour
{
    [Header("Timer Settings")]
    [SerializeField] private float _startTime = 60f;
    [SerializeField] private bool _startOnAwake = true;
    [SerializeField] private bool _destroyOnComplete = false;
    
    [Header("Display Settings")]
    [SerializeField] private TextMeshProUGUI _timerText;
    [SerializeField] private Text _legacyTimerText;
    [SerializeField] private string _timeFormat = "mm:ss";
    [SerializeField] private string _completedText = "TIME'S UP!";
    
    [Header("Visual Effects")]
    [SerializeField] private bool _enableWarningEffect = true;
    [SerializeField] private float _warningThreshold = 10f;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _warningColor = Color.red;
    [SerializeField] private float _blinkSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _tickSound;
    [SerializeField] private AudioClip _warningSound;
    [SerializeField] private AudioClip _completeSound;
    [SerializeField] private bool _playTickEverySecond = false;
    
    [Header("Events")]
    public UnityEvent OnTimerStart;
    public UnityEvent OnTimerComplete;
    public UnityEvent OnWarningThreshold;
    public UnityEvent<float> OnTimerUpdate;
    
    private float _currentTime;
    private bool _isRunning = false;
    private bool _isCompleted = false;
    private bool _warningTriggered = false;
    private float _lastSecond = -1f;
    private bool _blinkState = false;
    private float _blinkTimer = 0f;
    
    public float CurrentTime => _currentTime;
    public float StartTime => _startTime;
    public bool IsRunning => _isRunning;
    public bool IsCompleted => _isCompleted;
    public float Progress => _startTime > 0 ? 1f - (_currentTime / _startTime) : 1f;
    
    private void Awake()
    {
        _currentTime = _startTime;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_timerText == null)
            _timerText = GetComponent<TextMeshProUGUI>();
            
        if (_legacyTimerText == null)
            _legacyTimerText = GetComponent<Text>();
    }
    
    private void Start()
    {
        UpdateDisplay();
        
        if (_startOnAwake)
            StartTimer();
    }
    
    private void Update()
    {
        if (!_isRunning || _isCompleted)
            return;
            
        _currentTime -= Time.deltaTime;
        
        if (_currentTime <= 0f)
        {
            _currentTime = 0f;
            CompleteTimer();
        }
        else
        {
            CheckWarningThreshold();
            CheckTickSound();
            UpdateBlinkEffect();
        }
        
        UpdateDisplay();
        OnTimerUpdate?.Invoke(_currentTime);
    }
    
    public void StartTimer()
    {
        if (_isCompleted)
            return;
            
        _isRunning = true;
        OnTimerStart?.Invoke();
    }
    
    public void PauseTimer()
    {
        _isRunning = false;
    }
    
    public void ResumeTimer()
    {
        if (!_isCompleted)
            _isRunning = true;
    }
    
    public void StopTimer()
    {
        _isRunning = false;
        _currentTime = _startTime;
        _isCompleted = false;
        _warningTriggered = false;
        _lastSecond = -1f;
        UpdateDisplay();
        ResetVisualEffects();
    }
    
    public void ResetTimer()
    {
        StopTimer();
    }
    
    public void SetTime(float newTime)
    {
        _startTime = newTime;
        if (!_isRunning)
        {
            _currentTime = _startTime;
            UpdateDisplay();
        }
    }
    
    public void AddTime(float additionalTime)
    {
        _currentTime += additionalTime;
        if (_currentTime > _startTime)
            _currentTime = _startTime;
    }
    
    private void CompleteTimer()
    {
        _isRunning = false;
        _isCompleted = true;
        
        PlaySound(_completeSound);
        OnTimerComplete?.Invoke();
        
        if (_destroyOnComplete)
            Destroy(gameObject);
    }
    
    private void CheckWarningThreshold()
    {
        if (!_warningTriggered && _currentTime <= _warningThreshold)
        {
            _warningTriggered = true;
            PlaySound(_warningSound);
            OnWarningThreshold?.Invoke();
        }
    }
    
    private void CheckTickSound()
    {
        if (!_playTickEverySecond)
            return;
            
        float currentSecond = Mathf.Floor(_currentTime);
        if (currentSecond != _lastSecond && currentSecond >= 0)
        {
            _lastSecond = currentSecond;
            PlaySound(_tickSound);
        }
    }
    
    private void UpdateBlinkEffect()
    {
        if (!_enableWarningEffect || !_warningTriggered)
            return;
            
        _blinkTimer += Time.deltaTime;
        if (_blinkTimer >= 1f / _blinkSpeed)
        {
            _blinkState = !_blinkState;
            _blinkTimer = 0f;
        }
    }
    
    private void UpdateDisplay()
    {
        string timeString = FormatTime(_currentTime);
        
        if (_isCompleted)
            timeString = _completedText;
            
        if (_timerText != null)
        {
            _timerText.text = timeString;
            _timerText.color = GetCurrentColor();
        }
        
        if (_legacyTimerText != null)
        {
            _legacyTimerText.text = timeString;
            _legacyTimerText.color = GetCurrentColor();
        }
    }
    
    private Color GetCurrentColor()
    {
        if (!_enableWarningEffect || !_warningTriggered)
            return _normalColor;
            
        return _blinkState ? _warningColor : _normalColor;
    }
    
    private void ResetVisualEffects()
    {
        _blinkState = false;
        _blinkTimer = 0f;
        
        if (_timerText != null)
            _timerText.color = _normalColor;
            
        if (_legacyTimerText != null)
            _legacyTimerText.color = _normalColor;
    }
    
    private string FormatTime(float timeInSeconds)
    {
        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        int milliseconds = Mathf.FloorToInt((timeInSeconds * 100f) % 100f);
        
        switch (_timeFormat.ToLower())
        {
            case "mm:ss":
                return string.Format("{0:00}:{1:00}", minutes, seconds);
            case "mm:ss.ff":
                return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, milliseconds);
            case "ss":
                return string.Format("{0:00}", Mathf.FloorToInt(timeInSeconds));
            case "ss.ff":
                return string.Format("{0:00}.{1:00}", Mathf.FloorToInt(timeInSeconds), milliseconds);
            case "m:ss":
                return string.Format("{0}:{1:00}", minutes, seconds);
            default:
                return string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }
    
    private void OnValidate()
    {
        if (_startTime < 0f)
            _startTime = 0f;
            
        if (_warningThreshold < 0f)
            _warningThreshold = 0f;
            
        if (_blinkSpeed <= 0f)
            _blinkSpeed = 1f;
    }
}