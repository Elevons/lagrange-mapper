// Prompt: bicycle with pedaling
// Type: general

using UnityEngine;

public class Bicycle : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 8f;
    [SerializeField] private float _turnSpeed = 90f;
    [SerializeField] private float _minSpeedForTurning = 1f;
    
    [Header("Pedaling Settings")]
    [SerializeField] private float _pedalForce = 10f;
    [SerializeField] private float _pedalCooldown = 0.2f;
    [SerializeField] private float _pedalDecayRate = 2f;
    
    [Header("Physics")]
    [SerializeField] private float _groundCheckDistance = 1.1f;
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private float _gravity = -20f;
    
    [Header("Visual Components")]
    [SerializeField] private Transform _frontWheel;
    [SerializeField] private Transform _backWheel;
    [SerializeField] private Transform _pedals;
    [SerializeField] private Transform _handlebars;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pedalSound;
    [SerializeField] private AudioClip _wheelSound;
    
    private Rigidbody _rigidbody;
    private float _currentSpeed;
    private float _pedalPower;
    private float _lastPedalTime;
    private bool _isGrounded;
    private float _verticalVelocity;
    private float _wheelRotation;
    private float _pedalRotation;
    private Vector3 _moveDirection;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _rigidbody.freezeRotation = true;
        
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
        UpdatePedalPower();
        CheckGrounded();
        UpdateVisuals();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
        HandleGravity();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.W))
        {
            Pedal();
        }
        
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.S))
        {
            Brake();
        }
    }
    
    private void Pedal()
    {
        if (Time.time - _lastPedalTime >= _pedalCooldown && _isGrounded)
        {
            _pedalPower = Mathf.Min(_pedalPower + _pedalForce, _maxSpeed);
            _lastPedalTime = Time.time;
            
            if (_audioSource != null && _pedalSound != null)
            {
                _audioSource.PlayOneShot(_pedalSound, 0.5f);
            }
        }
    }
    
    private void Brake()
    {
        _pedalPower = Mathf.Max(_pedalPower - _deceleration * Time.deltaTime, 0f);
    }
    
    private void UpdatePedalPower()
    {
        if (Time.time - _lastPedalTime > _pedalCooldown)
        {
            _pedalPower = Mathf.Max(_pedalPower - _pedalDecayRate * Time.deltaTime, 0f);
        }
        
        _currentSpeed = _pedalPower;
    }
    
    private void HandleMovement()
    {
        if (!_isGrounded) return;
        
        float horizontalInput = Input.GetAxis("Horizontal");
        
        if (_currentSpeed > _minSpeedForTurning)
        {
            float turnAmount = horizontalInput * _turnSpeed * Time.fixedDeltaTime;
            transform.Rotate(0, turnAmount, 0);
        }
        
        _moveDirection = transform.forward * _currentSpeed;
        Vector3 targetVelocity = new Vector3(_moveDirection.x, _rigidbody.velocity.y, _moveDirection.z);
        _rigidbody.velocity = targetVelocity;
    }
    
    private void HandleGravity()
    {
        if (!_isGrounded)
        {
            _verticalVelocity += _gravity * Time.fixedDeltaTime;
            Vector3 velocity = _rigidbody.velocity;
            velocity.y = _verticalVelocity;
            _rigidbody.velocity = velocity;
        }
        else
        {
            _verticalVelocity = 0f;
        }
    }
    
    private void CheckGrounded()
    {
        RaycastHit hit;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, _groundCheckDistance, _groundLayer);
        
        if (_isGrounded && _verticalVelocity < 0)
        {
            _verticalVelocity = 0f;
        }
    }
    
    private void UpdateVisuals()
    {
        float wheelSpeed = _currentSpeed * 50f;
        _wheelRotation += wheelSpeed * Time.deltaTime;
        
        if (_frontWheel != null)
        {
            _frontWheel.localRotation = Quaternion.Euler(_wheelRotation, 0, 0);
        }
        
        if (_backWheel != null)
        {
            _backWheel.localRotation = Quaternion.Euler(_wheelRotation, 0, 0);
        }
        
        if (_pedals != null && Time.time - _lastPedalTime < _pedalCooldown * 2f)
        {
            _pedalRotation += 360f * Time.deltaTime;
            _pedals.localRotation = Quaternion.Euler(0, 0, _pedalRotation);
        }
        
        if (_handlebars != null)
        {
            float steerAngle = Input.GetAxis("Horizontal") * 30f;
            _handlebars.localRotation = Quaternion.Euler(0, steerAngle, 0);
        }
    }
    
    private void UpdateAudio()
    {
        if (_audioSource != null && _wheelSound != null)
        {
            if (_currentSpeed > 0.1f && _isGrounded)
            {
                if (!_audioSource.isPlaying)
                {
                    _audioSource.clip = _wheelSound;
                    _audioSource.Play();
                }
                
                _audioSource.pitch = Mathf.Lerp(0.5f, 2f, _currentSpeed / _maxSpeed);
                _audioSource.volume = Mathf.Lerp(0.1f, 0.8f, _currentSpeed / _maxSpeed);
            }
            else
            {
                if (_audioSource.isPlaying && _audioSource.clip == _wheelSound)
                {
                    _audioSource.Stop();
                }
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(transform.position, transform.position + Vector3.down * _groundCheckDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + _moveDirection.normalized * 2f);
    }
}