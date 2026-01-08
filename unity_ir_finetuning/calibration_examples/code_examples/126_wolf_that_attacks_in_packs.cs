// Prompt: wolf that attacks in packs
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class PackWolf : MonoBehaviour
{
    [Header("Wolf Stats")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _attackDamage = 20f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackCooldown = 1.5f;
    
    [Header("Pack Behavior")]
    [SerializeField] private float _packDetectionRadius = 15f;
    [SerializeField] private float _packFormationDistance = 3f;
    [SerializeField] private float _packAttackBonus = 1.2f;
    [SerializeField] private int _maxPackSize = 5;
    
    [Header("AI Behavior")]
    [SerializeField] private float _detectionRadius = 10f;
    [SerializeField] private float _loseTargetDistance = 20f;
    [SerializeField] private float _circleDistance = 4f;
    [SerializeField] private float _circleSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _howlSound;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    public UnityEvent OnPackFormed;
    
    private float _currentHealth;
    private Transform _target;
    private List<PackWolf> _packMembers = new List<PackWolf>();
    private bool _isPackLeader = false;
    private float _lastAttackTime;
    private Vector3 _circlePosition;
    private float _circleAngle;
    private bool _isDead = false;
    
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Animator _animator;
    private Collider _collider;
    
    private enum WolfState
    {
        Idle,
        Hunting,
        Circling,
        Attacking,
        Dead
    }
    
    private WolfState _currentState = WolfState.Idle;
    
    void Start()
    {
        _currentHealth = _maxHealth;
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _circleAngle = Random.Range(0f, 360f);
        
        StartCoroutine(PackDetectionCoroutine());
        StartCoroutine(TargetDetectionCoroutine());
    }
    
    void Update()
    {
        if (_isDead) return;
        
        UpdateStateMachine();
        UpdateAnimations();
    }
    
    void FixedUpdate()
    {
        if (_isDead) return;
        
        switch (_currentState)
        {
            case WolfState.Hunting:
                MoveTowardsTarget();
                break;
            case WolfState.Circling:
                CircleTarget();
                break;
            case WolfState.Attacking:
                AttackTarget();
                break;
        }
    }
    
    private void UpdateStateMachine()
    {
        if (_target == null)
        {
            _currentState = WolfState.Idle;
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        
        if (distanceToTarget > _loseTargetDistance)
        {
            _target = null;
            _currentState = WolfState.Idle;
            return;
        }
        
        switch (_currentState)
        {
            case WolfState.Idle:
                if (_target != null)
                    _currentState = WolfState.Hunting;
                break;
                
            case WolfState.Hunting:
                if (distanceToTarget <= _circleDistance)
                    _currentState = WolfState.Circling;
                break;
                
            case WolfState.Circling:
                if (distanceToTarget <= _attackRange && Time.time >= _lastAttackTime + _attackCooldown)
                    _currentState = WolfState.Attacking;
                else if (distanceToTarget > _circleDistance + 2f)
                    _currentState = WolfState.Hunting;
                break;
                
            case WolfState.Attacking:
                if (Time.time >= _lastAttackTime + _attackCooldown)
                    _currentState = WolfState.Circling;
                break;
        }
    }
    
    private void MoveTowardsTarget()
    {
        if (_target == null) return;
        
        Vector3 direction = (_target.position - transform.position).normalized;
        _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
        
        transform.LookAt(new Vector3(_target.position.x, transform.position.y, _target.position.z));
    }
    
    private void CircleTarget()
    {
        if (_target == null) return;
        
        _circleAngle += _circleSpeed * Time.fixedDeltaTime;
        
        Vector3 offset = new Vector3(
            Mathf.Cos(_circleAngle) * _circleDistance,
            0f,
            Mathf.Sin(_circleAngle) * _circleDistance
        );
        
        Vector3 targetPosition = _target.position + offset;
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
        
        transform.LookAt(new Vector3(_target.position.x, transform.position.y, _target.position.z));
    }
    
    private void AttackTarget()
    {
        if (_target == null || Time.time < _lastAttackTime + _attackCooldown) return;
        
        float distance = Vector3.Distance(transform.position, _target.position);
        if (distance <= _attackRange)
        {
            PerformAttack();
            _lastAttackTime = Time.time;
        }
    }
    
    private void PerformAttack()
    {
        float damage = _attackDamage;
        
        if (_packMembers.Count > 1)
            damage *= _packAttackBonus;
        
        if (_audioSource && _attackSound)
            _audioSource.PlayOneShot(_attackSound);
        
        if (_target.CompareTag("Player"))
        {
            var playerHealth = _target.GetComponent<PlayerHealthComponent>();
            if (playerHealth != null)
                playerHealth.TakeDamage(damage);
        }
        
        NotifyPackOfAttack();
    }
    
    private void NotifyPackOfAttack()
    {
        foreach (var wolf in _packMembers)
        {
            if (wolf != this && wolf._target == null)
            {
                wolf.SetTarget(_target);
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
        
        if (_currentHealth <= 0f)
        {
            Die();
        }
        else
        {
            AlertPack();
        }
    }
    
    private void AlertPack()
    {
        foreach (var wolf in _packMembers)
        {
            if (wolf != this && wolf._target == null)
            {
                wolf.SetTarget(_target);
            }
        }
        
        if (_audioSource && _howlSound)
            _audioSource.PlayOneShot(_howlSound);
    }
    
    private void Die()
    {
        _isDead = true;
        _currentState = WolfState.Dead;
        
        if (_audioSource && _deathSound)
            _audioSource.PlayOneShot(_deathSound);
        
        if (_collider)
            _collider.enabled = false;
        
        if (_rigidbody)
            _rigidbody.isKinematic = true;
        
        RemoveFromPack();
        OnDeath?.Invoke();
        
        StartCoroutine(DestroyAfterDelay(5f));
    }
    
    private void RemoveFromPack()
    {
        foreach (var wolf in _packMembers)
        {
            if (wolf != this)
                wolf._packMembers.Remove(this);
        }
        _packMembers.Clear();
    }
    
    public void SetTarget(Transform target)
    {
        _target = target;
        if (_currentState == WolfState.Idle)
            _currentState = WolfState.Hunting;
    }
    
    private IEnumerator PackDetectionCoroutine()
    {
        while (!_isDead)
        {
            DetectNearbyWolves();
            yield return new WaitForSeconds(2f);
        }
    }
    
    private void DetectNearbyWolves()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _packDetectionRadius);
        
        foreach (var collider in nearbyColliders)
        {
            var wolf = collider.GetComponent<PackWolf>();
            if (wolf != null && wolf != this && !wolf._isDead)
            {
                if (!_packMembers.Contains(wolf) && _packMembers.Count < _maxPackSize)
                {
                    JoinPack(wolf);
                }
            }
        }
        
        _packMembers.RemoveAll(wolf => wolf == null || wolf._isDead || 
                              Vector3.Distance(transform.position, wolf.transform.position) > _packDetectionRadius * 1.5f);
    }
    
    private void JoinPack(PackWolf otherWolf)
    {
        if (!_packMembers.Contains(otherWolf))
            _packMembers.Add(otherWolf);
            
        if (!otherWolf._packMembers.Contains(this))
            otherWolf._packMembers.Add(this);
        
        if (_packMembers.Count >= 2 && !_isPackLeader)
        {
            _isPackLeader = _packMembers.Count > otherWolf._packMembers.Count;
            OnPackFormed?.Invoke();
        }
    }
    
    private IEnumerator TargetDetectionCoroutine()
    {
        while (!_isDead)
        {
            if (_target == null)
                DetectTarget();
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private void DetectTarget()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _detectionRadius);
        
        foreach (var collider in nearbyColliders)
        {
            if (collider.CompareTag("Player"))
            {
                SetTarget(collider.transform);
                break;
            }
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator == null) return;
        
        float speed = _rigidbody.velocity.magnitude;
        _animator.SetFloat("Speed", speed);
        _animator.SetBool("IsAttacking", _currentState == WolfState.Attacking);
        _animator.SetBool("IsDead", _isDead);
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _packDetectionRadius);
        
        if (_packMembers != null)
        {
            Gizmos.color = Color.green;
            foreach (var wolf in _packMembers)
            {
                if (wolf != null && wolf != this)
                    Gizmos.DrawLine(transform.position, wolf.transform.position);
            }
        }
    }
}

[System.Serializable]
public class PlayerHealthComponent : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    private float _currentHealth;
    
    void Start()
    {
        _currentHealth = _maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0f)
        {
            Debug.Log("Player died!");
        }
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}