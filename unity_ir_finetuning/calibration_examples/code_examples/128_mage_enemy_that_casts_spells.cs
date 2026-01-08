// Prompt: mage enemy that casts spells
// Type: combat

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class MageEnemy : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private float _retreatDistance = 3f;
    
    [Header("Combat")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _spellCastTime = 1f;
    
    [Header("Spells")]
    [SerializeField] private GameObject _fireballPrefab;
    [SerializeField] private GameObject _iceBoltPrefab;
    [SerializeField] private GameObject _lightningPrefab;
    [SerializeField] private Transform _spellSpawnPoint;
    [SerializeField] private float _projectileSpeed = 15f;
    [SerializeField] private int _projectileDamage = 25;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _castingEffect;
    [SerializeField] private ParticleSystem _deathEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _castSound;
    [SerializeField] private AudioClip _deathSound;
    
    private Transform _player;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private int _currentHealth;
    private float _lastAttackTime;
    private bool _isCasting;
    private bool _isDead;
    private EnemyState _currentState;
    private Vector3 _initialPosition;
    
    private enum EnemyState
    {
        Idle,
        Chasing,
        Attacking,
        Retreating,
        Dead
    }
    
    private enum SpellType
    {
        Fireball,
        IceBolt,
        Lightning
    }
    
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _currentHealth = _maxHealth;
        _currentState = EnemyState.Idle;
        _initialPosition = transform.position;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_spellSpawnPoint == null)
            _spellSpawnPoint = transform;
            
        FindPlayer();
    }
    
    void Update()
    {
        if (_isDead) return;
        
        HandleStateMachine();
        UpdateAnimations();
    }
    
    void FixedUpdate()
    {
        if (_isDead || _isCasting) return;
        
        HandleMovement();
    }
    
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            _player = playerObject.transform;
    }
    
    private void HandleStateMachine()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        switch (_currentState)
        {
            case EnemyState.Idle:
                if (distanceToPlayer <= _detectionRange)
                    _currentState = EnemyState.Chasing;
                break;
                
            case EnemyState.Chasing:
                if (distanceToPlayer > _detectionRange)
                    _currentState = EnemyState.Idle;
                else if (distanceToPlayer <= _attackRange)
                    _currentState = EnemyState.Attacking;
                break;
                
            case EnemyState.Attacking:
                if (distanceToPlayer > _attackRange)
                    _currentState = EnemyState.Chasing;
                else if (distanceToPlayer < _retreatDistance)
                    _currentState = EnemyState.Retreating;
                else
                    TryAttack();
                break;
                
            case EnemyState.Retreating:
                if (distanceToPlayer >= _retreatDistance)
                    _currentState = EnemyState.Attacking;
                break;
        }
    }
    
    private void HandleMovement()
    {
        if (_player == null) return;
        
        Vector3 direction = Vector3.zero;
        
        switch (_currentState)
        {
            case EnemyState.Chasing:
                direction = (_player.position - transform.position).normalized;
                break;
                
            case EnemyState.Retreating:
                direction = (transform.position - _player.position).normalized;
                break;
        }
        
        if (direction != Vector3.zero)
        {
            _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
            transform.LookAt(new Vector3(_player.position.x, transform.position.y, _player.position.z));
        }
    }
    
    private void TryAttack()
    {
        if (Time.time - _lastAttackTime >= _attackCooldown && !_isCasting)
        {
            StartCoroutine(CastSpell());
            _lastAttackTime = Time.time;
        }
    }
    
    private IEnumerator CastSpell()
    {
        _isCasting = true;
        
        if (_castingEffect != null)
            _castingEffect.Play();
            
        if (_audioSource != null && _castSound != null)
            _audioSource.PlayOneShot(_castSound);
        
        yield return new WaitForSeconds(_spellCastTime);
        
        SpellType spellType = (SpellType)Random.Range(0, 3);
        LaunchSpell(spellType);
        
        _isCasting = false;
    }
    
    private void LaunchSpell(SpellType spellType)
    {
        if (_player == null) return;
        
        GameObject spellPrefab = GetSpellPrefab(spellType);
        if (spellPrefab == null) return;
        
        Vector3 direction = (_player.position - _spellSpawnPoint.position).normalized;
        GameObject spell = Instantiate(spellPrefab, _spellSpawnPoint.position, Quaternion.LookRotation(direction));
        
        Rigidbody spellRb = spell.GetComponent<Rigidbody>();
        if (spellRb != null)
        {
            spellRb.velocity = direction * _projectileSpeed;
        }
        
        SpellProjectile projectile = spell.GetComponent<SpellProjectile>();
        if (projectile == null)
        {
            projectile = spell.AddComponent<SpellProjectile>();
        }
        projectile.Initialize(_projectileDamage, spellType);
        
        Destroy(spell, 5f);
    }
    
    private GameObject GetSpellPrefab(SpellType spellType)
    {
        switch (spellType)
        {
            case SpellType.Fireball:
                return _fireballPrefab;
            case SpellType.IceBolt:
                return _iceBoltPrefab;
            case SpellType.Lightning:
                return _lightningPrefab;
            default:
                return _fireballPrefab;
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator == null) return;
        
        _animator.SetBool("IsMoving", _currentState == EnemyState.Chasing || _currentState == EnemyState.Retreating);
        _animator.SetBool("IsCasting", _isCasting);
        _animator.SetBool("IsDead", _isDead);
    }
    
    public void TakeDamage(int damage)
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
        _currentState = EnemyState.Dead;
        
        if (_deathEffect != null)
            _deathEffect.Play();
            
        if (_audioSource != null && _deathSound != null)
            _audioSource.PlayOneShot(_deathSound);
        
        GetComponent<Collider>().enabled = false;
        
        Destroy(gameObject, 3f);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isDead)
        {
            _currentState = EnemyState.Attacking;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _retreatDistance);
    }
}

public class SpellProjectile : MonoBehaviour
{
    private int _damage;
    private MageEnemy.SpellType _spellType;
    private bool _hasHit;
    
    public void Initialize(int damage, MageEnemy.SpellType spellType)
    {
        _damage = damage;
        _spellType = spellType;
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        
        if (other.CompareTag("Player"))
        {
            ApplySpellEffect(other.gameObject);
            _hasHit = true;
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy"))
        {
            _hasHit = true;
            Destroy(gameObject);
        }
    }
    
    private void ApplySpellEffect(GameObject target)
    {
        // Apply damage - you would typically have a health component here
        // For now, we'll just send a message that can be caught by any health system
        target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        
        // Apply spell-specific effects
        switch (_spellType)
        {
            case MageEnemy.SpellType.IceBolt:
                // Slow effect could be applied here
                break;
            case MageEnemy.SpellType.Lightning:
                // Stun effect could be applied here
                break;
        }
    }
}