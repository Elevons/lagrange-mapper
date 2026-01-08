// Prompt: boots that increase speed
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SpeedBoots : MonoBehaviour
{
    [Header("Speed Boost Settings")]
    [SerializeField] private float _speedMultiplier = 1.5f;
    [SerializeField] private float _boostDuration = 10f;
    [SerializeField] private bool _isPermanent = false;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private ParticleSystem _speedTrailEffect;
    
    [Header("Events")]
    public UnityEvent OnBootsPickedUp;
    public UnityEvent OnSpeedBoostExpired;
    
    private AudioSource _audioSource;
    private bool _hasBeenPickedUp = false;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasBeenPickedUp) return;
        
        if (other.CompareTag("Player"))
        {
            ApplySpeedBoost(other.gameObject);
        }
    }
    
    private void ApplySpeedBoost(GameObject player)
    {
        CharacterController characterController = player.GetComponent<CharacterController>();
        Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
        
        if (characterController != null)
        {
            ApplySpeedBoostToCharacterController(characterController);
        }
        else if (playerRigidbody != null)
        {
            ApplySpeedBoostToRigidbody(player);
        }
        else
        {
            ApplySpeedBoostToTransform(player);
        }
        
        PlayPickupEffects();
        OnBootsPickedUp?.Invoke();
        _hasBeenPickedUp = true;
        
        if (!_isPermanent)
        {
            Invoke(nameof(RemoveSpeedBoost), _boostDuration);
        }
        
        gameObject.SetActive(false);
    }
    
    private void ApplySpeedBoostToCharacterController(CharacterController controller)
    {
        SpeedBoostComponent speedBoost = controller.gameObject.GetComponent<SpeedBoostComponent>();
        if (speedBoost == null)
        {
            speedBoost = controller.gameObject.AddComponent<SpeedBoostComponent>();
        }
        
        speedBoost.Initialize(_speedMultiplier, _boostDuration, _isPermanent, _speedTrailEffect);
    }
    
    private void ApplySpeedBoostToRigidbody(GameObject player)
    {
        SpeedBoostComponent speedBoost = player.GetComponent<SpeedBoostComponent>();
        if (speedBoost == null)
        {
            speedBoost = player.AddComponent<SpeedBoostComponent>();
        }
        
        speedBoost.Initialize(_speedMultiplier, _boostDuration, _isPermanent, _speedTrailEffect);
    }
    
    private void ApplySpeedBoostToTransform(GameObject player)
    {
        SpeedBoostComponent speedBoost = player.GetComponent<SpeedBoostComponent>();
        if (speedBoost == null)
        {
            speedBoost = player.AddComponent<SpeedBoostComponent>();
        }
        
        speedBoost.Initialize(_speedMultiplier, _boostDuration, _isPermanent, _speedTrailEffect);
    }
    
    private void PlayPickupEffects()
    {
        if (_pickupEffect != null)
        {
            Instantiate(_pickupEffect, transform.position, transform.rotation);
        }
        
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
    }
    
    private void RemoveSpeedBoost()
    {
        OnSpeedBoostExpired?.Invoke();
    }
}

public class SpeedBoostComponent : MonoBehaviour
{
    private float _originalSpeed;
    private float _speedMultiplier;
    private float _boostDuration;
    private bool _isPermanent;
    private ParticleSystem _trailEffect;
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private bool _isActive = false;
    
    public void Initialize(float speedMultiplier, float duration, bool isPermanent, ParticleSystem trailEffect)
    {
        _speedMultiplier = speedMultiplier;
        _boostDuration = duration;
        _isPermanent = isPermanent;
        _trailEffect = trailEffect;
        
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        
        ApplySpeedBoost();
        
        if (_trailEffect != null)
        {
            ParticleSystem trail = Instantiate(_trailEffect, transform);
            trail.Play();
            
            if (!_isPermanent)
            {
                Destroy(trail.gameObject, _boostDuration);
            }
        }
        
        if (!_isPermanent)
        {
            Invoke(nameof(RemoveSpeedBoost), _boostDuration);
        }
    }
    
    private void ApplySpeedBoost()
    {
        if (_isActive) return;
        
        _isActive = true;
        
        // Store original speed based on component type
        if (_characterController != null)
        {
            // For CharacterController, we'll modify movement in a basic movement script
            BasicPlayerMovement movement = GetComponent<BasicPlayerMovement>();
            if (movement == null)
            {
                movement = gameObject.AddComponent<BasicPlayerMovement>();
            }
            movement.ApplySpeedMultiplier(_speedMultiplier);
        }
        else if (_rigidbody != null)
        {
            // For Rigidbody, we'll modify drag to simulate speed increase
            _originalSpeed = _rigidbody.drag;
            _rigidbody.drag = _originalSpeed / _speedMultiplier;
        }
    }
    
    private void RemoveSpeedBoost()
    {
        if (!_isActive) return;
        
        _isActive = false;
        
        if (_characterController != null)
        {
            BasicPlayerMovement movement = GetComponent<BasicPlayerMovement>();
            if (movement != null)
            {
                movement.RemoveSpeedMultiplier();
            }
        }
        else if (_rigidbody != null)
        {
            _rigidbody.drag = _originalSpeed;
        }
        
        if (!_isPermanent)
        {
            Destroy(this);
        }
    }
}

public class BasicPlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _baseSpeed = 5f;
    [SerializeField] private float _jumpForce = 5f;
    
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private float _currentSpeedMultiplier = 1f;
    private Vector3 _velocity;
    private bool _isGrounded;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
    }
    
    private void Update()
    {
        if (_characterController != null)
        {
            HandleCharacterControllerMovement();
        }
        else if (_rigidbody != null)
        {
            HandleRigidbodyMovement();
        }
    }
    
    private void HandleCharacterControllerMovement()
    {
        _isGrounded = _characterController.isGrounded;
        
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (direction.magnitude >= 0.1f)
        {
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            _characterController.Move(moveDirection * _baseSpeed * _currentSpeedMultiplier * Time.deltaTime);
        }
        
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(_jumpForce * -2f * Physics.gravity.y);
        }
        
        _velocity.y += Physics.gravity.y * Time.deltaTime;
        _characterController.Move(_velocity * Time.deltaTime);
    }
    
    private void HandleRigidbodyMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (direction.magnitude >= 0.1f)
        {
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            _rigidbody.MovePosition(transform.position + moveDirection * _baseSpeed * _currentSpeedMultiplier * Time.deltaTime);
        }
        
        if (Input.GetButtonDown("Jump"))
        {
            _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
    }
    
    public void ApplySpeedMultiplier(float multiplier)
    {
        _currentSpeedMultiplier = multiplier;
    }
    
    public void RemoveSpeedMultiplier()
    {
        _currentSpeedMultiplier = 1f;
    }
}