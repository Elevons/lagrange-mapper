// Prompt: tank with turret rotation
// Type: general

using UnityEngine;

public class Tank : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 90f;
    
    [Header("Turret")]
    [SerializeField] private Transform _turret;
    [SerializeField] private float _turretRotationSpeed = 120f;
    [SerializeField] private Transform _firePoint;
    
    [Header("Shooting")]
    [SerializeField] private GameObject _projectilePrefab;
    [SerializeField] private float _fireRate = 1f;
    [SerializeField] private float _projectileSpeed = 20f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _fireKey = KeyCode.Space;
    [SerializeField] private bool _useMouseAiming = true;
    
    private Rigidbody _rigidbody;
    private Camera _mainCamera;
    private float _lastFireTime;
    private Vector3 _targetDirection;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _mainCamera = Camera.main;
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_turret == null)
        {
            Transform turretChild = transform.Find("Turret");
            if (turretChild != null)
            {
                _turret = turretChild;
            }
        }
        
        if (_firePoint == null && _turret != null)
        {
            Transform firePointChild = _turret.Find("FirePoint");
            if (firePointChild != null)
            {
                _firePoint = firePointChild;
            }
        }
        
        _rigidbody.centerOfMass = Vector3.zero;
    }
    
    private void Update()
    {
        HandleInput();
        HandleTurretRotation();
        HandleShooting();
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
    }
    
    private void HandleInput()
    {
        if (_useMouseAiming && _mainCamera != null)
        {
            Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Vector3 targetPosition = hit.point;
                targetPosition.y = _turret.position.y;
                _targetDirection = (targetPosition - _turret.position).normalized;
            }
        }
        else
        {
            float horizontalAim = Input.GetAxis("Mouse X");
            if (Mathf.Abs(horizontalAim) > 0.1f)
            {
                _turret.Rotate(0, horizontalAim * _turretRotationSpeed * Time.deltaTime, 0);
            }
        }
    }
    
    private void HandleMovement()
    {
        float vertical = Input.GetAxis("Vertical");
        float horizontal = Input.GetAxis("Horizontal");
        
        Vector3 movement = transform.forward * vertical * _moveSpeed;
        _rigidbody.MovePosition(_rigidbody.position + movement * Time.fixedDeltaTime);
        
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            float rotation = horizontal * _rotationSpeed * Time.fixedDeltaTime;
            Quaternion deltaRotation = Quaternion.Euler(0, rotation, 0);
            _rigidbody.MoveRotation(_rigidbody.rotation * deltaRotation);
        }
    }
    
    private void HandleTurretRotation()
    {
        if (_turret == null) return;
        
        if (_useMouseAiming && _targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_targetDirection);
            targetRotation = Quaternion.Euler(0, targetRotation.eulerAngles.y, 0);
            
            _turret.rotation = Quaternion.RotateTowards(
                _turret.rotation, 
                targetRotation, 
                _turretRotationSpeed * Time.deltaTime
            );
        }
    }
    
    private void HandleShooting()
    {
        if (Input.GetKey(_fireKey) && CanFire())
        {
            Fire();
        }
    }
    
    private bool CanFire()
    {
        return Time.time >= _lastFireTime + (1f / _fireRate);
    }
    
    private void Fire()
    {
        if (_projectilePrefab == null || _firePoint == null) return;
        
        GameObject projectile = Instantiate(_projectilePrefab, _firePoint.position, _firePoint.rotation);
        
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb == null)
        {
            projectileRb = projectile.AddComponent<Rigidbody>();
        }
        
        projectileRb.velocity = _firePoint.forward * _projectileSpeed;
        
        ProjectileDestroyer destroyer = projectile.GetComponent<ProjectileDestroyer>();
        if (destroyer == null)
        {
            destroyer = projectile.AddComponent<ProjectileDestroyer>();
        }
        destroyer.SetLifetime(5f);
        
        _lastFireTime = Time.time;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_turret != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_turret.position, 0.5f);
            
            if (_firePoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_firePoint.position, 0.2f);
                Gizmos.DrawRay(_firePoint.position, _firePoint.forward * 3f);
            }
        }
        
        if (_useMouseAiming && _targetDirection != Vector3.zero)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawRay(transform.position, _targetDirection * 5f);
        }
    }
}

public class ProjectileDestroyer : MonoBehaviour
{
    private float _lifetime = 5f;
    private float _spawnTime;
    
    private void Start()
    {
        _spawnTime = Time.time;
    }
    
    private void Update()
    {
        if (Time.time >= _spawnTime + _lifetime)
        {
            Destroy(gameObject);
        }
    }
    
    public void SetLifetime(float lifetime)
    {
        _lifetime = lifetime;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}