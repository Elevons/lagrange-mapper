// Prompt: homing missile that tracks target
// Type: general

using UnityEngine;

public class HomingMissile : MonoBehaviour
{
    [Header("Targeting")]
    [SerializeField] private Transform _target;
    [SerializeField] private float _detectionRange = 50f;
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private string _targetTag = "Player";
    
    [Header("Movement")]
    [SerializeField] private float _speed = 15f;
    [SerializeField] private float _rotationSpeed = 200f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _maxSpeed = 25f;
    
    [Header("Homing Behavior")]
    [SerializeField] private float _homingStrength = 2f;
    [SerializeField] private float _predictionTime = 0.5f;
    [SerializeField] private bool _useLeadTargeting = true;
    
    [Header("Lifetime")]
    [SerializeField] private float _lifeTime = 10f;
    [SerializeField] private float _armingDelay = 0.5f;
    
    [Header("Effects")]
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionDamage = 100f;
    [SerializeField] private TrailRenderer _trailRenderer;
    [SerializeField] private ParticleSystem _thrusterParticles;
    
    private Rigidbody _rigidbody;
    private Vector3 _currentVelocity;
    private float _currentSpeed;
    private float _timeAlive;
    private bool _isArmed;
    private bool _hasExploded;
    private Vector3 _lastTargetPosition;
    private Vector3 _targetVelocity;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _currentSpeed = _speed;
        _currentVelocity = transform.forward * _currentSpeed;
        
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = true;
        }
        
        if (_thrusterParticles != null)
        {
            _thrusterParticles.Play();
        }
        
        FindInitialTarget();
    }
    
    private void Update()
    {
        _timeAlive += Time.deltaTime;
        
        if (_timeAlive >= _lifeTime)
        {
            Explode();
            return;
        }
        
        if (_timeAlive >= _armingDelay)
        {
            _isArmed = true;
        }
        
        if (_target == null)
        {
            FindTarget();
        }
        
        UpdateTargetVelocity();
        UpdateMovement();
    }
    
    private void FixedUpdate()
    {
        if (_hasExploded) return;
        
        _rigidbody.velocity = _currentVelocity;
    }
    
    private void FindInitialTarget()
    {
        if (_target != null) return;
        FindTarget();
    }
    
    private void FindTarget()
    {
        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, _detectionRange, _targetLayers);
        
        float closestDistance = float.MaxValue;
        Transform closestTarget = null;
        
        foreach (Collider col in potentialTargets)
        {
            if (col.transform == transform) continue;
            
            if (!string.IsNullOrEmpty(_targetTag) && !col.CompareTag(_targetTag)) continue;
            
            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = col.transform;
            }
        }
        
        _target = closestTarget;
    }
    
    private void UpdateTargetVelocity()
    {
        if (_target == null) return;
        
        Vector3 currentTargetPosition = _target.position;
        _targetVelocity = (currentTargetPosition - _lastTargetPosition) / Time.deltaTime;
        _lastTargetPosition = currentTargetPosition;
    }
    
    private void UpdateMovement()
    {
        if (_target == null)
        {
            // Continue straight if no target
            _currentVelocity = transform.forward * _currentSpeed;
            return;
        }
        
        Vector3 targetPosition = _target.position;
        
        // Lead targeting - predict where target will be
        if (_useLeadTargeting)
        {
            targetPosition += _targetVelocity * _predictionTime;
        }
        
        Vector3 directionToTarget = (targetPosition - transform.position).normalized;
        Vector3 desiredVelocity = directionToTarget * _currentSpeed;
        
        // Apply homing behavior
        _currentVelocity = Vector3.Lerp(_currentVelocity, desiredVelocity, _homingStrength * Time.deltaTime);
        
        // Accelerate over time
        _currentSpeed = Mathf.Min(_currentSpeed + _acceleration * Time.deltaTime, _maxSpeed);
        _currentVelocity = _currentVelocity.normalized * _currentSpeed;
        
        // Rotate to face movement direction
        if (_currentVelocity != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_currentVelocity);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasExploded || !_isArmed) return;
        
        if (other.transform == transform) return;
        
        // Check if we hit our target or any valid target
        bool isValidTarget = false;
        
        if (_target != null && other.transform == _target)
        {
            isValidTarget = true;
        }
        else if (!string.IsNullOrEmpty(_targetTag) && other.CompareTag(_targetTag))
        {
            isValidTarget = true;
        }
        else if ((_targetLayers.value & (1 << other.gameObject.layer)) != 0)
        {
            isValidTarget = true;
        }
        
        if (isValidTarget)
        {
            Explode();
        }
    }
    
    private void Explode()
    {
        if (_hasExploded) return;
        _hasExploded = true;
        
        // Apply explosion damage
        ApplyExplosionDamage();
        
        // Spawn explosion effect
        if (_explosionPrefab != null)
        {
            GameObject explosion = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            Destroy(explosion, 5f);
        }
        
        // Stop particles
        if (_thrusterParticles != null)
        {
            _thrusterParticles.Stop();
        }
        
        // Disable trail
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
        
        // Destroy missile
        Destroy(gameObject);
    }
    
    private void ApplyExplosionDamage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, _explosionRadius);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float damageMultiplier = 1f - (distance / _explosionRadius);
            float damage = _explosionDamage * damageMultiplier;
            
            // Try to apply damage through various common interfaces
            MonoBehaviour[] components = hit.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                // Use reflection to find damage methods
                var damageMethod = component.GetType().GetMethod("TakeDamage");
                if (damageMethod != null)
                {
                    damageMethod.Invoke(component, new object[] { damage });
                    break;
                }
            }
            
            // Apply physics force
            Rigidbody hitRigidbody = hit.GetComponent<Rigidbody>();
            if (hitRigidbody != null)
            {
                Vector3 forceDirection = (hit.transform.position - transform.position).normalized;
                float forceMultiplier = 1f - (distance / _explosionRadius);
                hitRigidbody.AddForce(forceDirection * _explosionDamage * forceMultiplier, ForceMode.Impulse);
            }
        }
    }
    
    public void SetTarget(Transform target)
    {
        _target = target;
    }
    
    public void SetTargetTag(string tag)
    {
        _targetTag = tag;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        // Draw explosion radius
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        
        // Draw line to target
        if (_target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }
}