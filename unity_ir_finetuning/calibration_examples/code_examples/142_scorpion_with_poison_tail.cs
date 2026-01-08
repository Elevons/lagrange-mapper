// Prompt: scorpion with poison tail
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class Scorpion : MonoBehaviour
{
    [System.Serializable]
    public class ScorpionEvents
    {
        public UnityEvent OnAttack;
        public UnityEvent OnPlayerHit;
        public UnityEvent OnDeath;
    }

    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _wanderRadius = 5f;
    [SerializeField] private float _wanderTimer = 3f;

    [Header("Combat")]
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _poisonDamage = 10f;
    [SerializeField] private float _poisonDuration = 5f;
    [SerializeField] private int _maxHealth = 50;

    [Header("Detection")]
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private LayerMask _playerLayer = 1;

    [Header("Animation")]
    [SerializeField] private Transform _tailTransform;
    [SerializeField] private float _tailAttackHeight = 1.5f;
    [SerializeField] private float _tailAttackSpeed = 5f;

    [Header("Effects")]
    [SerializeField] private GameObject _poisonEffect;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _deathSound;

    [Header("Events")]
    [SerializeField] private ScorpionEvents _events;

    private enum ScorpionState
    {
        Wandering,
        Chasing,
        Attacking,
        Dead
    }

    private ScorpionState _currentState = ScorpionState.Wandering;
    private Transform _player;
    private Vector3 _wanderTarget;
    private Vector3 _initialPosition;
    private float _lastAttackTime;
    private float _wanderTime;
    private int _currentHealth;
    private bool _isAttacking;
    private Vector3 _tailOriginalPosition;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Animator _animator;

    private void Start()
    {
        _currentHealth = _maxHealth;
        _initialPosition = transform.position;
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        _animator = GetComponent<Animator>();
        
        if (_tailTransform != null)
        {
            _tailOriginalPosition = _tailTransform.localPosition;
        }
        
        SetNewWanderTarget();
    }

    private void Update()
    {
        if (_currentState == ScorpionState.Dead) return;

        DetectPlayer();
        UpdateState();
        HandleMovement();
        HandleTailAnimation();
    }

    private void DetectPlayer()
    {
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, _detectionRange, _playerLayer);
        
        if (playersInRange.Length > 0)
        {
            Transform closestPlayer = null;
            float closestDistance = float.MaxValue;
            
            foreach (var playerCollider in playersInRange)
            {
                if (playerCollider.CompareTag("Player"))
                {
                    float distance = Vector3.Distance(transform.position, playerCollider.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestPlayer = playerCollider.transform;
                    }
                }
            }
            
            _player = closestPlayer;
        }
        else
        {
            _player = null;
        }
    }

    private void UpdateState()
    {
        switch (_currentState)
        {
            case ScorpionState.Wandering:
                if (_player != null)
                {
                    _currentState = ScorpionState.Chasing;
                }
                else if (Vector3.Distance(transform.position, _wanderTarget) < 0.5f)
                {
                    SetNewWanderTarget();
                }
                break;

            case ScorpionState.Chasing:
                if (_player == null)
                {
                    _currentState = ScorpionState.Wandering;
                    SetNewWanderTarget();
                }
                else if (Vector3.Distance(transform.position, _player.position) <= _attackRange)
                {
                    if (Time.time - _lastAttackTime >= _attackCooldown)
                    {
                        _currentState = ScorpionState.Attacking;
                        StartAttack();
                    }
                }
                break;

            case ScorpionState.Attacking:
                if (!_isAttacking)
                {
                    _currentState = _player != null ? ScorpionState.Chasing : ScorpionState.Wandering;
                }
                break;
        }
    }

    private void HandleMovement()
    {
        if (_currentState == ScorpionState.Attacking || _isAttacking) return;

        Vector3 targetPosition = _currentState == ScorpionState.Chasing ? _player.position : _wanderTarget;
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            
            Vector3 movement = direction * _moveSpeed * Time.deltaTime;
            _rigidbody.MovePosition(transform.position + movement);
        }

        if (_animator != null)
        {
            _animator.SetBool("IsMoving", direction != Vector3.zero);
        }
    }

    private void SetNewWanderTarget()
    {
        Vector2 randomDirection = Random.insideUnitCircle * _wanderRadius;
        _wanderTarget = _initialPosition + new Vector3(randomDirection.x, 0, randomDirection.y);
        _wanderTime = Time.time + _wanderTimer;
    }

    private void StartAttack()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
        }
        
        if (_audioSource != null && _attackSound != null)
        {
            _audioSource.PlayOneShot(_attackSound);
        }
        
        _events?.OnAttack?.Invoke();
        
        Invoke(nameof(ExecuteAttack), 0.5f);
        Invoke(nameof(EndAttack), 1f);
    }

    private void ExecuteAttack()
    {
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= _attackRange)
        {
            ApplyPoisonToPlayer(_player.gameObject);
            _events?.OnPlayerHit?.Invoke();
        }
    }

    private void EndAttack()
    {
        _isAttacking = false;
    }

    private void ApplyPoisonToPlayer(GameObject player)
    {
        PoisonEffect poisonComponent = player.GetComponent<PoisonEffect>();
        if (poisonComponent == null)
        {
            poisonComponent = player.AddComponent<PoisonEffect>();
        }
        
        poisonComponent.ApplyPoison(_poisonDamage, _poisonDuration);
        
        if (_poisonEffect != null)
        {
            GameObject effect = Instantiate(_poisonEffect, player.transform.position, Quaternion.identity);
            effect.transform.SetParent(player.transform);
            Destroy(effect, _poisonDuration);
        }
    }

    private void HandleTailAnimation()
    {
        if (_tailTransform == null) return;

        if (_isAttacking)
        {
            float attackProgress = Mathf.PingPong(Time.time * _tailAttackSpeed, 1f);
            Vector3 targetPosition = _tailOriginalPosition + Vector3.up * _tailAttackHeight * attackProgress;
            _tailTransform.localPosition = Vector3.Lerp(_tailTransform.localPosition, targetPosition, Time.deltaTime * 10f);
        }
        else
        {
            _tailTransform.localPosition = Vector3.Lerp(_tailTransform.localPosition, _tailOriginalPosition, Time.deltaTime * 5f);
        }
    }

    public void TakeDamage(int damage)
    {
        if (_currentState == ScorpionState.Dead) return;

        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        _currentState = ScorpionState.Dead;
        
        if (_animator != null)
        {
            _animator.SetTrigger("Death");
        }
        
        if (_audioSource != null && _deathSound != null)
        {
            _audioSource.PlayOneShot(_deathSound);
        }
        
        _events?.OnDeath?.Invoke();
        
        GetComponent<Collider>().enabled = false;
        _rigidbody.isKinematic = true;
        
        Destroy(gameObject, 3f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _currentState != ScorpionState.Dead)
        {
            if (Vector3.Distance(transform.position, other.transform.position) <= _attackRange)
            {
                if (Time.time - _lastAttackTime >= _attackCooldown)
                {
                    _currentState = ScorpionState.Attacking;
                    StartAttack();
                }
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_initialPosition, _wanderRadius);
    }

    [System.Serializable]
    public class PoisonEffect : MonoBehaviour
    {
        private float _damagePerSecond;
        private float _duration;
        private float _startTime;
        private bool _isActive;

        public void ApplyPoison(float damage, float duration)
        {
            _damagePerSecond = damage / duration;
            _duration = duration;
            _startTime = Time.time;
            _isActive = true;
        }

        private void Update()
        {
            if (!_isActive) return;

            if (Time.time - _startTime >= _duration)
            {
                _isActive = false;
                Destroy(this);
                return;
            }

            // Apply poison damage (you would typically reduce player health here)
            // Since we can't reference PlayerHealth, this is a placeholder
            Debug.Log($"Poison damage applied: {_damagePerSecond * Time.deltaTime}");
        }
    }
}