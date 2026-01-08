// Prompt: magnet that attracts nearby coins
// Type: pickup

using UnityEngine;
using System.Collections.Generic;

public class CoinMagnet : MonoBehaviour
{
    [Header("Magnet Settings")]
    [SerializeField] private float _attractionRadius = 5f;
    [SerializeField] private float _attractionForce = 10f;
    [SerializeField] private float _maxAttractionSpeed = 15f;
    [SerializeField] private LayerMask _coinLayerMask = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _magnetEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attractionSound;
    
    [Header("Debug")]
    [SerializeField] private bool _showGizmos = true;
    
    private List<Rigidbody> _attractedCoins = new List<Rigidbody>();
    private SphereCollider _detectionCollider;
    
    void Start()
    {
        SetupDetectionCollider();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    void SetupDetectionCollider()
    {
        _detectionCollider = gameObject.AddComponent<SphereCollider>();
        _detectionCollider.isTrigger = true;
        _detectionCollider.radius = _attractionRadius;
    }
    
    void FixedUpdate()
    {
        AttractCoins();
        CleanupNullReferences();
    }
    
    void AttractCoins()
    {
        for (int i = _attractedCoins.Count - 1; i >= 0; i--)
        {
            if (_attractedCoins[i] == null)
            {
                _attractedCoins.RemoveAt(i);
                continue;
            }
            
            Rigidbody coinRb = _attractedCoins[i];
            Vector3 direction = (transform.position - coinRb.transform.position).normalized;
            float distance = Vector3.Distance(transform.position, coinRb.transform.position);
            
            if (distance > _attractionRadius)
            {
                _attractedCoins.RemoveAt(i);
                continue;
            }
            
            float forceMagnitude = _attractionForce / Mathf.Max(distance, 0.1f);
            Vector3 force = direction * forceMagnitude;
            
            coinRb.AddForce(force, ForceMode.Force);
            
            if (coinRb.velocity.magnitude > _maxAttractionSpeed)
            {
                coinRb.velocity = coinRb.velocity.normalized * _maxAttractionSpeed;
            }
        }
    }
    
    void CleanupNullReferences()
    {
        _attractedCoins.RemoveAll(coin => coin == null);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (IsValidCoin(other))
        {
            Rigidbody coinRb = other.GetComponent<Rigidbody>();
            if (coinRb != null && !_attractedCoins.Contains(coinRb))
            {
                _attractedCoins.Add(coinRb);
                PlayAttractionEffect();
            }
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (IsValidCoin(other))
        {
            Rigidbody coinRb = other.GetComponent<Rigidbody>();
            if (coinRb != null)
            {
                _attractedCoins.Remove(coinRb);
            }
        }
    }
    
    bool IsValidCoin(Collider other)
    {
        return other.CompareTag("Coin") && 
               (_coinLayerMask.value & (1 << other.gameObject.layer)) != 0;
    }
    
    void PlayAttractionEffect()
    {
        if (_magnetEffect != null && !_magnetEffect.isPlaying)
        {
            _magnetEffect.Play();
        }
        
        if (_audioSource != null && _attractionSound != null)
        {
            _audioSource.PlayOneShot(_attractionSound);
        }
    }
    
    public void SetAttractionRadius(float radius)
    {
        _attractionRadius = Mathf.Max(0f, radius);
        if (_detectionCollider != null)
        {
            _detectionCollider.radius = _attractionRadius;
        }
    }
    
    public void SetAttractionForce(float force)
    {
        _attractionForce = Mathf.Max(0f, force);
    }
    
    public int GetAttractedCoinCount()
    {
        return _attractedCoins.Count;
    }
    
    void OnDrawGizmosSelected()
    {
        if (_showGizmos)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _attractionRadius);
            
            Gizmos.color = Color.red;
            foreach (var coin in _attractedCoins)
            {
                if (coin != null)
                {
                    Gizmos.DrawLine(transform.position, coin.transform.position);
                }
            }
        }
    }
}