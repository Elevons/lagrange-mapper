// Prompt: rotating puzzle ring
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class RotatingPuzzleRing : MonoBehaviour
{
    [Header("Ring Configuration")]
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _targetAngle = 0f;
    [SerializeField] private float _angleThreshold = 5f;
    [SerializeField] private bool _canRotate = true;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode _rotateClockwiseKey = KeyCode.E;
    [SerializeField] private KeyCode _rotateCounterClockwiseKey = KeyCode.Q;
    [SerializeField] private bool _useMouseInteraction = true;
    [SerializeField] private float _interactionDistance = 5f;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _correctColor = Color.green;
    [SerializeField] private Color _highlightColor = Color.yellow;
    [SerializeField] private Renderer _ringRenderer;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rotateSound;
    [SerializeField] private AudioClip _correctPositionSound;
    
    [Header("Events")]
    public UnityEvent OnCorrectPosition;
    public UnityEvent OnRotationStart;
    public UnityEvent OnRotationComplete;
    
    private float _currentAngle;
    private bool _isRotating;
    private bool _isInCorrectPosition;
    private bool _isHighlighted;
    private Camera _playerCamera;
    private Material _ringMaterial;
    private Color _originalColor;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_ringRenderer == null)
            _ringRenderer = GetComponent<Renderer>();
            
        if (_ringRenderer != null)
        {
            _ringMaterial = _ringRenderer.material;
            _originalColor = _ringMaterial.color;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        _currentAngle = transform.eulerAngles.z;
        CheckCorrectPosition();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateRotation();
        UpdateVisuals();
    }
    
    private void HandleInput()
    {
        if (!_canRotate || _isRotating)
            return;
            
        bool rotateClockwise = false;
        bool rotateCounterClockwise = false;
        
        // Keyboard input
        if (Input.GetKeyDown(_rotateClockwiseKey))
            rotateClockwise = true;
        else if (Input.GetKeyDown(_rotateCounterClockwiseKey))
            rotateCounterClockwise = true;
            
        // Mouse interaction
        if (_useMouseInteraction && Input.GetMouseButtonDown(0))
        {
            Ray ray = _playerCamera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit, _interactionDistance))
            {
                if (hit.collider.gameObject == gameObject)
                {
                    Vector3 hitPoint = hit.point;
                    Vector3 centerToHit = hitPoint - transform.position;
                    Vector3 mouseWorldPos = _playerCamera.ScreenToWorldPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, hit.distance));
                    
                    float angle = Vector3.SignedAngle(Vector3.right, centerToHit, Vector3.forward);
                    if (angle > 0)
                        rotateClockwise = true;
                    else
                        rotateCounterClockwise = true;
                }
            }
        }
        
        if (rotateClockwise)
            StartRotation(90f);
        else if (rotateCounterClockwise)
            StartRotation(-90f);
    }
    
    private void StartRotation(float rotationAmount)
    {
        if (_isRotating)
            return;
            
        _isRotating = true;
        _targetAngle = _currentAngle + rotationAmount;
        
        // Normalize angle
        while (_targetAngle >= 360f)
            _targetAngle -= 360f;
        while (_targetAngle < 0f)
            _targetAngle += 360f;
            
        OnRotationStart?.Invoke();
        PlaySound(_rotateSound);
    }
    
    private void UpdateRotation()
    {
        if (!_isRotating)
            return;
            
        float step = _rotationSpeed * Time.deltaTime;
        _currentAngle = Mathf.MoveTowardsAngle(_currentAngle, _targetAngle, step);
        
        transform.rotation = Quaternion.Euler(0, 0, _currentAngle);
        
        if (Mathf.Abs(Mathf.DeltaAngle(_currentAngle, _targetAngle)) < 0.1f)
        {
            _currentAngle = _targetAngle;
            transform.rotation = Quaternion.Euler(0, 0, _currentAngle);
            _isRotating = false;
            
            OnRotationComplete?.Invoke();
            CheckCorrectPosition();
        }
    }
    
    private void CheckCorrectPosition()
    {
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, 0f));
        bool wasInCorrectPosition = _isInCorrectPosition;
        _isInCorrectPosition = angleDifference <= _angleThreshold;
        
        if (_isInCorrectPosition && !wasInCorrectPosition)
        {
            OnCorrectPosition?.Invoke();
            PlaySound(_correctPositionSound);
        }
    }
    
    private void UpdateVisuals()
    {
        if (_ringMaterial == null)
            return;
            
        Color targetColor = _originalColor;
        
        if (_isInCorrectPosition)
            targetColor = _correctColor;
        else if (_isHighlighted)
            targetColor = _highlightColor;
        else
            targetColor = _normalColor;
            
        _ringMaterial.color = Color.Lerp(_ringMaterial.color, targetColor, Time.deltaTime * 5f);
    }
    
    private void OnMouseEnter()
    {
        if (_useMouseInteraction && _canRotate)
        {
            _isHighlighted = true;
        }
    }
    
    private void OnMouseExit()
    {
        _isHighlighted = false;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void SetCanRotate(bool canRotate)
    {
        _canRotate = canRotate;
    }
    
    public void SetTargetAngle(float angle)
    {
        while (angle >= 360f)
            angle -= 360f;
        while (angle < 0f)
            angle += 360f;
            
        // Update the correct position check to use the new target
        float angleDifference = Mathf.Abs(Mathf.DeltaAngle(_currentAngle, angle));
        _isInCorrectPosition = angleDifference <= _angleThreshold;
    }
    
    public bool IsInCorrectPosition()
    {
        return _isInCorrectPosition;
    }
    
    public float GetCurrentAngle()
    {
        return _currentAngle;
    }
    
    public void ResetRing()
    {
        _isRotating = false;
        _currentAngle = 0f;
        transform.rotation = Quaternion.identity;
        CheckCorrectPosition();
    }
}