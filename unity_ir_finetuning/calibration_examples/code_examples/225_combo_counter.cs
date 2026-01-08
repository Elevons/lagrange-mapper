// Prompt: combo counter
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class ComboCounter : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private int _maxCombo = 999;
    [SerializeField] private float _comboTimeWindow = 2f;
    [SerializeField] private int _minComboToDisplay = 2;
    
    [Header("UI References")]
    [SerializeField] private Text _comboText;
    [SerializeField] private GameObject _comboPanel;
    [SerializeField] private Animator _comboAnimator;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _comboParticles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _comboSound;
    [SerializeField] private AudioClip _comboBreakSound;
    
    [Header("Color Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _highComboColor = Color.yellow;
    [SerializeField] private Color _maxComboColor = Color.red;
    [SerializeField] private int _highComboThreshold = 10;
    [SerializeField] private int _maxComboThreshold = 50;
    
    [Header("Events")]
    public UnityEvent<int> OnComboChanged;
    public UnityEvent<int> OnComboBreak;
    public UnityEvent<int> OnHighCombo;
    
    private int _currentCombo = 0;
    private float _lastComboTime;
    private Coroutine _comboTimerCoroutine;
    private bool _isComboActive = false;
    
    public int CurrentCombo => _currentCombo;
    public bool IsComboActive => _isComboActive;
    
    private void Start()
    {
        InitializeCombo();
    }
    
    private void InitializeCombo()
    {
        _currentCombo = 0;
        _isComboActive = false;
        
        if (_comboPanel != null)
            _comboPanel.SetActive(false);
            
        UpdateComboDisplay();
    }
    
    public void AddCombo()
    {
        AddCombo(1);
    }
    
    public void AddCombo(int amount)
    {
        if (amount <= 0) return;
        
        _currentCombo = Mathf.Min(_currentCombo + amount, _maxCombo);
        _lastComboTime = Time.time;
        
        if (_currentCombo >= _minComboToDisplay && !_isComboActive)
        {
            _isComboActive = true;
            ShowComboUI();
        }
        
        UpdateComboDisplay();
        PlayComboEffects();
        
        OnComboChanged?.Invoke(_currentCombo);
        
        if (_currentCombo >= _highComboThreshold)
        {
            OnHighCombo?.Invoke(_currentCombo);
        }
        
        RestartComboTimer();
    }
    
    public void BreakCombo()
    {
        if (_currentCombo > 0)
        {
            int brokenCombo = _currentCombo;
            _currentCombo = 0;
            _isComboActive = false;
            
            HideComboUI();
            PlayComboBreakEffects();
            
            OnComboBreak?.Invoke(brokenCombo);
            OnComboChanged?.Invoke(_currentCombo);
        }
        
        StopComboTimer();
    }
    
    private void RestartComboTimer()
    {
        StopComboTimer();
        _comboTimerCoroutine = StartCoroutine(ComboTimerCoroutine());
    }
    
    private void StopComboTimer()
    {
        if (_comboTimerCoroutine != null)
        {
            StopCoroutine(_comboTimerCoroutine);
            _comboTimerCoroutine = null;
        }
    }
    
    private IEnumerator ComboTimerCoroutine()
    {
        while (Time.time - _lastComboTime < _comboTimeWindow)
        {
            yield return null;
        }
        
        BreakCombo();
    }
    
    private void UpdateComboDisplay()
    {
        if (_comboText != null)
        {
            _comboText.text = _currentCombo.ToString();
            _comboText.color = GetComboColor();
        }
    }
    
    private Color GetComboColor()
    {
        if (_currentCombo >= _maxComboThreshold)
            return _maxComboColor;
        else if (_currentCombo >= _highComboThreshold)
            return _highComboColor;
        else
            return _normalColor;
    }
    
    private void ShowComboUI()
    {
        if (_comboPanel != null)
        {
            _comboPanel.SetActive(true);
        }
        
        if (_comboAnimator != null)
        {
            _comboAnimator.SetTrigger("Show");
        }
    }
    
    private void HideComboUI()
    {
        if (_comboAnimator != null)
        {
            _comboAnimator.SetTrigger("Hide");
        }
        else if (_comboPanel != null)
        {
            _comboPanel.SetActive(false);
        }
    }
    
    private void PlayComboEffects()
    {
        if (_comboParticles != null && _currentCombo % 5 == 0)
        {
            _comboParticles.Play();
        }
        
        if (_audioSource != null && _comboSound != null)
        {
            float pitch = 1f + (_currentCombo * 0.02f);
            _audioSource.pitch = Mathf.Clamp(pitch, 0.8f, 2f);
            _audioSource.PlayOneShot(_comboSound);
        }
    }
    
    private void PlayComboBreakEffects()
    {
        if (_audioSource != null && _comboBreakSound != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(_comboBreakSound);
        }
    }
    
    public void ResetCombo()
    {
        _currentCombo = 0;
        _isComboActive = false;
        StopComboTimer();
        HideComboUI();
        UpdateComboDisplay();
        OnComboChanged?.Invoke(_currentCombo);
    }
    
    public void SetComboTimeWindow(float timeWindow)
    {
        _comboTimeWindow = Mathf.Max(0.1f, timeWindow);
    }
    
    public float GetRemainingTime()
    {
        if (!_isComboActive) return 0f;
        return Mathf.Max(0f, _comboTimeWindow - (Time.time - _lastComboTime));
    }
    
    public float GetComboTimeProgress()
    {
        if (!_isComboActive) return 0f;
        return GetRemainingTime() / _comboTimeWindow;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            AddCombo();
        }
    }
    
    private void OnDestroy()
    {
        StopComboTimer();
    }
}