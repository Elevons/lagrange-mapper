// Prompt: motion blur on speed
// Type: general

using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class SpeedMotionBlur : MonoBehaviour
{
    [Header("Motion Blur Settings")]
    [SerializeField] private PostProcessVolume _postProcessVolume;
    [SerializeField] private float _speedThreshold = 5f;
    [SerializeField] private float _maxBlurIntensity = 0.8f;
    [SerializeField] private float _blurTransitionSpeed = 2f;
    [SerializeField] private AnimationCurve _blurCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Speed Detection")]
    [SerializeField] private Transform _targetTransform;
    [SerializeField] private bool _useRigidbody = true;
    [SerializeField] private bool _detectVerticalSpeed = true;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = false;
    
    private MotionBlur _motionBlur;
    private Rigidbody _rigidbody;
    private Vector3 _lastPosition;
    private float _currentSpeed;
    private float _targetBlurIntensity;
    private float _currentBlurIntensity;
    
    void Start()
    {
        InitializeComponents();
        SetupMotionBlur();
        
        if (_targetTransform == null)
            _targetTransform = transform;
            
        _lastPosition = _targetTransform.position;
    }
    
    void InitializeComponents()
    {
        if (_postProcessVolume == null)
            _postProcessVolume = FindObjectOfType<PostProcessVolume>();
            
        if (_postProcessVolume == null)
        {
            Debug.LogError("SpeedMotionBlur: No PostProcessVolume found in scene!");
            enabled = false;
            return;
        }
        
        if (_useRigidbody && _targetTransform != null)
            _rigidbody = _targetTransform.GetComponent<Rigidbody>();
    }
    
    void SetupMotionBlur()
    {
        if (_postProcessVolume.profile.TryGetSettings(out _motionBlur))
        {
            _motionBlur.enabled.value = true;
            _motionBlur.shutterAngle.value = 0f;
        }
        else
        {
            Debug.LogError("SpeedMotionBlur: MotionBlur effect not found in PostProcess profile!");
            enabled = false;
        }
    }
    
    void Update()
    {
        CalculateSpeed();
        UpdateMotionBlur();
        
        if (_showDebugInfo)
            DisplayDebugInfo();
    }
    
    void CalculateSpeed()
    {
        if (_targetTransform == null) return;
        
        if (_useRigidbody && _rigidbody != null)
        {
            Vector3 velocity = _rigidbody.velocity;
            if (!_detectVerticalSpeed)
                velocity.y = 0f;
                
            _currentSpeed = velocity.magnitude;
        }
        else
        {
            Vector3 currentPosition = _targetTransform.position;
            Vector3 deltaPosition = currentPosition - _lastPosition;
            
            if (!_detectVerticalSpeed)
                deltaPosition.y = 0f;
                
            _currentSpeed = deltaPosition.magnitude / Time.deltaTime;
            _lastPosition = currentPosition;
        }
    }
    
    void UpdateMotionBlur()
    {
        if (_motionBlur == null) return;
        
        float speedRatio = Mathf.Clamp01((_currentSpeed - _speedThreshold) / _speedThreshold);
        _targetBlurIntensity = _blurCurve.Evaluate(speedRatio) * _maxBlurIntensity;
        
        _currentBlurIntensity = Mathf.Lerp(_currentBlurIntensity, _targetBlurIntensity, 
            Time.deltaTime * _blurTransitionSpeed);
        
        float shutterAngle = _currentBlurIntensity * 360f;
        _motionBlur.shutterAngle.value = shutterAngle;
    }
    
    void DisplayDebugInfo()
    {
        Debug.Log($"Speed: {_currentSpeed:F2} | Blur Intensity: {_currentBlurIntensity:F3} | Shutter Angle: {_motionBlur.shutterAngle.value:F1}");
    }
    
    public void SetSpeedThreshold(float threshold)
    {
        _speedThreshold = Mathf.Max(0f, threshold);
    }
    
    public void SetMaxBlurIntensity(float intensity)
    {
        _maxBlurIntensity = Mathf.Clamp01(intensity);
    }
    
    public void SetBlurTransitionSpeed(float speed)
    {
        _blurTransitionSpeed = Mathf.Max(0.1f, speed);
    }
    
    public void EnableMotionBlur(bool enable)
    {
        if (_motionBlur != null)
            _motionBlur.enabled.value = enable;
    }
    
    public float GetCurrentSpeed()
    {
        return _currentSpeed;
    }
    
    public float GetCurrentBlurIntensity()
    {
        return _currentBlurIntensity;
    }
    
    void OnValidate()
    {
        _speedThreshold = Mathf.Max(0f, _speedThreshold);
        _maxBlurIntensity = Mathf.Clamp01(_maxBlurIntensity);
        _blurTransitionSpeed = Mathf.Max(0.1f, _blurTransitionSpeed);
    }
}