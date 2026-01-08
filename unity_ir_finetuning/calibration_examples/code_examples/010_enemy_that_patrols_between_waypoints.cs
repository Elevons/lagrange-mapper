// Prompt: enemy that patrols between waypoints
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class EnemyPatrol : MonoBehaviour
{
    [Header("Patrol Settings")]
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _waitTime = 2f;
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private bool _loopPatrol = true;
    [SerializeField] private bool _reverseOnEnd = true;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private string _playerTag = "Player";
    
    [Header("Events")]
    public UnityEvent OnPlayerDetected;
    public UnityEvent OnPlayerLost;
    public UnityEvent OnWaypointReached;
    
    private int _currentWaypointIndex = 0;
    private bool _isWaiting = false;
    private bool _isReversing = false;
    private float _waitTimer = 0f;
    private Vector3 _startPosition;
    private bool _playerDetected = false;
    private Transform _detectedPlayer;
    
    private void Start()
    {
        _startPosition = transform.position;
        
        if (_waypoints == null || _waypoints.Length == 0)
        {
            Debug.LogWarning($"EnemyPatrol on {gameObject.name} has no waypoints assigned!");
            enabled = false;
            return;
        }
        
        ValidateWaypoints();
        
        if (_waypoints.Length > 0)
        {
            transform.position = _waypoints[0].position;
        }
    }
    
    private void Update()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;
        
        CheckForPlayer();
        
        if (!_playerDetected)
        {
            HandlePatrol();
        }
    }
    
    private void HandlePatrol()
    {
        if (_isWaiting)
        {
            _waitTimer += Time.deltaTime;
            if (_waitTimer >= _waitTime)
            {
                _isWaiting = false;
                _waitTimer = 0f;
                MoveToNextWaypoint();
            }
            return;
        }
        
        Transform targetWaypoint = _waypoints[_currentWaypointIndex];
        if (targetWaypoint == null) return;
        
        Vector3 targetPosition = targetWaypoint.position;
        Vector3 direction = (targetPosition - transform.position).normalized;
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
        
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, _moveSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            OnWaypointReached?.Invoke();
            
            if (_waitTime > 0f)
            {
                _isWaiting = true;
            }
            else
            {
                MoveToNextWaypoint();
            }
        }
    }
    
    private void MoveToNextWaypoint()
    {
        if (_waypoints.Length <= 1) return;
        
        if (_loopPatrol)
        {
            _currentWaypointIndex = (_currentWaypointIndex + 1) % _waypoints.Length;
        }
        else if (_reverseOnEnd)
        {
            if (!_isReversing)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _waypoints.Length)
                {
                    _currentWaypointIndex = _waypoints.Length - 2;
                    _isReversing = true;
                }
            }
            else
            {
                _currentWaypointIndex--;
                if (_currentWaypointIndex < 0)
                {
                    _currentWaypointIndex = 1;
                    _isReversing = false;
                }
            }
        }
        else
        {
            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                _currentWaypointIndex = _waypoints.Length - 1;
            }
        }
    }
    
    private void CheckForPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRadius, _playerLayer);
        bool playerFound = false;
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag(_playerTag))
            {
                if (!_playerDetected)
                {
                    _playerDetected = true;
                    _detectedPlayer = col.transform;
                    OnPlayerDetected?.Invoke();
                }
                playerFound = true;
                break;
            }
        }
        
        if (!playerFound && _playerDetected)
        {
            _playerDetected = false;
            _detectedPlayer = null;
            OnPlayerLost?.Invoke();
        }
    }
    
    private void ValidateWaypoints()
    {
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null)
            {
                Debug.LogWarning($"EnemyPatrol on {gameObject.name} has null waypoint at index {i}");
            }
        }
    }
    
    public void SetWaypoints(Transform[] newWaypoints)
    {
        _waypoints = newWaypoints;
        _currentWaypointIndex = 0;
        _isReversing = false;
        ValidateWaypoints();
    }
    
    public void AddWaypoint(Transform waypoint)
    {
        if (waypoint == null) return;
        
        Transform[] newWaypoints = new Transform[_waypoints.Length + 1];
        for (int i = 0; i < _waypoints.Length; i++)
        {
            newWaypoints[i] = _waypoints[i];
        }
        newWaypoints[_waypoints.Length] = waypoint;
        _waypoints = newWaypoints;
    }
    
    public void PausePatrol()
    {
        enabled = false;
    }
    
    public void ResumePatrol()
    {
        enabled = true;
    }
    
    public void ResetToStart()
    {
        _currentWaypointIndex = 0;
        _isReversing = false;
        _isWaiting = false;
        _waitTimer = 0f;
        _playerDetected = false;
        _detectedPlayer = null;
        
        if (_waypoints != null && _waypoints.Length > 0 && _waypoints[0] != null)
        {
            transform.position = _waypoints[0].position;
        }
        else
        {
            transform.position = _startPosition;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_waypoints == null || _waypoints.Length == 0) return;
        
        Gizmos.color = Color.blue;
        for (int i = 0; i < _waypoints.Length; i++)
        {
            if (_waypoints[i] == null) continue;
            
            Gizmos.DrawWireSphere(_waypoints[i].position, 0.5f);
            
            if (i < _waypoints.Length - 1 && _waypoints[i + 1] != null)
            {
                Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
            }
            else if (_loopPatrol && i == _waypoints.Length - 1 && _waypoints[0] != null)
            {
                Gizmos.DrawLine(_waypoints[i].position, _waypoints[0].position);
            }
        }
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        if (_waypoints.Length > 0 && _waypoints[_currentWaypointIndex] != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_waypoints[_currentWaypointIndex].position, 0.7f);
        }
    }
}