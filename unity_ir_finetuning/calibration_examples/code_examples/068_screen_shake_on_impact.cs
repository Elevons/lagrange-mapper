// Prompt: screen shake on impact
// Type: general

using UnityEngine;
using System.Collections;

public class ScreenShake : MonoBehaviour
{
    [Header("Shake Settings")]
    [SerializeField] private float _shakeDuration = 0.5f;
    [SerializeField] private float _shakeIntensity = 1.0f;
    [SerializeField] private AnimationCurve _shakeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Impact Detection")]
    [SerializeField] private bool _shakeOnCollision = true;
    [SerializeField] private bool _shakeOnTrigger = false;
    [SerializeField] private string[] _impactTags = { "Player", "Enemy", "Projectile" };
    [SerializeField] private float _minimumImpactForce = 5.0f;
    
    [Header("Manual Shake")]
    [SerializeField] private KeyCode _testShakeKey = KeyCode.Space;
    
    private Vector3 _originalPosition;
    private bool _isShaking = false;
    private Camera _camera;
    private Rigidbody _rigidbody;
    
    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = Camera.main;
            
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_camera != null)
            _originalPosition = _camera.transform.localPosition;
        else
            _originalPosition = transform.localPosition;
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(_testShakeKey))
        {
            TriggerShake(_shakeIntensity, _shakeDuration);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!_shakeOnCollision) return;
        
        if (ShouldShakeFromImpact(collision.gameObject, collision.relativeVelocity.magnitude))
        {
            float intensity = Mathf.Clamp(collision.relativeVelocity.magnitude / 10f, 0.1f, _shakeIntensity);
            TriggerShake(intensity, _shakeDuration);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_shakeOnTrigger) return;
        
        float impactForce = _minimumImpactForce;
        Rigidbody otherRb = other.GetComponent<Rigidbody>();
        if (otherRb != null)
            impactForce = otherRb.velocity.magnitude;
            
        if (ShouldShakeFromImpact(other.gameObject, impactForce))
        {
            float intensity = Mathf.Clamp(impactForce / 10f, 0.1f, _shakeIntensity);
            TriggerShake(intensity, _shakeDuration);
        }
    }
    
    private bool ShouldShakeFromImpact(GameObject impactObject, float force)
    {
        if (force < _minimumImpactForce) return false;
        
        if (_impactTags.Length == 0) return true;
        
        foreach (string tag in _impactTags)
        {
            if (impactObject.CompareTag(tag))
                return true;
        }
        
        return false;
    }
    
    public void TriggerShake(float intensity, float duration)
    {
        if (_isShaking) return;
        
        StartCoroutine(ShakeCoroutine(intensity, duration));
    }
    
    public void TriggerShake()
    {
        TriggerShake(_shakeIntensity, _shakeDuration);
    }
    
    private IEnumerator ShakeCoroutine(float intensity, float duration)
    {
        _isShaking = true;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float currentIntensity = intensity * _shakeCurve.Evaluate(progress);
            
            Vector3 randomOffset = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                0f
            ) * currentIntensity;
            
            if (_camera != null)
                _camera.transform.localPosition = _originalPosition + randomOffset;
            else
                transform.localPosition = _originalPosition + randomOffset;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        if (_camera != null)
            _camera.transform.localPosition = _originalPosition;
        else
            transform.localPosition = _originalPosition;
            
        _isShaking = false;
    }
    
    private void OnDisable()
    {
        StopAllCoroutines();
        
        if (_camera != null)
            _camera.transform.localPosition = _originalPosition;
        else
            transform.localPosition = _originalPosition;
            
        _isShaking = false;
    }
}