// Prompt: coin that spins and plays sound when collected
// Type: pickup

using UnityEngine;

public class Coin : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private Vector3 _rotationSpeed = new Vector3(0, 180, 0);
    
    [Header("Collection Settings")]
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private float _collectSoundVolume = 1f;
    [SerializeField] private bool _destroyOnCollect = true;
    [SerializeField] private float _destroyDelay = 0.1f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private float _collectEffectDuration = 2f;
    
    private AudioSource _audioSource;
    private bool _isCollected = false;
    private Collider _coinCollider;
    private Renderer _coinRenderer;
    
    private void Start()
    {
        SetupAudioSource();
        CacheComponents();
    }
    
    private void Update()
    {
        if (!_isCollected)
        {
            RotateCoin();
        }
    }
    
    private void SetupAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = _collectSound;
        _audioSource.volume = _collectSoundVolume;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }
    
    private void CacheComponents()
    {
        _coinCollider = GetComponent<Collider>();
        _coinRenderer = GetComponent<Renderer>();
        
        if (_coinCollider == null)
        {
            Debug.LogWarning($"Coin {gameObject.name} is missing a Collider component!");
        }
        else
        {
            _coinCollider.isTrigger = true;
        }
    }
    
    private void RotateCoin()
    {
        transform.Rotate(_rotationSpeed * Time.deltaTime);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectCoin();
        }
    }
    
    private void CollectCoin()
    {
        if (_isCollected) return;
        
        _isCollected = true;
        
        PlayCollectSound();
        SpawnCollectEffect();
        DisableVisuals();
        
        if (_destroyOnCollect)
        {
            Destroy(gameObject, _destroyDelay);
        }
    }
    
    private void PlayCollectSound()
    {
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.Play();
        }
    }
    
    private void SpawnCollectEffect()
    {
        if (_collectEffect != null)
        {
            GameObject effect = Instantiate(_collectEffect, transform.position, transform.rotation);
            Destroy(effect, _collectEffectDuration);
        }
    }
    
    private void DisableVisuals()
    {
        if (_coinRenderer != null)
        {
            _coinRenderer.enabled = false;
        }
        
        if (_coinCollider != null)
        {
            _coinCollider.enabled = false;
        }
    }
    
    public void ResetCoin()
    {
        _isCollected = false;
        
        if (_coinRenderer != null)
        {
            _coinRenderer.enabled = true;
        }
        
        if (_coinCollider != null)
        {
            _coinCollider.enabled = true;
        }
    }
}