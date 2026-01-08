// Prompt: pit fall trap with spikes
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class PitfallTrap : MonoBehaviour
{
    [Header("Trap Settings")]
    [SerializeField] private float _fallDepth = 5f;
    [SerializeField] private float _fallSpeed = 10f;
    [SerializeField] private float _resetDelay = 3f;
    [SerializeField] private bool _resetAfterFall = true;
    [SerializeField] private LayerMask _triggerLayers = -1;
    
    [Header("Spike Settings")]
    [SerializeField] private GameObject[] _spikes;
    [SerializeField] private float _spikeRiseSpeed = 8f;
    [SerializeField] private float _spikeRiseDelay = 0.5f;
    [SerializeField] private int _spikeDamage = 50;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _fallSound;
    [SerializeField] private AudioClip _spikeSound;
    [SerializeField] private AudioClip _resetSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _dustEffect;
    [SerializeField] private ParticleSystem _bloodEffect;
    
    [Header("Events")]
    public UnityEvent OnTrapTriggered;
    public UnityEvent OnPlayerDamaged;
    public UnityEvent OnTrapReset;
    
    private Vector3 _originalPosition;
    private Vector3 _targetPosition;
    private bool _isTriggered = false;
    private bool _isResetting = false;
    private bool _spikesRaised = false;
    private Vector3[] _originalSpikePositions;
    private Vector3[] _targetSpikePositions;
    private Collider _trapCollider;
    private Rigidbody _trapRigidbody;
    
    private void Start()
    {
        _originalPosition = transform.position;
        _targetPosition = _originalPosition + Vector3.down * _fallDepth;
        _trapCollider = GetComponent<Collider>();
        _trapRigidbody = GetComponent<Rigidbody>();
        
        if (_trapRigidbody != null)
        {
            _trapRigidbody.isKinematic = true;
        }
        
        InitializeSpikes();
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }
    
    private void InitializeSpikes()
    {
        if (_spikes == null || _spikes.Length == 0) return;
        
        _originalSpikePositions = new Vector3[_spikes.Length];
        _targetSpikePositions = new Vector3[_spikes.Length];
        
        for (int i = 0; i < _spikes.Length; i++)
        {
            if (_spikes[i] != null)
            {
                _originalSpikePositions[i] = _spikes[i].transform.localPosition;
                _targetSpikePositions[i] = _originalSpikePositions[i] + Vector3.up * 2f;
                
                // Hide spikes initially
                _spikes[i].transform.localPosition = _originalSpikePositions[i] - Vector3.up * 1f;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isTriggered || _isResetting) return;
        
        if (IsValidTarget(other))
        {
            TriggerTrap(other);
        }
    }
    
    private bool IsValidTarget(Collider other)
    {
        return (_triggerLayers.value & (1 << other.gameObject.layer)) > 0;
    }
    
    private void TriggerTrap(Collider target)
    {
        _isTriggered = true;
        OnTrapTriggered?.Invoke();
        
        PlaySound(_fallSound);
        PlayEffect(_dustEffect);
        
        StartCoroutine(FallSequence(target));
    }
    
    private System.Collections.IEnumerator FallSequence(Collider target)
    {
        // Fall animation
        float fallTime = 0f;
        float fallDuration = _fallDepth / _fallSpeed;
        
        while (fallTime < fallDuration)
        {
            fallTime += Time.deltaTime;
            float progress = fallTime / fallDuration;
            transform.position = Vector3.Lerp(_originalPosition, _targetPosition, progress);
            yield return null;
        }
        
        transform.position = _targetPosition;
        
        // Wait before raising spikes
        yield return new WaitForSeconds(_spikeRiseDelay);
        
        // Raise spikes
        yield return StartCoroutine(RaiseSpikes());
        
        // Check for damage
        CheckForDamage(target);
        
        // Reset if enabled
        if (_resetAfterFall)
        {
            yield return new WaitForSeconds(_resetDelay);
            yield return StartCoroutine(ResetTrap());
        }
    }
    
    private System.Collections.IEnumerator RaiseSpikes()
    {
        if (_spikes == null || _spikes.Length == 0) yield break;
        
        PlaySound(_spikeSound);
        _spikesRaised = true;
        
        float riseTime = 0f;
        float riseDuration = 2f / _spikeRiseSpeed;
        
        while (riseTime < riseDuration)
        {
            riseTime += Time.deltaTime;
            float progress = riseTime / riseDuration;
            
            for (int i = 0; i < _spikes.Length; i++)
            {
                if (_spikes[i] != null)
                {
                    Vector3 startPos = _originalSpikePositions[i] - Vector3.up * 1f;
                    _spikes[i].transform.localPosition = Vector3.Lerp(startPos, _targetSpikePositions[i], progress);
                }
            }
            
            yield return null;
        }
        
        // Ensure final positions
        for (int i = 0; i < _spikes.Length; i++)
        {
            if (_spikes[i] != null)
            {
                _spikes[i].transform.localPosition = _targetSpikePositions[i];
            }
        }
    }
    
    private void CheckForDamage(Collider target)
    {
        if (target == null) return;
        
        // Check if target is still in trap area
        Bounds trapBounds = _trapCollider.bounds;
        if (trapBounds.Contains(target.transform.position))
        {
            DamageTarget(target);
        }
    }
    
    private void DamageTarget(Collider target)
    {
        // Try to find a component that can take damage
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use SendMessage for generic damage handling
            target.SendMessage("TakeDamage", _spikeDamage, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("OnDamaged", _spikeDamage, SendMessageOptions.DontRequireReceiver);
        }
        
        // Apply physics damage if rigidbody exists
        var targetRigidbody = target.GetComponent<Rigidbody>();
        if (targetRigidbody != null)
        {
            targetRigidbody.AddForce(Vector3.down * 500f, ForceMode.Impulse);
        }
        
        PlayEffect(_bloodEffect);
        OnPlayerDamaged?.Invoke();
    }
    
    private System.Collections.IEnumerator ResetTrap()
    {
        _isResetting = true;
        PlaySound(_resetSound);
        
        // Lower spikes first
        if (_spikesRaised)
        {
            yield return StartCoroutine(LowerSpikes());
        }
        
        // Raise platform
        float resetTime = 0f;
        float resetDuration = _fallDepth / _fallSpeed;
        
        while (resetTime < resetDuration)
        {
            resetTime += Time.deltaTime;
            float progress = resetTime / resetDuration;
            transform.position = Vector3.Lerp(_targetPosition, _originalPosition, progress);
            yield return null;
        }
        
        transform.position = _originalPosition;
        
        _isTriggered = false;
        _isResetting = false;
        _spikesRaised = false;
        
        OnTrapReset?.Invoke();
    }
    
    private System.Collections.IEnumerator LowerSpikes()
    {
        if (_spikes == null || _spikes.Length == 0) yield break;
        
        float lowerTime = 0f;
        float lowerDuration = 2f / _spikeRiseSpeed;
        
        while (lowerTime < lowerDuration)
        {
            lowerTime += Time.deltaTime;
            float progress = lowerTime / lowerDuration;
            
            for (int i = 0; i < _spikes.Length; i++)
            {
                if (_spikes[i] != null)
                {
                    Vector3 endPos = _originalSpikePositions[i] - Vector3.up * 1f;
                    _spikes[i].transform.localPosition = Vector3.Lerp(_targetSpikePositions[i], endPos, progress);
                }
            }
            
            yield return null;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
        {
            effect.Play();
        }
    }
    
    public void ManualTrigger()
    {
        if (!_isTriggered && !_isResetting)
        {
            TriggerTrap(null);
        }
    }
    
    public void ManualReset()
    {
        if (_isTriggered && !_isResetting)
        {
            StartCoroutine(ResetTrap());
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector3 fallPosition = transform.position + Vector3.down * _fallDepth;
        Gizmos.DrawWireCube(fallPosition, transform.localScale);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, fallPosition);
    }
}