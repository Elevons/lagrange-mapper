// Prompt: bow that charges for power shots
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Bow : MonoBehaviour
{
    [Header("Bow Settings")]
    [SerializeField] private float _maxChargeTime = 2f;
    [SerializeField] private float _minPower = 0.3f;
    [SerializeField] private float _maxPower = 1f;
    [SerializeField] private AnimationCurve _chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Arrow Settings")]
    [SerializeField] private GameObject _arrowPrefab;
    [SerializeField] private Transform _arrowSpawnPoint;
    [SerializeField] private float _baseArrowSpeed = 20f;
    [SerializeField] private float _maxArrowSpeed = 50f;
    
    [Header("Visual Feedback")]
    [SerializeField] private LineRenderer _trajectoryLine;
    [SerializeField] private int _trajectoryPoints = 30;
    [SerializeField] private float _trajectoryTimeStep = 0.1f;
    [SerializeField] private ParticleSystem _chargeEffect;
    [SerializeField] private Transform _bowString;
    [SerializeField] private float _maxStringPull = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _drawSound;
    [SerializeField] private AudioClip _releaseSound;
    [SerializeField] private AudioClip _chargeSound;
    
    [Header("Events")]
    public UnityEvent<float> OnChargeChanged;
    public UnityEvent<float> OnArrowFired;
    
    private bool _isCharging;
    private float _currentChargeTime;
    private float _currentPower;
    private Vector3 _originalStringPosition;
    private Camera _playerCamera;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_bowString != null)
            _originalStringPosition = _bowString.localPosition;
            
        if (_trajectoryLine != null)
        {
            _trajectoryLine.positionCount = _trajectoryPoints;
            _trajectoryLine.enabled = false;
        }
        
        if (_chargeEffect != null)
            _chargeEffect.Stop();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCharging();
        UpdateVisuals();
    }
    
    private void HandleInput()
    {
        bool drawInput = Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);
        bool releaseInput = Input.GetMouseButtonUp(0) || Input.GetKeyUp(KeyCode.Space);
        
        if (drawInput && !_isCharging && CanFire())
        {
            StartCharging();
        }
        else if (releaseInput && _isCharging)
        {
            FireArrow();
        }
    }
    
    private bool CanFire()
    {
        return _arrowPrefab != null && _arrowSpawnPoint != null;
    }
    
    private void StartCharging()
    {
        _isCharging = true;
        _currentChargeTime = 0f;
        
        if (_chargeEffect != null)
            _chargeEffect.Play();
            
        if (_trajectoryLine != null)
            _trajectoryLine.enabled = true;
            
        PlaySound(_drawSound);
    }
    
    private void UpdateCharging()
    {
        if (!_isCharging) return;
        
        _currentChargeTime += Time.deltaTime;
        float chargeProgress = Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
        _currentPower = Mathf.Lerp(_minPower, _maxPower, _chargeCurve.Evaluate(chargeProgress));
        
        OnChargeChanged?.Invoke(_currentPower);
        
        if (_chargeSound != null && _audioSource != null && !_audioSource.isPlaying)
        {
            _audioSource.clip = _chargeSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void UpdateVisuals()
    {
        if (_isCharging)
        {
            UpdateBowString();
            UpdateTrajectory();
            UpdateChargeEffect();
        }
    }
    
    private void UpdateBowString()
    {
        if (_bowString == null) return;
        
        float pullAmount = _currentPower * _maxStringPull;
        Vector3 pullDirection = -transform.forward;
        _bowString.localPosition = _originalStringPosition + pullDirection * pullAmount;
    }
    
    private void UpdateTrajectory()
    {
        if (_trajectoryLine == null || _arrowSpawnPoint == null) return;
        
        Vector3 velocity = GetArrowVelocity();
        Vector3 startPos = _arrowSpawnPoint.position;
        
        for (int i = 0; i < _trajectoryPoints; i++)
        {
            float time = i * _trajectoryTimeStep;
            Vector3 point = startPos + velocity * time + 0.5f * Physics.gravity * time * time;
            _trajectoryLine.SetPosition(i, point);
        }
    }
    
    private void UpdateChargeEffect()
    {
        if (_chargeEffect == null) return;
        
        var main = _chargeEffect.main;
        main.startLifetime = _currentPower * 2f;
        
        var emission = _chargeEffect.emission;
        emission.rateOverTime = _currentPower * 50f;
    }
    
    private Vector3 GetArrowVelocity()
    {
        Vector3 direction = GetAimDirection();
        float speed = Mathf.Lerp(_baseArrowSpeed, _maxArrowSpeed, _currentPower);
        return direction * speed;
    }
    
    private Vector3 GetAimDirection()
    {
        if (_playerCamera != null)
        {
            Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
            return ray.direction;
        }
        
        return transform.forward;
    }
    
    private void FireArrow()
    {
        if (_arrowPrefab == null || _arrowSpawnPoint == null) return;
        
        GameObject arrow = Instantiate(_arrowPrefab, _arrowSpawnPoint.position, _arrowSpawnPoint.rotation);
        
        Rigidbody arrowRb = arrow.GetComponent<Rigidbody>();
        if (arrowRb == null)
            arrowRb = arrow.AddComponent<Rigidbody>();
            
        Vector3 velocity = GetArrowVelocity();
        arrowRb.velocity = velocity;
        
        Arrow arrowScript = arrow.GetComponent<Arrow>();
        if (arrowScript == null)
            arrowScript = arrow.AddComponent<Arrow>();
            
        arrowScript.Initialize(_currentPower);
        
        OnArrowFired?.Invoke(_currentPower);
        PlaySound(_releaseSound);
        ResetBow();
    }
    
    private void ResetBow()
    {
        _isCharging = false;
        _currentChargeTime = 0f;
        _currentPower = 0f;
        
        if (_bowString != null)
            _bowString.localPosition = _originalStringPosition;
            
        if (_trajectoryLine != null)
            _trajectoryLine.enabled = false;
            
        if (_chargeEffect != null)
            _chargeEffect.Stop();
            
        if (_audioSource != null && _audioSource.isPlaying && _audioSource.clip == _chargeSound)
        {
            _audioSource.Stop();
            _audioSource.loop = false;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.loop = false;
            _audioSource.Play();
        }
    }
    
    public float GetCurrentPower()
    {
        return _currentPower;
    }
    
    public bool IsCharging()
    {
        return _isCharging;
    }
}

public class Arrow : MonoBehaviour
{
    [Header("Arrow Settings")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _lifetime = 10f;
    [SerializeField] private bool _stickToSurfaces = true;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _hitEffect;
    [SerializeField] private AudioClip _hitSound;
    
    private float _powerMultiplier = 1f;
    private bool _hasHit;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        Destroy(gameObject, _lifetime);
    }
    
    public void Initialize(float powerMultiplier)
    {
        _powerMultiplier = powerMultiplier;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        
        HandleHit(other);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        
        HandleHit(collision.collider);
    }
    
    private void HandleHit(Collider hitCollider)
    {
        _hasHit = true;
        
        if (hitCollider.CompareTag("Player"))
            return;
            
        float finalDamage = _damage * _powerMultiplier;
        
        if (hitCollider.CompareTag("Enemy"))
        {
            var enemyHealth = hitCollider.GetComponent<EnemyHealth>();
            if (enemyHealth != null)
                enemyHealth.TakeDamage(finalDamage);
        }
        
        if (_stickToSurfaces)
        {
            if (_rigidbody != null)
            {
                _rigidbody.isKinematic = true;
                _rigidbody.velocity = Vector3.zero;
            }
            
            transform.SetParent(hitCollider.transform);
        }
        
        if (_hitEffect != null)
        {
            Instantiate(_hitEffect, transform.position, transform.rotation);
        }
        
        if (_hitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
        
        if (!_stickToSurfaces)
        {
            Destroy(gameObject, 0.1f);
        }
    }
}

public class EnemyHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _deathEffect;
    [SerializeField] private AudioClip _deathSound;
    
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    
    private AudioSource _audioSource;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0f, _currentHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
        
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }
    
    private void Die()
    {
        OnDeath?.Invoke();
        
        if (_deathEffect != null)
        {
            Instantiate(_deathEffect, transform.position, transform.rotation);
        }
        
        if (_deathSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deathSound);
            Destroy(gameObject, _deathSound.length);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}