// Prompt: staff with magic projectile
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class MagicStaff : MonoBehaviour
{
    [Header("Staff Settings")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _projectileSpeed = 10f;
    [SerializeField] private int _maxAmmo = 30;
    [SerializeField] private float _reloadTime = 2f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioClip _emptySound;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _fireAnimationTrigger = "Fire";
    [SerializeField] private string _reloadAnimationTrigger = "Reload";
    
    [Header("Events")]
    public UnityEvent<int> OnAmmoChanged;
    public UnityEvent OnReloadStarted;
    public UnityEvent OnReloadCompleted;
    public UnityEvent OnProjectileFired;
    
    private float _lastFireTime;
    private int _currentAmmo;
    private bool _isReloading;
    private Camera _playerCamera;
    
    private void Start()
    {
        _currentAmmo = _maxAmmo;
        _playerCamera = Camera.main;
        
        if (_firePoint == null)
            _firePoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        OnAmmoChanged?.Invoke(_currentAmmo);
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        if (_isReloading)
            return;
            
        if (Input.GetButtonDown("Fire1"))
        {
            TryFire();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartReload();
        }
    }
    
    private void TryFire()
    {
        if (Time.time - _lastFireTime < 1f / _fireRate)
            return;
            
        if (_currentAmmo <= 0)
        {
            PlayEmptySound();
            return;
        }
        
        Fire();
    }
    
    private void Fire()
    {
        _lastFireTime = Time.time;
        _currentAmmo--;
        
        Vector3 fireDirection = GetFireDirection();
        CreateProjectile(fireDirection);
        
        PlayFireEffects();
        OnAmmoChanged?.Invoke(_currentAmmo);
        OnProjectileFired?.Invoke();
        
        if (_currentAmmo <= 0)
        {
            StartReload();
        }
    }
    
    private Vector3 GetFireDirection()
    {
        if (_playerCamera != null)
        {
            Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
            return ray.direction;
        }
        
        return _firePoint.forward;
    }
    
    private void CreateProjectile(Vector3 direction)
    {
        if (_projectilePrefab == null)
            return;
            
        GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, Quaternion.LookRotation(direction));
        
        Rigidbody rb = projectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = direction * _projectileSpeed;
        }
        
        MagicProjectile projectileScript = projectile.GetComponent<MagicProjectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<MagicProjectile>();
        }
    }
    
    private void PlayFireEffects()
    {
        if (_muzzleFlash != null)
            _muzzleFlash.Play();
            
        if (_audioSource != null && _fireSound != null)
            _audioSource.PlayOneShot(_fireSound);
            
        if (_animator != null)
            _animator.SetTrigger(_fireAnimationTrigger);
    }
    
    private void PlayEmptySound()
    {
        if (_audioSource != null && _emptySound != null)
            _audioSource.PlayOneShot(_emptySound);
    }
    
    private void StartReload()
    {
        if (_isReloading || _currentAmmo >= _maxAmmo)
            return;
            
        _isReloading = true;
        OnReloadStarted?.Invoke();
        
        if (_audioSource != null && _reloadSound != null)
            _audioSource.PlayOneShot(_reloadSound);
            
        if (_animator != null)
            _animator.SetTrigger(_reloadAnimationTrigger);
            
        Invoke(nameof(CompleteReload), _reloadTime);
    }
    
    private void CompleteReload()
    {
        _currentAmmo = _maxAmmo;
        _isReloading = false;
        OnAmmoChanged?.Invoke(_currentAmmo);
        OnReloadCompleted?.Invoke();
    }
    
    public bool CanFire()
    {
        return !_isReloading && _currentAmmo > 0 && Time.time - _lastFireTime >= 1f / _fireRate;
    }
    
    public int GetCurrentAmmo()
    {
        return _currentAmmo;
    }
    
    public int GetMaxAmmo()
    {
        return _maxAmmo;
    }
    
    public bool IsReloading()
    {
        return _isReloading;
    }
}

public class MagicProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _lifetime = 5f;
    [SerializeField] private float _explosionRadius = 3f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Effects")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private GameObject _explosionEffectPrefab;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private TrailRenderer _trail;
    
    private bool _hasHit;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        Destroy(gameObject, _lifetime);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit)
            return;
            
        if (other.CompareTag("Player"))
            return;
            
        Hit(other);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit)
            return;
            
        if (collision.gameObject.CompareTag("Player"))
            return;
            
        Hit(collision.collider);
    }
    
    private void Hit(Collider hitCollider)
    {
        _hasHit = true;
        
        Vector3 hitPoint = transform.position;
        
        if (_explosionRadius > 0)
        {
            ExplodeAtPoint(hitPoint);
        }
        else
        {
            DealDamageToTarget(hitCollider.gameObject);
        }
        
        CreateHitEffects(hitPoint);
        DestroyProjectile();
    }
    
    private void ExplodeAtPoint(Vector3 explosionPoint)
    {
        Collider[] hitColliders = Physics.OverlapSphere(explosionPoint, _explosionRadius, _targetLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.CompareTag("Player"))
                continue;
                
            DealDamageToTarget(hitCollider.gameObject);
            
            Rigidbody rb = hitCollider.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 direction = (hitCollider.transform.position - explosionPoint).normalized;
                float distance = Vector3.Distance(explosionPoint, hitCollider.transform.position);
                float force = Mathf.Lerp(500f, 100f, distance / _explosionRadius);
                rb.AddForce(direction * force);
            }
        }
        
        if (_explosionEffectPrefab != null)
        {
            Instantiate(_explosionEffectPrefab, explosionPoint, Quaternion.identity);
        }
        
        if (_audioSource != null && _explosionSound != null)
        {
            _audioSource.PlayOneShot(_explosionSound);
        }
    }
    
    private void DealDamageToTarget(GameObject target)
    {
        if (target.CompareTag("Enemy"))
        {
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        }
        
        if (target.GetComponent<Rigidbody>() != null)
        {
            Vector3 force = transform.forward * 300f;
            target.GetComponent<Rigidbody>().AddForce(force);
        }
    }
    
    private void CreateHitEffects(Vector3 position)
    {
        if (_hitEffectPrefab != null)
        {
            Instantiate(_hitEffectPrefab, position, Quaternion.identity);
        }
        
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    private void DestroyProjectile()
    {
        if (_trail != null)
            _trail.enabled = false;
            
        GetComponent<Collider>().enabled = false;
        
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.velocity = Vector3.zero;
            
        Destroy(gameObject, 2f);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_explosionRadius > 0)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        }
    }
}