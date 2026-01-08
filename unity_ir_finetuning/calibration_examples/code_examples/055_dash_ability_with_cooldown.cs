// Prompt: dash ability with cooldown
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class DashAbility : MonoBehaviour
{
    [Header("Dash Settings")]
    [SerializeField] private float _dashDistance = 5f;
    [SerializeField] private float _dashDuration = 0.2f;
    [SerializeField] private float _dashCooldown = 2f;
    [SerializeField] private KeyCode _dashKey = KeyCode.LeftShift;
    
    [Header("Physics")]
    [SerializeField] private LayerMask _obstacleLayerMask = -1;
    [SerializeField] private float _raycastRadius = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _dashEffect;
    [SerializeField] private TrailRenderer _dashTrail;
    [SerializeField] private float _invulnerabilityDuration = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _dashSound;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Events")]
    public UnityEvent OnDashStart;
    public UnityEvent OnDashEnd;
    public UnityEvent OnDashCooldownComplete;
    
    private Rigidbody2D _rigidbody2D;
    private Collider2D _collider2D;
    private Vector2 _originalVelocity;
    private bool _isDashing;
    private bool _isOnCooldown;
    private float _cooldownTimer;
    private float _dashTimer;
    private Vector2 _dashDirection;
    private Vector2 _dashStartPosition;
    private Vector2 _dashTargetPosition;
    
    public bool IsDashing => _isDashing;
    public bool IsOnCooldown => _isOnCooldown;
    public float CooldownProgress => _isOnCooldown ? (_dashCooldown - _cooldownTimer) / _dashCooldown : 1f;
    
    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _collider2D = GetComponent<Collider2D>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Start()
    {
        if (_dashTrail != null)
            _dashTrail.enabled = false;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCooldown();
        UpdateDash();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_dashKey) && CanDash())
        {
            Vector2 inputDirection = GetInputDirection();
            if (inputDirection != Vector2.zero)
            {
                StartDash(inputDirection);
            }
        }
    }
    
    private Vector2 GetInputDirection()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        return new Vector2(horizontal, vertical).normalized;
    }
    
    private void UpdateCooldown()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                OnDashCooldownComplete?.Invoke();
            }
        }
    }
    
    private void UpdateDash()
    {
        if (!_isDashing) return;
        
        _dashTimer -= Time.deltaTime;
        
        float dashProgress = 1f - (_dashTimer / _dashDuration);
        Vector2 currentPosition = Vector2.Lerp(_dashStartPosition, _dashTargetPosition, dashProgress);
        transform.position = currentPosition;
        
        if (_dashTimer <= 0f)
        {
            EndDash();
        }
    }
    
    private bool CanDash()
    {
        return !_isDashing && !_isOnCooldown && _rigidbody2D != null;
    }
    
    private void StartDash(Vector2 direction)
    {
        _dashDirection = direction;
        _dashStartPosition = transform.position;
        
        Vector2 targetPosition = _dashStartPosition + (_dashDirection * _dashDistance);
        RaycastHit2D hit = Physics2D.CircleCast(_dashStartPosition, _raycastRadius, _dashDirection, _dashDistance, _obstacleLayerMask);
        
        if (hit.collider != null && hit.collider != _collider2D)
        {
            _dashTargetPosition = hit.point - (_dashDirection * _raycastRadius);
        }
        else
        {
            _dashTargetPosition = targetPosition;
        }
        
        _isDashing = true;
        _dashTimer = _dashDuration;
        _originalVelocity = _rigidbody2D.velocity;
        _rigidbody2D.velocity = Vector2.zero;
        _rigidbody2D.isKinematic = true;
        
        if (_collider2D != null && _invulnerabilityDuration > 0f)
        {
            _collider2D.enabled = false;
            Invoke(nameof(RestoreCollider), _invulnerabilityDuration);
        }
        
        if (_dashTrail != null)
            _dashTrail.enabled = true;
        
        if (_dashEffect != null)
            _dashEffect.Play();
        
        if (_audioSource != null && _dashSound != null)
            _audioSource.PlayOneShot(_dashSound);
        
        OnDashStart?.Invoke();
    }
    
    private void EndDash()
    {
        _isDashing = false;
        _rigidbody2D.isKinematic = false;
        _rigidbody2D.velocity = _originalVelocity;
        
        _isOnCooldown = true;
        _cooldownTimer = _dashCooldown;
        
        if (_dashTrail != null)
            _dashTrail.enabled = false;
        
        OnDashEnd?.Invoke();
    }
    
    private void RestoreCollider()
    {
        if (_collider2D != null)
            _collider2D.enabled = true;
    }
    
    public void SetDashDistance(float distance)
    {
        _dashDistance = Mathf.Max(0f, distance);
    }
    
    public void SetDashCooldown(float cooldown)
    {
        _dashCooldown = Mathf.Max(0f, cooldown);
    }
    
    public void ResetCooldown()
    {
        _isOnCooldown = false;
        _cooldownTimer = 0f;
    }
    
    public void ForceDash(Vector2 direction)
    {
        if (!_isDashing && direction != Vector2.zero)
        {
            StartDash(direction.normalized);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _raycastRadius);
        
        if (_isDashing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_dashStartPosition, _dashTargetPosition);
        }
        else
        {
            Vector2 inputDir = GetInputDirection();
            if (inputDir != Vector2.zero)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawRay(transform.position, inputDir * _dashDistance);
            }
        }
    }
}