// Prompt: wing glide
// Type: general

using UnityEngine;

public class WingGlide : MonoBehaviour
{
    [Header("Glide Settings")]
    [SerializeField] private float _glideSpeed = 8f;
    [SerializeField] private float _glideGravity = 2f;
    [SerializeField] private float _normalGravity = 9.81f;
    [SerializeField] private float _maxGlideTime = 10f;
    [SerializeField] private KeyCode _glideKey = KeyCode.Space;
    
    [Header("Wing Animation")]
    [SerializeField] private Transform _leftWing;
    [SerializeField] private Transform _rightWing;
    [SerializeField] private float _wingFlapSpeed = 2f;
    [SerializeField] private float _wingFlapAmount = 15f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _glideStartSound;
    [SerializeField] private AudioClip _glideLoopSound;
    [SerializeField] private AudioClip _glideEndSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _windTrailEffect;
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _cameraShakeAmount = 0.1f;
    
    private Rigidbody _rigidbody;
    private bool _isGliding = false;
    private float _currentGlideTime = 0f;
    private Vector3 _originalCameraPosition;
    private float _wingFlapTimer = 0f;
    private bool _canGlide = true;
    private float _glideStamina = 100f;
    private float _maxGlideStamina = 100f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                _cameraTransform = mainCamera.transform;
            }
        }
        
        if (_cameraTransform != null)
        {
            _originalCameraPosition = _cameraTransform.localPosition;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _glideStamina = _maxGlideStamina;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateWingAnimation();
        UpdateStamina();
        
        if (_isGliding)
        {
            _currentGlideTime += Time.deltaTime;
            
            if (_currentGlideTime >= _maxGlideTime || _glideStamina <= 0f || !Input.GetKey(_glideKey))
            {
                StopGliding();
            }
        }
    }
    
    private void FixedUpdate()
    {
        if (_isGliding)
        {
            ApplyGlidePhysics();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_glideKey) && _canGlide && _glideStamina > 10f && !_isGliding)
        {
            StartGliding();
        }
        
        if (Input.GetKeyUp(_glideKey) && _isGliding)
        {
            StopGliding();
        }
    }
    
    private void StartGliding()
    {
        _isGliding = true;
        _currentGlideTime = 0f;
        _canGlide = true;
        
        // Reduce gravity for gliding effect
        _rigidbody.drag = 1f;
        
        // Play start sound
        if (_audioSource != null && _glideStartSound != null)
        {
            _audioSource.PlayOneShot(_glideStartSound);
        }
        
        // Start loop sound
        if (_audioSource != null && _glideLoopSound != null)
        {
            _audioSource.clip = _glideLoopSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        // Start particle effect
        if (_windTrailEffect != null)
        {
            _windTrailEffect.Play();
        }
        
        // Extend wings
        AnimateWingsOpen();
    }
    
    private void StopGliding()
    {
        _isGliding = false;
        _currentGlideTime = 0f;
        
        // Restore normal physics
        _rigidbody.drag = 0f;
        
        // Stop loop sound and play end sound
        if (_audioSource != null)
        {
            _audioSource.Stop();
            if (_glideEndSound != null)
            {
                _audioSource.PlayOneShot(_glideEndSound);
            }
        }
        
        // Stop particle effect
        if (_windTrailEffect != null)
        {
            _windTrailEffect.Stop();
        }
        
        // Fold wings
        AnimateWingsClose();
        
        // Reset camera
        if (_cameraTransform != null)
        {
            _cameraTransform.localPosition = _originalCameraPosition;
        }
    }
    
    private void ApplyGlidePhysics()
    {
        Vector3 velocity = _rigidbody.velocity;
        
        // Apply reduced gravity
        Vector3 gravityForce = Vector3.up * (-_glideGravity * _rigidbody.mass);
        _rigidbody.AddForce(gravityForce, ForceMode.Force);
        
        // Apply forward glide force
        Vector3 forwardDirection = transform.forward;
        Vector3 glideForce = forwardDirection * _glideSpeed;
        _rigidbody.AddForce(glideForce, ForceMode.Force);
        
        // Limit vertical fall speed
        if (velocity.y < -5f)
        {
            velocity.y = -5f;
            _rigidbody.velocity = velocity;
        }
        
        // Add slight camera shake for immersion
        if (_cameraTransform != null)
        {
            Vector3 shakeOffset = new Vector3(
                Random.Range(-_cameraShakeAmount, _cameraShakeAmount),
                Random.Range(-_cameraShakeAmount, _cameraShakeAmount),
                0f
            );
            _cameraTransform.localPosition = _originalCameraPosition + shakeOffset;
        }
    }
    
    private void UpdateWingAnimation()
    {
        if (_isGliding)
        {
            _wingFlapTimer += Time.deltaTime * _wingFlapSpeed;
            float flapOffset = Mathf.Sin(_wingFlapTimer) * _wingFlapAmount;
            
            if (_leftWing != null)
            {
                _leftWing.localRotation = Quaternion.Euler(0f, 0f, flapOffset);
            }
            
            if (_rightWing != null)
            {
                _rightWing.localRotation = Quaternion.Euler(0f, 0f, -flapOffset);
            }
        }
    }
    
    private void AnimateWingsOpen()
    {
        if (_leftWing != null)
        {
            _leftWing.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        
        if (_rightWing != null)
        {
            _rightWing.localRotation = Quaternion.Euler(0f, 0f, -45f);
        }
    }
    
    private void AnimateWingsClose()
    {
        if (_leftWing != null)
        {
            _leftWing.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
        
        if (_rightWing != null)
        {
            _rightWing.localRotation = Quaternion.Euler(0f, 0f, 0f);
        }
    }
    
    private void UpdateStamina()
    {
        if (_isGliding)
        {
            _glideStamina -= 20f * Time.deltaTime;
            _glideStamina = Mathf.Max(0f, _glideStamina);
        }
        else
        {
            _glideStamina += 10f * Time.deltaTime;
            _glideStamina = Mathf.Min(_maxGlideStamina, _glideStamina);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_isGliding)
        {
            StopGliding();
        }
    }
    
    public bool IsGliding()
    {
        return _isGliding;
    }
    
    public float GetGlideStamina()
    {
        return _glideStamina;
    }
    
    public float GetGlideStaminaPercentage()
    {
        return _glideStamina / _maxGlideStamina;
    }
}