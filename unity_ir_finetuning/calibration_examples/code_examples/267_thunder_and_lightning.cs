// Prompt: thunder and lightning
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ThunderLightning : MonoBehaviour
{
    [Header("Lightning Settings")]
    [SerializeField] private Light _lightningLight;
    [SerializeField] private float _lightningIntensity = 8f;
    [SerializeField] private Color _lightningColor = Color.white;
    [SerializeField] private float _lightningDuration = 0.1f;
    [SerializeField] private int _minFlashes = 1;
    [SerializeField] private int _maxFlashes = 3;
    [SerializeField] private float _flashInterval = 0.05f;
    
    [Header("Thunder Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _thunderSounds;
    [SerializeField] private float _minThunderDelay = 0.5f;
    [SerializeField] private float _maxThunderDelay = 3f;
    [SerializeField] private float _thunderVolume = 1f;
    
    [Header("Storm Settings")]
    [SerializeField] private bool _autoStorm = true;
    [SerializeField] private float _minStormInterval = 5f;
    [SerializeField] private float _maxStormInterval = 15f;
    [SerializeField] private bool _randomIntensity = true;
    
    [Header("Environment Effects")]
    [SerializeField] private ParticleSystem _rainParticles;
    [SerializeField] private Material _skyboxMaterial;
    [SerializeField] private string _skyboxTintProperty = "_Tint";
    [SerializeField] private Color _stormSkyTint = new Color(0.3f, 0.3f, 0.4f, 1f);
    
    [Header("Events")]
    public UnityEvent OnLightningStrike;
    public UnityEvent OnThunderRoll;
    public UnityEvent OnStormStart;
    public UnityEvent OnStormEnd;
    
    private float _originalLightIntensity;
    private Color _originalLightColor;
    private Color _originalSkyTint;
    private bool _isStorming = false;
    private Coroutine _stormCoroutine;
    private Camera _mainCamera;
    
    private void Start()
    {
        _mainCamera = Camera.main;
        
        if (_lightningLight == null)
        {
            _lightningLight = GetComponent<Light>();
            if (_lightningLight == null)
            {
                GameObject lightGO = new GameObject("Lightning Light");
                lightGO.transform.SetParent(transform);
                _lightningLight = lightGO.AddComponent<Light>();
                _lightningLight.type = LightType.Directional;
            }
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _originalLightIntensity = _lightningLight.intensity;
        _originalLightColor = _lightningLight.color;
        _lightningLight.intensity = 0f;
        
        if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(_skyboxTintProperty))
        {
            _originalSkyTint = _skyboxMaterial.GetColor(_skyboxTintProperty);
        }
        
        if (_autoStorm)
        {
            _stormCoroutine = StartCoroutine(AutoStormRoutine());
        }
    }
    
    private void OnDestroy()
    {
        if (_stormCoroutine != null)
        {
            StopCoroutine(_stormCoroutine);
        }
        
        if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(_skyboxTintProperty))
        {
            _skyboxMaterial.SetColor(_skyboxTintProperty, _originalSkyTint);
        }
    }
    
    public void TriggerLightningStrike()
    {
        StartCoroutine(LightningStrikeRoutine());
    }
    
    public void StartStorm()
    {
        if (!_isStorming)
        {
            _isStorming = true;
            OnStormStart?.Invoke();
            
            if (_rainParticles != null && !_rainParticles.isPlaying)
            {
                _rainParticles.Play();
            }
            
            if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(_skyboxTintProperty))
            {
                StartCoroutine(ChangeSkyTint(_stormSkyTint, 2f));
            }
        }
    }
    
    public void EndStorm()
    {
        if (_isStorming)
        {
            _isStorming = false;
            OnStormEnd?.Invoke();
            
            if (_rainParticles != null && _rainParticles.isPlaying)
            {
                _rainParticles.Stop();
            }
            
            if (_skyboxMaterial != null && _skyboxMaterial.HasProperty(_skyboxTintProperty))
            {
                StartCoroutine(ChangeSkyTint(_originalSkyTint, 3f));
            }
        }
    }
    
    private IEnumerator AutoStormRoutine()
    {
        while (true)
        {
            float waitTime = Random.Range(_minStormInterval, _maxStormInterval);
            yield return new WaitForSeconds(waitTime);
            
            if (!_isStorming)
            {
                StartStorm();
                
                int lightningStrikes = Random.Range(3, 8);
                for (int i = 0; i < lightningStrikes; i++)
                {
                    yield return new WaitForSeconds(Random.Range(1f, 4f));
                    TriggerLightningStrike();
                }
                
                yield return new WaitForSeconds(Random.Range(5f, 10f));
                EndStorm();
            }
        }
    }
    
    private IEnumerator LightningStrikeRoutine()
    {
        OnLightningStrike?.Invoke();
        
        int flashCount = Random.Range(_minFlashes, _maxFlashes + 1);
        float intensity = _randomIntensity ? Random.Range(_lightningIntensity * 0.7f, _lightningIntensity * 1.3f) : _lightningIntensity;
        
        for (int i = 0; i < flashCount; i++)
        {
            _lightningLight.intensity = intensity;
            _lightningLight.color = _lightningColor;
            
            if (_mainCamera != null)
            {
                StartCoroutine(CameraShake(0.1f, 0.2f));
            }
            
            yield return new WaitForSeconds(_lightningDuration);
            
            _lightningLight.intensity = 0f;
            
            if (i < flashCount - 1)
            {
                yield return new WaitForSeconds(_flashInterval);
            }
        }
        
        float thunderDelay = Random.Range(_minThunderDelay, _maxThunderDelay);
        yield return new WaitForSeconds(thunderDelay);
        
        PlayThunder();
    }
    
    private void PlayThunder()
    {
        if (_thunderSounds != null && _thunderSounds.Length > 0 && _audioSource != null)
        {
            AudioClip thunderClip = _thunderSounds[Random.Range(0, _thunderSounds.Length)];
            _audioSource.clip = thunderClip;
            _audioSource.volume = _thunderVolume;
            _audioSource.Play();
            
            OnThunderRoll?.Invoke();
        }
    }
    
    private IEnumerator CameraShake(float duration, float magnitude)
    {
        if (_mainCamera == null) yield break;
        
        Vector3 originalPosition = _mainCamera.transform.localPosition;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            
            _mainCamera.transform.localPosition = new Vector3(originalPosition.x + x, originalPosition.y + y, originalPosition.z);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _mainCamera.transform.localPosition = originalPosition;
    }
    
    private IEnumerator ChangeSkyTint(Color targetColor, float duration)
    {
        if (_skyboxMaterial == null || !_skyboxMaterial.HasProperty(_skyboxTintProperty))
            yield break;
        
        Color startColor = _skyboxMaterial.GetColor(_skyboxTintProperty);
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            Color currentColor = Color.Lerp(startColor, targetColor, t);
            _skyboxMaterial.SetColor(_skyboxTintProperty, currentColor);
            yield return null;
        }
        
        _skyboxMaterial.SetColor(_skyboxTintProperty, targetColor);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            TriggerLightningStrike();
        }
        
        if (Input.GetKeyDown(KeyCode.K))
        {
            if (_isStorming)
                EndStorm();
            else
                StartStorm();
        }
    }
}