// Prompt: rotating coin that adds score when collected
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class CollectibleCoin : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private Vector3 _rotationSpeed = new Vector3(0, 180, 0);
    
    [Header("Collection Settings")]
    [SerializeField] private int _scoreValue = 10;
    [SerializeField] private bool _destroyOnCollect = true;
    [SerializeField] private float _collectDelay = 0f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private float _volume = 1f;
    
    [Header("Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private float _effectDuration = 2f;
    
    [Header("Events")]
    public UnityEvent<int> OnCoinCollected;
    public UnityEvent OnCollectionComplete;
    
    private AudioSource _audioSource;
    private Collider _collider;
    private Renderer _renderer;
    private bool _isCollected = false;
    
    public static int TotalScore { get; private set; } = 0;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _collectSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        _collider = GetComponent<Collider>();
        _renderer = GetComponent<Renderer>();
        
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (!_isCollected)
        {
            transform.Rotate(_rotationSpeed * Time.deltaTime);
        }
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
        
        TotalScore += _scoreValue;
        
        OnCoinCollected?.Invoke(_scoreValue);
        
        PlayCollectSound();
        SpawnCollectEffect();
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        if (_collectDelay > 0)
        {
            Invoke(nameof(CompleteCollection), _collectDelay);
        }
        else
        {
            CompleteCollection();
        }
    }
    
    private void PlayCollectSound()
    {
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.clip = _collectSound;
            _audioSource.volume = _volume;
            _audioSource.Play();
        }
    }
    
    private void SpawnCollectEffect()
    {
        if (_collectEffect != null)
        {
            GameObject effect = Instantiate(_collectEffect, transform.position, transform.rotation);
            Destroy(effect, _effectDuration);
        }
    }
    
    private void CompleteCollection()
    {
        OnCollectionComplete?.Invoke();
        
        if (_destroyOnCollect)
        {
            Destroy(gameObject);
        }
    }
    
    public static void ResetScore()
    {
        TotalScore = 0;
    }
    
    public void SetScoreValue(int value)
    {
        _scoreValue = Mathf.Max(0, value);
    }
    
    public int GetScoreValue()
    {
        return _scoreValue;
    }
    
    public bool IsCollected()
    {
        return _isCollected;
    }
}