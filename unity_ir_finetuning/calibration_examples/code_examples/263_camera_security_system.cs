// Prompt: camera security system
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class SecurityCamera : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private Camera _securityCamera;
    [SerializeField] private Transform _cameraHead;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _fieldOfView = 60f;
    [SerializeField] private LayerMask _detectionLayers = -1;
    [SerializeField] private LayerMask _obstacleLayers = -1;
    
    [Header("Rotation Settings")]
    [SerializeField] private bool _enableRotation = true;
    [SerializeField] private float _rotationSpeed = 30f;
    [SerializeField] private float _minRotationAngle = -45f;
    [SerializeField] private float _maxRotationAngle = 45f;
    [SerializeField] private float _pauseDuration = 2f;
    
    [Header("Detection Settings")]
    [SerializeField] private float _detectionTime = 3f;
    [SerializeField] private float _lostTargetTime = 5f;
    [SerializeField] private bool _requireLineOfSight = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private Light _cameraLight;
    [SerializeField] private Color _normalColor = Color.green;
    [SerializeField] private Color _alertColor = Color.red;
    [SerializeField] private Color _detectedColor = Color.red;
    [SerializeField] private MeshRenderer _cameraRenderer;
    [SerializeField] private Material _normalMaterial;
    [SerializeField] private Material _alertMaterial;
    [SerializeField] private Material _detectedMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _detectionSound;
    [SerializeField] private AudioClip _alertSound;
    [SerializeField] private AudioClip _lostTargetSound;
    
    [Header("Events")]
    public UnityEvent<Transform> OnTargetDetected;
    public UnityEvent<Transform> OnTargetLost;
    public UnityEvent<Transform> OnTargetFullyDetected;
    
    private CameraState _currentState = CameraState.Normal;
    private Transform _currentTarget;
    private float _detectionProgress = 0f;
    private float _lostTargetTimer = 0f;
    private bool _rotatingRight = true;
    private float _pauseTimer = 0f;
    private bool _isPaused = false;
    private List<Transform> _detectedTargets = new List<Transform>();
    private Coroutine _rotationCoroutine;
    
    private enum CameraState
    {
        Normal,
        Alert,
        Detected,
        Tracking
    }
    
    private void Start()
    {
        InitializeCamera();
        if (_enableRotation)
        {
            _rotationCoroutine = StartCoroutine(RotationRoutine());
        }
    }
    
    private void InitializeCamera()
    {
        if (_securityCamera == null)
            _securityCamera = GetComponentInChildren<Camera>();
        
        if (_cameraHead == null)
            _cameraHead = _securityCamera != null ? _securityCamera.transform : transform;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_cameraLight == null)
            _cameraLight = GetComponentInChildren<Light>();
        
        if (_cameraRenderer == null)
            _cameraRenderer = GetComponent<MeshRenderer>();
        
        SetCameraState(CameraState.Normal);
    }
    
    private void Update()
    {
        DetectTargets();
        UpdateDetectionProgress();
        UpdateVisuals();
    }
    
    private void DetectTargets()
    {
        _detectedTargets.Clear();
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRange, _detectionLayers);
        
        foreach (Collider col in colliders)
        {
            if (col.transform == transform) continue;
            
            if (IsTargetInFieldOfView(col.transform) && (!_requireLineOfSight || HasLineOfSight(col.transform)))
            {
                _detectedTargets.Add(col.transform);
            }
        }
        
        HandleTargetDetection();
    }
    
    private bool IsTargetInFieldOfView(Transform target)
    {
        Vector3 directionToTarget = (target.position - _cameraHead.position).normalized;
        float angle = Vector3.Angle(_cameraHead.forward, directionToTarget);
        return angle <= _fieldOfView * 0.5f;
    }
    
    private bool HasLineOfSight(Transform target)
    {
        Vector3 directionToTarget = target.position - _cameraHead.position;
        float distanceToTarget = directionToTarget.magnitude;
        
        if (Physics.Raycast(_cameraHead.position, directionToTarget.normalized, out RaycastHit hit, distanceToTarget, _obstacleLayers))
        {
            return hit.transform == target;
        }
        
        return true;
    }
    
    private void HandleTargetDetection()
    {
        if (_detectedTargets.Count > 0)
        {
            Transform closestTarget = GetClosestTarget();
            
            if (_currentTarget != closestTarget)
            {
                if (_currentTarget != null)
                {
                    OnTargetLost?.Invoke(_currentTarget);
                    PlaySound(_lostTargetSound);
                }
                
                _currentTarget = closestTarget;
                _detectionProgress = 0f;
                OnTargetDetected?.Invoke(_currentTarget);
                SetCameraState(CameraState.Alert);
            }
            
            _lostTargetTimer = 0f;
        }
        else
        {
            if (_currentTarget != null)
            {
                _lostTargetTimer += Time.deltaTime;
                
                if (_lostTargetTimer >= _lostTargetTime)
                {
                    OnTargetLost?.Invoke(_currentTarget);
                    PlaySound(_lostTargetSound);
                    _currentTarget = null;
                    _detectionProgress = 0f;
                    SetCameraState(CameraState.Normal);
                }
            }
        }
    }
    
    private Transform GetClosestTarget()
    {
        Transform closest = null;
        float closestDistance = float.MaxValue;
        
        foreach (Transform target in _detectedTargets)
        {
            float distance = Vector3.Distance(transform.position, target.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = target;
            }
        }
        
        return closest;
    }
    
    private void UpdateDetectionProgress()
    {
        if (_currentTarget != null && _detectedTargets.Contains(_currentTarget))
        {
            _detectionProgress += Time.deltaTime / _detectionTime;
            
            if (_detectionProgress >= 1f && _currentState != CameraState.Detected)
            {
                SetCameraState(CameraState.Detected);
                OnTargetFullyDetected?.Invoke(_currentTarget);
                PlaySound(_detectionSound);
            }
        }
        else if (_currentTarget != null)
        {
            _detectionProgress -= Time.deltaTime / (_detectionTime * 0.5f);
            _detectionProgress = Mathf.Max(0f, _detectionProgress);
            
            if (_detectionProgress <= 0f && _currentState == CameraState.Detected)
            {
                SetCameraState(CameraState.Alert);
            }
        }
    }
    
    private void SetCameraState(CameraState newState)
    {
        _currentState = newState;
        
        switch (_currentState)
        {
            case CameraState.Normal:
                if (_enableRotation && _rotationCoroutine == null)
                    _rotationCoroutine = StartCoroutine(RotationRoutine());
                break;
                
            case CameraState.Alert:
                if (_rotationCoroutine != null)
                {
                    StopCoroutine(_rotationCoroutine);
                    _rotationCoroutine = null;
                }
                TrackTarget();
                PlaySound(_alertSound);
                break;
                
            case CameraState.Detected:
                TrackTarget();
                break;
        }
    }
    
    private void TrackTarget()
    {
        if (_currentTarget != null)
        {
            Vector3 directionToTarget = (_currentTarget.position - _cameraHead.position).normalized;
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            _cameraHead.rotation = Quaternion.Slerp(_cameraHead.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private IEnumerator RotationRoutine()
    {
        while (_enableRotation && _currentState == CameraState.Normal)
        {
            if (!_isPaused)
            {
                float currentY = _cameraHead.localEulerAngles.y;
                if (currentY > 180f) currentY -= 360f;
                
                if (_rotatingRight)
                {
                    if (currentY >= _maxRotationAngle)
                    {
                        _rotatingRight = false;
                        _isPaused = true;
                        _pauseTimer = 0f;
                    }
                    else
                    {
                        _cameraHead.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
                    }
                }
                else
                {
                    if (currentY <= _minRotationAngle)
                    {
                        _rotatingRight = true;
                        _isPaused = true;
                        _pauseTimer = 0f;
                    }
                    else
                    {
                        _cameraHead.Rotate(0, -_rotationSpeed * Time.deltaTime, 0);
                    }
                }
            }
            else
            {
                _pauseTimer += Time.deltaTime;
                if (_pauseTimer >= _pauseDuration)
                {
                    _isPaused = false;
                }
            }
            
            yield return null;
        }
    }
    
    private void UpdateVisuals()
    {
        Color targetColor = _normalColor;
        Material targetMaterial = _normalMaterial;
        
        switch (_currentState)
        {
            case CameraState.Normal:
                targetColor = _normalColor;
                targetMaterial = _normalMaterial;
                break;
                
            case CameraState.Alert:
                targetColor = Color.Lerp(_normalColor, _alertColor, _detectionProgress);
                targetMaterial = _detectionProgress > 0.5f ? _alertMaterial : _normalMaterial;
                break;
                
            case CameraState.Detected:
                targetColor = _detectedColor;
                targetMaterial = _detectedMaterial;
                break;
        }
        
        if (_cameraLight != null)
        {
            _cameraLight.color = targetColor;
        }
        
        if (_cameraRenderer != null && targetMaterial != null)
        {
            _cameraRenderer.material = targetMaterial;
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        if (_cameraHead != null)
        {
            Gizmos.color = _currentState == CameraState.Detected ? Color.red : 
                          _currentState == CameraState.Alert ? Color.orange : Color.green;
            
            Vector3 forward = _cameraHead.forward;
            Vector3 right = Quaternion.AngleAxis(_fieldOfView * 0.5f, _cameraHead.up) * forward;
            Vector3 left = Quaternion.AngleAxis(-_fieldOfView * 0.5f, _cameraHead.up) * forward;
            
            Gizmos.DrawRay(_cameraHead.position, right * _detectionRange);
            Gizmos.DrawRay(_cameraHead.position, left * _detectionRange);
            Gizmos.DrawRay(_cameraHead.position, forward * _detectionRange);
        }
        
        if (_currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_cameraHead.position, _currentTarget.position);
        }
    }
}