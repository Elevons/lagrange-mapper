// Prompt: climbing on marked surfaces
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ClimbingSystem : MonoBehaviour
{
    [Header("Climbing Settings")]
    [SerializeField] private float _climbSpeed = 3f;
    [SerializeField] private float _climbDetectionDistance = 1.5f;
    [SerializeField] private LayerMask _climbableLayerMask = 1;
    [SerializeField] private string _climbableTag = "Climbable";
    
    [Header("Movement Controls")]
    [SerializeField] private KeyCode _climbKey = KeyCode.E;
    [SerializeField] private float _horizontalClimbSpeed = 2f;
    [SerializeField] private float _gravityScale = 1f;
    
    [Header("Detection")]
    [SerializeField] private Transform _climbDetectionPoint;
    [SerializeField] private float _climbDetectionRadius = 0.3f;
    [SerializeField] private Vector3 _climbOffset = new Vector3(0, 0, -0.5f);
    
    [Header("Events")]
    public UnityEvent OnClimbStart;
    public UnityEvent OnClimbEnd;
    
    private Rigidbody _rigidbody;
    private Collider _playerCollider;
    private bool _isClimbing;
    private bool _canClimb;
    private GameObject _currentClimbSurface;
    private Vector3 _climbDirection;
    private float _originalGravityScale;
    private bool _wasGrounded;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _playerCollider = GetComponent<Collider>();
        
        if (_rigidbody == null)
        {
            Debug.LogError("ClimbingSystem requires a Rigidbody component!");
            enabled = false;
            return;
        }
        
        if (_climbDetectionPoint == null)
        {
            GameObject detectionPoint = new GameObject("ClimbDetectionPoint");
            detectionPoint.transform.SetParent(transform);
            detectionPoint.transform.localPosition = Vector3.forward * 0.5f;
            _climbDetectionPoint = detectionPoint.transform;
        }
        
        _originalGravityScale = _rigidbody.drag;
    }
    
    private void Update()
    {
        DetectClimbableSurfaces();
        HandleClimbInput();
        
        if (_isClimbing)
        {
            HandleClimbMovement();
        }
    }
    
    private void DetectClimbableSurfaces()
    {
        Collider[] climbableObjects = Physics.OverlapSphere(
            _climbDetectionPoint.position, 
            _climbDetectionRadius, 
            _climbableLayerMask
        );
        
        _canClimb = false;
        _currentClimbSurface = null;
        
        foreach (Collider col in climbableObjects)
        {
            if (col.CompareTag(_climbableTag) && col.gameObject != gameObject)
            {
                _canClimb = true;
                _currentClimbSurface = col.gameObject;
                
                Vector3 surfaceNormal = GetSurfaceNormal(col);
                _climbDirection = -surfaceNormal;
                break;
            }
        }
        
        if (!_canClimb && _isClimbing)
        {
            StopClimbing();
        }
    }
    
    private Vector3 GetSurfaceNormal(Collider surface)
    {
        Vector3 directionToSurface = (surface.transform.position - transform.position).normalized;
        
        if (Physics.Raycast(transform.position, directionToSurface, out RaycastHit hit, _climbDetectionDistance, _climbableLayerMask))
        {
            return hit.normal;
        }
        
        return -transform.forward;
    }
    
    private void HandleClimbInput()
    {
        if (Input.GetKeyDown(_climbKey))
        {
            if (_canClimb && !_isClimbing)
            {
                StartClimbing();
            }
            else if (_isClimbing)
            {
                StopClimbing();
            }
        }
    }
    
    private void StartClimbing()
    {
        if (!_canClimb || _currentClimbSurface == null) return;
        
        _isClimbing = true;
        _rigidbody.useGravity = false;
        _rigidbody.velocity = Vector3.zero;
        
        Vector3 climbPosition = _currentClimbSurface.transform.position + _climbOffset;
        transform.position = Vector3.Lerp(transform.position, climbPosition, Time.deltaTime * 5f);
        
        OnClimbStart?.Invoke();
    }
    
    private void StopClimbing()
    {
        _isClimbing = false;
        _rigidbody.useGravity = true;
        _currentClimbSurface = null;
        
        OnClimbEnd?.Invoke();
    }
    
    private void HandleClimbMovement()
    {
        if (!_isClimbing || _currentClimbSurface == null) return;
        
        Vector3 moveDirection = Vector3.zero;
        
        float verticalInput = Input.GetAxis("Vertical");
        float horizontalInput = Input.GetAxis("Horizontal");
        
        if (Mathf.Abs(verticalInput) > 0.1f)
        {
            moveDirection += Vector3.up * verticalInput * _climbSpeed;
        }
        
        if (Mathf.Abs(horizontalInput) > 0.1f)
        {
            Vector3 rightDirection = Vector3.Cross(Vector3.up, _climbDirection).normalized;
            moveDirection += rightDirection * horizontalInput * _horizontalClimbSpeed;
        }
        
        _rigidbody.velocity = moveDirection;
        
        Vector3 targetPosition = transform.position + _climbDirection * 0.1f;
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 2f);
        
        if (_currentClimbSurface != null)
        {
            transform.LookAt(transform.position + _climbDirection, Vector3.up);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_climbableTag))
        {
            _canClimb = true;
            _currentClimbSurface = other.gameObject;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_climbableTag) && other.gameObject == _currentClimbSurface)
        {
            _canClimb = false;
            if (_isClimbing)
            {
                StopClimbing();
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_climbDetectionPoint != null)
        {
            Gizmos.color = _canClimb ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_climbDetectionPoint.position, _climbDetectionRadius);
            
            if (_isClimbing && _currentClimbSurface != null)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawRay(transform.position, _climbDirection * 2f);
            }
        }
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * _climbDetectionDistance);
    }
    
    public bool IsClimbing => _isClimbing;
    public bool CanClimb => _canClimb;
    public GameObject CurrentClimbSurface => _currentClimbSurface;
}