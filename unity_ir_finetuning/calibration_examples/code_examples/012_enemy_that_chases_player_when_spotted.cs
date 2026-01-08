// Prompt: enemy that chases player when spotted
// Type: combat

using UnityEngine;
using UnityEngine.AI;

public class EnemyChaser : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _fieldOfViewAngle = 60f;
    [SerializeField] private LayerMask _obstacleLayerMask = 1;
    [SerializeField] private LayerMask _playerLayerMask = 1 << 6;
    
    [Header("Chase Settings")]
    [SerializeField] private float _chaseSpeed = 5f;
    [SerializeField] private float _patrolSpeed = 2f;
    [SerializeField] private float _loseTargetTime = 5f;
    [SerializeField] private float _attackRange = 2f;
    
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] _patrolPoints;
    [SerializeField] private float _waitTimeAtPatrol = 2f;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color _detectionColor = Color.red;
    [SerializeField] private Color _patrolColor = Color.green;
    [SerializeField] private bool _showDebugGizmos = true;
    
    private NavMeshAgent _navAgent;
    private Transform _player;
    private EnemyState _currentState;
    private float _lastSeenTime;
    private Vector3 _lastKnownPlayerPosition;
    private int _currentPatrolIndex;
    private float _patrolWaitTimer;
    private bool _isWaitingAtPatrol;
    
    private enum EnemyState
    {
        Patrolling,
        Chasing,
        Searching,
        Attacking
    }
    
    void Start()
    {
        _navAgent = GetComponent<NavMeshAgent>();
        if (_navAgent == null)
        {
            Debug.LogError("NavMeshAgent component required on " + gameObject.name);
            enabled = false;
            return;
        }
        
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
        
        _currentState = EnemyState.Patrolling;
        _navAgent.speed = _patrolSpeed;
        
        if (_patrolPoints.Length > 0)
        {
            SetDestinationToPatrolPoint();
        }
    }
    
    void Update()
    {
        if (_player == null) return;
        
        switch (_currentState)
        {
            case EnemyState.Patrolling:
                HandlePatrolling();
                break;
            case EnemyState.Chasing:
                HandleChasing();
                break;
            case EnemyState.Searching:
                HandleSearching();
                break;
            case EnemyState.Attacking:
                HandleAttacking();
                break;
        }
        
        CheckForPlayer();
    }
    
    void HandlePatrolling()
    {
        if (_patrolPoints.Length == 0) return;
        
        if (!_navAgent.pathPending && _navAgent.remainingDistance < 0.5f)
        {
            if (!_isWaitingAtPatrol)
            {
                _isWaitingAtPatrol = true;
                _patrolWaitTimer = _waitTimeAtPatrol;
            }
            else
            {
                _patrolWaitTimer -= Time.deltaTime;
                if (_patrolWaitTimer <= 0f)
                {
                    _isWaitingAtPatrol = false;
                    _currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
                    SetDestinationToPatrolPoint();
                }
            }
        }
    }
    
    void HandleChasing()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _attackRange)
        {
            _currentState = EnemyState.Attacking;
            _navAgent.isStopped = true;
            return;
        }
        
        if (CanSeePlayer())
        {
            _lastSeenTime = Time.time;
            _lastKnownPlayerPosition = _player.position;
            _navAgent.SetDestination(_player.position);
        }
        else if (Time.time - _lastSeenTime > _loseTargetTime)
        {
            _currentState = EnemyState.Searching;
            _navAgent.SetDestination(_lastKnownPlayerPosition);
        }
    }
    
    void HandleSearching()
    {
        if (!_navAgent.pathPending && _navAgent.remainingDistance < 0.5f)
        {
            _currentState = EnemyState.Patrolling;
            _navAgent.speed = _patrolSpeed;
            SetDestinationToPatrolPoint();
        }
    }
    
    void HandleAttacking()
    {
        if (_player == null) return;
        
        Vector3 directionToPlayer = (_player.position - transform.position).normalized;
        transform.rotation = Quaternion.LookRotation(new Vector3(directionToPlayer.x, 0, directionToPlayer.z));
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer > _attackRange)
        {
            _currentState = EnemyState.Chasing;
            _navAgent.isStopped = false;
            _navAgent.speed = _chaseSpeed;
        }
    }
    
    void CheckForPlayer()
    {
        if (_player == null || _currentState == EnemyState.Attacking) return;
        
        if (CanSeePlayer() && _currentState != EnemyState.Chasing)
        {
            _currentState = EnemyState.Chasing;
            _navAgent.speed = _chaseSpeed;
            _navAgent.isStopped = false;
            _lastSeenTime = Time.time;
        }
    }
    
    bool CanSeePlayer()
    {
        if (_player == null) return false;
        
        Vector3 directionToPlayer = (_player.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer > _detectionRange) return false;
        
        float angleToPlayer = Vector3.Angle(transform.forward, directionToPlayer);
        if (angleToPlayer > _fieldOfViewAngle / 2f) return false;
        
        Vector3 rayOrigin = transform.position + Vector3.up * 1.5f;
        Vector3 rayTarget = _player.position + Vector3.up * 1f;
        
        if (Physics.Raycast(rayOrigin, (rayTarget - rayOrigin).normalized, out RaycastHit hit, distanceToPlayer, _obstacleLayerMask))
        {
            return false;
        }
        
        return true;
    }
    
    void SetDestinationToPatrolPoint()
    {
        if (_patrolPoints.Length == 0) return;
        
        _navAgent.SetDestination(_patrolPoints[_currentPatrolIndex].position);
    }
    
    void OnDrawGizmosSelected()
    {
        if (!_showDebugGizmos) return;
        
        Gizmos.color = _currentState == EnemyState.Chasing ? _detectionColor : _patrolColor;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        Vector3 leftBoundary = Quaternion.Euler(0, -_fieldOfViewAngle / 2f, 0) * transform.forward * _detectionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, _fieldOfViewAngle / 2f, 0) * transform.forward * _detectionRange;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, leftBoundary);
        Gizmos.DrawRay(transform.position, rightBoundary);
        
        if (_patrolPoints != null)
        {
            Gizmos.color = Color.cyan;
            for (int i = 0; i < _patrolPoints.Length; i++)
            {
                if (_patrolPoints[i] != null)
                {
                    Gizmos.DrawWireSphere(_patrolPoints[i].position, 0.5f);
                    if (i < _patrolPoints.Length - 1 && _patrolPoints[i + 1] != null)
                    {
                        Gizmos.DrawLine(_patrolPoints[i].position, _patrolPoints[i + 1].position);
                    }
                    else if (i == _patrolPoints.Length - 1 && _patrolPoints[0] != null)
                    {
                        Gizmos.DrawLine(_patrolPoints[i].position, _patrolPoints[0].position);
                    }
                }
            }
        }
    }
}