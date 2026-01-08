// Prompt: go-kart racing
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class GoKartController : MonoBehaviour
{
    [Header("Movement Settings")]
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
    
    [Header("Audio")]
    [SerializeField] private AudioSource _engineAudioSource;
    [SerializeField] private AudioClip _engineIdleClip;
    [SerializeField] private AudioClip _engineRevClip;
    [SerializeField] private float _minPitch = 0.8f;
    [SerializeField] private float _maxPitch = 2.0f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _leftWheelSmoke;
    [SerializeField] private ParticleSystem _rightWheelSmoke;
    [SerializeField] private TrailRenderer _leftSkidTrail;
    [SerializeField] private TrailRenderer _rightSkidTrail;
    
    private float _horizontalInput;
    private float _verticalInput;
    private float _currentSteerAngle;
    private float _currentBreakForce;
    private bool _isBraking;
    private Rigidbody _rigidbody;
    private float _currentSpeed;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.centerOfMass = new Vector3(0, -0.5f, 0);
        
        if (_engineAudioSource != null && _engineIdleClip != null)
        {
            _engineAudioSource.clip = _engineIdleClip;
            _engineAudioSource.loop = true;
            _engineAudioSource.Play();
        }
    }
    
    private void Update()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheelPoses();
        UpdateAudio();
        UpdateEffects();
    }
    
    private void FixedUpdate()
    {
        ApplyDownForce();
        _currentSpeed = _rigidbody.velocity.magnitude * 3.6f;
    }
    
    private void GetInput()
    {
        _horizontalInput = Input.GetAxis("Horizontal");
        _verticalInput = Input.GetAxis("Vertical");
        _isBraking = Input.GetKey(KeyCode.Space);
    }
    
    private void HandleMotor()
    {
        _frontLeftWheelCollider.motorTorque = _verticalInput * _motorForce;
        _frontRightWheelCollider.motorTorque = _verticalInput * _motorForce;
        
        _currentBreakForce = _isBraking ? _brakeForce : 0f;
        ApplyBraking();
    }
    
    private void ApplyBraking()
    {
        _frontRightWheelCollider.brakeTorque = _currentBreakForce;
        _frontLeftWheelCollider.brakeTorque = _currentBreakForce;
        _rearLeftWheelCollider.brakeTorque = _currentBreakForce;
        _rearRightWheelCollider.brakeTorque = _currentBreakForce;
    }
    
    private void HandleSteering()
    {
        _currentSteerAngle = _maxSteerAngle * _horizontalInput;
        _frontLeftWheelCollider.steerAngle = _currentSteerAngle;
        _frontRightWheelCollider.steerAngle = _currentSteerAngle;
    }
    
    private void UpdateWheelPoses()
    {
        UpdateWheelPose(_frontLeftWheelCollider, _frontLeftWheelTransform);
        UpdateWheelPose(_frontRightWheelCollider, _frontRightWheelTransform);
        UpdateWheelPose(_rearRightWheelCollider, _rearRightWheelTransform);
        UpdateWheelPose(_rearLeftWheelCollider, _rearLeftWheelTransform);
    }
    
    private void UpdateWheelPose(WheelCollider collider, Transform wheelTransform)
    {
        if (collider == null || wheelTransform == null) return;
        
        Vector3 pos;
        Quaternion rot;
        collider.GetWorldPose(out pos, out rot);
        wheelTransform.position = pos;
        wheelTransform.rotation = rot;
    }
    
    private void ApplyDownForce()
    {
        _rigidbody.AddForce(-transform.up * _downForce * _rigidbody.velocity.magnitude);
    }
    
    private void UpdateAudio()
    {
        if (_engineAudioSource == null) return;
        
        float speedRatio = Mathf.Clamp01(_currentSpeed / 100f);
        _engineAudioSource.pitch = Mathf.Lerp(_minPitch, _maxPitch, speedRatio);
        
        if (Mathf.Abs(_verticalInput) > 0.1f && _engineRevClip != null)
        {
            if (_engineAudioSource.clip != _engineRevClip)
            {
                _engineAudioSource.clip = _engineRevClip;
                _engineAudioSource.Play();
            }
        }
        else if (_engineIdleClip != null)
        {
            if (_engineAudioSource.clip != _engineIdleClip)
            {
                _engineAudioSource.clip = _engineIdleClip;
                _engineAudioSource.Play();
            }
        }
    }
    
    private void UpdateEffects()
    {
        bool isSkidding = _isBraking && _currentSpeed > 10f;
        
        if (_leftWheelSmoke != null)
        {
            if (isSkidding && !_leftWheelSmoke.isPlaying)
                _leftWheelSmoke.Play();
            else if (!isSkidding && _leftWheelSmoke.isPlaying)
                _leftWheelSmoke.Stop();
        }
        
        if (_rightWheelSmoke != null)
        {
            if (isSkidding && !_rightWheelSmoke.isPlaying)
                _rightWheelSmoke.Play();
            else if (!isSkidding && _rightWheelSmoke.isPlaying)
                _rightWheelSmoke.Stop();
        }
        
        if (_leftSkidTrail != null)
            _leftSkidTrail.emitting = isSkidding;
            
        if (_rightSkidTrail != null)
            _rightSkidTrail.emitting = isSkidding;
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public bool IsGrounded()
    {
        return _frontLeftWheelCollider.isGrounded || _frontRightWheelCollider.isGrounded ||
               _rearLeftWheelCollider.isGrounded || _rearRightWheelCollider.isGrounded;
    }
}

