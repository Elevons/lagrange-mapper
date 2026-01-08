// Prompt: sword that swings with animation
// Type: combat

using UnityEngine;

public class SwordController : MonoBehaviour
{
    [Header("Sword Settings")]
    [SerializeField] private float _swingDuration = 0.5f;
    [SerializeField] private float _swingAngle = 90f;
    [SerializeField] private AnimationCurve _swingCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private KeyCode _swingKey = KeyCode.Mouse0;
    
    [Header("Combat")]
    [SerializeField] private float _damage = 10f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private LayerMask _enemyLayers = 1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _swingSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Effects")]
    [SerializeField] private GameObject _slashEffect;
    [SerializeField] private Transform _effectSpawnPoint;
    
    private bool _isSwinging = false;
    private float _swingTimer = 0f;
    private Vector3 _initialRotation;
    private AudioSource _audioSource;
    private Animator _animator;
    private bool _hasDealtDamage = false;
    
    private void Start()
    {
        _initialRotation = transform.localEulerAngles;
        _audioSource = GetComponent<AudioSource>();
        _animator = GetComponent<Animator>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_effectSpawnPoint == null)
        {
            _effectSpawnPoint = transform;
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateSwingAnimation();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_swingKey) && !_isSwinging)
        {
            StartSwing();
        }
    }
    
    private void StartSwing()
    {
        _isSwinging = true;
        _swingTimer = 0f;
        _hasDealtDamage = false;
        
        if (_animator != null)
        {
            _animator.SetTrigger("Swing");
        }
        
        PlaySwingSound();
        SpawnSlashEffect();
    }
    
    private void UpdateSwingAnimation()
    {
        if (!_isSwinging) return;
        
        _swingTimer += Time.deltaTime;
        float progress = _swingTimer / _swingDuration;
        
        if (progress >= 1f)
        {
            progress = 1f;
            _isSwinging = false;
        }
        
        float curveValue = _swingCurve.Evaluate(progress);
        float currentAngle = curveValue * _swingAngle;
        
        Vector3 rotation = _initialRotation;
        rotation.z += currentAngle;
        transform.localEulerAngles = rotation;
        
        // Check for hits during the middle portion of the swing
        if (progress >= 0.3f && progress <= 0.7f && !_hasDealtDamage)
        {
            CheckForHits();
        }
        
        // Return to initial position when swing is complete
        if (!_isSwinging)
        {
            transform.localEulerAngles = _initialRotation;
        }
    }
    
    private void CheckForHits()
    {
        Vector3 attackPosition = transform.position + transform.forward * (_attackRange * 0.5f);
        Collider[] hitColliders = Physics.OverlapSphere(attackPosition, _attackRange, _enemyLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject != gameObject && 
                hitCollider.transform.parent != transform.parent)
            {
                DealDamage(hitCollider.gameObject);
                _hasDealtDamage = true;
                break;
            }
        }
    }
    
    private void DealDamage(GameObject target)
    {
        // Try to find common health components
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Send damage message - target can implement this method
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        }
        
        // Apply knockback
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackDirection = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(knockbackDirection * 5f, ForceMode.Impulse);
        }
        
        PlayHitSound();
        
        // Destroy objects tagged as destructible
        if (target.CompareTag("Destructible"))
        {
            Destroy(target);
        }
    }
    
    private void PlaySwingSound()
    {
        if (_audioSource != null && _swingSound != null)
        {
            _audioSource.PlayOneShot(_swingSound);
        }
    }
    
    private void PlayHitSound()
    {
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    private void SpawnSlashEffect()
    {
        if (_slashEffect != null && _effectSpawnPoint != null)
        {
            GameObject effect = Instantiate(_slashEffect, _effectSpawnPoint.position, _effectSpawnPoint.rotation);
            Destroy(effect, 2f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw attack range
        Gizmos.color = Color.red;
        Vector3 attackPosition = transform.position + transform.forward * (_attackRange * 0.5f);
        Gizmos.DrawWireSphere(attackPosition, _attackRange);
        
        // Draw swing arc
        Gizmos.color = Color.yellow;
        Vector3 startDirection = Quaternion.Euler(0, 0, -_swingAngle * 0.5f) * transform.right;
        Vector3 endDirection = Quaternion.Euler(0, 0, _swingAngle * 0.5f) * transform.right;
        
        Gizmos.DrawRay(transform.position, startDirection * _attackRange);
        Gizmos.DrawRay(transform.position, endDirection * _attackRange);
    }
}