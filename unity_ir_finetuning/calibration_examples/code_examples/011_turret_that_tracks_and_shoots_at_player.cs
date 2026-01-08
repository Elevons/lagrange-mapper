// Prompt: turret that tracks and shoots at player
// Type: movement

using UnityEngine;

public class PlayerTrackingTurret : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _fieldOfView = 90f;
    [SerializeField] private LayerMask _obstacleLayerMask = 1;
    
    [Header("Rotation")]
    [SerializeField] private Transform _turretHead;
    [SerializeField] private float _rotationSpeed = 2f;
    [SerializeField] private bool _smoothRotation = true;
    
    [Header("Shooting")]
    [SerializeField] private Transform _firePoint;
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _projectileSpeed = 20f;
    [SerializeField] private float _projectileLifetime = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _shootSound;
    
    private Transform _player;
    private float _nextFireTime;
    private bool _playerInRange;
    private bool _hasLineOfSight;
    
    private void Start()
    {
        if (_turretHead == null)
            _turretHead = transform;
            
        if (_firePoint == null)
            _firePoint = _turretHead;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        FindPlayer();
        
        if (_player != null)
        {
            CheckPlayerInRange();
            CheckLineOfSight();
            
            if (_playerInRange && _hasLineOfSight)
            {
                RotateTowardsPlayer();
                TryShoot();
            }
        }
    }
    
    private void FindPlayer()
    {
        if (_player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
                _player = playerObject.transform;
        }
    }
    
    private void CheckPlayerInRange()
    {
        if (_player == null)
        {
            _playerInRange = false;
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _playerInRange = distanceToPlayer <= _detectionRange;
        
        if (_playerInRange)
        {
            Vector3 directionToPlayer = (_player.position - transform.position).normalized;
            float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
            _playerInRange = angleToPlayer <= _fieldOfView * 0.5f;
        }
    }
    
    private void CheckLineOfSight()
    {
        if (!_playerInRange || _player == null)
        {
            _hasLineOfSight = false;
            return;
        }
        
        Vector3 directionToPlayer = (_player.position - _firePoint.position).normalized;
        float distanceToPlayer = Vector3.Distance(_firePoint.position, _player.position);
        
        RaycastHit hit;
        if (Physics.Raycast(_firePoint.position, directionToPlayer, out hit, distanceToPlayer, _obstacleLayerMask))
        {
            _hasLineOfSight = hit.collider.CompareTag("Player");
        }
        else
        {
            _hasLineOfSight = true;
        }
    }
    
    private void RotateTowardsPlayer()
    {
        if (_player == null) return;
        
        Vector3 targetDirection = (_player.position - _turretHead.position).normalized;
        targetDirection.y = 0f;
        
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        if (_smoothRotation)
        {
            _turretHead.rotation = Quaternion.Slerp(_turretHead.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
        else
        {
            _turretHead.rotation = targetRotation;
        }
    }
    
    private void TryShoot()
    {
        if (Time.time >= _nextFireTime && _projectilePrefab != null)
        {
            Shoot();
            _nextFireTime = Time.time + (1f / _fireRate);
        }
    }
    
    private void Shoot()
    {
        GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
        
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            Vector3 shootDirection = _firePoint.forward;
            if (_player != null)
            {
                shootDirection = (_player.position - _firePoint.position).normalized;
            }
            
            projectileRb.velocity = shootDirection * _projectileSpeed;
        }
        
        TurretProjectile projectileScript = projectile.GetComponent<TurretProjectile>();
        if (projectileScript == null)
        {
            projectileScript = projectile.AddComponent<TurretProjectile>();
        }
        projectileScript.Initialize(_projectileLifetime);
        
        if (_audioSource != null && _shootSound != null)
        {
            _audioSource.PlayOneShot(_shootSound);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Vector3 leftBoundary = Quaternion.Euler(0, -_fieldOfView * 0.5f, 0) * transform.forward * _detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, _fieldOfView * 0.5f, 0) * transform.forward * _detectionRange;
        
        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);
        
        if (_hasLineOfSight && _player != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(_firePoint.position, _player.position);
        }
    }
}

public class TurretProjectile : MonoBehaviour
{
    private float _lifetime;
    private float _spawnTime;
    
    public void Initialize(float lifetime)
    {
        _lifetime = lifetime;
        _spawnTime = Time.time;
    }
    
    private void Update()
    {
        if (Time.time - _spawnTime >= _lifetime)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Destroy(gameObject);
        }
        else if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        Destroy(gameObject);
    }
}