public class RaceCheckpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private int _checkpointIndex;
    [SerializeField] private bool _isFinishLine;
    [SerializeField] private Material _activeMaterial;
    [SerializeField] private Material _inactiveMaterial;
    
    private Renderer _renderer;
    private bool _isActive = true;
    
    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        UpdateVisual();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        
        GoKartController kart = other.GetComponent<GoKartController>();
        if (kart != null)
        {
            RaceManager raceManager = FindObjectOfType<RaceManager>();
            if (raceManager != null)
            {
                raceManager.CheckpointReached(_checkpointIndex, _isFinishLine, kart);
            }
            
            _isActive = false;
            UpdateVisual();
        }
    }
    
    public void ResetCheckpoint()
    {
        _isActive = true;
        UpdateVisual();
    }
    
    private void UpdateVisual()
    {
        if (_renderer != null)
        {
            _renderer.material = _isActive ? _activeMaterial : _inactiveMaterial;
        }
    }
}

public class RaceManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int _totalLaps = 3;
    [SerializeField] private int _totalCheckpoints = 5;
    [SerializeField] private Text _lapText;
    [SerializeField] private Text _timeText;
    [SerializeField] private Text _speedText;
    [SerializeField] private GameObject _raceCompletePanel;
    
    private int _currentLap = 1;
    private int _currentCheckpoint = 0;
    private float _raceTime = 0f;
    private bool _raceActive = true;
    private GoKartController _playerKart;
    private RaceCheckpoint[] _checkpoints;
    
    private void Start()
    {
        _playerKart = FindObjectOfType<GoKartController>();
        _checkpoints = FindObjectsOfType<RaceCheckpoint>();
        
        if (_raceCompletePanel != null)
            _raceCompletePanel.SetActive(false);
    }
    
    private void Update()
    {
        if (_raceActive)
        {
            _raceTime += Time.deltaTime;
            UpdateUI();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartRace();
        }
    }
    
    public void CheckpointReached(int checkpointIndex, bool isFinishLine, GoKartController kart)
    {
        if (!_raceActive || kart != _playerKart) return;
        
        if (checkpointIndex == _currentCheckpoint)
        {
            _currentCheckpoint++;
            
            if (isFinishLine)
            {
                if (_currentLap >= _totalLaps)
                {
                    CompleteRace();
                }
                else
                {
                    _currentLap++;
                    _currentCheckpoint = 0;
                    ResetCheckpoints();
                }
            }
        }
    }
    
    private void CompleteRace()
    {
        _raceActive = false;
        if (_raceCompletePanel != null)
        {
            _raceCompletePanel.SetActive(true);
        }
        
        Debug.Log($"Race Complete! Time: {FormatTime(_raceTime)}");
    }
    
    private void RestartRace()
    {
        _currentLap = 1;
        _currentCheckpoint = 0;
        _raceTime = 0f;
        _raceActive = true;
        
        if (_raceCompletePanel != null)
            _raceCompletePanel.SetActive(false);
            
        ResetCheckpoints();
    }
    
    private void ResetCheckpoints()
    {
        foreach (RaceCheckpoint checkpoint in _checkpoints)
        {
            if (checkpoint != null)
                checkpoint.ResetCheckpoint();
        }
    }
    
    private void UpdateUI()
    {
        if (_lapText != null)
            _lapText.text = $"Lap: {_currentLap}/{_totalLaps}";
            
        if (_timeText != null)
            _timeText.text = $"Time: {FormatTime(_raceTime)}";
            
        if (_speedText != null && _playerKart != null)
            _speedText.text = $"Speed: {_playerKart.GetCurrentSpeed():F0} km/h";
    }
    
    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time * 100f) % 100f);
        return $"{minutes:00}:{seconds:00}:{milliseconds:00}";
    }
}