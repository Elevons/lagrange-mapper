// Prompt: player ready state
// Type: movement

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerReadyState : MonoBehaviour
{
    [Header("Ready State Settings")]
    [SerializeField] private bool _isReady = false;
    [SerializeField] private float _readyCheckInterval = 0.1f;
    [SerializeField] private bool _requireGrounded = true;
    [SerializeField] private bool _requireMinHealth = false;
    [SerializeField] private float _minHealthThreshold = 50f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode _readyKey = KeyCode.R;
    [SerializeField] private string _readyButton = "Fire1";
    [SerializeField] private bool _useKeyInput = true;
    [SerializeField] private bool _useButtonInput = false;
    
    [Header("Ground Check")]
    [SerializeField] private Transform _groundCheckPoint;
    [SerializeField] private float _groundCheckRadius = 0.2f;
    [SerializeField] private LayerMask _groundLayerMask = 1;
    
    [Header("Health Check")]
    [SerializeField] private float _currentHealth = 100f;
    [SerializeField] private float _maxHealth = 100f;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _readyIndicator;
    [SerializeField] private Color _readyColor = Color.green;
    [SerializeField] private Color _notReadyColor = Color.red;
    [SerializeField] private Renderer _playerRenderer;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _readySound;
    [SerializeField] private AudioClip _notReadySound;
    
    [Header("Events")]
    public UnityEvent OnBecomeReady;
    public UnityEvent OnBecomeNotReady;
    public UnityEvent OnReadyStateChanged;
    
    private bool _wasReady = false;
    private Rigidbody _rigidbody;
    private CharacterController _characterController;
    private Material _originalMaterial;
    private Color _originalColor;
    
    public bool IsReady => _isReady;
    public float CurrentHealth => _currentHealth;
    public float HealthPercentage => _currentHealth / _maxHealth;
    
    private void Start()
    {
        InitializeComponents();
        InitializeVisuals();
        StartCoroutine(ReadyStateCheckCoroutine());
    }
    
    private void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _characterController = GetComponent<CharacterController>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_groundCheckPoint == null)
            _groundCheckPoint = transform;
    }
    
    private void InitializeVisuals()
    {
        if (_playerRenderer != null)
        {
            _originalMaterial = _playerRenderer.material;
            _originalColor = _originalMaterial.color;
        }
        
        UpdateVisualFeedback();
    }
    
    private void Update()
    {
        HandleInput();
    }
    
    private void HandleInput()
    {
        bool inputPressed = false;
        
        if (_useKeyInput && Input.GetKeyDown(_readyKey))
            inputPressed = true;
            
        if (_useButtonInput && Input.GetButtonDown(_readyButton))
            inputPressed = true;
            
        if (inputPressed)
        {
            ToggleReadyState();
        }
    }
    
    private IEnumerator ReadyStateCheckCoroutine()
    {
        while (true)
        {
            CheckReadyConditions();
            yield return new WaitForSeconds(_readyCheckInterval);
        }
    }
    
    private void CheckReadyConditions()
    {
        bool canBeReady = true;
        
        if (_requireGrounded && !IsGrounded())
            canBeReady = false;
            
        if (_requireMinHealth && _currentHealth < _minHealthThreshold)
            canBeReady = false;
            
        bool newReadyState = _isReady && canBeReady;
        
        if (newReadyState != _wasReady)
        {
            _isReady = newReadyState;
            OnReadyStateChange();
        }
    }
    
    private bool IsGrounded()
    {
        if (_groundCheckPoint == null) return true;
        
        return Physics.CheckSphere(_groundCheckPoint.position, _groundCheckRadius, _groundLayerMask);
    }
    
    public void ToggleReadyState()
    {
        SetReadyState(!_isReady);
    }
    
    public void SetReadyState(bool ready)
    {
        bool canBeReady = true;
        
        if (ready)
        {
            if (_requireGrounded && !IsGrounded())
                canBeReady = false;
                
            if (_requireMinHealth && _currentHealth < _minHealthThreshold)
                canBeReady = false;
        }
        
        bool newState = ready && canBeReady;
        
        if (newState != _isReady)
        {
            _isReady = newState;
            OnReadyStateChange();
        }
    }
    
    private void OnReadyStateChange()
    {
        if (_isReady != _wasReady)
        {
            if (_isReady)
            {
                OnBecomeReady?.Invoke();
                PlayReadySound();
            }
            else
            {
                OnBecomeNotReady?.Invoke();
                PlayNotReadySound();
            }
            
            OnReadyStateChanged?.Invoke();
            UpdateVisualFeedback();
            _wasReady = _isReady;
        }
    }
    
    private void UpdateVisualFeedback()
    {
        if (_readyIndicator != null)
        {
            _readyIndicator.SetActive(_isReady);
        }
        
        if (_playerRenderer != null)
        {
            Color targetColor = _isReady ? _readyColor : _notReadyColor;
            _playerRenderer.material.color = Color.Lerp(_originalColor, targetColor, 0.5f);
        }
    }
    
    private void PlayReadySound()
    {
        if (_audioSource != null && _readySound != null)
        {
            _audioSource.PlayOneShot(_readySound);
        }
    }
    
    private void PlayNotReadySound()
    {
        if (_audioSource != null && _notReadySound != null)
        {
            _audioSource.PlayOneShot(_notReadySound);
        }
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        CheckReadyConditions();
    }
    
    public void Heal(float healAmount)
    {
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + healAmount);
        CheckReadyConditions();
    }
    
    public void SetHealth(float health)
    {
        _currentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        CheckReadyConditions();
    }
    
    public void ResetReadyState()
    {
        _isReady = false;
        _wasReady = false;
        UpdateVisualFeedback();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_groundCheckPoint != null)
        {
            Gizmos.color = IsGrounded() ? Color.green : Color.red;
            Gizmos.DrawWireSphere(_groundCheckPoint.position, _groundCheckRadius);
        }
    }
    
    private void OnValidate()
    {
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        _minHealthThreshold = Mathf.Clamp(_minHealthThreshold, 0f, _maxHealth);
    }
}