// Prompt: rolling boulder trap
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class RollingBoulderTrap : MonoBehaviour
{
    [Header("Boulder Settings")]
    [SerializeField] private GameObject boulderPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float boulderSpeed = 10f;
    [SerializeField] private float boulderMass = 100f;
    [SerializeField] private float boulderLifetime = 15f;
    
    [Header("Trigger Settings")]
    [SerializeField] private bool triggerOnPlayerEnter = true;
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private float triggerDelay = 0.5f;
    [SerializeField] private string playerTag = "Player";
    
    [Header("Physics Settings")]
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private LayerMask groundLayer = 1;
    [SerializeField] private float bounceForce = 5f;
    [SerializeField] private float rollingResistance = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip boulderRollSound;
    [SerializeField] private AudioClip boulderCrashSound;
    [SerializeField] private float audioVolume = 1f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem dustEffect;
    [SerializeField] private GameObject impactEffectPrefab;
    
    [Header("Events")]
    public UnityEvent OnTrapTriggered;
    public UnityEvent OnBoulderSpawned;
    public UnityEvent OnBoulderDestroyed;
    
    private bool _hasTriggered = false;
    private AudioSource _audioSource;
    private Collider _triggerCollider;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null)
        {
            _triggerCollider.isTrigger = true;
        }
        
        if (spawnPoint == null)
        {
            spawnPoint = transform;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!triggerOnPlayerEnter) return;
        if (triggerOnce && _hasTriggered) return;
        if (!other.CompareTag(playerTag)) return;
        
        TriggerTrap();
    }
    
    public void TriggerTrap()
    {
        if (triggerOnce && _hasTriggered) return;
        
        _hasTriggered = true;
        OnTrapTriggered?.Invoke();
        
        if (triggerDelay > 0)
        {
            Invoke(nameof(SpawnBoulder), triggerDelay);
        }
        else
        {
            SpawnBoulder();
        }
    }
    
    private void SpawnBoulder()
    {
        if (boulderPrefab == null || spawnPoint == null) return;
        
        GameObject boulder = Instantiate(boulderPrefab, spawnPoint.position, spawnPoint.rotation);
        
        BoulderController boulderController = boulder.GetComponent<BoulderController>();
        if (boulderController == null)
        {
            boulderController = boulder.AddComponent<BoulderController>();
        }
        
        boulderController.Initialize(this, boulderSpeed, boulderMass, boulderLifetime, gravity, 
                                   groundLayer, bounceForce, rollingResistance);
        
        OnBoulderSpawned?.Invoke();
        
        if (boulderRollSound != null && _audioSource != null)
        {
            _audioSource.clip = boulderRollSound;
            _audioSource.volume = audioVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        if (dustEffect != null)
        {
            dustEffect.Play();
        }
    }
    
    public void OnBoulderImpact(Vector3 position)
    {
        if (impactEffectPrefab != null)
        {
            Instantiate(impactEffectPrefab, position, Quaternion.identity);
        }
        
        if (boulderCrashSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(boulderCrashSound, audioVolume);
        }
    }
    
    public void OnBoulderDestroyedCallback()
    {
        OnBoulderDestroyed?.Invoke();
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        if (dustEffect != null && dustEffect.isPlaying)
        {
            dustEffect.Stop();
        }
    }
    
    public void ResetTrap()
    {
        _hasTriggered = false;
    }
}

public class BoulderController : MonoBehaviour
{
    private RollingBoulderTrap _parentTrap;
    private float _speed;
    private float _mass;
    private float _lifetime;
    private float _gravity;
    private LayerMask _groundLayer;
    private float _bounceForce;
    private float _rollingResistance;
    
    private Rigidbody _rigidbody;
    private bool _isGrounded;
    private float _currentLifetime;
    private Vector3 _velocity;
    
    public void Initialize(RollingBoulderTrap parentTrap, float speed, float mass, float lifetime, 
                          float gravity, LayerMask groundLayer, float bounceForce, float rollingResistance)
    {
        _parentTrap = parentTrap;
        _speed = speed;
        _mass = mass;
        _lifetime = lifetime;
        _gravity = gravity;
        _groundLayer = groundLayer;
        _bounceForce = bounceForce;
        _rollingResistance = rollingResistance;
        
        SetupRigidbody();
        _velocity = transform.forward * _speed;
        _currentLifetime = _lifetime;
    }
    
    private void SetupRigidbody()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.mass = _mass;
        _rigidbody.useGravity = false;
        _rigidbody.drag = _rollingResistance;
        _rigidbody.angularDrag = 0.5f;
        
        if (GetComponent<Collider>() == null)
        {
            gameObject.AddComponent<SphereCollider>();
        }
    }
    
    private void FixedUpdate()
    {
        CheckGrounded();
        ApplyMovement();
        UpdateLifetime();
    }
    
    private void CheckGrounded()
    {
        float radius = GetComponent<Collider>().bounds.size.y * 0.5f;
        _isGrounded = Physics.CheckSphere(transform.position, radius + 0.1f, _groundLayer);
    }
    
    private void ApplyMovement()
    {
        if (!_isGrounded)
        {
            _velocity.y += _gravity * Time.fixedDeltaTime;
        }
        else
        {
            if (_velocity.y < 0)
            {
                _velocity.y = 0;
            }
        }
        
        _rigidbody.velocity = _velocity;
        
        if (_isGrounded && _velocity.magnitude > 0.1f)
        {
            float rotationSpeed = _velocity.magnitude / (GetComponent<Collider>().bounds.size.x * 0.5f);
            transform.Rotate(Vector3.right, rotationSpeed * Time.fixedDeltaTime * Mathf.Rad2Deg);
        }
    }
    
    private void UpdateLifetime()
    {
        _currentLifetime -= Time.fixedDeltaTime;
        if (_currentLifetime <= 0)
        {
            DestroyBoulder();
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            Rigidbody playerRb = collision.gameObject.GetComponent<Rigidbody>();
            if (playerRb != null)
            {
                Vector3 pushDirection = (collision.transform.position - transform.position).normalized;
                playerRb.AddForce(pushDirection * _bounceForce, ForceMode.Impulse);
            }
        }
        
        if ((_groundLayer.value & (1 << collision.gameObject.layer)) != 0)
        {
            Vector3 bounceDirection = Vector3.Reflect(_velocity.normalized, collision.contacts[0].normal);
            _velocity = bounceDirection * _velocity.magnitude * 0.7f;
            
            if (_parentTrap != null)
            {
                _parentTrap.OnBoulderImpact(collision.contacts[0].point);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("BoulderDestroyer") || other.CompareTag("DeathZone"))
        {
            DestroyBoulder();
        }
    }
    
    private void DestroyBoulder()
    {
        if (_parentTrap != null)
        {
            _parentTrap.OnBoulderDestroyedCallback();
        }
        
        Destroy(gameObject);
    }
}