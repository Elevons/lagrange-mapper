// Prompt: rising water level
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class RisingWater : MonoBehaviour
{
    [Header("Water Level Settings")]
    [SerializeField] private float _riseSpeed = 1f;
    [SerializeField] private float _maxHeight = 50f;
    [SerializeField] private float _startDelay = 0f;
    [SerializeField] private bool _startRisingOnAwake = true;
    
    [Header("Water Behavior")]
    [SerializeField] private AnimationCurve _riseSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);
    [SerializeField] private bool _pauseOnPlayerContact = false;
    [SerializeField] private float _pauseDuration = 2f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _splashEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _risingSound;
    [SerializeField] private AudioClip _splashSound;
    
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 1f;
    [SerializeField] private LayerMask _damageableLayers = -1;
    
    [Header("Events")]
    public UnityEvent OnWaterStartRising;
    public UnityEvent OnWaterReachMaxHeight;
    public UnityEvent<GameObject> OnObjectSubmerged;
    public UnityEvent<GameObject> OnPlayerContact;
    
    private float _currentHeight;
    private float _initialHeight;
    private bool _isRising = false;
    private float _startTime;
    private float _pauseEndTime;
    private bool _isPaused = false;
    private bool _hasReachedMax = false;
    
    private System.Collections.Generic.Dictionary<GameObject, float> _lastDamageTime = 
        new System.Collections.Generic.Dictionary<GameObject, float>();
    
    private void Awake()
    {
        _initialHeight = transform.position.y;
        _currentHeight = _initialHeight;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Start()
    {
        if (_startRisingOnAwake)
        {
            StartRising();
        }
    }
    
    private void Update()
    {
        if (!_isRising || _hasReachedMax)
            return;
            
        if (_isPaused && Time.time < _pauseEndTime)
            return;
            
        if (_isPaused && Time.time >= _pauseEndTime)
        {
            _isPaused = false;
        }
        
        if (Time.time < _startTime + _startDelay)
            return;
            
        float progress = (_currentHeight - _initialHeight) / (_maxHeight - _initialHeight);
        float speedMultiplier = _riseSpeedCurve.Evaluate(progress);
        
        _currentHeight += _riseSpeed * speedMultiplier * Time.deltaTime;
        
        if (_currentHeight >= _maxHeight)
        {
            _currentHeight = _maxHeight;
            _hasReachedMax = true;
            OnWaterReachMaxHeight?.Invoke();
            
            if (_audioSource != null && _audioSource.isPlaying)
                _audioSource.Stop();
        }
        
        Vector3 newPosition = transform.position;
        newPosition.y = _currentHeight;
        transform.position = newPosition;
    }
    
    public void StartRising()
    {
        _isRising = true;
        _startTime = Time.time;
        OnWaterStartRising?.Invoke();
        
        if (_audioSource != null && _risingSound != null)
        {
            _audioSource.clip = _risingSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    public void StopRising()
    {
        _isRising = false;
        
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }
    
    public void PauseRising()
    {
        if (!_isRising)
            return;
            
        _isPaused = true;
        _pauseEndTime = Time.time + _pauseDuration;
    }
    
    public void ResetWaterLevel()
    {
        _currentHeight = _initialHeight;
        _hasReachedMax = false;
        _isRising = false;
        _isPaused = false;
        _lastDamageTime.Clear();
        
        Vector3 newPosition = transform.position;
        newPosition.y = _currentHeight;
        transform.position = newPosition;
        
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }
    
    public void SetRiseSpeed(float newSpeed)
    {
        _riseSpeed = Mathf.Max(0f, newSpeed);
    }
    
    public void SetMaxHeight(float newMaxHeight)
    {
        _maxHeight = newMaxHeight;
    }
    
    public float GetWaterLevel()
    {
        return _currentHeight;
    }
    
    public float GetWaterProgress()
    {
        if (_maxHeight <= _initialHeight)
            return 0f;
            
        return Mathf.Clamp01((_currentHeight - _initialHeight) / (_maxHeight - _initialHeight));
    }
    
    public bool IsRising()
    {
        return _isRising && !_hasReachedMax;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            OnPlayerContact?.Invoke(other.gameObject);
            
            if (_pauseOnPlayerContact)
            {
                PauseRising();
            }
            
            PlaySplashEffect(other.transform.position);
        }
        
        OnObjectSubmerged?.Invoke(other.gameObject);
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (ShouldDamageObject(other))
        {
            DealDamage(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (_lastDamageTime.ContainsKey(other.gameObject))
        {
            _lastDamageTime.Remove(other.gameObject);
        }
    }
    
    private bool ShouldDamageObject(Collider other)
    {
        if (_damageAmount <= 0f)
            return false;
            
        if ((_damageableLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;
            
        if (!_lastDamageTime.ContainsKey(other.gameObject))
            return true;
            
        return Time.time >= _lastDamageTime[other.gameObject] + _damageInterval;
    }
    
    private void DealDamage(GameObject target)
    {
        _lastDamageTime[target] = Time.time;
        
        // Try to find health component
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use reflection to call TakeDamage if it exists
            var takeDamageMethod = healthComponent.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(healthComponent, new object[] { _damageAmount });
            }
        }
        
        // Alternative: Send message (safer approach)
        target.SendMessage("TakeDamage", _damageAmount, SendMessageOptions.DontRequireReceiver);
    }
    
    private void PlaySplashEffect(Vector3 position)
    {
        if (_splashEffect != null)
        {
            _splashEffect.transform.position = position;
            _splashEffect.Play();
        }
        
        if (_audioSource != null && _splashSound != null)
        {
            _audioSource.PlayOneShot(_splashSound);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        
        Vector3 currentPos = transform.position;
        Vector3 maxPos = new Vector3(currentPos.x, _maxHeight, currentPos.z);
        
        // Draw current water level
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(currentPos, new Vector3(10f, 0.1f, 10f));
        
        // Draw max water level
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(maxPos, new Vector3(10f, 0.1f, 10f));
        
        // Draw rise direction
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(currentPos, maxPos);
    }
}