// Prompt: object that floats upward when player is near (within 5 units) but falls faster when player is far away, plays a humming sound that gets higher pitched as it rises and lower as it falls, rotates opposite to its vertical movement direction
// Type: movement

using UnityEngine;

public class FloatingObject : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _fallSpeed = 4f;
    [SerializeField] private float _rotationSpeed = 45f;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private float _basePitch = 1f;
    [SerializeField] private float _pitchRange = 0.5f;
    [SerializeField] private float _pitchSmoothSpeed = 2f;
    
    [Header("References")]
    [SerializeField] private Transform _playerTransform;
    
    private Vector3 _startPosition;
    private float _currentVerticalVelocity;
    private float _targetPitch;
    private bool _isPlayerNear;
    
    private void Start()
    {
        _startPosition = transform.position;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.volume = 0.5f;
        }
        
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }
        
        _audioSource.pitch = _basePitch;
        if (!_audioSource.isPlaying)
            _audioSource.Play();
    }
    
    private void Update()
    {
        CheckPlayerDistance();
        HandleMovement();
        HandleRotation();
        HandleAudio();
    }
    
    private void CheckPlayerDistance()
    {
        if (_playerTransform == null)
            return;
            
        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        _isPlayerNear = distance <= _detectionRange;
    }
    
    private void HandleMovement()
    {
        if (_isPlayerNear)
        {
            _currentVerticalVelocity = _floatSpeed;
        }
        else
        {
            _currentVerticalVelocity = -_fallSpeed;
        }
        
        Vector3 movement = Vector3.up * _currentVerticalVelocity * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }
    
    private void HandleRotation()
    {
        float rotationDirection = _currentVerticalVelocity > 0 ? -1f : 1f;
        float rotationAmount = rotationDirection * _rotationSpeed * Time.deltaTime;
        transform.Rotate(0, 0, rotationAmount);
    }
    
    private void HandleAudio()
    {
        if (_audioSource == null)
            return;
            
        if (_currentVerticalVelocity > 0)
        {
            _targetPitch = _basePitch + _pitchRange;
        }
        else
        {
            _targetPitch = _basePitch - _pitchRange;
        }
        
        _audioSource.pitch = Mathf.Lerp(_audioSource.pitch, _targetPitch, _pitchSmoothSpeed * Time.deltaTime);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
    }
}