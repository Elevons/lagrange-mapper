// Prompt: crossbow with bolt travel time
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Crossbow : MonoBehaviour
{
    [Header("Crossbow Settings")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _boltPrefab;
    [SerializeField] private float _boltSpeed = 20f;
    [SerializeField] private float _reloadTime = 2f;
    [SerializeField] private bool _autoReload = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _reloadSound;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _fireAnimationTrigger = "Fire";
    [SerializeField] private string _reloadAnimationTrigger = "Reload";
    
    [Header("Events")]
    public UnityEvent OnFire;
    public UnityEvent OnReloadStart;
    public UnityEvent OnReloadComplete;
    
    private bool _isLoaded = true;
    private bool _isReloading = false;
    private float _reloadTimer = 0f;
    
    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_firePoint == null)
            _firePoint = transform;
    }
    
    private void Update()
    {
        HandleInput();
        HandleReloading();
    }
    
    private void HandleInput()
    {
        if (Input.GetButtonDown("Fire1"))
        {
            TryFire();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            StartReload();
        }
    }
    
    private void HandleReloading()
    {
        if (_isReloading)
        {
            _reloadTimer += Time.deltaTime;
            
            if (_reloadTimer >= _reloadTime)
            {
                CompleteReload();
            }
        }
    }
    
    public void TryFire()
    {
        if (!_isLoaded || _isReloading)
            return;
            
        Fire();
    }
    
    private void Fire()
    {
        if (_boltPrefab != null)
        {
            GameObject bolt = Instantiate(_boltPrefab, _firePoint.position, _firePoint.rotation);
            CrossbowBolt boltScript = bolt.GetComponent<CrossbowBolt>();
            
            if (boltScript == null)
                boltScript = bolt.AddComponent<CrossbowBolt>();
                
            boltScript.Initialize(_boltSpeed);
        }
        
        _isLoaded = false;
        
        PlayFireEffects();
        OnFire?.Invoke();
        
        if (_autoReload)
        {
            StartReload();
        }
    }
    
    private void PlayFireEffects()
    {
        if (_audioSource != null && _fireSound != null)
        {
            _audioSource.PlayOneShot(_fireSound);
        }
        
        if (_animator != null && !string.IsNullOrEmpty(_fireAnimationTrigger))
        {
            _animator.SetTrigger(_fireAnimationTrigger);
        }
    }
    
    public void StartReload()
    {
        if (_isLoaded || _isReloading)
            return;
            
        _isReloading = true;
        _reloadTimer = 0f;
        
        if (_audioSource != null && _reloadSound != null)
        {
            _audioSource.PlayOneShot(_reloadSound);
        }
        
        if (_animator != null && !string.IsNullOrEmpty(_reloadAnimationTrigger))
        {
            _animator.SetTrigger(_reloadAnimationTrigger);
        }
        
        OnReloadStart?.Invoke();
    }
    
    private void CompleteReload()
    {
        _isReloading = false;
        _isLoaded = true;
        _reloadTimer = 0f;
        
        OnReloadComplete?.Invoke();
    }
    
    public bool IsLoaded => _isLoaded;
    public bool IsReloading => _isReloading;
    public float ReloadProgress => _isReloading ? _reloadTimer / _reloadTime : 0f;
}

public class CrossbowBolt : MonoBehaviour
{
    [Header("Bolt Settings")]
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _lifetime = 10f;
    [SerializeField] private bool _stickToSurfaces = true;
    [SerializeField] private LayerMask _hitLayers = -1;
    
    [Header("Physics")]
    [SerializeField] private float _gravityScale = 1f;
    [SerializeField] private bool _useGravity = true;
    
    [Header("Effects")]
    [SerializeField] private GameObject _hitEffect;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private TrailRenderer _trail;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private bool _hasHit = false;
    private float _speed;
    private Vector3 _velocity;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        if (_collider == null)
            _collider = gameObject.AddComponent<CapsuleCollider>();
    }
    
    private void Start()
    {
        Destroy(gameObject, _lifetime);
    }
    
    private void FixedUpdate()
    {
        if (!_hasHit && _useGravity)
        {
            _rigidbody.AddForce(Physics.gravity * _gravityScale, ForceMode.Acceleration);
        }
        
        if (!_hasHit)
        {
            AlignWithVelocity();
        }
    }
    
    public void Initialize(float speed)
    {
        _speed = speed;
        _velocity = transform.forward * speed;
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = _velocity;
            _rigidbody.useGravity = _useGravity;
        }
    }
    
    private void AlignWithVelocity()
    {
        if (_rigidbody != null && _rigidbody.velocity.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(_rigidbody.velocity);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        HandleHit(other);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        HandleHit(collision.collider);
    }
    
    private void HandleHit(Collider hitCollider)
    {
        if (_hasHit)
            return;
            
        if ((_hitLayers.value & (1 << hitCollider.gameObject.layer)) == 0)
            return;
            
        _hasHit = true;
        
        // Deal damage to player
        if (hitCollider.CompareTag("Player"))
        {
            // Send damage message
            hitCollider.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        }
        
        // Stick to surface
        if (_stickToSurfaces)
        {
            StickToSurface(hitCollider);
        }
        
        // Play effects
        PlayHitEffects();
        
        // Destroy after sticking
        if (_stickToSurfaces)
        {
            Destroy(gameObject, 5f);
        }
        else
        {
            Destroy(gameObject, 0.1f);
        }
    }
    
    private void StickToSurface(Collider surface)
    {
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = true;
        }
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        // Parent to hit object if it's not static
        if (surface.attachedRigidbody != null)
        {
            transform.SetParent(surface.transform);
        }
        
        if (_trail != null)
        {
            _trail.enabled = false;
        }
    }
    
    private void PlayHitEffects()
    {
        if (_hitEffect != null)
        {
            Instantiate(_hitEffect, transform.position, transform.rotation);
        }
        
        if (_hitSound != null)
        {
            AudioSource.PlayClipAtPoint(_hitSound, transform.position);
        }
    }
}