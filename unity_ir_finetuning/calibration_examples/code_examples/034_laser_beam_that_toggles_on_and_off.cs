// Prompt: laser beam that toggles on and off
// Type: general

using UnityEngine;

public class LaserBeam : MonoBehaviour
{
    [Header("Laser Settings")]
    [SerializeField] private bool _startActive = true;
    [SerializeField] private float _toggleInterval = 2f;
    [SerializeField] private float _damage = 10f;
    [SerializeField] private LayerMask _damageableLayers = -1;
    
    [Header("Visual Components")]
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private ParticleSystem _startEffect;
    [SerializeField] private ParticleSystem _endEffect;
    [SerializeField] private Light _laserLight;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    [SerializeField] private AudioClip _loopSound;
    
    [Header("Raycast Settings")]
    [SerializeField] private float _maxDistance = 100f;
    [SerializeField] private float _damageInterval = 0.1f;
    
    private bool _isActive;
    private float _toggleTimer;
    private float _damageTimer;
    private Collider _currentTarget;
    private Vector3 _hitPoint;
    
    private void Start()
    {
        InitializeComponents();
        _isActive = _startActive;
        _toggleTimer = _toggleInterval;
        UpdateLaserState();
    }
    
    private void InitializeComponents()
    {
        if (_lineRenderer == null)
            _lineRenderer = GetComponent<LineRenderer>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_startPoint == null)
            _startPoint = transform;
            
        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.useWorldSpace = true;
        }
    }
    
    private void Update()
    {
        HandleToggleTimer();
        
        if (_isActive)
        {
            UpdateLaserRaycast();
            HandleDamage();
        }
    }
    
    private void HandleToggleTimer()
    {
        if (_toggleInterval > 0)
        {
            _toggleTimer -= Time.deltaTime;
            if (_toggleTimer <= 0)
            {
                ToggleLaser();
                _toggleTimer = _toggleInterval;
            }
        }
    }
    
    private void UpdateLaserRaycast()
    {
        Vector3 startPos = _startPoint.position;
        Vector3 direction = _endPoint != null ? (_endPoint.position - startPos).normalized : transform.forward;
        
        RaycastHit hit;
        if (Physics.Raycast(startPos, direction, out hit, _maxDistance, _damageableLayers))
        {
            _hitPoint = hit.point;
            _currentTarget = hit.collider;
        }
        else
        {
            _hitPoint = startPos + direction * _maxDistance;
            _currentTarget = null;
        }
        
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (_lineRenderer != null)
        {
            _lineRenderer.SetPosition(0, _startPoint.position);
            _lineRenderer.SetPosition(1, _hitPoint);
            _lineRenderer.enabled = _isActive;
        }
        
        if (_startEffect != null)
        {
            if (_isActive && !_startEffect.isPlaying)
                _startEffect.Play();
            else if (!_isActive && _startEffect.isPlaying)
                _startEffect.Stop();
        }
        
        if (_endEffect != null)
        {
            _endEffect.transform.position = _hitPoint;
            if (_isActive && !_endEffect.isPlaying)
                _endEffect.Play();
            else if (!_isActive && _endEffect.isPlaying)
                _endEffect.Stop();
        }
        
        if (_laserLight != null)
            _laserLight.enabled = _isActive;
    }
    
    private void HandleDamage()
    {
        if (_currentTarget == null || _damage <= 0)
            return;
            
        _damageTimer += Time.deltaTime;
        if (_damageTimer >= _damageInterval)
        {
            ApplyDamage(_currentTarget);
            _damageTimer = 0f;
        }
    }
    
    private void ApplyDamage(Collider target)
    {
        if (target.CompareTag("Player"))
        {
            // Apply damage to player using standard Unity components
            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                Vector3 knockback = (_hitPoint - _startPoint.position).normalized * _damage * 0.1f;
                rigidbody.AddForce(knockback, ForceMode.Impulse);
            }
        }
        
        // Send damage message to any component that might handle it
        target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        target.SendMessage("OnLaserHit", _damage, SendMessageOptions.DontRequireReceiver);
    }
    
    public void ToggleLaser()
    {
        _isActive = !_isActive;
        UpdateLaserState();
    }
    
    public void SetLaserActive(bool active)
    {
        _isActive = active;
        UpdateLaserState();
    }
    
    private void UpdateLaserState()
    {
        if (_audioSource != null)
        {
            if (_isActive)
            {
                if (_activationSound != null)
                    _audioSource.PlayOneShot(_activationSound);
                    
                if (_loopSound != null)
                {
                    _audioSource.clip = _loopSound;
                    _audioSource.loop = true;
                    _audioSource.Play();
                }
            }
            else
            {
                if (_deactivationSound != null)
                    _audioSource.PlayOneShot(_deactivationSound);
                    
                if (_audioSource.isPlaying && _audioSource.clip == _loopSound)
                    _audioSource.Stop();
            }
        }
        
        UpdateVisuals();
        _damageTimer = 0f;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_startPoint != null)
        {
            Gizmos.color = _isActive ? Color.red : Color.gray;
            Vector3 direction = _endPoint != null ? (_endPoint.position - _startPoint.position).normalized : transform.forward;
            Gizmos.DrawRay(_startPoint.position, direction * _maxDistance);
        }
    }
    
    private void OnDisable()
    {
        if (_startEffect != null && _startEffect.isPlaying)
            _startEffect.Stop();
            
        if (_endEffect != null && _endEffect.isPlaying)
            _endEffect.Stop();
            
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
    }
}