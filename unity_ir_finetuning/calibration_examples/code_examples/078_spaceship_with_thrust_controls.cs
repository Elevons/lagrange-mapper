// Prompt: spaceship with thrust controls
// Type: general

using UnityEngine;

public class SpaceshipController : MonoBehaviour
{
    [Header("Thrust Settings")]
    [SerializeField] private float _thrustForce = 10f;
    [SerializeField] private float _rotationSpeed = 100f;
    [SerializeField] private float _maxVelocity = 20f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode _thrustKey = KeyCode.W;
    [SerializeField] private KeyCode _leftRotateKey = KeyCode.A;
    [SerializeField] private KeyCode _rightRotateKey = KeyCode.D;
    [SerializeField] private KeyCode _reverseKey = KeyCode.S;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _thrustParticles;
    [SerializeField] private AudioSource _thrustAudioSource;
    [SerializeField] private Transform _thrustPoint;
    
    [Header("Physics Settings")]
    [SerializeField] private float _drag = 0.98f;
    [SerializeField] private float _angularDrag = 0.95f;
    
    private Rigidbody _rigidbody;
    private bool _isThrusting;
    private Vector3 _thrustDirection;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _rigidbody.drag = 0f;
        _rigidbody.angularDrag = 0f;
        
        if (_thrustPoint == null)
        {
            _thrustPoint = transform;
        }
        
        if (_thrustParticles != null)
        {
            _thrustParticles.Stop();
        }
    }
    
    private void Update()
    {
        HandleInput();
        HandleVisualEffects();
    }
    
    private void FixedUpdate()
    {
        HandleThrust();
        HandleRotation();
        ApplyDrag();
        ClampVelocity();
    }
    
    private void HandleInput()
    {
        _isThrusting = Input.GetKey(_thrustKey) || Input.GetKey(_reverseKey);
        
        if (Input.GetKey(_thrustKey))
        {
            _thrustDirection = transform.forward;
        }
        else if (Input.GetKey(_reverseKey))
        {
            _thrustDirection = -transform.forward;
        }
        else
        {
            _thrustDirection = Vector3.zero;
        }
    }
    
    private void HandleThrust()
    {
        if (_isThrusting && _thrustDirection != Vector3.zero)
        {
            Vector3 thrustForceVector = _thrustDirection * _thrustForce;
            _rigidbody.AddForce(thrustForceVector, ForceMode.Force);
        }
    }
    
    private void HandleRotation()
    {
        float rotationInput = 0f;
        
        if (Input.GetKey(_leftRotateKey))
        {
            rotationInput = -1f;
        }
        else if (Input.GetKey(_rightRotateKey))
        {
            rotationInput = 1f;
        }
        
        if (Mathf.Abs(rotationInput) > 0.1f)
        {
            Vector3 torque = transform.up * rotationInput * _rotationSpeed;
            _rigidbody.AddTorque(torque, ForceMode.Force);
        }
    }
    
    private void ApplyDrag()
    {
        _rigidbody.velocity *= _drag;
        _rigidbody.angularVelocity *= _angularDrag;
    }
    
    private void ClampVelocity()
    {
        if (_rigidbody.velocity.magnitude > _maxVelocity)
        {
            _rigidbody.velocity = _rigidbody.velocity.normalized * _maxVelocity;
        }
    }
    
    private void HandleVisualEffects()
    {
        if (_thrustParticles != null)
        {
            if (_isThrusting && Input.GetKey(_thrustKey))
            {
                if (!_thrustParticles.isPlaying)
                {
                    _thrustParticles.Play();
                }
            }
            else
            {
                if (_thrustParticles.isPlaying)
                {
                    _thrustParticles.Stop();
                }
            }
        }
        
        if (_thrustAudioSource != null)
        {
            if (_isThrusting)
            {
                if (!_thrustAudioSource.isPlaying)
                {
                    _thrustAudioSource.Play();
                }
            }
            else
            {
                if (_thrustAudioSource.isPlaying)
                {
                    _thrustAudioSource.Stop();
                }
            }
        }
    }
    
    public void SetThrustForce(float force)
    {
        _thrustForce = Mathf.Max(0f, force);
    }
    
    public void SetRotationSpeed(float speed)
    {
        _rotationSpeed = Mathf.Max(0f, speed);
    }
    
    public void SetMaxVelocity(float maxVel)
    {
        _maxVelocity = Mathf.Max(0f, maxVel);
    }
    
    public Vector3 GetVelocity()
    {
        return _rigidbody != null ? _rigidbody.velocity : Vector3.zero;
    }
    
    public float GetSpeed()
    {
        return _rigidbody != null ? _rigidbody.velocity.magnitude : 0f;
    }
    
    public bool IsThrusting()
    {
        return _isThrusting;
    }
    
    private void OnValidate()
    {
        _thrustForce = Mathf.Max(0f, _thrustForce);
        _rotationSpeed = Mathf.Max(0f, _rotationSpeed);
        _maxVelocity = Mathf.Max(0f, _maxVelocity);
        _drag = Mathf.Clamp01(_drag);
        _angularDrag = Mathf.Clamp01(_angularDrag);
    }
}