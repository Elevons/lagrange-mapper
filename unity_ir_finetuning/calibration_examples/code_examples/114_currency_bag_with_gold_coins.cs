// Prompt: currency bag with gold coins
// Type: pickup

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class CurrencyBag : MonoBehaviour
{
    [Header("Currency Settings")]
    [SerializeField] private int _goldAmount = 100;
    [SerializeField] private bool _isCollected = false;
    [SerializeField] private float _collectDistance = 2f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _coinPrefab;
    [SerializeField] private Transform _coinSpawnPoint;
    [SerializeField] private int _coinCount = 5;
    [SerializeField] private float _coinSpawnRadius = 1f;
    [SerializeField] private float _coinSpawnForce = 5f;
    [SerializeField] private ParticleSystem _collectEffect;
    
    [Header("Animation")]
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.2f;
    [SerializeField] private float _rotationSpeed = 45f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private AudioClip _coinSpillSound;
    [SerializeField] private float _audioVolume = 0.7f;
    
    [Header("Events")]
    public UnityEvent<int> OnGoldCollected;
    public UnityEvent OnBagDestroyed;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    private Collider _collider;
    private Renderer _renderer;
    private bool _isSpillingCoins = false;
    
    private void Start()
    {
        _startPosition = transform.position;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.volume = _audioVolume;
        _audioSource.playOnAwake = false;
        
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)_collider).isTrigger = true;
            ((SphereCollider)_collider).radius = _collectDistance;
        }
        
        _renderer = GetComponent<Renderer>();
        
        if (_coinSpawnPoint == null)
        {
            _coinSpawnPoint = transform;
        }
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        AnimateBag();
        CheckForPlayerNearby();
    }
    
    private void AnimateBag()
    {
        float bobOffset = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = _startPosition + Vector3.up * bobOffset;
        
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }
    
    private void CheckForPlayerNearby()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _collectDistance);
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Player"))
            {
                CollectBag();
                break;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectBag();
        }
    }
    
    private void CollectBag()
    {
        if (_isCollected) return;
        
        _isCollected = true;
        
        SpillCoins();
        PlayCollectEffects();
        OnGoldCollected?.Invoke(_goldAmount);
        
        StartCoroutine(DestroyAfterDelay(2f));
    }
    
    private void SpillCoins()
    {
        if (_coinPrefab == null || _isSpillingCoins) return;
        
        _isSpillingCoins = true;
        
        if (_coinSpillSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_coinSpillSound);
        }
        
        for (int i = 0; i < _coinCount; i++)
        {
            Vector3 spawnPosition = _coinSpawnPoint.position + Random.insideUnitSphere * _coinSpawnRadius;
            spawnPosition.y = _coinSpawnPoint.position.y;
            
            GameObject coin = Instantiate(_coinPrefab, spawnPosition, Random.rotation);
            
            Rigidbody coinRb = coin.GetComponent<Rigidbody>();
            if (coinRb == null)
            {
                coinRb = coin.AddComponent<Rigidbody>();
            }
            
            Vector3 forceDirection = (spawnPosition - _coinSpawnPoint.position).normalized;
            forceDirection.y = 0.5f;
            coinRb.AddForce(forceDirection * _coinSpawnForce, ForceMode.Impulse);
            
            coin.AddComponent<CoinBehavior>().Initialize(Random.Range(5, 25));
            
            Destroy(coin, 10f);
        }
    }
    
    private void PlayCollectEffects()
    {
        if (_collectEffect != null)
        {
            _collectEffect.Play();
        }
        
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collectSound);
        }
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        OnBagDestroyed?.Invoke();
        Destroy(gameObject);
    }
    
    public void SetGoldAmount(int amount)
    {
        _goldAmount = Mathf.Max(0, amount);
    }
    
    public int GetGoldAmount()
    {
        return _goldAmount;
    }
    
    public bool IsCollected()
    {
        return _isCollected;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _collectDistance);
        
        if (_coinSpawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_coinSpawnPoint.position, _coinSpawnRadius);
        }
    }
}

[System.Serializable]
public class CoinBehavior : MonoBehaviour
{
    private int _value;
    private float _collectRadius = 1f;
    private bool _isCollected = false;
    private AudioSource _audioSource;
    
    public void Initialize(int value)
    {
        _value = value;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.volume = 0.3f;
        _audioSource.playOnAwake = false;
        
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)col).isTrigger = true;
            ((SphereCollider)col).radius = _collectRadius;
        }
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        transform.Rotate(Vector3.up, 180f * Time.deltaTime);
        
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _collectRadius);
        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Player"))
            {
                CollectCoin();
                break;
            }
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
        
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = false;
        }
        
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        Destroy(gameObject, 0.1f);
    }
    
    public int GetValue()
    {
        return _value;
    }
}