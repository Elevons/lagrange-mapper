// Prompt: parry that reflects attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ParrySystem : MonoBehaviour
{
    [Header("Parry Settings")]
    [SerializeField] private float _parryWindow = 0.5f;
    [SerializeField] private float _parryCooldown = 1f;
    [SerializeField] private KeyCode _parryKey = KeyCode.Q;
    [SerializeField] private float _reflectionForce = 20f;
    [SerializeField] private LayerMask _projectileLayer = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _parryEffectPrefab;
    [SerializeField] private float _effectDuration = 0.3f;
    [SerializeField] private Color _parryColor = Color.cyan;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _parrySound;
    [SerializeField] private AudioClip _reflectSound;
    
    [Header("Events")]
    public UnityEvent OnParryActivated;
    public UnityEvent OnSuccessfulParry;
    public UnityEvent OnParryFailed;
    
    private bool _isParrying = false;
    private bool _canParry = true;
    private float _parryTimer = 0f;
    private AudioSource _audioSource;
    private SpriteRenderer _spriteRenderer;
    private Color _originalColor;
    private Collider2D _collider;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
            _originalColor = _spriteRenderer.color;
            
        _collider = GetComponent<Collider2D>();
        if (_collider == null)
            _collider = gameObject.AddComponent<CircleCollider2D>();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateParryState();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_parryKey) && _canParry)
        {
            StartParry();
        }
    }
    
    private void UpdateParryState()
    {
        if (_isParrying)
        {
            _parryTimer -= Time.deltaTime;
            if (_parryTimer <= 0f)
            {
                EndParry();
            }
        }
    }
    
    private void StartParry()
    {
        _isParrying = true;
        _parryTimer = _parryWindow;
        
        if (_spriteRenderer != null)
            _spriteRenderer.color = _parryColor;
            
        if (_parryEffectPrefab != null)
        {
            GameObject effect = Instantiate(_parryEffectPrefab, transform.position, transform.rotation);
            Destroy(effect, _effectDuration);
        }
        
        if (_parrySound != null && _audioSource != null)
            _audioSource.PlayOneShot(_parrySound);
            
        OnParryActivated?.Invoke();
        
        StartCoroutine(ParryCooldownCoroutine());
    }
    
    private void EndParry()
    {
        _isParrying = false;
        
        if (_spriteRenderer != null)
            _spriteRenderer.color = _originalColor;
    }
    
    private IEnumerator ParryCooldownCoroutine()
    {
        _canParry = false;
        yield return new WaitForSeconds(_parryCooldown);
        _canParry = true;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_isParrying && IsProjectile(other))
        {
            ReflectProjectile(other);
        }
    }
    
    private bool IsProjectile(Collider2D other)
    {
        return (_projectileLayer.value & (1 << other.gameObject.layer)) > 0 ||
               other.CompareTag("Projectile") ||
               other.GetComponent<Rigidbody2D>() != null;
    }
    
    private void ReflectProjectile(Collider2D projectile)
    {
        Rigidbody2D projectileRb = projectile.GetComponent<Rigidbody2D>();
        if (projectileRb == null)
            projectileRb = projectile.gameObject.AddComponent<Rigidbody2D>();
            
        Vector2 reflectionDirection = CalculateReflectionDirection(projectile.transform.position);
        projectileRb.velocity = reflectionDirection * _reflectionForce;
        
        // Change projectile ownership/layer to avoid hitting player
        if (projectile.gameObject.layer != LayerMask.NameToLayer("PlayerProjectile"))
        {
            projectile.gameObject.layer = LayerMask.NameToLayer("PlayerProjectile");
        }
        
        // Flip projectile tag if it exists
        if (projectile.CompareTag("EnemyProjectile"))
            projectile.tag = "PlayerProjectile";
            
        if (_reflectSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_reflectSound);
            
        OnSuccessfulParry?.Invoke();
        
        // Add visual effect at reflection point
        if (_parryEffectPrefab != null)
        {
            GameObject reflectEffect = Instantiate(_parryEffectPrefab, projectile.transform.position, Quaternion.identity);
            Destroy(reflectEffect, _effectDuration);
        }
    }
    
    private Vector2 CalculateReflectionDirection(Vector3 projectilePosition)
    {
        Vector2 incomingDirection = (projectilePosition - transform.position).normalized;
        Vector2 normal = -incomingDirection;
        Vector2 reflectedDirection = Vector2.Reflect(incomingDirection, normal);
        
        return reflectedDirection;
    }
    
    public bool IsCurrentlyParrying()
    {
        return _isParrying;
    }
    
    public bool CanParry()
    {
        return _canParry;
    }
    
    public float GetParryTimeRemaining()
    {
        return _isParrying ? _parryTimer : 0f;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_isParrying)
        {
            Gizmos.color = _parryColor;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}