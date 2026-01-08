// Prompt: time bomb that beeps every second with the beep frequency increasing as time runs down, the object's color pulses red in sync with beeps and shakes more violently as time decreases, after 10 seconds it explodes with massive force affecting all rigidbodies in a 20-unit radius
// Type: general

using UnityEngine;
using System.Collections;

public class TimeBomb : MonoBehaviour
{
    [Header("Bomb Settings")]
    [SerializeField] private float _countdownTime = 10f;
    [SerializeField] private float _explosionRadius = 20f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _warningColor = Color.red;
    [SerializeField] private float _pulseDuration = 0.1f;
    [SerializeField] private AnimationCurve _shakeIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float _maxShakeIntensity = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _beepSound;
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private float _minBeepInterval = 0.1f;
    [SerializeField] private float _maxBeepInterval = 1f;
    
    [Header("Explosion Effects")]
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField] private bool _destroyOnExplode = true;
    
    private float _timeRemaining;
    private bool _isActive = false;
    private bool _hasExploded = false;
    
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Vector3 _originalPosition;
    private Material _material;
    
    private Coroutine _countdownCoroutine;
    private Coroutine _pulseCoroutine;
    private Coroutine _shakeCoroutine;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_renderer != null)
        {
            _material = _renderer.material;
        }
        
        _originalPosition = transform.position;
        _timeRemaining = _countdownTime;
    }

    private void Start()
    {
        StartBomb();
    }

    public void StartBomb()
    {
        if (_isActive || _hasExploded) return;
        
        _isActive = true;
        _timeRemaining = _countdownTime;
        
        _countdownCoroutine = StartCoroutine(CountdownRoutine());
        _shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    public void DefuseBomb()
    {
        if (_hasExploded) return;
        
        _isActive = false;
        
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        
        ResetVisuals();
    }

    private IEnumerator CountdownRoutine()
    {
        while (_timeRemaining > 0 && _isActive)
        {
            float progress = 1f - (_timeRemaining / _countdownTime);
            float beepInterval = Mathf.Lerp(_maxBeepInterval, _minBeepInterval, progress);
            
            PlayBeep();
            StartPulse();
            
            yield return new WaitForSeconds(beepInterval);
            _timeRemaining -= beepInterval;
        }
        
        if (_isActive)
        {
            Explode();
        }
    }

    private IEnumerator ShakeRoutine()
    {
        while (_isActive && !_hasExploded)
        {
            float progress = 1f - (_timeRemaining / _countdownTime);
            float shakeIntensity = _shakeIntensityCurve.Evaluate(progress) * _maxShakeIntensity;
            
            Vector3 randomOffset = Random.insideUnitSphere * shakeIntensity;
            transform.position = _originalPosition + randomOffset;
            
            yield return null;
        }
        
        transform.position = _originalPosition;
    }

    private void PlayBeep()
    {
        if (_audioSource != null && _beepSound != null)
        {
            float progress = 1f - (_timeRemaining / _countdownTime);
            _audioSource.pitch = Mathf.Lerp(0.8f, 2f, progress);
            _audioSource.PlayOneShot(_beepSound);
        }
    }

    private void StartPulse()
    {
        if (_pulseCoroutine != null)
        {
            StopCoroutine(_pulseCoroutine);
        }
        _pulseCoroutine = StartCoroutine(PulseRoutine());
    }

    private IEnumerator PulseRoutine()
    {
        if (_material == null) yield break;
        
        Color originalColor = _material.color;
        float elapsed = 0f;
        
        while (elapsed < _pulseDuration)
        {
            float t = elapsed / _pulseDuration;
            float pulseValue = Mathf.Sin(t * Mathf.PI);
            
            _material.color = Color.Lerp(originalColor, _warningColor, pulseValue);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _material.color = originalColor;
    }

    private void Explode()
    {
        if (_hasExploded) return;
        
        _hasExploded = true;
        _isActive = false;
        
        // Stop all coroutines
        if (_countdownCoroutine != null) StopCoroutine(_countdownCoroutine);
        if (_pulseCoroutine != null) StopCoroutine(_pulseCoroutine);
        if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
        
        // Play explosion sound
        if (_audioSource != null && _explosionSound != null)
        {
            _audioSource.pitch = 1f;
            _audioSource.PlayOneShot(_explosionSound);
        }
        
        // Spawn explosion effect
        if (_explosionPrefab != null)
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
        }
        
        // Apply explosion force
        ApplyExplosionForce();
        
        // Destroy or disable the bomb
        if (_destroyOnExplode)
        {
            Destroy(gameObject, _explosionSound != null ? _explosionSound.length : 0f);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void ApplyExplosionForce()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRadius, _affectedLayers);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject == gameObject) continue;
            
            Rigidbody rb = col.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
            }
            
            // Damage players or other objects
            if (col.CompareTag("Player"))
            {
                // Send damage message if the object can receive it
                col.SendMessage("TakeDamage", 100f, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void ResetVisuals()
    {
        if (_material != null)
        {
            _material.color = _normalColor;
        }
        
        transform.position = _originalPosition;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
    }

    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }

    // Public methods for external control
    public float TimeRemaining => _timeRemaining;
    public bool IsActive => _isActive;
    public bool HasExploded => _hasExploded;
    
    public void SetCountdownTime(float time)
    {
        if (!_isActive)
        {
            _countdownTime = time;
            _timeRemaining = time;
        }
    }
}