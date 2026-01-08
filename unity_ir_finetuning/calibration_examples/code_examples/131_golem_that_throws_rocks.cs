// Prompt: golem that throws rocks
// Type: general

using UnityEngine;
using System.Collections;

public class RockThrowingGolem : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private Transform _eyePosition;
    
    [Header("Rock Throwing")]
    [SerializeField] private GameObject _rockPrefab;
    [SerializeField] private Transform _throwPoint;
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _throwCooldown = 2f;
    [SerializeField] private float _rockLifetime = 5f;
    [SerializeField] private int _rockDamage = 25;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _throwTrigger = "Throw";
    [SerializeField] private string _idleBool = "IsIdle";
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _throwSound;
    [SerializeField] private AudioClip _rockHitSound;
    
    private Transform _player;
    private bool _canThrow = true;
    private bool _playerInRange = false;
    private Coroutine _throwCoroutine;
    
    private void Start()
    {
        if (_eyePosition == null)
            _eyePosition = transform;
            
        if (_throwPoint == null)
            _throwPoint = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_animator == null)
            _animator = GetComponent<Animator>();
    }
    
    private void Update()
    {
        DetectPlayer();
        
        if (_playerInRange && _canThrow && _player != null)
        {
            LookAtPlayer();
            
            if (_throwCoroutine == null)
            {
                _throwCoroutine = StartCoroutine(ThrowRockSequence());
            }
        }
        else if (!_playerInRange && _throwCoroutine != null)
        {
            StopCoroutine(_throwCoroutine);
            _throwCoroutine = null;
            
            if (_animator != null)
                _animator.SetBool(_idleBool, true);
        }
    }
    
    private void DetectPlayer()
    {
        Collider[] playersInRange = Physics.OverlapSphere(_eyePosition.position, _detectionRange, _playerLayer);
        
        _playerInRange = false;
        _player = null;
        
        foreach (Collider col in playersInRange)
        {
            if (col.CompareTag("Player"))
            {
                Vector3 directionToPlayer = (col.transform.position - _eyePosition.position).normalized;
                
                if (Physics.Raycast(_eyePosition.position, directionToPlayer, out RaycastHit hit, _detectionRange))
                {
                    if (hit.collider.CompareTag("Player"))
                    {
                        _playerInRange = true;
                        _player = col.transform;
                        break;
                    }
                }
            }
        }
    }
    
    private void LookAtPlayer()
    {
        if (_player == null) return;
        
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        
        if (direction != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, Time.deltaTime * 2f);
        }
    }
    
    private IEnumerator ThrowRockSequence()
    {
        while (_playerInRange && _player != null)
        {
            if (_canThrow)
            {
                _canThrow = false;
                
                if (_animator != null)
                {
                    _animator.SetBool(_idleBool, false);
                    _animator.SetTrigger(_throwTrigger);
                }
                
                yield return new WaitForSeconds(0.5f);
                
                ThrowRock();
                
                yield return new WaitForSeconds(_throwCooldown);
                
                _canThrow = true;
            }
            else
            {
                yield return new WaitForSeconds(0.1f);
            }
        }
        
        _throwCoroutine = null;
    }
    
    private void ThrowRock()
    {
        if (_rockPrefab == null || _player == null) return;
        
        GameObject rock = Instantiate(_rockPrefab, _throwPoint.position, Quaternion.identity);
        
        ThrownRock rockComponent = rock.GetComponent<ThrownRock>();
        if (rockComponent == null)
        {
            rockComponent = rock.AddComponent<ThrownRock>();
        }
        
        rockComponent.Initialize(_rockDamage, _rockLifetime, _rockHitSound);
        
        Rigidbody rockRb = rock.GetComponent<Rigidbody>();
        if (rockRb == null)
        {
            rockRb = rock.AddComponent<Rigidbody>();
        }
        
        Vector3 throwDirection = CalculateThrowDirection();
        rockRb.AddForce(throwDirection * _throwForce, ForceMode.Impulse);
        
        if (_audioSource != null && _throwSound != null)
        {
            _audioSource.PlayOneShot(_throwSound);
        }
    }
    
    private Vector3 CalculateThrowDirection()
    {
        Vector3 targetPosition = _player.position + Vector3.up * 1f;
        Vector3 direction = (targetPosition - _throwPoint.position).normalized;
        
        float distance = Vector3.Distance(_throwPoint.position, targetPosition);
        float gravity = Physics.gravity.magnitude;
        float angle = 30f * Mathf.Deg2Rad;
        
        float velocity = Mathf.Sqrt(distance * gravity / Mathf.Sin(2f * angle));
        
        Vector3 throwVector = new Vector3(direction.x, Mathf.Tan(angle), direction.z).normalized;
        return throwVector * velocity / _throwForce;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_eyePosition != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_eyePosition.position, _detectionRange);
        }
        
        if (_throwPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_throwPoint.position, 0.2f);
        }
    }
}

public class ThrownRock : MonoBehaviour
{
    private int _damage;
    private float _lifetime;
    private AudioClip _hitSound;
    private bool _hasHit = false;
    
    public void Initialize(int damage, float lifetime, AudioClip hitSound)
    {
        _damage = damage;
        _lifetime = lifetime;
        _hitSound = hitSound;
        
        Destroy(gameObject, _lifetime);
        
        if (GetComponent<Collider>() == null)
        {
            SphereCollider col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.2f;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasHit) return;
        
        if (other.CompareTag("Player"))
        {
            _hasHit = true;
            
            // Apply damage to player if they have a method for it
            other.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            
            PlayHitEffect();
            Destroy(gameObject);
        }
        else if (!other.CompareTag("Golem") && !other.isTrigger)
        {
            _hasHit = true;
            PlayHitEffect();
            
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            Destroy(gameObject, 2f);
        }
    }
    
    private void PlayHitEffect()
    {
        if (_hitSound != null)
        {
            AudioSource.PlayClipAtPoint(_hitSound, transform.position);
        }
        
        // Simple particle effect
        GameObject effect = new GameObject("RockHitEffect");
        effect.transform.position = transform.position;
        
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.startLifetime = 0.5f;
        main.startSpeed = 5f;
        main.maxParticles = 20;
        
        Destroy(effect, 2f);
    }
}