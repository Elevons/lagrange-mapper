// Prompt: robot with laser attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class LaserRobot : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _attackRange = 8f;
    [SerializeField] private LayerMask _targetLayerMask = -1;
    [SerializeField] private LayerMask _obstacleLayerMask = -1;
    
    [Header("Laser Settings")]
    [SerializeField] private Transform _laserOrigin;
    [SerializeField] private LineRenderer _laserRenderer;
    [SerializeField] private float _laserDamage = 25f;
    [SerializeField] private float _laserDuration = 0.5f;
    [SerializeField] private float _laserCooldown = 2f;
    [SerializeField] private float _laserChargeTime = 1f;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _patrolRadius = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _laserChargeSound;
    [SerializeField] private AudioClip _laserFireSound;
    [SerializeField] private AudioClip _detectionSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _laserChargeEffect;
    [SerializeField] private ParticleSystem _laserImpactEffect;
    [SerializeField] private Light _laserLight;
    
    [Header("Events")]
    public UnityEvent OnTargetDetected;
    public UnityEvent OnLaserFired;
    public UnityEvent OnTargetLost;
    
    private enum RobotState
    {
        Patrolling,
        Chasing,
        Charging,
        Firing,
        Cooldown
    }
    
    private RobotState _currentState = RobotState.Patrolling;
    private Transform _currentTarget;
    private Vector3 _patrolCenter;
    private Vector3 _patrolDestination;
    private float _lastLaserTime;
    private float _chargeStartTime;
    private bool _hasLineOfSight;
    private Rigidbody _rigidbody;
    private Animator _animator;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _patrolCenter = transform.position;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_laserRenderer == null)
            _laserRenderer = GetComponent<LineRenderer>();
            
        if (_laserOrigin == null)
            _laserOrigin = transform;
            
        SetupLaserRenderer();
        SetNewPatrolDestination();
        
        _lastLaserTime = -_laserCooldown;
    }
    
    private void Update()
    {
        DetectTargets();
        UpdateStateMachine();
        UpdateLaserVisuals();
        UpdateAnimations();
    }
    
    private void FixedUpdate()
    {
        HandleMovement();
    }
    
    private void DetectTargets()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, _detectionRange, _targetLayerMask);
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;
        
        foreach (Collider target in targets)
        {
            if (target.CompareTag("Player"))
            {
                float distance = Vector3.Distance(transform.position, target.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target.transform;
                }
            }
        }
        
        if (closestTarget != null)
        {
            _hasLineOfSight = HasLineOfSight(closestTarget);
            
            if (_hasLineOfSight && _currentTarget == null)
            {
                _currentTarget = closestTarget;
                OnTargetDetected?.Invoke();
                PlaySound(_detectionSound);
            }
            else if (_hasLineOfSight)
            {
                _currentTarget = closestTarget;
            }
        }
        else if (_currentTarget != null)
        {
            _currentTarget = null;
            _hasLineOfSight = false;
            OnTargetLost?.Invoke();
        }
    }
    
    private bool HasLineOfSight(Transform target)
    {
        Vector3 direction = target.position - _laserOrigin.position;
        RaycastHit hit;
        
        if (Physics.Raycast(_laserOrigin.position, direction.normalized, out hit, direction.magnitude, _obstacleLayerMask))
        {
            return hit.collider.transform == target;
        }
        
        return true;
    }
    
    private void UpdateStateMachine()
    {
        switch (_currentState)
        {
            case RobotState.Patrolling:
                HandlePatrolling();
                break;
                
            case RobotState.Chasing:
                HandleChasing();
                break;
                
            case RobotState.Charging:
                HandleCharging();
                break;
                
            case RobotState.Firing:
                HandleFiring();
                break;
                
            case RobotState.Cooldown:
                HandleCooldown();
                break;
        }
    }
    
    private void HandlePatrolling()
    {
        if (_currentTarget != null && _hasLineOfSight)
        {
            _currentState = RobotState.Chasing;
            return;
        }
        
        if (Vector3.Distance(transform.position, _patrolDestination) < 1f)
        {
            SetNewPatrolDestination();
        }
    }
    
    private void HandleChasing()
    {
        if (_currentTarget == null || !_hasLineOfSight)
        {
            _currentState = RobotState.Patrolling;
            SetNewPatrolDestination();
            return;
        }
        
        float distanceToTarget = Vector3.Distance(transform.position, _currentTarget.position);
        
        if (distanceToTarget <= _attackRange && Time.time >= _lastLaserTime + _laserCooldown)
        {
            _currentState = RobotState.Charging;
            _chargeStartTime = Time.time;
            
            if (_laserChargeEffect != null)
                _laserChargeEffect.Play();
                
            PlaySound(_laserChargeSound);
        }
    }
    
    private void HandleCharging()
    {
        if (_currentTarget == null || !_hasLineOfSight)
        {
            _currentState = RobotState.Patrolling;
            
            if (_laserChargeEffect != null)
                _laserChargeEffect.Stop();
                
            return;
        }
        
        LookAtTarget(_currentTarget.position);
        
        if (Time.time >= _chargeStartTime + _laserChargeTime)
        {
            _currentState = RobotState.Firing;
            FireLaser();
        }
    }
    
    private void HandleFiring()
    {
        if (Time.time >= _lastLaserTime + _laserDuration)
        {
            _currentState = RobotState.Cooldown;
            DisableLaser();
        }
    }
    
    private void HandleCooldown()
    {
        if (Time.time >= _lastLaserTime + _laserCooldown)
        {
            if (_currentTarget != null && _hasLineOfSight)
            {
                _currentState = RobotState.Chasing;
            }
            else
            {
                _currentState = RobotState.Patrolling;
                SetNewPatrolDestination();
            }
        }
    }
    
    private void HandleMovement()
    {
        if (_rigidbody == null) return;
        
        Vector3 targetPosition = Vector3.zero;
        
        switch (_currentState)
        {
            case RobotState.Patrolling:
                targetPosition = _patrolDestination;
                break;
                
            case RobotState.Chasing:
                if (_currentTarget != null)
                    targetPosition = _currentTarget.position;
                break;
                
            case RobotState.Charging:
            case RobotState.Firing:
            case RobotState.Cooldown:
                return;
        }
        
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        
        if (direction.magnitude > 0.1f)
        {
            _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
            LookAtTarget(targetPosition);
        }
    }
    
    private void LookAtTarget(Vector3 targetPosition)
    {
        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;
        
        if (direction.magnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void SetNewPatrolDestination()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _patrolRadius;
        _patrolDestination = _patrolCenter + new Vector3(randomCircle.x, 0, randomCircle.y);
    }
    
    private void FireLaser()
    {
        if (_currentTarget == null) return;
        
        _lastLaserTime = Time.time;
        OnLaserFired?.Invoke();
        PlaySound(_laserFireSound);
        
        Vector3 laserDirection = (_currentTarget.position - _laserOrigin.position).normalized;
        RaycastHit hit;
        
        if (Physics.Raycast(_laserOrigin.position, laserDirection, out hit, _attackRange))
        {
            if (hit.collider.CompareTag("Player"))
            {
                // Apply damage to player
                var playerRigidbody = hit.collider.GetComponent<Rigidbody>();
                if (playerRigidbody != null)
                {
                    playerRigidbody.AddForce(laserDirection * 500f);
                }
                
                // Send damage message
                hit.collider.SendMessage("TakeDamage", _laserDamage, SendMessageOptions.DontRequireReceiver);
            }
            
            ShowLaserEffect(_laserOrigin.position, hit.point);
            
            if (_laserImpactEffect != null)
            {
                _laserImpactEffect.transform.position = hit.point;
                _laserImpactEffect.Play();
            }
        }
        else
        {
            Vector3 endPoint = _laserOrigin.position + laserDirection * _attackRange;
            ShowLaserEffect(_laserOrigin.position, endPoint);
        }
    }
    
    private void ShowLaserEffect(Vector3 startPoint, Vector3 endPoint)
    {
        if (_laserRenderer != null)
        {
            _laserRenderer.enabled = true;
            _laserRenderer.SetPosition(0, startPoint);
            _laserRenderer.SetPosition(1, endPoint);
        }
        
        if (_laserLight != null)
        {
            _laserLight.enabled = true;
        }
    }
    
    private void DisableLaser()
    {
        if (_laserRenderer != null)
            _laserRenderer.enabled = false;
            
        if (_laserLight != null)
            _laserLight.enabled = false;
            
        if (_laserChargeEffect != null)
            _laserChargeEffect.Stop();
    }
    
    private void UpdateLaserVisuals()
    {
        if (_laserRenderer != null && _laserRenderer.enabled)
        {
            float alpha = Mathf.PingPong(Time.time * 10f, 1f);
            Color laserColor = _laserRenderer.material.color;
            laserColor.a = alpha;
            _laserRenderer.material.color = laserColor;
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator == null) return;
        
        _animator.SetBool("IsMoving", _currentState == RobotState.Patrolling || _currentState == RobotState.Chasing);
        _animator.SetBool("IsCharging", _currentState == RobotState.Charging);
        _animator.SetBool("IsFiring", _currentState == RobotState.Firing);
        _animator.SetBool("HasTarget", _currentTarget != null);
    }
    
    private void SetupLaserRenderer()
    {
        if (_laserRenderer != null)
        {
            _laserRenderer.positionCount = 2;
            _laserRenderer.startWidth = 0.1f;
            _laserRenderer.endWidth = 0.05f;
            _laserRenderer.enabled = false;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_patrolCenter, _patrolRadius);
        
        if (_currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentTarget.position);
        }
    }
}