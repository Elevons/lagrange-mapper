// Prompt: motorcycle with leaning turns
// Type: general

using UnityEngine;

public class MotorcycleController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 30f;
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private float _deceleration = 15f;
    [SerializeField] private float _brakeForce = 25f;
    [SerializeField] private float _turnSpeed = 100f;
    [SerializeField] private float _reverseSpeed = 10f;

    [Header("Leaning Settings")]
    [SerializeField] private float _maxLeanAngle = 45f;
    [SerializeField] private float _leanSpeed = 3f;
    [SerializeField] private float _leanReturnSpeed = 2f;
    [SerializeField] private Transform _motorcycleBody;

    [Header("Physics Settings")]
    [SerializeField] private float _downForce = 100f;
    [SerializeField] private float _centerOfMassOffset = -0.5f;
    [SerializeField] private LayerMask _groundLayer = 1;

    [Header("Audio")]
    [SerializeField] private AudioSource _engineAudioSource;
    [SerializeField] private AudioClip _engineIdleClip;
    [SerializeField] private AudioClip _engineRevClip;
    [SerializeField] private float _minPitch = 0.8f;
    [SerializeField] private float _maxPitch = 2f;

    private Rigidbody _rigidbody;
    private float _currentSpeed;
    private float _motorInput;
    private float _steerInput;
    private float _currentLeanAngle;
    private bool _isGrounded;
    private Vector3 _originalCenterOfMass;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _originalCenterOfMass = _rigidbody.centerOfMass;
        _rigidbody.centerOfMass = _originalCenterOfMass + Vector3.up * _centerOfMassOffset;

        if (_motorcycleBody == null)
        {
            _motorcycleBody = transform;
        }

        if (_engineAudioSource == null)
        {
            _engineAudioSource = gameObject.AddComponent<AudioSource>();
            _engineAudioSource.loop = true;
            _engineAudioSource.playOnAwake = false;
        }

        if (_engineIdleClip != null)
        {
            _engineAudioSource.clip = _engineIdleClip;
            _engineAudioSource.Play();
        }
    }

    private void Update()
    {
        HandleInput();
        CheckGrounded();
        UpdateEngineAudio();
    }

    private void FixedUpdate()
    {
        if (_isGrounded)
        {
            HandleMotorcycleMovement();
            HandleLeaning();
            ApplyDownForce();
        }
    }

    private void HandleInput()
    {
        _motorInput = Input.GetAxis("Vertical");
        _steerInput = Input.GetAxis("Horizontal");
    }

    private void HandleMotorcycleMovement()
    {
        _currentSpeed = Vector3.Dot(_rigidbody.velocity, transform.forward);

        // Forward/Backward movement
        if (_motorInput > 0)
        {
            // Accelerating forward
            if (_currentSpeed < _maxSpeed)
            {
                _rigidbody.AddForce(transform.forward * _motorInput * _acceleration, ForceMode.Acceleration);
            }
        }
        else if (_motorInput < 0)
        {
            if (_currentSpeed > 0)
            {
                // Braking
                _rigidbody.AddForce(transform.forward * _motorInput * _brakeForce, ForceMode.Acceleration);
            }
            else if (_currentSpeed > -_reverseSpeed)
            {
                // Reversing
                _rigidbody.AddForce(transform.forward * _motorInput * _acceleration * 0.5f, ForceMode.Acceleration);
            }
        }
        else
        {
            // Natural deceleration
            _rigidbody.AddForce(-_rigidbody.velocity * _deceleration * 0.1f, ForceMode.Acceleration);
        }

        // Steering (only when moving)
        if (Mathf.Abs(_currentSpeed) > 0.1f && Mathf.Abs(_steerInput) > 0.1f)
        {
            float steerAmount = _steerInput * _turnSpeed * (_currentSpeed / _maxSpeed) * Time.fixedDeltaTime;
            transform.Rotate(0, steerAmount, 0);
        }
    }

    private void HandleLeaning()
    {
        float targetLeanAngle = 0f;

        // Calculate lean angle based on steering input and speed
        if (Mathf.Abs(_steerInput) > 0.1f && Mathf.Abs(_currentSpeed) > 1f)
        {
            float speedFactor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / _maxSpeed);
            targetLeanAngle = -_steerInput * _maxLeanAngle * speedFactor;
        }

        // Smoothly interpolate to target lean angle
        float lerpSpeed = Mathf.Abs(targetLeanAngle) > Mathf.Abs(_currentLeanAngle) ? _leanSpeed : _leanReturnSpeed;
        _currentLeanAngle = Mathf.Lerp(_currentLeanAngle, targetLeanAngle, lerpSpeed * Time.fixedDeltaTime);

        // Apply lean rotation to motorcycle body
        if (_motorcycleBody != null)
        {
            Vector3 currentRotation = _motorcycleBody.localEulerAngles;
            _motorcycleBody.localRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, _currentLeanAngle);
        }
    }

    private void CheckGrounded()
    {
        float rayDistance = 1.5f;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, rayDistance, _groundLayer);
    }

    private void ApplyDownForce()
    {
        if (_isGrounded)
        {
            _rigidbody.AddForce(Vector3.down * _downForce * _rigidbody.velocity.magnitude, ForceMode.Force);
        }
    }

    private void UpdateEngineAudio()
    {
        if (_engineAudioSource != null)
        {
            float speedRatio = Mathf.Abs(_currentSpeed) / _maxSpeed;
            float inputRatio = Mathf.Abs(_motorInput);
            
            float targetPitch = Mathf.Lerp(_minPitch, _maxPitch, Mathf.Max(speedRatio, inputRatio * 0.5f));
            _engineAudioSource.pitch = Mathf.Lerp(_engineAudioSource.pitch, targetPitch, Time.deltaTime * 3f);

            float targetVolume = Mathf.Lerp(0.3f, 1f, Mathf.Max(speedRatio, inputRatio * 0.3f));
            _engineAudioSource.volume = Mathf.Lerp(_engineAudioSource.volume, targetVolume, Time.deltaTime * 2f);

            // Switch between idle and rev clips based on input
            if (_motorInput > 0.1f && _engineRevClip != null && _engineAudioSource.clip != _engineRevClip)
            {
                _engineAudioSource.clip = _engineRevClip;
                _engineAudioSource.Play();
            }
            else if (_motorInput <= 0.1f && _engineIdleClip != null && _engineAudioSource.clip != _engineIdleClip)
            {
                _engineAudioSource.clip = _engineIdleClip;
                _engineAudioSource.Play();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Draw ground check ray
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * 1.5f);

        // Draw center of mass
        Gizmos.color = Color.yellow;
        if (_rigidbody != null)
        {
            Gizmos.DrawSphere(transform.TransformPoint(_rigidbody.centerOfMass), 0.1f);
        }

        // Draw lean angle visualization
        Gizmos.color = Color.blue;
        Vector3 leanDirection = Quaternion.AngleAxis(_currentLeanAngle, transform.forward) * transform.right;
        Gizmos.DrawRay(transform.position, leanDirection * 2f);
    }
}