// Prompt: minecart on rails
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class MinecartController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 10f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 3f;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _railDetectionDistance = 2f;
    
    [Header("Rail Following")]
    [SerializeField] private LayerMask _railLayerMask = 1;
    [SerializeField] private float _railSnapDistance = 0.5f;
    [SerializeField] private float _turnSpeed = 180f;
    
    [Header("Physics")]
    [SerializeField] private float _mass = 100f;
    [SerializeField] private float _friction = 0.1f;
    [SerializeField] private float _airResistance = 0.05f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rollingSound;
    [SerializeField] private AudioClip _brakeSound;
    
    [Header("Events")]
    public UnityEvent OnPlayerEnter;
    public UnityEvent OnPlayerExit;
    public UnityEvent OnMaxSpeedReached;
    
    private Rigidbody _rigidbody;
    private bool _hasPlayer = false;
    private float _currentSpeed = 0f;
    private Vector3 _currentDirection = Vector3.forward;
    private RaycastHit _railHit;
    private bool _isOnRails = true;
    private float _inputAcceleration = 0f;
    private bool _isBraking = false;
    
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
        
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
    }
    
    private void Update()
    {
        HandleInput();
        DetectRails();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        if (_isOnRails)
        {
            MoveOnRails();
        }
        else
        {
            ApplyGravity();
        }
        
        ApplyResistance();
        UpdateRigidbody();
    }
    
    private void HandleInput()
    {
        if (!_hasPlayer) return;
        
        _inputAcceleration = 0f;
        _isBraking = false;
        
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            _inputAcceleration = 1f;
        }
        else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            _inputAcceleration = -1f;
        }
        
        if (Input.GetKey(KeyCode.Space))
        {
            _isBraking = true;
        }
    }
    
    private void DetectRails()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.1f;
        Vector3 rayDirection = Vector3.down;
        
        _isOnRails = Physics.Raycast(rayOrigin, rayDirection, out _railHit, _railDetectionDistance, _railLayerMask);
        
        if (_isOnRails)
        {
            SnapToRail();
            UpdateDirection();
        }
    }
    
    private void SnapToRail()
    {
        Vector3 targetPosition = _railHit.point + Vector3.up * 0.1f;
        float snapDistance = Vector3.Distance(transform.position, targetPosition);
        
        if (snapDistance > _railSnapDistance)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.fixedDeltaTime * 10f);
        }
    }
    
    private void UpdateDirection()
    {
        Vector3 railForward = Vector3.Cross(_railHit.normal, transform.right).normalized;
        if (Vector3.Dot(railForward, _currentDirection) < 0)
        {
            railForward = -railForward;
        }
        
        _currentDirection = Vector3.Slerp(_currentDirection, railForward, Time.fixedDeltaTime * _turnSpeed);
        transform.rotation = Quaternion.LookRotation(_currentDirection, _railHit.normal);
    }
    
    private void MoveOnRails()
    {
        float targetSpeed = _currentSpeed;
        
        if (_isBraking)
        {
            targetSpeed = Mathf.Max(0f, _currentSpeed - _deceleration * Time.fixedDeltaTime);
        }
        else if (_inputAcceleration != 0f)
        {
            targetSpeed = _currentSpeed + _inputAcceleration * _acceleration * Time.fixedDeltaTime;
        }
        else
        {
            float slopeInfluence = Vector3.Dot(_currentDirection, Vector3.down) * _gravity * 0.1f;
            targetSpeed = _currentSpeed + slopeInfluence * Time.fixedDeltaTime;
        }
        
        _currentSpeed = Mathf.Clamp(targetSpeed, -_maxSpeed, _maxSpeed);
        
        if (Mathf.Abs(_currentSpeed) >= _maxSpeed * 0.95f && !_isBraking)
        {
            OnMaxSpeedReached?.Invoke();
        }
    }
    
    private void ApplyGravity()
    {
        _rigidbody.AddForce(Vector3.down * _gravity * _mass, ForceMode.Force);
    }
    
    private void ApplyResistance()
    {
        float frictionForce = _friction * _mass * _gravity;
        float airResistanceForce = _airResistance * _currentSpeed * _currentSpeed;
        
        float totalResistance = frictionForce + airResistanceForce;
        
        if (_currentSpeed > 0)
        {
            _currentSpeed = Mathf.Max(0f, _currentSpeed - totalResistance * Time.fixedDeltaTime);
        }
        else if (_currentSpeed < 0)
        {
            _currentSpeed = Mathf.Min(0f, _currentSpeed + totalResistance * Time.fixedDeltaTime);
        }
    }
    
    private void UpdateRigidbody()
    {
        if (_isOnRails)
        {
            Vector3 velocity = _currentDirection * _currentSpeed;
            _rigidbody.velocity = velocity;
        }
    }
    
    private void UpdateAudio()
    {
        if (_rollingSound != null && _audioSource != null)
        {
            if (Mathf.Abs(_currentSpeed) > 0.1f)
            {
                if (!_audioSource.isPlaying)
                {
                    _audioSource.clip = _rollingSound;
                    _audioSource.Play();
                }
                
                _audioSource.pitch = Mathf.Lerp(0.5f, 2f, Mathf.Abs(_currentSpeed) / _maxSpeed);
                _audioSource.volume = Mathf.Lerp(0.1f, 1f, Mathf.Abs(_currentSpeed) / _maxSpeed);
            }
            else
            {
                _audioSource.Stop();
            }
        }
        
        if (_isBraking && _brakeSound != null && _audioSource != null)
        {
            if (!_audioSource.isPlaying || _audioSource.clip != _brakeSound)
            {
                _audioSource.PlayOneShot(_brakeSound);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _hasPlayer = true;
            OnPlayerEnter?.Invoke();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _hasPlayer = false;
            OnPlayerExit?.Invoke();
        }
    }
    
    public void SetSpeed(float speed)
    {
        _currentSpeed = Mathf.Clamp(speed, -_maxSpeed, _maxSpeed);
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public bool IsOnRails()
    {
        return _isOnRails;
    }
    
    public void EmergencyStop()
    {
        _currentSpeed = 0f;
        _rigidbody.velocity = Vector3.zero;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.1f, Vector3.down * _railDetectionDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, _currentDirection * 2f);
        
        if (_isOnRails)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawSphere(_railHit.point, 0.1f);
        }
    }
}