// Prompt: quantum object that when observed (player looking at it via raycast) collapses to one state (visible, plays observation sound), when not observed exists in superposition (flickers between positions, plays quantum hum, translucent material)
// Type: movement

using UnityEngine;
using System.Collections;

public class QuantumObject : MonoBehaviour
{
    [Header("Quantum States")]
    [SerializeField] private float _observationDistance = 10f;
    [SerializeField] private LayerMask _obstacleLayerMask = -1;
    
    [Header("Superposition Settings")]
    [SerializeField] private float _flickerSpeed = 2f;
    [SerializeField] private float _positionVariance = 0.5f;
    [SerializeField] private int _superpositionCount = 3;
    [SerializeField] private float _transparencyInSuperposition = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _observationSound;
    [SerializeField] private AudioClip _quantumHumSound;
    [SerializeField] private float _humVolume = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _collapseEffect;
    [SerializeField] private float _materialTransitionSpeed = 2f;
    
    private Camera _playerCamera;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Material _originalMaterial;
    private Material _quantumMaterial;
    
    private bool _isObserved = false;
    private bool _wasObservedLastFrame = false;
    private Vector3 _originalPosition;
    private Vector3[] _superpositionPositions;
    private int _currentSuperpositionIndex = 0;
    private float _flickerTimer = 0f;
    private float _currentTransparency = 1f;
    private Coroutine _humCoroutine;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
        
        _originalPosition = transform.position;
        _originalMaterial = _renderer.material;
        _quantumMaterial = new Material(_originalMaterial);
        
        GenerateSuperpositionPositions();
        StartQuantumHum();
    }
    
    private void Update()
    {
        CheckObservationState();
        UpdateQuantumBehavior();
    }
    
    private void CheckObservationState()
    {
        _wasObservedLastFrame = _isObserved;
        _isObserved = false;
        
        if (_playerCamera == null) return;
        
        Vector3 directionToObject = (transform.position - _playerCamera.transform.position).normalized;
        float distanceToObject = Vector3.Distance(_playerCamera.transform.position, transform.position);
        
        if (distanceToObject <= _observationDistance)
        {
            float dotProduct = Vector3.Dot(_playerCamera.transform.forward, directionToObject);
            
            if (dotProduct > 0.5f)
            {
                Ray observationRay = new Ray(_playerCamera.transform.position, directionToObject);
                
                if (Physics.Raycast(observationRay, out RaycastHit hit, distanceToObject, _obstacleLayerMask))
                {
                    if (hit.collider.gameObject == gameObject)
                    {
                        _isObserved = true;
                    }
                }
            }
        }
        
        if (_isObserved != _wasObservedLastFrame)
        {
            OnObservationStateChanged();
        }
    }
    
    private void UpdateQuantumBehavior()
    {
        if (_isObserved)
        {
            UpdateObservedState();
        }
        else
        {
            UpdateSuperpositionState();
        }
        
        UpdateMaterialTransparency();
    }
    
    private void UpdateObservedState()
    {
        transform.position = Vector3.Lerp(transform.position, _originalPosition, Time.deltaTime * 5f);
        _currentTransparency = Mathf.Lerp(_currentTransparency, 1f, Time.deltaTime * _materialTransitionSpeed);
    }
    
    private void UpdateSuperpositionState()
    {
        _flickerTimer += Time.deltaTime * _flickerSpeed;
        
        if (_flickerTimer >= 1f)
        {
            _flickerTimer = 0f;
            _currentSuperpositionIndex = (_currentSuperpositionIndex + 1) % _superpositionCount;
        }
        
        Vector3 targetPosition = Vector3.Lerp(
            _superpositionPositions[_currentSuperpositionIndex],
            _superpositionPositions[(_currentSuperpositionIndex + 1) % _superpositionCount],
            _flickerTimer
        );
        
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 3f);
        _currentTransparency = Mathf.Lerp(_currentTransparency, _transparencyInSuperposition, Time.deltaTime * _materialTransitionSpeed);
    }
    
    private void UpdateMaterialTransparency()
    {
        Color materialColor = _quantumMaterial.color;
        materialColor.a = _currentTransparency;
        _quantumMaterial.color = materialColor;
        _renderer.material = _quantumMaterial;
    }
    
    private void OnObservationStateChanged()
    {
        if (_isObserved)
        {
            OnCollapse();
        }
        else
        {
            OnEnterSuperposition();
        }
    }
    
    private void OnCollapse()
    {
        if (_observationSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_observationSound);
        }
        
        if (_collapseEffect != null)
        {
            _collapseEffect.Play();
        }
        
        StopQuantumHum();
    }
    
    private void OnEnterSuperposition()
    {
        GenerateSuperpositionPositions();
        StartQuantumHum();
    }
    
    private void GenerateSuperpositionPositions()
    {
        _superpositionPositions = new Vector3[_superpositionCount];
        
        for (int i = 0; i < _superpositionCount; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-_positionVariance, _positionVariance),
                Random.Range(-_positionVariance * 0.5f, _positionVariance * 0.5f),
                Random.Range(-_positionVariance, _positionVariance)
            );
            
            _superpositionPositions[i] = _originalPosition + randomOffset;
        }
    }
    
    private void StartQuantumHum()
    {
        if (_quantumHumSound != null && _audioSource != null && _humCoroutine == null)
        {
            _humCoroutine = StartCoroutine(PlayQuantumHum());
        }
    }
    
    private void StopQuantumHum()
    {
        if (_humCoroutine != null)
        {
            StopCoroutine(_humCoroutine);
            _humCoroutine = null;
        }
        
        if (_audioSource != null && _audioSource.isPlaying && _audioSource.clip == _quantumHumSound)
        {
            _audioSource.Stop();
        }
    }
    
    private IEnumerator PlayQuantumHum()
    {
        while (!_isObserved)
        {
            if (_audioSource != null && _quantumHumSound != null)
            {
                _audioSource.clip = _quantumHumSound;
                _audioSource.volume = _humVolume;
                _audioSource.loop = true;
                _audioSource.Play();
                
                yield return new WaitWhile(() => !_isObserved && _audioSource.isPlaying);
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
    }
    
    private void OnDestroy()
    {
        if (_quantumMaterial != null)
        {
            DestroyImmediate(_quantumMaterial);
        }
        
        StopQuantumHum();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _observationDistance);
        
        if (_superpositionPositions != null)
        {
            Gizmos.color = Color.red;
            foreach (Vector3 pos in _superpositionPositions)
            {
                Gizmos.DrawWireCube(pos, Vector3.one * 0.1f);
            }
        }
    }
}