// Prompt: lens flare from sun
// Type: general

using UnityEngine;

public class SunLensFlare : MonoBehaviour
{
    [Header("Lens Flare Settings")]
    [SerializeField] private LensFlare _lensFlare;
    [SerializeField] private Light _sunLight;
    [SerializeField] private Camera _playerCamera;
    
    [Header("Visibility Control")]
    [SerializeField] private bool _checkVisibility = true;
    [SerializeField] private LayerMask _occlusionLayers = -1;
    [SerializeField] private float _maxDistance = 1000f;
    [SerializeField] private float _fadeSpeed = 5f;
    
    [Header("Dynamic Brightness")]
    [SerializeField] private bool _adjustBrightness = true;
    [SerializeField] private float _maxBrightness = 1f;
    [SerializeField] private float _minBrightness = 0.1f;
    [SerializeField] private AnimationCurve _brightnessCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Color Settings")]
    [SerializeField] private bool _matchSunColor = true;
    [SerializeField] private Color _customColor = Color.white;
    [SerializeField] private Gradient _timeOfDayGradient;
    [SerializeField] private bool _useTimeOfDay = false;
    [SerializeField] private float _timeOfDaySpeed = 0.1f;
    
    private float _targetBrightness;
    private float _currentBrightness;
    private bool _isVisible;
    private Vector3 _sunDirection;
    private float _timeOfDay;
    
    void Start()
    {
        InitializeComponents();
        SetupLensFlare();
    }
    
    void Update()
    {
        if (_lensFlare == null) return;
        
        UpdateSunDirection();
        CheckVisibility();
        UpdateBrightness();
        UpdateColor();
        ApplyLensFlareSettings();
    }
    
    void InitializeComponents()
    {
        if (_lensFlare == null)
            _lensFlare = GetComponent<LensFlare>();
        
        if (_lensFlare == null)
            _lensFlare = gameObject.AddComponent<LensFlare>();
        
        if (_sunLight == null)
            _sunLight = GetComponent<Light>();
        
        if (_playerCamera == null)
            _playerCamera = Camera.main;
        
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
    }
    
    void SetupLensFlare()
    {
        if (_lensFlare == null) return;
        
        _lensFlare.brightness = 0f;
        _currentBrightness = 0f;
        _targetBrightness = 0f;
        
        if (_timeOfDayGradient == null)
        {
            _timeOfDayGradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[3];
            colorKeys[0] = new GradientColorKey(new Color(1f, 0.5f, 0.2f), 0f);
            colorKeys[1] = new GradientColorKey(Color.white, 0.5f);
            colorKeys[2] = new GradientColorKey(new Color(1f, 0.7f, 0.4f), 1f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            _timeOfDayGradient.SetKeys(colorKeys, alphaKeys);
        }
    }
    
    void UpdateSunDirection()
    {
        if (_playerCamera == null) return;
        
        _sunDirection = (transform.position - _playerCamera.transform.position).normalized;
    }
    
    void CheckVisibility()
    {
        if (!_checkVisibility || _playerCamera == null)
        {
            _isVisible = true;
            _targetBrightness = _maxBrightness;
            return;
        }
        
        Vector3 cameraPosition = _playerCamera.transform.position;
        Vector3 sunPosition = transform.position;
        Vector3 direction = (sunPosition - cameraPosition).normalized;
        float distance = Vector3.Distance(cameraPosition, sunPosition);
        
        if (distance > _maxDistance)
        {
            _isVisible = false;
            _targetBrightness = 0f;
            return;
        }
        
        Vector3 cameraForward = _playerCamera.transform.forward;
        float dotProduct = Vector3.Dot(cameraForward, direction);
        
        if (dotProduct <= 0f)
        {
            _isVisible = false;
            _targetBrightness = 0f;
            return;
        }
        
        RaycastHit hit;
        if (Physics.Raycast(cameraPosition, direction, out hit, distance, _occlusionLayers))
        {
            if (hit.collider.transform != transform)
            {
                _isVisible = false;
                _targetBrightness = 0f;
                return;
            }
        }
        
        _isVisible = true;
        
        if (_adjustBrightness)
        {
            float normalizedDot = Mathf.Clamp01(dotProduct);
            float curveValue = _brightnessCurve.Evaluate(normalizedDot);
            _targetBrightness = Mathf.Lerp(_minBrightness, _maxBrightness, curveValue);
        }
        else
        {
            _targetBrightness = _maxBrightness;
        }
    }
    
    void UpdateBrightness()
    {
        _currentBrightness = Mathf.Lerp(_currentBrightness, _targetBrightness, Time.deltaTime * _fadeSpeed);
    }
    
    void UpdateColor()
    {
        if (_lensFlare == null) return;
        
        Color targetColor = Color.white;
        
        if (_useTimeOfDay)
        {
            _timeOfDay += Time.deltaTime * _timeOfDaySpeed;
            if (_timeOfDay > 1f) _timeOfDay = 0f;
            targetColor = _timeOfDayGradient.Evaluate(_timeOfDay);
        }
        else if (_matchSunColor && _sunLight != null)
        {
            targetColor = _sunLight.color;
        }
        else
        {
            targetColor = _customColor;
        }
        
        _lensFlare.color = targetColor;
    }
    
    void ApplyLensFlareSettings()
    {
        if (_lensFlare == null) return;
        
        _lensFlare.brightness = _currentBrightness;
        
        if (_sunLight != null)
        {
            _lensFlare.enabled = _sunLight.enabled && _currentBrightness > 0.01f;
        }
        else
        {
            _lensFlare.enabled = _currentBrightness > 0.01f;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (_playerCamera == null) return;
        
        Gizmos.color = _isVisible ? Color.green : Color.red;
        Gizmos.DrawLine(_playerCamera.transform.position, transform.position);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 5f);
    }
    
    public void SetBrightness(float brightness)
    {
        _maxBrightness = Mathf.Clamp01(brightness);
    }
    
    public void SetColor(Color color)
    {
        _customColor = color;
        _matchSunColor = false;
    }
    
    public void SetTimeOfDay(float normalizedTime)
    {
        _timeOfDay = Mathf.Clamp01(normalizedTime);
    }
    
    public bool IsVisible()
    {
        return _isVisible;
    }
    
    public float GetCurrentBrightness()
    {
        return _currentBrightness;
    }
}