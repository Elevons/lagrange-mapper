// Prompt: experience bar with level up
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ExperienceBar : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider _experienceSlider;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _experienceText;
    [SerializeField] private GameObject _levelUpEffect;
    
    [Header("Experience Settings")]
    [SerializeField] private int _currentLevel = 1;
    [SerializeField] private float _currentExperience = 0f;
    [SerializeField] private float _baseExperienceRequired = 100f;
    [SerializeField] private float _experienceMultiplier = 1.5f;
    [SerializeField] private int _maxLevel = 99;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _normalBarColor = Color.blue;
    [SerializeField] private Color _levelUpBarColor = Color.gold;
    [SerializeField] private float _levelUpEffectDuration = 2f;
    [SerializeField] private AnimationCurve _levelUpAnimationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _levelUpSound;
    [SerializeField] private AudioClip _experienceGainSound;
    
    [Header("Events")]
    public UnityEvent<int> OnLevelUp;
    public UnityEvent<float> OnExperienceGained;
    
    private float _experienceRequiredForNextLevel;
    private bool _isLevelingUp = false;
    private float _levelUpTimer = 0f;
    private Image _sliderFillImage;
    
    private void Start()
    {
        InitializeComponents();
        CalculateExperienceRequired();
        UpdateUI();
    }
    
    private void Update()
    {
        if (_isLevelingUp)
        {
            HandleLevelUpAnimation();
        }
    }
    
    private void InitializeComponents()
    {
        if (_experienceSlider == null)
            _experienceSlider = GetComponentInChildren<Slider>();
            
        if (_experienceSlider != null)
        {
            _sliderFillImage = _experienceSlider.fillRect.GetComponent<Image>();
            if (_sliderFillImage != null)
                _sliderFillImage.color = _normalBarColor;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_levelUpEffect != null)
            _levelUpEffect.SetActive(false);
    }
    
    private void CalculateExperienceRequired()
    {
        _experienceRequiredForNextLevel = _baseExperienceRequired * Mathf.Pow(_experienceMultiplier, _currentLevel - 1);
    }
    
    public void AddExperience(float amount)
    {
        if (_currentLevel >= _maxLevel) return;
        
        _currentExperience += amount;
        
        PlayExperienceGainSound();
        OnExperienceGained?.Invoke(amount);
        
        CheckForLevelUp();
        UpdateUI();
    }
    
    private void CheckForLevelUp()
    {
        while (_currentExperience >= _experienceRequiredForNextLevel && _currentLevel < _maxLevel)
        {
            LevelUp();
        }
    }
    
    private void LevelUp()
    {
        _currentExperience -= _experienceRequiredForNextLevel;
        _currentLevel++;
        
        CalculateExperienceRequired();
        StartLevelUpEffect();
        
        PlayLevelUpSound();
        OnLevelUp?.Invoke(_currentLevel);
    }
    
    private void StartLevelUpEffect()
    {
        _isLevelingUp = true;
        _levelUpTimer = 0f;
        
        if (_levelUpEffect != null)
            _levelUpEffect.SetActive(true);
            
        if (_sliderFillImage != null)
            _sliderFillImage.color = _levelUpBarColor;
    }
    
    private void HandleLevelUpAnimation()
    {
        _levelUpTimer += Time.deltaTime;
        float progress = _levelUpTimer / _levelUpEffectDuration;
        
        if (_sliderFillImage != null)
        {
            float animationValue = _levelUpAnimationCurve.Evaluate(progress);
            _sliderFillImage.color = Color.Lerp(_levelUpBarColor, _normalBarColor, animationValue);
        }
        
        if (progress >= 1f)
        {
            _isLevelingUp = false;
            
            if (_levelUpEffect != null)
                _levelUpEffect.SetActive(false);
                
            if (_sliderFillImage != null)
                _sliderFillImage.color = _normalBarColor;
        }
    }
    
    private void UpdateUI()
    {
        if (_experienceSlider != null)
        {
            if (_currentLevel >= _maxLevel)
            {
                _experienceSlider.value = 1f;
            }
            else
            {
                _experienceSlider.value = _currentExperience / _experienceRequiredForNextLevel;
            }
        }
        
        if (_levelText != null)
        {
            _levelText.text = "Level " + _currentLevel.ToString();
        }
        
        if (_experienceText != null)
        {
            if (_currentLevel >= _maxLevel)
            {
                _experienceText.text = "MAX LEVEL";
            }
            else
            {
                _experienceText.text = $"{_currentExperience:F0} / {_experienceRequiredForNextLevel:F0}";
            }
        }
    }
    
    private void PlayLevelUpSound()
    {
        if (_audioSource != null && _levelUpSound != null)
        {
            _audioSource.PlayOneShot(_levelUpSound);
        }
    }
    
    private void PlayExperienceGainSound()
    {
        if (_audioSource != null && _experienceGainSound != null)
        {
            _audioSource.PlayOneShot(_experienceGainSound, 0.5f);
        }
    }
    
    public void SetLevel(int level)
    {
        _currentLevel = Mathf.Clamp(level, 1, _maxLevel);
        _currentExperience = 0f;
        CalculateExperienceRequired();
        UpdateUI();
    }
    
    public void SetExperience(float experience)
    {
        _currentExperience = Mathf.Max(0f, experience);
        CheckForLevelUp();
        UpdateUI();
    }
    
    public int GetCurrentLevel()
    {
        return _currentLevel;
    }
    
    public float GetCurrentExperience()
    {
        return _currentExperience;
    }
    
    public float GetExperienceRequiredForNextLevel()
    {
        return _experienceRequiredForNextLevel;
    }
    
    public float GetExperienceProgress()
    {
        if (_currentLevel >= _maxLevel) return 1f;
        return _currentExperience / _experienceRequiredForNextLevel;
    }
    
    public bool IsMaxLevel()
    {
        return _currentLevel >= _maxLevel;
    }
}