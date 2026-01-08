// Prompt: wolf mount running
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class WolfMount : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 3f;
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private float _deceleration = 15f;
    [SerializeField] private float _turnSpeed = 180f;
    
    [Header("Stamina Settings")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaDrainRate = 20f;
    [SerializeField] private float _staminaRegenRate = 15f;
    [SerializeField] private float _minStaminaToRun = 10f;
    
    [Header("Animation Settings")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _speedParameter = "Speed";
    [SerializeField] private string _isRunningParameter = "IsRunning";
    [SerializeField] private string _isMountedParameter = "IsMounted";
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _footstepSounds;
    [SerializeField] private AudioClip[] _breathingSounds;
    [SerializeField] private float _footstepInterval = 0.5f;
    
    [Header("Mounting Settings")]
    [SerializeField] private Transform _mountPoint;
    [SerializeField] private float _mountRange = 2f;
    [SerializeField] private KeyCode _mountKey = KeyCode.E;
    [SerializeField] private KeyCode _dismountKey = KeyCode.F;
    
    [Header("Events")]
    public UnityEvent OnMounted;
    public UnityEvent OnDismounted;
    public UnityEvent OnStaminaDepleted;
    
    private CharacterController _characterController;
    private Transform _rider;
    private bool _isMounted = false;
    private bool _isRunning = false;
    private float _currentSpeed = 0f;
    private float _currentStamina;
    private float _footstepTimer = 0f;
    private Vector3 _moveDirection = Vector3.zero;
    private Camera _playerCamera;
    private Vector3 _lastPosition;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        if (_characterController == null)
            _characterController = gameObject.AddComponent<CharacterController>();
            
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_mountPoint == null)
        {
            GameObject mountPointObj = new GameObject("MountPoint");
            mountPointObj.transform.SetParent(transform);
            mountPointObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            _mountPoint = mountPointObj.transform;
        }
        
        _currentStamina = _maxStamina;
        _lastPosition = transform.position;
        _playerCamera = Camera.main;
    }
    
    private void Update()
    {
        HandleMountingInput();
        
        if (_isMounted && _rider != null)
        {
            HandleMovementInput();
            HandleStamina();
            UpdateAnimation();
            HandleAudio();
        }
        else
        {
            CheckForNearbyPlayer();
        }
    }
    
    private void FixedUpdate()
    {
        if (_isMounted && _rider != null)
        {
            ApplyMovement();
        }
    }
    
    private void HandleMountingInput()
    {
        if (Input.GetKeyDown(_mountKey) && !_isMounted)
        {
            TryMount();
        }
        else if (Input.GetKeyDown(_dismountKey) && _isMounted)
        {
            Dismount();
        }
    }
    
    private void CheckForNearbyPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= _mountRange)
            {
                // Visual indicator could be added here
            }
        }
    }
    
    private void TryMount()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= _mountRange)
            {
                Mount(player.transform);
            }
        }
    }
    
    private void Mount(Transform rider)
    {
        _rider = rider;
        _isMounted = true;
        
        // Disable player's character controller if it exists
        CharacterController playerController = _rider.GetComponent<CharacterController>();
        if (playerController != null)
            playerController.enabled = false;
            
        // Position rider on mount point
        _rider.position = _mountPoint.position;
        _rider.SetParent(_mountPoint);
        
        // Update animation
        if (_animator != null)
            _animator.SetBool(_isMountedParameter, true);
            
        OnMounted?.Invoke();
    }
    
    private void Dismount()
    {
        if (_rider != null)
        {
            // Re-enable player's character controller
            CharacterController playerController = _rider.GetComponent<CharacterController>();
            if (playerController != null)
                playerController.enabled = true;
                
            // Position rider next to wolf
            _rider.SetParent(null);
            _rider.position = transform.position + transform.right * 2f;
        }
        
        _rider = null;
        _isMounted = false;
        _isRunning = false;
        _currentSpeed = 0f;
        
        // Update animation
        if (_animator != null)
        {
            _animator.SetBool(_isMountedParameter, false);
            _animator.SetBool(_isRunningParameter, false);
            _animator.SetFloat(_speedParameter, 0f);
        }
        
        OnDismounted?.Invoke();
    }
    
    private void HandleMovementInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool runInput = Input.GetKey(KeyCode.LeftShift);
        
        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;
        
        if (inputDirection.magnitude > 0.1f)
        {
            // Calculate movement direction relative to camera
            Vector3 cameraForward = _playerCamera.transform.forward;
            Vector3 cameraRight = _playerCamera.transform.right;
            cameraForward.y = 0;
            cameraRight.y = 0;
            cameraForward.Normalize();
            cameraRight.Normalize();
            
            _moveDirection = (cameraForward * vertical + cameraRight * horizontal).normalized;
            
            // Determine if running
            _isRunning = runInput && _currentStamina > _minStaminaToRun && inputDirection.magnitude > 0.8f;
            
            float targetSpeed = _isRunning ? _runSpeed : _walkSpeed;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, _acceleration * Time.deltaTime);
            
            // Rotate wolf to face movement direction
            if (_moveDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
            }
        }
        else
        {
            _isRunning = false;
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * Time.deltaTime);
        }
    }
    
    private void ApplyMovement()
    {
        Vector3 movement = _moveDirection * _currentSpeed;
        movement.y = -9.81f; // Apply gravity
        
        _characterController.Move(movement * Time.fixedDeltaTime);
    }
    
    private void HandleStamina()
    {
        if (_isRunning && _currentSpeed > _walkSpeed)
        {
            _currentStamina -= _staminaDrainRate * Time.deltaTime;
            _currentStamina = Mathf.Max(0f, _currentStamina);
            
            if (_currentStamina <= 0f)
            {
                OnStaminaDepleted?.Invoke();
            }
        }
        else if (_currentStamina < _maxStamina)
        {
            _currentStamina += _staminaRegenRate * Time.deltaTime;
            _currentStamina = Mathf.Min(_maxStamina, _currentStamina);
        }
    }
    
    private void UpdateAnimation()
    {
        if (_animator != null)
        {
            float normalizedSpeed = _currentSpeed / _runSpeed;
            _animator.SetFloat(_speedParameter, normalizedSpeed);
            _animator.SetBool(_isRunningParameter, _isRunning);
        }
    }
    
    private void HandleAudio()
    {
        if (_currentSpeed > 0.1f)
        {
            _footstepTimer += Time.deltaTime;
            float intervalMultiplier = _isRunning ? 0.7f : 1f;
            
            if (_footstepTimer >= _footstepInterval * intervalMultiplier)
            {
                PlayFootstepSound();
                _footstepTimer = 0f;
            }
        }
        
        // Play breathing sounds when running
        if (_isRunning && _breathingSounds.Length > 0 && !_audioSource.isPlaying)
        {
            AudioClip breathingClip = _breathingSounds[Random.Range(0, _breathingSounds.Length)];
            _audioSource.PlayOneShot(breathingClip, 0.3f);
        }
    }
    
    private void PlayFootstepSound()
    {
        if (_footstepSounds.Length > 0 && _audioSource != null)
        {
            AudioClip footstepClip = _footstepSounds[Random.Range(0, _footstepSounds.Length)];
            _audioSource.PlayOneShot(footstepClip, 0.5f);
        }
    }
    
    public float GetStaminaPercentage()
    {
        return _currentStamina / _maxStamina;
    }
    
    public bool IsMounted()
    {
        return _isMounted;
    }
    
    public bool IsRunning()
    {
        return _isRunning;
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _mountRange);
        
        if (_mountPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_mountPoint.position, 0.2f);
        }
    }
}