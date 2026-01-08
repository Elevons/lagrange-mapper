// Prompt: healing meditation
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class HealingMeditation : MonoBehaviour
{
    [System.Serializable]
    public class HealingEvent : UnityEvent<float> { }
    
    [Header("Meditation Settings")]
    [SerializeField] private float _healingAmount = 25f;
    [SerializeField] private float _meditationDuration = 5f;
    [SerializeField] private float _cooldownTime = 10f;
    [SerializeField] private float _detectionRadius = 2f;
    [SerializeField] private KeyCode _meditationKey = KeyCode.E;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _healingParticles;
    [SerializeField] private Light _meditationLight;
    [SerializeField] private Color _healingColor = Color.green;
    [SerializeField] private AnimationCurve _healingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _meditationStartSound;
    [SerializeField] private AudioClip _healingCompleteSound;
    [SerializeField] private AudioClip _ambientMeditationLoop;
    
    [Header("UI")]
    [SerializeField] private Canvas _meditationUI;
    [SerializeField] private UnityEngine.UI.Slider _progressSlider;
    [SerializeField] private UnityEngine.UI.Text _instructionText;
    [SerializeField] private UnityEngine.UI.Text _cooldownText;
    
    [Header("Events")]
    public HealingEvent OnHealingComplete;
    public UnityEvent OnMeditationStart;
    public UnityEvent OnMeditationInterrupted;
    
    private bool _isMeditating = false;
    private bool _isOnCooldown = false;
    private float _meditationProgress = 0f;
    private float _cooldownTimer = 0f;
    private GameObject _currentPlayer;
    private Coroutine _meditationCoroutine;
    private Vector3 _originalLightIntensity;
    private Color _originalLightColor;
    
    private void Start()
    {
        InitializeComponents();
        SetupInitialState();
    }
    
    private void InitializeComponents()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_meditationLight != null)
        {
            _originalLightIntensity = new Vector3(_meditationLight.intensity, 0, 0);
            _originalLightColor = _meditationLight.color;
        }
        
        if (OnHealingComplete == null)
            OnHealingComplete = new HealingEvent();
    }
    
    private void SetupInitialState()
    {
        if (_meditationUI != null)
            _meditationUI.gameObject.SetActive(false);
            
        if (_healingParticles != null)
            _healingParticles.SetActive(false);
            
        if (_meditationLight != null)
            _meditationLight.enabled = false;
    }
    
    private void Update()
    {
        HandleCooldown();
        HandlePlayerInput();
        UpdateUI();
    }
    
    private void HandleCooldown()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                _cooldownTimer = 0f;
            }
        }
    }
    
    private void HandlePlayerInput()
    {
        if (_currentPlayer != null && !_isOnCooldown)
        {
            if (Input.GetKeyDown(_meditationKey) && !_isMeditating)
            {
                StartMeditation();
            }
            else if (Input.GetKeyUp(_meditationKey) && _isMeditating)
            {
                InterruptMeditation();
            }
        }
    }
    
    private void UpdateUI()
    {
        if (_meditationUI != null)
        {
            bool shouldShowUI = _currentPlayer != null && !_isOnCooldown;
            _meditationUI.gameObject.SetActive(shouldShowUI);
            
            if (shouldShowUI)
            {
                if (_progressSlider != null)
                    _progressSlider.value = _meditationProgress;
                    
                if (_instructionText != null)
                {
                    if (_isMeditating)
                        _instructionText.text = "Hold " + _meditationKey + " to meditate...";
                    else
                        _instructionText.text = "Press " + _meditationKey + " to start meditation";
                }
            }
        }
        
        if (_cooldownText != null)
        {
            if (_isOnCooldown)
            {
                _cooldownText.gameObject.SetActive(true);
                _cooldownText.text = "Cooldown: " + Mathf.Ceil(_cooldownTimer).ToString() + "s";
            }
            else
            {
                _cooldownText.gameObject.SetActive(false);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _currentPlayer = other.gameObject;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject == _currentPlayer)
        {
            _currentPlayer = null;
            if (_isMeditating)
                InterruptMeditation();
        }
    }
    
    private void StartMeditation()
    {
        if (_isMeditating || _isOnCooldown || _currentPlayer == null)
            return;
            
        _isMeditating = true;
        _meditationProgress = 0f;
        
        OnMeditationStart?.Invoke();
        
        if (_audioSource != null && _meditationStartSound != null)
            _audioSource.PlayOneShot(_meditationStartSound);
            
        if (_healingParticles != null)
            _healingParticles.SetActive(true);
            
        if (_meditationLight != null)
        {
            _meditationLight.enabled = true;
            _meditationLight.color = _healingColor;
        }
        
        _meditationCoroutine = StartCoroutine(MeditationProcess());
        
        if (_audioSource != null && _ambientMeditationLoop != null)
        {
            _audioSource.clip = _ambientMeditationLoop;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void InterruptMeditation()
    {
        if (!_isMeditating)
            return;
            
        _isMeditating = false;
        
        if (_meditationCoroutine != null)
        {
            StopCoroutine(_meditationCoroutine);
            _meditationCoroutine = null;
        }
        
        OnMeditationInterrupted?.Invoke();
        ResetMeditationEffects();
    }
    
    private IEnumerator MeditationProcess()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _meditationDuration)
        {
            if (!Input.GetKey(_meditationKey))
            {
                InterruptMeditation();
                yield break;
            }
            
            elapsedTime += Time.deltaTime;
            _meditationProgress = elapsedTime / _meditationDuration;
            
            UpdateMeditationEffects(_meditationProgress);
            
            yield return null;
        }
        
        CompleteMeditation();
    }
    
    private void UpdateMeditationEffects(float progress)
    {
        float curveValue = _healingCurve.Evaluate(progress);
        
        if (_meditationLight != null)
        {
            _meditationLight.intensity = _originalLightIntensity.x * curveValue;
        }
    }
    
    private void CompleteMeditation()
    {
        _isMeditating = false;
        _meditationProgress = 1f;
        
        if (_audioSource != null && _healingCompleteSound != null)
            _audioSource.PlayOneShot(_healingCompleteSound);
            
        OnHealingComplete?.Invoke(_healingAmount);
        
        StartCooldown();
        ResetMeditationEffects();
    }
    
    private void ResetMeditationEffects()
    {
        if (_healingParticles != null)
            _healingParticles.SetActive(false);
            
        if (_meditationLight != null)
        {
            _meditationLight.enabled = false;
            _meditationLight.color = _originalLightColor;
            _meditationLight.intensity = _originalLightIntensity.x;
        }
        
        if (_audioSource != null)
        {
            _audioSource.loop = false;
            _audioSource.Stop();
        }
        
        _meditationProgress = 0f;
    }
    
    private void StartCooldown()
    {
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _healingColor;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
    }
    
    private void OnValidate()
    {
        if (_healingAmount < 0f)
            _healingAmount = 0f;
            
        if (_meditationDuration < 0.1f)
            _meditationDuration = 0.1f;
            
        if (_cooldownTime < 0f)
            _cooldownTime = 0f;
            
        if (_detectionRadius < 0.1f)
            _detectionRadius = 0.1f;
    }
}