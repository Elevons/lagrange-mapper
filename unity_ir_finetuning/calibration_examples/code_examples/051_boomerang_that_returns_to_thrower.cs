// Prompt: boomerang that returns to thrower
// Type: general

using UnityEngine;

public class Boomerang : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _returnForce = 10f;
    [SerializeField] private float _maxDistance = 20f;
    [SerializeField] private float _returnDelay = 0.5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float _spinSpeed = 720f;
    [SerializeField] private Vector3 _spinAxis = Vector3.forward;
    
    [Header("Physics")]
    [SerializeField] private float _drag = 0.5f;
    [SerializeField] private float _gravityScale = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _throwSound;
    [SerializeField] private AudioClip _catchSound;
    [SerializeField] private AudioClip _whooshSound;
    
    private Transform _thrower;
    private Vector3 _throwDirection;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private bool _isReturning = false;
    private bool _isThrown = false;
    private float _returnTimer = 0f;
    private Vector3 _startPosition;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody.drag = _drag;
        _rigidbody.useGravity = true;
        _startPosition = transform.position;
    }
    
    private void Start()
    {
        Physics.gravity = new Vector3(0, -9.81f * _gravityScale, 0);
    }
    
    private void Update()
    {
        if (_isThrown)
        {
            HandleSpin();
            HandleReturn();
            CheckMaxDistance();
        }
    }
    
    private void HandleSpin()
    {
        transform.Rotate(_spinAxis * _spinSpeed * Time.deltaTime);
    }
    
    private void HandleReturn()
    {
        if (!_isReturning)
        {
            _returnTimer += Time.deltaTime;
            if (_returnTimer >= _returnDelay)
            {
                StartReturning();
            }
        }
        else if (_thrower != null)
        {
            Vector3 directionToThrower = (_thrower.position - transform.position).normalized;
            _rigidbody.AddForce(directionToThrower * _returnForce, ForceMode.Acceleration);
            
            float distanceToThrower = Vector3.Distance(transform.position, _thrower.position);
            if (distanceToThrower < 1f)
            {
                ReturnToThrower();
            }
        }
    }
    
    private void CheckMaxDistance()
    {
        if (_thrower != null)
        {
            float distanceFromThrower = Vector3.Distance(transform.position, _thrower.position);
            if (distanceFromThrower >= _maxDistance && !_isReturning)
            {
                StartReturning();
            }
        }
    }
    
    private void StartReturning()
    {
        _isReturning = true;
        _rigidbody.velocity = Vector3.zero;
        
        if (_whooshSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_whooshSound);
        }
    }
    
    public void ThrowBoomerang(Transform thrower, Vector3 direction)
    {
        if (_isThrown) return;
        
        _thrower = thrower;
        _throwDirection = direction.normalized;
        _isThrown = true;
        _isReturning = false;
        _returnTimer = 0f;
        
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.AddForce(_throwDirection * _throwForce, ForceMode.Impulse);
        
        if (_throwSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_throwSound);
        }
    }
    
    private void ReturnToThrower()
    {
        _isThrown = false;
        _isReturning = false;
        _returnTimer = 0f;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        
        transform.position = _thrower.position;
        transform.SetParent(_thrower);
        
        if (_catchSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_catchSound);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isThrown && other.transform == _thrower)
        {
            ReturnToThrower();
        }
        else if (_isThrown && !other.CompareTag("Player") && !other.isTrigger)
        {
            if (!_isReturning)
            {
                StartReturning();
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_isThrown && collision.transform != _thrower)
        {
            if (!_isReturning)
            {
                StartReturning();
            }
        }
    }
    
    public void ResetBoomerang()
    {
        _isThrown = false;
        _isReturning = false;
        _returnTimer = 0f;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        transform.position = _startPosition;
        transform.SetParent(null);
    }
    
    public bool IsAvailable()
    {
        return !_isThrown;
    }
}