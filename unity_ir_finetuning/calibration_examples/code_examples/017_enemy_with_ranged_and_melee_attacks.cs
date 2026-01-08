// Prompt: enemy with ranged and melee attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class Enemy : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 180f;
    
    [Header("Combat Ranges")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _meleeRange = 2f;
    [SerializeField] private float _rangedRange = 8f;
    [SerializeField] private float _attackCooldown = 2f;
    
    [Header("Melee Attack")]
    [SerializeField] private float _meleeDamage = 25f;
    [SerializeField] private float _meleeAttackDuration = 0.5f;
    
    [Header("Ranged Attack")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _projectileSpeed = 10f;
    [SerializeField] private float _rangedDamage = 15f;
    
    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent OnMeleeAttack;
    public UnityEvent OnRangedAttack;
    
    private Transform _player;
    private Rigidbody _rigidbody;
    private float _lastAttackTime;
    private bool _isAttacking;
    private EnemyState _currentState;
    
    private enum EnemyState
    {
        Idle,
        Chasing,
        MeleeAttacking,
        RangedAttacking,
        Dead
    }
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        _rigidbody = GetComponent<Rigidbody>();
        _currentState = EnemyState.Idle;
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.forward;
            _firePoint = firePointObj.transform;
        }
        
        FindPlayer();
    }
    
    private void Update()
    {
        if (_currentState == EnemyState.Dead) return;
        
        FindPlayer();
        UpdateState();
        HandleState();
    }
    
    private void FindPlayer()
    {
        if (_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                _player = playerObj.transform;
            }
        }
    }
    
    private void UpdateState()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (_isAttacking) return;
        
        if (distanceToPlayer > _detectionRange)
        {
            _currentState = EnemyState.Idle;
        }
        else if (distanceToPlayer <= _meleeRange && CanAttack())
        {
            _currentState = EnemyState.MeleeAttacking;
        }
        else if (distanceToPlayer <= _rangedRange && distanceToPlayer > _meleeRange && CanAttack())
        {
            _currentState = EnemyState.RangedAttacking;
        }
        else if (distanceToPlayer <= _detectionRange)
        {
            _currentState = EnemyState.Chasing;
        }
    }
    
    private void HandleState()
    {
        switch (_currentState)
        {
            case EnemyState.Idle:
                break;
                
            case EnemyState.Chasing:
                ChasePlayer();
                break;
                
            case EnemyState.MeleeAttacking:
                PerformMeleeAttack();
                break;
                
            case EnemyState.RangedAttacking:
                PerformRangedAttack();
                break;
        }
    }
    
    private void ChasePlayer()
    {
        if (_player == null) return;
        
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        
        _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.deltaTime);
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void PerformMeleeAttack()
    {
        if (_isAttacking) return;
        
        StartCoroutine(MeleeAttackCoroutine());
    }
    
    private System.Collections.IEnumerator MeleeAttackCoroutine()
    {
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        OnMeleeAttack?.Invoke();
        
        yield return new WaitForSeconds(_meleeAttackDuration);
        
        if (_player != null && Vector3.Distance(transform.position, _player.position) <= _meleeRange)
        {
            DealDamageToPlayer(_meleeDamage);
        }
        
        _isAttacking = false;
    }
    
    private void PerformRangedAttack()
    {
        if (_isAttacking) return;
        
        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        OnRangedAttack?.Invoke();
        
        FireProjectile();
        
        Invoke(nameof(ResetAttacking), 0.5f);
    }
    
    private void FireProjectile()
    {
        if (_projectilePrefab == null || _player == null) return;
        
        Vector3 direction = (_player.position - _firePoint.position).normalized;
        GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, Quaternion.LookRotation(direction));
        
        EnemyProjectile projectileScript = projectile.GetComponent<EnemyProjectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<EnemyProjectile>();
        }
        
        projectileScript.Initialize(direction, _projectileSpeed, _rangedDamage);
    }
    
    private void ResetAttacking()
    {
        _isAttacking = false;
    }
    
    private bool CanAttack()
    {
        return Time.time >= _lastAttackTime + _attackCooldown;
    }
    
    private void DealDamageToPlayer(float damage)
    {
        if (_player == null) return;
        
        _player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
    
    public void TakeDamage(float damage)
    {
        if (_currentState == EnemyState.Dead) return;
        
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        _currentState = EnemyState.Dead;
        OnDeath?.Invoke();
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
        
        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }
        
        Destroy(gameObject, 3f);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _rangedRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _meleeRange);
    }
}

public class EnemyProjectile : MonoBehaviour
{
    private Vector3 _direction;
    private float _speed;
    private float _damage;
    private float _lifetime = 5f;
    
    public void Initialize(Vector3 direction, float speed, float damage)
    {
        _direction = direction;
        _speed = speed;
        _damage = damage;
        
        Destroy(gameObject, _lifetime);
    }
    
    private void Update()
    {
        transform.Translate(_direction * _speed * Time.deltaTime, Space.World);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            Destroy(gameObject);
        }
        else if (!other.isTrigger && !other.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Enemy"))
        {
            Destroy(gameObject);
        }
    }
}