// Prompt: pressure plates requiring weight
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class PressurePlate : MonoBehaviour
{
    [Header("Pressure Plate Settings")]
    [SerializeField] private float _requiredWeight = 10f;
    [SerializeField] private float _activationDelay = 0.1f;
    [SerializeField] private bool _requiresContinuousWeight = true;
    [SerializeField] private bool _isOneTimeUse = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private Transform _plateTransform;
    [SerializeField] private float _pressedHeight = -0.1f;
    [SerializeField] private float _animationSpeed = 5f;
    [SerializeField] private Material _activeMaterial;
    [SerializeField] private Material _inactiveMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onActivated;
    [SerializeField] private UnityEvent _onDeactivated;
    
    private List<WeightedObject> _objectsOnPlate = new List<WeightedObject>();
    private float _currentWeight = 0f;
    private bool _isActivated = false;
    private bool _hasBeenUsed = false;
    private float _originalHeight;
    private float _activationTimer = 0f;
    private Renderer _plateRenderer;
    private Collider _plateCollider;
    
    [System.Serializable]
    public class WeightedObject
    {
        public GameObject gameObject;
        public float weight;
        
        public WeightedObject(GameObject obj, float w)
        {
            gameObject = obj;
            weight = w;
        }
    }
    
    void Start()
    {
        if (_plateTransform == null)
            _plateTransform = transform;
            
        _originalHeight = _plateTransform.position.y;
        _plateRenderer = GetComponent<Renderer>();
        _plateCollider = GetComponent<Collider>();
        
        if (_plateCollider != null)
            _plateCollider.isTrigger = true;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        UpdateVisuals();
    }
    
    void Update()
    {
        if (_hasBeenUsed && _isOneTimeUse)
            return;
            
        UpdatePlatePosition();
        
        if (_activationTimer > 0f)
        {
            _activationTimer -= Time.deltaTime;
            if (_activationTimer <= 0f)
            {
                CheckActivation();
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_hasBeenUsed && _isOneTimeUse)
            return;
            
        float weight = GetObjectWeight(other.gameObject);
        if (weight > 0f)
        {
            WeightedObject weightedObj = new WeightedObject(other.gameObject, weight);
            _objectsOnPlate.Add(weightedObj);
            _currentWeight += weight;
            
            _activationTimer = _activationDelay;
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (_hasBeenUsed && _isOneTimeUse)
            return;
            
        WeightedObject objToRemove = _objectsOnPlate.Find(obj => obj.gameObject == other.gameObject);
        if (objToRemove != null)
        {
            _objectsOnPlate.Remove(objToRemove);
            _currentWeight -= objToRemove.weight;
            _currentWeight = Mathf.Max(0f, _currentWeight);
            
            if (_requiresContinuousWeight)
            {
                _activationTimer = _activationDelay;
            }
        }
    }
    
    private float GetObjectWeight(GameObject obj)
    {
        // Check for Rigidbody mass
        Rigidbody rb = obj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            return rb.mass;
        }
        
        // Check for player tag (default weight)
        if (obj.CompareTag("Player"))
        {
            return 5f;
        }
        
        // Check for specific tags with predefined weights
        if (obj.CompareTag("Heavy"))
            return 15f;
        if (obj.CompareTag("Light"))
            return 2f;
            
        // Default weight for untagged objects
        return 1f;
    }
    
    private void CheckActivation()
    {
        bool shouldBeActive = _currentWeight >= _requiredWeight;
        
        if (shouldBeActive && !_isActivated)
        {
            ActivatePlate();
        }
        else if (!shouldBeActive && _isActivated && _requiresContinuousWeight)
        {
            DeactivatePlate();
        }
    }
    
    private void ActivatePlate()
    {
        if (_hasBeenUsed && _isOneTimeUse)
            return;
            
        _isActivated = true;
        _hasBeenUsed = true;
        
        UpdateVisuals();
        PlaySound(_activationSound);
        _onActivated?.Invoke();
    }
    
    private void DeactivatePlate()
    {
        if (_isOneTimeUse && _hasBeenUsed)
            return;
            
        _isActivated = false;
        
        UpdateVisuals();
        PlaySound(_deactivationSound);
        _onDeactivated?.Invoke();
    }
    
    private void UpdatePlatePosition()
    {
        float targetHeight = _isActivated ? _originalHeight + _pressedHeight : _originalHeight;
        Vector3 currentPos = _plateTransform.position;
        Vector3 targetPos = new Vector3(currentPos.x, targetHeight, currentPos.z);
        
        _plateTransform.position = Vector3.Lerp(currentPos, targetPos, Time.deltaTime * _animationSpeed);
    }
    
    private void UpdateVisuals()
    {
        if (_plateRenderer != null)
        {
            if (_isActivated && _activeMaterial != null)
            {
                _plateRenderer.material = _activeMaterial;
            }
            else if (!_isActivated && _inactiveMaterial != null)
            {
                _plateRenderer.material = _inactiveMaterial;
            }
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public bool IsActivated()
    {
        return _isActivated;
    }
    
    public float GetCurrentWeight()
    {
        return _currentWeight;
    }
    
    public float GetRequiredWeight()
    {
        return _requiredWeight;
    }
    
    public void SetRequiredWeight(float weight)
    {
        _requiredWeight = weight;
    }
    
    public void ResetPlate()
    {
        if (_isOneTimeUse)
            return;
            
        _objectsOnPlate.Clear();
        _currentWeight = 0f;
        _isActivated = false;
        _hasBeenUsed = false;
        _activationTimer = 0f;
        
        UpdateVisuals();
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActivated ? Color.green : Color.red;
        Gizmos.DrawWireCube(transform.position, GetComponent<Collider>()?.bounds.size ?? Vector3.one);
        
        // Draw weight indicator
        Gizmos.color = Color.yellow;
        Vector3 textPos = transform.position + Vector3.up * 2f;
        
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(textPos, $"Weight: {_currentWeight:F1}/{_requiredWeight:F1}");
        #endif
    }
}