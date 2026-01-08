// Prompt: roll dodge with invincibility frames
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class RollDodge : MonoBehaviour
{
    [Header("Roll Settings")]
    [SerializeField] private float _rollDistance = 5f;
    [SerializeField] private float _rollDuration = 0.5f;
    [SerializeField] private float _rollCooldown = 1f;
    [SerializeField] private KeyCode _rollKey = KeyCode.Space;
    [SerializeField] private bool _useMouseDirection = false;
    
    [Header("Invincibility Settings")]
    [SerializeField] private float _invincibilityDuration = 0.6f;
    [SerializeField] private int _flashCount = 6;
    
    [Header("Physics")]
    [SerializeField] private LayerMask _obstacleLayer = -1;
    [SerializeField] private float _capsuleRadius = 0.5f;
    
    [Header("Events")]
    public UnityEvent OnRollStart;
    public UnityEvent OnRollEnd;
    public UnityEvent OnInvincibilityStart;
    public UnityEvent OnInvincibilityEnd;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Renderer _renderer;
    private Camera _camera;
    
    private bool _isRolling = false;
    private bool _isInvincible = false;
    private bool _canRoll = true;
    private float _rollTimer = 0f;
    private float _invincibilityTimer = 0f;
    private float _cooldownTimer = 0f;
    private Vector3 _rollDirection;
    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private Color _originalColor;
    private bool _flashVisible = true;
    private float _flashTimer = 0f;
    private float _flashInterval;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _renderer = GetComponent<Renderer>();
        _camera = Camera.main;
        
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }
        
        _flashInterval = _invincibilityDuration / (_flashCount * 2);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCooldown();
        UpdateRoll();
        UpdateInvincibility();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_rollKey) && _canRoll && !_isRolling)
        {
            StartRoll();
        }
    }
    
    private void UpdateCooldown()
    {
        if (!_canRoll)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _canRoll = true;
            }
        }
    }
    
    private void UpdateRoll()
    {
        if (!_isRolling) return;
        
        _rollTimer += Time.deltaTime;
        float progress = _rollTimer / _rollDuration;
        
        if (progress >= 1f)
        {
            EndRoll();
            return;
        }
        
        Vector3 currentPosition = Vector3.Lerp(_startPosition, _targetPosition, progress);
        transform.position = currentPosition;
    }
    
    private void UpdateInvincibility()
    {
        if (!_isInvincible) return;
        
        _invincibilityTimer -= Time.deltaTime;
        if (_invincibilityTimer <= 0f)
        {
            EndInvincibility();
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (!_isInvincible || _renderer == null) return;
        
        _flashTimer += Time.deltaTime;
        if (_flashTimer >= _flashInterval)
        {
            _flashTimer = 0f;
            _flashVisible = !_flashVisible;
            
            Color targetColor = _flashVisible ? _originalColor : new Color(_originalColor.r, _originalColor.g, _originalColor.b, 0.3f);
            _renderer.material.color = targetColor;
        }
    }
    
    private void StartRoll()
    {
        _rollDirection = GetRollDirection();
        if (_rollDirection == Vector3.zero) return;
        
        Vector3 targetPos = transform.position + _rollDirection * _rollDistance;
        
        if (IsPathBlocked(transform.position, targetPos))
        {
            targetPos = GetSafeRollPosition(transform.position, _rollDirection);
        }
        
        _startPosition = transform.position;
        _targetPosition = targetPos;
        _rollTimer = 0f;
        _isRolling = true;
        _canRoll = false;
        _cooldownTimer = _rollCooldown;
        
        StartInvincibility();
        OnRollStart?.Invoke();
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
        }
    }
    
    private void EndRoll()
    {
        _isRolling = false;
        transform.position = _targetPosition;
        OnRollEnd?.Invoke();
    }
    
    private void StartInvincibility()
    {
        _isInvincible = true;
        _invincibilityTimer = _invincibilityDuration;
        _flashTimer = 0f;
        _flashVisible = true;
        OnInvincibilityStart?.Invoke();
        
        if (_collider != null)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);
        }
    }
    
    private void EndInvincibility()
    {
        _isInvincible = false;
        OnInvincibilityEnd?.Invoke();
        
        if (_renderer != null)
        {
            _renderer.material.color = _originalColor;
        }
        
        if (_collider != null)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), false);
        }
    }
    
    private Vector3 GetRollDirection()
    {
        Vector3 direction = Vector3.zero;
        
        if (_useMouseDirection && _camera != null)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = _camera.WorldToScreenPoint(transform.position).z;
            Vector3 worldMousePos = _camera.ScreenToWorldPoint(mousePos);
            direction = (worldMousePos - transform.position).normalized;
            direction.y = 0f;
        }
        else
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");
            direction = new Vector3(horizontal, 0f, vertical).normalized;
            
            if (direction == Vector3.zero)
            {
                direction = transform.forward;
            }
        }
        
        return direction;
    }
    
    private bool IsPathBlocked(Vector3 start, Vector3 end)
    {
        Vector3 direction = (end - start).normalized;
        float distance = Vector3.Distance(start, end);
        
        return Physics.CapsuleCast(
            start + Vector3.up * _capsuleRadius,
            start + Vector3.up * (GetComponent<Collider>().bounds.size.y - _capsuleRadius),
            _capsuleRadius,
            direction,
            distance,
            _obstacleLayer
        );
    }
    
    private Vector3 GetSafeRollPosition(Vector3 start, Vector3 direction)
    {
        float maxDistance = _rollDistance;
        float stepSize = 0.5f;
        
        for (float distance = maxDistance; distance > stepSize; distance -= stepSize)
        {
            Vector3 testPosition = start + direction * distance;
            
            if (!IsPathBlocked(start, testPosition))
            {
                return testPosition;
            }
        }
        
        return start;
    }
    
    public bool IsInvincible()
    {
        return _isInvincible;
    }
    
    public bool IsRolling()
    {
        return _isRolling;
    }
    
    public bool CanRoll()
    {
        return _canRoll && !_isRolling;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isInvincible && other.CompareTag("Enemy"))
        {
            return;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_isInvincible && collision.gameObject.CompareTag("Enemy"))
        {
            Physics.IgnoreCollision(_collider, collision.collider, true);
        }
    }
}