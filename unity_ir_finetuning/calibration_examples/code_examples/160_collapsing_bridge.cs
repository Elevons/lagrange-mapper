// Prompt: collapsing bridge
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class CollapsingBridge : MonoBehaviour
{
    [Header("Bridge Settings")]
    [SerializeField] private float _collapseDelay = 2f;
    [SerializeField] private float _collapseSpeed = 5f;
    [SerializeField] private float _shakeIntensity = 0.1f;
    [SerializeField] private float _shakeDuration = 1.5f;
    [SerializeField] private bool _resetAfterCollapse = true;
    [SerializeField] private float _resetDelay = 5f;
    
    [Header("Bridge Segments")]
    [SerializeField] private Transform[] _bridgeSegments;
    [SerializeField] private float _segmentCollapseInterval = 0.2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _crackingSound;
    [SerializeField] private AudioClip _collapseSound;
    
    [Header("Particles")]
    [SerializeField] private ParticleSystem _dustParticles;
    [SerializeField] private ParticleSystem _debrisParticles;
    
    [Header("Events")]
    public UnityEvent OnBridgeTriggered;
    public UnityEvent OnBridgeCollapsed;
    public UnityEvent OnBridgeReset;
    
    private bool _isCollapsing = false;
    private bool _hasCollapsed = false;
    private Vector3[] _originalPositions;
    private Quaternion[] _originalRotations;
    private Rigidbody[] _segmentRigidbodies;
    private Collider _triggerCollider;
    private List<GameObject> _playersOnBridge = new List<GameObject>();
    
    private void Start()
    {
        InitializeBridge();
    }
    
    private void InitializeBridge()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null)
        {
            _triggerCollider.isTrigger = true;
        }
        
        if (_bridgeSegments == null || _bridgeSegments.Length == 0)
        {
            _bridgeSegments = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
            {
                _bridgeSegments[i] = transform.GetChild(i);
            }
        }
        
        _originalPositions = new Vector3[_bridgeSegments.Length];
        _originalRotations = new Quaternion[_bridgeSegments.Length];
        _segmentRigidbodies = new Rigidbody[_bridgeSegments.Length];
        
        for (int i = 0; i < _bridgeSegments.Length; i++)
        {
            if (_bridgeSegments[i] != null)
            {
                _originalPositions[i] = _bridgeSegments[i].position;
                _originalRotations[i] = _bridgeSegments[i].rotation;
                
                _segmentRigidbodies[i] = _bridgeSegments[i].GetComponent<Rigidbody>();
                if (_segmentRigidbodies[i] == null)
                {
                    _segmentRigidbodies[i] = _bridgeSegments[i].gameObject.AddComponent<Rigidbody>();
                }
                _segmentRigidbodies[i].isKinematic = true;
            }
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isCollapsing && !_hasCollapsed)
        {
            if (!_playersOnBridge.Contains(other.gameObject))
            {
                _playersOnBridge.Add(other.gameObject);
                
                if (_playersOnBridge.Count == 1)
                {
                    StartCoroutine(TriggerCollapse());
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playersOnBridge.Remove(other.gameObject);
        }
    }
    
    private IEnumerator TriggerCollapse()
    {
        _isCollapsing = true;
        OnBridgeTriggered.Invoke();
        
        if (_crackingSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_crackingSound);
        }
        
        StartCoroutine(ShakeBridge());
        
        yield return new WaitForSeconds(_collapseDelay);
        
        StartCoroutine(CollapseBridge());
    }
    
    private IEnumerator ShakeBridge()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _shakeDuration)
        {
            for (int i = 0; i < _bridgeSegments.Length; i++)
            {
                if (_bridgeSegments[i] != null)
                {
                    Vector3 randomOffset = Random.insideUnitSphere * _shakeIntensity;
                    randomOffset.y = Mathf.Abs(randomOffset.y);
                    _bridgeSegments[i].position = _originalPositions[i] + randomOffset;
                }
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        for (int i = 0; i < _bridgeSegments.Length; i++)
        {
            if (_bridgeSegments[i] != null)
            {
                _bridgeSegments[i].position = _originalPositions[i];
            }
        }
    }
    
    private IEnumerator CollapseBridge()
    {
        if (_collapseSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collapseSound);
        }
        
        if (_dustParticles != null)
        {
            _dustParticles.Play();
        }
        
        for (int i = 0; i < _bridgeSegments.Length; i++)
        {
            if (_bridgeSegments[i] != null && _segmentRigidbodies[i] != null)
            {
                _segmentRigidbodies[i].isKinematic = false;
                _segmentRigidbodies[i].AddForce(Vector3.down * _collapseSpeed, ForceMode.VelocityChange);
                _segmentRigidbodies[i].AddTorque(Random.insideUnitSphere * _collapseSpeed * 0.5f, ForceMode.VelocityChange);
                
                if (_debrisParticles != null)
                {
                    ParticleSystem debris = Instantiate(_debrisParticles, _bridgeSegments[i].position, Quaternion.identity);
                    debris.Play();
                    Destroy(debris.gameObject, 3f);
                }
                
                yield return new WaitForSeconds(_segmentCollapseInterval);
            }
        }
        
        _hasCollapsed = true;
        OnBridgeCollapsed.Invoke();
        
        if (_resetAfterCollapse)
        {
            StartCoroutine(ResetBridge());
        }
    }
    
    private IEnumerator ResetBridge()
    {
        yield return new WaitForSeconds(_resetDelay);
        
        for (int i = 0; i < _bridgeSegments.Length; i++)
        {
            if (_bridgeSegments[i] != null && _segmentRigidbodies[i] != null)
            {
                _segmentRigidbodies[i].isKinematic = true;
                _segmentRigidbodies[i].velocity = Vector3.zero;
                _segmentRigidbodies[i].angularVelocity = Vector3.zero;
                
                _bridgeSegments[i].position = _originalPositions[i];
                _bridgeSegments[i].rotation = _originalRotations[i];
            }
        }
        
        _isCollapsing = false;
        _hasCollapsed = false;
        _playersOnBridge.Clear();
        
        OnBridgeReset.Invoke();
    }
    
    public void ForceCollapse()
    {
        if (!_isCollapsing && !_hasCollapsed)
        {
            StartCoroutine(TriggerCollapse());
        }
    }
    
    public void ResetBridgeImmediate()
    {
        StopAllCoroutines();
        
        for (int i = 0; i < _bridgeSegments.Length; i++)
        {
            if (_bridgeSegments[i] != null && _segmentRigidbodies[i] != null)
            {
                _segmentRigidbodies[i].isKinematic = true;
                _segmentRigidbodies[i].velocity = Vector3.zero;
                _segmentRigidbodies[i].angularVelocity = Vector3.zero;
                
                _bridgeSegments[i].position = _originalPositions[i];
                _bridgeSegments[i].rotation = _originalRotations[i];
            }
        }
        
        _isCollapsing = false;
        _hasCollapsed = false;
        _playersOnBridge.Clear();
        
        OnBridgeReset.Invoke();
    }
    
    public bool IsCollapsed => _hasCollapsed;
    public bool IsCollapsing => _isCollapsing;
}