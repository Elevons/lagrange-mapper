// Prompt: pressure plate system: when weight is applied it plays click sound, changes color to green, starts 5-second timer - if weight stays for 5 seconds it plays success sound and spawns reward object, each pressure plate remembers how many times it's been activated
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PressurePlate : MonoBehaviour
{
    [Header("Visual Settings")]
    [SerializeField] private Renderer _plateRenderer;
    [SerializeField] private Color _defaultColor = Color.gray;
    [SerializeField] private Color _pressedColor = Color.green;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _clickSound;
    [SerializeField] private AudioClip _successSound;
    
    [Header("Reward Settings")]
    [SerializeField] private GameObject _rewardPrefab;
    [SerializeField] private Transform _rewardSpawnPoint;
    [SerializeField] private float _activationTime = 5f;
    
    [Header("Detection Settings")]
    [SerializeField] private string _targetTag = "Player";
    [SerializeField] private LayerMask _detectionLayers = -1;
    
    [Header("Events")]
    public UnityEvent OnPlatePressed;
    public UnityEvent OnPlateReleased;
    public UnityEvent OnActivationComplete;
    
    private bool _isPressed = false;
    private bool _isActivating = false;
    private int _activationCount = 0;
    private Coroutine _activationCoroutine;
    private Collider _plateCollider;
    private Material _plateMaterial;
    
    public int ActivationCount => _activationCount;
    public bool IsPressed => _isPressed;
    public bool IsActivating => _isActivating;
    
    private void Start()
    {
        InitializeComponents();
        SetPlateColor(_defaultColor);
    }
    
    private void InitializeComponents()
    {
        if (_plateRenderer == null)
            _plateRenderer = GetComponent<Renderer>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_plateCollider == null)
            _plateCollider = GetComponent<Collider>();
            
        if (_rewardSpawnPoint == null)
            _rewardSpawnPoint = transform;
            
        if (_plateRenderer != null)
        {
            _plateMaterial = _plateRenderer.material;
        }
        
        if (_plateCollider != null && !_plateCollider.isTrigger)
        {
            Debug.LogWarning($"PressurePlate '{gameObject.name}' collider should be set as trigger!");
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (ShouldDetectObject(other))
        {
            PressPlate();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (ShouldDetectObject(other))
        {
            if (!HasAnyValidObjectsOnPlate())
            {
                ReleasePlate();
            }
        }
    }
    
    private bool ShouldDetectObject(Collider other)
    {
        if (string.IsNullOrEmpty(_targetTag))
            return IsInDetectionLayer(other.gameObject);
            
        return other.CompareTag(_targetTag) && IsInDetectionLayer(other.gameObject);
    }
    
    private bool IsInDetectionLayer(GameObject obj)
    {
        return (_detectionLayers.value & (1 << obj.layer)) != 0;
    }
    
    private bool HasAnyValidObjectsOnPlate()
    {
        Collider[] overlapping = Physics.OverlapBox(
            _plateCollider.bounds.center,
            _plateCollider.bounds.extents,
            transform.rotation,
            _detectionLayers
        );
        
        foreach (var collider in overlapping)
        {
            if (ShouldDetectObject(collider))
                return true;
        }
        
        return false;
    }
    
    private void PressPlate()
    {
        if (_isPressed) return;
        
        _isPressed = true;
        SetPlateColor(_pressedColor);
        PlayClickSound();
        OnPlatePressed?.Invoke();
        
        StartActivationTimer();
    }
    
    private void ReleasePlate()
    {
        if (!_isPressed) return;
        
        _isPressed = false;
        _isActivating = false;
        SetPlateColor(_defaultColor);
        OnPlateReleased?.Invoke();
        
        StopActivationTimer();
    }
    
    private void StartActivationTimer()
    {
        if (_activationCoroutine != null)
            StopCoroutine(_activationCoroutine);
            
        _activationCoroutine = StartCoroutine(ActivationCountdown());
    }
    
    private void StopActivationTimer()
    {
        if (_activationCoroutine != null)
        {
            StopCoroutine(_activationCoroutine);
            _activationCoroutine = null;
        }
    }
    
    private IEnumerator ActivationCountdown()
    {
        _isActivating = true;
        yield return new WaitForSeconds(_activationTime);
        
        if (_isPressed)
        {
            CompleteActivation();
        }
        
        _isActivating = false;
    }
    
    private void CompleteActivation()
    {
        _activationCount++;
        PlaySuccessSound();
        SpawnReward();
        OnActivationComplete?.Invoke();
    }
    
    private void SetPlateColor(Color color)
    {
        if (_plateMaterial != null)
        {
            _plateMaterial.color = color;
        }
    }
    
    private void PlayClickSound()
    {
        if (_audioSource != null && _clickSound != null)
        {
            _audioSource.PlayOneShot(_clickSound);
        }
    }
    
    private void PlaySuccessSound()
    {
        if (_audioSource != null && _successSound != null)
        {
            _audioSource.PlayOneShot(_successSound);
        }
    }
    
    private void SpawnReward()
    {
        if (_rewardPrefab != null && _rewardSpawnPoint != null)
        {
            Vector3 spawnPosition = _rewardSpawnPoint.position + Vector3.up * 0.5f;
            Instantiate(_rewardPrefab, spawnPosition, _rewardSpawnPoint.rotation);
        }
    }
    
    public void ResetActivationCount()
    {
        _activationCount = 0;
    }
    
    public void ForcePress()
    {
        PressPlate();
    }
    
    public void ForceRelease()
    {
        ReleasePlate();
    }
    
    private void OnValidate()
    {
        if (_activationTime < 0f)
            _activationTime = 0f;
    }
    
    private void OnDestroy()
    {
        StopActivationTimer();
    }
}