// Prompt: laser weapon with continuous beam
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class LaserWeapon : MonoBehaviour
{
    [Header("Laser Configuration")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private LineRenderer _laserLine;
    [SerializeField] private ParticleSystem _muzzleEffect;
    [SerializeField] private ParticleSystem _hitEffect;
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private float _maxRange = 100f;
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _damageInterval = 0.1f;
    
    [Header("Visual Effects")]
    [SerializeField] private Color _laserColor = Color.red;
    [SerializeField] private float _laserWidth = 0.1f;
    [SerializeField] private Material _laserMaterial;
    [SerializeField] private Light _laserLight;
    [SerializeField] private float _lightIntensity = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fireSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Input")]
    [SerializeField] private KeyCode _fireKey = KeyCode.Mouse0;
    [SerializeField] private bool _useInput = true;
    
    [Header("Events")]
    public UnityEvent OnLaserStart;
    public UnityEvent OnLaserStop;
    public UnityEvent<Vector3> OnLaserHit;
    
    private bool _isFiring;
    private float _lastDamageTime;
    private RaycastHit _currentHit;
    private GameObject _currentTarget;
    private TargetHealth _currentTargetHealth;
    
    [System.Serializable]
    public class TargetHealth
    {
        public float maxHealth = 100f;
        public float currentHealth;
        public bool isDead;
        
        public TargetHealth(float health)
        {
            maxHealth = health;
            currentHealth = health;
            isDead = false;
        }
        
        public void TakeDamage(float damage)
        {
            if (isDead) return;
            
            currentHealth -= damage;
            if (currentHealth <= 0)
            {
                currentHealth = 0;
                isDead = true;
            }
        }
    }
    
    private void Start()
    {
        InitializeLaser();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateLaser();
    }
    
    private void InitializeLaser()
    {
        if (_laserLine == null)
        {
            _laserLine = GetComponent<LineRenderer>();
            if (_laserLine == null)
            {
                _laserLine = gameObject.AddComponent<LineRenderer>();
            }
        }
        
        if (_firePoint == null)
            _firePoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetupLineRenderer();
        SetupLight();
        
        _laserLine.enabled = false;
        if (_laserLight != null)
            _laserLight.enabled = false;
    }
    
    private void SetupLineRenderer()
    {
        _laserLine.positionCount = 2;
        _laserLine.startWidth = _laserWidth;
        _laserLine.endWidth = _laserWidth;
        _laserLine.color = _laserColor;
        _laserLine.useWorldSpace = true;
        
        if (_laserMaterial != null)
            _laserLine.material = _laserMaterial;
    }
    
    private void SetupLight()
    {
        if (_laserLight == null)
        {
            GameObject lightObj = new GameObject("LaserLight");
            lightObj.transform.SetParent(_firePoint);
            lightObj.transform.localPosition = Vector3.zero;
            _laserLight = lightObj.AddComponent<Light>();
        }
        
        _laserLight.type = LightType.Spot;
        _laserLight.color = _laserColor;
        _laserLight.intensity = _lightIntensity;
        _laserLight.range = _maxRange;
        _laserLight.spotAngle = 30f;
    }
    
    private void HandleInput()
    {
        if (!_useInput) return;
        
        if (Input.GetKeyDown(_fireKey))
        {
            StartFiring();
        }
        else if (Input.GetKeyUp(_fireKey))
        {
            StopFiring();
        }
    }
    
    public void StartFiring()
    {
        if (_isFiring) return;
        
        _isFiring = true;
        _laserLine.enabled = true;
        
        if (_laserLight != null)
            _laserLight.enabled = true;
            
        if (_muzzleEffect != null)
            _muzzleEffect.Play();
            
        PlayFireSound();
        OnLaserStart?.Invoke();
    }
    
    public void StopFiring()
    {
        if (!_isFiring) return;
        
        _isFiring = false;
        _laserLine.enabled = false;
        
        if (_laserLight != null)
            _laserLight.enabled = false;
            
        if (_muzzleEffect != null)
            _muzzleEffect.Stop();
            
        if (_hitEffect != null)
            _hitEffect.Stop();
            
        StopFireSound();
        _currentTarget = null;
        _currentTargetHealth = null;
        OnLaserStop?.Invoke();
    }
    
    private void UpdateLaser()
    {
        if (!_isFiring) return;
        
        Vector3 startPos = _firePoint.position;
        Vector3 direction = _firePoint.forward;
        Vector3 endPos = startPos + direction * _maxRange;
        
        if (Physics.Raycast(startPos, direction, out _currentHit, _maxRange, _targetLayers))
        {
            endPos = _currentHit.point;
            HandleHit();
        }
        else
        {
            _currentTarget = null;
            _currentTargetHealth = null;
            if (_hitEffect != null)
                _hitEffect.Stop();
        }
        
        _laserLine.SetPosition(0, startPos);
        _laserLine.SetPosition(1, endPos);
        
        if (_laserLight != null)
        {
            _laserLight.transform.position = startPos;
            _laserLight.transform.LookAt(endPos);
        }
    }
    
    private void HandleHit()
    {
        if (_currentHit.collider == null) return;
        
        // Visual effects
        if (_hitEffect != null)
        {
            _hitEffect.transform.position = _currentHit.point;
            _hitEffect.transform.LookAt(_currentHit.point + _currentHit.normal);
            if (!_hitEffect.isPlaying)
                _hitEffect.Play();
        }
        
        // Damage handling
        if (Time.time >= _lastDamageTime + _damageInterval)
        {
            ApplyDamage(_currentHit.collider.gameObject);
            _lastDamageTime = Time.time;
            OnLaserHit?.Invoke(_currentHit.point);
        }
    }
    
    private void ApplyDamage(GameObject target)
    {
        if (target.CompareTag("Player"))
        {
            // Handle player damage
            HandlePlayerDamage(target);
        }
        else if (target.CompareTag("Enemy"))
        {
            // Handle enemy damage
            HandleEnemyDamage(target);
        }
        else
        {
            // Handle other objects
            HandleGenericDamage(target);
        }
        
        PlayHitSound();
    }
    
    private void HandlePlayerDamage(GameObject player)
    {
        // Send damage message if the player has a damage receiver
        player.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
    }
    
    private void HandleEnemyDamage(GameObject enemy)
    {
        if (_currentTarget != enemy)
        {
            _currentTarget = enemy;
            _currentTargetHealth = enemy.GetComponent<TargetHealth>();
            
            if (_currentTargetHealth == null)
            {
                _currentTargetHealth = enemy.AddComponent<TargetHealth>();
                _currentTargetHealth.maxHealth = 100f;
                _currentTargetHealth.currentHealth = 100f;
            }
        }
        
        if (_currentTargetHealth != null && !_currentTargetHealth.isDead)
        {
            _currentTargetHealth.TakeDamage(_damage);
            
            if (_currentTargetHealth.isDead)
            {
                DestroyTarget(enemy);
            }
        }
    }
    
    private void HandleGenericDamage(GameObject target)
    {
        // Try to send damage message
        target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        
        // Check for destructible objects
        if (target.CompareTag("Destructible"))
        {
            Destroy(target);
        }
    }
    
    private void DestroyTarget(GameObject target)
    {
        // Add explosion effect if available
        if (_hitEffect != null)
        {
            ParticleSystem explosion = Instantiate(_hitEffect, target.transform.position, Quaternion.identity);
            explosion.Play();
            Destroy(explosion.gameObject, 2f);
        }
        
        Destroy(target);
        _currentTarget = null;
        _currentTargetHealth = null;
    }
    
    private void PlayFireSound()
    {
        if (_audioSource != null && _fireSound != null)
        {
            _audioSource.clip = _fireSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void StopFireSound()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    private void PlayHitSound()
    {
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    private void OnDisable()
    {
        StopFiring();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_firePoint == null) return;
        
        Gizmos.color = _laserColor;
        Gizmos.DrawRay(_firePoint.position, _firePoint.forward * _maxRange);
    }
}