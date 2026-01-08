// Prompt: stealth crouch with visibility meter
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class StealthCrouchSystem : MonoBehaviour
{
    [Header("Crouch Settings")]
    [SerializeField] private KeyCode _crouchKey = KeyCode.LeftControl;
    [SerializeField] private float _crouchHeight = 1f;
    [SerializeField] private float _standHeight = 2f;
    [SerializeField] private float _crouchSpeed = 2f;
    [SerializeField] private float _standSpeed = 5f;
    [SerializeField] private float _heightTransitionSpeed = 8f;
    
    [Header("Visibility Settings")]
    [SerializeField] private float _maxVisibility = 100f;
    [SerializeField] private float _visibilityDecreaseRate = 20f;
    [SerializeField] private float _visibilityIncreaseRate = 30f;
    [SerializeField] private float _crouchVisibilityMultiplier = 0.3f;
    [SerializeField] private float _movementVisibilityMultiplier = 1.5f;
    [SerializeField] private float _detectionRange = 10f;
    
    [Header("UI")]
    [SerializeField] private Slider _visibilityMeter;
    [SerializeField] private Image _visibilityFill;
    [SerializeField] private Color _lowVisibilityColor = Color.green;
    [SerializeField] private Color _highVisibilityColor = Color.red;
    [SerializeField] private Canvas _stealthUI;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _crouchSound;
    [SerializeField] private AudioClip _standSound;
    
    private CharacterController _characterController;
    private Camera _playerCamera;
    private bool _isCrouching;
    private float _currentVisibility;
    private float _targetHeight;
    private Vector3 _lastPosition;
    private List<Transform> _nearbyEnemies = new List<Transform>();
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _playerCamera = GetComponentInChildren<Camera>();
        
        if (_characterController == null)
        {
            _characterController = gameObject.AddComponent<CharacterController>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _targetHeight = _standHeight;
        _characterController.height = _standHeight;
        _currentVisibility = 0f;
        _lastPosition = transform.position;
        
        SetupUI();
    }
    
    private void Update()
    {
        HandleCrouchInput();
        UpdateHeight();
        UpdateVisibility();
        UpdateUI();
        FindNearbyEnemies();
    }
    
    private void HandleCrouchInput()
    {
        bool crouchPressed = Input.GetKey(_crouchKey);
        
        if (crouchPressed && !_isCrouching)
        {
            StartCrouch();
        }
        else if (!crouchPressed && _isCrouching)
        {
            StopCrouch();
        }
    }
    
    private void StartCrouch()
    {
        _isCrouching = true;
        _targetHeight = _crouchHeight;
        
        if (_audioSource && _crouchSound)
        {
            _audioSource.PlayOneShot(_crouchSound);
        }
    }
    
    private void StopCrouch()
    {
        if (CanStandUp())
        {
            _isCrouching = false;
            _targetHeight = _standHeight;
            
            if (_audioSource && _standSound)
            {
                _audioSource.PlayOneShot(_standSound);
            }
        }
    }
    
    private bool CanStandUp()
    {
        float checkHeight = _standHeight - _crouchHeight;
        Vector3 checkStart = transform.position + Vector3.up * _crouchHeight;
        
        return !Physics.SphereCast(checkStart, _characterController.radius, Vector3.up, out RaycastHit hit, checkHeight);
    }
    
    private void UpdateHeight()
    {
        float currentHeight = _characterController.height;
        float newHeight = Mathf.MoveTowards(currentHeight, _targetHeight, _heightTransitionSpeed * Time.deltaTime);
        
        if (Mathf.Abs(newHeight - currentHeight) > 0.01f)
        {
            Vector3 centerOffset = Vector3.up * (newHeight - currentHeight) * 0.5f;
            _characterController.height = newHeight;
            _characterController.center = Vector3.up * newHeight * 0.5f;
            
            if (_playerCamera)
            {
                Vector3 cameraPos = _playerCamera.transform.localPosition;
                cameraPos.y += (newHeight - currentHeight) * 0.8f;
                _playerCamera.transform.localPosition = cameraPos;
            }
        }
    }
    
    private void UpdateVisibility()
    {
        float visibilityChange = 0f;
        bool isMoving = Vector3.Distance(transform.position, _lastPosition) > 0.01f;
        bool isInEnemySight = IsInEnemySight();
        
        if (isInEnemySight)
        {
            visibilityChange = _visibilityIncreaseRate;
            
            if (_isCrouching)
            {
                visibilityChange *= _crouchVisibilityMultiplier;
            }
            
            if (isMoving)
            {
                visibilityChange *= _movementVisibilityMultiplier;
            }
        }
        else
        {
            visibilityChange = -_visibilityDecreaseRate;
        }
        
        _currentVisibility = Mathf.Clamp(_currentVisibility + visibilityChange * Time.deltaTime, 0f, _maxVisibility);
        _lastPosition = transform.position;
    }
    
    private bool IsInEnemySight()
    {
        foreach (Transform enemy in _nearbyEnemies)
        {
            if (enemy == null) continue;
            
            Vector3 directionToPlayer = (transform.position - enemy.position).normalized;
            float distanceToPlayer = Vector3.Distance(transform.position, enemy.position);
            
            if (distanceToPlayer <= _detectionRange)
            {
                if (Physics.Raycast(enemy.position, directionToPlayer, out RaycastHit hit, distanceToPlayer))
                {
                    if (hit.transform == transform)
                    {
                        return true;
                    }
                }
            }
        }
        
        return false;
    }
    
    private void FindNearbyEnemies()
    {
        _nearbyEnemies.Clear();
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _detectionRange);
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Enemy") && col.transform != transform)
            {
                _nearbyEnemies.Add(col.transform);
            }
        }
    }
    
    private void UpdateUI()
    {
        if (_visibilityMeter)
        {
            _visibilityMeter.value = _currentVisibility / _maxVisibility;
        }
        
        if (_visibilityFill)
        {
            float normalizedVisibility = _currentVisibility / _maxVisibility;
            _visibilityFill.color = Color.Lerp(_lowVisibilityColor, _highVisibilityColor, normalizedVisibility);
        }
        
        if (_stealthUI)
        {
            _stealthUI.gameObject.SetActive(_nearbyEnemies.Count > 0);
        }
    }
    
    private void SetupUI()
    {
        if (_visibilityMeter == null && _stealthUI != null)
        {
            _visibilityMeter = _stealthUI.GetComponentInChildren<Slider>();
        }
        
        if (_visibilityFill == null && _visibilityMeter != null)
        {
            _visibilityFill = _visibilityMeter.fillRect.GetComponent<Image>();
        }
    }
    
    public bool IsCrouching()
    {
        return _isCrouching;
    }
    
    public float GetVisibilityPercentage()
    {
        return _currentVisibility / _maxVisibility;
    }
    
    public bool IsDetected()
    {
        return _currentVisibility >= _maxVisibility;
    }
    
    public float GetMovementSpeed()
    {
        return _isCrouching ? _crouchSpeed : _standSpeed;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        if (_isCrouching)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position + Vector3.up * _crouchHeight * 0.5f, 
                new Vector3(1f, _crouchHeight, 1f));
        }
    }
}