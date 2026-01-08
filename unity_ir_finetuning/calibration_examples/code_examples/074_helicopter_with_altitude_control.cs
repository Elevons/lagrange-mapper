// Prompt: helicopter with altitude control
// Type: general

using UnityEngine;

public class HelicopterController : MonoBehaviour
{
    [Header("Flight Controls")]
    [SerializeField] private float _liftForce = 1000f;
    [SerializeField] private float _maxAltitude = 100f;
    [SerializeField] private float _minAltitude = 0.5f;
    [SerializeField] private float _altitudeControlSensitivity = 2f;
    
    [Header("Movement")]
    [SerializeField] private float _forwardSpeed = 20f;
    [SerializeField] private float _sidewaysSpeed = 15f;
    [SerializeField] private float _rotationSpeed = 50f;
    
    [Header("Stability")]
    [SerializeField] private float _stabilizationForce = 500f;
    [SerializeField] private float _tiltAmount = 15f;
    [SerializeField] private float _tiltSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _rotorAudioSource;
    [SerializeField] private float _minRotorPitch = 0.5f;
    [SerializeField] private float _maxRotorPitch = 2f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _dustEffect;
    [SerializeField] private float _dustEffectHeight = 5f;
    
    private Rigidbody _rigidbody;
    private float _currentThrottle;
    private float _targetAltitude;
    private Vector3 _initialRotation;
    private bool _isGrounded;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.mass = 1000f;
        _rigidbody.drag = 1f;
        _rigidbody.angularDrag = 5f;
        
        _initialRotation = transform.eulerAngles;
        _targetAltitude = transform.position.y;
        
        if (_rotorAudioSource == null)
        {
            _rotorAudioSource = GetComponent<AudioSource>();
        }
        
        if (_dustEffect != null)
        {
            _dustEffect.Stop();
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateAudio();
        UpdateEffects();
    }
    
    private void FixedUpdate()
    {
        ApplyLift();
        ApplyMovement();
        ApplyStabilization();
        CheckGroundContact();
    }
    
    private void HandleInput()
    {
        float throttleInput = Input.GetAxis("Vertical");
        float yawInput = Input.GetAxis("Horizontal");
        
        _currentThrottle = Mathf.Clamp01(_currentThrottle + throttleInput * Time.deltaTime * _altitudeControlSensitivity);
        
        if (Input.GetKey(KeyCode.Q))
        {
            _targetAltitude = Mathf.Min(_targetAltitude + 10f * Time.deltaTime, _maxAltitude);
        }
        if (Input.GetKey(KeyCode.E))
        {
            _targetAltitude = Mathf.Max(_targetAltitude - 10f * Time.deltaTime, _minAltitude);
        }
        
        if (Mathf.Abs(yawInput) > 0.1f)
        {
            transform.Rotate(0, yawInput * _rotationSpeed * Time.deltaTime, 0);
        }
    }
    
    private void ApplyLift()
    {
        float currentAltitude = transform.position.y;
        float altitudeDifference = _targetAltitude - currentAltitude;
        
        float liftMultiplier = _currentThrottle;
        
        if (Mathf.Abs(altitudeDifference) > 0.5f)
        {
            liftMultiplier += Mathf.Sign(altitudeDifference) * 0.5f;
        }
        
        liftMultiplier = Mathf.Clamp01(liftMultiplier);
        
        Vector3 liftVector = Vector3.up * _liftForce * liftMultiplier;
        _rigidbody.AddForce(liftVector);
        
        Vector3 counterGravity = -Physics.gravity * _rigidbody.mass * 0.8f;
        _rigidbody.AddForce(counterGravity);
    }
    
    private void ApplyMovement()
    {
        float forwardInput = 0f;
        float sidewaysInput = 0f;
        
        if (Input.GetKey(KeyCode.W)) forwardInput = 1f;
        if (Input.GetKey(KeyCode.S)) forwardInput = -1f;
        if (Input.GetKey(KeyCode.A)) sidewaysInput = -1f;
        if (Input.GetKey(KeyCode.D)) sidewaysInput = 1f;
        
        Vector3 forwardForce = transform.forward * forwardInput * _forwardSpeed;
        Vector3 sidewaysForce = transform.right * sidewaysInput * _sidewaysSpeed;
        
        _rigidbody.AddForce(forwardForce + sidewaysForce);
        
        float tiltX = -forwardInput * _tiltAmount;
        float tiltZ = -sidewaysInput * _tiltAmount;
        
        Vector3 targetRotation = new Vector3(
            _initialRotation.x + tiltX,
            transform.eulerAngles.y,
            _initialRotation.z + tiltZ
        );
        
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(targetRotation), _tiltSpeed * Time.deltaTime);
    }
    
    private void ApplyStabilization()
    {
        Vector3 stabilizationForce = -_rigidbody.velocity * _stabilizationForce * Time.fixedDeltaTime;
        stabilizationForce.y *= 0.1f;
        _rigidbody.AddForce(stabilizationForce);
        
        Vector3 angularStabilization = -_rigidbody.angularVelocity * _stabilizationForce * 0.1f * Time.fixedDeltaTime;
        _rigidbody.AddTorque(angularStabilization);
    }
    
    private void CheckGroundContact()
    {
        RaycastHit hit;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, _minAltitude + 0.5f);
        
        if (_isGrounded && hit.distance < _minAltitude)
        {
            Vector3 position = transform.position;
            position.y = hit.point.y + _minAltitude;
            transform.position = position;
            
            Vector3 velocity = _rigidbody.velocity;
            velocity.y = Mathf.Max(velocity.y, 0f);
            _rigidbody.velocity = velocity;
        }
    }
    
    private void UpdateAudio()
    {
        if (_rotorAudioSource != null)
        {
            float pitchTarget = Mathf.Lerp(_minRotorPitch, _maxRotorPitch, _currentThrottle);
            _rotorAudioSource.pitch = Mathf.Lerp(_rotorAudioSource.pitch, pitchTarget, Time.deltaTime * 2f);
            
            if (!_rotorAudioSource.isPlaying)
            {
                _rotorAudioSource.Play();
            }
        }
    }
    
    private void UpdateEffects()
    {
        if (_dustEffect != null)
        {
            bool shouldPlayDust = _isGrounded && transform.position.y < _dustEffectHeight;
            
            if (shouldPlayDust && !_dustEffect.isPlaying)
            {
                _dustEffect.Play();
            }
            else if (!shouldPlayDust && _dustEffect.isPlaying)
            {
                _dustEffect.Stop();
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * _targetAltitude, 1f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position + Vector3.up * _maxAltitude, Vector3.one * 2f);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position + Vector3.up * _minAltitude, Vector3.one * 2f);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, Vector3.down * (_minAltitude + 0.5f));
    }
}