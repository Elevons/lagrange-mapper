// Prompt: wind tunnel
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class WindTunnel : MonoBehaviour
{
    [Header("Wind Settings")]
    [SerializeField] private float _windForce = 10f;
    [SerializeField] private Vector3 _windDirection = Vector3.forward;
    [SerializeField] private bool _useLocalDirection = true;
    [SerializeField] private float _maxWindDistance = 20f;
    [SerializeField] private AnimationCurve _forceFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    
    [Header("Affected Objects")]
    [SerializeField] private LayerMask _affectedLayers = -1;
    [SerializeField] private string[] _affectedTags = { "Player", "Debris", "Projectile" };
    [SerializeField] private bool _affectRigidbodies = true;
    [SerializeField] private bool _affectParticles = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _windParticles;
    [SerializeField] private bool _createWindParticles = true;
    [SerializeField] private int _particleCount = 100;
    [SerializeField] private float _particleSpeed = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _windSound;
    [SerializeField] private float _baseVolume = 0.5f;
    [SerializeField] private float _basePitch = 1f;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private float _updateFrequency = 0.1f;
    
    private List<Rigidbody> _affectedRigidbodies = new List<Rigidbody>();
    private List<ParticleSystem> _affectedParticleSystems = new List<ParticleSystem>();
    private float _lastUpdateTime;
    private Vector3 _worldWindDirection;
    
    private void Start()
    {
        SetupAudio();
        SetupParticles();
        UpdateWindDirection();
    }
    
    private void Update()
    {
        UpdateWindDirection();
        
        if (Time.time - _lastUpdateTime >= _updateFrequency)
        {
            DetectAffectedObjects();
            _lastUpdateTime = Time.time;
        }
        
        ApplyWindForces();
        UpdateAudioEffects();
    }
    
    private void UpdateWindDirection()
    {
        _worldWindDirection = _useLocalDirection ? transform.TransformDirection(_windDirection.normalized) : _windDirection.normalized;
    }
    
    private void DetectAffectedObjects()
    {
        _affectedRigidbodies.Clear();
        _affectedParticleSystems.Clear();
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRadius, _affectedLayers);
        
        foreach (Collider col in colliders)
        {
            if (!IsValidTarget(col.gameObject))
                continue;
                
            float distance = Vector3.Distance(transform.position, col.transform.position);
            if (distance > _maxWindDistance)
                continue;
            
            if (_affectRigidbodies)
            {
                Rigidbody rb = col.GetComponent<Rigidbody>();
                if (rb != null && !_affectedRigidbodies.Contains(rb))
                {
                    _affectedRigidbodies.Add(rb);
                }
            }
            
            if (_affectParticles)
            {
                ParticleSystem ps = col.GetComponent<ParticleSystem>();
                if (ps != null && !_affectedParticleSystems.Contains(ps))
                {
                    _affectedParticleSystems.Add(ps);
                }
            }
        }
    }
    
    private bool IsValidTarget(GameObject target)
    {
        if (_affectedTags.Length > 0)
        {
            foreach (string tag in _affectedTags)
            {
                if (target.CompareTag(tag))
                    return true;
            }
            return false;
        }
        return true;
    }
    
    private void ApplyWindForces()
    {
        foreach (Rigidbody rb in _affectedRigidbodies)
        {
            if (rb == null) continue;
            
            Vector3 directionToObject = rb.transform.position - transform.position;
            float distance = directionToObject.magnitude;
            
            if (distance > _maxWindDistance) continue;
            
            float normalizedDistance = distance / _maxWindDistance;
            float forceMultiplier = _forceFalloff.Evaluate(normalizedDistance);
            
            Vector3 windForceVector = _worldWindDirection * _windForce * forceMultiplier;
            
            // Apply drag factor based on object's velocity opposing wind
            float dragFactor = Mathf.Clamp01(1f - Vector3.Dot(rb.velocity.normalized, _worldWindDirection));
            windForceVector *= (0.5f + dragFactor * 0.5f);
            
            rb.AddForce(windForceVector, ForceMode.Force);
        }
        
        foreach (ParticleSystem ps in _affectedParticleSystems)
        {
            if (ps == null) continue;
            
            var velocityOverLifetime = ps.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            
            float distance = Vector3.Distance(transform.position, ps.transform.position);
            float normalizedDistance = distance / _maxWindDistance;
            float forceMultiplier = _forceFalloff.Evaluate(normalizedDistance);
            
            Vector3 particleWind = _worldWindDirection * _windForce * forceMultiplier * 0.1f;
            velocityOverLifetime.x = particleWind.x;
            velocityOverLifetime.y = particleWind.y;
            velocityOverLifetime.z = particleWind.z;
        }
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_windSound != null)
        {
            _audioSource.clip = _windSound;
            _audioSource.loop = true;
            _audioSource.volume = _baseVolume;
            _audioSource.pitch = _basePitch;
            _audioSource.Play();
        }
    }
    
    private void SetupParticles()
    {
        if (_createWindParticles && _windParticles == null)
        {
            GameObject particleObject = new GameObject("WindParticles");
            particleObject.transform.SetParent(transform);
            particleObject.transform.localPosition = Vector3.zero;
            
            _windParticles = particleObject.AddComponent<ParticleSystem>();
            
            var main = _windParticles.main;
            main.startLifetime = 2f;
            main.startSpeed = _particleSpeed;
            main.maxParticles = _particleCount;
            main.startSize = 0.1f;
            main.startColor = new Color(1f, 1f, 1f, 0.3f);
            
            var emission = _windParticles.emission;
            emission.rateOverTime = _particleCount * 0.5f;
            
            var shape = _windParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(_detectionRadius, _detectionRadius, 1f);
            
            var velocityOverLifetime = _windParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        }
        
        if (_windParticles != null)
        {
            var velocityOverLifetime = _windParticles.velocityOverLifetime;
            Vector3 particleVelocity = _worldWindDirection * _particleSpeed;
            velocityOverLifetime.x = particleVelocity.x;
            velocityOverLifetime.y = particleVelocity.y;
            velocityOverLifetime.z = particleVelocity.z;
        }
    }
    
    private void UpdateAudioEffects()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            float intensity = Mathf.Clamp01(_affectedRigidbodies.Count / 10f);
            _audioSource.volume = _baseVolume * (0.5f + intensity * 0.5f);
            _audioSource.pitch = _basePitch * (0.8f + intensity * 0.4f);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _maxWindDistance);
        
        Gizmos.color = Color.red;
        Vector3 windDir = _useLocalDirection ? transform.TransformDirection(_windDirection.normalized) : _windDirection.normalized;
        Gizmos.DrawRay(transform.position, windDir * _maxWindDistance);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
    }
    
    public void SetWindForce(float force)
    {
        _windForce = force;
    }
    
    public void SetWindDirection(Vector3 direction)
    {
        _windDirection = direction.normalized;
        UpdateWindDirection();
    }
    
    public void ToggleWind(bool enabled)
    {
        this.enabled = enabled;
        
        if (_windParticles != null)
        {
            if (enabled)
                _windParticles.Play();
            else
                _windParticles.Stop();
        }
        
        if (_audioSource != null)
        {
            if (enabled)
                _audioSource.Play();
            else
                _audioSource.Stop();
        }
    }
}