// Prompt: alarm that triggers on detection
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class AlarmSystem : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRadius = 10f;
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private bool _requireLineOfSight = true;
    [SerializeField] private LayerMask _obstacleLayers = 1;
    
    [Header("Alarm Settings")]
    [SerializeField] private float _alarmDuration = 5f;
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private bool _resetOnLostTarget = true;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _alarmSound;
    [SerializeField] private bool _loopAlarmSound = true;
    
    [Header("Visual Effects")]
    [SerializeField] private Light _alarmLight;
    [SerializeField] private Color _normalLightColor = Color.white;
    [SerializeField] private Color _alarmLightColor = Color.red;
    [SerializeField] private float _flashSpeed = 2f;
    [SerializeField] private Renderer _indicatorRenderer;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Material _alarmMaterial;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onAlarmTriggered;
    [SerializeField] private UnityEvent _onAlarmStopped;
    [SerializeField] private UnityEvent _onTargetDetected;
    [SerializeField] private UnityEvent _onTargetLost;
    
    private bool _isAlarmActive = false;
    private bool _targetDetected = false;
    private bool _isOnCooldown = false;
    private float _alarmTimer = 0f;
    private float _cooldownTimer = 0f;
    private Transform _currentTarget;
    private Coroutine _flashCoroutine;
    
    private enum AlarmState
    {
        Idle,
        Detecting,
        Alarming,
        Cooldown
    }
    
    private AlarmState _currentState = AlarmState.Idle;
    
    void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_alarmLight == null)
            _alarmLight = GetComponentInChildren<Light>();
            
        if (_indicatorRenderer == null)
            _indicatorRenderer = GetComponent<Renderer>();
            
        SetNormalState();
    }
    
    void Update()
    {
        switch (_currentState)
        {
            case AlarmState.Idle:
                CheckForTargets();
                break;
                
            case AlarmState.Detecting:
                if (_targetDetected && _currentTarget != null)
                {
                    if (!_isOnCooldown)
                    {
                        TriggerAlarm();
                    }
                }
                else
                {
                    _currentState = AlarmState.Idle;
                }
                break;
                
            case AlarmState.Alarming:
                _alarmTimer -= Time.deltaTime;
                
                if (_resetOnLostTarget && !_targetDetected)
                {
                    StopAlarm();
                }
                else if (_alarmTimer <= 0f)
                {
                    StopAlarm();
                }
                break;
                
            case AlarmState.Cooldown:
                _cooldownTimer -= Time.deltaTime;
                if (_cooldownTimer <= 0f)
                {
                    _isOnCooldown = false;
                    _currentState = AlarmState.Idle;
                }
                break;
        }
        
        CheckForTargets();
    }
    
    void CheckForTargets()
    {
        bool previousDetection = _targetDetected;
        _targetDetected = false;
        _currentTarget = null;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRadius, _targetLayers);
        
        foreach (Collider col in colliders)
        {
            if (!string.IsNullOrEmpty(_targetTag) && !col.CompareTag(_targetTag))
                continue;
                
            if (_requireLineOfSight)
            {
                Vector3 directionToTarget = (col.transform.position - transform.position).normalized;
                float distanceToTarget = Vector3.Distance(transform.position, col.transform.position);
                
                if (Physics.Raycast(transform.position, directionToTarget, distanceToTarget, _obstacleLayers))
                    continue;
            }
            
            _targetDetected = true;
            _currentTarget = col.transform;
            
            if (_currentState == AlarmState.Idle)
            {
                _currentState = AlarmState.Detecting;
            }
            
            break;
        }
        
        if (previousDetection && !_targetDetected)
        {
            _onTargetLost?.Invoke();
        }
        else if (!previousDetection && _targetDetected)
        {
            _onTargetDetected?.Invoke();
        }
    }
    
    void TriggerAlarm()
    {
        if (_isAlarmActive || _isOnCooldown)
            return;
            
        _isAlarmActive = true;
        _currentState = AlarmState.Alarming;
        _alarmTimer = _alarmDuration;
        
        if (_audioSource != null && _alarmSound != null)
        {
            _audioSource.clip = _alarmSound;
            _audioSource.loop = _loopAlarmSound;
            _audioSource.Play();
        }
        
        if (_alarmLight != null)
        {
            _alarmLight.color = _alarmLightColor;
            if (_flashCoroutine != null)
                StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashLight());
        }
        
        if (_indicatorRenderer != null && _alarmMaterial != null)
        {
            _indicatorRenderer.material = _alarmMaterial;
        }
        
        _onAlarmTriggered?.Invoke();
    }
    
    void StopAlarm()
    {
        if (!_isAlarmActive)
            return;
            
        _isAlarmActive = false;
        _currentState = AlarmState.Cooldown;
        _cooldownTimer = _cooldownTime;
        _isOnCooldown = true;
        
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
        
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        
        SetNormalState();
        _onAlarmStopped?.Invoke();
    }
    
    void SetNormalState()
    {
        if (_alarmLight != null)
        {
            _alarmLight.color = _normalLightColor;
        }
        
        if (_indicatorRenderer != null && _normalMaterial != null)
        {
            _indicatorRenderer.material = _normalMaterial;
        }
    }
    
    IEnumerator FlashLight()
    {
        while (_isAlarmActive)
        {
            if (_alarmLight != null)
            {
                _alarmLight.enabled = !_alarmLight.enabled;
            }
            yield return new WaitForSeconds(1f / _flashSpeed);
        }
        
        if (_alarmLight != null)
        {
            _alarmLight.enabled = true;
        }
    }
    
    public void ForceStopAlarm()
    {
        StopAlarm();
    }
    
    public void ResetAlarm()
    {
        StopAlarm();
        _isOnCooldown = false;
        _currentState = AlarmState.Idle;
    }
    
    public bool IsAlarmActive()
    {
        return _isAlarmActive;
    }
    
    public bool IsTargetDetected()
    {
        return _targetDetected;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _targetDetected ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, _currentTarget.position);
        }
    }
}