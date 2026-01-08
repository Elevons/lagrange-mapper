// Prompt: grappling hook movement
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class GrapplingHook : MonoBehaviour
{
    [Header("Grappling Settings")]
    [SerializeField] private float _maxGrappleDistance = 20f;
    [SerializeField] private float _grappleSpeed = 30f;
    [SerializeField] private float _swingForce = 15f;
    [SerializeField] private float _climbSpeed = 8f;
    [SerializeField] private LayerMask _grappleableLayers = -1;
    [SerializeField] private float _jointSpring = 4.5f;
    [SerializeField] private float _jointDamper = 7f;
    [SerializeField] private float _jointMassScale = 4.5f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _grappleKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode _releaseKey = KeyCode.Mouse1;
    [SerializeField] private KeyCode _climbUpKey = KeyCode.W;
    [SerializeField] private KeyCode _climbDownKey = KeyCode.S;
    
    [Header("Visual")]
    [SerializeField] private LineRenderer _grappleLine;
    [SerializeField] private Transform _grapplePoint;
    [SerializeField] private GameObject _hookPrefab;
    [SerializeField] private float _lineWidth = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _shootSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Events")]
    public UnityEvent OnGrappleStart;
    public UnityEvent OnGrappleHit;
    public UnityEvent OnGrappleRelease;
    
    private Camera _playerCamera;
    private Rigidbody _rigidbody;
    private SpringJoint _springJoint;
    private Vector3 _grappleTarget;
    private bool _isGrappling;
    private bool _isHooked;
    private GameObject _hookInstance;
    private float _originalDrag;
    private float _originalAngularDrag;
    
    private enum GrappleState
    {
        Idle,
        Shooting,
        Hooked,
        Retracting
    }
    
    private GrappleState _currentState = GrappleState.Idle;
    
    void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetupLineRenderer();
        
        _originalDrag = _rigidbody.drag;
        _originalAngularDrag = _rigidbody.angularDrag;
    }
    
    void Update()
    {
        HandleInput();
        UpdateVisuals();
        HandleClimbing();
    }
    
    void FixedUpdate()
    {
        switch (_currentState)
        {
            case GrappleState.Shooting:
                UpdateShooting();
                break;
            case GrappleState.Hooked:
                UpdateSwinging();
                break;
        }
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(_grappleKey) && !_isGrappling)
        {
            StartGrapple();
        }
        
        if (Input.GetKeyDown(_releaseKey) && _isGrappling)
        {
            ReleaseGrapple();
        }
    }
    
    void StartGrapple()
    {
        Vector3 rayOrigin = _playerCamera.transform.position;
        Vector3 rayDirection = _playerCamera.transform.forward;
        
        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, rayDirection, out hit, _maxGrappleDistance, _grappleableLayers))
        {
            _grappleTarget = hit.point;
            _isGrappling = true;
            _currentState = GrappleState.Shooting;
            
            PlaySound(_shootSound);
            OnGrappleStart?.Invoke();
            
            if (_hookPrefab != null)
            {
                _hookInstance = Instantiate(_hookPrefab, _grapplePoint.position, Quaternion.identity);
            }
        }
    }
    
    void UpdateShooting()
    {
        if (_hookInstance != null)
        {
            Vector3 direction = (_grappleTarget - _hookInstance.transform.position).normalized;
            _hookInstance.transform.position += direction * _grappleSpeed * Time.fixedDeltaTime;
            
            float distanceToTarget = Vector3.Distance(_hookInstance.transform.position, _grappleTarget);
            
            if (distanceToTarget < 0.5f)
            {
                _hookInstance.transform.position = _grappleTarget;
                CreateSpringJoint();
                _currentState = GrappleState.Hooked;
                _isHooked = true;
                
                PlaySound(_hitSound);
                OnGrappleHit?.Invoke();
            }
        }
        else
        {
            CreateSpringJoint();
            _currentState = GrappleState.Hooked;
            _isHooked = true;
            
            PlaySound(_hitSound);
            OnGrappleHit?.Invoke();
        }
    }
    
    void CreateSpringJoint()
    {
        _springJoint = gameObject.AddComponent<SpringJoint>();
        _springJoint.autoConfigureConnectedAnchor = false;
        _springJoint.connectedAnchor = _grappleTarget;
        
        float distanceFromPoint = Vector3.Distance(transform.position, _grappleTarget);
        _springJoint.maxDistance = distanceFromPoint * 0.8f;
        _springJoint.minDistance = distanceFromPoint * 0.25f;
        
        _springJoint.spring = _jointSpring;
        _springJoint.damper = _jointDamper;
        _springJoint.massScale = _jointMassScale;
        
        _rigidbody.drag = 0f;
        _rigidbody.angularDrag = 0f;
    }
    
    void UpdateSwinging()
    {
        Vector3 swingDirection = Vector3.zero;
        
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            swingDirection += -_playerCamera.transform.right;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            swingDirection += _playerCamera.transform.right;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            swingDirection += _playerCamera.transform.forward;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            swingDirection += -_playerCamera.transform.forward;
        
        swingDirection.y = 0;
        swingDirection = swingDirection.normalized;
        
        if (swingDirection != Vector3.zero)
        {
            _rigidbody.AddForce(swingDirection * _swingForce, ForceMode.Force);
        }
    }
    
    void HandleClimbing()
    {
        if (!_isHooked || _springJoint == null) return;
        
        if (Input.GetKey(_climbUpKey))
        {
            if (_springJoint.maxDistance > 2f)
            {
                _springJoint.maxDistance -= _climbSpeed * Time.deltaTime;
                _springJoint.minDistance -= _climbSpeed * Time.deltaTime;
            }
        }
        else if (Input.GetKey(_climbDownKey))
        {
            _springJoint.maxDistance += _climbSpeed * Time.deltaTime;
            _springJoint.minDistance += _climbSpeed * Time.deltaTime;
        }
    }
    
    void ReleaseGrapple()
    {
        _isGrappling = false;
        _isHooked = false;
        _currentState = GrappleState.Idle;
        
        if (_springJoint != null)
        {
            Destroy(_springJoint);
            _springJoint = null;
        }
        
        if (_hookInstance != null)
        {
            Destroy(_hookInstance);
            _hookInstance = null;
        }
        
        _rigidbody.drag = _originalDrag;
        _rigidbody.angularDrag = _originalAngularDrag;
        
        PlaySound(_releaseSound);
        OnGrappleRelease?.Invoke();
    }
    
    void UpdateVisuals()
    {
        if (_grappleLine == null) return;
        
        if (_isGrappling)
        {
            _grappleLine.enabled = true;
            _grappleLine.positionCount = 2;
            
            Vector3 startPoint = _grapplePoint != null ? _grapplePoint.position : transform.position;
            Vector3 endPoint = _isHooked ? _grappleTarget : 
                              (_hookInstance != null ? _hookInstance.transform.position : _grappleTarget);
            
            _grappleLine.SetPosition(0, startPoint);
            _grappleLine.SetPosition(1, endPoint);
        }
        else
        {
            _grappleLine.enabled = false;
        }
    }
    
    void SetupLineRenderer()
    {
        if (_grappleLine == null)
        {
            _grappleLine = gameObject.AddComponent<LineRenderer>();
        }
        
        _grappleLine.material = new Material(Shader.Find("Sprites/Default"));
        _grappleLine.color = Color.white;
        _grappleLine.startWidth = _lineWidth;
        _grappleLine.endWidth = _lineWidth;
        _grappleLine.enabled = false;
        _grappleLine.useWorldSpace = true;
    }
    
    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        if (_playerCamera != null)
        {
            Vector3 rayOrigin = _playerCamera.transform.position;
            Vector3 rayDirection = _playerCamera.transform.forward;
            Gizmos.DrawRay(rayOrigin, rayDirection * _maxGrappleDistance);
        }
        
        if (_isGrappling)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_grappleTarget, 0.5f);
        }
    }
    
    void OnDestroy()
    {
        if (_springJoint != null)
        {
            Destroy(_springJoint);
        }
        
        if (_hookInstance != null)
        {
            Destroy(_hookInstance);
        }
    }
}