// Prompt: pole vault jump
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class PoleVaultJump : MonoBehaviour
{
    [Header("Pole Vault Settings")]
    [SerializeField] private float _runSpeed = 8f;
    [SerializeField] private float _poleLength = 4f;
    [SerializeField] private float _vaultForce = 15f;
    [SerializeField] private float _minRunDistance = 5f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Physics")]
    [SerializeField] private float _gravity = -9.81f;
    [SerializeField] private float _airDrag = 0.98f;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _runKey = KeyCode.W;
    [SerializeField] private KeyCode _plantPoleKey = KeyCode.Space;
    [SerializeField] private KeyCode _releaseKey = KeyCode.Space;
    
    [Header("Visual")]
    [SerializeField] private Transform _poleTransform;
    [SerializeField] private LineRenderer _poleRenderer;
    [SerializeField] private Transform _playerModel;
    
    [Header("Events")]
    public UnityEvent OnRunStart;
    public UnityEvent OnPoleVault;
    public UnityEvent OnLanding;
    
    private Rigidbody _rigidbody;
    private CapsuleCollider _collider;
    private Vector3 _velocity;
    private Vector3 _runStartPosition;
    private Vector3 _poleContactPoint;
    private bool _isRunning;
    private bool _isPoleVaulting;
    private bool _isPoleInGround;
    private bool _isGrounded;
    private float _runDistance;
    private float _vaultAngle;
    private VaultState _currentState;
    
    private enum VaultState
    {
        Idle,
        Running,
        PoleContact,
        Vaulting,
        Airborne,
        Landing
    }
    
    void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<CapsuleCollider>();
        }
        
        _rigidbody.useGravity = false;
        _currentState = VaultState.Idle;
        
        SetupPoleVisual();
    }
    
    void Update()
    {
        HandleInput();
        UpdateGroundCheck();
        UpdateState();
        UpdatePoleVisual();
    }
    
    void FixedUpdate()
    {
        ApplyPhysics();
        UpdateMovement();
    }
    
    void HandleInput()
    {
        switch (_currentState)
        {
            case VaultState.Idle:
                if (Input.GetKeyDown(_runKey))
                {
                    StartRun();
                }
                break;
                
            case VaultState.Running:
                if (Input.GetKeyUp(_runKey))
                {
                    StopRun();
                }
                else if (Input.GetKeyDown(_plantPoleKey) && _runDistance >= _minRunDistance)
                {
                    PlantPole();
                }
                break;
                
            case VaultState.PoleContact:
            case VaultState.Vaulting:
                if (Input.GetKeyDown(_releaseKey))
                {
                    ReleasePole();
                }
                break;
        }
    }
    
    void UpdateGroundCheck()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        _isGrounded = Physics.Raycast(rayStart, Vector3.down, _groundCheckDistance + 0.1f, _groundLayer);
    }
    
    void UpdateState()
    {
        switch (_currentState)
        {
            case VaultState.Running:
                _runDistance = Vector3.Distance(_runStartPosition, transform.position);
                if (!_isRunning)
                {
                    _currentState = VaultState.Idle;
                }
                break;
                
            case VaultState.Vaulting:
                if (!_isPoleInGround)
                {
                    _currentState = VaultState.Airborne;
                }
                break;
                
            case VaultState.Airborne:
                if (_isGrounded && _velocity.y <= 0)
                {
                    _currentState = VaultState.Landing;
                    OnLanding?.Invoke();
                }
                break;
                
            case VaultState.Landing:
                if (_velocity.magnitude < 0.5f)
                {
                    _currentState = VaultState.Idle;
                }
                break;
        }
    }
    
    void ApplyPhysics()
    {
        if (!_isGrounded || _currentState == VaultState.Vaulting || _currentState == VaultState.Airborne)
        {
            _velocity.y += _gravity * Time.fixedDeltaTime;
            _velocity *= _airDrag;
        }
        else if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = 0;
        }
    }
    
    void UpdateMovement()
    {
        switch (_currentState)
        {
            case VaultState.Running:
                Vector3 runDirection = transform.forward;
                _velocity = runDirection * _runSpeed;
                break;
                
            case VaultState.PoleContact:
                CalculateVaultTrajectory();
                break;
                
            case VaultState.Vaulting:
                PerformVault();
                break;
        }
        
        if (_velocity.magnitude > 0.01f)
        {
            _rigidbody.velocity = _velocity;
        }
    }
    
    void StartRun()
    {
        _isRunning = true;
        _currentState = VaultState.Running;
        _runStartPosition = transform.position;
        _runDistance = 0f;
        OnRunStart?.Invoke();
    }
    
    void StopRun()
    {
        _isRunning = false;
        _velocity = Vector3.zero;
    }
    
    void PlantPole()
    {
        Vector3 poleDirection = transform.forward;
        Vector3 poleEndPosition = transform.position + poleDirection * _poleLength;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position, poleDirection, out hit, _poleLength, _groundLayer))
        {
            _poleContactPoint = hit.point;
            _isPoleInGround = true;
            _currentState = VaultState.PoleContact;
            
            float distanceToContact = Vector3.Distance(transform.position, _poleContactPoint);
            _vaultAngle = Mathf.Atan2(distanceToContact, transform.position.y - _poleContactPoint.y) * Mathf.Rad2Deg;
        }
    }
    
    void CalculateVaultTrajectory()
    {
        Vector3 directionToPole = (_poleContactPoint - transform.position).normalized;
        float currentSpeed = _velocity.magnitude;
        
        Vector3 vaultDirection = Vector3.up + directionToPole;
        vaultDirection.Normalize();
        
        _velocity = vaultDirection * (currentSpeed + _vaultForce * 0.5f);
        _currentState = VaultState.Vaulting;
        OnPoleVault?.Invoke();
    }
    
    void PerformVault()
    {
        Vector3 toPole = _poleContactPoint - transform.position;
        float distanceToPole = toPole.magnitude;
        
        if (distanceToPole > _poleLength * 1.2f)
        {
            ReleasePole();
            return;
        }
        
        Vector3 centripetal = toPole.normalized * (_velocity.magnitude * _velocity.magnitude / distanceToPole);
        _velocity += centripetal * Time.fixedDeltaTime * 0.1f;
        
        Vector3 upwardForce = Vector3.up * _vaultForce * Time.fixedDeltaTime;
        _velocity += upwardForce;
    }
    
    void ReleasePole()
    {
        _isPoleInGround = false;
        _currentState = VaultState.Airborne;
        
        Vector3 releaseBoost = Vector3.up * _vaultForce * 0.3f;
        _velocity += releaseBoost;
    }
    
    void SetupPoleVisual()
    {
        if (_poleRenderer == null && _poleTransform != null)
        {
            _poleRenderer = _poleTransform.GetComponent<LineRenderer>();
            if (_poleRenderer == null)
            {
                _poleRenderer = _poleTransform.gameObject.AddComponent<LineRenderer>();
            }
        }
        
        if (_poleRenderer != null)
        {
            _poleRenderer.positionCount = 2;
            _poleRenderer.startWidth = 0.05f;
            _poleRenderer.endWidth = 0.02f;
            _poleRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _poleRenderer.color = Color.brown;
        }
    }
    
    void UpdatePoleVisual()
    {
        if (_poleRenderer == null) return;
        
        bool showPole = _currentState == VaultState.PoleContact || _currentState == VaultState.Vaulting;
        _poleRenderer.enabled = showPole;
        
        if (showPole && _isPoleInGround)
        {
            Vector3 handPosition = transform.position + Vector3.up * 1.8f;
            _poleRenderer.SetPosition(0, handPosition);
            _poleRenderer.SetPosition(1, _poleContactPoint);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _minRunDistance);
        
        if (_isPoleInGround)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(_poleContactPoint, 0.2f);
            Gizmos.DrawLine(transform.position, _poleContactPoint);
        }
        
        Gizmos.color = Color.blue;
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        Gizmos.DrawRay(rayStart, Vector3.down * (_groundCheckDistance + 0.1f));
    }
}