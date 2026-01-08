// Prompt: wind zone that pushes objects
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class WindZone : MonoBehaviour
{
    [Header("Wind Properties")]
    [SerializeField] private float _windStrength = 10f;
    [SerializeField] private Vector3 _windDirection = Vector3.forward;
    [SerializeField] private bool _normalizeDirection = true;
    
    [Header("Wind Variation")]
    [SerializeField] private bool _enableTurbulence = true;
    [SerializeField] private float _turbulenceStrength = 2f;
    [SerializeField] private float _turbulenceFrequency = 1f;
    [SerializeField] private bool _enableGustiness = false;
    [SerializeField] private float _gustStrength = 5f;
    [SerializeField] private float _gustFrequency = 0.5f;
    
    [Header("Zone Settings")]
    [SerializeField] private bool _useColliderBounds = true;
    [SerializeField] private LayerMask _affectedLayers = -1;
    [SerializeField] private bool _affectRigidbodies = true;
    [SerializeField] private bool _affectParticles = true;
    
    [Header("Force Application")]
    [SerializeField] private ForceMode _forceMode = ForceMode.Force;
    [SerializeField] private bool _useDistanceFalloff = true;
    [SerializeField] private AnimationCurve _falloffCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    
    [Header("Debug")]
    [SerializeField] private bool _showGizmos = true;
    [SerializeField] private Color _gizmoColor = Color.cyan;
    
    private Collider _zoneCollider;
    private List<Rigidbody> _affectedRigidbodies = new List<Rigidbody>();
    private List<ParticleSystem> _affectedParticles = new List<ParticleSystem>();
    private Vector3 _normalizedDirection;
    private float _turbulenceOffset;
    private float _gustOffset;
    
    private void Start()
    {
        _zoneCollider = GetComponent<Collider>();
        if (_zoneCollider == null)
        {
            Debug.LogWarning("WindZone requires a Collider component to define the wind area.");
        }
        else
        {
            _zoneCollider.isTrigger = true;
        }
        
        UpdateWindDirection();
        
        _turbulenceOffset = Random.Range(0f, 100f);
        _gustOffset = Random.Range(0f, 100f);
    }
    
    private void Update()
    {
        if (_normalizeDirection)
        {
            UpdateWindDirection();
        }
        
        ApplyWindForces();
    }
    
    private void UpdateWindDirection()
    {
        _normalizedDirection = _windDirection.normalized;
    }
    
    private void ApplyWindForces()
    {
        float currentTime = Time.time;
        Vector3 finalWindDirection = _normalizedDirection;
        float finalWindStrength = _windStrength;
        
        // Apply turbulence
        if (_enableTurbulence)
        {
            Vector3 turbulence = new Vector3(
                Mathf.PerlinNoise(currentTime * _turbulenceFrequency + _turbulenceOffset, 0f) - 0.5f,
                Mathf.PerlinNoise(currentTime * _turbulenceFrequency + _turbulenceOffset + 100f, 0f) - 0.5f,
                Mathf.PerlinNoise(currentTime * _turbulenceFrequency + _turbulenceOffset + 200f, 0f) - 0.5f
            );
            
            finalWindDirection += turbulence * _turbulenceStrength * 0.1f;
            finalWindDirection = finalWindDirection.normalized;
        }
        
        // Apply gustiness
        if (_enableGustiness)
        {
            float gustMultiplier = 1f + (Mathf.PerlinNoise(currentTime * _gustFrequency + _gustOffset, 0f) - 0.5f) * _gustStrength * 0.2f;
            finalWindStrength *= gustMultiplier;
        }
        
        // Apply forces to rigidbodies
        if (_affectRigidbodies)
        {
            for (int i = _affectedRigidbodies.Count - 1; i >= 0; i--)
            {
                if (_affectedRigidbodies[i] == null)
                {
                    _affectedRigidbodies.RemoveAt(i);
                    continue;
                }
                
                ApplyWindToRigidbody(_affectedRigidbodies[i], finalWindDirection, finalWindStrength);
            }
        }
        
        // Apply forces to particle systems
        if (_affectParticles)
        {
            for (int i = _affectedParticles.Count - 1; i >= 0; i--)
            {
                if (_affectedParticles[i] == null)
                {
                    _affectedParticles.RemoveAt(i);
                    continue;
                }
                
                ApplyWindToParticles(_affectedParticles[i], finalWindDirection, finalWindStrength);
            }
        }
    }
    
    private void ApplyWindToRigidbody(Rigidbody rb, Vector3 windDirection, float windStrength)
    {
        if (rb == null) return;
        
        float distanceMultiplier = 1f;
        
        if (_useDistanceFalloff && _zoneCollider != null)
        {
            Vector3 closestPoint = _zoneCollider.ClosestPoint(rb.position);
            float distance = Vector3.Distance(rb.position, closestPoint);
            float maxDistance = GetMaxDistanceInCollider();
            
            if (maxDistance > 0f)
            {
                float normalizedDistance = Mathf.Clamp01(distance / maxDistance);
                distanceMultiplier = _falloffCurve.Evaluate(normalizedDistance);
            }
        }
        
        Vector3 force = windDirection * windStrength * distanceMultiplier * Time.deltaTime;
        rb.AddForce(force, _forceMode);
    }
    
    private void ApplyWindToParticles(ParticleSystem particles, Vector3 windDirection, float windStrength)
    {
        if (particles == null) return;
        
        var externalForces = particles.externalForces;
        if (externalForces.enabled)
        {
            // If external forces are already enabled, we can't directly control them
            // This is a limitation of Unity's particle system
            return;
        }
        
        var velocityOverLifetime = particles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        
        float effectiveStrength = windStrength * 0.1f; // Scale down for particles
        velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(windDirection.x * effectiveStrength);
        velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(windDirection.y * effectiveStrength);
        velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(windDirection.z * effectiveStrength);
    }
    
    private float GetMaxDistanceInCollider()
    {
        if (_zoneCollider == null) return 1f;
        
        Bounds bounds = _zoneCollider.bounds;
        return Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 0.5f;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsInAffectedLayers(other.gameObject)) return;
        
        if (_affectRigidbodies)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && !_affectedRigidbodies.Contains(rb))
            {
                _affectedRigidbodies.Add(rb);
            }
        }
        
        if (_affectParticles)
        {
            ParticleSystem particles = other.GetComponent<ParticleSystem>();
            if (particles != null && !_affectedParticles.Contains(particles))
            {
                _affectedParticles.Add(particles);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!IsInAffectedLayers(other.gameObject)) return;
        
        if (_affectRigidbodies)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null)
            {
                _affectedRigidbodies.Remove(rb);
            }
        }
        
        if (_affectParticles)
        {
            ParticleSystem particles = other.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                _affectedParticles.Remove(particles);
                
                // Reset particle velocity when leaving wind zone
                var velocityOverLifetime = particles.velocityOverLifetime;
                velocityOverLifetime.enabled = false;
            }
        }
    }
    
    private bool IsInAffectedLayers(GameObject obj)
    {
        return (_affectedLayers.value & (1 << obj.layer)) != 0;
    }
    
    public void SetWindDirection(Vector3 direction)
    {
        _windDirection = direction;
        UpdateWindDirection();
    }
    
    public void SetWindStrength(float strength)
    {
        _windStrength = Mathf.Max(0f, strength);
    }
    
    public Vector3 GetCurrentWindDirection()
    {
        return _normalizedDirection;
    }
    
    public float GetCurrentWindStrength()
    {
        return _windStrength;
    }
    
    private void OnDrawGizmos()
    {
        if (!_showGizmos) return;
        
        Gizmos.color = _gizmoColor;
        
        if (_zoneCollider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (_zoneCollider is BoxCollider box)
            {
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (_zoneCollider is SphereCollider sphere)
            {
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            else if (_zoneCollider is CapsuleCollider capsule)
            {
                Gizmos.DrawWireCube(capsule.center, new Vector3(capsule.radius * 2, capsule.height, capsule.radius * 2));
            }
        }
        
        // Draw wind direction arrows
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.yellow;
        
        Vector3 center = transform.position;
        Vector3 windDir = transform.TransformDirection(_windDirection.normalized);
        
        for (int i = 0; i < 5; i++)
        {
            Vector3 offset = Random.insideUnitSphere * 2f;
            offset.y = Mathf.Abs(offset.y);
            Vector3 start = center + offset;
            Vector3 end = start + windDir * 3f;
            
            Gizmos.DrawLine(start, end);
            
            // Arrow head
            Vector3 arrowHead1 = end - windDir * 0.5f + Vector3.Cross(windDir, Vector3.up) * 0.3f;
            Vector3 arrowHead2 = end - windDir * 0.5f - Vector3.Cross(windDir, Vector3.up) * 0.3f;
            Gizmos.DrawLine(end, arrowHead1);
            Gizmos.DrawLine(end, arrowHead2);
        }
    }
}