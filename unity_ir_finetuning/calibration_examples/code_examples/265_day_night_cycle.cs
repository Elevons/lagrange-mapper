// Prompt: day night cycle
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class DayNightCycle : MonoBehaviour
{
    [Header("Time Settings")]
    [SerializeField] private float _dayDurationInMinutes = 10f;
    [SerializeField] private float _currentTimeOfDay = 0.5f;
    [SerializeField] private bool _pauseTime = false;
    
    [Header("Sun Settings")]
    [SerializeField] private Light _sunLight;
    [SerializeField] private Transform _sunTransform;
    [SerializeField] private Gradient _sunColorGradient;
    [SerializeField] private AnimationCurve _sunIntensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float _maxSunIntensity = 1.5f;
    
    [Header("Moon Settings")]
    [SerializeField] private Light _moonLight;
    [SerializeField] private Transform _moonTransform;
    [SerializeField] private Color _moonColor = Color.blue;
    [SerializeField] private float _maxMoonIntensity = 0.3f;
    
    [Header("Sky Settings")]
    [SerializeField] private Material _skyboxMaterial;
    [SerializeField] private Gradient _fogColorGradient;
    [SerializeField] private AnimationCurve _fogDensityCurve = AnimationCurve.EaseInOut(0f, 0.01f, 1f, 0.01f);
    [SerializeField] private float _maxFogDensity = 0.05f;
    
    [Header("Ambient Settings")]
    [SerializeField] private Gradient _ambientColorGradient;
    [SerializeField] private AnimationCurve _ambientIntensityCurve = AnimationCurve.EaseInOut(0f, 0.3f, 1f, 1f);
    [SerializeField] private float _maxAmbientIntensity = 1f;
    
    [Header("Events")]
    public UnityEvent OnSunrise;
    public UnityEvent OnNoon;
    public UnityEvent OnSunset;
    public UnityEvent OnMidnight;
    
    private float _timeSpeed;
    private bool _hasSunriseTriggered = false;
    private bool _hasNoonTriggered = false;
    private bool _hasSunsetTriggered = false;
    private bool _hasMidnightTriggered = false;
    
    private void Start()
    {
        _timeSpeed = 1f / (_dayDurationInMinutes * 60f);
        
        if (_sunLight == null)
            _sunLight = FindObjectOfType<Light>();
            
        if (_sunTransform == null && _sunLight != null)
            _sunTransform = _sunLight.transform;
            
        if (_skyboxMaterial == null)
            _skyboxMaterial = RenderSettings.skybox;
            
        InitializeGradients();
        UpdateCycle();
    }
    
    private void Update()
    {
        if (!_pauseTime)
        {
            _currentTimeOfDay += Time.deltaTime * _timeSpeed;
            
            if (_currentTimeOfDay >= 1f)
            {
                _currentTimeOfDay = 0f;
                ResetEventTriggers();
            }
            
            UpdateCycle();
            CheckTimeEvents();
        }
    }
    
    private void UpdateCycle()
    {
        UpdateSun();
        UpdateMoon();
        UpdateSkybox();
        UpdateFog();
        UpdateAmbientLighting();
    }
    
    private void UpdateSun()
    {
        if (_sunLight == null) return;
        
        float sunAngle = _currentTimeOfDay * 360f - 90f;
        
        if (_sunTransform != null)
        {
            _sunTransform.rotation = Quaternion.Euler(sunAngle, 30f, 0f);
        }
        
        float sunIntensity = _sunIntensityCurve.Evaluate(_currentTimeOfDay) * _maxSunIntensity;
        _sunLight.intensity = sunIntensity;
        
        _sunLight.color = _sunColorGradient.Evaluate(_currentTimeOfDay);
        
        _sunLight.enabled = sunIntensity > 0.01f;
    }
    
    private void UpdateMoon()
    {
        if (_moonLight == null) return;
        
        float moonAngle = (_currentTimeOfDay + 0.5f) % 1f * 360f - 90f;
        
        if (_moonTransform != null)
        {
            _moonTransform.rotation = Quaternion.Euler(moonAngle, 30f, 0f);
        }
        
        float nightTime = Mathf.Abs(_currentTimeOfDay - 0.5f) * 2f;
        nightTime = 1f - nightTime;
        nightTime = Mathf.Clamp01(nightTime);
        
        float moonIntensity = nightTime * _maxMoonIntensity;
        _moonLight.intensity = moonIntensity;
        _moonLight.color = _moonColor;
        
        _moonLight.enabled = moonIntensity > 0.01f;
    }
    
    private void UpdateSkybox()
    {
        if (_skyboxMaterial == null) return;
        
        if (_skyboxMaterial.HasProperty("_Exposure"))
        {
            float exposure = _sunIntensityCurve.Evaluate(_currentTimeOfDay);
            _skyboxMaterial.SetFloat("_Exposure", exposure);
        }
        
        if (_skyboxMaterial.HasProperty("_Tint"))
        {
            Color tint = _sunColorGradient.Evaluate(_currentTimeOfDay);
            _skyboxMaterial.SetColor("_Tint", tint);
        }
    }
    
    private void UpdateFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogColor = _fogColorGradient.Evaluate(_currentTimeOfDay);
        RenderSettings.fogDensity = _fogDensityCurve.Evaluate(_currentTimeOfDay) * _maxFogDensity;
    }
    
    private void UpdateAmbientLighting()
    {
        RenderSettings.ambientLight = _ambientColorGradient.Evaluate(_currentTimeOfDay);
        RenderSettings.ambientIntensity = _ambientIntensityCurve.Evaluate(_currentTimeOfDay) * _maxAmbientIntensity;
    }
    
    private void CheckTimeEvents()
    {
        if (_currentTimeOfDay >= 0.25f && _currentTimeOfDay < 0.26f && !_hasSunriseTriggered)
        {
            OnSunrise?.Invoke();
            _hasSunriseTriggered = true;
        }
        else if (_currentTimeOfDay >= 0.5f && _currentTimeOfDay < 0.51f && !_hasNoonTriggered)
        {
            OnNoon?.Invoke();
            _hasNoonTriggered = true;
        }
        else if (_currentTimeOfDay >= 0.75f && _currentTimeOfDay < 0.76f && !_hasSunsetTriggered)
        {
            OnSunset?.Invoke();
            _hasSunsetTriggered = true;
        }
        else if (_currentTimeOfDay >= 0.99f && !_hasMidnightTriggered)
        {
            OnMidnight?.Invoke();
            _hasMidnightTriggered = true;
        }
    }
    
    private void ResetEventTriggers()
    {
        _hasSunriseTriggered = false;
        _hasNoonTriggered = false;
        _hasSunsetTriggered = false;
        _hasMidnightTriggered = false;
    }
    
    private void InitializeGradients()
    {
        if (_sunColorGradient.colorKeys.Length == 0)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[5];
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 0f);
            colorKeys[1] = new GradientColorKey(new Color(1f, 0.6f, 0.3f), 0.25f);
            colorKeys[2] = new GradientColorKey(Color.white, 0.5f);
            colorKeys[3] = new GradientColorKey(new Color(1f, 0.4f, 0.2f), 0.75f);
            colorKeys[4] = new GradientColorKey(new Color(0.2f, 0.2f, 0.4f), 1f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
            
            _sunColorGradient.SetKeys(colorKeys, alphaKeys);
        }
        
        if (_fogColorGradient.colorKeys.Length == 0)
        {
            _fogColorGradient = _sunColorGradient;
        }
        
        if (_ambientColorGradient.colorKeys.Length == 0)
        {
            _ambientColorGradient = _sunColorGradient;
        }
    }
    
    public void SetTimeOfDay(float time)
    {
        _currentTimeOfDay = Mathf.Clamp01(time);
        ResetEventTriggers();
        UpdateCycle();
    }
    
    public void PauseTime(bool pause)
    {
        _pauseTime = pause;
    }
    
    public void SetDayDuration(float minutes)
    {
        _dayDurationInMinutes = Mathf.Max(0.1f, minutes);
        _timeSpeed = 1f / (_dayDurationInMinutes * 60f);
    }
    
    public float GetTimeOfDay()
    {
        return _currentTimeOfDay;
    }
    
    public bool IsDay()
    {
        return _currentTimeOfDay > 0.25f && _currentTimeOfDay < 0.75f;
    }
    
    public bool IsNight()
    {
        return !IsDay();
    }
}