// Prompt: underwater breath timer
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class UnderwaterBreathTimer : MonoBehaviour
{
    [Header("Breath Settings")]
    [SerializeField] private float _maxBreathTime = 30f;
    [SerializeField] private float _breathRecoveryRate = 2f;
    [SerializeField] private float _breathDrainRate = 1f;
    [SerializeField] private float _lowBreathThreshold = 0.3f;
    
    [Header("Water Detection")]
    [SerializeField] private string _waterTag = "Water";
    [SerializeField] private Transform _breathCheckPoint;
    [SerializeField] private LayerMask _waterLayerMask = -1;
    [SerializeField] private float _checkRadius = 0.1f;
    
    [Header("UI Elements")]
    [SerializeField] private Slider _breathBar;
    [SerializeField] private Image _breathBarFill;
    [SerializeField] private GameObject _breathUI;
    [SerializeField] private Color _normalBreathColor = Color.blue;
    [SerializeField] private Color _lowBreathColor = Color.red;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _breathingSound;
    [SerializeField] private AudioClip _drowningSound;
    [SerializeField] private AudioClip _gasping Sound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 1f;
    
    [Header("Events")]
    public UnityEvent OnBreathDepleted;
    public UnityEvent OnStartDrowning;
    public UnityEvent OnStopDrowning;
    public UnityEvent OnSurfaced;
    public UnityEvent OnSubmerged;
    
    private float _currentBreathTime;
    private bool _isUnderwater = false;
    private bool _isDrowning = false;
    private float _lastDamageTime;
    private bool _wasUnderwater = false;
    private Coroutine _breathingCoroutine;
    
    private void Start()
    {
        _currentBreathTime = _maxBreathTime;
        
        if (_breathCheckPoint == null)
            _breathCheckPoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        InitializeUI();
    }
    
    private void Update()
    {
        CheckWaterStatus();
        UpdateBreathTimer();
        UpdateUI();
        HandleAudio();
    }
    
    private void CheckWaterStatus()
    {
        bool currentlyUnderwater = IsUnderwater();
        
        if (currentlyUnderwater != _wasUnderwater)
        {
            if (currentlyUnderwater)
            {
                OnSubmerged?.Invoke();
                StartBreathing();
            }
            else
            {
                OnSurfaced?.Invoke();
                StopBreathing();
            }
        }
        
        _isUnderwater = currentlyUnderwater;
        _wasUnderwater = currentlyUnderwater;
    }
    
    private bool IsUnderwater()
    {
        Collider[] waterColliders = Physics.OverlapSphere(_breathCheckPoint.position, _checkRadius, _waterLayerMask);
        
        foreach (Collider collider in waterColliders)
        {
            if (collider.CompareTag(_waterTag))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void UpdateBreathTimer()
    {
        if (_isUnderwater)
        {
            _currentBreathTime -= _breathDrainRate * Time.deltaTime;
            _currentBreathTime = Mathf.Max(0f, _currentBreathTime);
            
            if (_currentBreathTime <= 0f && !_isDrowning)
            {
                StartDrowning();
            }
        }
        else
        {
            if (_isDrowning)
            {
                StopDrowning();
            }
            
            _currentBreathTime += _breathRecoveryRate * Time.deltaTime;
            _currentBreathTime = Mathf.Min(_maxBreathTime, _currentBreathTime);
        }
    }
    
    private void StartBreathing()
    {
        if (_bubbleEffect != null && !_bubbleEffect.isPlaying)
        {
            _bubbleEffect.Play();
        }
    }
    
    private void StopBreathing()
    {
        if (_bubbleEffect != null && _bubbleEffect.isPlaying)
        {
            _bubbleEffect.Stop();
        }
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        if (_gaspingSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_gaspingSound);
        }
    }
    
    private void StartDrowning()
    {
        _isDrowning = true;
        _lastDamageTime = Time.time;
        OnStartDrowning?.Invoke();
        OnBreathDepleted?.Invoke();
    }
    
    private void StopDrowning()
    {
        _isDrowning = false;
        OnStopDrowning?.Invoke();
    }
    
    private void FixedUpdate()
    {
        if (_isDrowning && Time.time >= _lastDamageTime + _damageInterval)
        {
            ApplyDrowningDamage();
            _lastDamageTime = Time.time;
        }
    }
    
    private void ApplyDrowningDamage()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Try to find health component
            var healthComponent = player.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                // Send damage message that can be received by any health system
                player.SendMessage("TakeDamage", _damageAmount, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    private void InitializeUI()
    {
        if (_breathBar != null)
        {
            _breathBar.maxValue = _maxBreathTime;
            _breathBar.value = _currentBreathTime;
        }
        
        if (_breathUI != null)
        {
            _breathUI.SetActive(false);
        }
        
        UpdateBreathBarColor();
    }
    
    private void UpdateUI()
    {
        if (_breathUI != null)
        {
            _breathUI.SetActive(_isUnderwater || _currentBreathTime < _maxBreathTime);
        }
        
        if (_breathBar != null)
        {
            _breathBar.value = _currentBreathTime;
        }
        
        UpdateBreathBarColor();
    }
    
    private void UpdateBreathBarColor()
    {
        if (_breathBarFill != null)
        {
            float breathPercentage = _currentBreathTime / _maxBreathTime;
            
            if (breathPercentage <= _lowBreathThreshold)
            {
                _breathBarFill.color = Color.Lerp(_lowBreathColor, _normalBreathColor, 
                    breathPercentage / _lowBreathThreshold);
            }
            else
            {
                _breathBarFill.color = _normalBreathColor;
            }
        }
    }
    
    private void HandleAudio()
    {
        if (_audioSource == null) return;
        
        if (_isDrowning && _drowningSound != null)
        {
            if (!_audioSource.isPlaying || _audioSource.clip != _drowningSound)
            {
                _audioSource.clip = _drowningSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        else if (_isUnderwater && _breathingSound != null)
        {
            if (!_audioSource.isPlaying || _audioSource.clip != _breathingSound)
            {
                _audioSource.clip = _breathingSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        else if (!_isUnderwater && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    public void AddBreathTime(float amount)
    {
        _currentBreathTime += amount;
        _currentBreathTime = Mathf.Min(_maxBreathTime, _currentBreathTime);
        
        if (_isDrowning && _currentBreathTime > 0f)
        {
            StopDrowning();
        }
    }
    
    public void SetMaxBreathTime(float newMaxTime)
    {
        float ratio = _currentBreathTime / _maxBreathTime;
        _maxBreathTime = newMaxTime;
        _currentBreathTime = _maxBreathTime * ratio;
        
        if (_breathBar != null)
        {
            _breathBar.maxValue = _maxBreathTime;
        }
    }
    
    public float GetBreathPercentage()
    {
        return _currentBreathTime / _maxBreathTime;
    }
    
    public bool IsCurrentlyDrowning()
    {
        return _isDrowning;
    }
    
    public bool IsCurrentlyUnderwater()
    {
        return _isUnderwater;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_breathCheckPoint != null)
        {
            Gizmos.color = _isUnderwater ? Color.blue : Color.green;
            Gizmos.DrawWireSphere(_breathCheckPoint.position, _checkRadius);
        }
    }
}