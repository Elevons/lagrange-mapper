// Prompt: falling boulder triggered by proximity
// Type: general

using UnityEngine;

public class FallingBoulder : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private LayerMask _playerLayer = -1;
    [SerializeField] private string _playerTag = "Player";
    
    [Header("Boulder Settings")]
    [SerializeField] private float _fallSpeed = 10f;
    [SerializeField] private float _fallAcceleration = 9.81f;
    [SerializeField] private float _maxFallSpeed = 20f;
    [SerializeField] private float _destroyDelay = 3f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _dustEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _triggerSound;
    [SerializeField] private AudioClip _impactSound;
    
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 50f;
    [SerializeField] private float _damageRadius = 2f;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private bool _isTriggered = false;
    private bool _hasFallen = false;
    private Vector3 _initialPosition;
    private float _currentFallSpeed = 0f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _initialPosition = transform.position;
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
    }
    
    private void Update()
    {
        if (!_isTriggered && !_hasFallen)
        {
            CheckForPlayer();
        }
        
        if (_isTriggered && !_hasFallen)
        {
            HandleFalling();
        }
    }
    
    private void CheckForPlayer()
    {
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, _detectionRadius, _playerLayer);
        
        foreach (Collider player in playersInRange)
        {
            if (player.CompareTag(_playerTag))
            {
                TriggerFall();
                break;
            }
        }
    }
    
    private void TriggerFall()
    {
        if (_isTriggered) return;
        
        _isTriggered = true;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        
        if (_triggerSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_triggerSound);
        }
        
        if (_dustEffect != null)
        {
            _dustEffect.Play();
        }
    }
    
    private void HandleFalling()
    {
        _currentFallSpeed += _fallAcceleration * Time.deltaTime;
        _currentFallSpeed = Mathf.Min(_currentFallSpeed, _maxFallSpeed);
        
        Vector3 velocity = _rigidbody.velocity;
        velocity.y = -_currentFallSpeed;
        _rigidbody.velocity = velocity;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasFallen) return;
        
        if (collision.gameObject.CompareTag("Ground") || collision.gameObject.layer == LayerMask.NameToLayer("Ground"))
        {
            HandleImpact();
        }
        else if (collision.gameObject.CompareTag(_playerTag))
        {
            DealDamageToPlayer(collision.gameObject);
            HandleImpact();
        }
    }
    
    private void HandleImpact()
    {
        _hasFallen = true;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        
        if (_impactSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_impactSound);
        }
        
        DealAreaDamage();
        
        if (_dustEffect != null)
        {
            _dustEffect.Stop();
            _dustEffect.Play();
        }
        
        Invoke(nameof(DestroyBoulder), _destroyDelay);
    }
    
    private void DealDamageToPlayer(GameObject player)
    {
        var playerHealth = player.GetComponent<Health>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(_damageAmount);
        }
    }
    
    private void DealAreaDamage()
    {
        Collider[] objectsInDamageRadius = Physics.OverlapSphere(transform.position, _damageRadius);
        
        foreach (Collider obj in objectsInDamageRadius)
        {
            if (obj.CompareTag(_playerTag))
            {
                var health = obj.GetComponent<Health>();
                if (health != null)
                {
                    float distance = Vector3.Distance(transform.position, obj.transform.position);
                    float damageMultiplier = 1f - (distance / _damageRadius);
                    float finalDamage = _damageAmount * damageMultiplier;
                    health.TakeDamage(finalDamage);
                }
            }
        }
    }
    
    private void DestroyBoulder()
    {
        Destroy(gameObject);
    }
    
    public void ResetBoulder()
    {
        transform.position = _initialPosition;
        _isTriggered = false;
        _hasFallen = false;
        _currentFallSpeed = 0f;
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.velocity = Vector3.zero;
        
        if (_dustEffect != null)
        {
            _dustEffect.Stop();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _damageRadius);
    }
    
    [System.Serializable]
    public class Health : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _currentHealth;
        
        private void Start()
        {
            _currentHealth = _maxHealth;
        }
        
        public void TakeDamage(float damage)
        {
            _currentHealth -= damage;
            _currentHealth = Mathf.Max(0, _currentHealth);
            
            if (_currentHealth <= 0)
            {
                Die();
            }
        }
        
        public void Heal(float amount)
        {
            _currentHealth += amount;
            _currentHealth = Mathf.Min(_maxHealth, _currentHealth);
        }
        
        private void Die()
        {
            gameObject.SetActive(false);
        }
        
        public float GetCurrentHealth()
        {
            return _currentHealth;
        }
        
        public float GetMaxHealth()
        {
            return _maxHealth;
        }
    }
}