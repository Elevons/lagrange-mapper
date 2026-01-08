// Prompt: depth of field focus
// Type: general

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DepthOfFieldFocus : MonoBehaviour
{
    [Header("Focus Settings")]
    [SerializeField] private Camera _targetCamera;
    [SerializeField] private LayerMask _focusLayerMask = -1;
    [SerializeField] private float _focusSpeed = 5f;
    [SerializeField] private float _minFocusDistance = 0.1f;
    [SerializeField] private float _maxFocusDistance = 100f;
    [SerializeField] private float _defaultFocusDistance = 10f;
    
    [Header("Auto Focus")]
    [SerializeField] private bool _enableAutoFocus = true;
    [SerializeField] private float _autoFocusUpdateRate = 0.1f;
    [SerializeField] private Vector2 _screenCenter = new Vector2(0.5f, 0.5f);
    
    [Header("Manual Focus")]
    [SerializeField] private bool _enableManualFocus = false;
    [SerializeField] private KeyCode _focusKey = KeyCode.F;
    [SerializeField] private Transform _manualFocusTarget;
    
    [Header("Depth of Field Settings")]
    [SerializeField] private float _aperture = 5.6f;
    [SerializeField] private float _focalLength = 50f;
    [SerializeField] private int _bladeCount = 5;
    [SerializeField] private float _bladeCurvature = 1f;
    [SerializeField] private float _bladeRotation = 0f;
    
    private Volume _postProcessVolume;
    private DepthOfField _depthOfField;
    private float _currentFocusDistance;
    private float _targetFocusDistance;
    private float _lastAutoFocusTime;
    private bool _isInitialized;
    
    private void Start()
    {
        InitializeComponents();
        _currentFocusDistance = _defaultFocusDistance;
        _targetFocusDistance = _defaultFocusDistance;
    }
    
    private void InitializeComponents()
    {
        if (_targetCamera == null)
            _targetCamera = Camera.main;
            
        if (_targetCamera == null)
            _targetCamera = FindObjectOfType<Camera>();
            
        if (_targetCamera == null)
        {
            Debug.LogError("DepthOfFieldFocus: No camera found!");
            return;
        }
        
        _postProcessVolume = FindObjectOfType<Volume>();
        
        if (_postProcessVolume == null)
        {
            GameObject volumeGO = new GameObject("Post Process Volume");
            _postProcessVolume = volumeGO.AddComponent<Volume>();
            _postProcessVolume.isGlobal = true;
        }
        
        if (_postProcessVolume.profile == null)
        {
            _postProcessVolume.profile = ScriptableObject.CreateInstance<VolumeProfile>();
        }
        
        if (!_postProcessVolume.profile.TryGet<DepthOfField>(out _depthOfField))
        {
            _depthOfField = _postProcessVolume.profile.Add<DepthOfField>(false);
        }
        
        SetupDepthOfField();
        _isInitialized = true;
    }
    
    private void SetupDepthOfField()
    {
        if (_depthOfField == null) return;
        
        _depthOfField.mode.value = DepthOfFieldMode.Bokeh;
        _depthOfField.focusDistance.value = _currentFocusDistance;
        _depthOfField.aperture.value = _aperture;
        _depthOfField.focalLength.value = _focalLength;
        _depthOfField.bladeCount.value = _bladeCount;
        _depthOfField.bladeCurvature.value = _bladeCurvature;
        _depthOfField.bladeRotation.value = _bladeRotation;
        _depthOfField.active = true;
    }
    
    private void Update()
    {
        if (!_isInitialized) return;
        
        HandleManualFocus();
        HandleAutoFocus();
        UpdateFocusDistance();
        UpdateDepthOfField();
    }
    
    private void HandleManualFocus()
    {
        if (!_enableManualFocus) return;
        
        if (Input.GetKeyDown(_focusKey))
        {
            if (_manualFocusTarget != null)
            {
                FocusOnTarget(_manualFocusTarget);
            }
            else
            {
                FocusOnScreenCenter();
            }
        }
    }
    
    private void HandleAutoFocus()
    {
        if (!_enableAutoFocus) return;
        
        if (Time.time - _lastAutoFocusTime >= _autoFocusUpdateRate)
        {
            FocusOnScreenCenter();
            _lastAutoFocusTime = Time.time;
        }
    }
    
    private void FocusOnScreenCenter()
    {
        Vector3 screenPoint = new Vector3(
            _screenCenter.x * Screen.width,
            _screenCenter.y * Screen.height,
            0f
        );
        
        Ray ray = _targetCamera.ScreenPointToRay(screenPoint);
        
        if (Physics.Raycast(ray, out RaycastHit hit, _maxFocusDistance, _focusLayerMask))
        {
            float distance = Vector3.Distance(_targetCamera.transform.position, hit.point);
            SetTargetFocusDistance(distance);
        }
        else
        {
            SetTargetFocusDistance(_defaultFocusDistance);
        }
    }
    
    private void FocusOnTarget(Transform target)
    {
        if (target == null) return;
        
        float distance = Vector3.Distance(_targetCamera.transform.position, target.position);
        SetTargetFocusDistance(distance);
    }
    
    private void SetTargetFocusDistance(float distance)
    {
        _targetFocusDistance = Mathf.Clamp(distance, _minFocusDistance, _maxFocusDistance);
    }
    
    private void UpdateFocusDistance()
    {
        if (Mathf.Abs(_currentFocusDistance - _targetFocusDistance) > 0.01f)
        {
            _currentFocusDistance = Mathf.Lerp(
                _currentFocusDistance,
                _targetFocusDistance,
                _focusSpeed * Time.deltaTime
            );
        }
    }
    
    private void UpdateDepthOfField()
    {
        if (_depthOfField == null) return;
        
        _depthOfField.focusDistance.value = _currentFocusDistance;
        _depthOfField.aperture.value = _aperture;
        _depthOfField.focalLength.value = _focalLength;
        _depthOfField.bladeCount.value = _bladeCount;
        _depthOfField.bladeCurvature.value = _bladeCurvature;
        _depthOfField.bladeRotation.value = _bladeRotation;
    }
    
    public void SetFocusDistance(float distance)
    {
        SetTargetFocusDistance(distance);
    }
    
    public void SetAperture(float aperture)
    {
        _aperture = Mathf.Clamp(aperture, 1f, 32f);
    }
    
    public void SetFocalLength(float focalLength)
    {
        _focalLength = Mathf.Clamp(focalLength, 1f, 300f);
    }
    
    public void EnableAutoFocus(bool enable)
    {
        _enableAutoFocus = enable;
    }
    
    public void SetManualFocusTarget(Transform target)
    {
        _manualFocusTarget = target;
    }
    
    public float GetCurrentFocusDistance()
    {
        return _currentFocusDistance;
    }
    
    private void OnValidate()
    {
        _aperture = Mathf.Clamp(_aperture, 1f, 32f);
        _focalLength = Mathf.Clamp(_focalLength, 1f, 300f);
        _focusSpeed = Mathf.Max(0.1f, _focusSpeed);
        _minFocusDistance = Mathf.Max(0.1f, _minFocusDistance);
        _maxFocusDistance = Mathf.Max(_minFocusDistance + 0.1f, _maxFocusDistance);
        _defaultFocusDistance = Mathf.Clamp(_defaultFocusDistance, _minFocusDistance, _maxFocusDistance);
        _autoFocusUpdateRate = Mathf.Max(0.01f, _autoFocusUpdateRate);
        _screenCenter.x = Mathf.Clamp01(_screenCenter.x);
        _screenCenter.y = Mathf.Clamp01(_screenCenter.y);
        _bladeCount = Mathf.Clamp(_bladeCount, 3, 9);
        _bladeCurvature = Mathf.Clamp01(_bladeCurvature);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_targetCamera == null) return;
        
        Gizmos.color = Color.yellow;
        Vector3 cameraPos = _targetCamera.transform.position;
        Vector3 forward = _targetCamera.transform.forward;
        
        // Draw focus distance
        Vector3 focusPoint = cameraPos + forward * _currentFocusDistance;
        Gizmos.DrawWireSphere(focusPoint, 0.5f);
        
        // Draw focus ray from screen center
        Vector3 screenPoint = new Vector3(
            _screenCenter.x * Screen.width,
            _screenCenter.y * Screen.height,
            0f
        );
        
        Ray ray = _targetCamera.ScreenPointToRay(screenPoint);
        Gizmos.color = Color.red;
        Gizmos.DrawRay(ray.origin, ray.direction * _maxFocusDistance);
    }
}