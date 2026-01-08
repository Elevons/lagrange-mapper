// Prompt: glider with wind currents
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Glider : MonoBehaviour
{
    [Header("Glider Physics")]
    [SerializeField] private float _liftForce = 15f;
    [SerializeField] private float _dragCoefficient = 0.98f;
    [SerializeField] private float _minGlideSpeed = 5f;
    [SerializeField] private float _maxGlideSpeed = 25f;
    [SerializeField] private float _stallSpeed = 3f;
    
    [Header("Control Settings")]
    [SerializeField] private float _pitchSensitivity = 2f;
    [SerializeField] private float _rollSensitivity = 1.5f;
    [SerializeField] private float _yawSensitivity = 1f;
    [SerializeField] private float _maxPitchAngle = 45f;
    [SerializeField] private float _maxRollAngle = 60f;
    
    [Header("Wind Response")]
    [SerializeField] private float _windInfluenceMultiplier = 1.2f;
    [SerializeField] private float _turbulenceResistance = 0.8f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _windAudioSource;
    [SerializeField] private AudioClip _windSoundClip;
    [SerializeField] private float _minWindVolume = 0.1f;
    [SerializeField] private float _maxWindVolume = 0.8f;
    
    [Header("Events")]
    public UnityEvent OnStall;
    public UnityEvent OnLanding;
    public UnityEvent<float> OnSpeedChanged;
    
    private Rigidbody _rigidbody;
    private bool _isGrounded = false;
    private bool _isStalling = false;
    private float _currentSpeed;
    private Vector3 _windVelocity;
    private WindCurrent _currentWindZone;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = true;
        _rigidbody.drag = 0.1f;
        _rigidbody.angularDrag = 5f;
        
        if (_windAudioSource == null)
        {
            _windAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        SetupAudio();
    }
    
    private void SetupAudio()
    {
        if (_windAudioSource != null && _windSoundClip != null)
        {
            _windAudioSource.clip = _windSoundClip;
            _windAudioSource.loop = true;
            _windAudioSource.volume = _minWindVolume;
            _windAudioSource.Play();
        }
    }
    
    private void Update()
    {
        if (_isGrounded) return;
        
        HandleInput();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        if (_isGrounded) return;
        
        CalculateAerodynamics();
        ApplyWindEffects();
        CheckStallCondition();
        
        _currentSpeed = _rigidbody.velocity.magnitude;
        OnSpeedChanged?.Invoke(_currentSpeed);
    }
    
    private void HandleInput()
    {
        float pitch = Input.GetAxis("Vertical") * _pitchSensitivity;
        float roll = -Input.GetAxis("Horizontal") * _rollSensitivity;
        float yaw = 0f;
        
        if (Input.GetKey(KeyCode.Q))
            yaw = -_yawSensitivity;
        else if (Input.GetKey(KeyCode.E))
            yaw = _yawSensitivity;
        
        ApplyControlInputs(pitch, roll, yaw);
    }
    
    private void ApplyControlInputs(float pitch, float roll, float yaw)
    {
        Vector3 torque = new Vector3(pitch, yaw, roll) * Time.fixedDeltaTime;
        
        Vector3 currentEuler = transform.eulerAngles;
        float normalizedPitch = Mathf.DeltaAngle(0, currentEuler.x);
        float normalizedRoll = Mathf.DeltaAngle(0, currentEuler.z);
        
        if (Mathf.Abs(normalizedPitch) > _maxPitchAngle && Mathf.Sign(pitch) == Mathf.Sign(normalizedPitch))
            torque.x = 0;
        
        if (Mathf.Abs(normalizedRoll) > _maxRollAngle && Mathf.Sign(roll) == Mathf.Sign(normalizedRoll))
            torque.z = 0;
        
        _rigidbody.AddTorque(torque, ForceMode.VelocityChange);
    }
    
    private void CalculateAerodynamics()
    {
        Vector3 velocity = _rigidbody.velocity;
        float speed = velocity.magnitude;
        
        if (speed < 0.1f) return;
        
        Vector3 forward = transform.forward;
        Vector3 up = transform.up;
        
        float angleOfAttack = Vector3.Angle(forward, velocity.normalized);
        float liftAmount = Mathf.Clamp01(1f - (angleOfAttack / 90f)) * _liftForce;
        
        Vector3 lift = up * liftAmount * speed * 0.1f;
        _rigidbody.AddForce(lift, ForceMode.Force);
        
        Vector3 drag = -velocity.normalized * speed * speed * 0.01f * _dragCoefficient;
        _rigidbody.AddForce(drag, ForceMode.Force);
        
        _rigidbody.velocity = Vector3.ClampMagnitude(_rigidbody.velocity, _maxGlideSpeed);
    }
    
    private void ApplyWindEffects()
    {
        if (_currentWindZone != null)
        {
            _windVelocity = _currentWindZone.GetWindVelocityAtPosition(transform.position);
            Vector3 windForce = _windVelocity * _windInfluenceMultiplier;
            
            if (_currentWindZone.HasTurbulence())
            {
                Vector3 turbulence = _currentWindZone.GetTurbulence(transform.position);
                windForce += turbulence * (1f - _turbulenceResistance);
            }
            
            _rigidbody.AddForce(windForce, ForceMode.Force);
        }
    }
    
    private void CheckStallCondition()
    {
        bool wasStalling = _isStalling;
        _isStalling = _currentSpeed < _stallSpeed;
        
        if (_isStalling && !wasStalling)
        {
            OnStall?.Invoke();
        }
        
        if (_isStalling)
        {
            _rigidbody.AddForce(Vector3.down * 20f, ForceMode.Force);
        }
    }
    
    private void UpdateAudio()
    {
        if (_windAudioSource != null && _windSoundClip != null)
        {
            float speedRatio = Mathf.Clamp01(_currentSpeed / _maxGlideSpeed);
            float windStrength = _windVelocity.magnitude * 0.1f;
            float targetVolume = Mathf.Lerp(_minWindVolume, _maxWindVolume, speedRatio + windStrength);
            
            _windAudioSource.volume = Mathf.Lerp(_windAudioSource.volume, targetVolume, Time.deltaTime * 2f);
            _windAudioSource.pitch = Mathf.Lerp(0.8f, 1.4f, speedRatio);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ground"))
        {
            _isGrounded = true;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            OnLanding?.Invoke();
        }
        
        WindCurrent windCurrent = other.GetComponent<WindCurrent>();
        if (windCurrent != null)
        {
            _currentWindZone = windCurrent;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Ground"))
        {
            _isGrounded = false;
        }
        
        WindCurrent windCurrent = other.GetComponent<WindCurrent>();
        if (windCurrent != null && windCurrent == _currentWindZone)
        {
            _currentWindZone = null;
            _windVelocity = Vector3.zero;
        }
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public bool IsStalling()
    {
        return _isStalling;
    }
    
    public Vector3 GetWindVelocity()
    {
        return _windVelocity;
    }
}

public class WindCurrent : MonoBehaviour
{
    [Header("Wind Properties")]
    [SerializeField] private Vector3 _windDirection = Vector3.forward;
    [SerializeField] private float _windStrength = 10f;
    [SerializeField] private AnimationCurve _strengthOverDistance = AnimationCurve.Linear(0, 1, 1, 0);
    
    [Header("Turbulence")]
    [SerializeField] private bool _hasTurbulence = false;
    [SerializeField] private float _turbulenceStrength = 2f;
    [SerializeField] private float _turbulenceFrequency = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _windParticles;
    [SerializeField] private Color _windColor = Color.white;
    
    private Collider _windZone;
    private Vector3 _center;
    private Vector3 _size;
    
    private void Start()
    {
        _windZone = GetComponent<Collider>();
        if (_windZone == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = Vector3.one * 10f;
            _windZone = boxCollider;
        }
        
        _windZone.isTrigger = true;
        _center = _windZone.bounds.center;
        _size = _windZone.bounds.size;
        
        SetupParticles();
    }
    
    private void SetupParticles()
    {
        if (_windParticles == null)
        {
            GameObject particleGO = new GameObject("WindParticles");
            particleGO.transform.SetParent(transform);
            particleGO.transform.localPosition = Vector3.zero;
            _windParticles = particleGO.AddComponent<ParticleSystem>();
        }
        
        var main = _windParticles.main;
        main.startColor = _windColor;
        main.startSpeed = _windStrength * 0.5f;
        main.maxParticles = 100;
        
        var shape = _windParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = _size;
        
        var velocityOverLifetime = _windParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.x = _windDirection.x * _windStrength;
        velocityOverLifetime.y = _windDirection.y * _windStrength;
        velocityOverLifetime.z = _windDirection.z * _windStrength;
    }
    
    public Vector3 GetWindVelocityAtPosition(Vector3 position)
    {
        float distance = Vector3.Distance(position, _center);
        float maxDistance = Mathf.Max(_size.x, _size.y, _size.z) * 0.5f;
        float normalizedDistance = Mathf.Clamp01(distance / maxDistance);
        
        float strengthMultiplier = _strengthOverDistance.Evaluate(normalizedDistance);
        return _windDirection.normalized * _windStrength * strengthMultiplier;
    }
    
    public bool HasTurbulence()
    {
        return _hasTurbulence;
    }
    
    public Vector3 GetTurbulence(Vector3 position)
    {
        if (!_hasTurbulence) return Vector3.zero;
        
        float time = Time.time * _turbulenceFrequency;
        float noiseX = Mathf.PerlinNoise(position.x * 0.1f + time, position.z * 0.1f) - 0.5f;
        float noiseY = Mathf.PerlinNoise(position.y * 0.1f + time, position.x * 0.1f) - 0.5f;
        float noiseZ = Mathf.PerlinNoise(position.z * 0.1f + time, position.y * 0.1f) - 0.5f;
        
        return new Vector3(noiseX, noiseY, noiseZ) * _turbulenceStrength;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _windColor;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        
        Gizmos.color = Color.yellow;
        Vector3 arrowEnd = _windDirection.normalized * 2f;
        Gizmos.DrawRay(Vector3.zero, arrowEnd);
        Gizmos.DrawWireSphere(arrowEnd, 0.2f);
    }
}