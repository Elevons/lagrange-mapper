// Prompt: minion that follows a leader
// Type: general

using UnityEngine;
using UnityEngine.AI;

public class MinionFollower : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform _leader;
    [SerializeField] private float _followDistance = 3f;
    [SerializeField] private float _stopDistance = 1.5f;
    [SerializeField] private float _maxFollowDistance = 10f;
    [SerializeField] private float _updateRate = 0.1f;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3.5f;
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private bool _useNavMesh = true;
    
    [Header("Behavior")]
    [SerializeField] private bool _teleportWhenTooFar = true;
    [SerializeField] private float _teleportDistance = 20f;
    [SerializeField] private LayerMask _obstacleLayerMask = 1;
    
    private NavMeshAgent _navAgent;
    private Rigidbody _rigidbody;
    private float _lastUpdateTime;
    private Vector3 _targetPosition;
    private bool _isFollowing;
    
    private void Start()
    {
        InitializeComponents();
        FindLeaderIfNull();
        _targetPosition = transform.position;
    }
    
    private void InitializeComponents()
    {
        if (_useNavMesh)
        {
            _navAgent = GetComponent<NavMeshAgent>();
            if (_navAgent != null)
            {
                _navAgent.speed = _moveSpeed;
                _navAgent.stoppingDistance = _stopDistance;
            }
        }
        else
        {
            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody == null)
            {
                _rigidbody = gameObject.AddComponent<Rigidbody>();
                _rigidbody.freezeRotation = true;
            }
        }
    }
    
    private void FindLeaderIfNull()
    {
        if (_leader == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _leader = player.transform;
            }
        }
    }
    
    private void Update()
    {
        if (_leader == null) return;
        
        if (Time.time - _lastUpdateTime >= _updateRate)
        {
            UpdateFollowBehavior();
            _lastUpdateTime = Time.time;
        }
        
        if (!_useNavMesh && _isFollowing)
        {
            MoveWithoutNavMesh();
        }
        
        RotateTowardsMovementDirection();
    }
    
    private void UpdateFollowBehavior()
    {
        float distanceToLeader = Vector3.Distance(transform.position, _leader.position);
        
        if (_teleportWhenTooFar && distanceToLeader > _teleportDistance)
        {
            TeleportToLeader();
            return;
        }
        
        if (distanceToLeader > _followDistance)
        {
            Vector3 directionToLeader = (_leader.position - transform.position).normalized;
            _targetPosition = _leader.position - directionToLeader * _followDistance;
            
            if (HasClearPath(_targetPosition))
            {
                StartFollowing();
            }
        }
        else if (distanceToLeader <= _stopDistance)
        {
            StopFollowing();
        }
    }
    
    private void StartFollowing()
    {
        _isFollowing = true;
        
        if (_useNavMesh && _navAgent != null)
        {
            _navAgent.SetDestination(_targetPosition);
        }
    }
    
    private void StopFollowing()
    {
        _isFollowing = false;
        
        if (_useNavMesh && _navAgent != null)
        {
            _navAgent.ResetPath();
        }
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
        }
    }
    
    private void MoveWithoutNavMesh()
    {
        Vector3 direction = (_targetPosition - transform.position).normalized;
        float distanceToTarget = Vector3.Distance(transform.position, _targetPosition);
        
        if (distanceToTarget > _stopDistance)
        {
            if (_rigidbody != null)
            {
                _rigidbody.velocity = direction * _moveSpeed;
            }
            else
            {
                transform.position += direction * _moveSpeed * Time.deltaTime;
            }
        }
        else
        {
            if (_rigidbody != null)
            {
                _rigidbody.velocity = Vector3.zero;
            }
        }
    }
    
    private void RotateTowardsMovementDirection()
    {
        Vector3 movementDirection = Vector3.zero;
        
        if (_useNavMesh && _navAgent != null && _navAgent.hasPath)
        {
            movementDirection = _navAgent.velocity.normalized;
        }
        else if (_rigidbody != null)
        {
            movementDirection = _rigidbody.velocity.normalized;
        }
        else if (_isFollowing)
        {
            movementDirection = (_targetPosition - transform.position).normalized;
        }
        
        if (movementDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movementDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private bool HasClearPath(Vector3 targetPos)
    {
        Vector3 direction = (targetPos - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetPos);
        
        return !Physics.Raycast(transform.position, direction, distance, _obstacleLayerMask);
    }
    
    private void TeleportToLeader()
    {
        Vector3 teleportPosition = _leader.position + Random.insideUnitSphere * 2f;
        teleportPosition.y = _leader.position.y;
        
        if (Physics.CheckSphere(teleportPosition, 0.5f, _obstacleLayerMask))
        {
            teleportPosition = _leader.position;
        }
        
        transform.position = teleportPosition;
        StopFollowing();
    }
    
    public void SetLeader(Transform newLeader)
    {
        _leader = newLeader;
    }
    
    public void SetFollowDistance(float distance)
    {
        _followDistance = Mathf.Max(0f, distance);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_leader == null) return;
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_leader.position, _followDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_leader.position, _stopDistance);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_leader.position, _teleportDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, _targetPosition);
    }
}