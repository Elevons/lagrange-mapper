// Prompt: light that flickers randomly
// Type: general

using UnityEngine;

public class FlickeringLight : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light _targetLight;
    [SerializeField] private float _originalIntensity = 1f;
    [SerializeField] private bool _useOriginalColor = true;
    [SerializeField] private Color _originalColor = Color.white;
    
    [Header("Flicker Behavior")]
    [SerializeField] private float _minFlickerInterval = 0.1f;
    [SerializeField] private float _maxFlickerInterval = 2f;
    [SerializeField] private float _minIntensity = 0.1f;
    [SerializeField] private float _maxIntensity = 1.2f;
    [SerializeField] private float _flickerSpeed = 10f;
    
    [Header("Advanced Settings")]
    [SerializeField] private bool _randomizeColor = false;
    [SerializeField] private Color _flickerColorMin = Color.yellow;
    [SerializeField] private Color _flickerColorMax = Color.orange;
    [SerializeField] private AnimationCurve _flickerCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private bool _enableSmoothTransition = true;
    
    private float _nextFlickerTime;
    private float _targetIntensity;
    private Color _targetColor;
    private bool _isFlickering = false;
    private float _flickerDuration = 0.2f;
    private float _flickerStartTime;
    
    private void Start()
    {
        if (_targetLight == null)
        {
            _targetLight = GetComponent<Light>();
        }
        
        if (_targetLight == null)
        {
            Debug.LogError("FlickeringLight: No Light component found!");
            enabled = false;
            return;
        }
        
        _originalIntensity = _targetLight.intensity;
        if (_useOriginalColor)
        {
            _originalColor = _targetLight.color;
        }
        
        _targetIntensity = _originalIntensity;
        _targetColor = _originalColor;
        
        ScheduleNextFlicker();
    }
    
    private void Update()
    {
        if (_targetLight == null) return;
        
        if (Time.time >= _nextFlickerTime && !_isFlickering)
        {
            StartFlicker();
        }
        
        if (_isFlickering)
        {
            UpdateFlicker();
        }
        else if (_enableSmoothTransition)
        {
            SmoothReturnToOriginal();
        }
    }
    
    private void StartFlicker()
    {
        _isFlickering = true;
        _flickerStartTime = Time.time;
        _flickerDuration = Random.Range(0.05f, 0.3f);
        
        _targetIntensity = Random.Range(_minIntensity, _maxIntensity);
        
        if (_randomizeColor)
        {
            _targetColor = Color.Lerp(_flickerColorMin, _flickerColorMax, Random.value);
        }
        else
        {
            _targetColor = _originalColor;
        }
    }
    
    private void UpdateFlicker()
    {
        float flickerProgress = (Time.time - _flickerStartTime) / _flickerDuration;
        
        if (flickerProgress >= 1f)
        {
            _isFlickering = false;
            ScheduleNextFlicker();
            return;
        }
        
        float curveValue = _flickerCurve.Evaluate(flickerProgress);
        float noise = Mathf.PerlinNoise(Time.time * _flickerSpeed, 0f);
        float flickerMultiplier = Mathf.Lerp(0.3f, 1f, noise);
        
        float currentIntensity = Mathf.Lerp(_originalIntensity, _targetIntensity * flickerMultiplier, curveValue);
        Color currentColor = Color.Lerp(_originalColor, _targetColor, curveValue);
        
        _targetLight.intensity = currentIntensity;
        _targetLight.color = currentColor;
    }
    
    private void SmoothReturnToOriginal()
    {
        _targetLight.intensity = Mathf.Lerp(_targetLight.intensity, _originalIntensity, Time.deltaTime * 5f);
        _targetLight.color = Color.Lerp(_targetLight.color, _originalColor, Time.deltaTime * 5f);
    }
    
    private void ScheduleNextFlicker()
    {
        float interval = Random.Range(_minFlickerInterval, _maxFlickerInterval);
        _nextFlickerTime = Time.time + interval;
    }
    
    public void SetFlickerEnabled(bool enabled)
    {
        if (!enabled)
        {
            _isFlickering = false;
            _targetLight.intensity = _originalIntensity;
            _targetLight.color = _originalColor;
        }
        
        this.enabled = enabled;
    }
    
    public void TriggerFlicker()
    {
        if (!_isFlickering)
        {
            _nextFlickerTime = Time.time;
        }
    }
    
    private void OnValidate()
    {
        _minFlickerInterval = Mathf.Max(0.01f, _minFlickerInterval);
        _maxFlickerInterval = Mathf.Max(_minFlickerInterval, _maxFlickerInterval);
        _minIntensity = Mathf.Max(0f, _minIntensity);
        _maxIntensity = Mathf.Max(_minIntensity, _maxIntensity);
        _flickerSpeed = Mathf.Max(0.1f, _flickerSpeed);
    }
}