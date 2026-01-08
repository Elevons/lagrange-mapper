// Prompt: gliding with parachute
// Type: general

using UnityEngine;

public class ParachuteGlider : MonoBehaviour
{
    [Header("Parachute Settings")]
    [SerializeField] private GameObject _parachuteModel;
    [SerializeField] private float _deployTime = 1.5f;
    [SerializeField] private float _packTime = 0.8f;
    
    [Header("Gliding Physics")]
    [SerializeField] private float _glidingGravity = 2f;
    [SerializeField] private float _normalGravity = 9.81f;
    [SerializeField] private float _horizontalSpeed = 8f;
    [SerializeField] private float _maxFallSpeed = 3f;
    [SerializeField] private float _liftForce = 5f;
    
    [Header("Controls")]
    [SerializeField] private KeyCode _deployKey = KeyCode.Space;
    [SerializeField] private float _turnSpeed = 45f;
    [SerializeField] private float _tiltAmount = 15f;
    
    [Header("Ground Detection")]
    [SerializeField] private LayerMask _groundLayer = 1;
    [SerializeField] private float _groundCheckDistance = 1.5f;
    [SerializeField] private float _autoPackHeight = 2f;
    
    private Rigidbody _rigidbody;
    private Animator _animator;
    private bool _isGliding = false;
    private bool _isDeploying = false;
    private bool _isPacking = false;
    private float _deployTimer = 0f;
    private float _originalDrag;
    private Vector3 _originalScale;
    private bool _isGrounded = false;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _originalDrag = _rigidbody.drag;
        
        if (_parachuteModel != null)
        {
            _originalScale = _parachuteModel.transform.localScale;
            _parachuteModel.transform.localScale = Vector3.zero;
            _parachuteModel.SetActive(false);
        }
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleInput();
        UpdateParachuteAnimation();
        
        if (_isGliding && !_isGrounded)
        {
            HandleGlidingMovement();
        }
        
        if (_isGrounded && _isGliding)
        {
            PackParachute();
        }
    }
    
    private void FixedUpdate()
    {
        if (_isGliding && !_isGrounded)
        {
            ApplyGlidingPhysics();
        }
    }
    
    private void CheckGrounded()
    {
        RaycastHit hit;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, _groundCheckDistance, _groundLayer);
        
        if (_isGrounded && hit.distance < _autoPackHeight && _isGliding)
        {
            PackParachute();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_deployKey))
        {
            if (!_isGliding && !_isGrounded && !_isDeploying && !_isPacking)
            {
                DeployParachute();
            }
            else if (_isGliding && !_isDeploying && !_isPacking)
            {
                PackParachute();
            }
        }
    }
    
    private void HandleGlidingMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Horizontal movement
        Vector3 moveDirection = new Vector3(horizontal, 0, vertical).normalized;
        if (moveDirection.magnitude > 0.1f)
        {
            Vector3 targetDirection = transform.TransformDirection(moveDirection);
            _rigidbody.AddForce(targetDirection * _horizontalSpeed, ForceMode.Acceleration);
        }
        
        // Turning
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            transform.Rotate(0, horizontal * _turnSpeed * Time.deltaTime, 0);
            
            // Tilt parachute
            if (_parachuteModel != null)
            {
                Vector3 tiltRotation = new Vector3(0, 0, -horizontal * _tiltAmount);
                _parachuteModel.transform.localRotation = Quaternion.Lerp(
                    _parachuteModel.transform.localRotation,
                    Quaternion.Euler(tiltRotation),
                    Time.deltaTime * 3f
                );
            }
        }
        else if (_parachuteModel != null)
        {
            _parachuteModel.transform.localRotation = Quaternion.Lerp(
                _parachuteModel.transform.localRotation,
                Quaternion.identity,
                Time.deltaTime * 3f
            );
        }
        
        // Forward lift
        if (vertical > 0.1f)
        {
            _rigidbody.AddForce(transform.forward * _liftForce * vertical, ForceMode.Acceleration);
        }
    }
    
    private void ApplyGlidingPhysics()
    {
        // Apply custom gravity
        _rigidbody.AddForce(Vector3.down * _glidingGravity, ForceMode.Acceleration);
        
        // Limit fall speed
        if (_rigidbody.velocity.y < -_maxFallSpeed)
        {
            Vector3 velocity = _rigidbody.velocity;
            velocity.y = -_maxFallSpeed;
            _rigidbody.velocity = velocity;
        }
        
        // Add air resistance
        Vector3 airResistance = -_rigidbody.velocity * 0.5f;
        airResistance.y *= 0.8f; // Less resistance on vertical movement
        _rigidbody.AddForce(airResistance, ForceMode.Acceleration);
    }
    
    private void DeployParachute()
    {
        if (_isDeploying || _isPacking || _isGliding) return;
        
        _isDeploying = true;
        _deployTimer = 0f;
        
        if (_parachuteModel != null)
        {
            _parachuteModel.SetActive(true);
        }
        
        if (_animator != null)
        {
            _animator.SetTrigger("Deploy");
        }
    }
    
    private void PackParachute()
    {
        if (_isPacking || !_isGliding) return;
        
        _isPacking = true;
        _isGliding = false;
        _deployTimer = 0f;
        
        // Restore normal physics
        _rigidbody.drag = _originalDrag;
        _rigidbody.useGravity = true;
        
        if (_animator != null)
        {
            _animator.SetTrigger("Pack");
        }
    }
    
    private void UpdateParachuteAnimation()
    {
        if (_isDeploying)
        {
            _deployTimer += Time.deltaTime;
            float progress = _deployTimer / _deployTime;
            
            if (_parachuteModel != null)
            {
                _parachuteModel.transform.localScale = Vector3.Lerp(Vector3.zero, _originalScale, progress);
            }
            
            if (progress >= 1f)
            {
                _isDeploying = false;
                _isGliding = true;
                
                // Apply gliding physics
                _rigidbody.drag = 3f;
                _rigidbody.useGravity = false;
            }
        }
        else if (_isPacking)
        {
            _deployTimer += Time.deltaTime;
            float progress = _deployTimer / _packTime;
            
            if (_parachuteModel != null)
            {
                _parachuteModel.transform.localScale = Vector3.Lerp(_originalScale, Vector3.zero, progress);
            }
            
            if (progress >= 1f)
            {
                _isPacking = false;
                
                if (_parachuteModel != null)
                {
                    _parachuteModel.SetActive(false);
                }
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, Vector3.down * _groundCheckDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position - Vector3.up * _autoPackHeight, 0.5f);
    }
}