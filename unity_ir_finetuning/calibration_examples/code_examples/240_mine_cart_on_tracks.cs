// Prompt: mine cart on tracks
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class MineCart : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 10f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 8f;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _slopeSpeedMultiplier = 1.5f;
    
    [Header("Track Detection")]
    [SerializeField] private LayerMask _trackLayerMask = 1;
    [SerializeField] private float _trackDetectionDistance = 2f;
    [SerializeField] private Transform _frontWheelPoint;
    [SerializeField] private Transform _rearWheelPoint;
    
    [Header("Physics")]
    [SerializeField] private float _mass = 100f;
    [SerializeField] private float _friction = 0.1f;
    [SerializeField] private float _airResistance = 0.05f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rollingSound;
    [SerializeField] private AudioClip _brakeSound;
    [SerializeField] private float _minPitchSpeed = 0f;
    [SerializeField] private float _maxPitchSpeed = 10f;
    
    [Header("Events")]
    public UnityEvent OnCartStart;
    public UnityEvent OnCartStop;
    public UnityEvent<float> OnSpeedChanged;
    public UnityEvent OnDerailed;
    
    private Rigidbody _rigidbody;
    private Vector3 _currentDirection;
    private float _currentSpeed;
    private bool _isOnTrack;
    private bool _isMoving;
    private bool _playerOnBoard;
    private RaycastHit _frontHit;
    private RaycastHit _rearHit;
    private Vector3 _lastValidDirection;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.mass = _mass;
        _rigidbody.useGravity = false;
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _audioSource.clip = _rollingSound;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        
        if (_frontWheelPoint == null)
        {
            GameObject frontWheel = new GameObject("FrontWheelPoint");
            frontWheel.transform.SetParent(transform);
            frontWheel.transform.localPosition = Vector3.forward * 1f;
            _frontWheelPoint = frontWheel.transform;
        }
        
        if (_rearWheelPoint == null)
        {
            GameObject rearWheel = new GameObject("RearWheelPoint");
            rearWheel.transform.SetParent(transform);
            rearWheel.transform.localPosition = Vector3.back * 1f;
            _rearWheelPoint = rearWheel.transform;
        }
        
        _currentDirection = transform.forward;
        _lastValidDirection = _currentDirection;
    }
    
    private void FixedUpdate()
    {
        DetectTrack();
        
        if (_isOnTrack)
        {
            CalculateTrackDirection();
            ApplyMovement();
        }
        else
        {
            ApplyGravity();
            HandleDerailment();
        }
        
        UpdateAudio();
        OnSpeedChanged?.Invoke(_currentSpeed);
    }
    
    private void DetectTrack()
    {
        bool frontOnTrack = Physics.Raycast(_frontWheelPoint.position, Vector3.down, out _frontHit, _trackDetectionDistance, _trackLayerMask);
        bool rearOnTrack = Physics.Raycast(_rearWheelPoint.position, Vector3.down, out _rearHit, _trackDetectionDistance, _trackLayerMask);
        
        _isOnTrack = frontOnTrack && rearOnTrack;
        
        if (!_isOnTrack && _isMoving)
        {
            OnDerailed?.Invoke();
        }
    }
    
    private void CalculateTrackDirection()
    {
        if (_isOnTrack)
        {
            Vector3 frontPoint = _frontHit.point;
            Vector3 rearPoint = _rearHit.point;
            
            Vector3 trackDirection = (frontPoint - rearPoint).normalized;
            
            if (Vector3.Dot(trackDirection, _lastValidDirection) < 0)
            {
                trackDirection = -trackDirection;
            }
            
            _currentDirection = trackDirection;
            _lastValidDirection = trackDirection;
            
            Vector3 averageNormal = (_frontHit.normal + _rearHit.normal) * 0.5f;
            Vector3 rightDirection = Vector3.Cross(trackDirection, averageNormal).normalized;
            Vector3 upDirection = Vector3.Cross(rightDirection, trackDirection).normalized;
            
            transform.rotation = Quaternion.LookRotation(trackDirection, upDirection);
        }
    }
    
    private void ApplyMovement()
    {
        float slopeAngle = Vector3.Angle(Vector3.up, (_frontHit.normal + _rearHit.normal) * 0.5f) - 90f;
        float slopeForce = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * _gravity;
        
        if (_playerOnBoard)
        {
            _currentSpeed += _acceleration * Time.fixedDeltaTime;
        }
        
        _currentSpeed += slopeForce * _slopeSpeedMultiplier * Time.fixedDeltaTime;
        
        float resistanceForce = _friction + (_airResistance * _currentSpeed * _currentSpeed);
        _currentSpeed -= resistanceForce * Time.fixedDeltaTime;
        
        _currentSpeed = Mathf.Clamp(_currentSpeed, -_maxSpeed, _maxSpeed);
        
        if (Mathf.Abs(_currentSpeed) < 0.1f)
        {
            _currentSpeed = 0f;
            if (_isMoving)
            {
                _isMoving = false;
                OnCartStop?.Invoke();
            }
        }
        else
        {
            if (!_isMoving)
            {
                _isMoving = true;
                OnCartStart?.Invoke();
            }
        }
        
        Vector3 velocity = _currentDirection * _currentSpeed;
        _rigidbody.velocity = velocity;
        
        Vector3 averageTrackPoint = (_frontHit.point + _rearHit.point) * 0.5f;
        Vector3 targetPosition = averageTrackPoint + Vector3.up * 0.5f;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * 10f);
    }
    
    private void ApplyGravity()
    {
        _rigidbody.useGravity = true;
        _currentSpeed = _rigidbody.velocity.magnitude;
    }
    
    private void HandleDerailment()
    {
        _rigidbody.useGravity = true;
        
        if (_currentSpeed > 1f)
        {
            _currentSpeed -= _deceleration * Time.fixedDeltaTime;
        }
    }
    
    private void UpdateAudio()
    {
        if (_isMoving && _currentSpeed > 0.1f)
        {
            if (!_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
            
            float normalizedSpeed = Mathf.InverseLerp(_minPitchSpeed, _maxPitchSpeed, _currentSpeed);
            _audioSource.pitch = Mathf.Lerp(0.5f, 2f, normalizedSpeed);
            _audioSource.volume = Mathf.Lerp(0.3f, 1f, normalizedSpeed);
        }
        else
        {
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerOnBoard = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerOnBoard = false;
        }
    }
    
    public void ApplyBrake()
    {
        _currentSpeed *= 0.5f;
        
        if (_brakeSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_brakeSound);
        }
    }
    
    public void AddForce(float force)
    {
        _currentSpeed += force;
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public bool IsOnTrack()
    {
        return _isOnTrack;
    }
    
    public bool IsMoving()
    {
        return _isMoving;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_frontWheelPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(_frontWheelPoint.position, Vector3.down * _trackDetectionDistance);
        }
        
        if (_rearWheelPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_rearWheelPoint.position, Vector3.down * _trackDetectionDistance);
        }
        
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, _currentDirection * 2f);
    }
}