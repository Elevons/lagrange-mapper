// Prompt: ledge grab and climb up
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class LedgeGrabber : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private LayerMask _ledgeLayerMask = 1;
    [SerializeField] private float _grabDistance = 1.5f;
    [SerializeField] private float _grabHeight = 0.5f;
    [SerializeField] private Vector3 _grabOffset = new Vector3(0, 0.5f, 0);
    
    [Header("Climb Settings")]
    [SerializeField] private float _climbDuration = 1.0f;
    [SerializeField] private AnimationCurve _climbCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Vector3 _climbEndOffset = new Vector3(0, 1.5f, 0.5f);
    
    [Header("Input")]
    [SerializeField] private KeyCode _climbKey = KeyCode.Space;
    [SerializeField] private KeyCode _releaseKey = KeyCode.S;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onLedgeGrabbed;
    [SerializeField] private UnityEvent _onLedgeReleased;
    [SerializeField] private UnityEvent _onClimbComplete;
    
    private Rigidbody _rigidbody;
    private Collider _collider;
    private bool _isGrabbingLedge = false;
    private bool _isClimbing = false;
    private Transform _currentLedge;
    private Vector3 _grabPosition;
    private Vector3 _climbStartPosition;
    private Vector3 _climbEndPosition;
    private float _climbTimer = 0f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody == null)
        {
            Debug.LogError("LedgeGrabber requires a Rigidbody component!");
        }
    }
    
    private void Update()
    {
        if (_isClimbing)
        {
            HandleClimbing();
            return;
        }
        
        if (_isGrabbingLedge)
        {
            HandleLedgeInput();
        }
        else
        {
            CheckForLedgeGrab();
        }
    }
    
    private void CheckForLedgeGrab()
    {
        if (_rigidbody == null || _rigidbody.velocity.y > 0) return;
        
        Vector3 rayOrigin = transform.position + Vector3.up * _grabHeight;
        Vector3 rayDirection = transform.forward;
        
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, _grabDistance, _ledgeLayerMask))
        {
            Vector3 ledgeTop = hit.point + Vector3.up * 0.1f;
            Vector3 checkPosition = ledgeTop + rayDirection * 0.2f;
            
            if (!Physics.Raycast(checkPosition, Vector3.down, 0.3f, _ledgeLayerMask))
            {
                GrabLedge(hit.transform, hit.point);
            }
        }
    }
    
    private void GrabLedge(Transform ledge, Vector3 grabPoint)
    {
        _currentLedge = ledge;
        _isGrabbingLedge = true;
        _grabPosition = grabPoint + _grabOffset;
        
        if (_rigidbody != null)
        {
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }
        
        transform.position = _grabPosition;
        transform.LookAt(_grabPosition + transform.forward);
        
        _onLedgeGrabbed?.Invoke();
    }
    
    private void HandleLedgeInput()
    {
        if (Input.GetKeyDown(_climbKey))
        {
            StartClimbing();
        }
        else if (Input.GetKeyDown(_releaseKey))
        {
            ReleaseLedge();
        }
    }
    
    private void StartClimbing()
    {
        if (_isClimbing || _currentLedge == null) return;
        
        _isClimbing = true;
        _climbTimer = 0f;
        _climbStartPosition = transform.position;
        _climbEndPosition = _grabPosition + _climbEndOffset;
        
        Vector3 ledgeForward = _currentLedge.forward;
        if (Vector3.Dot(transform.forward, ledgeForward) < 0)
        {
            ledgeForward = -ledgeForward;
        }
        _climbEndPosition = _grabPosition + Vector3.up * _climbEndOffset.y + ledgeForward * _climbEndOffset.z;
    }
    
    private void HandleClimbing()
    {
        _climbTimer += Time.deltaTime;
        float progress = _climbTimer / _climbDuration;
        
        if (progress >= 1f)
        {
            CompleteClimb();
            return;
        }
        
        float curveValue = _climbCurve.Evaluate(progress);
        Vector3 currentPosition = Vector3.Lerp(_climbStartPosition, _climbEndPosition, curveValue);
        transform.position = currentPosition;
    }
    
    private void CompleteClimb()
    {
        _isClimbing = false;
        _isGrabbingLedge = false;
        transform.position = _climbEndPosition;
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
        }
        
        _currentLedge = null;
        _onClimbComplete?.Invoke();
    }
    
    private void ReleaseLedge()
    {
        _isGrabbingLedge = false;
        _isClimbing = false;
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = true;
        }
        
        _currentLedge = null;
        _onLedgeReleased?.Invoke();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 rayOrigin = transform.position + Vector3.up * _grabHeight;
        Gizmos.DrawRay(rayOrigin, transform.forward * _grabDistance);
        
        if (_isGrabbingLedge)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_grabPosition, 0.2f);
            
            if (_isClimbing)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(_climbEndPosition, 0.2f);
                Gizmos.DrawLine(_climbStartPosition, _climbEndPosition);
            }
        }
    }
}