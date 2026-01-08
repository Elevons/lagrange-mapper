// Prompt: demon that summons minions
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class DemonSummoner : MonoBehaviour
{
    [Header("Demon Stats")]
    [SerializeField] private float _maxHealth = 200f;
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 90f;
    
    [Header("Summoning")]
    [SerializeField] private GameObject _minionPrefab;
    [SerializeField] private int _maxMinions = 5;
    [SerializeField] private float _summonCooldown = 3f;
    [SerializeField] private float _summonRange = 10f;
    [SerializeField] private float _summonRadius = 2f;
    [SerializeField] private ParticleSystem _summonEffect;
    [SerializeField] private AudioClip _summonSound;
    
    [Header("Combat")]
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private float _attackDamage = 25f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _summonTrigger = "Summon";
    [SerializeField] private string _attackTrigger = "Attack";
    [SerializeField] private string _deathTrigger = "Death";
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Events")]
    public UnityEvent OnDemonDeath;
    public UnityEvent OnMinionSummoned;
    
    private Transform _player;
    private List<GameObject> _activeMinions = new List<GameObject>();
    private float _lastSummonTime;
    private float _lastAttackTime;
    private bool _isDead = false;
    private Rigidbody _rigidbody;
    private Collider _collider;
    
    private enum DemonState
    {
        Idle,
        Chasing,
        Summoning,
        Attacking,
        Dead
    }
    
    private DemonState _currentState = DemonState.Idle;
    
    void Start()
    {
        _currentHealth = _maxHealth;
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_animator == null)
            _animator = GetComponent<Animator>();
        
        FindPlayer();
        StartCoroutine(UpdateBehavior());
    }
    
    void Update()
    {
        if (_isDead) return;
        
        CleanupDeadMinions();
        
        if (_player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
            UpdateState(distanceToPlayer);
        }
    }
    
    void FixedUpdate()
    {
        if (_isDead || _player == null) return;
        
        if (_currentState == DemonState.Chasing)
        {
            MoveTowardsPlayer();
        }
    }
    
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    private void UpdateState(float distanceToPlayer)
    {
        switch (_currentState)
        {
            case DemonState.Idle:
                if (distanceToPlayer <= _detectionRange)
                {
                    _currentState = DemonState.Chasing;
                }
                break;
                
            case DemonState.Chasing:
                if (distanceToPlayer > _detectionRange)
                {
                    _currentState = DemonState.Idle;
                }
                else if (distanceToPlayer <= _attackRange && Time.time >= _lastAttackTime + _attackCooldown)
                {
                    _currentState = DemonState.Attacking;
                    StartCoroutine(AttackSequence());
                }
                else if (_activeMinions.Count < _maxMinions && Time.time >= _lastSummonTime + _summonCooldown)
                {
                    _currentState = DemonState.Summoning;
                    StartCoroutine(SummonSequence());
                }
                break;
        }
    }
    
    private void MoveTowardsPlayer()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        
        _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.fixedDeltaTime);
        }
    }
    
    private IEnumerator SummonSequence()
    {
        if (_animator != null)
            _animator.SetTrigger(_summonTrigger);
        
        yield return new WaitForSeconds(0.5f);
        
        SummonMinion();
        _lastSummonTime = Time.time;
        _currentState = DemonState.Chasing;
    }
    
    private void SummonMinion()
    {
        if (_minionPrefab == null) return;
        
        Vector3 summonPosition = GetRandomSummonPosition();
        GameObject minion = Instantiate(_minionPrefab, summonPosition, Quaternion.identity);
        _activeMinions.Add(minion);
        
        if (_summonEffect != null)
        {
            ParticleSystem effect = Instantiate(_summonEffect, summonPosition, Quaternion.identity);
            Destroy(effect.gameObject, 3f);
        }
        
        if (_audioSource != null && _summonSound != null)
        {
            _audioSource.PlayOneShot(_summonSound);
        }
        
        OnMinionSummoned?.Invoke();
    }
    
    private Vector3 GetRandomSummonPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _summonRadius;
        Vector3 summonPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        RaycastHit hit;
        if (Physics.Raycast(summonPosition + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            summonPosition.y = hit.point.y;
        }
        
        return summonPosition;
    }
    
    private IEnumerator AttackSequence()
    {
        if (_animator != null)
            _animator.SetTrigger(_attackTrigger);
        
        yield return new WaitForSeconds(0.3f);
        
        PerformAttack();
        _lastAttackTime = Time.time;
        _currentState = DemonState.Chasing;
    }
    
    private void PerformAttack()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer <= _attackRange)
        {
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, _attackRange, _playerLayer);
            
            foreach (Collider hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    // Deal damage to player - you would implement your damage system here
                    Debug.Log($"Demon deals {_attackDamage} damage to player");
                }
            }
        }
        
        if (_audioSource != null && _attackSound != null)
        {
            _audioSource.PlayOneShot(_attackSound);
        }
    }
    
    private void CleanupDeadMinions()
    {
        _activeMinions.RemoveAll(minion => minion == null);
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        _isDead = true;
        _currentState = DemonState.Dead;
        
        if (_animator != null)
            _animator.SetTrigger(_deathTrigger);
        
        if (_audioSource != null && _deathSound != null)
        {
            _audioSource.PlayOneShot(_deathSound);
        }
        
        if (_collider != null)
            _collider.enabled = false;
        
        if (_rigidbody != null)
            _rigidbody.isKinematic = true;
        
        OnDemonDeath?.Invoke();
        
        Destroy(gameObject, 3f);
    }
    
    private IEnumerator UpdateBehavior()
    {
        while (!_isDead)
        {
            if (_player == null)
            {
                FindPlayer();
            }
            
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isDead)
        {
            if (_currentState == DemonState.Idle)
            {
                _currentState = DemonState.Chasing;
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _summonRadius);
    }
}