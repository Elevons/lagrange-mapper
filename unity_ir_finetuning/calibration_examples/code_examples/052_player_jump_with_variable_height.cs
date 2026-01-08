// Prompt: player jump with variable height
// Type: movement

using UnityEngine;

public class PlayerJump : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _variableJumpMultiplier = 0.5f;
    [SerializeField] private float _maxJumpTime = 0.3f;
    [SerializeField] private float _fallMultiplier = 2.5f;
    [SerializeField] private float _lowJumpMultiplier = 2f;
    
    [Header("Ground Detection")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayerMask = 1;
    
    [Header("Input")]
    [SerializeField] private KeyCode _jumpKey = KeyCode.Space;
    
    private Rigidbody2D _rigidbody;
    private bool _isGrounded;
    private bool _isJumping;
    private float _jumpTimeCounter;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        
        if (_rigidbody == null)
        {
            Debug.LogError("PlayerJump requires a Rigidbody2D component!");
            enabled = false;
            return;
        }
        
        if (_groundCheck == null)
        {
            GameObject groundCheckObj = new GameObject("GroundCheck");
            groundCheckObj.transform.SetParent(transform);
            groundCheckObj.transform.localPosition = new Vector3(0, -0.5f, 0);
            _groundCheck = groundCheckObj.transform;
        }
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleJumpInput();
        ApplyVariableJump();
    }
    
    private void FixedUpdate()
    {
        ApplyBetterJump();
    }
    
    private void CheckGrounded()
    {
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayerMask);
    }
    
    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(_jumpKey) && _isGrounded)
        {
            StartJump();
        }
        
        if (Input.GetKey(_jumpKey) && _isJumping)
        {
            if (_jumpTimeCounter > 0)
            {
                _rigidbody.velocity = new Vector2(_rigidbody.velocity.x, _jumpForce);
                _jumpTimeCounter -= Time.deltaTime;
            }
            else
            {
                _isJumping = false;
            }
        }
        
        if (Input.GetKeyUp(_jumpKey))
        {
            _isJumping = false;
        }
    }
    
    private void StartJump()
    {
        _isJumping = true;
        _jumpTimeCounter = _maxJumpTime;
        _rigidbody.velocity = new Vector2(_rigidbody.velocity.x, _jumpForce);
    }
    
    private void ApplyVariableJump()
    {
        if (_rigidbody.velocity.y < 0 && !Input.GetKey(_jumpKey))
        {
            _rigidbody.velocity += Vector2.up * Physics2D.gravity.y * (_lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }
    
    private void ApplyBetterJump()
    {
        if (_rigidbody.velocity.y < 0)
        {
            _rigidbody.velocity += Vector2.up * Physics2D.gravity.y * (_fallMultiplier - 1) * Time.deltaTime;
        }
        else if (_rigidbody.velocity.y > 0 && !Input.GetKey(_jumpKey))
        {
            _rigidbody.velocity += Vector2.up * Physics2D.gravity.y * (_lowJumpMultiplier - 1) * Time.deltaTime;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_groundCheck != null)
        {
            Gizmos.color = _isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheck.position, _groundCheckRadius);
        }
    }
}