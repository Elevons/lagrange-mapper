// Prompt: swinging axe pendulum
// Type: general

using UnityEngine;

public class SwingingAxePendulum : MonoBehaviour
{
    [Header("Pendulum Settings")]
    [SerializeField] private float _swingAngle = 45f;
    [SerializeField] private float _swingSpeed = 2f;
    [SerializeField] private bool _startSwingingOnAwake = true;
    [SerializeField] private float _delayBeforeStart = 0f;
    
    [Header("Damage Settings")]
    [SerializeField] private int _damage = 50;
    [SerializeField] private float _damageForce = 10f;
    [SerializeField] private LayerMask _damageableLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _swingSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private float _audioVolume = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _hitParticles;
    [SerializeField] private TrailRenderer _trailRenderer;
    
    private bool _isSwinging = false;
    private float _currentAngle = 0f;
    private float _swingDirection = 1f;
    private AudioSource _audioSource;
    private Vector3 _initialRotation;
    private float _startDelay;
    
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _initialRotation = transform.eulerAngles;
        _startDelay = _delayBeforeStart;
    }
    
    private void Start()
    {
        if (_startSwingingOnAwake && _delayBeforeStart <= 0f)
        {
            StartSwinging();
        }
    }
    
    private void Update()
    {
        if (_startSwingingOnAwake && _startDelay > 0f)
        {
            _startDelay -= Time.deltaTime;
            if (_startDelay <= 0f)
            {
                StartSwinging();
            }
        }
        
        if (_isSwinging)
        {
            UpdateSwing();
        }
    }
    
    private void UpdateSwing()
    {
        _currentAngle += _swingSpeed * _swingDirection * Time.deltaTime * 90f;
        
        if (_currentAngle >= _swingAngle)
        {
            _currentAngle = _swingAngle;
            _swingDirection = -1f;
            PlaySwingSound();
        }
        else if (_currentAngle <= -_swingAngle)
        {
            _currentAngle = -_swingAngle;
            _swingDirection = 1f;
            PlaySwingSound();
        }
        
        Vector3 rotation = _initialRotation;
        rotation.z += _currentAngle;
        transform.eulerAngles = rotation;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (IsInLayerMask(other.gameObject.layer, _damageableLayers))
        {
            DealDamage(other);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (IsInLayerMask(collision.gameObject.layer, _damageableLayers))
        {
            DealDamage(collision.collider);
            
            if (_hitParticles != null)
            {
                _hitParticles.transform.position = collision.contacts[0].point;
                _hitParticles.Play();
            }
        }
    }
    
    private void DealDamage(Collider target)
    {
        if (target.CompareTag("Player"))
        {
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 forceDirection = (target.transform.position - transform.position).normalized;
                targetRb.AddForce(forceDirection * _damageForce, ForceMode.Impulse);
            }
            
            PlayHitSound();
        }
        
        IDamageable damageable = target.GetComponent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(_damage);
        }
    }
    
    private void PlaySwingSound()
    {
        if (_swingSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_swingSound, _audioVolume);
        }
    }
    
    private void PlayHitSound()
    {
        if (_hitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_hitSound, _audioVolume);
        }
    }
    
    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
    
    public void StartSwinging()
    {
        _isSwinging = true;
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = true;
        }
    }
    
    public void StopSwinging()
    {
        _isSwinging = false;
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
    }
    
    public void SetSwingSpeed(float speed)
    {
        _swingSpeed = Mathf.Max(0f, speed);
    }
    
    public void SetSwingAngle(float angle)
    {
        _swingAngle = Mathf.Clamp(angle, 0f, 90f);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 leftSwing = Quaternion.Euler(0, 0, -_swingAngle) * Vector3.down;
        Vector3 rightSwing = Quaternion.Euler(0, 0, _swingAngle) * Vector3.down;
        
        Gizmos.DrawRay(transform.position, leftSwing * 2f);
        Gizmos.DrawRay(transform.position, rightSwing * 2f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.1f);
    }
}

public interface IDamageable
{
    void TakeDamage(int damage);
}