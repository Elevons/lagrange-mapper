// Prompt: bouncing gem that moves randomly
// Type: movement

using UnityEngine;

public class BouncingGem : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _bounceForce = 10f;
    [SerializeField] private float _directionChangeInterval = 2f;
    
    [Header("Bounce Settings")]
    [SerializeField] private float _minBounceAngle = 30f;
    [SerializeField] private float _maxBounceAngle = 150f;
    [SerializeField] private LayerMask _bounceLayerMask = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobAmplitude = 0.5f;
    [SerializeField] private float _bobFrequency = 2f;
    
    private Rigidbody _rigidbody;
    private Vector3 _currentDirection;
    private float _directionTimer;
    private Vector3 _startPosition;
    private float _bobTimer;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _rigidbody.drag = 0.5f;
        _rigidbody.angularDrag = 0.5f;
        
        _startPosition = transform.position;
        _directionTimer = _directionChangeInterval;
        
        ChooseRandomDirection();
    }
    
    private void Update()
    {
        HandleDirectionChange();
        HandleVisualEffects();
    }
    
    private void FixedUpdate()
    {
        ApplyMovement();
    }
    
    private void HandleDirectionChange()
    {
        _directionTimer -= Time.deltaTime;
        
        if (_directionTimer <= 0f)
        {
            ChooseRandomDirection();
            _directionTimer = _directionChangeInterval + Random.Range(-0.5f, 0.5f);
        }
    }
    
    private void HandleVisualEffects()
    {
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
        
        _bobTimer += Time.deltaTime * _bobFrequency;
        float bobOffset = Mathf.Sin(_bobTimer) * _bobAmplitude;
        
        Vector3 newPosition = transform.position;
        newPosition.y = _startPosition.y + bobOffset;
        transform.position = newPosition;
        
        _startPosition.x = transform.position.x;
        _startPosition.z = transform.position.z;
    }
    
    private void ApplyMovement()
    {
        Vector3 targetVelocity = _currentDirection * _moveSpeed;
        targetVelocity.y = _rigidbody.velocity.y;
        
        _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, targetVelocity, Time.fixedDeltaTime * 5f);
    }
    
    private void ChooseRandomDirection()
    {
        float randomAngle = Random.Range(0f, 360f);
        _currentDirection = new Vector3(
            Mathf.Cos(randomAngle * Mathf.Deg2Rad),
            0f,
            Mathf.Sin(randomAngle * Mathf.Deg2Rad)
        ).normalized;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (IsInLayerMask(collision.gameObject.layer, _bounceLayerMask))
        {
            HandleBounce(collision);
        }
    }
    
    private void HandleBounce(Collision collision)
    {
        Vector3 normal = collision.contacts[0].normal;
        Vector3 reflectedDirection = Vector3.Reflect(_currentDirection, normal);
        
        float bounceAngle = Random.Range(_minBounceAngle, _maxBounceAngle);
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0f;
        randomDirection.Normalize();
        
        _currentDirection = Vector3.Slerp(reflectedDirection, randomDirection, 0.3f).normalized;
        
        _rigidbody.AddForce(normal * _bounceForce, ForceMode.Impulse);
        
        _directionTimer = _directionChangeInterval;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HandlePlayerInteraction();
        }
    }
    
    private void HandlePlayerInteraction()
    {
        _rigidbody.AddForce(Vector3.up * _bounceForce * 0.5f, ForceMode.Impulse);
        ChooseRandomDirection();
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, _currentDirection * 2f);
    }
}