// Prompt: swimming dive
// Type: general

using UnityEngine;

public class SwimmingDive : MonoBehaviour
{
    [Header("Dive Settings")]
    [SerializeField] private float _diveForce = 15f;
    [SerializeField] private float _diveAngle = 45f;
    [SerializeField] private float _underwaterDrag = 5f;
    [SerializeField] private float _surfaceDrag = 1f;
    [SerializeField] private float _buoyancyForce = 10f;
    [SerializeField] private float _maxDiveDepth = 20f;
    
    [Header("Water Detection")]
    [SerializeField] private LayerMask _waterLayer = 1;
    [SerializeField] private Transform _waterCheckPoint;
    [SerializeField] private float _waterCheckRadius = 0.5f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _diveKey = KeyCode.Space;
    [SerializeField] private KeyCode _surfaceKey = KeyCode.LeftShift;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _diveAnimationTrigger = "Dive";
    [SerializeField] private string _swimAnimationBool = "IsSwimming";
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _splashEffect;
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _splashSound;
    [SerializeField] private AudioClip _underwaterSound;
    
    private Rigidbody _rigidbody;
    private bool _isUnderwater;
    private bool _canDive = true;
    private float _originalDrag;
    private float _waterSurfaceY;
    private Vector3 _diveStartPosition;
    private float _currentDepth;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            Debug.LogError("SwimmingDive requires a Rigidbody component!");
            enabled = false;
            return;
        }
        
        _originalDrag = _rigidbody.drag;
        
        if (_waterCheckPoint == null)
            _waterCheckPoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        CheckWaterStatus();
        HandleInput();
        UpdateAnimations();
        ApplyBuoyancy();
        UpdateDepth();
    }
    
    private void CheckWaterStatus()
    {
        bool wasUnderwater = _isUnderwater;
        _isUnderwater = Physics.CheckSphere(_waterCheckPoint.position, _waterCheckRadius, _waterLayer);
        
        if (_isUnderwater != wasUnderwater)
        {
            OnWaterStatusChanged(_isUnderwater);
        }
        
        if (_isUnderwater)
        {
            _rigidbody.drag = _underwaterDrag;
            
            Collider waterCollider = Physics.OverlapSphere(_waterCheckPoint.position, _waterCheckRadius, _waterLayer)[0];
            if (waterCollider != null)
            {
                _waterSurfaceY = waterCollider.bounds.max.y;
            }
        }
        else
        {
            _rigidbody.drag = _originalDrag;
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_diveKey) && _canDive)
        {
            if (_isUnderwater)
            {
                PerformUnderwaterDive();
            }
            else
            {
                PerformSurfaceDive();
            }
        }
        
        if (Input.GetKey(_surfaceKey) && _isUnderwater)
        {
            SwimToSurface();
        }
    }
    
    private void PerformSurfaceDive()
    {
        Vector3 diveDirection = Quaternion.AngleAxis(-_diveAngle, transform.right) * transform.forward;
        _rigidbody.AddForce(diveDirection * _diveForce, ForceMode.Impulse);
        
        _diveStartPosition = transform.position;
        _canDive = false;
        
        if (_animator != null)
            _animator.SetTrigger(_diveAnimationTrigger);
            
        if (_splashEffect != null && !_isUnderwater)
            _splashEffect.Play();
            
        PlaySound(_splashSound);
        
        Invoke(nameof(ResetDiveAbility), 1f);
    }
    
    private void PerformUnderwaterDive()
    {
        Vector3 diveDirection = -transform.up;
        _rigidbody.AddForce(diveDirection * _diveForce * 0.7f, ForceMode.Impulse);
        
        _canDive = false;
        Invoke(nameof(ResetDiveAbility), 0.5f);
    }
    
    private void SwimToSurface()
    {
        Vector3 surfaceDirection = Vector3.up;
        _rigidbody.AddForce(surfaceDirection * _buoyancyForce * 1.5f, ForceMode.Force);
    }
    
    private void ApplyBuoyancy()
    {
        if (_isUnderwater)
        {
            float buoyancyMultiplier = Mathf.Clamp01(_currentDepth / _maxDiveDepth);
            Vector3 buoyancyForceVector = Vector3.up * _buoyancyForce * buoyancyMultiplier;
            _rigidbody.AddForce(buoyancyForceVector, ForceMode.Force);
            
            if (_bubbleEffect != null && !_bubbleEffect.isPlaying)
                _bubbleEffect.Play();
        }
        else
        {
            if (_bubbleEffect != null && _bubbleEffect.isPlaying)
                _bubbleEffect.Stop();
        }
    }
    
    private void UpdateDepth()
    {
        if (_isUnderwater)
        {
            _currentDepth = Mathf.Max(0, _waterSurfaceY - transform.position.y);
            
            if (_currentDepth > _maxDiveDepth)
            {
                Vector3 position = transform.position;
                position.y = _waterSurfaceY - _maxDiveDepth;
                transform.position = position;
                _currentDepth = _maxDiveDepth;
            }
        }
        else
        {
            _currentDepth = 0f;
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator != null)
        {
            _animator.SetBool(_swimAnimationBool, _isUnderwater);
        }
    }
    
    private void OnWaterStatusChanged(bool enteredWater)
    {
        if (enteredWater)
        {
            PlaySound(_underwaterSound);
            if (_splashEffect != null)
                _splashEffect.Play();
        }
        else
        {
            if (_audioSource != null && _audioSource.clip == _underwaterSound)
                _audioSource.Stop();
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
        }
    }
    
    private void ResetDiveAbility()
    {
        _canDive = true;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_waterCheckPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_waterCheckPoint.position, _waterCheckRadius);
        }
        
        Gizmos.color = Color.red;
        Vector3 diveDirection = Quaternion.AngleAxis(-_diveAngle, transform.right) * transform.forward;
        Gizmos.DrawRay(transform.position, diveDirection * 3f);
    }
}