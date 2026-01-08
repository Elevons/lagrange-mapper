// Prompt: crab that moves sideways
// Type: movement

using UnityEngine;

public class SidewaysCrab : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _changeDirectionTime = 3f;
    [SerializeField] private bool _startMovingRight = true;
    
    [Header("Ground Detection")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    [SerializeField] private LayerMask _groundLayerMask = 1;
    
    [Header("Wall Detection")]
    [SerializeField] private Transform _wallCheckPoint;
    [SerializeField] private float _wallCheckDistance = 0.5f;
    [SerializeField] private LayerMask _wallLayerMask = 1;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    
    private Rigidbody2D _rigidbody2D;
    private SpriteRenderer _spriteRenderer;
    private bool _movingRight;
    private float _directionTimer;
    private bool _isGrounded;
    private bool _wallDetected;
    
    private void Start()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        if (_animator == null)
            _animator = GetComponent<Animator>();
            
        _movingRight = _startMovingRight;
        _directionTimer = _changeDirectionTime;
        
        if (_groundCheckPoint == null)
        {
            GameObject groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(transform);
            groundCheck.transform.localPosition = new Vector3(0, -0.5f, 0);
            _groundCheckPoint = groundCheck.transform;
        }
        
        if (_wallCheckPoint == null)
        {
            GameObject wallCheck = new GameObject("WallCheck");
            wallCheck.transform.SetParent(transform);
            wallCheck.transform.localPosition = new Vector3(0.5f, 0, 0);
            _wallCheckPoint = wallCheck.transform;
        }
    }
    
    private void Update()
    {
        _directionTimer -= Time.deltaTime;
        
        if (_directionTimer <= 0f)
        {
            ChangeDirection();
            _directionTimer = _changeDirectionTime;
        }
        
        CheckGround();
        CheckWall();
        
        if (!_isGrounded || _wallDetected)
        {
            ChangeDirection();
        }
        
        UpdateAnimation();
    }
    
    private void FixedUpdate()
    {
        MoveCrab();
    }
    
    private void MoveCrab()
    {
        if (_rigidbody2D == null) return;
        
        float horizontalMovement = _movingRight ? _moveSpeed : -_moveSpeed;
        _rigidbody2D.velocity = new Vector2(horizontalMovement, _rigidbody2D.velocity.y);
        
        UpdateSpriteDirection();
    }
    
    private void CheckGround()
    {
        if (_groundCheckPoint == null) return;
        
        Vector3 checkPosition = _groundCheckPoint.position;
        if (_movingRight)
            checkPosition += Vector3.right * 0.3f;
        else
            checkPosition += Vector3.left * 0.3f;
            
        _isGrounded = Physics2D.Raycast(checkPosition, Vector2.down, _groundCheckDistance, _groundLayerMask);
        
        Debug.DrawRay(checkPosition, Vector2.down * _groundCheckDistance, _isGrounded ? Color.green : Color.red);
    }
    
    private void CheckWall()
    {
        if (_wallCheckPoint == null) return;
        
        Vector2 direction = _movingRight ? Vector2.right : Vector2.left;
        _wallDetected = Physics2D.Raycast(_wallCheckPoint.position, direction, _wallCheckDistance, _wallLayerMask);
        
        Debug.DrawRay(_wallCheckPoint.position, direction * _wallCheckDistance, _wallDetected ? Color.red : Color.green);
    }
    
    private void ChangeDirection()
    {
        _movingRight = !_movingRight;
        _directionTimer = _changeDirectionTime;
        
        if (_wallCheckPoint != null)
        {
            Vector3 wallCheckPos = _wallCheckPoint.localPosition;
            wallCheckPos.x = Mathf.Abs(wallCheckPos.x) * (_movingRight ? 1 : -1);
            _wallCheckPoint.localPosition = wallCheckPos;
        }
    }
    
    private void UpdateSpriteDirection()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.flipX = !_movingRight;
        }
    }
    
    private void UpdateAnimation()
    {
        if (_animator != null)
        {
            bool isMoving = Mathf.Abs(_rigidbody2D.velocity.x) > 0.1f;
            _animator.SetBool("IsMoving", isMoving);
            _animator.SetBool("MovingRight", _movingRight);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ChangeDirection();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 groundPos = _groundCheckPoint.position;
            if (_movingRight)
                groundPos += Vector3.right * 0.3f;
            else
                groundPos += Vector3.left * 0.3f;
            Gizmos.DrawLine(groundPos, groundPos + Vector3.down * _groundCheckDistance);
        }
        
        if (_wallCheckPoint != null)
        {
            Gizmos.color = Color.blue;
            Vector3 direction = _movingRight ? Vector3.right : Vector3.left;
            Gizmos.DrawLine(_wallCheckPoint.position, _wallCheckPoint.position + direction * _wallCheckDistance);
        }
    }
}