// Prompt: speed boost pickup that increases movement
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class SpeedBoostPickup : MonoBehaviour
{
    [Header("Speed Boost Settings")]
    [SerializeField] private float _speedMultiplier = 2f;
    [SerializeField] private float _boostDuration = 5f;
    [SerializeField] private bool _stackable = false;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    
    [Header("Events")]
    public UnityEvent OnPickedUp;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    private bool _isPickedUp = false;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    private void Update()
    {
        if (_isPickedUp) return;
        
        // Rotate the pickup
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        
        // Bob up and down
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        
        if (other.CompareTag("Player"))
        {
            ApplySpeedBoost(other.gameObject);
            PickupItem();
        }
    }
    
    private void ApplySpeedBoost(GameObject player)
    {
        SpeedBoostEffect boostEffect = player.GetComponent<SpeedBoostEffect>();
        
        if (boostEffect == null)
        {
            boostEffect = player.AddComponent<SpeedBoostEffect>();
        }
        
        boostEffect.ApplySpeedBoost(_speedMultiplier, _boostDuration, _stackable);
    }
    
    private void PickupItem()
    {
        _isPickedUp = true;
        
        // Play pickup sound
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        // Spawn pickup effect
        if (_pickupEffect != null)
        {
            Instantiate(_pickupEffect, transform.position, transform.rotation);
        }
        
        // Invoke event
        OnPickedUp?.Invoke();
        
        // Hide visual components
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null) renderer.enabled = false;
        
        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.enabled = false;
        
        // Destroy after sound finishes
        float destroyDelay = _pickupSound != null ? _pickupSound.length : 0.1f;
        Destroy(gameObject, destroyDelay);
    }
}

public class SpeedBoostEffect : MonoBehaviour
{
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private float _originalSpeed;
    private float _currentMultiplier = 1f;
    private float _boostEndTime;
    private bool _hasActiveBoost = false;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        
        // Store original speed from a basic movement script if it exists
        BasicMovement movement = GetComponent<BasicMovement>();
        if (movement != null)
        {
            _originalSpeed = movement.moveSpeed;
        }
        else
        {
            _originalSpeed = 5f; // Default fallback speed
        }
    }
    
    private void Update()
    {
        if (_hasActiveBoost && Time.time >= _boostEndTime)
        {
            RemoveSpeedBoost();
        }
    }
    
    public void ApplySpeedBoost(float multiplier, float duration, bool stackable)
    {
        if (stackable)
        {
            _currentMultiplier *= multiplier;
        }
        else
        {
            _currentMultiplier = Mathf.Max(_currentMultiplier, multiplier);
        }
        
        _boostEndTime = Time.time + duration;
        _hasActiveBoost = true;
        
        UpdateMovementSpeed();
    }
    
    private void UpdateMovementSpeed()
    {
        BasicMovement movement = GetComponent<BasicMovement>();
        if (movement != null)
        {
            movement.moveSpeed = _originalSpeed * _currentMultiplier;
        }
    }
    
    private void RemoveSpeedBoost()
    {
        _currentMultiplier = 1f;
        _hasActiveBoost = false;
        
        BasicMovement movement = GetComponent<BasicMovement>();
        if (movement != null)
        {
            movement.moveSpeed = _originalSpeed;
        }
    }
}

public class BasicMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 8f;
    
    private CharacterController _controller;
    private Rigidbody _rigidbody;
    private Vector3 _velocity;
    private bool _isGrounded;
    
    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
    }
    
    private void Update()
    {
        if (_controller != null)
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
        _isGrounded = _controller.isGrounded;
        
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
            _controller.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
        
        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpForce * -2f * Physics.gravity.y);
        }
        
        _velocity.y += Physics.gravity.y * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
    
    private void HandleRigidbodyMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 direction = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (direction.magnitude >= 0.1f)
        {
            Vector3 moveDirection = transform.right * horizontal + transform.forward * vertical;
            _rigidbody.MovePosition(transform.position + moveDirection * moveSpeed * Time.deltaTime);
        }
        
        if (Input.GetButtonDown("Jump"))
        {
            _rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }
}