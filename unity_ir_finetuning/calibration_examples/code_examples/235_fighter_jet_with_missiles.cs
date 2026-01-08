// Prompt: fighter jet with missiles
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class FighterJet : MonoBehaviour
{
    [Header("Flight Controls")]
    [SerializeField] private float _thrustForce = 1500f;
    [SerializeField] private float _pitchTorque = 1000f;
    [SerializeField] private float _yawTorque = 800f;
    [SerializeField] private float _rollTorque = 600f;
    [SerializeField] private float _maxSpeed = 200f;
    [SerializeField] private float _liftForce = 800f;
    
    [Header("Missile System")]
    [SerializeField] private GameObject _missilePrefab;
    [SerializeField] private Transform[] _missileSpawnPoints;
    [SerializeField] private int _maxMissiles = 8;
    [SerializeField] private float _missileFireRate = 0.5f;
    [SerializeField] private float _missileSpeed = 300f;
    [SerializeField] private float _missileLifetime = 10f;
    [SerializeField] private float _targetingRange = 500f;
    [SerializeField] private LayerMask _targetLayerMask = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _engineAudioSource;
    [SerializeField] private AudioClip _missileFireSound;
    [SerializeField] private AudioClip _engineSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem[] _engineTrails;
    [SerializeField] private ParticleSystem _muzzleFlash;
    
    private Rigidbody _rigidbody;
    private int _currentMissileCount;
    private float _lastMissileFireTime;
    private int _currentSpawnPointIndex;
    private Transform _currentTarget;
    private List<GameObject> _activeMissiles = new List<GameObject>();
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.mass = 1000f;
        _rigidbody.drag = 0.1f;
        _rigidbody.angularDrag = 5f;
        
        _currentMissileCount = _maxMissiles;
        
        if (_engineAudioSource != null && _engineSound != null)
        {
            _engineAudioSource.clip = _engineSound;
            _engineAudioSource.loop = true;
            _engineAudioSource.Play();
        }
        
        SetupEngineTrails();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateEngineAudio();
        FindTarget();
        CleanupMissiles();
    }
    
    private void FixedUpdate()
    {
        ApplyThrust();
        ApplyLift();
        ApplyRotation();
        LimitSpeed();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
        {
            FireMissile();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            ReloadMissiles();
        }
    }
    
    private void ApplyThrust()
    {
        float thrust = Input.GetAxis("Vertical");
        Vector3 thrustVector = transform.forward * thrust * _thrustForce;
        _rigidbody.AddForce(thrustVector);
    }
    
    private void ApplyLift()
    {
        float speed = Vector3.Dot(_rigidbody.velocity, transform.forward);
        float liftAmount = speed * speed * 0.001f * _liftForce;
        Vector3 liftVector = transform.up * liftAmount;
        _rigidbody.AddForce(liftVector);
    }
    
    private void ApplyRotation()
    {
        float pitch = -Input.GetAxis("Vertical") * _pitchTorque;
        float yaw = Input.GetAxis("Horizontal") * _yawTorque;
        float roll = -Input.GetAxis("Horizontal") * _rollTorque;
        
        _rigidbody.AddTorque(transform.right * pitch);
        _rigidbody.AddTorque(transform.up * yaw);
        _rigidbody.AddTorque(transform.forward * roll);
    }
    
    private void LimitSpeed()
    {
        if (_rigidbody.velocity.magnitude > _maxSpeed)
        {
            _rigidbody.velocity = _rigidbody.velocity.normalized * _maxSpeed;
        }
    }
    
    private void FireMissile()
    {
        if (_currentMissileCount <= 0 || Time.time - _lastMissileFireTime < _missileFireRate)
            return;
        
        if (_missilePrefab == null || _missileSpawnPoints == null || _missileSpawnPoints.Length == 0)
            return;
        
        Transform spawnPoint = _missileSpawnPoints[_currentSpawnPointIndex];
        GameObject missile = Instantiate(_missilePrefab, spawnPoint.position, spawnPoint.rotation);
        
        Missile missileScript = missile.GetComponent<Missile>();
        if (missileScript == null)
        {
            missileScript = missile.AddComponent<Missile>();
        }
        
        missileScript.Initialize(_missileSpeed, _missileLifetime, _currentTarget);
        _activeMissiles.Add(missile);
        
        _currentMissileCount--;
        _lastMissileFireTime = Time.time;
        _currentSpawnPointIndex = (_currentSpawnPointIndex + 1) % _missileSpawnPoints.Length;
        
        PlayMissileFireEffects();
    }
    
    private void PlayMissileFireEffects()
    {
        if (_missileFireSound != null)
        {
            AudioSource.PlayClipAtPoint(_missileFireSound, transform.position);
        }
        
        if (_muzzleFlash != null)
        {
            _muzzleFlash.Play();
        }
    }
    
    private void FindTarget()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, _targetingRange, _targetLayerMask);
        float closestDistance = float.MaxValue;
        Transform closestTarget = null;
        
        foreach (Collider target in targets)
        {
            if (target.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target.transform;
            }
        }
        
        _currentTarget = closestTarget;
    }
    
    private void ReloadMissiles()
    {
        _currentMissileCount = _maxMissiles;
    }
    
    private void UpdateEngineAudio()
    {
        if (_engineAudioSource != null)
        {
            float throttle = Mathf.Abs(Input.GetAxis("Vertical"));
            _engineAudioSource.pitch = Mathf.Lerp(0.5f, 2f, throttle);
            _engineAudioSource.volume = Mathf.Lerp(0.3f, 1f, throttle);
        }
    }
    
    private void SetupEngineTrails()
    {
        if (_engineTrails != null)
        {
            foreach (ParticleSystem trail in _engineTrails)
            {
                if (trail != null)
                {
                    var emission = trail.emission;
                    emission.enabled = true;
                }
            }
        }
    }
    
    private void CleanupMissiles()
    {
        _activeMissiles.RemoveAll(missile => missile == null);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _targetingRange);
        
        if (_currentTarget != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _currentTarget.position);
        }
    }
}

