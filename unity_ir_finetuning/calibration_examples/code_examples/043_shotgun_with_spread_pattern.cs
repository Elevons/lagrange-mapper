// Prompt: shotgun with spread pattern
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class Shotgun : MonoBehaviour
{
    [Header("Shotgun Settings")]
    [SerializeField] private int _pelletsPerShot = 8;
    [SerializeField] private float _damage = 15f;
    [SerializeField] private float _range = 20f;
    [SerializeField] private float _spreadAngle = 30f;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private float _volume = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private LineRenderer _tracerPrefab;
    [SerializeField] private GameObject _impactEffect;
    [SerializeField] private float _tracerDuration = 0.1f;
    
    [Header("Ammo")]
    [SerializeField] private int _maxAmmo = 6;
    [SerializeField] private int _currentAmmo = 6;
    [SerializeField] private float _reloadTime = 2f;
    
    [Header("Events")]
    public UnityEvent<int> OnAmmoChanged;
    public UnityEvent OnFire;
    public UnityEvent OnReload;
    public UnityEvent OnEmpty;
    
    private Transform _firePoint;
    private AudioSource _audioSource;
    private float _lastFireTime;
    private bool _isReloading;
    private Camera _playerCamera;
    
    private void Start()
    {
        _firePoint = transform.Find("FirePoint");
        if (_firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.forward;
            _firePoint = firePointObj.transform;
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _playerCamera = Camera.main;
        if (_playerCamera == null)
        {
            _playerCamera = FindObjectOfType<Camera>();
        }
        
        OnAmmoChanged?.Invoke(_currentAmmo);
    }
    
    private void Update()
    {
        if (_isReloading) return;
        
        if (Input.GetButtonDown("Fire1"))
        {
            TryFire();
        }
        
        if (Input.GetKeyDown(KeyCode.R) && _currentAmmo < _maxAmmo)
        {
            StartReload();
        }
    }
    
    private void TryFire()
    {
        if (Time.time - _lastFireTime < 1f / _fireRate) return;
        
        if (_currentAmmo <= 0)
        {
            OnEmpty?.Invoke();
            return;
        }
        
        Fire();
        _lastFireTime = Time.time;
    }
    
    private void Fire()
    {
        _currentAmmo--;
        OnAmmoChanged?.Invoke(_currentAmmo);
        OnFire?.Invoke();
        
        PlayFireSound();
        ShowMuzzleFlash();
        
        Vector3 fireDirection = _firePoint.forward;
        if (_playerCamera != null)
        {
            fireDirection = _playerCamera.transform.forward;
        }
        
        for (int i = 0; i < _pelletsPerShot; i++)
        {
            Vector3 spreadDirection = ApplySpread(fireDirection);
            FirePellet(spreadDirection);
        }
    }
    
    private Vector3 ApplySpread(Vector3 baseDirection)
    {
        float halfSpread = _spreadAngle * 0.5f;
        float randomX = Random.Range(-halfSpread, halfSpread);
        float randomY = Random.Range(-halfSpread, halfSpread);
        
        Quaternion spreadRotation = Quaternion.Euler(randomX, randomY, 0);
        return spreadRotation * baseDirection;
    }
    
    private void FirePellet(Vector3 direction)
    {
        RaycastHit hit;
        Vector3 startPoint = _firePoint.position;
        
        if (Physics.Raycast(startPoint, direction, out hit, _range, _targetLayers))
        {
            ProcessHit(hit);
            CreateTracer(startPoint, hit.point);
            CreateImpactEffect(hit.point, hit.normal);
        }
        else
        {
            Vector3 endPoint = startPoint + direction * _range;
            CreateTracer(startPoint, endPoint);
        }
    }
    
    private void ProcessHit(RaycastHit hit)
    {
        Rigidbody hitRigidbody = hit.collider.GetComponent<Rigidbody>();
        if (hitRigidbody != null)
        {
            Vector3 force = hit.normal * -_damage * 10f;
            hitRigidbody.AddForceAtPosition(force, hit.point, ForceMode.Impulse);
        }
        
        IDamageable damageable = hit.collider.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
        }
        
        if (hit.collider.CompareTag("Enemy"))
        {
            Destroy(hit.collider.gameObject);
        }
    }
    
    private void CreateTracer(Vector3 start, Vector3 end)
    {
        if (_tracerPrefab == null) return;
        
        LineRenderer tracer = Instantiate(_tracerPrefab);
        tracer.positionCount = 2;
        tracer.SetPosition(0, start);
        tracer.SetPosition(1, end);
        
        Destroy(tracer.gameObject, _tracerDuration);
    }
    
    private void CreateImpactEffect(Vector3 position, Vector3 normal)
    {
        if (_impactEffect == null) return;
        
        GameObject effect = Instantiate(_impactEffect, position, Quaternion.LookRotation(normal));
        Destroy(effect, 2f);
    }
    
    private void PlayFireSound()
    {
        if (_fireSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_fireSound, _volume);
        }
    }
    
    private void ShowMuzzleFlash()
    {
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Play();
        }
    }
    
    private void StartReload()
    {
        if (_isReloading) return;
        
        _isReloading = true;
        OnReload?.Invoke();
        
        if (_reloadSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_reloadSound, _volume);
        }
        
        Invoke(nameof(CompleteReload), _reloadTime);
    }
    
    private void CompleteReload()
    {
        _currentAmmo = _maxAmmo;
        _isReloading = false;
        OnAmmoChanged?.Invoke(_currentAmmo);
    }
    
    public void AddAmmo(int amount)
    {
        _currentAmmo = Mathf.Min(_currentAmmo + amount, _maxAmmo);
        OnAmmoChanged?.Invoke(_currentAmmo);
    }
    
    public bool IsReloading => _isReloading;
    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _maxAmmo;
}

public interface IDamageable
{
    void TakeDamage(float damage);
}