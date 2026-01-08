// Prompt: star collectible that bounces
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class StarCollectible : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float _bounceHeight = 2f;
    [SerializeField] private float _bounceSpeed = 2f;
    [SerializeField] private AnimationCurve _bounceCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private Vector3 _rotationAxis = Vector3.up;
    
    [Header("Collection Settings")]
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private bool _destroyOnCollect = true;
    [SerializeField] private float _destroyDelay = 0f;
    
    [Header("Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private float _collectAnimationDuration = 0.5f;
    [SerializeField] private AnimationCurve _collectScaleCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Events")]
    public UnityEvent OnStarCollected;
    
    private Vector3 _startPosition;
    private float _bounceTimer;
    private bool _isCollected;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Collider _collider;
    
    private void Start()
    {
        _startPosition = transform.position;
        _bounceTimer = 0f;
        _isCollected = false;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _collectSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        UpdateBounce();
        UpdateRotation();
    }
    
    private void UpdateBounce()
    {
        _bounceTimer += Time.deltaTime * _bounceSpeed;
        float normalizedTime = (_bounceTimer % (2f * Mathf.PI)) / (2f * Mathf.PI);
        float bounceOffset = _bounceCurve.Evaluate(normalizedTime) * _bounceHeight;
        
        Vector3 newPosition = _startPosition;
        newPosition.y += Mathf.Sin(_bounceTimer) * bounceOffset;
        transform.position = newPosition;
    }
    
    private void UpdateRotation()
    {
        transform.Rotate(_rotationAxis * _rotationSpeed * Time.deltaTime);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag(_playerTag))
        {
            CollectStar();
        }
    }
    
    private void CollectStar()
    {
        if (_isCollected) return;
        
        _isCollected = true;
        
        PlayCollectSound();
        SpawnCollectEffect();
        OnStarCollected?.Invoke();
        
        if (_destroyOnCollect)
        {
            StartCoroutine(CollectAnimation());
        }
        else
        {
            DisableStar();
        }
    }
    
    private void PlayCollectSound()
    {
        if (_audioSource != null && _collectSound != null)
        {
            _audioSource.clip = _collectSound;
            _audioSource.Play();
        }
    }
    
    private void SpawnCollectEffect()
    {
        if (_collectEffect != null)
        {
            GameObject effect = Instantiate(_collectEffect, transform.position, transform.rotation);
            
            ParticleSystem particles = effect.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                Destroy(effect, particles.main.duration + particles.main.startLifetime.constantMax);
            }
            else
            {
                Destroy(effect, 2f);
            }
        }
    }
    
    private System.Collections.IEnumerator CollectAnimation()
    {
        Vector3 originalScale = transform.localScale;
        float timer = 0f;
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        while (timer < _collectAnimationDuration)
        {
            timer += Time.deltaTime;
            float normalizedTime = timer / _collectAnimationDuration;
            float scaleMultiplier = _collectScaleCurve.Evaluate(normalizedTime);
            
            transform.localScale = originalScale * scaleMultiplier;
            
            yield return null;
        }
        
        if (_destroyDelay > 0f)
        {
            yield return new WaitForSeconds(_destroyDelay);
        }
        
        Destroy(gameObject);
    }
    
    private void DisableStar()
    {
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
    
    public void ResetStar()
    {
        _isCollected = false;
        _bounceTimer = 0f;
        transform.position = _startPosition;
        transform.localScale = Vector3.one;
        
        if (_renderer != null)
        {
            _renderer.enabled = true;
        }
        
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
    
    private void OnValidate()
    {
        if (_bounceHeight < 0f)
            _bounceHeight = 0f;
            
        if (_bounceSpeed < 0f)
            _bounceSpeed = 0f;
            
        if (_collectAnimationDuration < 0f)
            _collectAnimationDuration = 0f;
            
        if (_destroyDelay < 0f)
            _destroyDelay = 0f;
    }
}