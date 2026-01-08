// Prompt: spider that drops from above
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SpiderDrop : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private float _detectionHeight = 10f;
    
    [Header("Drop Behavior")]
    [SerializeField] private float _dropSpeed = 8f;
    [SerializeField] private float _dropDelay = 0.5f;
    [SerializeField] private float _maxDropDistance = 15f;
    [SerializeField] private bool _returnToStart = true;
    [SerializeField] private float _returnSpeed = 3f;
    [SerializeField] private float _returnDelay = 2f;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private float _groundCheckDistance = 0.5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _dropSound;
    [SerializeField] private AudioClip _landSound;
    [SerializeField] private float _audioVolume = 1f;
    
    [Header("Events")]
    public UnityEvent OnDropStart;
    public UnityEvent OnLanded;
    public UnityEvent OnReturnComplete;
    
    private enum SpiderState
    {
        Waiting,
        Dropping,
        OnGround,
        Returning
    }
    
    private SpiderState _currentState = SpiderState.Waiting;
    private Vector3 _startPosition;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    private Transform _playerTransform;
    private float _dropTimer;
    private float _returnTimer;
    private bool _hasDropped;
    
    private void Start()
    {
        _startPosition = transform.position;
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        
        _audioSource.volume = _audioVolume;
        _audioSource.playOnAwake = false;
    }
    
    private void Update()
    {
        switch (_currentState)
        {
            case SpiderState.Waiting:
                HandleWaitingState();
                break;
            case SpiderState.Dropping:
                HandleDroppingState();
                break;
            case SpiderState.OnGround:
                HandleGroundState();
                break;
            case SpiderState.Returning:
                HandleReturningState();
                break;
        }
    }
    
    private void HandleWaitingState()
    {
        if (_hasDropped && !_returnToStart)
            return;
            
        DetectPlayer();
        
        if (_playerTransform != null && _dropTimer > 0f)
        {
            _dropTimer -= Time.deltaTime;
            if (_dropTimer <= 0f)
            {
                StartDrop();
            }
        }
    }
    
    private void HandleDroppingState()
    {
        if (IsGrounded() || HasDroppedMaxDistance())
        {
            Land();
        }
    }
    
    private void HandleGroundState()
    {
        if (_returnToStart)
        {
            _returnTimer -= Time.deltaTime;
            if (_returnTimer <= 0f)
            {
                StartReturn();
            }
        }
    }
    
    private void HandleReturningState()
    {
        Vector3 direction = (_startPosition - transform.position).normalized;
        transform.position += direction * _returnSpeed * Time.deltaTime;
        
        if (Vector3.Distance(transform.position, _startPosition) < 0.1f)
        {
            transform.position = _startPosition;
            _currentState = SpiderState.Waiting;
            _hasDropped = false;
            _rigidbody.isKinematic = true;
            _rigidbody.useGravity = false;
            OnReturnComplete?.Invoke();
        }
    }
    
    private void DetectPlayer()
    {
        Vector3 detectionCenter = transform.position;
        detectionCenter.y -= _detectionHeight * 0.5f;
        
        Collider[] hits = Physics.OverlapCylinder(
            detectionCenter - Vector3.up * _detectionHeight * 0.5f,
            detectionCenter + Vector3.up * _detectionHeight * 0.5f,
            _detectionRadius,
            _playerLayer
        );
        
        Transform closestPlayer = null;
        float closestDistance = float.MaxValue;
        
        foreach (Collider hit in hits)
        {
            if (hit.CompareTag("Player"))
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = hit.transform;
                }
            }
        }
        
        if (closestPlayer != null && _playerTransform == null)
        {
            _playerTransform = closestPlayer;
            _dropTimer = _dropDelay;
        }
        else if (closestPlayer == null)
        {
            _playerTransform = null;
            _dropTimer = 0f;
        }
    }
    
    private void StartDrop()
    {
        _currentState = SpiderState.Dropping;
        _rigidbody.isKinematic = false;
        _rigidbody.useGravity = true;
        _rigidbody.velocity = Vector3.down * _dropSpeed;
        
        PlaySound(_dropSound);
        OnDropStart?.Invoke();
    }
    
    private void Land()
    {
        _currentState = SpiderState.OnGround;
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        _rigidbody.useGravity = false;
        _returnTimer = _returnDelay;
        _hasDropped = true;
        _playerTransform = null;
        
        PlaySound(_landSound);
        OnLanded?.Invoke();
    }
    
    private void StartReturn()
    {
        _currentState = SpiderState.Returning;
    }
    
    private bool IsGrounded()
    {
        return Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance, _groundLayer);
    }
    
    private bool HasDroppedMaxDistance()
    {
        return Vector3.Distance(transform.position, _startPosition) >= _maxDropDistance;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _currentState == SpiderState.Dropping)
        {
            // Handle player collision during drop
            Land();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        center.y -= _detectionHeight * 0.5f;
        Gizmos.DrawWireCylinder(center, _detectionRadius, _detectionHeight);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.2f);
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_startPosition, 0.3f);
        }
        
        Gizmos.color = Color.green;
        Gizmos.DrawRay(transform.position, Vector3.down * _groundCheckDistance);
    }
}

public static class PhysicsExtensions
{
    public static Collider[] OverlapCylinder(Vector3 point0, Vector3 point1, float radius, LayerMask layerMask)
    {
        // Simplified cylinder overlap using capsule
        return Physics.OverlapCapsule(point0, point1, radius, layerMask);
    }
    
    public static void DrawWireCylinder(this Gizmos gizmos, Vector3 center, float radius, float height)
    {
        Vector3 top = center + Vector3.up * height * 0.5f;
        Vector3 bottom = center - Vector3.up * height * 0.5f;
        
        // Draw circles
        DrawCircle(top, radius);
        DrawCircle(bottom, radius);
        
        // Draw connecting lines
        Gizmos.DrawLine(top + Vector3.forward * radius, bottom + Vector3.forward * radius);
        Gizmos.DrawLine(top - Vector3.forward * radius, bottom - Vector3.forward * radius);
        Gizmos.DrawLine(top + Vector3.right * radius, bottom + Vector3.right * radius);
        Gizmos.DrawLine(top - Vector3.right * radius, bottom - Vector3.right * radius);
    }
    
    private static void DrawCircle(Vector3 center, float radius)
    {
        int segments = 16;
        float angleStep = 360f / segments;
        Vector3 prevPoint = center + Vector3.forward * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 newPoint = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * radius;
            Gizmos.DrawLine(prevPoint, newPoint);
            prevPoint = newPoint;
        }
    }
}