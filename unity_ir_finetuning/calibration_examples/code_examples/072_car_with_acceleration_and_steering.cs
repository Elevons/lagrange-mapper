// Prompt: car with acceleration and steering
// Type: general

using UnityEngine;

public class CarController : MonoBehaviour
{
    [Header("Car Settings")]
    [SerializeField] private float _motorForce = 1500f;
    [SerializeField] private float _brakeForce = 3000f;
    [SerializeField] private float _maxSteerAngle = 30f;
    [SerializeField] private float _downForce = 100f;
    
    [Header("Wheel Colliders")]
    [SerializeField] private WheelCollider _frontLeftWheelCollider;
    [SerializeField] private WheelCollider _frontRightWheelCollider;
    [SerializeField] private WheelCollider _rearLeftWheelCollider;
    [SerializeField] private WheelCollider _rearRightWheelCollider;
    
    [Header("Wheel Transforms")]
    [SerializeField] private Transform _frontLeftWheelTransform;
    [SerializeField] private Transform _frontRightWheelTransform;
    [SerializeField] private Transform _rearLeftWheelTransform;
    [SerializeField] private Transform _rearRightWheelTransform;
    
    [Header("Input Settings")]
    [SerializeField] private string _horizontalInputAxis = "Horizontal";
    [SerializeField] private string _verticalInputAxis = "Vertical";
    [SerializeField] private KeyCode _brakeKey = KeyCode.Space;
    
    private float _horizontalInput;
    private float _verticalInput;
    private float _steerAngle;
    private bool _isBreaking;
    private Rigidbody _carRigidbody;
    
    private void Start()
    {
        _carRigidbody = GetComponent<Rigidbody>();
        if (_carRigidbody == null)
        {
            _carRigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _carRigidbody.centerOfMass = new Vector3(0, -0.5f, 0.5f);
    }
    
    private void Update()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();
    }
    
    private void FixedUpdate()
    {
        ApplyDownForce();
    }
    
    private void GetInput()
    {
        _horizontalInput = Input.GetAxis(_horizontalInputAxis);
        _verticalInput = Input.GetAxis(_verticalInputAxis);
        _isBreaking = Input.GetKey(_brakeKey);
    }
    
    private void HandleMotor()
    {
        if (_frontLeftWheelCollider == null || _frontRightWheelCollider == null) return;
        
        _frontLeftWheelCollider.motorTorque = _verticalInput * _motorForce;
        _frontRightWheelCollider.motorTorque = _verticalInput * _motorForce;
        
        float currentBrakeForce = _isBreaking ? _brakeForce : 0f;
        ApplyBreaking(currentBrakeForce);
    }
    
    private void ApplyBreaking(float brakeForce)
    {
        if (_frontRightWheelCollider != null)
            _frontRightWheelCollider.brakeTorque = brakeForce;
        if (_frontLeftWheelCollider != null)
            _frontLeftWheelCollider.brakeTorque = brakeForce;
        if (_rearLeftWheelCollider != null)
            _rearLeftWheelCollider.brakeTorque = brakeForce;
        if (_rearRightWheelCollider != null)
            _rearRightWheelCollider.brakeTorque = brakeForce;
    }
    
    private void HandleSteering()
    {
        _steerAngle = _maxSteerAngle * _horizontalInput;
        
        if (_frontLeftWheelCollider != null)
            _frontLeftWheelCollider.steerAngle = _steerAngle;
        if (_frontRightWheelCollider != null)
            _frontRightWheelCollider.steerAngle = _steerAngle;
    }
    
    private void UpdateWheels()
    {
        UpdateSingleWheel(_frontLeftWheelCollider, _frontLeftWheelTransform);
        UpdateSingleWheel(_frontRightWheelCollider, _frontRightWheelTransform);
        UpdateSingleWheel(_rearRightWheelCollider, _rearRightWheelTransform);
        UpdateSingleWheel(_rearLeftWheelCollider, _rearLeftWheelTransform);
    }
    
    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        if (wheelCollider == null || wheelTransform == null) return;
        
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }
    
    private void ApplyDownForce()
    {
        if (_carRigidbody != null)
        {
            _carRigidbody.AddForce(-transform.up * _downForce * _carRigidbody.velocity.magnitude);
        }
    }
    
    public float GetCurrentSpeed()
    {
        return _carRigidbody != null ? _carRigidbody.velocity.magnitude * 3.6f : 0f;
    }
    
    public bool IsGrounded()
    {
        return (_frontLeftWheelCollider != null && _frontLeftWheelCollider.isGrounded) ||
               (_frontRightWheelCollider != null && _frontRightWheelCollider.isGrounded) ||
               (_rearLeftWheelCollider != null && _rearLeftWheelCollider.isGrounded) ||
               (_rearRightWheelCollider != null && _rearRightWheelCollider.isGrounded);
    }
}