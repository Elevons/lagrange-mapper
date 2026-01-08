// Prompt: stealth enemy that ambushes player
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class StealthEnemy : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRadius = 10f;
    [SerializeField] private float _ambushRadius = 3f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private LayerMask _obstacleLayer = 1;
    
    [Header("Stealth Settings")]
    [SerializeField] private float _stealthDuration = 5f;
    [SerializeField] private float _visibilityTransitionSpeed = 2f;
    [SerializeField] private float _minAlpha = 0.1f;
    [SerializeField] private float _maxAlpha = 1f;
    
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _ambushSpeed = 8f;
    [SerializeField] private float _retreatSpeed = 6f;
    [SerializeField] private float _circleDistance = 8f;
    
    [Header("Attack Settings")]
    [SerializeField] private float _attackDamage = 25f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _attackRange = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _ambushSound;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _retreatSound;
    
    [Header("Events")]
    public UnityEvent<float> OnPlayerAttacked;
    public UnityEvent OnAmbushTriggered;
    public UnityEvent OnStealthActivated;
    
    private enum EnemyState
    {
        Patrolling,
        Stalking,
        Ambushing,
        Attacking,
        Retreating,
        Cooldown
    }
    
    private EnemyState _currentState = EnemyState.Patrolling;
    private Transform _player;
    private Renderer _renderer;
    private Material _material;
    private AudioSource _audioSource;
    private Rigidbody _rigidbody;
    
    private Vector3 _originalPosition;
    private Vector3 _lastKnownPlayerPosition;
    private float _stealthTimer;
    private float _attackTimer;
    private float _currentAlpha;
    private bool _playerInRange;
    private bool _hasLineOfSight;
    private float _circleAngle;
    
    private void Start()
    {
        _originalPosition = transform.position;
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_renderer != null && _renderer.material != null)
        {
            _material = new Material(_renderer.material);
            _renderer.material = _material;
            _currentAlpha = _maxAlpha;
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.freezeRotation = true;
        
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        SetStealth(false);
    }
    
    private void Update()
    {
        if (_player == null)
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            return;
        }
        
        UpdatePlayerDetection();
        UpdateState();
        UpdateVisibility();
        UpdateTimers();
    }
    
    private void UpdatePlayerDetection()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _playerInRange = distanceToPlayer <= _detectionRadius;
        
        if (_playerInRange)
        {
            _hasLineOfSight = HasLineOfSight(_player.position);
            if (_hasLineOfSight)
            {
                _lastKnownPlayerPosition = _player.position;
            }
        }
        else
        {
            _hasLineOfSight = false;
        }
    }
    
    private void UpdateState()
    {
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                HandlePatrolling();
                break;
            case EnemyState.Stalking:
                HandleStalking();
                break;
            case EnemyState.Ambushing:
                HandleAmbushing();
                break;
            case EnemyState.Attacking:
                HandleAttacking();
                break;
            case EnemyState.Retreating:
                HandleRetreating();
                break;
            case EnemyState.Cooldown:
                HandleCooldown();
                break;
        }
    }
    
    private void HandlePatrolling()
    {
        if (_playerInRange && _hasLineOfSight)
        {
            ChangeState(EnemyState.Stalking);
            return;
        }
        
        // Simple patrol around original position
        Vector3 patrolTarget = _originalPosition + new Vector3(
            Mathf.Sin(Time.time * 0.5f) * 3f,
            0,
            Mathf.Cos(Time.time * 0.5f) * 3f
        );
        
        MoveTowards(patrolTarget, _moveSpeed * 0.5f);
    }
    
    private void HandleStalking()
    {
        if (!_playerInRange)
        {
            ChangeState(EnemyState.Patrolling);
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _ambushRadius)
        {
            ChangeState(EnemyState.Ambushing);
            return;
        }
        
        // Circle around player while maintaining distance
        _circleAngle += Time.deltaTime * 0.5f;
        Vector3 circlePosition = _player.position + new Vector3(
            Mathf.Sin(_circleAngle) * _circleDistance,
            0,
            Mathf.Cos(_circleAngle) * _circleDistance
        );
        
        MoveTowards(circlePosition, _moveSpeed);
        SetStealth(true);
    }
    
    private void HandleAmbushing()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _attackRange)
        {
            ChangeState(EnemyState.Attacking);
            return;
        }
        
        // Rush towards player
        MoveTowards(_player.position, _ambushSpeed);
        SetStealth(false);
        
        // Play ambush sound once
        if (_audioSource != null && _ambushSound != null && !_audioSource.isPlaying)
        {
            _audioSource.PlayOneShot(_ambushSound);
            OnAmbushTriggered?.Invoke();
        }
    }
    
    private void HandleAttacking()
    {
        if (_attackTimer <= 0f)
        {
            PerformAttack();
            _attackTimer = _attackCooldown;
            ChangeState(EnemyState.Retreating);
        }
    }
    
    private void HandleRetreating()
    {
        Vector3 retreatDirection = (transform.position - _player.position).normalized;
        Vector3 retreatTarget = transform.position + retreatDirection * 5f;
        
        MoveTowards(retreatTarget, _retreatSpeed);
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer >= _detectionRadius * 0.8f)
        {
            ChangeState(EnemyState.Cooldown);
        }
    }
    
    private void HandleCooldown()
    {
        if (_stealthTimer <= 0f)
        {
            ChangeState(EnemyState.Patrolling);
        }
    }
    
    private void ChangeState(EnemyState newState)
    {
        _currentState = newState;
        
        switch (newState)
        {
            case EnemyState.Stalking:
                _stealthTimer = _stealthDuration;
                OnStealthActivated?.Invoke();
                break;
            case EnemyState.Retreating:
                if (_audioSource != null && _retreatSound != null)
                {
                    _audioSource.PlayOneShot(_retreatSound);
                }
                break;
            case EnemyState.Cooldown:
                _stealthTimer = _stealthDuration * 0.5f;
                SetStealth(true);
                break;
        }
    }
    
    private void MoveTowards(Vector3 target, float speed)
    {
        Vector3 direction = (target - transform.position).normalized;
        direction.y = 0; // Keep movement on horizontal plane
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = new Vector3(direction.x * speed, _rigidbody.velocity.y, direction.z * speed);
        }
        else
        {
            transform.position += direction * speed * Time.deltaTime;
        }
        
        // Face movement direction
        if (direction != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }
    
    private void PerformAttack()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer <= _attackRange)
        {
            // Deal damage to player
            var playerHealth = _player.GetComponent<MonoBehaviour>();
            if (playerHealth != null)
            {
                OnPlayerAttacked?.Invoke(_attackDamage);
            }
            
            if (_audioSource != null && _attackSound != null)
            {
                _audioSource.PlayOneShot(_attackSound);
            }
        }
    }
    
    private void SetStealth(bool stealthed)
    {
        float targetAlpha = stealthed ? _minAlpha : _maxAlpha;
        _currentAlpha = Mathf.MoveTowards(_currentAlpha, targetAlpha, _visibilityTransitionSpeed * Time.deltaTime);
    }
    
    private void UpdateVisibility()
    {
        if (_material != null)
        {
            Color color = _material.color;
            color.a = _currentAlpha;
            _material.color = color;
        }
    }
    
    private void UpdateTimers()
    {
        if (_stealthTimer > 0f)
        {
            _stealthTimer -= Time.deltaTime;
        }
        
        if (_attackTimer > 0f)
        {
            _attackTimer -= Time.deltaTime;
        }
    }
    
    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        float distance = direction.magnitude;
        
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, direction.normalized, out RaycastHit hit, distance, _obstacleLayer))
        {
            return false;
        }
        
        return true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _currentState == EnemyState.Ambushing)
        {
            ChangeState(EnemyState.Attacking);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCircle(transform.position, _detectionRadius);
        
        // Ambush radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireCircle(transform.position, _ambushRadius);
        
        // Attack range
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCircle(transform.position, _attackRange);
        
        // Line of sight
        if (_player != null && _hasLineOfSight)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, _player.position + Vector3.up * 0.5f);
        }
    }
}