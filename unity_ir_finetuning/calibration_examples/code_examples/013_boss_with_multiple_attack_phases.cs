// Prompt: boss with multiple attack phases
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class BossController : MonoBehaviour
{
    [System.Serializable]
    public class BossPhase
    {
        [Header("Phase Settings")]
        public string phaseName = "Phase";
        public float healthThreshold = 100f;
        public float moveSpeed = 3f;
        public float attackCooldown = 2f;
        public Color phaseColor = Color.white;
        
        [Header("Attack Pattern")]
        public AttackType[] attackPattern;
        public float attackDamage = 10f;
        public float attackRange = 5f;
    }
    
    [System.Serializable]
    public enum AttackType
    {
        MeleeSlash,
        RangedProjectile,
        AreaSlam,
        ChargeAttack,
        SpinAttack
    }
    
    [Header("Boss Stats")]
    [SerializeField] private float _maxHealth = 300f;
    [SerializeField] private float _currentHealth;
    [SerializeField] private bool _isInvulnerable = false;
    
    [Header("Boss Phases")]
    [SerializeField] private BossPhase[] _phases;
    [SerializeField] private int _currentPhaseIndex = 0;
    
    [Header("Movement")]
    [SerializeField] private Transform _player;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Attack Settings")]
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _projectileSpeed = 10f;
    [SerializeField] private LayerMask _damageLayer = 1;
    
    [Header("Visual Effects")]
    [SerializeField] private Renderer _bossRenderer;
    [SerializeField] private ParticleSystem _phaseTransitionEffect;
    [SerializeField] private ParticleSystem _attackEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _phaseTransitionSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Events")]
    public UnityEvent OnBossDefeated;
    public UnityEvent<int> OnPhaseChanged;
    public UnityEvent<float> OnHealthChanged;
    
    private Rigidbody _rigidbody;
    private bool _isAttacking = false;
    private bool _isDead = false;
    private float _lastAttackTime;
    private int _currentAttackIndex = 0;
    private Coroutine _currentAttackCoroutine;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_bossRenderer == null)
            _bossRenderer = GetComponent<Renderer>();
    }
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        InitializePhases();
        FindPlayer();
        
        if (_phases.Length > 0)
        {
            ApplyPhaseSettings(_phases[0]);
        }
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _detectionRange)
        {
            if (!_isAttacking)
            {
                MoveTowardsPlayer();
                TryAttack();
            }
        }
    }
    
    private void InitializePhases()
    {
        if (_phases == null || _phases.Length == 0)
        {
            _phases = new BossPhase[3];
            
            _phases[0] = new BossPhase
            {
                phaseName = "Phase 1",
                healthThreshold = _maxHealth * 0.66f,
                moveSpeed = 2f,
                attackCooldown = 3f,
                phaseColor = Color.green,
                attackPattern = new AttackType[] { AttackType.MeleeSlash, AttackType.RangedProjectile },
                attackDamage = 15f,
                attackRange = 3f
            };
            
            _phases[1] = new BossPhase
            {
                phaseName = "Phase 2",
                healthThreshold = _maxHealth * 0.33f,
                moveSpeed = 3f,
                attackCooldown = 2f,
                phaseColor = Color.yellow,
                attackPattern = new AttackType[] { AttackType.AreaSlam, AttackType.ChargeAttack, AttackType.RangedProjectile },
                attackDamage = 20f,
                attackRange = 4f
            };
            
            _phases[2] = new BossPhase
            {
                phaseName = "Phase 3",
                healthThreshold = 0f,
                moveSpeed = 4f,
                attackCooldown = 1.5f,
                phaseColor = Color.red,
                attackPattern = new AttackType[] { AttackType.SpinAttack, AttackType.ChargeAttack, AttackType.AreaSlam, AttackType.RangedProjectile },
                attackDamage = 25f,
                attackRange = 5f
            };
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
    
    private void MoveTowardsPlayer()
    {
        if (_player == null || _currentPhaseIndex >= _phases.Length) return;
        
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        
        float moveSpeed = _phases[_currentPhaseIndex].moveSpeed;
        _rigidbody.MovePosition(transform.position + direction * moveSpeed * Time.deltaTime);
        
        transform.LookAt(new Vector3(_player.position.x, transform.position.y, _player.position.z));
    }
    
    private void TryAttack()
    {
        if (_currentPhaseIndex >= _phases.Length) return;
        
        BossPhase currentPhase = _phases[_currentPhaseIndex];
        
        if (Time.time - _lastAttackTime >= currentPhase.attackCooldown)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
            
            if (distanceToPlayer <= currentPhase.attackRange)
            {
                PerformAttack();
            }
        }
    }
    
    private void PerformAttack()
    {
        if (_currentPhaseIndex >= _phases.Length) return;
        
        BossPhase currentPhase = _phases[_currentPhaseIndex];
        
        if (currentPhase.attackPattern.Length == 0) return;
        
        AttackType attackType = currentPhase.attackPattern[_currentAttackIndex];
        _currentAttackIndex = (_currentAttackIndex + 1) % currentPhase.attackPattern.Length;
        
        _lastAttackTime = Time.time;
        
        if (_currentAttackCoroutine != null)
        {
            StopCoroutine(_currentAttackCoroutine);
        }
        
        _currentAttackCoroutine = StartCoroutine(ExecuteAttack(attackType, currentPhase));
    }
    
    private IEnumerator ExecuteAttack(AttackType attackType, BossPhase phase)
    {
        _isAttacking = true;
        
        PlayAttackSound();
        PlayAttackEffect();
        
        switch (attackType)
        {
            case AttackType.MeleeSlash:
                yield return StartCoroutine(MeleeSlashAttack(phase));
                break;
            case AttackType.RangedProjectile:
                yield return StartCoroutine(RangedProjectileAttack(phase));
                break;
            case AttackType.AreaSlam:
                yield return StartCoroutine(AreaSlamAttack(phase));
                break;
            case AttackType.ChargeAttack:
                yield return StartCoroutine(ChargeAttack(phase));
                break;
            case AttackType.SpinAttack:
                yield return StartCoroutine(SpinAttack(phase));
                break;
        }
        
        _isAttacking = false;
    }
    
    private IEnumerator MeleeSlashAttack(BossPhase phase)
    {
        yield return new WaitForSeconds(0.5f);
        
        Collider[] hitColliders = Physics.OverlapSphere(_attackPoint.position, phase.attackRange, _damageLayer);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                DamagePlayer(hit.gameObject, phase.attackDamage);
            }
        }
    }
    
    private IEnumerator RangedProjectileAttack(BossPhase phase)
    {
        if (_projectilePrefab != null && _player != null)
        {
            Vector3 direction = (_player.position - _attackPoint.position).normalized;
            
            GameObject projectile = Instantiate(_projectilePrefab, _attackPoint.position, Quaternion.LookRotation(direction));
            Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
            
            if (projectileRb == null)
                projectileRb = projectile.AddComponent<Rigidbody>();
                
            projectileRb.velocity = direction * _projectileSpeed;
            
            ProjectileDamage projectileDamage = projectile.GetComponent<ProjectileDamage>();
            if (projectileDamage == null)
                projectileDamage = projectile.AddComponent<ProjectileDamage>();
                
            projectileDamage.damage = phase.attackDamage;
            
            Destroy(projectile, 5f);
        }
        
        yield return new WaitForSeconds(0.3f);
    }
    
    private IEnumerator AreaSlamAttack(BossPhase phase)
    {
        yield return new WaitForSeconds(1f);
        
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, phase.attackRange * 1.5f, _damageLayer);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                DamagePlayer(hit.gameObject, phase.attackDamage * 1.2f);
            }
        }
    }
    
    private IEnumerator ChargeAttack(BossPhase phase)
    {
        if (_player == null) yield break;
        
        Vector3 chargeDirection = (_player.position - transform.position).normalized;
        chargeDirection.y = 0;
        
        float chargeDistance = 8f;
        float chargeSpeed = phase.moveSpeed * 3f;
        
        Vector3 startPosition = transform.position;
        Vector3 targetPosition = startPosition + chargeDirection * chargeDistance;
        
        float chargeTime = chargeDistance / chargeSpeed;
        float elapsedTime = 0f;
        
        while (elapsedTime < chargeTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / chargeTime;
            
            Vector3 currentPosition = Vector3.Lerp(startPosition, targetPosition, progress);
            _rigidbody.MovePosition(currentPosition);
            
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 1.5f, _damageLayer);
            foreach (Collider hit in hitColliders)
            {
                if (hit.CompareTag("Player"))
                {
                    DamagePlayer(hit.gameObject, phase.attackDamage * 1.5f);
                }
            }
            
            yield return null;
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private IEnumerator SpinAttack(BossPhase phase)
    {
        float spinDuration = 2f;
        float spinSpeed = 720f;
        float elapsedTime = 0f;
        
        while (elapsedTime < spinDuration)
        {
            elapsedTime += Time.deltaTime;
            transform.Rotate(0, spinSpeed * Time.deltaTime, 0);
            
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, phase.attackRange, _damageLayer);
            foreach (Collider hit in hitColliders)
            {
                if (hit.CompareTag("Player"))
                {
                    DamagePlayer(hit.gameObject, phase.attackDamage * 0.5f);
                }
            }
            
            yield return null;
        }
    }
    
    private void DamagePlayer(GameObject player, float damage)
    {
        // Send damage message to player
        player.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead || _isInvulnerable) return;
        
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0, _currentHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
        
        CheckPhaseTransition();
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    private void CheckPhaseTransition()
    {
        if (_currentPhaseIndex >= _phases.Length - 1) return;
        
        BossPhase nextPhase = _phases[_currentPhaseIndex + 1];
        
        if (_currentHealth <= nextPhase.healthThreshold)
        {
            TransitionToNextPhase();
        }
    }
    
    private void TransitionToNextPhase()
    {
        _currentPhaseIndex++;
        
        if (_currentPhaseIndex < _phases.Length)
        {
            ApplyPhaseSettings(_phases[_currentPhaseIndex]);
            OnPhaseChanged?.Invoke(_currentPhaseIndex);
            
            PlayPhaseTransitionEffect();
            PlayPhaseTransitionSound();
            
            _isInvulnerable = true;
            StartCoroutine(InvulnerabilityPeriod(2f));
        }
    }
    
    private void ApplyPhaseSettings(BossPhase phase)
    {
        if (_bossRenderer != null)
        {
            _bossRenderer.material.color = phase.phaseColor;
        }
        
        _currentAttackIndex = 0;
    }
    
    private IEnumerator InvulnerabilityPeriod(float duration)
    {
        yield return new WaitForSeconds(duration);
        _isInvulnerable = false;
    }
    
    private void Die()
    {
        _isDead = true;
        _isAttacking = false;