// Prompt: bat that hangs from ceiling
// Type: general

using UnityEngine;
using System.Collections;

public class HangingBat : MonoBehaviour
{
    [Header("Hanging Behavior")]
    [SerializeField] private float _hangHeight = 2f;
    [SerializeField] private float _swayAmount = 0.5f;
    [SerializeField] private float _swaySpeed = 1f;
    [SerializeField] private bool _startHanging = true;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    [SerializeField] private string _playerTag = "Player";
    
    [Header("Flight Behavior")]
    [SerializeField] private float _flightSpeed = 8f;
    [SerializeField] private float _flightHeight = 3f;
    [SerializeField] private float _flightDuration = 5f;
    [SerializeField] private float _returnDelay = 3f;
    [SerializeField] private AnimationCurve _flightCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Animation")]
    [SerializeField] private float _wingFlapSpeed = 10f;
    [SerializeField] private float _hangingRotationSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _flutterSound;
    [SerializeField] private AudioClip _squeakSound;
    [SerializeField] private float _audioVolume = 0.5f;
    
    private Vector3 _originalPosition;
    private Vector3 _hangingPosition;
    private bool _isHanging = true;
    private bool _isFlying = false;
    private bool _isReturning = false;
    private float _swayTimer = 0f;
    private float _flightTimer = 0f;
    private Vector3 _flightStartPosition;
    private Vector3 _flightTargetPosition;
    private AudioSource _audioSource;
    private Animator _animator;
    private Rigidbody _rigidbody;
    private Collider _collider;
    
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_rigidbody != null)
        {
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }
    }
    
    private void Start()
    {
        _originalPosition = transform.position;
        _hangingPosition = _originalPosition;
        
        if (_startHanging)
        {
            SetHangingState(true);
        }
        
        _audioSource.volume = _audioVolume;
        _swayTimer = Random.Range(0f, Mathf.PI * 2f);
    }
    
    private void Update()
    {
        if (_isHanging)
        {
            UpdateHangingBehavior();
            CheckForPlayer();
        }
        else if (_isFlying)
        {
            UpdateFlightBehavior();
        }
        else if (_isReturning)
        {
            UpdateReturnBehavior();
        }
        
        UpdateAnimations();
    }
    
    private void UpdateHangingBehavior()
    {
        _swayTimer += Time.deltaTime * _swaySpeed;
        
        Vector3 swayOffset = new Vector3(
            Mathf.Sin(_swayTimer) * _swayAmount,
            0f,
            Mathf.Cos(_swayTimer * 0.7f) * _swayAmount * 0.5f
        );
        
        transform.position = _hangingPosition + swayOffset;
        
        float rotationZ = Mathf.Sin(_swayTimer) * 15f;
        transform.rotation = Quaternion.Euler(180f, 0f, rotationZ);
    }
    
    private void CheckForPlayer()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, _detectionRadius, _playerLayer);
        
        foreach (Collider col in nearbyObjects)
        {
            if (col.CompareTag(_playerTag))
            {
                StartFlight();
                break;
            }
        }
    }
    
    private void StartFlight()
    {
        if (_isFlying || _isReturning) return;
        
        _isHanging = false;
        _isFlying = true;
        _flightTimer = 0f;
        
        _flightStartPosition = transform.position;
        _flightTargetPosition = _originalPosition + Vector3.up * _flightHeight + 
                               new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            _rigidbody.useGravity = false;
        }
        
        PlaySound(_flutterSound);
        
        if (Random.value < 0.3f)
        {
            PlaySound(_squeakSound);
        }
    }
    
    private void UpdateFlightBehavior()
    {
        _flightTimer += Time.deltaTime;
        float progress = _flightTimer / _flightDuration;
        
        if (progress >= 1f)
        {
            StartCoroutine(ReturnToHanging());
            return;
        }
        
        float curveValue = _flightCurve.Evaluate(progress);
        Vector3 currentTarget = Vector3.Lerp(_flightStartPosition, _flightTargetPosition, curveValue);
        
        Vector3 flutterOffset = new Vector3(
            Mathf.Sin(Time.time * _wingFlapSpeed) * 0.2f,
            Mathf.Cos(Time.time * _wingFlapSpeed * 1.3f) * 0.1f,
            Mathf.Sin(Time.time * _wingFlapSpeed * 0.8f) * 0.15f
        );
        
        transform.position = currentTarget + flutterOffset;
        
        Vector3 direction = (_flightTargetPosition - _flightStartPosition).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 3f);
        }
    }
    
    private IEnumerator ReturnToHanging()
    {
        _isFlying = false;
        _isReturning = true;
        
        yield return new WaitForSeconds(_returnDelay);
        
        float returnDuration = 2f;
        float returnTimer = 0f;
        Vector3 returnStartPosition = transform.position;
        
        while (returnTimer < returnDuration)
        {
            returnTimer += Time.deltaTime;
            float progress = returnTimer / returnDuration;
            
            transform.position = Vector3.Lerp(returnStartPosition, _hangingPosition, progress);
            
            Quaternion targetRotation = Quaternion.Euler(180f, 0f, 0f);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, progress);
            
            yield return null;
        }
        
        SetHangingState(true);
    }
    
    private void UpdateReturnBehavior()
    {
        // Handled in coroutine
    }
    
    private void SetHangingState(bool hanging)
    {
        _isHanging = hanging;
        _isFlying = false;
        _isReturning = false;
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = hanging;
            _rigidbody.useGravity = false;
        }
        
        if (hanging)
        {
            transform.position = _hangingPosition;
            transform.rotation = Quaternion.Euler(180f, 0f, 0f);
        }
    }
    
    private void UpdateAnimations()
    {
        if (_animator != null)
        {
            _animator.SetBool("IsHanging", _isHanging);
            _animator.SetBool("IsFlying", _isFlying);
            _animator.SetFloat("WingFlapSpeed", _isFlying ? _wingFlapSpeed : 0f);
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.red;
        Vector3 hangPos = Application.isPlaying ? _hangingPosition : transform.position;
        Gizmos.DrawLine(hangPos, hangPos + Vector3.up * _hangHeight);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(hangPos + Vector3.up * _flightHeight, Vector3.one * 0.5f);
    }
    
    public void ForceStartFlight()
    {
        if (_isHanging)
        {
            StartFlight();
        }
    }
    
    public void ForceReturnToHanging()
    {
        StopAllCoroutines();
        SetHangingState(true);
    }
}