public class Missile : MonoBehaviour
{
    private float _speed;
    private float _lifetime;
    private Transform _target;
    private Rigidbody _rigidbody;
    private float _spawnTime;
    private bool _isInitialized;
    
    [SerializeField] private float _turnRate = 180f;
    [SerializeField] private float _explosionRadius = 10f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private GameObject _explosionEffect;
    [SerializeField] private AudioClip _explosionSound;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _spawnTime = Time.time;
        
        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            collider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)collider).radius = 0.5f;
        }
        collider.isTrigger = true;
    }
    
    public void Initialize(float speed, float lifetime, Transform target)
    {
        _speed = speed;
        _lifetime = lifetime;
        _target = target;
        _isInitialized = true;
    }
    
    private void Update()
    {
        if (!_isInitialized) return;
        
        if (Time.time - _spawnTime > _lifetime)
        {
            Explode();
            return;
        }
        
        MoveTowardsTarget();
    }
    
    private void MoveTowardsTarget()
    {
        Vector3 direction;
        
        if (_target != null)
        {
            direction = (_target.position - transform.position).normalized;
            Vector3 targetDirection = Vector3.RotateTowards(transform.forward, direction, _turnRate * Mathf.Deg2Rad * Time.deltaTime, 0f);
            transform.rotation = Quaternion.LookRotation(targetDirection);
        }
        
        _rigidbody.velocity = transform.forward * _speed;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.parent != null && other.transform.parent.GetComponent<FighterJet>() != null)
            return;
        
        Explode();
    }
    
    private void Explode()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRadius);
        
        foreach (Collider hitCollider in hitColliders)
        {
            Rigidbody hitRigidbody = hitCollider.GetComponent<Rigidbody>();
            if (hitRigidbody != null)
            {
                hitRigidbody.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
            }
        }
        
        if (_explosionEffect != null)
        {
            Instantiate(_explosionEffect, transform.position, Quaternion.identity);
        }
        
        if (_explosionSound != null)
        {
            AudioSource.PlayClipAtPoint(_explosionSound, transform.position);
        }
        
        Destroy(gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }
}