// Prompt: zombie that walks slowly toward player
// Type: movement

using UnityEngine;
using UnityEngine.AI;

public class ZombieController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _stopDistance = 1.5f;
    
    [Header("Animation")]
    [SerializeField] private Animator _animator;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _groanSounds;
    [SerializeField] private float _groanInterval = 3f;
    
    private Transform _player;
    private NavMeshAgent _navMeshAgent;
    private Rigidbody _rigidbody;
    private float _lastGroanTime;
    private bool _hasNavMesh;
    
    void Start()
    {
        InitializeComponents();
        FindPlayer();
        _lastGroanTime = Time.time + Random.Range(0f, _groanInterval);
    }
    
    void InitializeComponents()
    {
        _navMeshAgent = GetComponent<NavMeshAgent>();
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_navMeshAgent != null)
        {
            _hasNavMesh = true;
            _navMeshAgent.speed = _moveSpeed;
            _navMeshAgent.stoppingDistance = _stopDistance;
            _navMeshAgent.angularSpeed = _rotationSpeed;
        }
        else if (_rigidbody != null)
        {
            _rigidbody.freezeRotation = true;
        }
        
        if (_animator == null)
        {
            _animator = GetComponent<Animator>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _detectionRange)
        {
            MoveTowardsPlayer(distanceToPlayer);
            UpdateAnimation(true);
        }
        else
        {
            UpdateAnimation(false);
        }
        
        PlayRandomGroan();
    }
    
    void MoveTowardsPlayer(float distanceToPlayer)
    {
        if (distanceToPlayer <= _stopDistance)
        {
            return;
        }
        
        if (_hasNavMesh && _navMeshAgent.enabled)
        {
            _navMeshAgent.SetDestination(_player.position);
        }
        else
        {
            MoveWithoutNavMesh();
        }
    }
    
    void MoveWithoutNavMesh()
    {
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0f;
        
        if (_rigidbody != null)
        {
            Vector3 movement = direction * _moveSpeed * Time.deltaTime;
            _rigidbody.MovePosition(transform.position + movement);
        }
        else
        {
            transform.Translate(direction * _moveSpeed * Time.deltaTime, Space.World);
        }
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    void UpdateAnimation(bool isMoving)
    {
        if (_animator != null)
        {
            _animator.SetBool("IsWalking", isMoving);
            
            if (_hasNavMesh && _navMeshAgent != null)
            {
                _animator.SetFloat("Speed", _navMeshAgent.velocity.magnitude);
            }
            else
            {
                _animator.SetFloat("Speed", isMoving ? _moveSpeed : 0f);
            }
        }
    }
    
    void PlayRandomGroan()
    {
        if (_audioSource != null && _groanSounds != null && _groanSounds.Length > 0)
        {
            if (Time.time >= _lastGroanTime + _groanInterval)
            {
                AudioClip randomGroan = _groanSounds[Random.Range(0, _groanSounds.Length)];
                _audioSource.PlayOneShot(randomGroan);
                _lastGroanTime = Time.time;
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _stopDistance);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (_audioSource != null && _groanSounds != null && _groanSounds.Length > 0)
            {
                AudioClip randomGroan = _groanSounds[Random.Range(0, _groanSounds.Length)];
                _audioSource.PlayOneShot(randomGroan);
            }
        }
    }
}