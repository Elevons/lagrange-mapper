// Prompt: bookshelf with hidden switch
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class BookshelfHiddenSwitch : MonoBehaviour
{
    [Header("Bookshelf Settings")]
    [SerializeField] private Transform _bookshelf;
    [SerializeField] private Transform _hiddenBook;
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Animation Settings")]
    [SerializeField] private float _bookPullDistance = 0.2f;
    [SerializeField] private float _animationDuration = 1f;
    [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _bookPullSound;
    [SerializeField] private AudioClip _switchActivateSound;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _highlightEffect;
    [SerializeField] private ParticleSystem _activationParticles;
    
    [Header("Events")]
    public UnityEvent OnSwitchActivated;
    public UnityEvent OnSwitchDeactivated;
    
    private bool _isActivated = false;
    private bool _isAnimating = false;
    private bool _playerInRange = false;
    private Vector3 _originalBookPosition;
    private Vector3 _pulledBookPosition;
    private Camera _playerCamera;
    private Collider _bookCollider;
    
    private void Start()
    {
        InitializeComponents();
        SetupPositions();
        SetupAudio();
    }
    
    private void InitializeComponents()
    {
        if (_hiddenBook == null)
            _hiddenBook = transform.GetChild(0);
            
        if (_bookshelf == null)
            _bookshelf = transform;
            
        _bookCollider = _hiddenBook.GetComponent<Collider>();
        if (_bookCollider == null)
            _bookCollider = _hiddenBook.gameObject.AddComponent<BoxCollider>();
            
        _playerCamera = Camera.main;
        
        if (_highlightEffect != null)
            _highlightEffect.SetActive(false);
    }
    
    private void SetupPositions()
    {
        _originalBookPosition = _hiddenBook.localPosition;
        _pulledBookPosition = _originalBookPosition + _hiddenBook.forward * _bookPullDistance;
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }
    
    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
    }
    
    private void CheckPlayerProximity()
    {
        bool wasInRange = _playerInRange;
        _playerInRange = false;
        
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, _interactionDistance, _playerLayer);
        
        foreach (Collider col in nearbyObjects)
        {
            if (col.CompareTag("Player"))
            {
                _playerInRange = true;
                break;
            }
        }
        
        if (_playerInRange != wasInRange)
        {
            if (_highlightEffect != null)
                _highlightEffect.SetActive(_playerInRange && !_isActivated);
        }
    }
    
    private void HandleInput()
    {
        if (!_playerInRange || _isAnimating)
            return;
            
        if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            if (IsLookingAtBook())
            {
                ToggleSwitch();
            }
        }
    }
    
    private bool IsLookingAtBook()
    {
        if (_playerCamera == null)
            return true;
            
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0));
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, _interactionDistance))
        {
            return hit.collider == _bookCollider;
        }
        
        return false;
    }
    
    private void ToggleSwitch()
    {
        if (_isAnimating)
            return;
            
        _isActivated = !_isActivated;
        StartCoroutine(AnimateBook());
        
        if (_isActivated)
        {
            PlaySound(_switchActivateSound);
            OnSwitchActivated?.Invoke();
            
            if (_activationParticles != null)
                _activationParticles.Play();
        }
        else
        {
            PlaySound(_bookPullSound);
            OnSwitchDeactivated?.Invoke();
        }
        
        if (_highlightEffect != null)
            _highlightEffect.SetActive(false);
    }
    
    private System.Collections.IEnumerator AnimateBook()
    {
        _isAnimating = true;
        
        Vector3 startPosition = _hiddenBook.localPosition;
        Vector3 targetPosition = _isActivated ? _pulledBookPosition : _originalBookPosition;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _animationDuration;
            float curveValue = _animationCurve.Evaluate(progress);
            
            _hiddenBook.localPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);
            
            yield return null;
        }
        
        _hiddenBook.localPosition = targetPosition;
        _isAnimating = false;
        
        if (_highlightEffect != null && _playerInRange && !_isActivated)
            _highlightEffect.SetActive(true);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
        }
    }
    
    public void ResetSwitch()
    {
        if (_isAnimating)
            return;
            
        _isActivated = false;
        _hiddenBook.localPosition = _originalBookPosition;
        
        if (_highlightEffect != null && _playerInRange)
            _highlightEffect.SetActive(true);
    }
    
    public void ForceActivate()
    {
        if (_isAnimating || _isActivated)
            return;
            
        _isActivated = true;
        _hiddenBook.localPosition = _pulledBookPosition;
        OnSwitchActivated?.Invoke();
        
        if (_activationParticles != null)
            _activationParticles.Play();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionDistance);
        
        if (_hiddenBook != null)
        {
            Gizmos.color = Color.red;
            Vector3 pullDirection = _hiddenBook.forward * _bookPullDistance;
            Gizmos.DrawLine(_hiddenBook.position, _hiddenBook.position + pullDirection);
        }
    }
}