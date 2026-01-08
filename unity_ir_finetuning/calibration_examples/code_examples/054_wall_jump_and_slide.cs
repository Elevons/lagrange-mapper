// Prompt: wall jump and slide
// Type: movement

using UnityEngine;

public class WallJumpSlide : MonoBehaviour
{
    [Header("Wall Detection")]
    [SerializeField] private LayerMask _wallLayerMask = 1;
    [SerializeField] private float _wallCheckDistance = 0.6f;
    [SerializeField] private Transform _wallCheckPoint;
    
    [Header("Wall Slide")]
    [SerializeField] private float _wallSlideSpeed = 2f;
    [SerializeField] private float _wallSlideGravityScale = 0.3f;
    
    [Header("Wall Jump")]
    [SerializeField] private float _wallJumpForce = 15f;
    [SerializeField] private Vector2 _wallJumpDirection = new Vector2(1f, 1.2f);
    [SerializeField] private float _wallJumpTime = 0.2f;
    [SerializeField] private float _wallJumpCooldown = 0.1f;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayerMask = 1;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    [SerializeField] private Transform _groundCheckPoint;
    
    [Header("Input")]
    [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
    
    private Rigidbody2D _rigidbody;
    private bool _isTouchingWall;
    private bool _isWallSliding;
    private bool _isWallJumping;
    private bool _isGrounded;
    private float _wallJumpTimer;
    private float _wallJumpCooldownTimer;
    private float _originalGravityScale;
    private int _wallDirection;
    private float _horizontalInput;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _originalGravityScale = _rigidbody.gravityScale;
        
        if (_wallCheckPoint == null)
        {
            GameObject wallCheck = new GameObject("WallCheck");
            wallCheck.transform.SetParent(transform);
            wallCheck.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            _wallCheckPoint = wallCheck.transform;
        }
        
        if (_groundCheckPoint == null)
        {
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(transform);
            groundCheck.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            _groundCheckPoint = groundCheck.transform;
        }
    }
    
    private void Update()
    {
        _horizontalInput = Input.GetAxisRaw("Horizontal");
        
        CheckWallTouch();
        CheckGrounded();
        HandleWallSlide();
        HandleWallJump();
        
        if (_wallJumpTimer > 0)
        {
            _wallJumpTimer -= Time.deltaTime;
        }
        else
        {
            _isWallJumping = false;
        }
        
        if (_wallJumpCooldownTimer > 0)
        {
            _wallJumpCooldownTimer -= Time.deltaTime;
        }
    }
    
    private void CheckWallTouch()
    {
        _isTouchingWall = false;
        _wallDirection = 0;
        
        // Check right wall
        RaycastHit2D rightHit = Physics2D.Raycast(_wallCheckPoint.position, Vector2.right, _wallCheckDistance, _wallLayerMask);
        if (rightHit.collider != null)
        {
            _isTouchingWall = true;
            _wallDirection = 1;
        }
        else
        {
            // Check left wall
            RaycastHit2D leftHit = Physics2D.Raycast(_wallCheckPoint.position, Vector2.left, _wallCheckDistance, _wallLayerMask);
            if (leftHit.collider != null)
            {
                _isTouchingWall = true;
                _wallDirection = -1;
            }
        }
    }
    
    private void CheckGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(_groundCheckPoint.position, Vector2.down, _groundCheckDistance, _groundLayerMask);
        _isGrounded = hit.collider != null;
    }
    
    private void HandleWallSlide()
    {
        if (_isTouchingWall && !_isGrounded && _rigidbody.velocity.y < 0 && !_isWallJumping)
        {
            bool shouldSlide = (_wallDirection == 1 && _horizontalInput > 0) || (_wallDirection == -1 && _horizontalInput < 0);
            
            if (shouldSlide)
            {
                _isWallSliding = true;
                _rigidbody.gravityScale = _wallSlideGravityScale;
                
                if (_rigidbody.velocity.y < -_wallSlideSpeed)
                {
                    _rigidbody.velocity = new Vector2(_rigidbody.velocity.x, -_wallSlideSpeed);
                }
            }
            else
            {
                StopWallSlide();
            }
        }
        else
        {
            StopWallSlide();
        }
    }
    
    private void StopWallSlide()
    {
        if (_isWallSliding)
        {
            _isWallSliding = false;
            _rigidbody.gravityScale = _originalGravityScale;
        }
    }
    
    private void HandleWallJump()
    {
        if (Input.GetKeyDown(_jumpKey) && _isWallSliding && _wallJumpCooldownTimer <= 0)
        {
            PerformWallJump();
        }
    }
    
    private void PerformWallJump()
    {
        _isWallJumping = true;
        _isWallSliding = false;
        _wallJumpTimer = _wallJumpTime;
        _wallJumpCooldownTimer = _wallJumpCooldown;
        
        _rigidbody.gravityScale = _originalGravityScale;
        
        Vector2 jumpDirection = new Vector2(_wallJumpDirection.x * -_wallDirection, _wallJumpDirection.y);
        jumpDirection.Normalize();
        
        _rigidbody.velocity = new Vector2(0f, 0f);
        _rigidbody.AddForce(jumpDirection * _wallJumpForce, ForceMode2D.Impulse);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_wallCheckPoint != null)
        {
            Gizmos.color = _isTouchingWall ? Color.red : Color.green;
            Gizmos.DrawRay(_wallCheckPoint.position, Vector3.right * _wallCheckDistance);
            Gizmos.DrawRay(_wallCheckPoint.position, Vector3.left * _wallCheckDistance);
        }
        
        if (_groundCheckPoint != null)
        {
            Gizmos.color = _isGrounded ? Color.red : Color.blue;
            Gizmos.DrawRay(_groundCheckPoint.position, Vector3.down * _groundCheckDistance);
        }
        
        if (_isWallSliding)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}