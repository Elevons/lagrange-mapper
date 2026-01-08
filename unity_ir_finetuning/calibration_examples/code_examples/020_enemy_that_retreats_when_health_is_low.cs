// Prompt: enemy that retreats when health is low
// Type: combat

using UnityEngine;
using UnityEngine.AI;

public class EnemyRetreat : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _retreatHealthThreshold = 30f;
    
    [Header("Movement Settings")]
    [SerializeField] private float _normalSpeed = 3.5f;
    [SerializeField] private float _retreatSpeed = 6f;
    [SerializeField] private float _retreatDistance = 15f;
    [SerializeField] private float _stopRetreatDistance = 20f;
    
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Visual Feedback")]
    [SerializeField] private Renderer _enemyRenderer;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _retreatColor = Color.yellow;
    
    private float _currentHealth;
    private bool _isRetreating = false;
    private Transform _player;
    private NavMeshAgent _navAgent;
    private Vector3 _retreatTarget;
    private Material _enemyMaterial;
    
    private enum EnemyState
    {
        Idle,
        Chasing,
        Retreating
    }
    
    private EnemyState _currentState = EnemyState.Idle;
    
    void Start()
    {
        _currentHealth = _maxHealth;
        _navAgent = GetComponent<NavMeshAgent>();
        
        if (_navAgent == null)
        {
            Debug.LogError("NavMeshAgent component required on " + gameObject.name);
            enabled = false;
            return;
        }
        
        _navAgent.speed = _normalSpeed;
        
        if (_enemyRenderer != null)
        {
            _enemyMaterial = _enemyRenderer.material;
            _enemyMaterial.color = _normalColor;
        }
        
        FindPlayer();
    }
    
    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        UpdateState(distanceToPlayer);
        HandleMovement(distanceToPlayer);
        UpdateVisuals();
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    void UpdateState(float distanceToPlayer)
    {
        if (_currentHealth <= _retreatHealthThreshold)
        {
            if (!_isRetreating)
            {
                StartRetreat();
            }
            
            if (distanceToPlayer >= _stopRetreatDistance)
            {
                _currentState = EnemyState.Idle;
            }
            else
            {
                _currentState = EnemyState.Retreating;
            }
        }
        else
        {
            _isRetreating = false;
            
            if (distanceToPlayer <= _detectionRange)
            {
                _currentState = EnemyState.Chasing;
            }
            else
            {
                _currentState = EnemyState.Idle;
            }
        }
    }
    
    void HandleMovement(float distanceToPlayer)
    {
        switch (_currentState)
        {
            case EnemyState.Idle:
                _navAgent.SetDestination(transform.position);
                _navAgent.speed = _normalSpeed;
                break;
                
            case EnemyState.Chasing:
                _navAgent.SetDestination(_player.position);
                _navAgent.speed = _normalSpeed;
                break;
                
            case EnemyState.Retreating:
                if (Vector3.Distance(transform.position, _retreatTarget) < 2f || !_navAgent.hasPath)
                {
                    CalculateRetreatTarget();
                }
                _navAgent.SetDestination(_retreatTarget);
                _navAgent.speed = _retreatSpeed;
                break;
        }
    }
    
    void StartRetreat()
    {
        _isRetreating = true;
        CalculateRetreatTarget();
    }
    
    void CalculateRetreatTarget()
    {
        Vector3 directionAwayFromPlayer = (transform.position - _player.position).normalized;
        Vector3 potentialTarget = transform.position + directionAwayFromPlayer * _retreatDistance;
        
        NavMeshHit hit;
        if (NavMesh.SamplePosition(potentialTarget, out hit, _retreatDistance, NavMesh.AllAreas))
        {
            _retreatTarget = hit.position;
        }
        else
        {
            Vector3[] alternativeDirections = {
                Quaternion.Euler(0, 45, 0) * directionAwayFromPlayer,
                Quaternion.Euler(0, -45, 0) * directionAwayFromPlayer,
                Quaternion.Euler(0, 90, 0) * directionAwayFromPlayer,
                Quaternion.Euler(0, -90, 0) * directionAwayFromPlayer
            };
            
            foreach (Vector3 direction in alternativeDirections)
            {
                Vector3 testTarget = transform.position + direction * _retreatDistance;
                if (NavMesh.SamplePosition(testTarget, out hit, _retreatDistance, NavMesh.AllAreas))
                {
                    _retreatTarget = hit.position;
                    return;
                }
            }
            
            _retreatTarget = transform.position;
        }
    }
    
    void UpdateVisuals()
    {
        if (_enemyMaterial != null)
        {
            Color targetColor = _isRetreating ? _retreatColor : _normalColor;
            _enemyMaterial.color = Color.Lerp(_enemyMaterial.color, targetColor, Time.deltaTime * 3f);
        }
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    void Die()
    {
        Destroy(gameObject);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _player = other.transform;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _retreatDistance);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _stopRetreatDistance);
        
        if (_isRetreating && Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _retreatTarget);
            Gizmos.DrawWireSphere(_retreatTarget, 1f);
        }
    }
}