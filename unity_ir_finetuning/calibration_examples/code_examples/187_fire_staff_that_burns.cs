// Prompt: fire staff that burns
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FireStaff : MonoBehaviour
{
    [Header("Fire Staff Settings")]
    [SerializeField] private float _fireRange = 10f;
    [SerializeField] private float _fireDamage = 25f;
    [SerializeField] private float _burnDuration = 3f;
    [SerializeField] private float _burnDamagePerSecond = 5f;
    [SerializeField] private float _cooldownTime = 1f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _fireParticles;
    [SerializeField] private ParticleSystem _castEffect;
    [SerializeField] private LineRenderer _fireBeam;
    [SerializeField] private Light _fireLight;
    [SerializeField] private Transform _firePoint;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _castSound;
    [SerializeField] private AudioClip _hitSound;
    
    private bool _canCast = true;
    private Camera _playerCamera;
    private Dictionary<GameObject, BurnEffect> _burningTargets = new Dictionary<GameObject, BurnEffect>();
    
    [System.Serializable]
    public class BurnEffect
    {
        public float remainingTime;
        public Coroutine burnCoroutine;
        public ParticleSystem burnParticles;
        
        public BurnEffect(float duration)
        {
            remainingTime = duration;
        }
    }
    
    void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_firePoint == null)
            _firePoint = transform;
            
        if (_fireBeam != null)
        {
            _fireBeam.enabled = false;
            _fireBeam.startWidth = 0.1f;
            _fireBeam.endWidth = 0.05f;
        }
        
        if (_fireLight != null)
        {
            _fireLight.enabled = false;
            _fireLight.color = Color.red;
            _fireLight.intensity = 2f;
        }
    }
    
    void Update()
    {
        HandleInput();
        UpdateVisualEffects();
    }
    
    void HandleInput()
    {
        if (Input.GetButtonDown("Fire1") && _canCast)
        {
            CastFire();
        }
    }
    
    void CastFire()
    {
        if (_playerCamera == null) return;
        
        _canCast = false;
        
        Vector3 rayOrigin = _playerCamera.transform.position;
        Vector3 rayDirection = _playerCamera.transform.forward;
        
        if (_firePoint != null)
        {
            rayOrigin = _firePoint.position;
            rayDirection = (_playerCamera.transform.position + _playerCamera.transform.forward * _fireRange - _firePoint.position).normalized;
        }
        
        RaycastHit hit;
        Vector3 targetPoint;
        
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, _fireRange, _targetLayers))
        {
            targetPoint = hit.point;
            ProcessFireHit(hit);
        }
        else
        {
            targetPoint = rayOrigin + rayDirection * _fireRange;
        }
        
        StartCoroutine(FireEffectCoroutine(rayOrigin, targetPoint));
        StartCoroutine(CooldownCoroutine());
        
        PlayCastSound();
    }
    
    void ProcessFireHit(RaycastHit hit)
    {
        GameObject target = hit.collider.gameObject;
        
        // Apply immediate damage
        ApplyDamage(target, _fireDamage);
        
        // Apply burn effect
        ApplyBurnEffect(target);
        
        PlayHitSound();
    }
    
    void ApplyDamage(GameObject target, float damage)
    {
        // Try different common health component patterns
        var health = target.GetComponent<MonoBehaviour>();
        if (health != null)
        {
            // Use SendMessage as a fallback for unknown health systems
            target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        }
        
        // If target has Rigidbody, add force
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (target.transform.position - transform.position).normalized;
            rb.AddForce(forceDirection * damage * 0.1f, ForceMode.Impulse);
        }
    }
    
    void ApplyBurnEffect(GameObject target)
    {
        if (_burningTargets.ContainsKey(target))
        {
            // Refresh burn duration
            _burningTargets[target].remainingTime = _burnDuration;
            return;
        }
        
        BurnEffect burnEffect = new BurnEffect(_burnDuration);
        
        // Create burn particles
        if (_fireParticles != null)
        {
            GameObject burnParticleObj = Instantiate(_fireParticles.gameObject, target.transform.position, Quaternion.identity);
            burnEffect.burnParticles = burnParticleObj.GetComponent<ParticleSystem>();
            burnParticleObj.transform.SetParent(target.transform);
        }
        
        burnEffect.burnCoroutine = StartCoroutine(BurnCoroutine(target, burnEffect));
        _burningTargets[target] = burnEffect;
    }
    
    IEnumerator BurnCoroutine(GameObject target, BurnEffect burnEffect)
    {
        while (burnEffect.remainingTime > 0 && target != null)
        {
            ApplyDamage(target, _burnDamagePerSecond);
            burnEffect.remainingTime -= 1f;
            yield return new WaitForSeconds(1f);
        }
        
        // Clean up
        if (_burningTargets.ContainsKey(target))
        {
            if (burnEffect.burnParticles != null)
                Destroy(burnEffect.burnParticles.gameObject);
            _burningTargets.Remove(target);
        }
    }
    
    IEnumerator FireEffectCoroutine(Vector3 startPoint, Vector3 endPoint)
    {
        // Enable visual effects
        if (_fireBeam != null)
        {
            _fireBeam.enabled = true;
            _fireBeam.SetPosition(0, startPoint);
            _fireBeam.SetPosition(1, endPoint);
        }
        
        if (_fireLight != null)
        {
            _fireLight.enabled = true;
        }
        
        if (_castEffect != null)
        {
            _castEffect.Play();
        }
        
        yield return new WaitForSeconds(0.1f);
        
        // Disable visual effects
        if (_fireBeam != null)
            _fireBeam.enabled = false;
            
        if (_fireLight != null)
            _fireLight.enabled = false;
    }
    
    IEnumerator CooldownCoroutine()
    {
        yield return new WaitForSeconds(_cooldownTime);
        _canCast = true;
    }
    
    void UpdateVisualEffects()
    {
        if (_fireParticles != null)
        {
            if (_canCast)
            {
                if (!_fireParticles.isPlaying)
                    _fireParticles.Play();
            }
            else
            {
                if (_fireParticles.isPlaying)
                    _fireParticles.Stop();
            }
        }
    }
    
    void PlayCastSound()
    {
        if (_audioSource != null && _castSound != null)
        {
            _audioSource.PlayOneShot(_castSound);
        }
    }
    
    void PlayHitSound()
    {
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }
    
    void OnDestroy()
    {
        // Clean up all burning effects
        foreach (var kvp in _burningTargets)
        {
            if (kvp.Value.burnCoroutine != null)
                StopCoroutine(kvp.Value.burnCoroutine);
            if (kvp.Value.burnParticles != null)
                Destroy(kvp.Value.burnParticles.gameObject);
        }
        _burningTargets.Clear();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 origin = _firePoint != null ? _firePoint.position : transform.position;
        Vector3 direction = transform.forward;
        
        if (_playerCamera != null)
            direction = _playerCamera.transform.forward;
            
        Gizmos.DrawRay(origin, direction * _fireRange);
    }
}