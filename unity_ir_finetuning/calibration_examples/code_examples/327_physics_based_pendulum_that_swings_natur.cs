// Prompt: physics-based pendulum that swings naturally but when it reaches the bottom of its swing it applies an explosive force to any rigidbody it touches, plays a bell sound, and creates a shockwave particle effect
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ExplosivePendulum : MonoBehaviour
{
    [Header("Pendulum Physics")]
    [SerializeField] private Transform _pivotPoint;
    [SerializeField] private float _pendulumLength = 5f;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _damping = 0.995f;
    [SerializeField] private float _initialAngle = 45f;
    
    [Header("Explosion Settings")]
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private float _explosionRadius = 10f;
    [SerializeField] private float _upwardModifier = 3f;
    [SerializeField] private float _bottomThreshold = 0.1f;
    [SerializeField] private float _explosionCooldown = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _bellSound;
    [SerializeField] private float _volume = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _shockwaveEffect;
    [SerializeField] private GameObject _shockwavePrefab;
    
    [Header("Events")]
    public UnityEvent OnExplosion;
    
    private float _currentAngle;
    private float _angularVelocity;
    private bool _canExplode = true;
    private float _lastExplosionTime;
    private Vector3 _restPosition;
    private bool _wasAtBottom;
    
    private void Start()
    {
        InitializePendulum();
        SetupAudio();
        SetupEffects();
    }
    
    private void InitializePendulum()
    {
        if (_pivotPoint == null)
            _pivotPoint = transform.parent != null ? transform.parent : transform;
            
        _currentAngle = _initialAngle * Mathf.Deg2Rad;
        _angularVelocity = 0f;
        _restPosition = _pivotPoint.position + Vector3.down * _pendulumLength;
        
        UpdatePendulumPosition();
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.playOnAwake = false;
        _audioSource.volume = _volume;
    }
    
    private void SetupEffects()
    {
        if (_shockwaveEffect == null)
        {
            _shockwaveEffect = GetComponentInChildren<ParticleSystem>();
        }
    }
    
    private void FixedUpdate()
    {
        UpdatePendulumPhysics();
        UpdatePendulumPosition();
        CheckForExplosion();
    }
    
    private void UpdatePendulumPhysics()
    {
        float angularAcceleration = -(_gravity / _pendulumLength) * Mathf.Sin(_currentAngle);
        _angularVelocity += angularAcceleration * Time.fixedDeltaTime;
        _angularVelocity *= _damping;
        _currentAngle += _angularVelocity * Time.fixedDeltaTime;
    }
    
    private void UpdatePendulumPosition()
    {
        Vector3 pendulumPosition = _pivotPoint.position + new Vector3(
            Mathf.Sin(_currentAngle) * _pendulumLength,
            -Mathf.Cos(_currentAngle) * _pendulumLength,
            0f
        );
        
        transform.position = pendulumPosition;
        
        Vector3 direction = (transform.position - _pivotPoint.position).normalized;
        transform.rotation = Quaternion.LookRotation(Vector3.forward, -direction);
    }
    
    private void CheckForExplosion()
    {
        bool isAtBottom = IsAtBottomOfSwing();
        
        if (isAtBottom && !_wasAtBottom && _canExplode && Time.time - _lastExplosionTime > _explosionCooldown)
        {
            TriggerExplosion();
        }
        
        _wasAtBottom = isAtBottom;
    }
    
    private bool IsAtBottomOfSwing()
    {
        return Mathf.Abs(_currentAngle) < _bottomThreshold;
    }
    
    private void TriggerExplosion()
    {
        _canExplode = false;
        _lastExplosionTime = Time.time;
        
        ApplyExplosiveForce();
        PlayBellSound();
        CreateShockwaveEffect();
        OnExplosion?.Invoke();
        
        Invoke(nameof(ResetExplosionCooldown), _explosionCooldown);
    }
    
    private void ApplyExplosiveForce()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRadius);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius, _upwardModifier, ForceMode.Impulse);
            }
        }
    }
    
    private void PlayBellSound()
    {
        if (_audioSource != null && _bellSound != null)
        {
            _audioSource.PlayOneShot(_bellSound, _volume);
        }
    }
    
    private void CreateShockwaveEffect()
    {
        if (_shockwaveEffect != null)
        {
            _shockwaveEffect.transform.position = transform.position;
            _shockwaveEffect.Play();
        }
        
        if (_shockwavePrefab != null)
        {
            GameObject shockwave = Instantiate(_shockwavePrefab, transform.position, Quaternion.identity);
            Destroy(shockwave, 5f);
        }
    }
    
    private void ResetExplosionCooldown()
    {
        _canExplode = true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (IsAtBottomOfSwing() && _canExplode)
        {
            Rigidbody otherRb = other.GetComponent<Rigidbody>();
            if (otherRb != null)
            {
                TriggerExplosion();
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_pivotPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_pivotPoint.position, 0.1f);
            Gizmos.DrawLine(_pivotPoint.position, transform.position);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _explosionRadius);
            
            Gizmos.color = Color.green;
            Vector3 bottomPosition = _pivotPoint.position + Vector3.down * _pendulumLength;
            Gizmos.DrawWireSphere(bottomPosition, _bottomThreshold * _pendulumLength);
        }
    }
    
    public void SetPendulumAngle(float angleInDegrees)
    {
        _currentAngle = angleInDegrees * Mathf.Deg2Rad;
        _angularVelocity = 0f;
        UpdatePendulumPosition();
    }
    
    public void AddImpulse(float impulse)
    {
        _angularVelocity += impulse;
    }
    
    public float GetCurrentAngle()
    {
        return _currentAngle * Mathf.Rad2Deg;
    }
}