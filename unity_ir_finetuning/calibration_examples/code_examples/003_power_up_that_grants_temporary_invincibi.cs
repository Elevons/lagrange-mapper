// Prompt: power-up that grants temporary invincibility
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class InvincibilityPowerUp : MonoBehaviour
{
    [Header("Power-Up Settings")]
    [SerializeField] private float _invincibilityDuration = 5f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    [SerializeField] private bool _destroyOnPickup = true;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private Color _invincibilityColor = Color.yellow;
    [SerializeField] private float _flashSpeed = 10f;
    
    [Header("Events")]
    public UnityEvent OnPowerUpCollected;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    private void Update()
    {
        AnimatePowerUp();
    }
    
    private void AnimatePowerUp()
    {
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ApplyInvincibility(other.gameObject);
            HandlePickupEffects();
            OnPowerUpCollected?.Invoke();
            
            if (_destroyOnPickup)
            {
                Destroy(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    private void ApplyInvincibility(GameObject player)
    {
        InvincibilityEffect invincibilityEffect = player.GetComponent<InvincibilityEffect>();
        
        if (invincibilityEffect == null)
        {
            invincibilityEffect = player.AddComponent<InvincibilityEffect>();
        }
        
        invincibilityEffect.ActivateInvincibility(_invincibilityDuration, _invincibilityColor, _flashSpeed);
    }
    
    private void HandlePickupEffects()
    {
        if (_pickupEffect != null)
        {
            Instantiate(_pickupEffect, transform.position, transform.rotation);
        }
        
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
    }
}

public class InvincibilityEffect : MonoBehaviour
{
    private bool _isInvincible = false;
    private Renderer[] _renderers;
    private Color[] _originalColors;
    private Collider _playerCollider;
    private Coroutine _invincibilityCoroutine;
    
    public bool IsInvincible => _isInvincible;
    
    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>();
        _originalColors = new Color[_renderers.Length];
        _playerCollider = GetComponent<Collider>();
        
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i].material != null)
            {
                _originalColors[i] = _renderers[i].material.color;
            }
        }
    }
    
    public void ActivateInvincibility(float duration, Color invincibilityColor, float flashSpeed)
    {
        if (_invincibilityCoroutine != null)
        {
            StopCoroutine(_invincibilityCoroutine);
        }
        
        _invincibilityCoroutine = StartCoroutine(InvincibilityCoroutine(duration, invincibilityColor, flashSpeed));
    }
    
    private IEnumerator InvincibilityCoroutine(float duration, Color invincibilityColor, float flashSpeed)
    {
        _isInvincible = true;
        float elapsed = 0f;
        
        if (_playerCollider != null)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), true);
        }
        
        while (elapsed < duration)
        {
            float flashValue = Mathf.Sin(Time.time * flashSpeed) * 0.5f + 0.5f;
            
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    _renderers[i].material.color = Color.Lerp(_originalColors[i], invincibilityColor, flashValue);
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _isInvincible = false;
        
        if (_playerCollider != null)
        {
            Physics.IgnoreLayerCollision(gameObject.layer, LayerMask.NameToLayer("Enemy"), false);
        }
        
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null && _renderers[i].material != null)
            {
                _renderers[i].material.color = _originalColors[i];
            }
        }
        
        _invincibilityCoroutine = null;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isInvincible && (other.CompareTag("Enemy") || other.CompareTag("Projectile")))
        {
            // Ignore damage while invincible
            return;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_isInvincible && (collision.gameObject.CompareTag("Enemy") || collision.gameObject.CompareTag("Projectile")))
        {
            // Ignore damage while invincible
            return;
        }
    }
}