// Prompt: sprint with stamina cost
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SprintSystem : MonoBehaviour
{
    [Header("Sprint Settings")]
    [SerializeField] private float _normalSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;
    [SerializeField] private KeyCode _sprintKey = KeyCode.LeftShift;
    
    [Header("Stamina Settings")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _staminaDrainRate = 20f;
    [SerializeField] private float _staminaRegenRate = 15f;
    [SerializeField] private float _regenDelay = 1f;
    [SerializeField] private float _minStaminaToSprint = 10f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _sprintSound;
    [SerializeField] private AudioClip _exhaustedSound;
    
    [Header("Events")]
    public UnityEvent<float, float> OnStaminaChanged;
    public UnityEvent OnSprintStarted;
    public UnityEvent OnSprintStopped;
    public UnityEvent OnExhausted;
    
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private float _currentStamina;
    private bool _isSprinting;
    private bool _canSprint = true;
    private float _lastSprintTime;
    private Vector3 _moveDirection;
    private bool _isMoving;
    
    private void Start()
    {
        _currentStamina = _maxStamina;
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleInput();
        HandleSprinting();
        HandleStamina();
        HandleMovement();
    }
    
    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        _isMoving = _moveDirection.magnitude > 0.1f;
        
        bool sprintInput = Input.GetKey(_sprintKey);
        bool shouldSprint = sprintInput && _isMoving && _canSprint && _currentStamina >= _minStaminaToSprint;
        
        if (shouldSprint && !_isSprinting)
        {
            StartSprint();
        }
        else if ((!sprintInput || !_isMoving || _currentStamina <= 0f) && _isSprinting)
        {
            StopSprint();
        }
    }
    
    private void HandleSprinting()
    {
        if (_isSprinting && _currentStamina > 0f)
        {
            _currentStamina -= _staminaDrainRate * Time.deltaTime;
            _lastSprintTime = Time.time;
            
            if (_currentStamina <= 0f)
            {
                _currentStamina = 0f;
                StopSprint();
                OnExhausted?.Invoke();
                
                if (_audioSource && _exhaustedSound)
                    _audioSource.PlayOneShot(_exhaustedSound);
            }
        }
    }
    
    private void HandleStamina()
    {
        if (!_isSprinting && _currentStamina < _maxStamina)
        {
            if (Time.time - _lastSprintTime >= _regenDelay)
            {
                _currentStamina += _staminaRegenRate * Time.deltaTime;
                _currentStamina = Mathf.Clamp(_currentStamina, 0f, _maxStamina);
            }
        }
        
        _canSprint = _currentStamina >= _minStaminaToSprint;
        OnStaminaChanged?.Invoke(_currentStamina, _maxStamina);
    }
    
    private void HandleMovement()
    {
        if (!_isMoving) return;
        
        float currentSpeed = _isSprinting ? _sprintSpeed : _normalSpeed;
        Vector3 movement = transform.TransformDirection(_moveDirection) * currentSpeed;
        
        if (_characterController != null)
        {
            movement.y = -9.81f;
            _characterController.Move(movement * Time.deltaTime);
        }
        else if (_rigidbody != null)
        {
            Vector3 velocity = new Vector3(movement.x, _rigidbody.velocity.y, movement.z);
            _rigidbody.velocity = velocity;
        }
        else
        {
            transform.Translate(movement * Time.deltaTime, Space.World);
        }
    }
    
    private void StartSprint()
    {
        _isSprinting = true;
        OnSprintStarted?.Invoke();
        
        if (_audioSource && _sprintSound)
        {
            _audioSource.clip = _sprintSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void StopSprint()
    {
        _isSprinting = false;
        OnSprintStopped?.Invoke();
        
        if (_audioSource && _audioSource.isPlaying && _audioSource.clip == _sprintSound)
        {
            _audioSource.Stop();
        }
    }
    
    public float GetStaminaPercentage()
    {
        return _currentStamina / _maxStamina;
    }
    
    public bool IsSprinting()
    {
        return _isSprinting;
    }
    
    public bool CanSprint()
    {
        return _canSprint;
    }
    
    public void RestoreStamina(float amount)
    {
        _currentStamina = Mathf.Clamp(_currentStamina + amount, 0f, _maxStamina);
    }
    
    public void DrainStamina(float amount)
    {
        _currentStamina = Mathf.Clamp(_currentStamina - amount, 0f, _maxStamina);
        
        if (_currentStamina <= 0f && _isSprinting)
        {
            StopSprint();
        }
    }
}