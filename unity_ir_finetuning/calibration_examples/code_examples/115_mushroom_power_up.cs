// Prompt: mushroom power-up
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class MushroomPowerUp : MonoBehaviour
{
    [Header("Power-Up Settings")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _bounceForce = 5f;
    [SerializeField] private int _scoreValue = 100;
    [SerializeField] private float _lifeTime = 10f;
    
    [Header("Movement")]
    [SerializeField] private bool _moveOnSpawn = true;
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private LayerMask _wallLayer = 1;
    
    [Header("Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private AudioClip _spawnSound;
    
    [Header("Events")]
    public UnityEvent<int> OnScoreAdded;
    public UnityEvent OnPowerUpCollected;
    
    private Rigidbody2D _rigidbody;
    private SpriteRenderer _spriteRenderer;
    private Collider2D _collider;
    private AudioSource _audioSource;
    private bool _isMoving;
    private int _moveDirection = 1;
    private float _groundCheckDistance = 0.6f;
    private float _wallCheckDistance = 0.6f;
    private bool _isCollected;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _collider = GetComponent<Collider2D>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody2D>();
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider2D>();
        }
        
        _collider.isTrigger = true;
    }
    
    private void Start()
    {
        InitializePowerUp();
        
        if (_spawnSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_spawnSound);
        }
        
        Destroy(gameObject, _lifeTime);
    }
    
    private void InitializePowerUp()
    {
        _isMoving = _moveOnSpawn;
        _moveDirection = Random.Range(0, 2) == 0 ? -1 : 1;
        
        if (_rigidbody != null)
        {
            _rigidbody.gravityScale = 1f;
            _rigidbody.freezeRotation = true;
        }
    }
    
    private void FixedUpdate()
    {
        if (_isCollected || !_isMoving) return;
        
        HandleMovement();
        CheckForObstacles();
    }
    
    private void HandleMovement()
    {
        if (_rigidbody == null) return;
        
        Vector2 velocity = _rigidbody.velocity;
        velocity.x = _moveSpeed * _moveDirection;
        _rigidbody.velocity = velocity;
    }
    
    private void CheckForObstacles()
    {
        Vector2 rayOrigin = transform.position;
        Vector2 rayDirection = Vector2.right * _moveDirection;
        
        RaycastHit2D wallHit = Physics2D.Raycast(rayOrigin, rayDirection, _wallCheckDistance, _wallLayer);
        if (wallHit.collider != null)
        {
            _moveDirection *= -1;
            return;
        }
        
        Vector2 groundCheckOrigin = rayOrigin + rayDirection * 0.5f;
        RaycastHit2D groundHit = Physics2D.Raycast(groundCheckOrigin, Vector2.down, _groundCheckDistance, _groundLayer);
        if (groundHit.collider == null)
        {
            _moveDirection *= -1;
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectPowerUp(other.gameObject);
        }
    }
    
    private void CollectPowerUp(GameObject player)
    {
        _isCollected = true;
        _isMoving = false;
        
        ApplyPowerUpEffect(player);
        PlayCollectEffects();
        AddScore();
        
        OnPowerUpCollected?.Invoke();
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        if (_spriteRenderer != null)
        {
            _spriteRenderer.enabled = false;
        }
        
        Destroy(gameObject, 0.5f);
    }
    
    private void ApplyPowerUpEffect(GameObject player)
    {
        PlayerGrowthEffect growthEffect = player.GetComponent<PlayerGrowthEffect>();
        if (growthEffect == null)
        {
            growthEffect = player.AddComponent<PlayerGrowthEffect>();
        }
        
        growthEffect.GrowPlayer();
    }
    
    private void PlayCollectEffects()
    {
        if (_collectEffect != null)
        {
            Instantiate(_collectEffect, transform.position, Quaternion.identity);
        }
        
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collectSound);
        }
    }
    
    private void AddScore()
    {
        OnScoreAdded?.Invoke(_scoreValue);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Vector2 rayOrigin = transform.position;
        Vector2 rayDirection = Vector2.right * _moveDirection;
        Gizmos.DrawRay(rayOrigin, rayDirection * _wallCheckDistance);
        
        Gizmos.color = Color.blue;
        Vector2 groundCheckOrigin = rayOrigin + rayDirection * 0.5f;
        Gizmos.DrawRay(groundCheckOrigin, Vector2.down * _groundCheckDistance);
    }
}

public class PlayerGrowthEffect : MonoBehaviour
{
    [SerializeField] private float _growthScale = 1.5f;
    [SerializeField] private float _growthDuration = 0.3f;
    [SerializeField] private AnimationCurve _growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private bool _isGrowing;
    private float _growthTimer;
    
    private void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale * _growthScale;
    }
    
    public void GrowPlayer()
    {
        if (_isGrowing) return;
        
        StartCoroutine(GrowthCoroutine());
    }
    
    private System.Collections.IEnumerator GrowthCoroutine()
    {
        _isGrowing = true;
        _growthTimer = 0f;
        Vector3 startScale = transform.localScale;
        
        while (_growthTimer < _growthDuration)
        {
            _growthTimer += Time.deltaTime;
            float progress = _growthTimer / _growthDuration;
            float curveValue = _growthCurve.Evaluate(progress);
            
            transform.localScale = Vector3.Lerp(startScale, _targetScale, curveValue);
            yield return null;
        }
        
        transform.localScale = _targetScale;
        _isGrowing = false;
    }
}