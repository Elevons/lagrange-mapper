// Prompt: double jump ability
// Type: movement

using UnityEngine;

public class DoubleJumpController : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _doubleJumpForce = 8f;
    [SerializeField] private int _maxJumps = 2;
    
    [Header("Ground Detection")]
    [SerializeField] private Transform _groundCheck;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayerMask = 1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _doubleJumpSound;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _jumpParticles;
    [SerializeField] private ParticleSystem _doubleJumpParticles;
    
    private Rigidbody2D _rigidbody2D;
    private AudioSource _audioSource;
    private int _jumpsRemaining;
    private bool _isGrounded;
    private bool _wasGrounded;
    
    private void Start()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody2D == null)
        {
            Debug.LogError("DoubleJumpController requires a Rigidbody2D component!");
            enabled = false;
            return;
        }
        
        if (_groundCheck == null)
        {
            _groundCheck = transform;
        }
        
        _jumpsRemaining = _maxJumps;
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleJumpInput();
    }
    
    private void CheckGrounded()
    {
        _wasGrounded = _isGrounded;
        _isGrounded = Physics2D.OverlapCircle(_groundCheck.position, _groundCheckRadius, _groundLayerMask);
        
        if (_isGrounded && !_wasGrounded)
        {
            _jumpsRemaining = _maxJumps;
        }
    }
    
    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && CanJump())
        {
            PerformJump();
        }
    }
    
    private bool CanJump()
    {
        return _jumpsRemaining > 0;
    }
    
    private void PerformJump()
    {
        _rigidbody2D.velocity = new Vector2(_rigidbody2D.velocity.x, 0f);
        
        bool isFirstJump = _jumpsRemaining == _maxJumps;
        float jumpForce = isFirstJump ? _jumpForce : _doubleJumpForce;
        
        _rigidbody2D.AddForce(Vector2.up * jumpForce, ForceMode2D.Impulse);
        _jumpsRemaining--;
        
        PlayJumpEffects(isFirstJump);
    }
    
    private void PlayJumpEffects(bool isFirstJump)
    {
        if (_audioSource != null)
        {
            AudioClip soundToPlay = isFirstJump ? _jumpSound : _doubleJumpSound;
            if (soundToPlay != null)
            {
                _audioSource.PlayOneShot(soundToPlay);
            }
        }
        
        ParticleSystem particlesToPlay = isFirstJump ? _jumpParticles : _doubleJumpParticles;
        if (particlesToPlay != null)
        {
            particlesToPlay.Play();
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
    
    public bool IsGrounded => _isGrounded;
    public int JumpsRemaining => _jumpsRemaining;
    public int MaxJumps => _maxJumps;
}