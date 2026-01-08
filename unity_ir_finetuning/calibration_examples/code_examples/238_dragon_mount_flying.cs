// Prompt: dragon mount flying
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class DragonMount : MonoBehaviour
{
    [Header("Flight Settings")]
    [SerializeField] private float _flySpeed = 15f;
    [SerializeField] private float _acceleration = 5f;
    [SerializeField] private float _deceleration = 8f;
    [SerializeField] private float _maxAltitude = 100f;
    [SerializeField] private float _minAltitude = 2f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float _pitchSpeed = 60f;
    [SerializeField] private float _yawSpeed = 90f;
    [SerializeField] private float _rollSpeed = 45f;
    [SerializeField] private float _maxPitchAngle = 45f;
    [SerializeField] private float _maxRollAngle = 30f;
    
    [Header("Wing Animation")]
    [SerializeField] private Transform _leftWing;
    [SerializeField] private Transform _rightWing;
    [SerializeField] private float _wingFlapSpeed = 3f;
    [SerializeField] private float _wingFlapAngle = 20f;
    
    [Header("Rider Settings")]
    [SerializeField] private Transform _riderSeat;
    [SerializeField] private float _mountRange = 3f;
    [SerializeField] private KeyCode _mountKey = KeyCode.E;
    [SerializeField] private KeyCode _dismountKey = KeyCode.Q;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _wingFlapSound;
    [SerializeField] private AudioClip _roarSound;
    
    [Header("Events")]
    public UnityEvent OnPlayerMounted;
    public UnityEvent OnPlayerDismounted;
    public UnityEvent OnTakeOff;
    public UnityEvent OnLanding;
    
    private Rigidbody _rigidbody;
    private Transform _rider;
    private Vector3 _currentVelocity;
    private float _currentSpeed;
    private float _wingFlapTimer;
    private bool _isGrounded = true;
    private bool _hasRider = false;
    private Vector3 _originalRiderPosition;
    private Quaternion _originalRiderRotation;
    private CharacterController _riderController;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _rigidbody.drag = 2f;
        _rigidbody.angularDrag = 5f;
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.spatialBlend = 1f;
        _audioSource.volume = 0.7f;
    }
    
    private void Update()
    {
        CheckForRiderInput();
        
        if (_hasRider)
        {
            HandleFlightInput();
            AnimateWings();
            PlayFlightSounds();
        }
        else
        {
            HandleIdleBehavior();
        }
        
        CheckGroundStatus();
    }
    
    private void FixedUpdate()
    {
        if (_hasRider)
        {
            ApplyFlightPhysics();
        }
        else
        {
            ApplyIdlePhysics();
        }
    }
    
    private void CheckForRiderInput()
    {
        if (!_hasRider)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && Vector3.Distance(transform.position, player.transform.position) <= _mountRange)
            {
                if (Input.GetKeyDown(_mountKey))
                {
                    MountPlayer(player.transform);
                }
            }
        }
        else
        {
            if (Input.GetKeyDown(_dismountKey))
            {
                DismountPlayer();
            }
        }
    }
    
    private void MountPlayer(Transform player)
    {
        _rider = player;
        _hasRider = true;
        
        _originalRiderPosition = player.position;
        _originalRiderRotation = player.rotation;
        
        _riderController = player.GetComponent<CharacterController>();
        if (_riderController != null)
        {
            _riderController.enabled = false;
        }
        
        Rigidbody riderRb = player.GetComponent<Rigidbody>();
        if (riderRb != null)
        {
            riderRb.isKinematic = true;
        }
        
        player.SetParent(_riderSeat);
        player.localPosition = Vector3.zero;
        player.localRotation = Quaternion.identity;
        
        OnPlayerMounted?.Invoke();
        
        if (_roarSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_roarSound);
        }
    }
    
    private void DismountPlayer()
    {
        if (_rider == null) return;
        
        _rider.SetParent(null);
        
        Vector3 dismountPosition = transform.position + transform.right * 3f;
        RaycastHit hit;
        if (Physics.Raycast(dismountPosition + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            dismountPosition = hit.point + Vector3.up * 0.5f;
        }
        
        _rider.position = dismountPosition;
        _rider.rotation = _originalRiderRotation;
        
        if (_riderController != null)
        {
            _riderController.enabled = true;
        }
        
        Rigidbody riderRb = _rider.GetComponent<Rigidbody>();
        if (riderRb != null)
        {
            riderRb.isKinematic = false;
        }
        
        _hasRider = false;
        _rider = null;
        
        OnPlayerDismounted?.Invoke();
    }
    
    private void HandleFlightInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        float ascend = Input.GetKey(KeyCode.Space) ? 1f : 0f;
        float descend = Input.GetKey(KeyCode.LeftShift) ? 1f : 0f;
        
        Vector3 inputDirection = new Vector3(horizontal, ascend - descend, vertical);
        
        if (inputDirection.magnitude > 0.1f)
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, _flySpeed, _acceleration * Time.deltaTime);
            
            if (_isGrounded && (ascend > 0 || vertical > 0))
            {
                _isGrounded = false;
                OnTakeOff?.Invoke();
            }
        }
        else
        {
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, 0f, _deceleration * Time.deltaTime);
        }
        
        if (inputDirection.magnitude > 0.1f)
        {
            float targetYaw = horizontal * _yawSpeed * Time.deltaTime;
            float targetPitch = -vertical * _pitchSpeed * Time.deltaTime;
            float targetRoll = -horizontal * _rollSpeed * Time.deltaTime;
            
            targetPitch = Mathf.Clamp(targetPitch, -_maxPitchAngle, _maxPitchAngle);
            targetRoll = Mathf.Clamp(targetRoll, -_maxRollAngle, _maxRollAngle);
            
            transform.Rotate(targetPitch, targetYaw, 0f, Space.Self);
            
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.z = Mathf.LerpAngle(currentEuler.z, targetRoll, Time.deltaTime * 2f);
            transform.eulerAngles = currentEuler;
        }
        else
        {
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.z = Mathf.LerpAngle(currentEuler.z, 0f, Time.deltaTime * 2f);
            transform.eulerAngles = currentEuler;
        }
    }
    
    private void ApplyFlightPhysics()
    {
        Vector3 forwardMovement = transform.forward * _currentSpeed;
        
        float currentAltitude = transform.position.y;
        if (currentAltitude > _maxAltitude)
        {
            forwardMovement.y = Mathf.Min(forwardMovement.y, 0f);
        }
        else if (currentAltitude < _minAltitude)
        {
            forwardMovement.y = Mathf.Max(forwardMovement.y, 2f);
        }
        
        _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, forwardMovement, Time.fixedDeltaTime * 3f);
    }
    
    private void ApplyIdlePhysics()
    {
        if (!_isGrounded)
        {
            _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, Vector3.down * 2f, Time.fixedDeltaTime);
        }
        else
        {
            _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, Vector3.zero, Time.fixedDeltaTime * 5f);
        }
    }
    
    private void HandleIdleBehavior()
    {
        transform.rotation = Quaternion.Lerp(transform.rotation, 
            Quaternion.LookRotation(transform.forward, Vector3.up), Time.deltaTime);
    }
    
    private void AnimateWings()
    {
        if (_leftWing == null || _rightWing == null) return;
        
        _wingFlapTimer += Time.deltaTime * _wingFlapSpeed;
        float flapAngle = Mathf.Sin(_wingFlapTimer) * _wingFlapAngle;
        
        _leftWing.localRotation = Quaternion.Euler(0f, 0f, flapAngle);
        _rightWing.localRotation = Quaternion.Euler(0f, 0f, -flapAngle);
    }
    
    private void PlayFlightSounds()
    {
        if (_wingFlapSound != null && _audioSource != null && _currentSpeed > 0.5f)
        {
            if (!_audioSource.isPlaying)
            {
                _audioSource.clip = _wingFlapSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            
            _audioSource.pitch = Mathf.Lerp(0.8f, 1.5f, _currentSpeed / _flySpeed);
        }
        else if (_audioSource != null && _audioSource.isPlaying && _audioSource.clip == _wingFlapSound)
        {
            _audioSource.Stop();
        }
    }
    
    private void CheckGroundStatus()
    {
        RaycastHit hit;
        bool wasGrounded = _isGrounded;
        _isGrounded = Physics.Raycast(transform.position, Vector3.down, out hit, 2f);
        
        if (!wasGrounded && _isGrounded)
        {
            OnLanding?.Invoke();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _mountRange);
        
        if (_riderSeat != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_riderSeat.position, Vector3.one * 0.5f);
        }
        
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, Vector3.down * 2f);
    }
}