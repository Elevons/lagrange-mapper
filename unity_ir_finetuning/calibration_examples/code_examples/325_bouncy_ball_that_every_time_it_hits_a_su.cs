// Prompt: bouncy ball that every time it hits a surface bounces higher than the previous bounce, plays a pitch that increases with each bounce, changes color based on bounce count, and leaves a temporary trail - after 10 bounces it explodes into confetti particles
// Type: combat

using UnityEngine;
using System.Collections;

public class BouncyBall : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float _initialBounceForce = 10f;
    [SerializeField] private float _bounceMultiplier = 1.2f;
    [SerializeField] private int _maxBounces = 10;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip _bounceSound;
    [SerializeField] private float _basePitch = 0.5f;
    [SerializeField] private float _pitchIncrement = 0.15f;
    
    [Header("Visual Settings")]
    [SerializeField] private Color[] _bounceColors = new Color[]
    {
        Color.white, Color.yellow, Color.orange, Color.red, 
        Color.magenta, Color.blue, Color.cyan, Color.green,
        Color.gray, Color.black
    };
    
    [Header("Trail Settings")]
    [SerializeField] private float _trailTime = 0.5f;
    [SerializeField] private float _trailWidth = 0.1f;
    
    [Header("Explosion Settings")]
    [SerializeField] private int _confettiCount = 50;
    [SerializeField] private float _explosionForce = 15f;
    [SerializeField] private float _confettiLifetime = 3f;
    
    private Rigidbody _rigidbody;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private TrailRenderer _trailRenderer;
    private int _bounceCount = 0;
    private float _currentBounceForce;
    private bool _hasExploded = false;
    
    void Start()
    {
        InitializeComponents();
        SetupTrail();
        _currentBounceForce = _initialBounceForce;
    }
    
    void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            gameObject.AddComponent<MeshRenderer>();
            _renderer = GetComponent<Renderer>();
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (GetComponent<Collider>() == null)
            gameObject.AddComponent<SphereCollider>();
    }
    
    void SetupTrail()
    {
        _trailRenderer = GetComponent<TrailRenderer>();
        if (_trailRenderer == null)
            _trailRenderer = gameObject.AddComponent<TrailRenderer>();
            
        _trailRenderer.time = _trailTime;
        _trailRenderer.startWidth = _trailWidth;
        _trailRenderer.endWidth = 0f;
        _trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        _trailRenderer.color = Color.white;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (_hasExploded) return;
        
        _bounceCount++;
        
        if (_bounceCount >= _maxBounces)
        {
            ExplodeIntoConfetti();
            return;
        }
        
        PerformBounce(collision);
        PlayBounceSound();
        UpdateVisuals();
    }
    
    void PerformBounce(Collision collision)
    {
        Vector3 bounceDirection = Vector3.Reflect(_rigidbody.velocity.normalized, collision.contacts[0].normal);
        _currentBounceForce *= _bounceMultiplier;
        
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.AddForce(bounceDirection * _currentBounceForce, ForceMode.Impulse);
    }
    
    void PlayBounceSound()
    {
        if (_bounceSound != null && _audioSource != null)
        {
            _audioSource.clip = _bounceSound;
            _audioSource.pitch = _basePitch + (_pitchIncrement * _bounceCount);
            _audioSource.Play();
        }
    }
    
    void UpdateVisuals()
    {
        if (_renderer != null && _bounceColors.Length > 0)
        {
            int colorIndex = Mathf.Clamp(_bounceCount - 1, 0, _bounceColors.Length - 1);
            _renderer.material.color = _bounceColors[colorIndex];
            
            if (_trailRenderer != null)
                _trailRenderer.color = _bounceColors[colorIndex];
        }
    }
    
    void ExplodeIntoConfetti()
    {
        _hasExploded = true;
        
        for (int i = 0; i < _confettiCount; i++)
        {
            CreateConfettiPiece();
        }
        
        if (_audioSource != null && _bounceSound != null)
        {
            _audioSource.pitch = _basePitch + (_pitchIncrement * _maxBounces);
            _audioSource.Play();
        }
        
        StartCoroutine(DestroyAfterExplosion());
    }
    
    void CreateConfettiPiece()
    {
        GameObject confetti = GameObject.CreatePrimitive(PrimitiveType.Cube);
        confetti.transform.position = transform.position + Random.insideUnitSphere * 0.5f;
        confetti.transform.localScale = Vector3.one * Random.Range(0.05f, 0.15f);
        
        Renderer confettiRenderer = confetti.GetComponent<Renderer>();
        if (confettiRenderer != null && _bounceColors.Length > 0)
        {
            confettiRenderer.material.color = _bounceColors[Random.Range(0, _bounceColors.Length)];
        }
        
        Rigidbody confettiRb = confetti.AddComponent<Rigidbody>();
        Vector3 explosionDirection = Random.insideUnitSphere;
        confettiRb.AddForce(explosionDirection * _explosionForce, ForceMode.Impulse);
        confettiRb.AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
        
        StartCoroutine(DestroyConfetti(confetti));
    }
    
    IEnumerator DestroyConfetti(GameObject confetti)
    {
        yield return new WaitForSeconds(_confettiLifetime);
        if (confetti != null)
            Destroy(confetti);
    }
    
    IEnumerator DestroyAfterExplosion()
    {
        if (_renderer != null)
            _renderer.enabled = false;
            
        if (_trailRenderer != null)
            _trailRenderer.enabled = false;
            
        yield return new WaitForSeconds(1f);
        Destroy(gameObject);
    }
}