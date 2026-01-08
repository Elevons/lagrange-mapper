// Prompt: spike trap that damages on contact
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class SpikeTrap : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int _damageAmount = 10;
    [SerializeField] private float _damageInterval = 1f;
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject _spikesVisual;
    [SerializeField] private float _animationSpeed = 5f;
    [SerializeField] private Vector3 _extendedPosition = Vector3.up;
    [SerializeField] private Vector3 _retractedPosition = Vector3.zero;
    
    [Header("Activation Settings")]
    [SerializeField] private bool _isActive = true;
    [SerializeField] private bool _animateSpikes = true;
    [SerializeField] private float _activationDelay = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _damageSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnPlayerDamaged;
    public UnityEvent OnTrapActivated;
    
    private bool _playerInRange = false;
    private float _lastDamageTime = 0f;
    private bool _spikesExtended = false;
    private Vector3 _originalSpikePosition;
    private Coroutine _activationCoroutine;
    
    private void Start()
    {
        if (_spikesVisual != null)
        {
            _originalSpikePosition = _spikesVisual.transform.localPosition;
            _retractedPosition += _originalSpikePosition;
            _extendedPosition += _originalSpikePosition;
            
            if (!_isActive)
            {
                _spikesVisual.transform.localPosition = _retractedPosition;
            }
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }
    
    private void Update()
    {
        if (_animateSpikes && _spikesVisual != null)
        {
            Vector3 targetPosition = _spikesExtended ? _extendedPosition : _retractedPosition;
            _spikesVisual.transform.localPosition = Vector3.Lerp(
                _spikesVisual.transform.localPosition, 
                targetPosition, 
                Time.deltaTime * _animationSpeed
            );
        }
        
        if (_playerInRange && _isActive && Time.time >= _lastDamageTime + _damageInterval)
        {
            DealDamage();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
            
            if (_isActive)
            {
                if (_activationCoroutine != null)
                {
                    StopCoroutine(_activationCoroutine);
                }
                _activationCoroutine = StartCoroutine(ActivateWithDelay());
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
            _spikesExtended = false;
            
            if (_activationCoroutine != null)
            {
                StopCoroutine(_activationCoroutine);
                _activationCoroutine = null;
            }
        }
    }
    
    private System.Collections.IEnumerator ActivateWithDelay()
    {
        yield return new WaitForSeconds(_activationDelay);
        
        if (_playerInRange && _isActive)
        {
            ActivateTrap();
        }
    }
    
    private void ActivateTrap()
    {
        _spikesExtended = true;
        
        if (_audioSource != null && _activationSound != null)
        {
            _audioSource.PlayOneShot(_activationSound);
        }
        
        OnTrapActivated?.Invoke();
        
        if (_playerInRange)
        {
            DealDamage();
        }
    }
    
    private void DealDamage()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _lastDamageTime = Time.time;
            
            // Try to find a component that can take damage
            var healthComponent = player.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                // Use SendMessage as a fallback for damage dealing
                player.SendMessage("TakeDamage", _damageAmount, SendMessageOptions.DontRequireReceiver);
            }
            
            if (_audioSource != null && _damageSound != null)
            {
                _audioSource.PlayOneShot(_damageSound);
            }
            
            OnPlayerDamaged?.Invoke(player);
        }
    }
    
    public void SetActive(bool active)
    {
        _isActive = active;
        if (!active)
        {
            _spikesExtended = false;
            _playerInRange = false;
        }
    }
    
    public void ToggleActive()
    {
        SetActive(!_isActive);
    }
    
    public void ForceActivate()
    {
        if (_isActive)
        {
            ActivateTrap();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive ? Color.red : Color.gray;
        Gizmos.DrawWireCube(transform.position, GetComponent<Collider>().bounds.size);
        
        if (_spikesVisual != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position + _retractedPosition, transform.position + _extendedPosition);
        }
    }
}