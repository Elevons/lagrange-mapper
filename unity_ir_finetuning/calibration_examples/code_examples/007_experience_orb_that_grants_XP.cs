// Prompt: experience orb that grants XP
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ExperienceOrb : MonoBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private int _experienceValue = 10;
    [SerializeField] private float _attractionRange = 5f;
    [SerializeField] private float _attractionSpeed = 8f;
    [SerializeField] private float _collectDistance = 0.5f;
    
    [Header("Visual Settings")]
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.3f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private AnimationCurve _attractionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private float _collectVolume = 0.7f;
    
    [Header("Effects")]
    [SerializeField] private GameObject _collectEffect;
    [SerializeField] private float _lifeTime = 30f;
    
    [Header("Events")]
    public UnityEvent<int> OnExperienceCollected;
    
    private Transform _player;
    private Vector3 _startPosition;
    private bool _isBeingAttracted = false;
    private bool _isCollected = false;
    private float _bobTimer = 0f;
    private AudioSource _audioSource;
    private Rigidbody _rigidbody;
    private Collider _collider;
    
    private void Start()
    {
        _startPosition = transform.position;
        _bobTimer = Random.Range(0f, Mathf.PI * 2f);
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        _rigidbody.useGravity = false;
        _rigidbody.isKinematic = true;
        
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            SphereCollider sphereCollider = gameObject.AddComponent<SphereCollider>();
            sphereCollider.isTrigger = true;
            sphereCollider.radius = 0.5f;
            _collider = sphereCollider;
        }
        _collider.isTrigger = true;
        
        if (_lifeTime > 0f)
        {
            Destroy(gameObject, _lifeTime);
        }
        
        FindPlayer();
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        if (_player == null)
        {
            FindPlayer();
        }
        
        HandleMovement();
        HandleVisualEffects();
    }
    
    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    private void HandleMovement()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _attractionRange && !_isBeingAttracted)
        {
            _isBeingAttracted = true;
        }
        
        if (_isBeingAttracted)
        {
            Vector3 directionToPlayer = (_player.position - transform.position).normalized;
            float attractionForce = _attractionCurve.Evaluate(1f - (distanceToPlayer / _attractionRange));
            
            transform.position = Vector3.MoveTowards(
                transform.position,
                _player.position,
                _attractionSpeed * attractionForce * Time.deltaTime
            );
            
            if (distanceToPlayer <= _collectDistance)
            {
                CollectOrb();
            }
        }
        else
        {
            _bobTimer += Time.deltaTime * _bobSpeed;
            Vector3 bobOffset = Vector3.up * Mathf.Sin(_bobTimer) * _bobHeight;
            transform.position = _startPosition + bobOffset;
        }
    }
    
    private void HandleVisualEffects()
    {
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isCollected) return;
        
        if (other.CompareTag("Player"))
        {
            CollectOrb();
        }
    }
    
    private void CollectOrb()
    {
        if (_isCollected) return;
        
        _isCollected = true;
        
        PlayCollectSound();
        SpawnCollectEffect();
        
        OnExperienceCollected?.Invoke(_experienceValue);
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        Destroy(gameObject, 0.1f);
    }
    
    private void PlayCollectSound()
    {
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.clip = _collectSound;
            _audioSource.volume = _collectVolume;
            _audioSource.Play();
        }
    }
    
    private void SpawnCollectEffect()
    {
        if (_collectEffect != null)
        {
            GameObject effect = Instantiate(_collectEffect, transform.position, Quaternion.identity);
            
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
    
    public void SetExperienceValue(int value)
    {
        _experienceValue = Mathf.Max(0, value);
    }
    
    public int GetExperienceValue()
    {
        return _experienceValue;
    }
    
    public void SetAttractionRange(float range)
    {
        _attractionRange = Mathf.Max(0f, range);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _attractionRange);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _collectDistance);
    }
}