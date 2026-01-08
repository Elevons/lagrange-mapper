// Prompt: crouch and crawl movement
// Type: movement

using UnityEngine;

public class CrouchCrawlMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _walkSpeed = 5f;
    [SerializeField] private float _crouchSpeed = 2.5f;
    [SerializeField] private float _crawlSpeed = 1.5f;
    [SerializeField] private float _acceleration = 10f;
    [SerializeField] private float _deceleration = 10f;
    
    [Header("Stance Settings")]
    [SerializeField] private float _standingHeight = 2f;
    [SerializeField] private float _crouchHeight = 1.2f;
    [SerializeField] private float _crawlHeight = 0.6f;
    [SerializeField] private float _stanceTransitionSpeed = 8f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode _crouchKey = KeyCode.LeftControl;
    [SerializeField] private KeyCode _crawlKey = KeyCode.C;
    
    [Header("Ground Check")]
    [SerializeField] private LayerMask _groundLayerMask = 1;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    
    [Header("Ceiling Check")]
    [SerializeField] private float _ceilingCheckDistance = 0.1f;
    [SerializeField] private LayerMask _ceilingLayerMask = 1;
    
    private CharacterController _characterController;
    private Camera _playerCamera;
    private Vector3 _moveDirection;
    private Vector3 _velocity;
    private float _currentSpeed;
    private float _targetHeight;
    private float _currentCameraHeight;
    private bool _isGrounded;
    private MovementStance _currentStance = MovementStance.Standing;
    
    private enum MovementStance
    {
        Standing,
        Crouching,
        Crawling
    }
    
    void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();
        
        if (_characterController == null)
        {
            Debug.LogError("CharacterController component required!");
            enabled = false;
            return;
        }
        
        if (_playerCamera == null)
        {
            _playerCamera = Camera.main;
        }
        
        _targetHeight = _standingHeight;
        _currentCameraHeight = _standingHeight - 0.1f;
        _characterController.height = _standingHeight;
    }
    
    void Update()
    {
        HandleInput();
        HandleStanceTransition();
        HandleMovement();
        UpdateCameraPosition();
    }
    
    void HandleInput()
    {
        bool crouchInput = Input.GetKey(_crouchKey);
        bool crawlInput = Input.GetKey(_crawlKey);
        
        MovementStance desiredStance = MovementStance.Standing;
        
        if (crawlInput)
        {
            desiredStance = MovementStance.Crawling;
        }
        else if (crouchInput)
        {
            desiredStance = MovementStance.Crouching;
        }
        
        if (desiredStance != _currentStance)
        {
            if (CanChangeStance(desiredStance))
            {
                _currentStance = desiredStance;
                UpdateTargetHeight();
            }
        }
    }
    
    bool CanChangeStance(MovementStance newStance)
    {
        if (newStance == MovementStance.Standing || newStance == MovementStance.Crouching)
        {
            float checkHeight = newStance == MovementStance.Standing ? _standingHeight : _crouchHeight;
            return !CheckCeiling(checkHeight);
        }
        return true;
    }
    
    bool CheckCeiling(float checkHeight)
    {
        Vector3 rayStart = transform.position + Vector3.up * _characterController.height * 0.5f;
        Vector3 rayEnd = transform.position + Vector3.up * (checkHeight * 0.5f + _ceilingCheckDistance);
        
        return Physics.Raycast(rayStart, Vector3.up, Vector3.Distance(rayStart, rayEnd), _ceilingLayerMask);
    }
    
    void UpdateTargetHeight()
    {
        switch (_currentStance)
        {
            case MovementStance.Standing:
                _targetHeight = _standingHeight;
                break;
            case MovementStance.Crouching:
                _targetHeight = _crouchHeight;
                break;
            case MovementStance.Crawling:
                _targetHeight = _crawlHeight;
                break;
        }
    }
    
    void HandleStanceTransition()
    {
        if (Mathf.Abs(_characterController.height - _targetHeight) > 0.01f)
        {
            float newHeight = Mathf.Lerp(_characterController.height, _targetHeight, Time.deltaTime * _stanceTransitionSpeed);
            
            Vector3 centerOffset = Vector3.up * (newHeight - _characterController.height) * 0.5f;
            _characterController.height = newHeight;
            _characterController.center = Vector3.up * newHeight * 0.5f;
            
            transform.position += centerOffset;
        }
    }
    
    void HandleMovement()
    {
        _isGrounded = CheckGrounded();
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 inputDirection = new Vector3(horizontal, 0, vertical).normalized;
        Vector3 worldDirection = transform.TransformDirection(inputDirection);
        
        float targetSpeed = GetCurrentMaxSpeed();
        if (inputDirection.magnitude > 0.1f)
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, Time.deltaTime * _acceleration);
        }
        else
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, Time.deltaTime * _deceleration);
        }
        
        _moveDirection = worldDirection * _currentSpeed;
        
        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }
        
        _velocity.y += Physics.gravity.y * Time.deltaTime;
        
        Vector3 finalMovement = _moveDirection + Vector3.up * _velocity.y;
        _characterController.Move(finalMovement * Time.deltaTime);
    }
    
    float GetCurrentMaxSpeed()
    {
        switch (_currentStance)
        {
            case MovementStance.Standing:
                return _walkSpeed;
            case MovementStance.Crouching:
                return _crouchSpeed;
            case MovementStance.Crawling:
                return _crawlSpeed;
            default:
                return _walkSpeed;
        }
    }
    
    bool CheckGrounded()
    {
        Vector3 rayStart = transform.position + Vector3.up * 0.1f;
        return Physics.Raycast(rayStart, Vector3.down, _characterController.height * 0.5f + _groundCheckDistance, _groundLayerMask);
    }
    
    void UpdateCameraPosition()
    {
        if (_playerCamera != null)
        {
            float targetCameraHeight = _targetHeight - 0.1f;
            _currentCameraHeight = Mathf.Lerp(_currentCameraHeight, targetCameraHeight, Time.deltaTime * _stanceTransitionSpeed);
            
            Vector3 cameraPosition = _playerCamera.transform.localPosition;
            cameraPosition.y = _currentCameraHeight;
            _playerCamera.transform.localPosition = cameraPosition;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (_characterController != null)
        {
            Gizmos.color = Color.green;
            Vector3 groundCheckStart = transform.position + Vector3.up * 0.1f;
            Vector3 groundCheckEnd = groundCheckStart + Vector3.down * (_characterController.height * 0.5f + _groundCheckDistance);
            Gizmos.DrawLine(groundCheckStart, groundCheckEnd);
            
            Gizmos.color = Color.red;
            Vector3 ceilingCheckStart = transform.position + Vector3.up * _characterController.height * 0.5f;
            Vector3 ceilingCheckEnd = ceilingCheckStart + Vector3.up * _ceilingCheckDistance;
            Gizmos.DrawLine(ceilingCheckStart, ceilingCheckEnd);
        }
    }
}