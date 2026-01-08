// Prompt: submarine underwater
// Type: general

using UnityEngine;

public class SubmarineController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _forwardSpeed = 10f;
    [SerializeField] private float _rotationSpeed = 50f;
    [SerializeField] private float _verticalSpeed = 5f;
    [SerializeField] private float _maxDepth = -50f;
    [SerializeField] private float _minDepth = -2f;
    
    [Header("Physics")]
    [SerializeField] private float _buoyancyForce = 100f;
    [SerializeField] private float _waterDrag = 2f;
    [SerializeField] private float _waterAngularDrag = 5f;
    
    [Header("Ballast System")]
    [SerializeField] private float _ballastFillRate = 2f;
    [SerializeField] private float _ballastEmptyRate = 3f;
    [SerializeField] private float _maxBallastWeight = 500f;
    
    [Header("Sonar")]
    [SerializeField] private float _sonarRange = 100f;
    [SerializeField] private LayerMask _sonarLayerMask = -1;
    [SerializeField] private AudioSource _sonarAudioSource;
    [SerializeField] private AudioClip _sonarPingSound;
    
    [Header("Lights")]
    [SerializeField] private Light[] _submarineLights;
    [SerializeField] private float _lightIntensityAtSurface = 0.5f;
    [SerializeField] private float _lightIntensityAtDepth = 2f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private ParticleSystem _propellerEffect;
    [SerializeField] private Transform _propellerTransform;
    [SerializeField] private float _propellerRotationSpeed = 360f;
    
    private Rigidbody _rigidbody;
    private float _currentBallastWeight = 0f;
    private float _waterSurfaceY = 0f;
    private bool _isSubmerged = false;
    private float _currentDepth = 0f;
    private float _sonarCooldown = 0f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        SetupUnderwaterPhysics();
        InitializeLights();
        InitializeEffects();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateDepth();
        UpdateLighting();
        UpdateEffects();
        UpdateSonar();
    }
    
    private void FixedUpdate()
    {
        ApplyBuoyancy();
        ApplyMovement();
        ApplyBallastSystem();
    }
    
    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float dive = 0f;
        
        if (Input.GetKey(KeyCode.Q)) dive = 1f;
        if (Input.GetKey(KeyCode.E)) dive = -1f;
        
        // Store input for FixedUpdate
        _horizontalInput = horizontal;
        _verticalInput = vertical;
        _diveInput = dive;
        
        // Ballast controls
        if (Input.GetKey(KeyCode.F))
        {
            FillBallast();
        }
        else if (Input.GetKey(KeyCode.R))
        {
            EmptyBallast();
        }
        
        // Sonar
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerSonar();
        }
        
        // Lights toggle
        if (Input.GetKeyDown(KeyCode.L))
        {
            ToggleLights();
        }
    }
    
    private float _horizontalInput;
    private float _verticalInput;
    private float _diveInput;
    
    private void ApplyMovement()
    {
        // Forward/backward movement
        Vector3 forwardForce = transform.forward * _verticalInput * _forwardSpeed;
        _rigidbody.AddForce(forwardForce);
        
        // Rotation
        float torque = _horizontalInput * _rotationSpeed;
        _rigidbody.AddTorque(0, torque, 0);
        
        // Vertical movement (diving/surfacing)
        Vector3 verticalForce = Vector3.up * _diveInput * _verticalSpeed;
        _rigidbody.AddForce(verticalForce);
        
        // Clamp depth
        Vector3 position = transform.position;
        position.y = Mathf.Clamp(position.y, _maxDepth, _minDepth);
        transform.position = position;
    }
    
    private void ApplyBuoyancy()
    {
        if (_isSubmerged)
        {
            float buoyancyAdjustment = _buoyancyForce - _currentBallastWeight;
            _rigidbody.AddForce(Vector3.up * buoyancyAdjustment);
        }
    }
    
    private void ApplyBallastSystem()
    {
        // Natural ballast adjustment based on depth
        float depthRatio = Mathf.Abs(_currentDepth) / Mathf.Abs(_maxDepth);
        float targetBallast = depthRatio * _maxBallastWeight * 0.3f;
        
        if (_currentBallastWeight < targetBallast)
        {
            _currentBallastWeight += _ballastFillRate * Time.fixedDeltaTime;
        }
        else if (_currentBallastWeight > targetBallast)
        {
            _currentBallastWeight -= _ballastEmptyRate * Time.fixedDeltaTime;
        }
        
        _currentBallastWeight = Mathf.Clamp(_currentBallastWeight, 0f, _maxBallastWeight);
    }
    
    private void FillBallast()
    {
        _currentBallastWeight += _ballastFillRate * Time.deltaTime;
        _currentBallastWeight = Mathf.Clamp(_currentBallastWeight, 0f, _maxBallastWeight);
    }
    
    private void EmptyBallast()
    {
        _currentBallastWeight -= _ballastEmptyRate * Time.deltaTime;
        _currentBallastWeight = Mathf.Clamp(_currentBallastWeight, 0f, _maxBallastWeight);
    }
    
    private void SetupUnderwaterPhysics()
    {
        _rigidbody.drag = _waterDrag;
        _rigidbody.angularDrag = _waterAngularDrag;
        _rigidbody.useGravity = true;
    }
    
    private void UpdateDepth()
    {
        _currentDepth = transform.position.y - _waterSurfaceY;
        _isSubmerged = _currentDepth < 0f;
    }
    
    private void InitializeLights()
    {
        if (_submarineLights == null || _submarineLights.Length == 0)
        {
            _submarineLights = GetComponentsInChildren<Light>();
        }
    }
    
    private void UpdateLighting()
    {
        if (_submarineLights == null) return;
        
        float depthRatio = Mathf.Abs(_currentDepth) / Mathf.Abs(_maxDepth);
        float targetIntensity = Mathf.Lerp(_lightIntensityAtSurface, _lightIntensityAtDepth, depthRatio);
        
        foreach (Light light in _submarineLights)
        {
            if (light != null)
            {
                light.intensity = targetIntensity;
            }
        }
    }
    
    private void ToggleLights()
    {
        if (_submarineLights == null) return;
        
        foreach (Light light in _submarineLights)
        {
            if (light != null)
            {
                light.enabled = !light.enabled;
            }
        }
    }
    
    private void InitializeEffects()
    {
        if (_bubbleEffect != null)
        {
            var emission = _bubbleEffect.emission;
            emission.enabled = false;
        }
        
        if (_propellerEffect != null)
        {
            var emission = _propellerEffect.emission;
            emission.enabled = false;
        }
    }
    
    private void UpdateEffects()
    {
        UpdateBubbleEffect();
        UpdatePropellerEffect();
        UpdatePropellerRotation();
    }
    
    private void UpdateBubbleEffect()
    {
        if (_bubbleEffect == null) return;
        
        var emission = _bubbleEffect.emission;
        bool shouldEmit = _isSubmerged && (_diveInput != 0f || _rigidbody.velocity.magnitude > 1f);
        emission.enabled = shouldEmit;
        
        if (shouldEmit)
        {
            emission.rateOverTime = Mathf.Abs(_currentDepth) * 2f;
        }
    }
    
    private void UpdatePropellerEffect()
    {
        if (_propellerEffect == null) return;
        
        var emission = _propellerEffect.emission;
        bool shouldEmit = Mathf.Abs(_verticalInput) > 0.1f;
        emission.enabled = shouldEmit;
        
        if (shouldEmit)
        {
            emission.rateOverTime = Mathf.Abs(_verticalInput) * 50f;
        }
    }
    
    private void UpdatePropellerRotation()
    {
        if (_propellerTransform == null) return;
        
        float rotationAmount = _verticalInput * _propellerRotationSpeed * Time.deltaTime;
        _propellerTransform.Rotate(0, 0, rotationAmount);
    }
    
    private void UpdateSonar()
    {
        if (_sonarCooldown > 0f)
        {
            _sonarCooldown -= Time.deltaTime;
        }
    }
    
    private void TriggerSonar()
    {
        if (_sonarCooldown > 0f) return;
        
        _sonarCooldown = 2f;
        
        if (_sonarAudioSource != null && _sonarPingSound != null)
        {
            _sonarAudioSource.PlayOneShot(_sonarPingSound);
        }
        
        // Detect objects in range
        Collider[] detectedObjects = Physics.OverlapSphere(transform.position, _sonarRange, _sonarLayerMask);
        
        foreach (Collider col in detectedObjects)
        {
            if (col.gameObject != gameObject)
            {
                Debug.Log($"Sonar detected: {col.name} at distance {Vector3.Distance(transform.position, col.transform.position):F1}m");
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player entered submarine trigger zone");
        }
        
        if (other.name.Contains("Fish") || other.CompareTag("Fish"))
        {
            Debug.Log("Fish detected near submarine");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw sonar range
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _sonarRange);
        
        // Draw depth limits
        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(transform.position.x - 5f, _maxDepth, transform.position.z),
                       new Vector3(transform.position.x + 5f, _maxDepth, transform.position.z));
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(transform.position.x - 5f, _minDepth, transform.position.z),
                       new Vector3(transform.position.x + 5f, _minDepth, transform.position.z));
    }
}