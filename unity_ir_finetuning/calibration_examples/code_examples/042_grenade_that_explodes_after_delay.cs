// Prompt: grenade that explodes after delay
// Type: general

using UnityEngine;
using System.Collections;

public class Grenade : MonoBehaviour
{
    [Header("Explosion Settings")]
    [SerializeField] private float _explosionDelay = 3f;
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private float _damage = 100f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField] private ParticleSystem _fuseParticles;
    [SerializeField] private Light _fuseLight;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _fuseSound;
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Physics")]
    [SerializeField] private float _bounceForce = 0.3f;
    [SerializeField] private int _maxBounces = 3;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private bool _hasExploded = false;
    private bool _fuseStarted = false;
    private int _bounceCount = 0;
    private float _fuseTimer = 0f;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Start()
    {
        StartFuse();
    }
    
    private void Update()
    {
        if (_fuseStarted && !_hasExploded)
        {
            _fuseTimer += Time.deltaTime;
            
            UpdateFuseEffects();
            
            if (_fuseTimer >= _explosionDelay)
            {
                Explode();
            }
        }
    }
    
    private void StartFuse()
    {
        _fuseStarted = true;
        _fuseTimer = 0f;
        
        if (_fuseParticles != null)
            _fuseParticles.Play();
            
        if (_fuseLight != null)
            _fuseLight.enabled = true;
            
        if (_audioSource != null && _fuseSound != null)
        {
            _audioSource.clip = _fuseSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void UpdateFuseEffects()
    {
        float timeRemaining = _explosionDelay - _fuseTimer;
        float normalizedTime = 1f - (timeRemaining / _explosionDelay);
        
        if (_fuseLight != null)
        {
            _fuseLight.intensity = Mathf.Lerp(0.5f, 2f, normalizedTime);
            
            if (timeRemaining < 1f)
            {
                _fuseLight.intensity *= Mathf.Sin(Time.time * 20f) * 0.5f + 1f;
            }
        }
        
        if (_fuseParticles != null && timeRemaining < 1f)
        {
            var emission = _fuseParticles.emission;
            emission.rateOverTime = Mathf.Lerp(10f, 50f, normalizedTime);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded || _bounceCount >= _maxBounces)
            return;
            
        _bounceCount++;
        
        Vector3 bounceDirection = Vector3.Reflect(_rigidbody.velocity.normalized, collision.contacts[0].normal);
        _rigidbody.velocity = bounceDirection * _bounceForce * _rigidbody.velocity.magnitude;
        
        if (_audioSource != null && !_audioSource.isPlaying)
        {
            _audioSource.pitch = Random.Range(0.8f, 1.2f);
            _audioSource.PlayOneShot(_fuseSound, 0.3f);
        }
    }
    
    private void Explode()
    {
        if (_hasExploded)
            return;
            
        _hasExploded = true;
        
        CreateExplosionEffects();
        ApplyExplosionDamage();
        ApplyExplosionForce();
        
        Destroy(gameObject, 0.1f);
    }
    
    private void CreateExplosionEffects()
    {
        if (_explosionPrefab != null)
        {
            GameObject explosion = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 5f);
        }
        
        if (_audioSource != null && _explosionSound != null)
        {
            _audioSource.Stop();
            _audioSource.loop = false;
            _audioSource.clip = _explosionSound;
            _audioSource.volume = 1f;
            _audioSource.Play();
        }
        
        if (_fuseParticles != null)
            _fuseParticles.Stop();
            
        if (_fuseLight != null)
            _fuseLight.enabled = false;
            
        if (_collider != null)
            _collider.enabled = false;
            
        GetComponent<Renderer>().enabled = false;
    }
    
    private void ApplyExplosionDamage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRadius, _affectedLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider == _collider)
                continue;
                
            float distance = Vector3.Distance(transform.position, hitCollider.transform.position);
            float damageMultiplier = 1f - (distance / _explosionRadius);
            float finalDamage = _damage * damageMultiplier;
            
            if (hitCollider.CompareTag("Player"))
            {
                ApplyDamageToTarget(hitCollider.gameObject, finalDamage);
            }
            else if (hitCollider.CompareTag("Enemy"))
            {
                ApplyDamageToTarget(hitCollider.gameObject, finalDamage);
            }
            
            Destructible destructible = hitCollider.GetComponent<Destructible>();
            if (destructible != null)
            {
                destructible.TakeDamage(finalDamage);
            }
        }
    }
    
    private void ApplyDamageToTarget(GameObject target, float damage)
    {
        target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        
        CharacterController controller = target.GetComponent<CharacterController>();
        if (controller != null)
        {
            target.SendMessage("OnExplosionHit", damage, SendMessageOptions.DontRequireReceiver);
        }
    }
    
    private void ApplyExplosionForce()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRadius, _affectedLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider == _collider)
                continue;
                
            Rigidbody hitRigidbody = hitCollider.GetComponent<Rigidbody>();
            if (hitRigidbody != null)
            {
                hitRigidbody.AddExplosionForce(_explosionForce, transform.position, _explosionRadius, 3f);
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius * 0.5f);
    }
    
    public void SetExplosionDelay(float delay)
    {
        _explosionDelay = delay;
    }
    
    public void SetExplosionRadius(float radius)
    {
        _explosionRadius = radius;
    }
    
    public void SetDamage(float damage)
    {
        _damage = damage;
    }
    
    public void ExplodeImmediately()
    {
        _fuseTimer = _explosionDelay;
    }
}

public class Destructible : MonoBehaviour
{
    [Header("Destructible Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private GameObject _destroyedPrefab;
    
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
            DestroyObject();
        }
    }
    
    private void DestroyObject()
    {
        if (_destroyedPrefab != null)
        {
            Instantiate(_destroyedPrefab, transform.position, transform.rotation);
        }
        
        Destroy(gameObject);
    }
}