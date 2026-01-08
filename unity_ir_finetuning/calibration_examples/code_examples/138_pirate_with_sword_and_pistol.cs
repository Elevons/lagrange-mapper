// Prompt: pirate with sword and pistol
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class PirateController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 180f;
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _shootRange = 6f;
    
    [Header("Combat")]
    [SerializeField] private float _swordDamage = 25f;
    [SerializeField] private float _pistolDamage = 40f;
    [SerializeField] private float _swordCooldown = 1.5f;
    [SerializeField] private float _pistolCooldown = 3f;
    [SerializeField] private int _maxAmmo = 6;
    [SerializeField] private float _reloadTime = 2f;
    
    [Header("Health")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _swordSwingSound;
    [SerializeField] private AudioClip _pistolFireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Effects")]
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [SerializeField] private Transform _pistolMuzzle;
    [SerializeField] private GameObject _bloodEffectPrefab;
    
    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent OnAttack;
    public UnityEvent OnReload;
    
    private Transform _target;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private AudioSource _audioSource;
    private Collider _collider;
    
    private float _lastSwordAttack;
    private float _lastPistolShot;
    private int _currentAmmo;
    private bool _isReloading;
    private bool _isDead;
    private float _reloadStartTime;
    
    private PirateState _currentState = PirateState.Patrolling;
    private Vector3 _patrolDirection;
    private float _patrolTimer;
    private float _patrolChangeInterval = 3f;
    
    private enum PirateState
    {
        Patrolling,
        Chasing,
        Attacking,
        Reloading,
        Dead
    }
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider>();
        
        _currentHealth = _maxHealth;
        _currentAmmo = _maxAmmo;
        _patrolDirection = Random.insideUnitSphere;
        _patrolDirection.y = 0;
        _patrolDirection.Normalize();
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        HandleReloading();
        UpdateState();
        HandleMovement();
        HandleCombat();
        UpdateAnimations();
    }
    
    private void HandleReloading()
    {
        if (_isReloading)
        {
            if (Time.time - _reloadStartTime >= _reloadTime)
            {
                _currentAmmo = _maxAmmo;
                _isReloading = false;
                _currentState = PirateState.Chasing;
                OnReload?.Invoke();
            }
        }
    }
    
    private void UpdateState()
    {
        if (_isReloading)
        {
            _currentState = PirateState.Reloading;
            return;
        }
        
        FindNearestPlayer();
        
        if (_target == null)
        {
            _currentState = PirateState.Patrolling;
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        
        if (distanceToTarget <= _attackRange)
        {
            _currentState = PirateState.Attacking;
        }
        else if (distanceToTarget <= _detectionRange)
        {
            _currentState = PirateState.Chasing;
        }
        else
        {
            _currentState = PirateState.Patrolling;
            _target = null;
        }
    }
    
    private void FindNearestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float nearestDistance = _detectionRange;
        Transform nearestPlayer = null;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestPlayer = player.transform;
            }
        }
        
        _target = nearestPlayer;
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection = Vector3.zero;
        
        switch (_currentState)
        {
            case PirateState.Patrolling:
                HandlePatrolling();
                moveDirection = _patrolDirection;
                break;
                
            case PirateState.Chasing:
                if (_target != null)
                {
                    moveDirection = (_target.position - transform.position).normalized;
                }
                break;
                
            case PirateState.Attacking:
                if (_target != null)
                {
                    Vector3 lookDirection = (_target.position - transform.position).normalized;
                    lookDirection.y = 0;
                    if (lookDirection != Vector3.zero)
                    {
                        Quaternion targetRotation = Quaternion.LookRotation(lookDirection);
                        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
                    }
                }
                return;
                
            case PirateState.Reloading:
                return;
        }
        
        if (moveDirection != Vector3.zero)
        {
            _rigidbody.MovePosition(transform.position + moveDirection * _moveSpeed * Time.deltaTime);
            
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void HandlePatrolling()
    {
        _patrolTimer += Time.deltaTime;
        if (_patrolTimer >= _patrolChangeInterval)
        {
            _patrolDirection = Random.insideUnitSphere;
            _patrolDirection.y = 0;
            _patrolDirection.Normalize();
            _patrolTimer = 0f;
        }
    }
    
    private void HandleCombat()
    {
        if (_currentState != PirateState.Attacking || _target == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        
        if (distanceToTarget <= _attackRange && Time.time - _lastSwordAttack >= _swordCooldown)
        {
            SwordAttack();
        }
        else if (distanceToTarget <= _shootRange && _currentAmmo > 0 && Time.time - _lastPistolShot >= _pistolCooldown)
        {
            PistolShoot();
        }
        else if (_currentAmmo == 0 && !_isReloading)
        {
            StartReload();
        }
    }
    
    private void SwordAttack()
    {
        _lastSwordAttack = Time.time;
        OnAttack?.Invoke();
        
        if (_audioSource && _swordSwingSound)
        {
            _audioSource.PlayOneShot(_swordSwingSound);
        }
        
        Collider[] hitColliders = Physics.OverlapSphere(transform.position + transform.forward * 1.5f, 1f);
        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                DealDamageToPlayer(hit.gameObject, _swordDamage);
            }
        }
    }
    
    private void PistolShoot()
    {
        _lastPistolShot = Time.time;
        _currentAmmo--;
        OnAttack?.Invoke();
        
        if (_audioSource && _pistolFireSound)
        {
            _audioSource.PlayOneShot(_pistolFireSound);
        }
        
        if (_muzzleFlashPrefab && _pistolMuzzle)
        {
            GameObject flash = Instantiate(_muzzleFlashPrefab, _pistolMuzzle.position, _pistolMuzzle.rotation);
            Destroy(flash, 0.1f);
        }
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, _shootRange))
        {
            if (hit.collider.CompareTag("Player"))
            {
                DealDamageToPlayer(hit.collider.gameObject, _pistolDamage);
            }
        }
    }
    
    private void StartReload()
    {
        _isReloading = true;
        _reloadStartTime = Time.time;
        
        if (_audioSource && _reloadSound)
        {
            _audioSource.PlayOneShot(_reloadSound);
        }
    }
    
    private void DealDamageToPlayer(GameObject player, float damage)
    {
        if (_bloodEffectPrefab)
        {
            GameObject blood = Instantiate(_bloodEffectPrefab, player.transform.position, Quaternion.identity);
            Destroy(blood, 2f);
        }
        
        player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        
        if (_bloodEffectPrefab)
        {
            GameObject blood = Instantiate(_bloodEffectPrefab, transform.position + Vector3.up, Quaternion.identity);
            Destroy(blood, 2f);
        }
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void Die()
    {
        _isDead = true;
        _currentState = PirateState.Dead;
        
        if (_audioSource && _deathSound)
        {
            _audioSource.PlayOneShot(_deathSound);
        }
        
        if (_collider)
        {
            _collider.enabled = false;
        }
        
        if (_rigidbody)
        {
            _rigidbody.isKinematic = true;
        }
        
        OnDeath?.Invoke();
        
        Destroy(gameObject, 3f);
    }
    
    private void UpdateAnimations()
    {
        if (!_animator) return;
        
        _animator.SetBool("IsMoving", _currentState == PirateState.Patrolling || _currentState == PirateState.Chasing);
        _animator.SetBool("IsAttacking", _currentState == PirateState.Attacking);
        _animator.SetBool("IsReloading", _isReloading);
        _animator.SetBool("IsDead", _isDead);
        _animator.SetFloat("Health", _currentHealth / _maxHealth);
        _animator.SetInt("Ammo", _currentAmmo);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _shootRange);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1.5f, 1f);
    }
}