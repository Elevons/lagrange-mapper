// Prompt: knight with shield blocking
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class KnightShieldController : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private GameObject _shieldObject;
    [SerializeField] private Transform _shieldPosition;
    [SerializeField] private float _blockDuration = 2f;
    [SerializeField] private float _blockCooldown = 1f;
    [SerializeField] private float _blockAngle = 90f;
    [SerializeField] private LayerMask _projectileLayer = 1;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _blockTrigger = "Block";
    [SerializeField] private string _isBlockingBool = "IsBlocking";
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _blockSound;
    [SerializeField] private AudioClip _deflectSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _blockEffect;
    [SerializeField] private ParticleSystem _deflectEffect;
    
    [Header("Input")]
    [SerializeField] private KeyCode _blockKey = KeyCode.Mouse1;
    
    [Header("Events")]
    public UnityEvent OnBlockStart;
    public UnityEvent OnBlockEnd;
    public UnityEvent OnSuccessfulBlock;
    
    private bool _isBlocking = false;
    private bool _canBlock = true;
    private float _blockTimer = 0f;
    private float _cooldownTimer = 0f;
    private Collider _shieldCollider;
    private Rigidbody _rb;
    
    private void Start()
    {
        if (_shieldObject != null)
        {
            _shieldCollider = _shieldObject.GetComponent<Collider>();
            if (_shieldCollider == null)
            {
                _shieldCollider = _shieldObject.AddComponent<BoxCollider>();
                _shieldCollider.isTrigger = true;
            }
        }
        
        _rb = GetComponent<Rigidbody>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_animator == null)
            _animator = GetComponent<Animator>();
            
        if (_shieldObject != null)
            _shieldObject.SetActive(false);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateBlockTimer();
        UpdateCooldownTimer();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_blockKey) && _canBlock && !_isBlocking)
        {
            StartBlocking();
        }
        else if (Input.GetKeyUp(_blockKey) && _isBlocking)
        {
            StopBlocking();
        }
    }
    
    private void UpdateBlockTimer()
    {
        if (_isBlocking)
        {
            _blockTimer += Time.deltaTime;
            if (_blockTimer >= _blockDuration)
            {
                StopBlocking();
            }
        }
    }
    
    private void UpdateCooldownTimer()
    {
        if (!_canBlock)
        {
            _cooldownTimer += Time.deltaTime;
            if (_cooldownTimer >= _blockCooldown)
            {
                _canBlock = true;
                _cooldownTimer = 0f;
            }
        }
    }
    
    private void StartBlocking()
    {
        _isBlocking = true;
        _blockTimer = 0f;
        
        if (_shieldObject != null)
        {
            _shieldObject.SetActive(true);
            PositionShield();
        }
        
        if (_animator != null)
        {
            _animator.SetTrigger(_blockTrigger);
            _animator.SetBool(_isBlockingBool, true);
        }
        
        if (_audioSource != null && _blockSound != null)
        {
            _audioSource.PlayOneShot(_blockSound);
        }
        
        if (_blockEffect != null)
        {
            _blockEffect.Play();
        }
        
        OnBlockStart?.Invoke();
    }
    
    private void StopBlocking()
    {
        _isBlocking = false;
        _canBlock = false;
        _cooldownTimer = 0f;
        
        if (_shieldObject != null)
        {
            _shieldObject.SetActive(false);
        }
        
        if (_animator != null)
        {
            _animator.SetBool(_isBlockingBool, false);
        }
        
        if (_blockEffect != null)
        {
            _blockEffect.Stop();
        }
        
        OnBlockEnd?.Invoke();
    }
    
    private void PositionShield()
    {
        if (_shieldObject == null) return;
        
        if (_shieldPosition != null)
        {
            _shieldObject.transform.position = _shieldPosition.position;
            _shieldObject.transform.rotation = _shieldPosition.rotation;
        }
        else
        {
            Vector3 forward = transform.forward;
            Vector3 shieldPos = transform.position + forward * 0.5f + Vector3.up * 1f;
            _shieldObject.transform.position = shieldPos;
            _shieldObject.transform.LookAt(transform.position + forward * 2f);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isBlocking) return;
        
        if (IsProjectile(other))
        {
            HandleProjectileBlock(other);
        }
        else if (other.CompareTag("Enemy"))
        {
            HandleEnemyBlock(other);
        }
    }
    
    private bool IsProjectile(Collider other)
    {
        return (_projectileLayer.value & (1 << other.gameObject.layer)) > 0 ||
               other.CompareTag("Projectile") ||
               other.GetComponent<Rigidbody>() != null;
    }
    
    private void HandleProjectileBlock(Collider other)
    {
        Vector3 directionToProjectile = (other.transform.position - transform.position).normalized;
        Vector3 shieldForward = _shieldObject != null ? _shieldObject.transform.forward : transform.forward;
        
        float angle = Vector3.Angle(-shieldForward, directionToProjectile);
        
        if (angle <= _blockAngle * 0.5f)
        {
            BlockProjectile(other);
        }
    }
    
    private void HandleEnemyBlock(Collider other)
    {
        Vector3 directionToEnemy = (other.transform.position - transform.position).normalized;
        Vector3 shieldForward = _shieldObject != null ? _shieldObject.transform.forward : transform.forward;
        
        float angle = Vector3.Angle(-shieldForward, directionToEnemy);
        
        if (angle <= _blockAngle * 0.5f)
        {
            BlockEnemyAttack(other);
        }
    }
    
    private void BlockProjectile(Collider projectile)
    {
        Rigidbody projectileRb = projectile.GetComponent<Rigidbody>();
        if (projectileRb != null)
        {
            Vector3 reflectDirection = Vector3.Reflect(projectileRb.velocity.normalized, 
                _shieldObject != null ? _shieldObject.transform.forward : transform.forward);
            projectileRb.velocity = reflectDirection * projectileRb.velocity.magnitude;
        }
        
        PlayDeflectEffects();
        OnSuccessfulBlock?.Invoke();
        
        Destroy(projectile.gameObject, 0.1f);
    }
    
    private void BlockEnemyAttack(Collider enemy)
    {
        Rigidbody enemyRb = enemy.GetComponent<Rigidbody>();
        if (enemyRb != null)
        {
            Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
            enemyRb.AddForce(knockbackDirection * 5f, ForceMode.Impulse);
        }
        
        PlayDeflectEffects();
        OnSuccessfulBlock?.Invoke();
    }
    
    private void PlayDeflectEffects()
    {
        if (_audioSource != null && _deflectSound != null)
        {
            _audioSource.PlayOneShot(_deflectSound);
        }
        
        if (_deflectEffect != null)
        {
            _deflectEffect.Play();
        }
    }
    
    public bool IsBlocking()
    {
        return _isBlocking;
    }
    
    public bool CanBlock()
    {
        return _canBlock;
    }
    
    public float GetCooldownProgress()
    {
        return _canBlock ? 1f : _cooldownTimer / _blockCooldown;
    }
}