// Prompt: skill point orb
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SkillPointOrb : MonoBehaviour
{
    [Header("Orb Settings")]
    [SerializeField] private int _skillPointValue = 1;
    [SerializeField] private float _collectRadius = 2f;
    [SerializeField] private bool _autoCollect = true;
    [SerializeField] private float _lifeTime = 30f;
    
    [Header("Movement")]
    [SerializeField] private bool _floatMovement = true;
    [SerializeField] private float _floatSpeed = 1f;
    [SerializeField] private float _floatAmplitude = 0.5f;
    [SerializeField] private float _attractionSpeed = 8f;
    [SerializeField] private float _attractionRange = 5f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private float _pulseIntensity = 0.2f;
    
    [Header("Events")]
    public UnityEvent<int> OnSkillPointCollected;
    public UnityEvent OnOrbDestroyed;
    
    private Transform _player;
    private Vector3 _startPosition;
    private float _timeAlive;
    private bool _isBeingCollected;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Material _originalMaterial;
    private Vector3 _originalScale;
    private Collider _collider;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_renderer != null)
        {
            _originalMaterial = _renderer.material;
            _originalScale = transform.localScale;
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
            _collider.radius = _collectRadius;
        }
        
        FindPlayer();
    }
    
    private void Update()
    {
        _timeAlive += Time.deltaTime;
        
        if (_timeAlive >= _lifeTime && !_isBeingCollected)
        {
            DestroyOrb();
            return;
        }
        
        if (_isBeingCollected)
        {
            MoveTowardsPlayer();
            return;
        }
        
        if (_floatMovement)
        {
            FloatAnimation();
        }
        
        PulseEffect();
        
        if (_autoCollect && _player != null)
        {
            CheckForPlayerProximity();
        }
    }
    
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    private void FloatAnimation()
    {
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatAmplitude;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        
        transform.Rotate(0, 50f * Time.deltaTime, 0);
    }
    
    private void PulseEffect()
    {
        if (_renderer != null)
        {
            float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseIntensity;
            transform.localScale = _originalScale * pulse;
            
            if (_originalMaterial != null)
            {
                Color color = _originalMaterial.color;
                color.a = 0.7f + Mathf.Sin(Time.time * _pulseSpeed) * 0.3f;
                _renderer.material.color = color;
            }
        }
    }
    
    private void CheckForPlayerProximity()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _attractionRange && !_isBeingCollected)
        {
            _isBeingCollected = true;
        }
    }
    
    private void MoveTowardsPlayer()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        Vector3 direction = (_player.position - transform.position).normalized;
        transform.position += direction * _attractionSpeed * Time.deltaTime;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer <= 0.5f)
        {
            CollectOrb();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isBeingCollected)
        {
            if (_autoCollect)
            {
                _isBeingCollected = true;
            }
            else
            {
                CollectOrb();
            }
        }
    }
    
    private void CollectOrb()
    {
        if (_collectEffect != null)
        {
            Instantiate(_collectEffect, transform.position, Quaternion.identity);
        }
        
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collectSound);
        }
        
        OnSkillPointCollected?.Invoke(_skillPointValue);
        
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collectSound);
            Destroy(gameObject, _collectSound.length);
        }
        else
        {
            Destroy(gameObject);
        }
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
    }
    
    private void DestroyOrb()
    {
        OnOrbDestroyed?.Invoke();
        Destroy(gameObject);
    }
    
    public void SetSkillPointValue(int value)
    {
        _skillPointValue = Mathf.Max(1, value);
    }
    
    public int GetSkillPointValue()
    {
        return _skillPointValue;
    }
    
    public void ForceCollect()
    {
        if (!_isBeingCollected)
        {
            _isBeingCollected = true;
        }
    }
    
    private void OnDestroy()
    {
        if (_originalMaterial != null)
        {
            Destroy(_originalMaterial);
        }
    }
}