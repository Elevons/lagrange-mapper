// Prompt: arrow trap that shoots from wall
// Type: environment

using UnityEngine;
using System.Collections;

public class ArrowTrap : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private LayerMask _targetLayerMask = -1;
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private Transform _detectionPoint;
    
    [Header("Arrow Settings")]
    [SerializeField] private GameObject _arrowPrefab;
    [SerializeField] private Transform _shootPoint;
    [SerializeField] private float _arrowSpeed = 20f;
    [SerializeField] private float _arrowLifetime = 5f;
    [SerializeField] private int _arrowDamage = 25;
    
    [Header("Timing Settings")]
    [SerializeField] private float _shootDelay = 0.5f;
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private bool _continuousFiring = false;
    [SerializeField] private float _continuousFireRate = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _shootSound;
    [SerializeField] private AudioClip _reloadSound;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _muzzleFlash;
    [SerializeField] private GameObject _warningIndicator;
    
    private bool _isOnCooldown = false;
    private bool _isFiring = false;
    private Coroutine _firingCoroutine;
    
    private void Start()
    {
        if (_detectionPoint == null)
            _detectionPoint = transform;
            
        if (_shootPoint == null)
            _shootPoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_warningIndicator != null)
            _warningIndicator.SetActive(false);
    }
    
    private void Update()
    {
        if (_isOnCooldown || _isFiring)
            return;
            
        DetectTargets();
    }
    
    private void DetectTargets()
    {
        Collider[] targets = Physics.OverlapSphere(_detectionPoint.position, _detectionRange, _targetLayerMask);
        
        foreach (Collider target in targets)
        {
            if (target.CompareTag(_targetTag))
            {
                Vector3 directionToTarget = (target.transform.position - _detectionPoint.position).normalized;
                
                if (Physics.Raycast(_detectionPoint.position, directionToTarget, out RaycastHit hit, _detectionRange))
                {
                    if (hit.collider == target)
                    {
                        StartFiring();
                        break;
                    }
                }
            }
        }
    }
    
    private void StartFiring()
    {
        if (_isFiring)
            return;
            
        _isFiring = true;
        
        if (_continuousFiring)
        {
            _firingCoroutine = StartCoroutine(ContinuousFire());
        }
        else
        {
            StartCoroutine(SingleShot());
        }
    }
    
    private IEnumerator SingleShot()
    {
        ShowWarning();
        yield return new WaitForSeconds(_shootDelay);
        
        ShootArrow();
        HideWarning();
        
        _isFiring = false;
        StartCoroutine(Cooldown());
    }
    
    private IEnumerator ContinuousFire()
    {
        ShowWarning();
        yield return new WaitForSeconds(_shootDelay);
        
        while (_isFiring && HasTargetsInRange())
        {
            ShootArrow();
            yield return new WaitForSeconds(1f / _continuousFireRate);
        }
        
        HideWarning();
        _isFiring = false;
        StartCoroutine(Cooldown());
    }
    
    private bool HasTargetsInRange()
    {
        Collider[] targets = Physics.OverlapSphere(_detectionPoint.position, _detectionRange, _targetLayerMask);
        
        foreach (Collider target in targets)
        {
            if (target.CompareTag(_targetTag))
            {
                Vector3 directionToTarget = (target.transform.position - _detectionPoint.position).normalized;
                
                if (Physics.Raycast(_detectionPoint.position, directionToTarget, out RaycastHit hit, _detectionRange))
                {
                    if (hit.collider == target)
                        return true;
                }
            }
        }
        
        return false;
    }
    
    private void ShootArrow()
    {
        if (_arrowPrefab == null || _shootPoint == null)
            return;
            
        GameObject arrow = Instantiate(_arrowPrefab, _shootPoint.position, _shootPoint.rotation);
        
        TrapArrow arrowScript = arrow.GetComponent<TrapArrow>();
        if (arrowScript == null)
            arrowScript = arrow.AddComponent<TrapArrow>();
            
        arrowScript.Initialize(_shootPoint.forward, _arrowSpeed, _arrowDamage, _arrowLifetime);
        
        PlayShootEffects();
    }
    
    private void PlayShootEffects()
    {
        if (_audioSource != null && _shootSound != null)
            _audioSource.PlayOneShot(_shootSound);
            
        if (_muzzleFlash != null)
            _muzzleFlash.Play();
    }
    
    private void ShowWarning()
    {
        if (_warningIndicator != null)
            _warningIndicator.SetActive(true);
    }
    
    private void HideWarning()
    {
        if (_warningIndicator != null)
            _warningIndicator.SetActive(false);
    }
    
    private IEnumerator Cooldown()
    {
        _isOnCooldown = true;
        
        if (_audioSource != null && _reloadSound != null)
            _audioSource.PlayOneShot(_reloadSound);
            
        yield return new WaitForSeconds(_cooldownTime);
        _isOnCooldown = false;
    }
    
    public void StopFiring()
    {
        if (_firingCoroutine != null)
        {
            StopCoroutine(_firingCoroutine);
            _firingCoroutine = null;
        }
        
        _isFiring = false;
        HideWarning();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_detectionPoint == null)
            _detectionPoint = transform;
            
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_detectionPoint.position, _detectionRange);
        
        if (_shootPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_shootPoint.position, _shootPoint.forward * 3f);
        }
    }
}

public class TrapArrow : MonoBehaviour
{
    private Vector3 _direction;
    private float _speed;
    private int _damage;
    private float _lifetime;
    private Rigidbody _rigidbody;
    private bool _hasHit = false;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        _rigidbody.useGravity = false;
    }
    
    public void Initialize(Vector3 direction, float speed, int damage, float lifetime)
    {
        _direction = direction.normalized;
        _speed = speed;
        _damage = damage;
        _lifetime = lifetime;
        
        _rigidbody.velocity = _direction * _speed;
        transform.rotation = Quaternion.LookRotation(_direction);
        
        Destroy(gameObject, _lifetime);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit)
            return;
            
        if (other.CompareTag("Player"))
        {
            _hasHit = true;
            
            // Try to find health component on player
            var healthComponent = other.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                // Use reflection to try calling common health methods
                var healthType = healthComponent.GetType();
                var takeDamageMethod = healthType.GetMethod("TakeDamage");
                if (takeDamageMethod != null)
                {
                    takeDamageMethod.Invoke(healthComponent, new object[] { _damage });
                }
            }
            
            StickToTarget(other.transform);
        }
        else if (!other.isTrigger)
        {
            _hasHit = true;
            StickToSurface();
        }
    }
    
    private void StickToTarget(Transform target)
    {
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        transform.SetParent(target);
        
        Destroy(gameObject, 2f);
    }
    
    private void StickToSurface()
    {
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        
        Destroy(gameObject, 5f);
    }
}