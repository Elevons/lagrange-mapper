// Prompt: gun that fires projectiles
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class Gun : MonoBehaviour
{
    [System.Serializable]
    public class Projectile : MonoBehaviour
    {
        [Header("Projectile Settings")]
        [SerializeField] private float _speed = 10f;
        [SerializeField] private float _lifetime = 5f;
        [SerializeField] private int _damage = 10;
        [SerializeField] private LayerMask _hitLayers = -1;
        
        private Rigidbody _rigidbody;
        private bool _hasHit = false;
        
        public UnityEvent<GameObject> OnHit;
        
        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                
            _rigidbody.useGravity = false;
            Destroy(gameObject, _lifetime);
        }
        
        private void Start()
        {
            _rigidbody.velocity = transform.forward * _speed;
        }
        
        private void OnTriggerEnter(Collider other)
        {
            if (_hasHit) return;
            
            if ((_hitLayers.value & (1 << other.gameObject.layer)) != 0)
            {
                _hasHit = true;
                OnHit?.Invoke(other.gameObject);
                
                if (other.CompareTag("Player"))
                {
                    Debug.Log($"Player hit for {_damage} damage!");
                }
                
                Destroy(gameObject);
            }
        }
    }
    
    [Header("Gun Settings")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private bool _autoFire = false;
    [SerializeField] private KeyCode _fireKey = KeyCode.Space;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fireSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private GameObject _muzzleFlashEffect;
    
    [Header("Ammo")]
    [SerializeField] private int _maxAmmo = 30;
    [SerializeField] private int _currentAmmo;
    [SerializeField] private float _reloadTime = 2f;
    
    private float _nextFireTime = 0f;
    private bool _isReloading = false;
    
    public UnityEvent OnFire;
    public UnityEvent OnReload;
    public UnityEvent OnAmmoEmpty;
    
    private void Start()
    {
        _currentAmmo = _maxAmmo;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_firePoint == null)
            _firePoint = transform;
            
        if (_projectilePrefab == null)
        {
            _projectilePrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _projectilePrefab.transform.localScale = Vector3.one * 0.1f;
            _projectilePrefab.AddComponent<Projectile>();
            
            Collider collider = _projectilePrefab.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (_isReloading) return;
        
        bool shouldFire = false;
        
        if (_autoFire)
        {
            shouldFire = Input.GetKey(_fireKey);
        }
        else
        {
            shouldFire = Input.GetKeyDown(_fireKey);
        }
        
        if (shouldFire && CanFire())
        {
            Fire();
        }
        
        if (Input.GetKeyDown(KeyCode.R) && _currentAmmo < _maxAmmo)
        {
            StartReload();
        }
    }
    
    private bool CanFire()
    {
        return Time.time >= _nextFireTime && _currentAmmo > 0 && !_isReloading;
    }
    
    public void Fire()
    {
        if (!CanFire()) return;
        
        _nextFireTime = Time.time + (1f / _fireRate);
        _currentAmmo--;
        
        GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
        
        Projectile projectileScript = projectile.GetComponent<Projectile>();
        if (projectileScript != null)
        {
            projectileScript.OnHit.AddListener(OnProjectileHit);
        }
        
        PlayFireEffects();
        OnFire?.Invoke();
        
        if (_currentAmmo <= 0)
        {
            OnAmmoEmpty?.Invoke();
        }
    }
    
    private void PlayFireEffects()
    {
        if (_audioSource != null && _fireSound != null)
        {
            _audioSource.PlayOneShot(_fireSound);
        }
        
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Play();
        }
        
        if (_muzzleFlashEffect != null)
        {
            GameObject flash = Instantiate(_muzzleFlashEffect, _firePoint.position, _firePoint.rotation);
            Destroy(flash, 0.1f);
        }
    }
    
    private void OnProjectileHit(GameObject hitObject)
    {
        Debug.Log($"Projectile hit: {hitObject.name}");
    }
    
    public void StartReload()
    {
        if (_isReloading || _currentAmmo >= _maxAmmo) return;
        
        _isReloading = true;
        OnReload?.Invoke();
        Invoke(nameof(CompleteReload), _reloadTime);
    }
    
    private void CompleteReload()
    {
        _currentAmmo = _maxAmmo;
        _isReloading = false;
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
    
    public void AddAmmo(int amount)
    {
        _currentAmmo = Mathf.Min(_currentAmmo + amount, _maxAmmo);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_firePoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_firePoint.position, 0.1f);
            Gizmos.DrawRay(_firePoint.position, _firePoint.forward * 2f);
        }
    }
}