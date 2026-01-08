// Prompt: jetpack flight
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class JetpackFlight : MonoBehaviour
{
    [Header("Jetpack Settings")]
    [SerializeField] private float _thrustForce = 15f;
    [SerializeField] private float _maxFuel = 100f;
    [SerializeField] private float _fuelConsumptionRate = 20f;
    [SerializeField] private float _fuelRegenRate = 10f;
    [SerializeField] private float _maxSpeed = 20f;
    [SerializeField] private bool _requiresFuel = true;
    
    [Header("Controls")]
    [SerializeField] private KeyCode _thrustKey = KeyCode.Space;
    [SerializeField] private bool _useMouseInput = false;
    
    [Header("Physics")]
    [SerializeField] private float _drag = 2f;
    [SerializeField] private float _angularDrag = 5f;
    [SerializeField] private bool _stabilizeRotation = true;
    [SerializeField] private float _stabilizationForce = 50f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _thrustParticles;
    [SerializeField] private AudioSource _thrustAudio;
    [SerializeField] private Transform _thrustPoint;
    [SerializeField] private float _screenShakeIntensity = 0.1f;
    
    [Header("Events")]
    public UnityEvent OnJetpackStart;
    public UnityEvent OnJetpackStop;
    public UnityEvent OnFuelEmpty;
    public UnityEvent<float> OnFuelChanged;
    
    private Rigidbody _rigidbody;
    private float _currentFuel;
    private bool _isThrusting;
    private Vector3 _thrustDirection;
    private Camera _mainCamera;
    private Vector3 _originalCameraPosition;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _mainCamera = Camera.main;
        if (_mainCamera != null)
        {
            _originalCameraPosition = _mainCamera.transform.localPosition;
        }
    }
    
    private void Start()
    {
        _currentFuel = _maxFuel;
        _rigidbody.drag = _drag;
        _rigidbody.angularDrag = _angularDrag;
        
        if (_thrustPoint == null)
        {
            _thrustPoint = transform;
        }
        
        OnFuelChanged?.Invoke(_currentFuel / _maxFuel);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateFuel();
        UpdateEffects();
        UpdateScreenShake();
    }
    
    private void FixedUpdate()
    {
        if (_isThrusting && CanThrust())
        {
            ApplyThrust();
        }
        
        if (_stabilizeRotation && !_isThrusting)
        {
            StabilizeRotation();
        }
        
        LimitSpeed();
    }
    
    private void HandleInput()
    {
        bool thrustInput = false;
        
        if (_useMouseInput)
        {
            thrustInput = Input.GetMouseButton(0);
            
            Vector3 mousePosition = Input.mousePosition;
            Vector3 worldPosition = _mainCamera.ScreenToWorldPoint(new Vector3(mousePosition.x, mousePosition.y, 10f));
            _thrustDirection = (worldPosition - transform.position).normalized;
        }
        else
        {
            thrustInput = Input.GetKey(_thrustKey);
            
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            _thrustDirection = new Vector3(horizontal, vertical, 0f).normalized;
            
            if (_thrustDirection == Vector3.zero)
            {
                _thrustDirection = transform.up;
            }
        }
        
        if (thrustInput && CanThrust() && !_isThrusting)
        {
            StartThrust();
        }
        else if (!thrustInput && _isThrusting)
        {
            StopThrust();
        }
    }
    
    private bool CanThrust()
    {
        return !_requiresFuel || _currentFuel > 0f;
    }
    
    private void StartThrust()
    {
        _isThrusting = true;
        OnJetpackStart?.Invoke();
        
        if (_thrustParticles != null)
        {
            _thrustParticles.Play();
        }
        
        if (_thrustAudio != null && !_thrustAudio.isPlaying)
        {
            _thrustAudio.Play();
        }
    }
    
    private void StopThrust()
    {
        _isThrusting = false;
        OnJetpackStop?.Invoke();
        
        if (_thrustParticles != null)
        {
            _thrustParticles.Stop();
        }
        
        if (_thrustAudio != null)
        {
            _thrustAudio.Stop();
        }
    }
    
    private void ApplyThrust()
    {
        Vector3 force = _thrustDirection * _thrustForce;
        _rigidbody.AddForce(force, ForceMode.Acceleration);
        
        if (_thrustPoint != null)
        {
            _rigidbody.AddForceAtPosition(force, _thrustPoint.position, ForceMode.Acceleration);
        }
    }
    
    private void UpdateFuel()
    {
        if (!_requiresFuel) return;
        
        if (_isThrusting && _currentFuel > 0f)
        {
            _currentFuel -= _fuelConsumptionRate * Time.deltaTime;
            _currentFuel = Mathf.Max(0f, _currentFuel);
            
            if (_currentFuel <= 0f)
            {
                StopThrust();
                OnFuelEmpty?.Invoke();
            }
        }
        else if (!_isThrusting && _currentFuel < _maxFuel)
        {
            _currentFuel += _fuelRegenRate * Time.deltaTime;
            _currentFuel = Mathf.Min(_maxFuel, _currentFuel);
        }
        
        OnFuelChanged?.Invoke(_currentFuel / _maxFuel);
    }
    
    private void UpdateEffects()
    {
        if (_thrustParticles != null)
        {
            if (_isThrusting && CanThrust())
            {
                if (!_thrustParticles.isPlaying)
                {
                    _thrustParticles.Play();
                }
                
                var main = _thrustParticles.main;
                main.startSpeed = _thrustForce * 0.5f;
            }
            else if (_thrustParticles.isPlaying)
            {
                _thrustParticles.Stop();
            }
        }
        
        if (_thrustAudio != null)
        {
            if (_isThrusting && CanThrust())
            {
                if (!_thrustAudio.isPlaying)
                {
                    _thrustAudio.Play();
                }
                _thrustAudio.volume = Mathf.Lerp(0.1f, 1f, _rigidbody.velocity.magnitude / _maxSpeed);
            }
            else if (_thrustAudio.isPlaying)
            {
                _thrustAudio.Stop();
            }
        }
    }
    
    private void UpdateScreenShake()
    {
        if (_mainCamera == null || _screenShakeIntensity <= 0f) return;
        
        if (_isThrusting && CanThrust())
        {
            Vector3 shake = Random.insideUnitSphere * _screenShakeIntensity;
            _mainCamera.transform.localPosition = _originalCameraPosition + shake;
        }
        else
        {
            _mainCamera.transform.localPosition = Vector3.Lerp(_mainCamera.transform.localPosition, _originalCameraPosition, Time.deltaTime * 10f);
        }
    }
    
    private void StabilizeRotation()
    {
        Vector3 targetUp = Vector3.up;
        Vector3 currentUp = transform.up;
        Vector3 torque = Vector3.Cross(currentUp, targetUp) * _stabilizationForce;
        _rigidbody.AddTorque(torque, ForceMode.Acceleration);
    }
    
    private void LimitSpeed()
    {
        if (_rigidbody.velocity.magnitude > _maxSpeed)
        {
            _rigidbody.velocity = _rigidbody.velocity.normalized * _maxSpeed;
        }
    }
    
    public void RefillFuel()
    {
        _currentFuel = _maxFuel;
        OnFuelChanged?.Invoke(1f);
    }
    
    public void SetFuel(float amount)
    {
        _currentFuel = Mathf.Clamp(amount, 0f, _maxFuel);
        OnFuelChanged?.Invoke(_currentFuel / _maxFuel);
    }
    
    public float GetFuelPercentage()
    {
        return _currentFuel / _maxFuel;
    }
    
    public bool IsThrusting()
    {
        return _isThrusting;
    }
    
    public void SetThrustForce(float force)
    {
        _thrustForce = Mathf.Max(0f, force);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_thrustPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_thrustPoint.position, 0.5f);
            
            if (_isThrusting)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(_thrustPoint.position, -_thrustDirection * 2f);
            }
        }
    }
}