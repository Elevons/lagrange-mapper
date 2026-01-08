// Prompt: water current that pushes player
// Type: movement

using UnityEngine;

public class WaterCurrent : MonoBehaviour
{
    [Header("Current Settings")]
    [SerializeField] private Vector3 _currentDirection = Vector3.forward;
    [SerializeField] private float _currentStrength = 5f;
    [SerializeField] private bool _useLocalDirection = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _particleEffect;
    [SerializeField] private bool _showGizmos = true;
    [SerializeField] private Color _gizmoColor = Color.cyan;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _waterFlowClip;
    [SerializeField] private float _audioVolume = 0.5f;
    
    [Header("Force Settings")]
    [SerializeField] private ForceMode _forceMode = ForceMode.Force;
    [SerializeField] private float _maxVelocity = 10f;
    [SerializeField] private bool _limitVelocity = true;
    
    private Collider _currentCollider;
    
    private void Start()
    {
        _currentCollider = GetComponent<Collider>();
        if (_currentCollider == null)
        {
            Debug.LogWarning("WaterCurrent requires a Collider component to function properly.");
        }
        
        if (_currentCollider != null && !_currentCollider.isTrigger)
        {
            _currentCollider.isTrigger = true;
        }
        
        SetupAudio();
        SetupParticles();
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_audioSource != null && _waterFlowClip != null)
        {
            _audioSource.clip = _waterFlowClip;
            _audioSource.loop = true;
            _audioSource.volume = _audioVolume;
            _audioSource.Play();
        }
    }
    
    private void SetupParticles()
    {
        if (_particleEffect != null)
        {
            var main = _particleEffect.main;
            main.loop = true;
            
            var velocityOverLifetime = _particleEffect.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            
            Vector3 particleDirection = _useLocalDirection ? transform.TransformDirection(_currentDirection) : _currentDirection;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = particleDirection.x * _currentStrength * 0.5f;
            velocityOverLifetime.y = particleDirection.y * _currentStrength * 0.5f;
            velocityOverLifetime.z = particleDirection.z * _currentStrength * 0.5f;
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ApplyCurrentForce(other);
        }
    }
    
    private void ApplyCurrentForce(Collider target)
    {
        Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();
        if (targetRigidbody == null) return;
        
        Vector3 forceDirection = _useLocalDirection ? transform.TransformDirection(_currentDirection.normalized) : _currentDirection.normalized;
        Vector3 force = forceDirection * _currentStrength;
        
        if (_limitVelocity)
        {
            Vector3 currentVelocity = targetRigidbody.velocity;
            Vector3 projectedVelocity = Vector3.Project(currentVelocity, forceDirection);
            
            if (projectedVelocity.magnitude < _maxVelocity)
            {
                targetRigidbody.AddForce(force, _forceMode);
            }
        }
        else
        {
            targetRigidbody.AddForce(force, _forceMode);
        }
    }
    
    private void OnValidate()
    {
        if (_currentStrength < 0f)
            _currentStrength = 0f;
            
        if (_maxVelocity < 0f)
            _maxVelocity = 0f;
            
        if (_audioVolume < 0f)
            _audioVolume = 0f;
        else if (_audioVolume > 1f)
            _audioVolume = 1f;
            
        if (Application.isPlaying)
        {
            SetupParticles();
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!_showGizmos) return;
        
        Gizmos.color = _gizmoColor;
        
        Vector3 center = transform.position;
        Vector3 direction = _useLocalDirection ? transform.TransformDirection(_currentDirection.normalized) : _currentDirection.normalized;
        
        Gizmos.DrawRay(center, direction * _currentStrength);
        
        Gizmos.color = new Color(_gizmoColor.r, _gizmoColor.g, _gizmoColor.b, 0.3f);
        if (_currentCollider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (_currentCollider is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (_currentCollider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
        
        Gizmos.matrix = Matrix4x4.identity;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos) return;
        
        Gizmos.color = Color.white;
        Vector3 center = transform.position;
        Vector3 direction = _useLocalDirection ? transform.TransformDirection(_currentDirection.normalized) : _currentDirection.normalized;
        
        for (int i = 0; i < 5; i++)
        {
            float offset = i * 0.5f;
            Gizmos.DrawRay(center + direction * offset, direction * 0.3f);
        }
    }
}