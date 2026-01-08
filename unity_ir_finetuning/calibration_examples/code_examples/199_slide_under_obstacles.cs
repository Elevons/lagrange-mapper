// Prompt: slide under obstacles
// Type: general

using UnityEngine;

public class SlideController : MonoBehaviour
{
    [Header("Slide Settings")]
    [SerializeField] private float _slideDuration = 1f;
    [SerializeField] private float _slideSpeed = 8f;
    [SerializeField] private float _slideHeight = 0.5f;
    [SerializeField] private KeyCode _slideKey = KeyCode.S;
    [SerializeField] private KeyCode _slideKeyAlt = KeyCode.LeftControl;
    
    [Header("Physics")]
    [SerializeField] private float _normalHeight = 2f;
    [SerializeField] private LayerMask _obstacleLayer = -1;
    [SerializeField] private float _groundCheckDistance = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _slideSound;
    [SerializeField] private AudioClip _slideEndSound;
    
    private Rigidbody _rigidbody;
    private CapsuleCollider _capsuleCollider;
    private AudioSource _audioSource;
    private Animator _animator;
    
    private bool _isSliding = false;
    private bool _isGrounded = false;
    private float _slideTimer = 0f;
    private float _originalHeight;
    private Vector3 _originalCenter;
    private Vector3 _slideDirection;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _capsuleCollider = GetComponent<CapsuleCollider>();
        _audioSource = GetComponent<AudioSource>();
        _animator = GetComponent<Animator>();
        
        if (_capsuleCollider != null)
        {
            _originalHeight = _capsuleCollider.height;
            _originalCenter = _capsuleCollider.center;
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
    }
    
    private void Update()
    {
        CheckGrounded();
        HandleSlideInput();
        UpdateSlide();
    }
    
    private void CheckGrounded()
    {
        if (Physics.Raycast(transform.position, Vector3.down, _groundCheckDistance + 0.1f, _obstacleLayer))
        {
            _isGrounded = true;
        }
        else
        {
            _isGrounded = false;
        }
    }
    
    private void HandleSlideInput()
    {
        if (!_isSliding && _isGrounded && (Input.GetKeyDown(_slideKey) || Input.GetKeyDown(_slideKeyAlt)))
        {
            StartSlide();
        }
        
        if (_isSliding && (Input.GetKeyUp(_slideKey) && Input.GetKeyUp(_slideKeyAlt)))
        {
            EndSlide();
        }
    }
    
    private void UpdateSlide()
    {
        if (_isSliding)
        {
            _slideTimer += Time.deltaTime;
            
            if (_slideTimer >= _slideDuration)
            {
                EndSlide();
            }
            else
            {
                ApplySlideMovement();
            }
        }
    }
    
    private void StartSlide()
    {
        _isSliding = true;
        _slideTimer = 0f;
        _slideDirection = transform.forward;
        
        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _slideHeight;
            _capsuleCollider.center = new Vector3(_originalCenter.x, _slideHeight * 0.5f, _originalCenter.z);
        }
        
        if (_animator != null)
        {
            _animator.SetBool("IsSliding", true);
        }
        
        if (_audioSource != null && _slideSound != null)
        {
            _audioSource.PlayOneShot(_slideSound);
        }
    }
    
    private void EndSlide()
    {
        if (!CanStandUp())
        {
            return;
        }
        
        _isSliding = false;
        _slideTimer = 0f;
        
        if (_capsuleCollider != null)
        {
            _capsuleCollider.height = _originalHeight;
            _capsuleCollider.center = _originalCenter;
        }
        
        if (_animator != null)
        {
            _animator.SetBool("IsSliding", false);
        }
        
        if (_audioSource != null && _slideEndSound != null)
        {
            _audioSource.PlayOneShot(_slideEndSound);
        }
    }
    
    private bool CanStandUp()
    {
        Vector3 checkPosition = transform.position + Vector3.up * (_normalHeight - _slideHeight);
        return !Physics.CheckSphere(checkPosition, 0.4f, _obstacleLayer);
    }
    
    private void ApplySlideMovement()
    {
        if (_rigidbody != null && _isGrounded)
        {
            Vector3 slideVelocity = _slideDirection * _slideSpeed;
            slideVelocity.y = _rigidbody.velocity.y;
            _rigidbody.velocity = slideVelocity;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Obstacle") && _isSliding)
        {
            Debug.Log("Successfully slid under obstacle!");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.down * _groundCheckDistance, 0.1f);
        
        if (_isSliding)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, _slideHeight, 1f));
        }
        else
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, new Vector3(1f, _normalHeight, 1f));
        }
    }
}