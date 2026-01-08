// Prompt: bridge that extends
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ExtendingBridge : MonoBehaviour
{
    [Header("Bridge Settings")]
    [SerializeField] private Transform _bridgeSegment;
    [SerializeField] private float _extendDistance = 10f;
    [SerializeField] private float _extendSpeed = 2f;
    [SerializeField] private bool _autoExtend = false;
    [SerializeField] private float _autoExtendDelay = 1f;
    
    [Header("Trigger Settings")]
    [SerializeField] private bool _usePlayerTrigger = true;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private KeyCode _manualExtendKey = KeyCode.E;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _extendSound;
    [SerializeField] private AudioClip _retractSound;
    
    [Header("Events")]
    public UnityEvent OnBridgeExtended;
    public UnityEvent OnBridgeRetracted;
    public UnityEvent OnExtensionComplete;
    
    private Vector3 _originalPosition;
    private Vector3 _extendedPosition;
    private bool _isExtended = false;
    private bool _isExtending = false;
    private bool _isRetracting = false;
    private bool _playerInRange = false;
    private Coroutine _autoExtendCoroutine;
    
    private void Start()
    {
        if (_bridgeSegment == null)
            _bridgeSegment = transform;
            
        _originalPosition = _bridgeSegment.position;
        _extendedPosition = _originalPosition + _bridgeSegment.forward * _extendDistance;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_autoExtend)
        {
            _autoExtendCoroutine = StartCoroutine(AutoExtendCoroutine());
        }
    }
    
    private void Update()
    {
        if (!_autoExtend)
        {
            HandleManualInput();
        }
        
        HandleBridgeMovement();
    }
    
    private void HandleManualInput()
    {
        bool shouldExtend = false;
        
        if (_usePlayerTrigger && _playerInRange && Input.GetKeyDown(_manualExtendKey))
        {
            shouldExtend = true;
        }
        else if (!_usePlayerTrigger && Input.GetKeyDown(_manualExtendKey))
        {
            shouldExtend = true;
        }
        
        if (shouldExtend)
        {
            if (!_isExtended && !_isExtending)
            {
                ExtendBridge();
            }
            else if (_isExtended && !_isRetracting)
            {
                RetractBridge();
            }
        }
    }
    
    private void HandleBridgeMovement()
    {
        if (_isExtending)
        {
            _bridgeSegment.position = Vector3.MoveTowards(_bridgeSegment.position, _extendedPosition, _extendSpeed * Time.deltaTime);
            
            if (Vector3.Distance(_bridgeSegment.position, _extendedPosition) < 0.01f)
            {
                _bridgeSegment.position = _extendedPosition;
                _isExtending = false;
                _isExtended = true;
                OnExtensionComplete?.Invoke();
            }
        }
        else if (_isRetracting)
        {
            _bridgeSegment.position = Vector3.MoveTowards(_bridgeSegment.position, _originalPosition, _extendSpeed * Time.deltaTime);
            
            if (Vector3.Distance(_bridgeSegment.position, _originalPosition) < 0.01f)
            {
                _bridgeSegment.position = _originalPosition;
                _isRetracting = false;
                _isExtended = false;
                OnExtensionComplete?.Invoke();
            }
        }
    }
    
    public void ExtendBridge()
    {
        if (_isExtended || _isExtending) return;
        
        _isExtending = true;
        OnBridgeExtended?.Invoke();
        
        if (_audioSource != null && _extendSound != null)
        {
            _audioSource.PlayOneShot(_extendSound);
        }
    }
    
    public void RetractBridge()
    {
        if (!_isExtended || _isRetracting) return;
        
        _isRetracting = true;
        OnBridgeRetracted?.Invoke();
        
        if (_audioSource != null && _retractSound != null)
        {
            _audioSource.PlayOneShot(_retractSound);
        }
    }
    
    public void ToggleBridge()
    {
        if (_isExtended || _isRetracting)
        {
            RetractBridge();
        }
        else if (!_isExtended || _isExtending)
        {
            ExtendBridge();
        }
    }
    
    private System.Collections.IEnumerator AutoExtendCoroutine()
    {
        yield return new WaitForSeconds(_autoExtendDelay);
        ExtendBridge();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_usePlayerTrigger && other.CompareTag(_playerTag))
        {
            _playerInRange = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (_usePlayerTrigger && other.CompareTag(_playerTag))
        {
            _playerInRange = false;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_bridgeSegment == null) return;
        
        Vector3 startPos = Application.isPlaying ? _originalPosition : _bridgeSegment.position;
        Vector3 endPos = startPos + _bridgeSegment.forward * _extendDistance;
        
        Gizmos.color = Color.green;
        Gizmos.DrawLine(startPos, endPos);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(endPos, Vector3.one * 0.5f);
    }
    
    public bool IsExtended => _isExtended;
    public bool IsMoving => _isExtending || _isRetracting;
    public float ExtensionProgress
    {
        get
        {
            if (_bridgeSegment == null) return 0f;
            float totalDistance = Vector3.Distance(_originalPosition, _extendedPosition);
            float currentDistance = Vector3.Distance(_originalPosition, _bridgeSegment.position);
            return Mathf.Clamp01(currentDistance / totalDistance);
        }
    }
}