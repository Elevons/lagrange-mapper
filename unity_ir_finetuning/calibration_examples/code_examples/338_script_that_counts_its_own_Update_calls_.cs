// Prompt: script that counts its own Update calls and every 1000 calls plays a different sound, changes its material, and spawns a clone of itself - each generation plays slightly higher-pitched sound than parent
// Type: general

using UnityEngine;

public class SelfReplicatingCounter : MonoBehaviour
{
    [Header("Counter Settings")]
    [SerializeField] private int _updateCallsPerGeneration = 1000;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip _baseSound;
    [SerializeField] private float _pitchIncrement = 0.2f;
    [SerializeField] private float _basePitch = 1.0f;
    
    [Header("Material Settings")]
    [SerializeField] private Material[] _generationMaterials;
    [SerializeField] private Color[] _generationColors;
    
    [Header("Spawning Settings")]
    [SerializeField] private Vector3 _spawnOffset = Vector3.right * 2f;
    [SerializeField] private int _maxGenerations = 10;
    
    private int _updateCallCount = 0;
    private int _generation = 0;
    private float _currentPitch;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Material _materialInstance;

    private void Start()
    {
        InitializeComponents();
        SetupGeneration();
    }

    private void Update()
    {
        _updateCallCount++;
        
        if (_updateCallCount >= _updateCallsPerGeneration)
        {
            ProcessGeneration();
            _updateCallCount = 0;
        }
    }

    private void InitializeComponents()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null)
        {
            _materialInstance = new Material(_renderer.material);
            _renderer.material = _materialInstance;
        }
    }

    private void SetupGeneration()
    {
        _currentPitch = _basePitch + (_generation * _pitchIncrement);
        
        if (_audioSource != null)
        {
            _audioSource.pitch = _currentPitch;
            _audioSource.clip = _baseSound;
        }
        
        UpdateVisuals();
    }

    private void ProcessGeneration()
    {
        PlayGenerationSound();
        ChangeMaterial();
        
        if (_generation < _maxGenerations)
        {
            SpawnClone();
        }
    }

    private void PlayGenerationSound()
    {
        if (_audioSource != null && _baseSound != null)
        {
            _audioSource.pitch = _currentPitch;
            _audioSource.PlayOneShot(_baseSound);
        }
    }

    private void ChangeMaterial()
    {
        if (_renderer == null) return;

        if (_generationMaterials != null && _generationMaterials.Length > 0)
        {
            int materialIndex = _generation % _generationMaterials.Length;
            if (_generationMaterials[materialIndex] != null)
            {
                if (_materialInstance != null)
                {
                    DestroyImmediate(_materialInstance);
                }
                _materialInstance = new Material(_generationMaterials[materialIndex]);
                _renderer.material = _materialInstance;
            }
        }
        else if (_generationColors != null && _generationColors.Length > 0)
        {
            int colorIndex = _generation % _generationColors.Length;
            if (_materialInstance != null)
            {
                _materialInstance.color = _generationColors[colorIndex];
            }
        }
        else
        {
            // Fallback: Generate random color based on generation
            if (_materialInstance != null)
            {
                float hue = (_generation * 0.1f) % 1.0f;
                _materialInstance.color = Color.HSVToRGB(hue, 0.8f, 1.0f);
            }
        }
    }

    private void SpawnClone()
    {
        Vector3 spawnPosition = transform.position + _spawnOffset;
        GameObject clone = Instantiate(gameObject, spawnPosition, transform.rotation);
        
        SelfReplicatingCounter cloneScript = clone.GetComponent<SelfReplicatingCounter>();
        if (cloneScript != null)
        {
            cloneScript._generation = _generation + 1;
            cloneScript.SetupGeneration();
        }
    }

    private void UpdateVisuals()
    {
        // Scale slightly based on generation for visual feedback
        float scaleMultiplier = 1.0f + (_generation * 0.05f);
        transform.localScale = Vector3.one * scaleMultiplier;
        
        // Update name for debugging
        gameObject.name = $"SelfReplicatingCounter_Gen{_generation}";
    }

    private void OnDestroy()
    {
        if (_materialInstance != null)
        {
            DestroyImmediate(_materialInstance);
        }
    }

    // Public method to manually set generation (useful for spawned clones)
    public void SetGeneration(int generation)
    {
        _generation = Mathf.Clamp(generation, 0, _maxGenerations);
        SetupGeneration();
    }

    // Public getter for current generation
    public int GetGeneration()
    {
        return _generation;
    }

    // Public getter for update call count
    public int GetUpdateCallCount()
    {
        return _updateCallCount;
    }
}