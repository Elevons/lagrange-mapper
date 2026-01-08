// Prompt: surfboard on waves
// Type: general

using UnityEngine;
using System.Collections;

public class SurfboardController : MonoBehaviour
{
    [Header("Surfboard Physics")]
    [SerializeField] private float _maxSpeed = 15f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _turnSpeed = 120f;
    [SerializeField] private float _stabilityForce = 10f;
    [SerializeField] private float _buoyancyForce = 20f;
    
    [Header("Wave Interaction")]
    [SerializeField] private float _waveHeight = 2f;
    [SerializeField] private float _waveSpeed = 1f;
    [SerializeField] private float _waveLength = 10f;
    [SerializeField] private LayerMask _waterLayer = 1;
    
    [Header("Tilt and Balance")]
    [SerializeField] private float _maxTiltAngle = 30f;
    [SerializeField] private float _tiltSpeed = 2f;
    [SerializeField] private float _balanceRecoverySpeed = 3f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _splashSound;
    [SerializeField] private AudioClip _ridingSound;
    
    private Rigidbody _rigidbody;
    private Transform _playerTransform;
    private Vector3 _initialPosition;
    private float _currentSpeed;
    private float _currentTilt;
    private bool _isOnWave;
    private bool _isPlayerOnBoard;
    private float _waveOffset;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.drag = 2f;
        _rigidbody.angularDrag = 5f;
        _initialPosition = transform.position;
        _waveOffset = Random.Range(0f, Mathf.PI * 2f);
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.spatialBlend = 1f;
        _audioSource.volume = 0.5f;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateWavePosition();
        HandleTilt();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        ApplyBuoyancy();
        ApplyMovement();
        ApplyStability();
    }
    
    private void HandleInput()
    {
        if (!_isPlayerOnBoard) return;
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        // Apply turning
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            transform.Rotate(0, horizontal * _turnSpeed * Time.deltaTime, 0);
            _currentTilt = Mathf.Lerp(_currentTilt, horizontal * _maxTiltAngle, _tiltSpeed * Time.deltaTime);
        }
        else
        {
            _currentTilt = Mathf.Lerp(_currentTilt, 0f, _balanceRecoverySpeed * Time.deltaTime);
        }
        
        // Apply forward movement
        if (vertical > 0.1f)
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, _maxSpeed * vertical, _acceleration * Time.deltaTime);
        }
        else
        {
            _currentSpeed = Mathf.Lerp(_currentSpeed, 0f, _acceleration * 0.5f * Time.deltaTime);
        }
    }
    
    private void UpdateWavePosition()
    {
        float waveY = Mathf.Sin((transform.position.x / _waveLength + Time.time * _waveSpeed + _waveOffset)) * _waveHeight;
        float targetY = _initialPosition.y + waveY;
        
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 5f, Vector3.down, out hit, 10f, _waterLayer))
        {
            targetY = hit.point.y + 0.5f;
            _isOnWave = true;
        }
        else
        {
            _isOnWave = false;
        }
        
        Vector3 targetPosition = new Vector3(transform.position.x, targetY, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * 2f);
    }
    
    private void ApplyBuoyancy()
    {
        if (_isOnWave)
        {
            Vector3 buoyancy = Vector3.up * _buoyancyForce;
            _rigidbody.AddForce(buoyancy, ForceMode.Force);
        }
    }
    
    private void ApplyMovement()
    {
        if (_isPlayerOnBoard && _currentSpeed > 0.1f)
        {
            Vector3 forward = transform.forward * _currentSpeed;
            _rigidbody.AddForce(forward, ForceMode.Force);
            
            // Limit max velocity
            if (_rigidbody.velocity.magnitude > _maxSpeed)
            {
                _rigidbody.velocity = _rigidbody.velocity.normalized * _maxSpeed;
            }
        }
    }
    
    private void ApplyStability()
    {
        Vector3 stabilityTorque = Vector3.zero;
        
        // Roll stability
        float rollAngle = Vector3.Angle(Vector3.up, transform.up);
        if (rollAngle > 5f)
        {
            Vector3 rollCorrection = Vector3.Cross(transform.up, Vector3.up);
            stabilityTorque += rollCorrection * _stabilityForce;
        }
        
        // Pitch stability
        Vector3 forwardProjected = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        float pitchAngle = Vector3.Angle(transform.forward, forwardProjected);
        if (pitchAngle > 10f)
        {
            Vector3 pitchCorrection = Vector3.Cross(transform.forward, forwardProjected);
            stabilityTorque += pitchCorrection * _stabilityForce * 0.5f;
        }
        
        _rigidbody.AddTorque(stabilityTorque, ForceMode.Force);
    }
    
    private void HandleTilt()
    {
        Vector3 currentRotation = transform.eulerAngles;
        currentRotation.z = _currentTilt;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(currentRotation), Time.deltaTime * _tiltSpeed);
    }
    
    private void UpdateAudio()
    {
        if (_isPlayerOnBoard && _currentSpeed > 1f && _ridingSound != null)
        {
            if (!_audioSource.isPlaying || _audioSource.clip != _ridingSound)
            {
                _audioSource.clip = _ridingSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            
            _audioSource.pitch = Mathf.Lerp(0.8f, 1.5f, _currentSpeed / _maxSpeed);
        }
        else if (_audioSource.isPlaying && _audioSource.clip == _ridingSound)
        {
            _audioSource.Stop();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerOnBoard = true;
            _playerTransform = other.transform;
            
            // Position player on surfboard
            other.transform.SetParent(transform);
            other.transform.localPosition = Vector3.up * 0.5f;
            
            if (_splashSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_splashSound);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerOnBoard = false;
            
            if (_playerTransform != null)
            {
                _playerTransform.SetParent(null);
                _playerTransform = null;
            }
            
            _currentSpeed = 0f;
            _currentTilt = 0f;
            
            if (_audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, 2f);
        
        Gizmos.color = Color.cyan;
        for (int i = 0; i < 20; i++)
        {
            float x = transform.position.x + (i - 10) * 2f;
            float waveY = Mathf.Sin((x / _waveLength + Time.time * _waveSpeed + _waveOffset)) * _waveHeight;
            Vector3 wavePoint = new Vector3(x, _initialPosition.y + waveY, transform.position.z);
            Gizmos.DrawWireSphere(wavePoint, 0.2f);
        }
    }
}