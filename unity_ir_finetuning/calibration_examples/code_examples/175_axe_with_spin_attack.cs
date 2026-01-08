// Prompt: axe with spin attack
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class SpinAxe : MonoBehaviour
{
    [Header("Axe Settings")]
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _spinDuration = 1.5f;
    [SerializeField] private float _spinSpeed = 720f;
    [SerializeField] private float _cooldownTime = 3f;
    [SerializeField] private float _attackRadius = 2f;
    [SerializeField] private LayerMask _enemyLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _spinEffect;
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _spinSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Events")]
    public UnityEvent OnSpinStart;
    public UnityEvent OnSpinEnd;
    public UnityEvent<GameObject> OnEnemyHit;
    
    private bool _isSpinning = false;
    private bool _canSpin = true;
    private float _spinTimer = 0f;
    private float _cooldownTimer = 0f;
    private Transform _axeTransform;
    private Vector3 _originalRotation;
    private System.Collections.Generic.HashSet<Collider> _hitEnemies = new System.Collections.Generic.HashSet<Collider>();
    
    private void Start()
    {
        _axeTransform = transform;
        _originalRotation = _axeTransform.eulerAngles;
        
        if (_trailRenderer != null)
            _trailRenderer.enabled = false;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleCooldown();
        HandleSpinInput();
        HandleSpinRotation();
    }
    
    private void HandleCooldown()
    {
        if (!_canSpin)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _canSpin = true;
            }
        }
    }
    
    private void HandleSpinInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) && _canSpin && !_isSpinning)
        {
            StartSpinAttack();
        }
    }
    
    private void HandleSpinRotation()
    {
        if (_isSpinning)
        {
            _spinTimer -= Time.deltaTime;
            
            float rotationAmount = _spinSpeed * Time.deltaTime;
            _axeTransform.Rotate(0, 0, rotationAmount);
            
            CheckForEnemiesInRange();
            
            if (_spinTimer <= 0f)
            {
                EndSpinAttack();
            }
        }
    }
    
    private void StartSpinAttack()
    {
        _isSpinning = true;
        _canSpin = false;
        _spinTimer = _spinDuration;
        _cooldownTimer = _cooldownTime;
        _hitEnemies.Clear();
        
        if (_trailRenderer != null)
            _trailRenderer.enabled = true;
            
        if (_spinEffect != null)
            _spinEffect.Play();
            
        if (_audioSource != null && _spinSound != null)
            _audioSource.PlayOneShot(_spinSound);
            
        OnSpinStart?.Invoke();
    }
    
    private void EndSpinAttack()
    {
        _isSpinning = false;
        _axeTransform.eulerAngles = _originalRotation;
        
        if (_trailRenderer != null)
            _trailRenderer.enabled = false;
            
        if (_spinEffect != null)
            _spinEffect.Stop();
            
        OnSpinEnd?.Invoke();
    }
    
    private void CheckForEnemiesInRange()
    {
        Collider[] enemiesInRange = Physics.OverlapSphere(_axeTransform.position, _attackRadius, _enemyLayers);
        
        foreach (Collider enemy in enemiesInRange)
        {
            if (!_hitEnemies.Contains(enemy) && !enemy.CompareTag("Player"))
            {
                HitEnemy(enemy);
                _hitEnemies.Add(enemy);
            }
        }
    }
    
    private void HitEnemy(Collider enemy)
    {
        // Apply damage through various methods
        var healthComponent = enemy.GetComponent<Health>();
        if (healthComponent != null)
        {
            healthComponent.TakeDamage(_damage);
        }
        
        // Try alternative damage methods
        enemy.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        enemy.SendMessage("OnHit", _damage, SendMessageOptions.DontRequireReceiver);
        
        // Apply knockback
        Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            Vector3 knockbackDirection = (enemy.transform.position - _axeTransform.position).normalized;
            enemyRb.AddForce(knockbackDirection * 500f);
        }
        
        // Play hit sound
        if (_audioSource != null && _hitSound != null)
            _audioSource.PlayOneShot(_hitSound);
            
        OnEnemyHit?.Invoke(enemy.gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isSpinning ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attackRadius);
    }
    
    [System.Serializable]
    public class Health : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;
        private float _currentHealth;
        
        private void Start()
        {
            _currentHealth = _maxHealth;
        }
        
        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            if (_currentHealth <= 0f)
            {
                Die();
            }
        }
        
        private void Die()
        {
            Destroy(gameObject);
        }
    }
}