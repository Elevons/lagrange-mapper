// Prompt: mace with stun effect
// Type: general

using UnityEngine;
using System.Collections;

public class Mace : MonoBehaviour
{
    [Header("Mace Settings")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Stun Effect")]
    [SerializeField] private float _stunDuration = 2f;
    [SerializeField] private float _stunChance = 0.3f;
    [SerializeField] private GameObject _stunEffectPrefab;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _hitEffect;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _stunSound;
    [SerializeField] private Transform _attackPoint;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _attackTrigger = "Attack";
    
    private AudioSource _audioSource;
    private bool _canAttack = true;
    private Camera _playerCamera;
    
    [System.Serializable]
    public class StunnedTarget
    {
        public GameObject target;
        public float stunEndTime;
        public GameObject stunEffect;
        public Vector3 originalVelocity;
        public bool wasKinematic;
    }
    
    private System.Collections.Generic.List<StunnedTarget> _stunnedTargets = new System.Collections.Generic.List<StunnedTarget>();
    
    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_attackPoint == null)
            _attackPoint = transform;
            
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }
    
    void Update()
    {
        HandleInput();
        UpdateStunnedTargets();
    }
    
    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && _canAttack)
        {
            Attack();
        }
    }
    
    void Attack()
    {
        if (!_canAttack) return;
        
        _canAttack = false;
        
        if (_animator != null)
            _animator.SetTrigger(_attackTrigger);
            
        StartCoroutine(PerformAttack());
        StartCoroutine(AttackCooldown());
    }
    
    IEnumerator PerformAttack()
    {
        yield return new WaitForSeconds(0.3f);
        
        Collider[] hitTargets = Physics.OverlapSphere(_attackPoint.position, _attackRange, _targetLayers);
        
        foreach (Collider target in hitTargets)
        {
            if (target.gameObject == gameObject) continue;
            
            ProcessHit(target.gameObject);
        }
    }
    
    void ProcessHit(GameObject target)
    {
        // Apply damage
        var healthComponent = target.GetComponent<Health>();
        if (healthComponent != null)
        {
            healthComponent.TakeDamage(_damage);
        }
        
        // Check for stun
        bool shouldStun = Random.Range(0f, 1f) <= _stunChance;
        if (shouldStun)
        {
            ApplyStun(target);
        }
        
        // Visual and audio effects
        PlayHitEffects(target.transform.position, shouldStun);
    }
    
    void ApplyStun(GameObject target)
    {
        // Remove existing stun if present
        RemoveStun(target);
        
        StunnedTarget stunnedTarget = new StunnedTarget
        {
            target = target,
            stunEndTime = Time.time + _stunDuration
        };
        
        // Handle different target types
        var rigidbody = target.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            stunnedTarget.originalVelocity = rigidbody.velocity;
            stunnedTarget.wasKinematic = rigidbody.isKinematic;
            rigidbody.velocity = Vector3.zero;
            rigidbody.isKinematic = true;
        }
        
        var navMeshAgent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (navMeshAgent != null)
        {
            navMeshAgent.isStopped = true;
        }
        
        var characterController = target.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }
        
        // Spawn stun effect
        if (_stunEffectPrefab != null)
        {
            stunnedTarget.stunEffect = Instantiate(_stunEffectPrefab, target.transform.position + Vector3.up, Quaternion.identity);
            stunnedTarget.stunEffect.transform.SetParent(target.transform);
        }
        
        _stunnedTargets.Add(stunnedTarget);
    }
    
    void UpdateStunnedTargets()
    {
        for (int i = _stunnedTargets.Count - 1; i >= 0; i--)
        {
            if (_stunnedTargets[i].target == null || Time.time >= _stunnedTargets[i].stunEndTime)
            {
                RemoveStunAtIndex(i);
            }
        }
    }
    
    void RemoveStun(GameObject target)
    {
        for (int i = _stunnedTargets.Count - 1; i >= 0; i--)
        {
            if (_stunnedTargets[i].target == target)
            {
                RemoveStunAtIndex(i);
                break;
            }
        }
    }
    
    void RemoveStunAtIndex(int index)
    {
        if (index < 0 || index >= _stunnedTargets.Count) return;
        
        StunnedTarget stunnedTarget = _stunnedTargets[index];
        
        if (stunnedTarget.target != null)
        {
            // Restore movement
            var rigidbody = stunnedTarget.target.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.isKinematic = stunnedTarget.wasKinematic;
                if (!stunnedTarget.wasKinematic)
                    rigidbody.velocity = stunnedTarget.originalVelocity;
            }
            
            var navMeshAgent = stunnedTarget.target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (navMeshAgent != null)
            {
                navMeshAgent.isStopped = false;
            }
            
            var characterController = stunnedTarget.target.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }
        
        // Remove stun effect
        if (stunnedTarget.stunEffect != null)
        {
            Destroy(stunnedTarget.stunEffect);
        }
        
        _stunnedTargets.RemoveAt(index);
    }
    
    void PlayHitEffects(Vector3 position, bool wasStunned)
    {
        if (_hitEffect != null)
        {
            _hitEffect.transform.position = position;
            _hitEffect.Play();
        }
        
        if (_audioSource != null)
        {
            if (wasStunned && _stunSound != null)
                _audioSource.PlayOneShot(_stunSound);
            else if (_hitSound != null)
                _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    IEnumerator AttackCooldown()
    {
        yield return new WaitForSeconds(_attackCooldown);
        _canAttack = true;
    }
    
    void OnDrawGizmosSelected()
    {
        if (_attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_attackPoint.position, _attackRange);
        }
    }
    
    void OnDestroy()
    {
        // Clean up all stuns when mace is destroyed
        for (int i = _stunnedTargets.Count - 1; i >= 0; i--)
        {
            RemoveStunAtIndex(i);
        }
    }
}

[System.Serializable]
public class Health : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    void Start()
    {
        _currentHealth = _maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    void Die()
    {
        Destroy(gameObject);
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}