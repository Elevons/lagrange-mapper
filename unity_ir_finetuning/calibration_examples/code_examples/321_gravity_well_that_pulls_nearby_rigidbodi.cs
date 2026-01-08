// Prompt: gravity well that pulls nearby rigidbodies toward it with increasing force based on distance, plays a low-frequency rumble that gets louder as objects get closer, changes the light intensity based on how many objects are currently being pulled, and creates particle effects at the point where objects collide with the center
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class GravityWell : MonoBehaviour
{
    [Header("Gravity Settings")]
    [SerializeField] private float _gravitationalForce = 100f;
    [SerializeField] private float _maxRange = 20f;
    [SerializeField] private AnimationCurve _forceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rumbleClip;
    [SerializeField] private float _baseVolume = 0.1f;
    [SerializeField] private float _maxVolume = 0.8f;
    [SerializeField] private float _basePitch = 0.5f;
    [SerializeField] private float _maxPitch = 1.2f;
    
    [Header("Light Settings")]
    [SerializeField] private Light _wellLight;
    [SerializeField] private float _baseLightIntensity = 1f;
    [SerializeField] private float _intensityPerObject = 0.5f;
    [SerializeField] private float _maxLightIntensity = 10f;
    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem _collisionParticles;
    [SerializeField] private float _collisionThreshold = 2f;
    [SerializeField] private float _particleEmissionRate = 50f;
    
    [Header("Visual Effects")]
    [SerializeField] private Transform _centerPoint;
    [SerializeField] private bool _showGizmos = true;
    
    private List<Rigidbody> _affectedBodies = new List<Rigidbody>();
    private Dictionary<Rigidbody, float> _lastDistances = new Dictionary<Rigidbody, float>();
    private Collider[] _nearbyColliders = new Collider[50];
    private float _currentAverageDistance = 0f;
    
    private void Start()
    {
        if (_centerPoint == null)
            _centerPoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        SetupAudioSource();
        
        if (_wellLight == null)
            _wellLight = GetComponent<Light>();
            
        if (_collisionParticles == null)
            _collisionParticles = GetComponentInChildren<ParticleSystem>();
            
        SetupParticleSystem();
    }
    
    private void SetupAudioSource()
    {
        _audioSource.clip = _rumbleClip;
        _audioSource.loop = true;
        _audioSource.volume = 0f;
        _audioSource.pitch = _basePitch;
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Logarithmic;
        _audioSource.maxDistance = _maxRange * 2f;
        
        if (_rumbleClip != null)
            _audioSource.Play();
    }
    
    private void SetupParticleSystem()
    {
        if (_collisionParticles != null)
        {
            var emission = _collisionParticles.emission;
            emission.enabled = false;
            
            var shape = _collisionParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
        }
    }
    
    private void FixedUpdate()
    {
        UpdateAffectedBodies();
        ApplyGravitationalForces();
        UpdateAudioEffects();
        UpdateLightIntensity();
        CheckForCollisions();
    }
    
    private void UpdateAffectedBodies()
    {
        _affectedBodies.Clear();
        
        int numColliders = Physics.OverlapSphereNonAlloc(
            _centerPoint.position, 
            _maxRange, 
            _nearbyColliders, 
            _affectedLayers
        );
        
        for (int i = 0; i < numColliders; i++)
        {
            Rigidbody rb = _nearbyColliders[i].GetComponent<Rigidbody>();
            if (rb != null && rb.gameObject != gameObject)
            {
                _affectedBodies.Add(rb);
            }
        }
    }
    
    private void ApplyGravitationalForces()
    {
        float totalDistance = 0f;
        int validBodies = 0;
        
        foreach (Rigidbody rb in _affectedBodies)
        {
            if (rb == null) continue;
            
            Vector3 direction = _centerPoint.position - rb.position;
            float distance = direction.magnitude;
            
            if (distance > _maxRange) continue;
            
            float normalizedDistance = distance / _maxRange;
            float forceMultiplier = _forceCurve.Evaluate(1f - normalizedDistance);
            
            Vector3 force = direction.normalized * (_gravitationalForce * forceMultiplier * rb.mass);
            rb.AddForce(force, ForceMode.Force);
            
            _lastDistances[rb] = distance;
            totalDistance += distance;
            validBodies++;
        }
        
        _currentAverageDistance = validBodies > 0 ? totalDistance / validBodies : _maxRange;
    }
    
    private void UpdateAudioEffects()
    {
        if (_audioSource == null || _rumbleClip == null) return;
        
        int objectCount = _affectedBodies.Count;
        
        if (objectCount > 0)
        {
            float normalizedDistance = Mathf.Clamp01(_currentAverageDistance / _maxRange);
            float volumeMultiplier = 1f - normalizedDistance;
            
            float targetVolume = Mathf.Lerp(_baseVolume, _maxVolume, volumeMultiplier);
            targetVolume *= Mathf.Clamp01(objectCount / 5f);
            
            float targetPitch = Mathf.Lerp(_basePitch, _maxPitch, volumeMultiplier);
            
            _audioSource.volume = Mathf.Lerp(_audioSource.volume, targetVolume, Time.fixedDeltaTime * 2f);
            _audioSource.pitch = Mathf.Lerp(_audioSource.pitch, targetPitch, Time.fixedDeltaTime * 2f);
        }
        else
        {
            _audioSource.volume = Mathf.Lerp(_audioSource.volume, 0f, Time.fixedDeltaTime * 3f);
            _audioSource.pitch = Mathf.Lerp(_audioSource.pitch, _basePitch, Time.fixedDeltaTime * 2f);
        }
    }
    
    private void UpdateLightIntensity()
    {
        if (_wellLight == null) return;
        
        float targetIntensity = _baseLightIntensity + (_affectedBodies.Count * _intensityPerObject);
        targetIntensity = Mathf.Clamp(targetIntensity, _baseLightIntensity, _maxLightIntensity);
        
        _wellLight.intensity = Mathf.Lerp(_wellLight.intensity, targetIntensity, Time.fixedDeltaTime * 2f);
    }
    
    private void CheckForCollisions()
    {
        foreach (Rigidbody rb in _affectedBodies)
        {
            if (rb == null) continue;
            
            float distance = Vector3.Distance(rb.position, _centerPoint.position);
            
            if (distance <= _collisionThreshold)
            {
                TriggerCollisionEffect(rb.position);
            }
        }
    }
    
    private void TriggerCollisionEffect(Vector3 collisionPoint)
    {
        if (_collisionParticles == null) return;
        
        _collisionParticles.transform.position = collisionPoint;
        
        var emission = _collisionParticles.emission;
        emission.enabled = true;
        
        var emissionRate = _collisionParticles.emission;
        emissionRate.rateOverTime = _particleEmissionRate;
        
        _collisionParticles.Emit((int)(_particleEmissionRate * Time.fixedDeltaTime));
        
        Invoke(nameof(StopParticleEmission), 0.1f);
    }
    
    private void StopParticleEmission()
    {
        if (_collisionParticles != null)
        {
            var emission = _collisionParticles.emission;
            emission.enabled = false;
        }
    }
    
    private void OnDrawGizmos()
    {
        if (!_showGizmos) return;
        
        Vector3 center = _centerPoint != null ? _centerPoint.position : transform.position;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(center, _maxRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, _collisionThreshold);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawSphere(center, 0.2f);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos) return;
        
        Vector3 center = _centerPoint != null ? _centerPoint.position : transform.position;
        
        Gizmos.color = new Color(1f, 1f, 0f, 0.1f);
        Gizmos.DrawSphere(center, _maxRange);
        
        Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
        Gizmos.DrawSphere(center, _collisionThreshold);
    }
}