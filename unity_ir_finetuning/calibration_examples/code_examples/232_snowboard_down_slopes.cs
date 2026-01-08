// Prompt: snowboard down slopes
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SnowboardController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 20f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 3f;
    [SerializeField] private float _turnSpeed = 100f;
    [SerializeField] private float _gravityMultiplier = 2f;
    
    [Header("Slope Detection")]
    [SerializeField] private float _slopeCheckDistance = 1.5f;
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private float _minSlopeAngle = 5f;
    
    [Header("Physics")]
    [SerializeField] private float _airDrag = 0.5f;
    [SerializeField] private float _groundDrag = 2f;
    [SerializeField] private float _jumpForce = 8f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _snowSprayEffect;
    [SerializeField] private TrailRenderer _boardTrail;
    [SerializeField] private Transform _boardVisual;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _carveSound;
    [SerializeField] private AudioClip _jumpSound;
    
    [Header("Events")]
    public UnityEvent OnLanding;
    public UnityEvent OnJump;
    public UnityEvent<float> OnSpeedChange;
    
    private Rigidbody _rigidbody;
    private bool _isGrounded;
    private bool _isOnSlope;
    private float _currentSpeed;
    private float _slopeAngle;
    private Vector3 _slopeNormal;
    private Vector3 _moveDirection;
    private bool _wasGrounded;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.freezeRotation = true;
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_snowSprayEffect != null)
        {
            _snowSprayEffect.Stop();
        }
    }
    
    private void Update()
    {
        HandleInput();
        CheckGroundStatus();
        UpdateVisualEffects();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        ApplyMovement();
        ApplyGravity();
        HandleDrag();
    }
    
    private void HandleInput()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");
        
        if (_isGrounded)
        {
            // Turn the snowboard
            if (Mathf.Abs(horizontalInput) > 0.1f)
            {
                transform.Rotate(0, horizontalInput * _turnSpeed * Time.deltaTime, 0);
            }
            
            // Calculate movement direction based on slope
            if (_isOnSlope)
            {
                _moveDirection = GetSlopeDirection();
            }
            else
            {
                _moveDirection = transform.forward;
            }
            
            // Jump
            if (Input.GetKeyDown(KeyCode.Space))
            {
                Jump();
            }
        }
    }
    
    private void CheckGroundStatus()
    {
        RaycastHit hit;
        _wasGrounded = _isGrounded;
        
        if (Physics.Raycast(transform.position, Vector3.down, out hit, _slopeCheckDistance, _groundLayer))
        {
            _isGrounded = true;
            _slopeNormal = hit.normal;
            _slopeAngle = Vector3.Angle(Vector3.up, _slopeNormal);
            _isOnSlope = _slopeAngle > _minSlopeAngle && _slopeAngle < 60f;
            
            // Landing detection
            if (!_wasGrounded && _isGrounded)
            {
                OnLanding?.Invoke();
            }
        }
        else
        {
            _isGrounded = false;
            _isOnSlope = false;
            _slopeAngle = 0f;
        }
    }
    
    private Vector3 GetSlopeDirection()
    {
        Vector3 forward = transform.forward;
        return Vector3.ProjectOnPlane(forward, _slopeNormal).normalized;
    }
    
    private void ApplyMovement()
    {
        if (_isGrounded && _isOnSlope)
        {
            // Calculate slope-based acceleration
            float slopeAcceleration = _acceleration * (_slopeAngle / 45f);
            _currentSpeed = Mathf.Min(_currentSpeed + slopeAcceleration * Time.fixedDeltaTime, _maxSpeed);
            
            // Apply movement
            Vector3 targetVelocity = _moveDirection * _currentSpeed;
            targetVelocity.y = _rigidbody.velocity.y;
            _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, targetVelocity, Time.fixedDeltaTime * 5f);
        }
        else if (_isGrounded && !_isOnSlope)
        {
            // Decelerate on flat ground
            _currentSpeed = Mathf.Max(_currentSpeed - _deceleration * Time.fixedDeltaTime, 0f);
            
            Vector3 targetVelocity = _moveDirection * _currentSpeed;
            targetVelocity.y = _rigidbody.velocity.y;
            _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, targetVelocity, Time.fixedDeltaTime * 3f);
        }
        
        OnSpeedChange?.Invoke(_currentSpeed);
    }
    
    private void ApplyGravity()
    {
        if (!_isGrounded)
        {
            _rigidbody.AddForce(Vector3.down * _gravityMultiplier * Physics.gravity.magnitude, ForceMode.Acceleration);
        }
    }
    
    private void HandleDrag()
    {
        _rigidbody.drag = _isGrounded ? _groundDrag : _airDrag;
    }
    
    private void Jump()
    {
        if (_isGrounded)
        {
            _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            OnJump?.Invoke();
            
            if (_audioSource != null && _jumpSound != null)
            {
                _audioSource.PlayOneShot(_jumpSound);
            }
        }
    }
    
    private void UpdateVisualEffects()
    {
        // Snow spray effect
        if (_snowSprayEffect != null)
        {
            if (_isGrounded && _currentSpeed > 5f)
            {
                if (!_snowSprayEffect.isPlaying)
                {
                    _snowSprayEffect.Play();
                }
                
                var emission = _snowSprayEffect.emission;
                emission.rateOverTime = _currentSpeed * 2f;
            }
            else if (_snowSprayEffect.isPlaying)
            {
                _snowSprayEffect.Stop();
            }
        }
        
        // Board trail
        if (_boardTrail != null)
        {
            _boardTrail.emitting = _isGrounded && _currentSpeed > 3f;
        }
        
        // Board visual rotation
        if (_boardVisual != null && _isGrounded)
        {
            float tiltAngle = Mathf.Clamp(Input.GetAxis("Horizontal") * 15f, -15f, 15f);
            _boardVisual.localRotation = Quaternion.Lerp(_boardVisual.localRotation, 
                Quaternion.Euler(0, 0, -tiltAngle), Time.deltaTime * 5f);
        }
    }
    
    private void UpdateAudio()
    {
        if (_audioSource != null && _carveSound != null)
        {
            if (_isGrounded && _currentSpeed > 3f && Mathf.Abs(Input.GetAxis("Horizontal")) > 0.3f)
            {
                if (!_audioSource.isPlaying)
                {
                    _audioSource.clip = _carveSound;
                    _audioSource.Play();
                }
                _audioSource.volume = Mathf.Clamp01(_currentSpeed / _maxSpeed);
            }
            else if (_audioSource.isPlaying && _audioSource.clip == _carveSound)
            {
                _audioSource.Stop();
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw slope check ray
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * _slopeCheckDistance);
        
        // Draw slope normal
        if (_isOnSlope)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(transform.position, _slopeNormal * 2f);
        }
        
        // Draw movement direction
        if (_moveDirection != Vector3.zero)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, _moveDirection * 3f);
        }
    }
}