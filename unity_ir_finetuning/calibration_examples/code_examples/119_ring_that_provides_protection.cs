// Prompt: ring that provides protection
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ProtectionRing : MonoBehaviour
{
    [System.Serializable]
    public class ProtectionEvent : UnityEvent<float> { }
    
    [Header("Protection Settings")]
    [SerializeField] private float _protectionAmount = 50f;
    [SerializeField] private float _protectionDuration = 10f;
    [SerializeField] private bool _isTemporary = true;
    [SerializeField] private bool _canStackProtection = false;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _protectionEffect;
    [SerializeField] private ParticleSystem _pickupParticles;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private Color _protectionColor = Color.blue;
    
    [Header("Ring Behavior")]
    [SerializeField] private bool _rotateRing = true;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private bool _floatUpDown = true;
    [SerializeField] private float _floatAmplitude = 0.5f;
    [SerializeField] private float _floatSpeed = 2f;
    
    [Header("Events")]
    public ProtectionEvent OnProtectionGranted;
    public UnityEvent OnRingCollected;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Collider _collider;
    private bool _isCollected = false;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_collider != null)
            _collider.isTrigger = true;
            
        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.color = _protectionColor;
            _renderer.material.SetFloat("_Metallic", 0.8f);
            _renderer.material.SetFloat("_Smoothness", 0.9f);
        }
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        if (_rotateRing)
        {
            transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        }
        
        if (_floatUpDown)
        {
            float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            GrantProtection(other.gameObject);
            CollectRing();
        }
    }
    
    private void GrantProtection(GameObject player)
    {
        PlayerProtection protection = player.GetComponent<PlayerProtection>();
        if (protection == null)
        {
            protection = player.AddComponent<PlayerProtection>();
        }
        
        if (_canStackProtection || !protection.HasProtection())
        {
            protection.AddProtection(_protectionAmount, _protectionDuration, _isTemporary);
            OnProtectionGranted?.Invoke(_protectionAmount);
            
            if (_protectionEffect != null)
            {
                GameObject effect = Instantiate(_protectionEffect, player.transform);
                effect.transform.localPosition = Vector3.zero;
                
                if (_isTemporary)
                {
                    Destroy(effect, _protectionDuration);
                }
            }
        }
    }
    
    private void CollectRing()
    {
        _isCollected = true;
        
        if (_pickupParticles != null)
        {
            _pickupParticles.Play();
        }
        
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        OnRingCollected?.Invoke();
        
        if (_renderer != null)
            _renderer.enabled = false;
            
        if (_collider != null)
            _collider.enabled = false;
            
        Destroy(gameObject, 2f);
    }
    
    public void SetProtectionAmount(float amount)
    {
        _protectionAmount = amount;
    }
    
    public void SetProtectionDuration(float duration)
    {
        _protectionDuration = duration;
    }
    
    public float GetProtectionAmount()
    {
        return _protectionAmount;
    }
    
    public float GetProtectionDuration()
    {
        return _protectionDuration;
    }
}

public class PlayerProtection : MonoBehaviour
{
    private float _currentProtection = 0f;
    private float _protectionTimer = 0f;
    private bool _hasTemporaryProtection = false;
    private Renderer _playerRenderer;
    private Color _originalColor;
    private Material _originalMaterial;
    
    private void Start()
    {
        _playerRenderer = GetComponent<Renderer>();
        if (_playerRenderer != null && _playerRenderer.material != null)
        {
            _originalMaterial = _playerRenderer.material;
            _originalColor = _originalMaterial.color;
        }
    }
    
    private void Update()
    {
        if (_hasTemporaryProtection && _protectionTimer > 0f)
        {
            _protectionTimer -= Time.deltaTime;
            
            if (_protectionTimer <= 0f)
            {
                RemoveProtection();
            }
            else
            {
                UpdateProtectionVisual();
            }
        }
    }
    
    public void AddProtection(float amount, float duration, bool isTemporary)
    {
        _currentProtection += amount;
        
        if (isTemporary)
        {
            _hasTemporaryProtection = true;
            _protectionTimer = duration;
        }
        
        ApplyProtectionVisual();
    }
    
    public bool HasProtection()
    {
        return _currentProtection > 0f;
    }
    
    public float GetProtection()
    {
        return _currentProtection;
    }
    
    public bool TakeDamage(float damage)
    {
        if (_currentProtection > 0f)
        {
            _currentProtection -= damage;
            
            if (_currentProtection <= 0f)
            {
                _currentProtection = 0f;
                RemoveProtection();
            }
            
            return true;
        }
        
        return false;
    }
    
    private void ApplyProtectionVisual()
    {
        if (_playerRenderer != null && _playerRenderer.material != null)
        {
            _playerRenderer.material.color = Color.Lerp(_originalColor, Color.blue, 0.3f);
            _playerRenderer.material.SetFloat("_Metallic", 0.5f);
        }
    }
    
    private void UpdateProtectionVisual()
    {
        if (_playerRenderer != null && _playerRenderer.material != null)
        {
            float alpha = Mathf.PingPong(Time.time * 3f, 1f);
            Color protectionColor = Color.Lerp(_originalColor, Color.blue, 0.3f * alpha);
            _playerRenderer.material.color = protectionColor;
        }
    }
    
    private void RemoveProtection()
    {
        _hasTemporaryProtection = false;
        _protectionTimer = 0f;
        
        if (_playerRenderer != null && _originalMaterial != null)
        {
            _playerRenderer.material.color = _originalColor;
            _playerRenderer.material.SetFloat("_Metallic", _originalMaterial.GetFloat("_Metallic"));
        }
    }
}