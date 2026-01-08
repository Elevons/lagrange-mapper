// Prompt: crumbling platform that falls after stepping
// Type: environment

using UnityEngine;
using System.Collections;

public class CrumblingPlatform : MonoBehaviour
{
    [Header("Crumbling Settings")]
    [SerializeField] private float _crumbleDelay = 1.0f;
    [SerializeField] private float _fallSpeed = 5.0f;
    [SerializeField] private float _shakeIntensity = 0.1f;
    [SerializeField] private float _shakeDuration = 0.8f;
    [SerializeField] private float _respawnDelay = 5.0f;
    [SerializeField] private bool _respawnAfterFall = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _crumbleSound;
    [SerializeField] private AudioClip _fallSound;
    
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private bool _isTriggered = false;
    private bool _isFalling = false;
    private Rigidbody _rigidbody;
    private Collider _platformCollider;
    private AudioSource _audioSource;
    private Coroutine _crumbleCoroutine;
    
    private void Start()
    {
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.isKinematic = true;
        
        _platformCollider = GetComponent<Collider>();
        if (_platformCollider == null)
        {
            _platformCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isTriggered && !_isFalling && other.CompareTag("Player"))
        {
            TriggerCrumble();
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (!_isTriggered && !_isFalling && collision.gameObject.CompareTag("Player"))
        {
            TriggerCrumble();
        }
    }
    
    private void TriggerCrumble()
    {
        _isTriggered = true;
        
        if (_crumbleCoroutine != null)
        {
            StopCoroutine(_crumbleCoroutine);
        }
        
        _crumbleCoroutine = StartCoroutine(CrumbleSequence());
    }
    
    private IEnumerator CrumbleSequence()
    {
        // Play crumble sound
        if (_crumbleSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_crumbleSound);
        }
        
        // Shake effect
        float shakeTimer = 0f;
        Vector3 shakePosition = _originalPosition;
        
        while (shakeTimer < _shakeDuration)
        {
            Vector3 randomOffset = Random.insideUnitSphere * _shakeIntensity;
            randomOffset.y = Mathf.Abs(randomOffset.y) * 0.5f; // Reduce vertical shake
            transform.position = shakePosition + randomOffset;
            
            shakeTimer += Time.deltaTime;
            yield return null;
        }
        
        // Wait for crumble delay
        yield return new WaitForSeconds(_crumbleDelay - _shakeDuration);
        
        // Start falling
        StartFalling();
    }
    
    private void StartFalling()
    {
        _isFalling = true;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        
        // Add downward force
        _rigidbody.AddForce(Vector3.down * _fallSpeed, ForceMode.VelocityChange);
        
        // Play fall sound
        if (_fallSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_fallSound);
        }
        
        // Start respawn timer if enabled
        if (_respawnAfterFall)
        {
            StartCoroutine(RespawnAfterDelay());
        }
    }
    
    private IEnumerator RespawnAfterDelay()
    {
        yield return new WaitForSeconds(_respawnDelay);
        RespawnPlatform();
    }
    
    private void RespawnPlatform()
    {
        // Reset position and rotation
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
        
        // Reset rigidbody
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        
        // Reset flags
        _isTriggered = false;
        _isFalling = false;
        
        // Re-enable collider if it was disabled
        if (_platformCollider != null)
        {
            _platformCollider.enabled = true;
        }
    }
    
    public void ResetPlatform()
    {
        if (_crumbleCoroutine != null)
        {
            StopCoroutine(_crumbleCoroutine);
            _crumbleCoroutine = null;
        }
        
        StopAllCoroutines();
        RespawnPlatform();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        if (Application.isPlaying && _isTriggered)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, transform.localScale * 1.1f);
        }
    }
}