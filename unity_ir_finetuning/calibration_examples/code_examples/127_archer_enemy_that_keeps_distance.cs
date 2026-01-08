// Prompt: archer enemy that keeps distance
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class ArcherEnemy : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _loseTargetRange = 20f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private LayerMask _obstacleLayer = 1;

    [Header("Combat")]
    [SerializeField] private float _preferredDistance = 8f;
    [SerializeField] private float _minDistance = 5f;
    [SerializeField] private float _maxDistance = 12f;
    [SerializeField] private float _shootCooldown = 2f;
    [SerializeField] private GameObject _arrowPrefab;
    [SerializeField] private Transform _shootPoint;
    [SerializeField] private float _arrowSpeed = 10f;

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _backpedalSpeed = 4f;
    [SerializeField] private float _strafeSpeed = 3.5f;
    [SerializeField] private float _rotationSpeed = 5f;

    [Header("Health")]
    [SerializeField] private int _maxHealth = 50;
    [SerializeField] private float _hitStunDuration = 0.3f;

    [Header("Events")]
    public UnityEvent OnPlayerDetected;
    public UnityEvent OnPlayerLost;
    public UnityEvent OnArrowShot;
    public UnityEvent OnDeath;

    private Transform _player;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private int _currentHealth;
    private float _lastShootTime;
    private float _hitStunEndTime;
    private bool _hasTarget;
    private Vector3 _lastKnownPlayerPosition;
    private float _strafeDirection = 1f;
    private float _strafeTimer;

    private enum State
    {
        Idle,
        Pursuing,
        Maintaining,
        Retreating,
        Strafing,
        Stunned,
        Dead
    }

    private State _currentState = State.Idle;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _currentHealth = _maxHealth;

        if (_shootPoint == null)
            _shootPoint = transform;

        if (_rigidbody != null)
        {
            _rigidbody.freezeRotation = true;
        }
    }

    private void Update()
    {
        if (_currentState == State.Dead) return;

        HandleTargetDetection();
        UpdateState();
        HandleShooting();
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (_currentState == State.Dead || _currentState == State.Stunned) return;

        HandleMovement();
        HandleRotation();
    }

    private void HandleTargetDetection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player == null)
        {
            if (_hasTarget)
            {
                LoseTarget();
            }
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);

        if (!_hasTarget && distanceToPlayer <= _detectionRange)
        {
            if (HasLineOfSight(player.transform.position))
            {
                AcquireTarget(player.transform);
            }
        }
        else if (_hasTarget && distanceToPlayer > _loseTargetRange)
        {
            LoseTarget();
        }
        else if (_hasTarget)
        {
            _lastKnownPlayerPosition = player.transform.position;
        }
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _shootPoint.position;
        float distance = direction.magnitude;
        
        return !Physics.Raycast(_shootPoint.position, direction.normalized, distance, _obstacleLayer);
    }

    private void AcquireTarget(Transform playerTransform)
    {
        _player = playerTransform;
        _hasTarget = true;
        _lastKnownPlayerPosition = playerTransform.position;
        OnPlayerDetected?.Invoke();
    }

    private void LoseTarget()
    {
        _player = null;
        _hasTarget = false;
        _currentState = State.Idle;
        OnPlayerLost?.Invoke();
    }

    private void UpdateState()
    {
        if (Time.time < _hitStunEndTime)
        {
            _currentState = State.Stunned;
            return;
        }

        if (!_hasTarget)
        {
            _currentState = State.Idle;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _lastKnownPlayerPosition);

        if (distanceToPlayer < _minDistance)
        {
            _currentState = State.Retreating;
        }
        else if (distanceToPlayer > _maxDistance)
        {
            _currentState = State.Pursuing;
        }
        else
        {
            _strafeTimer += Time.deltaTime;
            if (_strafeTimer > 2f)
            {
                _strafeDirection *= -1f;
                _strafeTimer = 0f;
            }
            _currentState = State.Strafing;
        }
    }

    private void HandleMovement()
    {
        if (!_hasTarget || _rigidbody == null) return;

        Vector3 moveDirection = Vector3.zero;
        Vector3 toPlayer = (_lastKnownPlayerPosition - transform.position).normalized;
        Vector3 strafeDirection = Vector3.Cross(toPlayer, Vector3.up) * _strafeDirection;

        switch (_currentState)
        {
            case State.Pursuing:
                moveDirection = toPlayer * _moveSpeed;
                break;

            case State.Retreating:
                moveDirection = -toPlayer * _backpedalSpeed;
                break;

            case State.Strafing:
                moveDirection = strafeDirection * _strafeSpeed;
                break;
        }

        if (moveDirection != Vector3.zero)
        {
            _rigidbody.velocity = new Vector3(moveDirection.x, _rigidbody.velocity.y, moveDirection.z);
        }
        else
        {
            _rigidbody.velocity = new Vector3(0, _rigidbody.velocity.y, 0);
        }
    }

    private void HandleRotation()
    {
        if (!_hasTarget) return;

        Vector3 lookDirection = (_lastKnownPlayerPosition - transform.position).normalized;
        lookDirection.y = 0;

        if (lookDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        }
    }

    private void HandleShooting()
    {
        if (!_hasTarget || _currentState == State.Stunned) return;
        if (Time.time < _lastShootTime + _shootCooldown) return;

        float distanceToPlayer = Vector3.Distance(transform.position, _lastKnownPlayerPosition);
        if (distanceToPlayer <= _maxDistance && HasLineOfSight(_lastKnownPlayerPosition))
        {
            ShootArrow();
        }
    }

    private void ShootArrow()
    {
        if (_arrowPrefab == null) return;

        Vector3 shootDirection = (_lastKnownPlayerPosition - _shootPoint.position).normalized;
        GameObject arrow = Instantiate(_arrowPrefab, _shootPoint.position, Quaternion.LookRotation(shootDirection));
        
        Rigidbody arrowRb = arrow.GetComponent<Rigidbody>();
        if (arrowRb != null)
        {
            arrowRb.velocity = shootDirection * _arrowSpeed;
        }

        Arrow arrowComponent = arrow.GetComponent<Arrow>();
        if (arrowComponent == null)
        {
            arrowComponent = arrow.AddComponent<Arrow>();
        }
        arrowComponent.Initialize(10, 5f);

        _lastShootTime = Time.time;
        OnArrowShot?.Invoke();
    }

    private void UpdateAnimations()
    {
        if (_animator == null) return;

        _animator.SetBool("HasTarget", _hasTarget);
        _animator.SetFloat("Speed", _rigidbody != null ? _rigidbody.velocity.magnitude : 0f);
        _animator.SetBool("IsRetreating", _currentState == State.Retreating);
        _animator.SetBool("IsStunned", _currentState == State.Stunned);
    }

    public void TakeDamage(int damage)
    {
        if (_currentState == State.Dead) return;

        _currentHealth -= damage;
        _hitStunEndTime = Time.time + _hitStunDuration;

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        _currentState = State.Dead;
        _hasTarget = false;
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }

        if (_animator != null)
        {
            _animator.SetTrigger("Die");
        }

        OnDeath?.Invoke();
        
        Destroy(gameObject, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _currentState != State.Dead)
        {
            if (!_hasTarget)
            {
                AcquireTarget(other.transform);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _minDistance);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _preferredDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _maxDistance);
    }

    [System.Serializable]
    public class Arrow : MonoBehaviour
    {
        private int _damage;
        private float _lifetime;
        private float _spawnTime;

        public void Initialize(int damage, float lifetime)
        {
            _damage = damage;
            _lifetime = lifetime;
            _spawnTime = Time.time;
        }

        private void Update()
        {
            if (Time.time > _spawnTime + _lifetime)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                ArcherEnemy archer = other.GetComponent<ArcherEnemy>();
                if (archer != null)
                {
                    archer.TakeDamage(_damage);
                }
                Destroy(gameObject);
            }
            else if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}