// Prompt: dragon that breathes fire
// Type: general

using UnityEngine;
using System.Collections;

public class Dragon : MonoBehaviour
{
    [Header("Dragon Settings")]
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 2f;
    
    [Header("Fire Breath")]
    [SerializeField] private Transform _fireBreathPoint;
    [SerializeField] private GameObject _fireBreathPrefab;
    [SerializeField] private ParticleSystem _fireParticles;
    [SerializeField] private float _fireBreathDuration = 3f;
    [SerializeField] private float _fireBreathCooldown = 5f;
    [SerializeField] private float _fireDamage = 25f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _roarSound;
    [SerializeField] private AudioClip _fireBreathSound;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    
    private Transform _target;
    private bool _isBreathingFire = false;
    private float _lastFireBreathTime = 0f;
    private Rigidbody _rigidbody;
    private DragonState _currentState = DragonState.Idle;
    
    private enum DragonState
    {
        Idle,
        Chasing,
        Attacking,
        BreathingFire
    }
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_fireBreathPoint == null)
        {
            GameObject firePoint = new GameObject("FireBreathPoint");
            firePoint.transform.SetParent(transform);
            firePoint.transform.localPosition = Vector3.forward * 2f;
            _fireBreathPoint = firePoint.transform;
        }
        
        _lastFireBreathTime = -_fireBreathCooldown;
    }
    
    private void Update()
    {
        FindTarget();
        UpdateState();
        HandleState();
        UpdateAnimations();
    }
    
    private void FindTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRange, _targetLayers);
        float closestDistance = Mathf.Infinity;
        Transform closestTarget = null;
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = col.transform;
                }
            }
        }
        
        _target = closestTarget;
    }
    
    private void UpdateState()
    {
        if (_target == null)
        {
            _currentState = DragonState.Idle;
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        
        if (_isBreathingFire)
        {
            _currentState = DragonState.BreathingFire;
        }
        else if (distanceToTarget <= _attackRange && Time.time >= _lastFireBreathTime + _fireBreathCooldown)
        {
            _currentState = DragonState.Attacking;
        }
        else if (distanceToTarget <= _detectionRange)
        {
            _currentState = DragonState.Chasing;
        }
        else
        {
            _currentState = DragonState.Idle;
        }
    }
    
    private void HandleState()
    {
        switch (_currentState)
        {
            case DragonState.Idle:
                HandleIdle();
                break;
            case DragonState.Chasing:
                HandleChasing();
                break;
            case DragonState.Attacking:
                HandleAttacking();
                break;
            case DragonState.BreathingFire:
                HandleFireBreathing();
                break;
        }
    }
    
    private void HandleIdle()
    {
        _rigidbody.velocity = Vector3.zero;
    }
    
    private void HandleChasing()
    {
        if (_target == null) return;
        
        Vector3 direction = (_target.position - transform.position).normalized;
        Vector3 targetRotation = Quaternion.LookRotation(direction).eulerAngles;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetRotation.y, 0), _rotationSpeed * Time.deltaTime);
        
        _rigidbody.velocity = transform.forward * _moveSpeed;
    }
    
    private void HandleAttacking()
    {
        if (_target == null) return;
        
        _rigidbody.velocity = Vector3.zero;
        
        Vector3 direction = (_target.position - transform.position).normalized;
        Vector3 targetRotation = Quaternion.LookRotation(direction).eulerAngles;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetRotation.y, 0), _rotationSpeed * 2f * Time.deltaTime);
        
        if (!_isBreathingFire)
        {
            StartCoroutine(BreatheFire());
        }
    }
    
    private void HandleFireBreathing()
    {
        if (_target == null) return;
        
        _rigidbody.velocity = Vector3.zero;
        
        Vector3 direction = (_target.position - transform.position).normalized;
        Vector3 targetRotation = Quaternion.LookRotation(direction).eulerAngles;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, targetRotation.y, 0), _rotationSpeed * Time.deltaTime);
    }
    
    private IEnumerator BreatheFire()
    {
        _isBreathingFire = true;
        _lastFireBreathTime = Time.time;
        
        PlayRoar();
        
        yield return new WaitForSeconds(0.5f);
        
        if (_fireParticles != null)
            _fireParticles.Play();
            
        if (_fireBreathPrefab != null)
        {
            GameObject fireBreath = Instantiate(_fireBreathPrefab, _fireBreathPoint.position, _fireBreathPoint.rotation);
            Destroy(fireBreath, _fireBreathDuration);
        }
        
        PlayFireBreathSound();
        
        float fireTimer = 0f;
        while (fireTimer < _fireBreathDuration)
        {
            DealFireDamage();
            fireTimer += Time.deltaTime;
            yield return null;
        }
        
        if (_fireParticles != null)
            _fireParticles.Stop();
            
        _isBreathingFire = false;
    }
    
    private void DealFireDamage()
    {
        Vector3 fireDirection = _fireBreathPoint.forward;
        RaycastHit[] hits = Physics.RaycastAll(_fireBreathPoint.position, fireDirection, _attackRange, _targetLayers);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.CompareTag("Player"))
            {
                Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
                if (targetRb != null)
                {
                    Vector3 knockback = (hit.collider.transform.position - transform.position).normalized * 5f;
                    targetRb.AddForce(knockback, ForceMode.Impulse);
                }
            }
        }
    }
    
    private void PlayRoar()
    {
        if (_audioSource != null && _roarSound != null)
        {
            _audioSource.PlayOneShot(_roarSound);
        }
    }
    
    private void PlayFireBreathSound()
    {
        if (_audioSource != null && _fireBreathSound != null)
        {
            _audioSource.PlayOneShot(_fireBreathSound);
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator == null) return;
        
        _animator.SetBool("IsMoving", _currentState == DragonState.Chasing);
        _animator.SetBool("IsAttacking", _currentState == DragonState.Attacking);
        _animator.SetBool("IsBreathingFire", _isBreathingFire);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        if (_fireBreathPoint != null)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawRay(_fireBreathPoint.position, _fireBreathPoint.forward * _attackRange);
        }
    }
}