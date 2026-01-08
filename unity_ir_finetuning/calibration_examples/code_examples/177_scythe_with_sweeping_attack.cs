// Prompt: scythe with sweeping attack
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class ScytheWeapon : MonoBehaviour
{
    [Header("Attack Settings")]
    [SerializeField] private float _attackDamage = 50f;
    [SerializeField] private float _attackRange = 3f;
    [SerializeField] private float _sweepAngle = 120f;
    [SerializeField] private float _attackDuration = 0.5f;
    [SerializeField] private float _attackCooldown = 1f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Animation")]
    [SerializeField] private Transform _scytheModel;
    [SerializeField] private float _sweepSpeed = 360f;
    [SerializeField] private AnimationCurve _sweepCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _slashEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _swooshSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Events")]
    public UnityEvent<float> OnDamageDealt;
    public UnityEvent OnAttackStarted;
    public UnityEvent OnAttackCompleted;
    
    private bool _isAttacking = false;
    private bool _canAttack = true;
    private float _lastAttackTime;
    private Vector3 _originalRotation;
    private HashSet<Collider> _hitTargets = new HashSet<Collider>();
    
    private void Start()
    {
        if (_scytheModel == null)
            _scytheModel = transform;
            
        _originalRotation = _scytheModel.localEulerAngles;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCooldown();
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && _canAttack)
        {
            StartAttack();
        }
    }
    
    private void UpdateCooldown()
    {
        if (!_canAttack && Time.time >= _lastAttackTime + _attackCooldown)
        {
            _canAttack = true;
        }
    }
    
    public void StartAttack()
    {
        if (_isAttacking || !_canAttack) return;
        
        StartCoroutine(PerformSweepAttack());
    }
    
    private IEnumerator PerformSweepAttack()
    {
        _isAttacking = true;
        _canAttack = false;
        _lastAttackTime = Time.time;
        _hitTargets.Clear();
        
        OnAttackStarted?.Invoke();
        
        if (_audioSource != null && _swooshSound != null)
            _audioSource.PlayOneShot(_swooshSound);
            
        if (_slashEffect != null)
            _slashEffect.Play();
        
        float elapsedTime = 0f;
        float startAngle = -_sweepAngle * 0.5f;
        float endAngle = _sweepAngle * 0.5f;
        
        while (elapsedTime < _attackDuration)
        {
            float progress = elapsedTime / _attackDuration;
            float curveValue = _sweepCurve.Evaluate(progress);
            float currentAngle = Mathf.Lerp(startAngle, endAngle, curveValue);
            
            _scytheModel.localEulerAngles = _originalRotation + new Vector3(0, 0, currentAngle);
            
            CheckForTargets(currentAngle);
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        _scytheModel.localEulerAngles = _originalRotation;
        _isAttacking = false;
        
        OnAttackCompleted?.Invoke();
    }
    
    private void CheckForTargets(float currentAngle)
    {
        Vector3 attackDirection = Quaternion.Euler(0, 0, currentAngle) * transform.right;
        Vector3 attackOrigin = transform.position;
        
        Collider[] hitColliders = Physics.OverlapSphere(attackOrigin, _attackRange, _targetLayers);
        
        foreach (Collider hitCollider in hitColliders)
        {
            if (_hitTargets.Contains(hitCollider) || hitCollider.transform == transform)
                continue;
                
            Vector3 directionToTarget = (hitCollider.transform.position - attackOrigin).normalized;
            float angleToTarget = Vector3.Angle(transform.right, directionToTarget);
            
            if (angleToTarget <= _sweepAngle * 0.5f)
            {
                DealDamage(hitCollider);
                _hitTargets.Add(hitCollider);
            }
        }
    }
    
    private void DealDamage(Collider target)
    {
        if (_audioSource != null && _hitSound != null)
            _audioSource.PlayOneShot(_hitSound);
        
        // Try to damage using common health interfaces
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use reflection to call common damage methods
            var damageMethod = healthComponent.GetType().GetMethod("TakeDamage");
            if (damageMethod != null)
            {
                damageMethod.Invoke(healthComponent, new object[] { _attackDamage });
            }
            else
            {
                // Try alternative method names
                var altMethod = healthComponent.GetType().GetMethod("Damage");
                if (altMethod != null)
                {
                    altMethod.Invoke(healthComponent, new object[] { _attackDamage });
                }
            }
        }
        
        // Apply knockback
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackDirection = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(knockbackDirection * 10f, ForceMode.Impulse);
        }
        
        OnDamageDealt?.Invoke(_attackDamage);
        
        // Destroy objects tagged as destructible
        if (target.CompareTag("Destructible"))
        {
            Destroy(target.gameObject);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        // Draw sweep arc
        Vector3 leftBound = Quaternion.Euler(0, 0, -_sweepAngle * 0.5f) * transform.right * _attackRange;
        Vector3 rightBound = Quaternion.Euler(0, 0, _sweepAngle * 0.5f) * transform.right * _attackRange;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + leftBound);
        Gizmos.DrawLine(transform.position, transform.position + rightBound);
        
        // Draw arc
        for (int i = 0; i < 20; i++)
        {
            float angle1 = Mathf.Lerp(-_sweepAngle * 0.5f, _sweepAngle * 0.5f, i / 19f);
            float angle2 = Mathf.Lerp(-_sweepAngle * 0.5f, _sweepAngle * 0.5f, (i + 1) / 19f);
            
            Vector3 point1 = transform.position + Quaternion.Euler(0, 0, angle1) * transform.right * _attackRange;
            Vector3 point2 = transform.position + Quaternion.Euler(0, 0, angle2) * transform.right * _attackRange;
            
            Gizmos.DrawLine(point1, point2);
        }
    }
}