// Prompt: enemy that explodes on death
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class ExplodingEnemy : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    private float _currentHealth;
    
    [Header("Explosion Settings")]
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionForce = 500f;
    [SerializeField] private float _explosionDamage = 50f;
    [SerializeField] private LayerMask _explosionLayers = -1;
    [SerializeField] private GameObject _explosionEffectPrefab;
    [SerializeField] private AudioClip _explosionSound;
    
    [Header("Visual Settings")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private Color _damageColor = Color.red;
    [SerializeField] private float _damageFlashDuration = 0.1f;
    
    [Header("Events")]
    public UnityEvent OnDeath;
    public UnityEvent OnTakeDamage;
    
    private Color _originalColor;
    private bool _isDead = false;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();
            
        if (_renderer != null)
            _originalColor = _renderer.material.color;
            
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        OnTakeDamage?.Invoke();
        
        if (_renderer != null)
            StartCoroutine(FlashDamage());
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    private System.Collections.IEnumerator FlashDamage()
    {
        if (_renderer != null)
        {
            _renderer.material.color = _damageColor;
            yield return new WaitForSeconds(_damageFlashDuration);
            _renderer.material.color = _originalColor;
        }
    }
    
    private void Die()
    {
        if (_isDead) return;
        
        _isDead = true;
        OnDeath?.Invoke();
        
        Explode();
        
        Destroy(gameObject);
    }
    
    private void Explode()
    {
        Vector3 explosionPosition = transform.position;
        
        // Create explosion effect
        if (_explosionEffectPrefab != null)
        {
            GameObject effect = Instantiate(_explosionEffectPrefab, explosionPosition, Quaternion.identity);
            Destroy(effect, 5f);
        }
        
        // Play explosion sound
        if (_explosionSound != null && _audioSource != null)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, explosionPosition);
        }
        
        // Find all colliders in explosion radius
        Collider[] colliders = Physics.OverlapSphere(explosionPosition, _explosionRadius, _explosionLayers);
        
        foreach (Collider hit in colliders)
        {
            if (hit.gameObject == gameObject) continue;
            
            // Apply explosion force
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, explosionPosition, _explosionRadius);
            }
            
            // Apply damage to other exploding enemies
            ExplodingEnemy otherEnemy = hit.GetComponent<ExplodingEnemy>();
            if (otherEnemy != null)
            {
                float distance = Vector3.Distance(explosionPosition, hit.transform.position);
                float damageMultiplier = 1f - (distance / _explosionRadius);
                otherEnemy.TakeDamage(_explosionDamage * damageMultiplier);
            }
            
            // Damage player
            if (hit.CompareTag("Player"))
            {
                float distance = Vector3.Distance(explosionPosition, hit.transform.position);
                float damageMultiplier = 1f - (distance / _explosionRadius);
                float finalDamage = _explosionDamage * damageMultiplier;
                
                // Send damage message if player has a method to receive it
                hit.SendMessage("TakeDamage", finalDamage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Optional: Explode on contact with player
            // Die();
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Projectile"))
        {
            TakeDamage(25f);
            Destroy(collision.gameObject);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
    
    public bool IsDead()
    {
        return _isDead;
    }
}