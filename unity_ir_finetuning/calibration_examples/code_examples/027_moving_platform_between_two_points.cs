// Prompt: moving platform between two points
// Type: environment

using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private Transform _pointA;
    [SerializeField] private Transform _pointB;
    [SerializeField] private float _speed = 2f;
    [SerializeField] private float _waitTime = 1f;
    [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Platform Settings")]
    [SerializeField] private bool _startAtPointA = true;
    [SerializeField] private bool _moveOnStart = true;
    [SerializeField] private LayerMask _passengerLayer = -1;
    
    private Vector3 _startPoint;
    private Vector3 _endPoint;
    private bool _movingToB = true;
    private float _journeyLength;
    private float _journeyTime;
    private float _waitTimer;
    private bool _isWaiting;
    private bool _isMoving;
    private Vector3 _lastPosition;
    private Vector3 _platformVelocity;
    
    private void Start()
    {
        InitializePlatform();
        
        if (_moveOnStart)
        {
            StartMoving();
        }
    }
    
    private void InitializePlatform()
    {
        if (_pointA == null || _pointB == null)
        {
            Debug.LogError("MovingPlatform: Point A and Point B must be assigned!");
            enabled = false;
            return;
        }
        
        _startPoint = _pointA.position;
        _endPoint = _pointB.position;
        _journeyLength = Vector3.Distance(_startPoint, _endPoint);
        
        if (_startAtPointA)
        {
            transform.position = _startPoint;
            _movingToB = true;
        }
        else
        {
            transform.position = _endPoint;
            _movingToB = false;
        }
        
        _lastPosition = transform.position;
    }
    
    private void Update()
    {
        if (!_isMoving) return;
        
        if (_isWaiting)
        {
            HandleWaiting();
        }
        else
        {
            HandleMovement();
        }
        
        CalculateVelocity();
    }
    
    private void HandleWaiting()
    {
        _waitTimer -= Time.deltaTime;
        
        if (_waitTimer <= 0f)
        {
            _isWaiting = false;
            _journeyTime = 0f;
            SwapPoints();
        }
    }
    
    private void HandleMovement()
    {
        _journeyTime += Time.deltaTime * _speed / _journeyLength;
        
        float curveValue = _movementCurve.Evaluate(_journeyTime);
        Vector3 currentTarget = _movingToB ? _endPoint : _startPoint;
        Vector3 currentStart = _movingToB ? _startPoint : _endPoint;
        
        transform.position = Vector3.Lerp(currentStart, currentTarget, curveValue);
        
        if (_journeyTime >= 1f)
        {
            transform.position = currentTarget;
            
            if (_waitTime > 0f)
            {
                _isWaiting = true;
                _waitTimer = _waitTime;
            }
            else
            {
                _journeyTime = 0f;
                SwapPoints();
            }
        }
    }
    
    private void SwapPoints()
    {
        _movingToB = !_movingToB;
    }
    
    private void CalculateVelocity()
    {
        _platformVelocity = (transform.position - _lastPosition) / Time.deltaTime;
        _lastPosition = transform.position;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (IsValidPassenger(other))
        {
            AttachPassenger(other.transform);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (IsValidPassenger(other))
        {
            DetachPassenger(other.transform);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (IsValidPassenger(collision.collider))
        {
            AttachPassenger(collision.transform);
        }
    }
    
    private void OnCollisionExit(Collision collision)
    {
        if (IsValidPassenger(collision.collider))
        {
            DetachPassenger(collision.transform);
        }
    }
    
    private bool IsValidPassenger(Collider collider)
    {
        return (_passengerLayer.value & (1 << collider.gameObject.layer)) != 0;
    }
    
    private void AttachPassenger(Transform passenger)
    {
        passenger.SetParent(transform);
        
        Rigidbody passengerRb = passenger.GetComponent<Rigidbody>();
        if (passengerRb != null)
        {
            passengerRb.velocity += _platformVelocity;
        }
    }
    
    private void DetachPassenger(Transform passenger)
    {
        passenger.SetParent(null);
    }
    
    public void StartMoving()
    {
        _isMoving = true;
        _journeyTime = 0f;
        _isWaiting = false;
    }
    
    public void StopMoving()
    {
        _isMoving = false;
        _isWaiting = false;
    }
    
    public void ResetToStart()
    {
        StopMoving();
        transform.position = _startAtPointA ? _startPoint : _endPoint;
        _movingToB = _startAtPointA;
        _lastPosition = transform.position;
    }
    
    private void OnDrawGizmos()
    {
        if (_pointA != null && _pointB != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_pointA.position, _pointB.position);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_pointA.position, 0.3f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_pointB.position, 0.3f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, transform.localScale);
        }
    }
}