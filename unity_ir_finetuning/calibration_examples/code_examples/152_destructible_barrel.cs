// Prompt: destructible barrel
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class DestructibleBarrel : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth;
    
    [Header("Damage Settings")]
    [SerializeField] private string[] _damageableTags = { "Bullet", "Explosion", "Weapon" };
    [SerializeField] private int _explosionDamage = 50;
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private LayerMask _explosionLayerMask = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _destructionParticles;
    [SerializeField] private GameObject _damageParticles;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _destructionSound;
    [SerializeField] private Material _damagedMaterial;
    
    [Header("Physics")]
    [SerializeField] private bool _explodeOnDestruction = true;
    [SerializeField] private float _explosionForce = 500f;
    [SerializeField] private GameObject[] _debrisPrefabs;
    [SerializeField] private int _debrisCount = 5;
    [SerializeField] private float _debrisLifetime = 10f;
    
    [Header("Events")]
    public UnityEvent OnDamaged;
    public UnityEvent OnDestroyed;
    
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Collider _collider;
    private Material _originalMaterial;
    private bool _isDestroyed = false;
    
    void Start()
    {
        _currentHealth = _maxHealth;
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider>();
        
        if (_renderer != null)
        {
            _originalMaterial = _renderer.material;
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_isDestroyed) return;
        
        foreach (string tag in _damageableTags)
        {
            if (other.CompareTag(tag))
            {
                int damage = GetDamageFromCollider(other);
                TakeDamage(damage);
                break;
            }
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (_isDestroyed) return;
        
        foreach (string tag in _damageableTags)
        {
            if (collision.gameObject.CompareTag(tag))
            {
                int damage = GetDamageFromCollider(collision.collider);
                TakeDamage(damage);
                break;
            }
        }
    }
    
    private int GetDamageFromCollider(Collider other)
    {
        if (other.CompareTag("Bullet"))
            return 25;
        else if (other.CompareTag("Explosion"))
            return 75;
        else if (other.CompareTag("Weapon"))
            return 40;
        
        return 10;
    }
    
    public void TakeDamage(int damage)
    {
        if (_isDestroyed) return;
        
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0, _currentHealth);
        
        PlayHitEffect();
        OnDamaged?.Invoke();
        
        UpdateVisualDamage();
        
        if (_currentHealth <= 0)
        {
            DestroyBarrel();
        }
    }
    
    private void PlayHitEffect()
    {
        if (_damageParticles != null)
        {
            GameObject particles = Instantiate(_damageParticles, transform.position, transform.rotation);
            Destroy(particles, 2f);
        }
        
        if (_hitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    private void UpdateVisualDamage()
    {
        if (_renderer == null || _damagedMaterial == null) return;
        
        float healthPercentage = (float)_currentHealth / _maxHealth;
        
        if (healthPercentage < 0.5f && _renderer.material != _damagedMaterial)
        {
            _renderer.material = _damagedMaterial;
        }
    }
    
    private void DestroyBarrel()
    {
        if (_isDestroyed) return;
        _isDestroyed = true;
        
        OnDestroyed?.Invoke();
        
        if (_explodeOnDestruction)
        {
            CreateExplosion();
        }
        
        CreateDestructionEffects();
        SpawnDebris();
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        Destroy(gameObject, 1f);
    }
    
    private void CreateExplosion()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRadius, _explosionLayerMask);
        
        foreach (Collider nearbyObject in colliders)
        {
            if (nearbyObject == _collider) continue;
            
            Rigidbody rb = nearbyObject.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
            }
            
            DestructibleBarrel otherBarrel = nearbyObject.GetComponent<DestructibleBarrel>();
            if (otherBarrel != null && !otherBarrel._isDestroyed)
            {
                float distance = Vector3.Distance(transform.position, otherBarrel.transform.position);
                int damage = Mathf.RoundToInt(_explosionDamage * (1f - distance / _explosionRadius));
                otherBarrel.TakeDamage(damage);
            }
            
            if (nearbyObject.CompareTag("Player"))
            {
                // Apply damage to player if they have a method to receive damage
                nearbyObject.SendMessage("TakeDamage", _explosionDamage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    private void CreateDestructionEffects()
    {
        if (_destructionParticles != null)
        {
            GameObject particles = Instantiate(_destructionParticles, transform.position, transform.rotation);
            Destroy(particles, 5f);
        }
        
        if (_destructionSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_destructionSound);
        }
    }
    
    private void SpawnDebris()
    {
        if (_debrisPrefabs == null || _debrisPrefabs.Length == 0) return;
        
        for (int i = 0; i < _debrisCount; i++)
        {
            GameObject debrisPrefab = _debrisPrefabs[Random.Range(0, _debrisPrefabs.Length)];
            if (debrisPrefab == null) continue;
            
            Vector3 spawnPosition = transform.position + Random.insideUnitSphere * 0.5f;
            Quaternion spawnRotation = Random.rotation;
            
            GameObject debris = Instantiate(debrisPrefab, spawnPosition, spawnRotation);
            
            Rigidbody debrisRb = debris.GetComponent<Rigidbody>();
            if (debrisRb == null)
            {
                debrisRb = debris.AddComponent<Rigidbody>();
            }
            
            Vector3 explosionDirection = Random.insideUnitSphere;
            explosionDirection.y = Mathf.Abs(explosionDirection.y);
            debrisRb.AddForce(explosionDirection * _explosionForce * 0.5f);
            debrisRb.AddTorque(Random.insideUnitSphere * _explosionForce * 0.1f);
            
            Destroy(debris, _debrisLifetime);
        }
    }
    
    public void SetHealth(int health)
    {
        _currentHealth = Mathf.Clamp(health, 0, _maxHealth);
        UpdateVisualDamage();
    }
    
    public int GetCurrentHealth()
    {
        return _currentHealth;
    }
    
    public float GetHealthPercentage()
    {
        return (float)_currentHealth / _maxHealth;
    }
    
    void OnDrawGizmosSelected()
    {
        if (_explodeOnDestruction)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
    }
}