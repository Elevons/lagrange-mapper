// Prompt: skateboard with tricks
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SkateboardController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _acceleration = 8f;
    [SerializeField] private float _deceleration = 12f;
    [SerializeField] private float _turnSpeed = 120f;
    [SerializeField] private float _pushForce = 10f;
    
    [Header("Trick Settings")]
    [SerializeField] private float _jumpForce = 8f;
    [SerializeField] private float _flipSpeed = 360f;
    [SerializeField] private float _trickTimeWindow = 0.5f;
    [SerializeField] private float _landingTolerance = 15f;
    
    [Header("Physics")]
    [SerializeField] private float _groundCheckDistance = 0.6f;
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private Transform _groundCheckPoint;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pushSound;
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _landSound;
    [SerializeField] private AudioClip _trickSound;
    
    [Header("Events")]
    public UnityEvent<TrickType> OnTrickPerformed;
    public UnityEvent<float> OnSpeedChanged;
    public UnityEvent OnLanded;
    
    private Rigidbody _rigidbody;
    private bool _isGrounded;
    private bool _isPerformingTrick;
    private float _currentSpeed;
    private float _trickTimer;
    private Vector3 _trickRotation;
    private TrickType _currentTrick;
    
    public enum TrickType
    {
        None,
        Ollie,
        Kickflip,
        Heelflip,
        Shuvit,
        Treflip
    }
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_groundCheckPoint == null)
        {
            GameObject checkPoint = new GameObject("GroundCheck");
            checkPoint.transform.SetParent(transform);
            checkPoint.transform.localPosition = Vector3.down * 0.5f;
            _groundCheckPoint = checkPoint.transform;
        }
        
        _rigidbody.centerOfMass = Vector3.down * 0.2f;
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleInput();
        UpdateTrickTimer();
        OnSpeedChanged?.Invoke(_currentSpeed);
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
        ApplyTrickRotation();
    }
    
    private void CheckGrounded()
    {
        bool wasGrounded = _isGrounded;
        _isGrounded = Physics.Raycast(_groundCheckPoint.position, Vector3.down, _groundCheckDistance, _groundLayer);
        
        if (!wasGrounded && _isGrounded)
        {
            OnLanded?.Invoke();
            PlaySound(_landSound);
            
            if (_isPerformingTrick)
            {
                CompleteTrick();
            }
        }
        
        Debug.DrawRay(_groundCheckPoint.position, Vector3.down * _groundCheckDistance, _isGrounded ? Color.green : Color.red);
    }
    
    private void HandleInput()
    {
        if (_isGrounded && !_isPerformingTrick)
        {
            // Push forward
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
            {
                Push();
            }
            
            // Tricks
            if (Input.GetKeyDown(KeyCode.Space))
            {
                PerformTrick(TrickType.Ollie);
            }
            else if (Input.GetKeyDown(KeyCode.Q))
            {
                PerformTrick(TrickType.Kickflip);
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                PerformTrick(TrickType.Heelflip);
            }
            else if (Input.GetKeyDown(KeyCode.R))
            {
                PerformTrick(TrickType.Shuvit);
            }
            else if (Input.GetKeyDown(KeyCode.T))
            {
                PerformTrick(TrickType.Treflip);
            }
        }
        
        // Steering
        float steerInput = Input.GetAxis("Horizontal");
        if (Mathf.Abs(steerInput) > 0.1f && _currentSpeed > 1f)
        {
            transform.Rotate(0, steerInput * _turnSpeed * Time.deltaTime, 0);
        }
    }
    
    private void HandleMovement()
    {
        if (_isGrounded)
        {
            // Apply deceleration
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0, _deceleration * Time.fixedDeltaTime);
            
            // Apply movement
            Vector3 moveDirection = transform.forward * _currentSpeed;
            _rigidbody.velocity = new Vector3(moveDirection.x, _rigidbody.velocity.y, moveDirection.z);
        }
    }
    
    private void Push()
    {
        _currentSpeed = Mathf.Min(_currentSpeed + _pushForce, _maxSpeed);
        PlaySound(_pushSound);
    }
    
    private void PerformTrick(TrickType trickType)
    {
        if (_isPerformingTrick) return;
        
        _isPerformingTrick = true;
        _currentTrick = trickType;
        _trickTimer = _trickTimeWindow;
        
        // Apply jump force
        _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        
        // Set trick rotation based on type
        switch (trickType)
        {
            case TrickType.Ollie:
                _trickRotation = Vector3.zero;
                break;
            case TrickType.Kickflip:
                _trickRotation = new Vector3(_flipSpeed, 0, 0);
                break;
            case TrickType.Heelflip:
                _trickRotation = new Vector3(-_flipSpeed, 0, 0);
                break;
            case TrickType.Shuvit:
                _trickRotation = new Vector3(0, _flipSpeed, 0);
                break;
            case TrickType.Treflip:
                _trickRotation = new Vector3(_flipSpeed, _flipSpeed, 0);
                break;
        }
        
        PlaySound(_jumpSound);
        OnTrickPerformed?.Invoke(trickType);
    }
    
    private void ApplyTrickRotation()
    {
        if (_isPerformingTrick && !_isGrounded)
        {
            Vector3 rotationThisFrame = _trickRotation * Time.fixedDeltaTime;
            transform.Rotate(rotationThisFrame, Space.Self);
        }
    }
    
    private void UpdateTrickTimer()
    {
        if (_isPerformingTrick)
        {
            _trickTimer -= Time.deltaTime;
            
            if (_trickTimer <= 0 && !_isGrounded)
            {
                // Trick failed - didn't land in time
                _isPerformingTrick = false;
                _currentTrick = TrickType.None;
                _trickRotation = Vector3.zero;
            }
        }
    }
    
    private void CompleteTrick()
    {
        if (_currentTrick == TrickType.None) return;
        
        // Check if landed properly (board relatively upright)
        float uprightAngle = Vector3.Angle(transform.up, Vector3.up);
        
        if (uprightAngle <= _landingTolerance)
        {
            // Successful trick landing
            PlaySound(_trickSound);
            Debug.Log($"Successfully landed {_currentTrick}!");
        }
        else
        {
            // Failed landing
            Debug.Log($"Failed to land {_currentTrick} properly. Angle: {uprightAngle:F1}°");
        }
        
        _isPerformingTrick = false;
        _currentTrick = TrickType.None;
        _trickRotation = Vector3.zero;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public bool IsGrounded()
    {
        return _isGrounded;
    }
    
    public bool IsPerformingTrick()
    {
        return _isPerformingTrick;
    }
    
    public TrickType GetCurrentTrick()
    {
        return _currentTrick;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, 0.1f);
            Gizmos.DrawLine(_groundCheckPoint.position, _groundCheckPoint.position + Vector3.down * _groundCheckDistance);
        }
    }
}