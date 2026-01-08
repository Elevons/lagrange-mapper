// Prompt: hoverboard floating
// Type: general

using UnityEngine;

public class HoverboardController : MonoBehaviour
{
    [Header("Hover Settings")]
    [SerializeField] private float _hoverHeight = 1.5f;
    [SerializeField] private float _hoverForce = 300f;
    [SerializeField] private float _hoverDamping = 50f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _turnSpeed = 100f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 8f;
    
    [Header("Tilt Effects")]
    [SerializeField] private float _maxTiltAngle = 15f;
    [SerializeField] private float _tiltSpeed = 3f;
    
    [Header("Hover Points")]
    [SerializeField] private Transform[] _hoverPoints;
    
    private Rigidbody _rigidbody;
    private float _currentSpeed;
    private Vector3 _moveInput;
    private float _turnInput;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.centerOfMass = Vector3.down * 0.5f;
        
        if (_hoverPoints == null || _hoverPoints.Length == 0)
        {
            CreateDefaultHoverPoints();
        }
    }
    
    private void Update()
    {
        HandleInput();
        ApplyTiltEffect();
    }
    
    private void FixedUpdate()
    {
        ApplyHoverForce();
        ApplyMovement();
        ApplyTurning();
    }
    
    private void HandleInput()
    {
        _moveInput.x = Input.GetAxis("Horizontal");
        _moveInput.z = Input.GetAxis("Vertical");
        _turnInput = Input.GetAxis("Horizontal");
    }
    
    private void ApplyHoverForce()
    {
        foreach (Transform hoverPoint in _hoverPoints)
        {
            if (hoverPoint == null) continue;
            
            RaycastHit hit;
            Vector3 rayStart = hoverPoint.position;
            Vector3 rayDirection = -hoverPoint.up;
            
            if (Physics.Raycast(rayStart, rayDirection, out hit, _hoverHeight * 2f, _groundLayer))
            {
                float distance = hit.distance;
                float hoverRatio = (_hoverHeight - distance) / _hoverHeight;
                
                if (hoverRatio > 0)
                {
                    Vector3 force = hoverPoint.up * _hoverForce * hoverRatio;
                    Vector3 velocity = _rigidbody.GetPointVelocity(hoverPoint.position);
                    float dampingForce = Vector3.Dot(velocity, hoverPoint.up) * _hoverDamping;
                    
                    _rigidbody.AddForceAtPosition(force - hoverPoint.up * dampingForce, hoverPoint.position);
                }
            }
        }
    }
    
    private void ApplyMovement()
    {
        Vector3 worldMoveInput = transform.TransformDirection(_moveInput);
        worldMoveInput.y = 0;
        
        if (_moveInput.magnitude > 0.1f)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _moveSpeed, _acceleration * Time.fixedDeltaTime);
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0, _deceleration * Time.fixedDeltaTime);
        }
        
        Vector3 targetVelocity = worldMoveInput.normalized * _currentSpeed;
        Vector3 velocityChange = targetVelocity - new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
        
        _rigidbody.AddForce(velocityChange, ForceMode.VelocityChange);
    }
    
    private void ApplyTurning()
    {
        if (Mathf.Abs(_turnInput) > 0.1f)
        {
            float torque = _turnInput * _turnSpeed * Time.fixedDeltaTime;
            _rigidbody.AddTorque(transform.up * torque);
        }
    }
    
    private void ApplyTiltEffect()
    {
        float forwardTilt = -_moveInput.z * _maxTiltAngle;
        float sideTilt = _moveInput.x * _maxTiltAngle;
        
        Quaternion targetRotation = Quaternion.Euler(forwardTilt, transform.eulerAngles.y, sideTilt);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _tiltSpeed * Time.deltaTime);
    }
    
    private void CreateDefaultHoverPoints()
    {
        _hoverPoints = new Transform[4];
        
        Vector3[] positions = {
            new Vector3(-0.5f, 0, 0.5f),   // Front Left
            new Vector3(0.5f, 0, 0.5f),    // Front Right
            new Vector3(-0.5f, 0, -0.5f),  // Back Left
            new Vector3(0.5f, 0, -0.5f)    // Back Right
        };
        
        for (int i = 0; i < positions.Length; i++)
        {
            GameObject hoverPoint = new GameObject($"HoverPoint_{i}");
            hoverPoint.transform.SetParent(transform);
            hoverPoint.transform.localPosition = positions[i];
            _hoverPoints[i] = hoverPoint.transform;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_hoverPoints == null) return;
        
        Gizmos.color = Color.blue;
        foreach (Transform hoverPoint in _hoverPoints)
        {
            if (hoverPoint == null) continue;
            
            Vector3 start = hoverPoint.position;
            Vector3 end = start - hoverPoint.up * _hoverHeight * 2f;
            
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(start - hoverPoint.up * _hoverHeight, 0.1f);
        }
    }
}