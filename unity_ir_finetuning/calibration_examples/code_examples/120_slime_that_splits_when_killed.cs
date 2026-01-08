// Prompt: slime that splits when killed
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SplittingSlime : MonoBehaviour
{
    [Header("Slime Properties")]
    [SerializeField] private int _health = 3;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private float _jumpInterval = 2f;
    
    [Header("Splitting Behavior")]
    [SerializeField] private int _maxSplitGenerations = 3;
    [SerializeField] private int _currentGeneration = 0;
    [SerializeField] private int _splitCount = 2;
    [SerializeField] private float _splitForce = 3f;
    [SerializeField] private float _childSizeMultiplier = 0.7f;
    [SerializeField] private float _childHealthMultiplier = 0.5f;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Components")]
    [SerializeField] private GameObject _slimePrefab;
    [SerializeField] private ParticleSystem _deathEffect;
    [SerializeField] private AudioClip _splitSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Events")]
    public UnityEvent OnSlimeDeath;
    public UnityEvent OnSlimeSplit;
    
    private Rigidbody2D _rigidbody;
    private SpriteRenderer _spriteRenderer;
    private AudioSource _audioSource;
    private Transform _player;
    private float _nextJumpTime;
    private bool _isDead = false;
    private Vector2 _moveDirection;
    private float _directionChangeTime;
    private float _nextDirectionChange;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody2D>();
            _rigidbody.freezeRotation = true;
        }
        
        if (_spriteRenderer == null)
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    private void Start()
    {
        _nextJumpTime = Time.time + _jumpInterval;
        _nextDirectionChange = Time.time + Random.Range(1f, 3f);
        _moveDirection = Random.insideUnitCircle.normalized;
        
        AdjustSizeForGeneration();
        FindPlayer();
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        FindPlayer();
        HandleMovement();
        HandleJumping();
    }
    
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
    }
    
    private void HandleMovement()
    {
        if (Time.time >= _nextDirectionChange)
        {
            if (_player != null && Vector2.Distance(transform.position, _player.position) <= _detectionRange)
            {
                _moveDirection = (_player.position - transform.position).normalized;
            }
            else
            {
                _moveDirection = Random.insideUnitCircle.normalized;
            }
            
            _nextDirectionChange = Time.time + Random.Range(1f, 3f);
        }
        
        _rigidbody.velocity = new Vector2(_moveDirection.x * _moveSpeed, _rigidbody.velocity.y);
        
        if (_moveDirection.x > 0)
            _spriteRenderer.flipX = false;
        else if (_moveDirection.x < 0)
            _spriteRenderer.flipX = true;
    }
    
    private void HandleJumping()
    {
        if (Time.time >= _nextJumpTime && IsGrounded())
        {
            _rigidbody.AddForce(Vector2.up * _jumpForce, ForceMode2D.Impulse);
            _nextJumpTime = Time.time + _jumpInterval + Random.Range(-0.5f, 0.5f);
        }
    }
    
    private bool IsGrounded()
    {
        RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 0.6f);
        return hit.collider != null && !hit.collider.isTrigger;
    }
    
    private void AdjustSizeForGeneration()
    {
        float sizeMultiplier = Mathf.Pow(_childSizeMultiplier, _currentGeneration);
        transform.localScale = Vector3.one * sizeMultiplier;
        
        _health = Mathf.Max(1, Mathf.RoundToInt(_health * Mathf.Pow(_childHealthMultiplier, _currentGeneration)));
    }
    
    public void TakeDamage(int damage)
    {
        if (_isDead) return;
        
        _health -= damage;
        
        if (_spriteRenderer != null)
        {
            StartCoroutine(FlashRed());
        }
        
        if (_health <= 0)
        {
            Die();
        }
    }
    
    private System.Collections.IEnumerator FlashRed()
    {
        Color originalColor = _spriteRenderer.color;
        _spriteRenderer.color = Color.red;
        yield return new WaitForSeconds(0.1f);
        _spriteRenderer.color = originalColor;
    }
    
    private void Die()
    {
        if (_isDead) return;
        
        _isDead = true;
        
        if (_currentGeneration < _maxSplitGenerations)
        {
            Split();
        }
        else
        {
            CompleteDeath();
        }
    }
    
    private void Split()
    {
        OnSlimeSplit?.Invoke();
        
        if (_splitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_splitSound);
        }
        
        if (_slimePrefab != null)
        {
            for (int i = 0; i < _splitCount; i++)
            {
                Vector3 spawnPosition = transform.position + (Vector3)(Random.insideUnitCircle * 0.5f);
                GameObject newSlime = Instantiate(_slimePrefab, spawnPosition, Quaternion.identity);
                
                SplittingSlime slimeScript = newSlime.GetComponent<SplittingSlime>();
                if (slimeScript != null)
                {
                    slimeScript._currentGeneration = _currentGeneration + 1;
                    slimeScript.AdjustSizeForGeneration();
                    
                    Rigidbody2D newRb = newSlime.GetComponent<Rigidbody2D>();
                    if (newRb != null)
                    {
                        Vector2 splitDirection = Random.insideUnitCircle.normalized;
                        newRb.AddForce(splitDirection * _splitForce, ForceMode2D.Impulse);
                    }
                }
            }
        }
        
        CompleteDeath();
    }
    
    private void CompleteDeath()
    {
        OnSlimeDeath?.Invoke();
        
        if (_deathSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_deathSound);
        }
        
        if (_deathEffect != null)
        {
            ParticleSystem effect = Instantiate(_deathEffect, transform.position, Quaternion.identity);
            Destroy(effect.gameObject, 2f);
        }
        
        if (_audioSource != null && (_deathSound != null || _splitSound != null))
        {
            Destroy(gameObject, 1f);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            TakeDamage(_health);
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            TakeDamage(_health);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, Vector2.down * 0.6f);
    }
}