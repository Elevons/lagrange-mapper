// Prompt: fish that jumps from water
// Type: movement

using UnityEngine;

public class JumpingFish : MonoBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float _jumpForce = 10f;
    [SerializeField] private float _jumpInterval = 3f;
    [SerializeField] private float _jumpVariation = 1f;
    [SerializeField] private float _waterLevel = 0f;
    [SerializeField] private AnimationCurve _jumpCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Movement")]
    [SerializeField] private float _swimSpeed = 2f;
    [SerializeField] private float _swimRadius = 5f;
    [SerializeField] private bool _randomSwimming = true;
    
    [Header("Animation")]
    [SerializeField] private float _rotationSpeed = 180f;
    [SerializeField] private float _splashEffectDuration = 0.5f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _splashEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _jumpSound;
    [SerializeField] private AudioClip _splashSound;
    
    private Rigidbody _rigidbody;
    private Vector3 _startPosition;
    private Vector3 _swimTarget;
    private float _nextJumpTime;
    private bool _isJumping;
    private bool _isInWater;
    private float _jumpStartTime;
    private Vector3 _jumpStartPosition;
    private Vector3 _jumpTargetPosition;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _startPosition = transform.position;
        _swimTarget = _startPosition;
        _nextJumpTime = Time.time + Random.Range(_jumpInterval - _jumpVariation, _jumpInterval + _jumpVariation);
        _isInWater = transform.position.y <= _waterLevel;
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        SetupRigidbody();
        GenerateNewSwimTarget();
    }
    
    private void SetupRigidbody()
    {
        _rigidbody.drag = _isInWater ? 5f : 0.5f;
        _rigidbody.angularDrag = 3f;
        _rigidbody.useGravity = !_isInWater;
    }
    
    private void Update()
    {
        CheckWaterState();
        HandleJumping();
        HandleSwimming();
        HandleRotation();
    }
    
    private void CheckWaterState()
    {
        bool wasInWater = _isInWater;
        _isInWater = transform.position.y <= _waterLevel;
        
        if (wasInWater != _isInWater)
        {
            SetupRigidbody();
            
            if (_isInWater)
            {
                OnEnterWater();
            }
            else
            {
                OnExitWater();
            }
        }
    }
    
    private void OnEnterWater()
    {
        PlaySplashEffect();
        PlaySound(_splashSound);
        _isJumping = false;
        GenerateNewSwimTarget();
    }
    
    private void OnExitWater()
    {
        PlaySplashEffect();
        PlaySound(_jumpSound);
    }
    
    private void HandleJumping()
    {
        if (_isInWater && !_isJumping && Time.time >= _nextJumpTime)
        {
            StartJump();
        }
    }
    
    private void StartJump()
    {
        _isJumping = true;
        _jumpStartTime = Time.time;
        _jumpStartPosition = transform.position;
        
        Vector3 jumpDirection = Vector3.up + Random.insideUnitSphere * 0.3f;
        jumpDirection.y = Mathf.Abs(jumpDirection.y);
        jumpDirection.Normalize();
        
        _rigidbody.velocity = jumpDirection * _jumpForce;
        
        _nextJumpTime = Time.time + Random.Range(_jumpInterval - _jumpVariation, _jumpInterval + _jumpVariation);
    }
    
    private void HandleSwimming()
    {
        if (!_isInWater || _isJumping) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, _swimTarget);
        
        if (distanceToTarget < 0.5f)
        {
            GenerateNewSwimTarget();
        }
        
        Vector3 direction = (_swimTarget - transform.position).normalized;
        direction.y = 0f;
        
        _rigidbody.AddForce(direction * _swimSpeed, ForceMode.Force);
        
        Vector3 velocity = _rigidbody.velocity;
        velocity.y = Mathf.Lerp(velocity.y, 0f, Time.deltaTime * 2f);
        _rigidbody.velocity = velocity;
    }
    
    private void GenerateNewSwimTarget()
    {
        if (_randomSwimming)
        {
            Vector2 randomCircle = Random.insideUnitCircle * _swimRadius;
            _swimTarget = _startPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }
        else
        {
            float angle = Time.time * 0.5f;
            _swimTarget = _startPosition + new Vector3(
                Mathf.Cos(angle) * _swimRadius,
                0f,
                Mathf.Sin(angle) * _swimRadius
            );
        }
        
        _swimTarget.y = _waterLevel - 0.5f;
    }
    
    private void HandleRotation()
    {
        Vector3 targetDirection;
        
        if (_isInWater && !_isJumping)
        {
            targetDirection = (_swimTarget - transform.position).normalized;
            if (targetDirection.magnitude < 0.1f)
            {
                targetDirection = transform.forward;
            }
        }
        else
        {
            targetDirection = _rigidbody.velocity.normalized;
            if (targetDirection.magnitude < 0.1f)
            {
                targetDirection = transform.forward;
            }
        }
        
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * Time.deltaTime
            );
        }
    }
    
    private void PlaySplashEffect()
    {
        if (_splashEffect != null)
        {
            _splashEffect.transform.position = new Vector3(transform.position.x, _waterLevel, transform.position.z);
            _splashEffect.Play();
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
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(_startPosition, _swimRadius);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(new Vector3(-10f, _waterLevel, -10f), new Vector3(10f, _waterLevel, 10f));
        
        if (_isInWater)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _swimTarget);
        }
    }
}