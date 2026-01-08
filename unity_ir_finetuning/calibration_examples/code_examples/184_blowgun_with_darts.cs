// Prompt: blowgun with darts
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Blowgun : MonoBehaviour
{
    [Header("Blowgun Settings")]
    [SerializeField] private GameObject _dartPrefab;
    [SerializeField] private Transform _firePoint;
    [SerializeField] private float _dartSpeed = 20f;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private int _maxAmmo = 10;
    [SerializeField] private float _reloadTime = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _shootSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioClip _emptySound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private Transform _blowgunModel;
    [SerializeField] private float _recoilForce = 0.1f;
    
    [Header("Events")]
    public UnityEvent OnShoot;
    public UnityEvent OnReload;
    public UnityEvent OnAmmoEmpty;
    
    private int _currentAmmo;
    private float _lastFireTime;
    private bool _isReloading;
    private AudioSource _audioSource;
    private Vector3 _originalPosition;
    
    private void Start()
    {
        _currentAmmo = _maxAmmo;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_blowgunModel != null)
            _originalPosition = _blowgunModel.localPosition;
            
        if (_firePoint == null)
            _firePoint = transform;
    }
    
    private void Update()
    {
        HandleInput();
        HandleRecoil();
    }
    
    private void HandleInput()
    {
        if (Input.GetButtonDown("Fire1") && CanShoot())
        {
            Shoot();
        }
        
        if (Input.GetKeyDown(KeyCode.R) && !_isReloading && _currentAmmo < _maxAmmo)
        {
            StartCoroutine(Reload());
        }
    }
    
    private bool CanShoot()
    {
        return !_isReloading && 
               _currentAmmo > 0 && 
               Time.time >= _lastFireTime + (1f / _fireRate);
    }
    
    private void Shoot()
    {
        if (_dartPrefab == null) return;
        
        _currentAmmo--;
        _lastFireTime = Time.time;
        
        GameObject dart = Instantiate(_dartPrefab, _firePoint.position, _firePoint.rotation);
        Dart dartComponent = dart.GetComponent<Dart>();
        if (dartComponent == null)
            dartComponent = dart.AddComponent<Dart>();
            
        dartComponent.Initialize(_dartSpeed);
        
        PlaySound(_shootSound);
        
        if (_muzzleFlash != null)
            _muzzleFlash.Play();
            
        ApplyRecoil();
        OnShoot?.Invoke();
        
        if (_currentAmmo <= 0)
        {
            OnAmmoEmpty?.Invoke();
        }
    }
    
    private void ApplyRecoil()
    {
        if (_blowgunModel != null)
        {
            _blowgunModel.localPosition = _originalPosition - transform.forward * _recoilForce;
        }
    }
    
    private void HandleRecoil()
    {
        if (_blowgunModel != null)
        {
            _blowgunModel.localPosition = Vector3.Lerp(_blowgunModel.localPosition, _originalPosition, Time.deltaTime * 10f);
        }
    }
    
    private IEnumerator Reload()
    {
        _isReloading = true;
        PlaySound(_reloadSound);
        OnReload?.Invoke();
        
        yield return new WaitForSeconds(_reloadTime);
        
        _currentAmmo = _maxAmmo;
        _isReloading = false;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void TryShoot()
    {
        if (CanShoot())
        {
            Shoot();
        }
        else if (_currentAmmo <= 0)
        {
            PlaySound(_emptySound);
        }
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

public class Dart : MonoBehaviour
{
    [Header("Dart Settings")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _lifetime = 5f;
    [SerializeField] private bool _stickToSurfaces = true;
    [SerializeField] private LayerMask _hitLayers = -1;
    
    [Header("Effects")]
    [SerializeField] private GameObject _hitEffect;
    [SerializeField] private AudioClip _hitSound;
    
    private Rigidbody _rigidbody;
    private bool _hasHit;
    private AudioSource _audioSource;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        Destroy(gameObject, _lifetime);
    }
    
    public void Initialize(float speed)
    {
        if (_rigidbody != null)
        {
            _rigidbody.velocity = transform.forward * speed;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        
        if (((1 << other.gameObject.layer) & _hitLayers) != 0)
        {
            HandleHit(other);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        
        if (((1 << collision.gameObject.layer) & _hitLayers) != 0)
        {
            HandleHit(collision.collider);
        }
    }
    
    private void HandleHit(Collider hitCollider)
    {
        _hasHit = true;
        
        if (hitCollider.CompareTag("Player"))
        {
            DealDamageToPlayer(hitCollider.gameObject);
        }
        else
        {
            DealDamageToTarget(hitCollider.gameObject);
        }
        
        if (_stickToSurfaces)
        {
            StickToSurface(hitCollider);
        }
        
        SpawnHitEffect();
        PlayHitSound();
        
        if (!_stickToSurfaces)
        {
            Destroy(gameObject, 0.1f);
        }
    }
    
    private void DealDamageToPlayer(GameObject player)
    {
        // Send damage message to player
        player.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
    }
    
    private void DealDamageToTarget(GameObject target)
    {
        // Try to find health component or send damage message
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        }
    }
    
    private void StickToSurface(Collider surface)
    {
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
        
        transform.SetParent(surface.transform);
        
        Destroy(gameObject, _lifetime - 1f);
    }
    
    private void SpawnHitEffect()
    {
        if (_hitEffect != null)
        {
            Instantiate(_hitEffect, transform.position, transform.rotation);
        }
    }
    
    private void PlayHitSound()
    {
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
}