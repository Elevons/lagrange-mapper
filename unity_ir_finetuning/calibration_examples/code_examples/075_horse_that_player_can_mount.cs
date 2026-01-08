// Prompt: horse that player can mount
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class MountableHorse : MonoBehaviour
{
    [Header("Horse Settings")]
    [SerializeField] private float _moveSpeed = 8f;
    [SerializeField] private float _rotationSpeed = 120f;
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaDrainRate = 20f;
    [SerializeField] private float _staminaRegenRate = 15f;
    
    [Header("Mount Points")]
    [SerializeField] private Transform _mountPoint;
    [SerializeField] private float _mountRange = 3f;
    [SerializeField] private KeyCode _mountKey = KeyCode.E;
    
    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckRadius = 0.3f;
    [SerializeField] private LayerMask _groundLayerMask = 1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] _hoofstepSounds;
    [SerializeField] private AudioClip _neighSound;
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private float _hoofstepInterval = 0.5f;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    
    [Header("Events")]
    public UnityEvent OnPlayerMounted;
    public UnityEvent OnPlayerDismounted;
    public UnityEvent OnHorseJump;
    
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Transform _mountedPlayer;
    private bool _isPlayerMounted;
    private float _currentStamina;
    private bool _isGrounded;
    private float _lastHoofstepTime;
    private Vector3 _originalPlayerPosition;
    private Transform _originalPlayerParent;
    private bool _canMount = true;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_mountPoint == null)
        {
            GameObject mountPointObj = new GameObject("MountPoint");
            mountPointObj.transform.SetParent(transform);
            mountPointObj.transform.localPosition = new Vector3(0, 1.5f, 0);
            _mountPoint = mountPointObj.transform;
        }
        
        if (_groundCheckPoint == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = new Vector3(0, -0.5f, 0);
            _groundCheckPoint = groundCheckObj.transform;
        }
    }
    
    private void Start()
    {
        _currentStamina = _maxStamina;
        _rigidbody.freezeRotation = true;
    }
    
    private void Update()
    {
        CheckForPlayer();
        HandleInput();
        UpdateStamina();
        CheckGrounded();
        UpdateAnimations();
    }
    
    private void FixedUpdate()
    {
        if (_isPlayerMounted)
        {
            HandleMovement();
        }
    }
    
    private void CheckForPlayer()
    {
        if (_isPlayerMounted || !_canMount) return;
        
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _mountRange);
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Player"))
            {
                ShowMountPrompt(true);
                return;
            }
        }
        
        ShowMountPrompt(false);
    }
    
    private void ShowMountPrompt(bool show)
    {
        // This would typically show UI prompt - for now just a debug message
        if (show && !_isPlayerMounted)
        {
            Debug.Log($"Press {_mountKey} to mount horse");
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_mountKey))
        {
            if (!_isPlayerMounted)
            {
                TryMountPlayer();
            }
            else
            {
                DismountPlayer();
            }
        }
        
        if (_isPlayerMounted && Input.GetKeyDown(KeyCode.Space) && _isGrounded && _currentStamina > 20f)
        {
            Jump();
        }
    }
    
    private void TryMountPlayer()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _mountRange);
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Player"))
            {
                MountPlayer(col.transform);
                break;
            }
        }
    }
    
    private void MountPlayer(Transform player)
    {
        _mountedPlayer = player;
        _isPlayerMounted = true;
        
        // Store original player state
        _originalPlayerPosition = player.position;
        _originalPlayerParent = player.parent;
        
        // Attach player to mount point
        player.SetParent(_mountPoint);
        player.localPosition = Vector3.zero;
        player.localRotation = Quaternion.identity;
        
        // Disable player's rigidbody and collider if they exist
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.isKinematic = true;
        }
        
        Collider playerCol = player.GetComponent<Collider>();
        if (playerCol != null)
        {
            playerCol.enabled = false;
        }
        
        PlaySound(_neighSound);
        OnPlayerMounted?.Invoke();
    }
    
    private void DismountPlayer()
    {
        if (!_isPlayerMounted || _mountedPlayer == null) return;
        
        // Find safe dismount position
        Vector3 dismountPosition = FindSafeDismountPosition();
        
        // Restore player state
        _mountedPlayer.SetParent(_originalPlayerParent);
        _mountedPlayer.position = dismountPosition;
        
        // Re-enable player components
        Rigidbody playerRb = _mountedPlayer.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.isKinematic = false;
        }
        
        Collider playerCol = _mountedPlayer.GetComponent<Collider>();
        if (playerCol != null)
        {
            playerCol.enabled = true;
        }
        
        _mountedPlayer = null;
        _isPlayerMounted = false;
        
        OnPlayerDismounted?.Invoke();
    }
    
    private Vector3 FindSafeDismountPosition()
    {
        Vector3[] directions = {
            -transform.right,
            transform.right,
            -transform.forward,
            transform.forward
        };
        
        foreach (Vector3 direction in directions)
        {
            Vector3 testPosition = transform.position + direction * 2f;
            
            if (!Physics.CheckSphere(testPosition, 0.5f, _groundLayerMask))
            {
                return testPosition;
            }
        }
        
        return transform.position + Vector3.back * 2f;
    }
    
    private void HandleMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
        {
            // Movement
            Vector3 movement = transform.forward * vertical * _moveSpeed;
            _rigidbody.velocity = new Vector3(movement.x, _rigidbody.velocity.y, movement.z);
            
            // Rotation
            transform.Rotate(0, horizontal * _rotationSpeed * Time.fixedDeltaTime, 0);
            
            // Drain stamina when running
            if (Input.GetKey(KeyCode.LeftShift) && _currentStamina > 0)
            {
                _rigidbody.velocity = new Vector3(_rigidbody.velocity.x * 1.5f, _rigidbody.velocity.y, _rigidbody.velocity.z * 1.5f);
                _currentStamina -= _staminaDrainRate * Time.fixedDeltaTime;
            }
            
            PlayHoofstepSounds();
        }
    }
    
    private void Jump()
    {
        _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        _currentStamina -= 20f;
        PlaySound(_jumpSound);
        OnHorseJump?.Invoke();
    }
    
    private void UpdateStamina()
    {
        if (_currentStamina < _maxStamina)
        {
            _currentStamina += _staminaRegenRate * Time.deltaTime;
            _currentStamina = Mathf.Clamp(_currentStamina, 0, _maxStamina);
        }
    }
    
    private void CheckGrounded()
    {
        _isGrounded = Physics.CheckSphere(_groundCheckPoint.position, _groundCheckRadius, _groundLayerMask);
    }
    
    private void UpdateAnimations()
    {
        if (_animator == null) return;
        
        float speed = _rigidbody.velocity.magnitude;
        _animator.SetFloat("Speed", speed);
        _animator.SetBool("IsGrounded", _isGrounded);
        _animator.SetBool("IsMounted", _isPlayerMounted);
    }
    
    private void PlayHoofstepSounds()
    {
        if (_hoofstepSounds.Length == 0 || Time.time - _lastHoofstepTime < _hoofstepInterval) return;
        
        AudioClip randomHoofstep = _hoofstepSounds[Random.Range(0, _hoofstepSounds.Length)];
        PlaySound(randomHoofstep);
        _lastHoofstepTime = Time.time;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw mount range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _mountRange);
        
        // Draw ground check
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
        }
    }
    
    public float GetStaminaPercentage()
    {
        return _currentStamina / _maxStamina;
    }
    
    public bool IsPlayerMounted()
    {
        return _isPlayerMounted;
    }
    
    public void SetCanMount(bool canMount)
    {
        _canMount = canMount;
    }
}