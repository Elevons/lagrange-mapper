// Prompt: explosive crate
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ExplosiveCrate : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private float _upwardModifier = 3f;
    [SerializeField] private float _damage = 50f;
    [SerializeField] private LayerMask _explosionLayers = -1;
    
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private bool _explodeOnDestroy = true;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _explosionEffect;
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private float _shakeIntensity = 0.5f;
    [SerializeField] private float _shakeDuration = 0.3f;
    
    [Header("Chain Reaction")]
    [SerializeField] private bool _canChainReact = true;
    [SerializeField] private float _chainReactionDelay = 0.1f;
    
    [Header("Events")]
    public UnityEvent OnExplode;
    public UnityEvent<float> OnDamaged;
    
    private float _currentHealth;
    private bool _hasExploded = false;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Collider _collider;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        _audioSource = GetComponent<AudioSource>();
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasExploded) return;
        
        if (other.CompareTag("Player") || other.name.Contains("Projectile") || other.name.Contains("Bullet"))
        {
            TakeDamage(_maxHealth);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded) return;
        
        float impactForce = collision.relativeVelocity.magnitude;
        if (impactForce > 10f)
        {
            TakeDamage(impactForce * 2f);
        }
    }
    
    public void TakeDamage(float damageAmount)
    {
        if (_hasExploded) return;
        
        _currentHealth -= damageAmount;
        OnDamaged?.Invoke(damageAmount);
        
        if (_currentHealth <= 0f)
        {
            Explode();
        }
        else
        {
            StartCoroutine(DamageFlash());
        }
    }
    
    public void Explode()
    {
        if (_hasExploded) return;
        
        _hasExploded = true;
        
        // Create explosion effect
        if (_explosionEffect != null)
        {
            Instantiate(_explosionEffect, transform.position, Quaternion.identity);
        }
        
        // Play explosion sound
        if (_explosionSound != null && _audioSource != null)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, transform.position);
        }
        
        // Apply explosion force and damage
        ApplyExplosionEffects();
        
        // Trigger camera shake
        StartCoroutine(CameraShake());
        
        // Invoke event
        OnExplode?.Invoke();
        
        // Hide visual components
        if (_renderer != null) _renderer.enabled = false;
        if (_collider != null) _collider.enabled = false;
        
        // Destroy after a short delay to allow effects to play
        Destroy(gameObject, 2f);
    }
    
    private void ApplyExplosionEffects()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRadius, _explosionLayers);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit == _collider) continue;
            
            // Apply force to rigidbodies
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius, _upwardModifier);
            }
            
            // Damage other explosive crates for chain reactions
            if (_canChainReact)
            {
                ExplosiveCrate otherCrate = hit.GetComponent<ExplosiveCrate>();
                if (otherCrate != null && !otherCrate._hasExploded)
                {
                    StartCoroutine(ChainReactionDelay(otherCrate));
                }
            }
            
            // Apply damage to objects with health
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float damageMultiplier = Mathf.Clamp01(1f - (distance / _explosionRadius));
                damageable.TakeDamage(_damage * damageMultiplier);
            }
            
            // Damage player if hit
            if (hit.CompareTag("Player"))
            {
                // Send damage message to player
                hit.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }
    
    private System.Collections.IEnumerator ChainReactionDelay(ExplosiveCrate otherCrate)
    {
        yield return new WaitForSeconds(_chainReactionDelay);
        if (otherCrate != null)
        {
            otherCrate.Explode();
        }
    }
    
    private System.Collections.IEnumerator DamageFlash()
    {
        if (_renderer == null) yield break;
        
        Color originalColor = _renderer.material.color;
        _renderer.material.color = Color.red;
        
        yield return new WaitForSeconds(0.1f);
        
        if (_renderer != null)
        {
            _renderer.material.color = originalColor;
        }
    }
    
    private System.Collections.IEnumerator CameraShake()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) yield break;
        
        Vector3 originalPosition = mainCamera.transform.position;
        float elapsed = 0f;
        
        while (elapsed < _shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * _shakeIntensity;
            float y = Random.Range(-1f, 1f) * _shakeIntensity;
            
            mainCamera.transform.position = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        mainCamera.transform.position = originalPosition;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
    
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}