// Prompt: lever that activates mechanism
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Lever : MonoBehaviour
{
    [Header("Lever Settings")]
    [SerializeField] private bool _isActivated = false;
    [SerializeField] private bool _canBeToggled = true;
    [SerializeField] private float _activationDistance = 2f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    
    [Header("Animation")]
    [SerializeField] private Transform _leverHandle;
    [SerializeField] private float _rotationAngle = 45f;
    [SerializeField] private float _animationSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private Light _indicatorLight;
    [SerializeField] private Color _activeColor = Color.green;
    [SerializeField] private Color _inactiveColor = Color.red;
    [SerializeField] private ParticleSystem _activationEffect;
    
    [Header("Events")]
    public UnityEvent OnLeverActivated;
    public UnityEvent OnLeverDeactivated;
    public UnityEvent<bool> OnLeverStateChanged;
    
    private Transform _player;
    private bool _playerInRange = false;
    private Vector3 _originalRotation;
    private Vector3 _targetRotation;
    private bool _isAnimating = false;
    
    private void Start()
    {
        if (_leverHandle != null)
        {
            _originalRotation = _leverHandle.localEulerAngles;
            _targetRotation = _originalRotation;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
            
        UpdateVisualState();
    }
    
    private void Update()
    {
        CheckForPlayer();
        HandleInput();
        AnimateLever();
    }
    
    private void CheckForPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _player = player.transform;
            float distance = Vector3.Distance(transform.position, _player.position);
            bool wasInRange = _playerInRange;
            _playerInRange = distance <= _activationDistance;
            
            if (_playerInRange != wasInRange)
            {
                if (_interactionPrompt != null)
                    _interactionPrompt.SetActive(_playerInRange);
            }
        }
        else
        {
            _playerInRange = false;
            if (_interactionPrompt != null)
                _interactionPrompt.SetActive(false);
        }
    }
    
    private void HandleInput()
    {
        if (_playerInRange && Input.GetKeyDown(_interactionKey) && !_isAnimating)
        {
            if (_canBeToggled || !_isActivated)
            {
                ToggleLever();
            }
        }
    }
    
    private void AnimateLever()
    {
        if (_leverHandle != null && _isAnimating)
        {
            _leverHandle.localEulerAngles = Vector3.Lerp(_leverHandle.localEulerAngles, _targetRotation, Time.deltaTime * _animationSpeed);
            
            if (Vector3.Distance(_leverHandle.localEulerAngles, _targetRotation) < 0.1f)
            {
                _leverHandle.localEulerAngles = _targetRotation;
                _isAnimating = false;
            }
        }
    }
    
    public void ToggleLever()
    {
        if (_isAnimating) return;
        
        _isActivated = !_isActivated;
        UpdateLeverState();
    }
    
    public void ActivateLever()
    {
        if (_isActivated || _isAnimating) return;
        
        _isActivated = true;
        UpdateLeverState();
    }
    
    public void DeactivateLever()
    {
        if (!_isActivated || _isAnimating) return;
        
        _isActivated = false;
        UpdateLeverState();
    }
    
    private void UpdateLeverState()
    {
        if (_leverHandle != null)
        {
            _targetRotation = _originalRotation + (_isActivated ? Vector3.forward * _rotationAngle : Vector3.zero);
            _isAnimating = true;
        }
        
        PlaySound();
        UpdateVisualState();
        TriggerEffects();
        InvokeEvents();
    }
    
    private void PlaySound()
    {
        if (_audioSource != null)
        {
            AudioClip clipToPlay = _isActivated ? _activationSound : _deactivationSound;
            if (clipToPlay != null)
            {
                _audioSource.PlayOneShot(clipToPlay);
            }
        }
    }
    
    private void UpdateVisualState()
    {
        if (_indicatorLight != null)
        {
            _indicatorLight.color = _isActivated ? _activeColor : _inactiveColor;
            _indicatorLight.enabled = _isActivated;
        }
    }
    
    private void TriggerEffects()
    {
        if (_activationEffect != null && _isActivated)
        {
            _activationEffect.Play();
        }
    }
    
    private void InvokeEvents()
    {
        if (_isActivated)
        {
            OnLeverActivated?.Invoke();
        }
        else
        {
            OnLeverDeactivated?.Invoke();
        }
        
        OnLeverStateChanged?.Invoke(_isActivated);
    }
    
    public bool IsActivated()
    {
        return _isActivated;
    }
    
    public void SetCanBeToggled(bool canToggle)
    {
        _canBeToggled = canToggle;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _activationDistance);
    }
}