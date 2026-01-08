// Prompt: rope climbing
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class RopeClimbing : MonoBehaviour
{
    [Header("Rope Settings")]
    [SerializeField] private float _climbSpeed = 3f;
    [SerializeField] private float _slideSpeed = 1f;
    [SerializeField] private float _grabDistance = 1.5f;
    [SerializeField] private LayerMask _ropeLayer = 1;
    [SerializeField] private string _ropeTag = "Rope";
    
    [Header("Input")]
    [SerializeField] private KeyCode _grabKey = KeyCode.E;
    [SerializeField] private string _verticalAxis = "Vertical";
    
    [Header("Physics")]
    [SerializeField] private float _gravityScale = 1f;
    [SerializeField] private float _swayForce = 2f;
    [SerializeField] private float _maxSwayAngle = 30f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _grabSound;
    [SerializeField] private AudioClip _climbSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Events")]
    public UnityEvent OnRopeGrabbed;
    public UnityEvent OnRopeReleased;
    public UnityEvent OnReachedTop;
    public UnityEvent OnReachedBottom;
    
    private Rigidbody2D _rigidbody;
    private Collider2D _collider;
    private AudioSource _audioSource;
    private Transform _currentRope;
    private bool _isClimbing;
    private bool _wasGrounded;
    private float _originalGravityScale;
    private Vector2 _ropeGrabPoint;
    private float _ropeLength;
    private float _currentRopePosition;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            Debug.LogError("RopeClimbing requires a Rigidbody2D component!");
            enabled = false;
            return;
        }
        
        _originalGravityScale = _rigidbody.gravityScale;
    }
    
    private void Update()
    {
        HandleInput();
        
        if (_isClimbing)
        {
            HandleClimbing();
            HandleSwaying();
            CheckRopeBounds();
        }
        else
        {
            CheckForNearbyRope();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_grabKey))
        {
            if (_isClimbing)
            {
                ReleaseRope();
            }
            else
            {
                TryGrabRope();
            }
        }
    }
    
    private void CheckForNearbyRope()
    {
        Collider2D[] nearbyRopes = Physics2D.OverlapCircleAll(transform.position, _grabDistance, _ropeLayer);
        
        foreach (var rope in nearbyRopes)
        {
            if (rope.CompareTag(_ropeTag))
            {
                // Visual feedback could be added here
                break;
            }
        }
    }
    
    private void TryGrabRope()
    {
        Collider2D[] nearbyRopes = Physics2D.OverlapCircleAll(transform.position, _grabDistance, _ropeLayer);
        
        foreach (var rope in nearbyRopes)
        {
            if (rope.CompareTag(_ropeTag))
            {
                GrabRope(rope.transform);
                break;
            }
        }
    }
    
    private void GrabRope(Transform rope)
    {
        _currentRope = rope;
        _isClimbing = true;
        _rigidbody.gravityScale = 0f;
        _rigidbody.velocity = Vector2.zero;
        
        // Calculate rope grab point and length
        _ropeGrabPoint = _currentRope.position;
        _ropeLength = _currentRope.localScale.y;
        
        // Calculate current position on rope (0 = bottom, 1 = top)
        float distanceFromBottom = Vector2.Distance(transform.position, _ropeGrabPoint - Vector3.up * _ropeLength * 0.5f);
        _currentRopePosition = Mathf.Clamp01(distanceFromBottom / _ropeLength);
        
        // Position player on rope
        Vector3 ropePosition = _ropeGrabPoint + Vector3.up * (_currentRopePosition - 0.5f) * _ropeLength;
        transform.position = new Vector3(ropePosition.x, transform.position.y, transform.position.z);
        
        PlaySound(_grabSound);
        OnRopeGrabbed?.Invoke();
    }
    
    private void ReleaseRope()
    {
        _currentRope = null;
        _isClimbing = false;
        _rigidbody.gravityScale = _originalGravityScale;
        
        PlaySound(_releaseSound);
        OnRopeReleased?.Invoke();
    }
    
    private void HandleClimbing()
    {
        if (_currentRope == null)
        {
            ReleaseRope();
            return;
        }
        
        float verticalInput = Input.GetAxis(_verticalAxis);
        
        if (Mathf.Abs(verticalInput) > 0.1f)
        {
            // Move along rope
            float moveSpeed = verticalInput > 0 ? _climbSpeed : _slideSpeed;
            _currentRopePosition += verticalInput * moveSpeed * Time.deltaTime / _ropeLength;
            _currentRopePosition = Mathf.Clamp01(_currentRopePosition);
            
            // Update position
            Vector3 ropePosition = _ropeGrabPoint + Vector3.up * (_currentRopePosition - 0.5f) * _ropeLength;
            transform.position = new Vector3(ropePosition.x, ropePosition.y, transform.position.z);
            
            // Play climbing sound
            if (_audioSource != null && _climbSound != null && !_audioSource.isPlaying)
            {
                PlaySound(_climbSound);
            }
        }
    }
    
    private void HandleSwaying()
    {
        float horizontalInput = Input.GetAxis("Horizontal");
        
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            // Apply sway force to the rope (if it has physics)
            Rigidbody2D ropeRb = _currentRope.GetComponent<Rigidbody2D>();
            if (ropeRb != null)
            {
                Vector2 swayForce = Vector2.right * horizontalInput * _swayForce;
                ropeRb.AddForceAtPosition(swayForce, transform.position);
            }
            else
            {
                // Simple sway animation for static ropes
                float swayAmount = Mathf.Sin(Time.time * 3f) * horizontalInput * 0.5f;
                swayAmount = Mathf.Clamp(swayAmount, -_maxSwayAngle, _maxSwayAngle);
                
                Vector3 swayOffset = Vector3.right * swayAmount * 0.1f;
                Vector3 ropePosition = _ropeGrabPoint + Vector3.up * (_currentRopePosition - 0.5f) * _ropeLength;
                transform.position = new Vector3(ropePosition.x + swayOffset.x, ropePosition.y, transform.position.z);
            }
        }
    }
    
    private void CheckRopeBounds()
    {
        if (_currentRopePosition >= 0.95f)
        {
            OnReachedTop?.Invoke();
        }
        else if (_currentRopePosition <= 0.05f)
        {
            OnReachedBottom?.Invoke();
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnTriggerExit2D(Collider2D other)
    {
        if (_isClimbing && other.transform == _currentRope)
        {
            ReleaseRope();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw grab distance
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCircle(transform.position, _grabDistance);
        
        // Draw rope connection when climbing
        if (_isClimbing && _currentRope != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentRope.position);
        }
    }
}