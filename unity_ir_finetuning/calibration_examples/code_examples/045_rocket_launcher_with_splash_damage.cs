// Prompt: rocket launcher with splash damage
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class RocketLauncher : MonoBehaviour
{
    [Header("Rocket Settings")]
    [SerializeField] private GameObject _rocketPrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _rocketSpeed = 20f;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private float _reloadTime = 3f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _reloadSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;

    [Header("Events")]
    public UnityEvent<int> OnAmmoChanged;
    public UnityEvent OnReloadStarted;
    public UnityEvent OnReloadCompleted;

    private float _lastFireTime;
    private int _currentAmmo;
    private bool _isReloading;

    private void Start()
    {
        _currentAmmo = _maxAmmo;
        OnAmmoChanged?.Invoke(_currentAmmo);
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        if (Input.GetButtonDown("Fire1") && CanFire())
        {
            Fire();
        }

        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && _currentAmmo < _maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }

    private bool CanFire()
    {
        return !_isReloading && 
               _currentAmmo > 0 && 
               Time.time >= _lastFireTime + (1f / _fireRate);
    }

    private void Fire()
    {
        _lastFireTime = Time.time;
        _currentAmmo--;
        OnAmmoChanged?.Invoke(_currentAmmo);

        if (_rocketPrefab != null && _firePoint != null)
        {
            GameObject rocket = Instantiate(_rocketPrefab, _firePoint.position, _firePoint.rotation);
            Rocket rocketScript = rocket.GetComponent<Rocket>();
            if (rocketScript == null)
            {
                rocketScript = rocket.AddComponent<Rocket>();
            }
            rocketScript.Initialize(_rocketSpeed);
        }

        if (_muzzleFlash != null)
            _muzzleFlash.Play();

        if (_audioSource != null && _fireSound != null)
            _audioSource.PlayOneShot(_fireSound);

        if (_currentAmmo <= 0)
        {
            StartCoroutine(Reload());
        }
    }

    private IEnumerator Reload()
    {
        _isReloading = true;
        OnReloadStarted?.Invoke();

        if (_audioSource != null && _reloadSound != null)
            _audioSource.PlayOneShot(_reloadSound);

        yield return new WaitForSeconds(_reloadTime);

        _currentAmmo = _maxAmmo;
        _isReloading = false;
        OnAmmoChanged?.Invoke(_currentAmmo);
        OnReloadCompleted?.Invoke();
    }
}

public class Rocket : MonoBehaviour
{
    [Header("Rocket Properties")]
    [SerializeField] private float _speed = 20f;
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionDamage = 100f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private float _lifetime = 10f;

    [Header("Effects")]
    [SerializeField] private GameObject _explosionEffect;
    [SerializeField] private AudioClip _explosionSound;

    [Header("Layers")]
    [SerializeField] private LayerMask _damageableLayers = -1;

    private Rigidbody _rigidbody;
    private bool _hasExploded;

    public void Initialize(float speed)
    {
        _speed = speed;
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        _rigidbody.velocity = transform.forward * _speed;
        _rigidbody.useGravity = true;

        Destroy(gameObject, _lifetime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_hasExploded)
        {
            Explode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!_hasExploded)
        {
            Explode();
        }
    }

    private void Explode()
    {
        _hasExploded = true;

        // Create explosion effect
        if (_explosionEffect != null)
        {
            Instantiate(_explosionEffect, transform.position, Quaternion.identity);
        }

        // Play explosion sound
        if (_explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, transform.position);
        }

        // Apply splash damage
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRadius, _damageableLayers);
        
        foreach (Collider hit in colliders)
        {
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float damageMultiplier = 1f - (distance / _explosionRadius);
            damageMultiplier = Mathf.Clamp01(damageMultiplier);
            
            float finalDamage = _explosionDamage * damageMultiplier;

            // Apply damage to player
            if (hit.CompareTag("Player"))
            {
                PlayerDamageReceiver playerDamage = hit.GetComponent<PlayerDamageReceiver>();
                if (playerDamage != null)
                {
                    playerDamage.TakeDamage(finalDamage);
                }
            }

            // Apply damage to enemies
            EnemyDamageReceiver enemyDamage = hit.GetComponent<EnemyDamageReceiver>();
            if (enemyDamage != null)
            {
                enemyDamage.TakeDamage(finalDamage);
            }

            // Apply explosion force to rigidbodies
            Rigidbody hitRigidbody = hit.GetComponent<Rigidbody>();
            if (hitRigidbody != null)
            {
                hitRigidbody.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
            }
        }

        Destroy(gameObject);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
}

public class PlayerDamageReceiver : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    private void Start()
    {
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);

        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        _currentHealth += amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }
}

public class EnemyDamageReceiver : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 50f;
    [SerializeField] private float _currentHealth;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    private void Start()
    {
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);

        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
            Destroy(gameObject, 0.1f);
        }
    }
}