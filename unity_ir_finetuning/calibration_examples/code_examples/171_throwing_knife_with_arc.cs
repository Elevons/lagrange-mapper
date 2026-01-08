// Prompt: throwing knife with arc
// Type: general

using UnityEngine;

public class ThrowingKnife : MonoBehaviour
{
    [Header("Throwing Settings")]
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _arcHeight = 3f;
    [SerializeField] private float _rotationSpeed = 720f;
    [SerializeField] private float _maxThrowDistance = 20f;
    
    [Header("Physics")]
    [SerializeField] private float _gravityScale = 1f;
    [SerializeField] private LayerMask _hitLayers = -1;
    
    [Header("Effects")]
    [SerializeField] private GameObject _hitEffectPrefab;
    [SerializeField] private AudioClip _throwSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private TrailRenderer _trailRenderer;
    
    [Header("Damage")]
    [SerializeField] private float _damage = 25f;
    [SerializeField] private bool _destroyOnHit = true;
    [SerializeField] private float _lifeTime = 5f;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private AudioSource _audioSource;
    private bool _hasBeenThrown = false;
    private bool _hasHit = false;
    private Vector3 _initialPosition;
    private float _timeAlive = 0f;
    
    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<CapsuleCollider>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody.useGravity = false;
        _collider.isTrigger = true;
        _initialPosition = transform.position;
    }
    
    private void Start()
    {
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
    }
    
    private void Update()
    {
        if (_hasBeenThrown && !_hasHit)
        {
            _timeAlive += Time.deltaTime;
            
            if (_timeAlive >= _lifeTime)
            {
                DestroyKnife();
                return;
            }
            
            transform.Rotate(Vector3.forward * _rotationSpeed * Time.deltaTime);
            
            Vector3 currentVelocity = _rigidbody.velocity;
            currentVelocity.y -= Physics.gravity.y * _gravityScale * Time.deltaTime;
            _rigidbody.velocity = currentVelocity;
            
            if (Vector3.Distance(_initialPosition, transform.position) > _maxThrowDistance)
            {
                DestroyKnife();
            }
        }
    }
    
    public void ThrowKnife(Vector3 targetPosition)
    {
        if (_hasBeenThrown) return;
        
        _hasBeenThrown = true;
        _initialPosition = transform.position;
        
        Vector3 direction = (targetPosition - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPosition);
        
        Vector3 velocity = CalculateArcVelocity(transform.position, targetPosition, _arcHeight);
        
        _rigidbody.velocity = velocity;
        transform.LookAt(targetPosition);
        
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = true;
        }
        
        PlaySound(_throwSound);
    }
    
    public void ThrowKnife(Vector3 direction, float force)
    {
        if (_hasBeenThrown) return;
        
        _hasBeenThrown = true;
        _initialPosition = transform.position;
        
        Vector3 throwDirection = direction.normalized;
        throwDirection.y += 0.3f;
        
        _rigidbody.velocity = throwDirection * force;
        transform.LookAt(transform.position + throwDirection);
        
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = true;
        }
        
        PlaySound(_throwSound);
    }
    
    private Vector3 CalculateArcVelocity(Vector3 startPos, Vector3 endPos, float arcHeight)
    {
        Vector3 direction = endPos - startPos;
        Vector3 directionXZ = new Vector3(direction.x, 0, direction.z);
        
        float time = directionXZ.magnitude / _throwForce;
        
        Vector3 velocityXZ = directionXZ / time;
        float velocityY = (direction.y + 0.5f * Physics.gravity.y * time * time) / time;
        velocityY += Mathf.Sqrt(2 * Physics.gravity.y * arcHeight);
        
        return new Vector3(velocityXZ.x, velocityY, velocityXZ.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_hasBeenThrown || _hasHit) return;
        
        if (((1 << other.gameObject.layer) & _hitLayers) == 0) return;
        
        _hasHit = true;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
        
        HandleHit(other);
        PlaySound(_hitSound);
        
        if (_hitEffectPrefab != null)
        {
            Instantiate(_hitEffectPrefab, transform.position, transform.rotation);
        }
        
        if (_destroyOnHit)
        {
            Invoke(nameof(DestroyKnife), 0.1f);
        }
    }
    
    private void HandleHit(Collider hitCollider)
    {
        if (hitCollider.CompareTag("Player"))
        {
            ApplyDamageToPlayer(hitCollider.gameObject);
        }
        else if (hitCollider.CompareTag("Enemy"))
        {
            ApplyDamageToEnemy(hitCollider.gameObject);
        }
        
        StickToSurface(hitCollider);
    }
    
    private void ApplyDamageToPlayer(GameObject player)
    {
        var playerRigidbody = player.GetComponent<Rigidbody>();
        if (playerRigidbody != null)
        {
            Vector3 knockback = transform.forward * 5f;
            playerRigidbody.AddForce(knockback, ForceMode.Impulse);
        }
    }
    
    private void ApplyDamageToEnemy(GameObject enemy)
    {
        var enemyRigidbody = enemy.GetComponent<Rigidbody>();
        if (enemyRigidbody != null)
        {
            Vector3 knockback = transform.forward * 3f;
            enemyRigidbody.AddForce(knockback, ForceMode.Impulse);
        }
    }
    
    private void StickToSurface(Collider surface)
    {
        transform.SetParent(surface.transform);
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, transform.forward, out hit, 1f))
        {
            transform.position = hit.point;
            transform.rotation = Quaternion.LookRotation(hit.normal);
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void DestroyKnife()
    {
        if (_hitEffectPrefab != null)
        {
            Instantiate(_hitEffectPrefab, transform.position, transform.rotation);
        }
        
        Destroy(gameObject);
    }
    
    public void ResetKnife()
    {
        _hasBeenThrown = false;
        _hasHit = false;
        _timeAlive = 0f;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = false;
        _collider.isTrigger = true;
        transform.SetParent(null);
        
        if (_trailRenderer != null)
        {
            _trailRenderer.enabled = false;
        }
    }
    
    public bool HasBeenThrown => _hasBeenThrown;
    public bool HasHit => _hasHit;
    public float Damage => _damage;
}