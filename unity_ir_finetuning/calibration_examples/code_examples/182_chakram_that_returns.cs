// Prompt: chakram that returns
// Type: general

using UnityEngine;

public class ReturningChakram : MonoBehaviour
{
    [Header("Chakram Settings")]
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _returnSpeed = 10f;
    [SerializeField] private float _maxDistance = 20f;
    [SerializeField] private float _rotationSpeed = 720f;
    [SerializeField] private float _returnDelay = 0.5f;
    
    [Header("Damage Settings")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private LayerMask _enemyLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _throwSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _returnSound;
    
    private enum ChakramState
    {
        Idle,
        Thrown,
        Returning,
        Caught
    }
    
    private ChakramState _currentState = ChakramState.Idle;
    private Transform _thrower;
    private Vector3 _throwDirection;
    private Vector3 _startPosition;
    private float _distanceTraveled;
    private float _returnTimer;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Collider _collider;
    private bool _hasHitTarget;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody.useGravity = false;
    }
    
    private void Start()
    {
        _startPosition = transform.position;
    }
    
    private void Update()
    {
        HandleRotation();
        HandleMovement();
        CheckReturnConditions();
    }
    
    private void HandleRotation()
    {
        if (_currentState != ChakramState.Idle && _currentState != ChakramState.Caught)
        {
            transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void HandleMovement()
    {
        switch (_currentState)
        {
            case ChakramState.Thrown:
                HandleThrownMovement();
                break;
            case ChakramState.Returning:
                HandleReturningMovement();
                break;
        }
    }
    
    private void HandleThrownMovement()
    {
        _distanceTraveled += _rigidbody.velocity.magnitude * Time.deltaTime;
        
        if (_returnTimer > 0f)
        {
            _returnTimer -= Time.deltaTime;
        }
    }
    
    private void HandleReturningMovement()
    {
        if (_thrower != null)
        {
            Vector3 directionToThrower = (_thrower.position - transform.position).normalized;
            _rigidbody.velocity = directionToThrower * _returnSpeed;
            
            float distanceToThrower = Vector3.Distance(transform.position, _thrower.position);
            if (distanceToThrower < 1f)
            {
                CatchChakram();
            }
        }
        else
        {
            Vector3 directionToStart = (_startPosition - transform.position).normalized;
            _rigidbody.velocity = directionToStart * _returnSpeed;
            
            float distanceToStart = Vector3.Distance(transform.position, _startPosition);
            if (distanceToStart < 1f)
            {
                ResetChakram();
            }
        }
    }
    
    private void CheckReturnConditions()
    {
        if (_currentState == ChakramState.Thrown)
        {
            bool shouldReturn = _distanceTraveled >= _maxDistance || 
                              _returnTimer <= 0f || 
                              _hasHitTarget;
            
            if (shouldReturn)
            {
                StartReturning();
            }
        }
    }
    
    public void ThrowChakram(Transform thrower, Vector3 direction)
    {
        if (_currentState != ChakramState.Idle) return;
        
        _thrower = thrower;
        _throwDirection = direction.normalized;
        _currentState = ChakramState.Thrown;
        _distanceTraveled = 0f;
        _returnTimer = _returnDelay;
        _hasHitTarget = false;
        
        _rigidbody.velocity = _throwDirection * _throwForce;
        
        PlaySound(_throwSound);
    }
    
    private void StartReturning()
    {
        if (_currentState != ChakramState.Thrown) return;
        
        _currentState = ChakramState.Returning;
        PlaySound(_returnSound);
    }
    
    private void CatchChakram()
    {
        _currentState = ChakramState.Caught;
        _rigidbody.velocity = Vector3.zero;
        
        if (_thrower != null)
        {
            transform.SetParent(_thrower);
            transform.localPosition = Vector3.zero;
        }
        
        ResetChakram();
    }
    
    private void ResetChakram()
    {
        _currentState = ChakramState.Idle;
        _rigidbody.velocity = Vector3.zero;
        _distanceTraveled = 0f;
        _returnTimer = 0f;
        _hasHitTarget = false;
        _thrower = null;
        
        transform.SetParent(null);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_currentState != ChakramState.Thrown && _currentState != ChakramState.Returning) return;
        
        if (other.CompareTag("Player") && _currentState == ChakramState.Returning)
        {
            CatchChakram();
            return;
        }
        
        if (_thrower != null && other.transform == _thrower) return;
        
        if (IsInLayerMask(other.gameObject.layer, _enemyLayers))
        {
            DealDamage(other);
            _hasHitTarget = true;
            
            if (_currentState == ChakramState.Thrown)
            {
                StartReturning();
            }
        }
        else if (other.CompareTag("Wall") || other.CompareTag("Obstacle"))
        {
            _hasHitTarget = true;
            
            if (_currentState == ChakramState.Thrown)
            {
                StartReturning();
            }
        }
    }
    
    private void DealDamage(Collider target)
    {
        var targetHealth = target.GetComponent<Health>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(_damage);
        }
        
        PlaySound(_hitSound);
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public bool IsAvailable()
    {
        return _currentState == ChakramState.Idle;
    }
    
    public void ForceReturn()
    {
        if (_currentState == ChakramState.Thrown)
        {
            StartReturning();
        }
    }
}

[System.Serializable]
public class Health : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }
    
    private void Die()
    {
        Destroy(gameObject);
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}