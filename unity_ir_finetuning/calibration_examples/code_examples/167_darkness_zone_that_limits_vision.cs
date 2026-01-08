// Prompt: darkness zone that limits vision
// Type: general

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class DarknessZone : MonoBehaviour
{
    [Header("Darkness Settings")]
    [SerializeField] private float _darknessIntensity = 0.8f;
    [SerializeField] private float _transitionSpeed = 2f;
    [SerializeField] private float _visionRadius = 5f;
    [SerializeField] private bool _useGlobalVolumeProfile = true;
    
    [Header("Light Settings")]
    [SerializeField] private Light _playerLight;
    [SerializeField] private float _originalLightIntensity = 1f;
    [SerializeField] private float _darknessLightIntensity = 0.2f;
    
    [Header("Fog Settings")]
    [SerializeField] private bool _enableFog = true;
    [SerializeField] private Color _fogColor = Color.black;
    [SerializeField] private float _fogDensity = 0.1f;
    [SerializeField] private FogMode _fogMode = FogMode.ExponentialSquared;
    
    [Header("Post Processing")]
    [SerializeField] private Volume _darknessVolume;
    [SerializeField] private AnimationCurve _darknessTransition = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    private bool _playerInZone = false;
    private float _currentDarknessLevel = 0f;
    private Camera _playerCamera;
    private GameObject _playerObject;
    
    // Original environment settings
    private float _originalAmbientIntensity;
    private Color _originalAmbientColor;
    private bool _originalFogEnabled;
    private Color _originalFogColor;
    private float _originalFogDensity;
    private FogMode _originalFogMode;
    private float _originalCameraFarClip;
    
    // Vision limitation components
    private GameObject _visionMask;
    private SphereCollider _visionCollider;
    
    private void Start()
    {
        InitializeDarknessZone();
        StoreOriginalSettings();
        CreateVisionMask();
    }
    
    private void Update()
    {
        UpdateDarknessEffect();
        UpdateVisionLimitation();
    }
    
    private void InitializeDarknessZone()
    {
        if (GetComponent<Collider>() == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
        }
        
        _playerCamera = Camera.main;
        if (_playerCamera == null)
        {
            _playerCamera = FindObjectOfType<Camera>();
        }
        
        if (_darknessVolume == null)
        {
            _darknessVolume = FindObjectOfType<Volume>();
        }
    }
    
    private void StoreOriginalSettings()
    {
        _originalAmbientIntensity = RenderSettings.ambientIntensity;
        _originalAmbientColor = RenderSettings.ambientLight;
        _originalFogEnabled = RenderSettings.fog;
        _originalFogColor = RenderSettings.fogColor;
        _originalFogDensity = RenderSettings.fogDensity;
        _originalFogMode = RenderSettings.fogMode;
        
        if (_playerCamera != null)
        {
            _originalCameraFarClip = _playerCamera.farClipPlane;
        }
        
        if (_playerLight == null)
        {
            GameObject lightObject = GameObject.FindWithTag("Player");
            if (lightObject != null)
            {
                _playerLight = lightObject.GetComponentInChildren<Light>();
            }
        }
        
        if (_playerLight != null)
        {
            _originalLightIntensity = _playerLight.intensity;
        }
    }
    
    private void CreateVisionMask()
    {
        _visionMask = new GameObject("VisionMask");
        _visionMask.transform.SetParent(transform);
        _visionMask.SetActive(false);
        
        _visionCollider = _visionMask.AddComponent<SphereCollider>();
        _visionCollider.isTrigger = true;
        _visionCollider.radius = _visionRadius;
        
        Rigidbody rb = _visionMask.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }
    
    private void UpdateDarknessEffect()
    {
        float targetDarkness = _playerInZone ? _darknessIntensity : 0f;
        _currentDarknessLevel = Mathf.MoveTowards(_currentDarknessLevel, targetDarkness, _transitionSpeed * Time.deltaTime);
        
        float transitionValue = _darknessTransition.Evaluate(_currentDarknessLevel / _darknessIntensity);
        
        ApplyAmbientLighting(transitionValue);
        ApplyFogEffect(transitionValue);
        ApplyLightIntensity(transitionValue);
        ApplyCameraSettings(transitionValue);
        ApplyPostProcessing(transitionValue);
    }
    
    private void ApplyAmbientLighting(float intensity)
    {
        RenderSettings.ambientIntensity = Mathf.Lerp(_originalAmbientIntensity, _originalAmbientIntensity * (1f - intensity), intensity);
        RenderSettings.ambientLight = Color.Lerp(_originalAmbientColor, Color.black, intensity * 0.5f);
    }
    
    private void ApplyFogEffect(float intensity)
    {
        if (_enableFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(_originalFogColor, _fogColor, intensity);
            RenderSettings.fogDensity = Mathf.Lerp(_originalFogDensity, _fogDensity, intensity);
            RenderSettings.fogMode = _fogMode;
        }
    }
    
    private void ApplyLightIntensity(float intensity)
    {
        if (_playerLight != null)
        {
            _playerLight.intensity = Mathf.Lerp(_originalLightIntensity, _darknessLightIntensity, intensity);
        }
    }
    
    private void ApplyCameraSettings(float intensity)
    {
        if (_playerCamera != null)
        {
            float reducedFarClip = _originalCameraFarClip * (1f - intensity * 0.7f);
            _playerCamera.farClipPlane = Mathf.Max(reducedFarClip, 10f);
        }
    }
    
    private void ApplyPostProcessing(float intensity)
    {
        if (_darknessVolume != null)
        {
            _darknessVolume.weight = intensity;
        }
    }
    
    private void UpdateVisionLimitation()
    {
        if (_playerInZone && _playerObject != null && _visionMask != null)
        {
            _visionMask.transform.position = _playerObject.transform.position;
            _visionMask.SetActive(true);
        }
        else if (_visionMask != null)
        {
            _visionMask.SetActive(false);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInZone = true;
            _playerObject = other.gameObject;
            
            if (_playerCamera == null)
            {
                _playerCamera = other.GetComponentInChildren<Camera>();
            }
            
            if (_playerLight == null)
            {
                _playerLight = other.GetComponentInChildren<Light>();
                if (_playerLight != null)
                {
                    _originalLightIntensity = _playerLight.intensity;
                }
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInZone = false;
            _playerObject = null;
        }
    }
    
    private void OnDestroy()
    {
        RestoreOriginalSettings();
    }
    
    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            RestoreOriginalSettings();
        }
    }
    
    private void RestoreOriginalSettings()
    {
        RenderSettings.ambientIntensity = _originalAmbientIntensity;
        RenderSettings.ambientLight = _originalAmbientColor;
        RenderSettings.fog = _originalFogEnabled;
        RenderSettings.fogColor = _originalFogColor;
        RenderSettings.fogDensity = _originalFogDensity;
        RenderSettings.fogMode = _originalFogMode;
        
        if (_playerCamera != null)
        {
            _playerCamera.farClipPlane = _originalCameraFarClip;
        }
        
        if (_playerLight != null)
        {
            _playerLight.intensity = _originalLightIntensity;
        }
        
        if (_darknessVolume != null)
        {
            _darknessVolume.weight = 0f;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        if (_playerInZone && _playerObject != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_playerObject.transform.position, _visionRadius);
        }
    }
}