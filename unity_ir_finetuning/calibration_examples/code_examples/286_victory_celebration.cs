// Prompt: victory celebration
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class VictoryCelebration : MonoBehaviour
{
    [Header("Victory Trigger")]
    [SerializeField] private bool _triggerOnStart = false;
    [SerializeField] private KeyCode _testKey = KeyCode.V;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem[] _confettiSystems;
    [SerializeField] private ParticleSystem[] _fireworkSystems;
    [SerializeField] private Light[] _celebrationLights;
    [SerializeField] private Color _lightColor = Color.yellow;
    [SerializeField] private float _lightIntensity = 2f;
    [SerializeField] private float _lightFlashSpeed = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _victoryMusic;
    [SerializeField] private AudioClip[] _cheerSounds;
    [SerializeField] private float _musicVolume = 0.8f;
    [SerializeField] private float _cheerVolume = 1f;
    
    [Header("Animation")]
    [SerializeField] private Animator[] _celebrationAnimators;
    [SerializeField] private string _victoryTrigger = "Victory";
    [SerializeField] private Transform[] _bouncingObjects;
    [SerializeField] private float _bounceHeight = 2f;
    [SerializeField] private float _bounceSpeed = 3f;
    
    [Header("Camera Effects")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private bool _enableCameraShake = true;
    [SerializeField] private float _shakeIntensity = 0.5f;
    [SerializeField] private float _shakeDuration = 2f;
    
    [Header("UI Elements")]
    [SerializeField] private GameObject _victoryPanel;
    [SerializeField] private CanvasGroup _victoryCanvasGroup;
    [SerializeField] private float _uiFadeSpeed = 2f;
    
    [Header("Timing")]
    [SerializeField] private float _celebrationDuration = 10f;
    [SerializeField] private float _delayBeforeStart = 0.5f;
    
    [Header("Events")]
    public UnityEvent OnVictoryStart;
    public UnityEvent OnVictoryEnd;
    
    private bool _isActive = false;
    private Vector3[] _originalPositions;
    private Color[] _originalLightColors;
    private float[] _originalLightIntensities;
    private Vector3 _originalCameraPosition;
    private Coroutine _celebrationCoroutine;
    
    private void Start()
    {
        InitializeComponents();
        
        if (_triggerOnStart)
        {
            StartCelebration();
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(_testKey))
        {
            StartCelebration();
        }
    }
    
    private void InitializeComponents()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        StoreOriginalValues();
        
        if (_victoryPanel != null)
            _victoryPanel.SetActive(false);
            
        if (_victoryCanvasGroup != null)
            _victoryCanvasGroup.alpha = 0f;
    }
    
    private void StoreOriginalValues()
    {
        if (_bouncingObjects != null && _bouncingObjects.Length > 0)
        {
            _originalPositions = new Vector3[_bouncingObjects.Length];
            for (int i = 0; i < _bouncingObjects.Length; i++)
            {
                if (_bouncingObjects[i] != null)
                    _originalPositions[i] = _bouncingObjects[i].position;
            }
        }
        
        if (_celebrationLights != null && _celebrationLights.Length > 0)
        {
            _originalLightColors = new Color[_celebrationLights.Length];
            _originalLightIntensities = new float[_celebrationLights.Length];
            
            for (int i = 0; i < _celebrationLights.Length; i++)
            {
                if (_celebrationLights[i] != null)
                {
                    _originalLightColors[i] = _celebrationLights[i].color;
                    _originalLightIntensities[i] = _celebrationLights[i].intensity;
                }
            }
        }
        
        if (_mainCamera != null)
            _originalCameraPosition = _mainCamera.transform.position;
    }
    
    public void StartCelebration()
    {
        if (_isActive) return;
        
        _isActive = true;
        
        if (_celebrationCoroutine != null)
            StopCoroutine(_celebrationCoroutine);
            
        _celebrationCoroutine = StartCoroutine(CelebrationSequence());
    }
    
    public void StopCelebration()
    {
        if (!_isActive) return;
        
        if (_celebrationCoroutine != null)
        {
            StopCoroutine(_celebrationCoroutine);
            _celebrationCoroutine = null;
        }
        
        StartCoroutine(EndCelebration());
    }
    
    private IEnumerator CelebrationSequence()
    {
        OnVictoryStart?.Invoke();
        
        yield return new WaitForSeconds(_delayBeforeStart);
        
        StartParticleEffects();
        StartAudioEffects();
        StartAnimations();
        StartLightEffects();
        StartUIEffects();
        
        if (_enableCameraShake && _mainCamera != null)
            StartCoroutine(CameraShake());
            
        StartCoroutine(BouncingAnimation());
        
        yield return new WaitForSeconds(_celebrationDuration);
        
        yield return StartCoroutine(EndCelebration());
    }
    
    private void StartParticleEffects()
    {
        if (_confettiSystems != null)
        {
            foreach (var system in _confettiSystems)
            {
                if (system != null)
                    system.Play();
            }
        }
        
        if (_fireworkSystems != null)
        {
            foreach (var system in _fireworkSystems)
            {
                if (system != null)
                    system.Play();
            }
        }
    }
    
    private void StartAudioEffects()
    {
        if (_audioSource != null)
        {
            if (_victoryMusic != null)
            {
                _audioSource.clip = _victoryMusic;
                _audioSource.volume = _musicVolume;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            
            if (_cheerSounds != null && _cheerSounds.Length > 0)
            {
                StartCoroutine(PlayRandomCheers());
            }
        }
    }
    
    private IEnumerator PlayRandomCheers()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(Random.Range(1f, 3f));
            
            if (_cheerSounds.Length > 0 && _audioSource != null)
            {
                AudioClip randomCheer = _cheerSounds[Random.Range(0, _cheerSounds.Length)];
                if (randomCheer != null)
                {
                    _audioSource.PlayOneShot(randomCheer, _cheerVolume);
                }
            }
        }
    }
    
    private void StartAnimations()
    {
        if (_celebrationAnimators != null)
        {
            foreach (var animator in _celebrationAnimators)
            {
                if (animator != null)
                    animator.SetTrigger(_victoryTrigger);
            }
        }
    }
    
    private void StartLightEffects()
    {
        if (_celebrationLights != null)
        {
            StartCoroutine(FlashLights());
        }
    }
    
    private IEnumerator FlashLights()
    {
        while (_isActive)
        {
            foreach (var light in _celebrationLights)
            {
                if (light != null)
                {
                    light.color = _lightColor;
                    light.intensity = _lightIntensity * (0.5f + 0.5f * Mathf.Sin(Time.time * _lightFlashSpeed));
                }
            }
            yield return null;
        }
    }
    
    private void StartUIEffects()
    {
        if (_victoryPanel != null)
            _victoryPanel.SetActive(true);
            
        if (_victoryCanvasGroup != null)
            StartCoroutine(FadeInUI());
    }
    
    private IEnumerator FadeInUI()
    {
        float elapsedTime = 0f;
        float startAlpha = _victoryCanvasGroup.alpha;
        
        while (elapsedTime < 1f / _uiFadeSpeed)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime * _uiFadeSpeed;
            _victoryCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
            yield return null;
        }
        
        _victoryCanvasGroup.alpha = 1f;
    }
    
    private IEnumerator CameraShake()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _shakeDuration && _isActive)
        {
            Vector3 randomOffset = Random.insideUnitSphere * _shakeIntensity;
            randomOffset.z = 0f;
            
            _mainCamera.transform.position = _originalCameraPosition + randomOffset;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        _mainCamera.transform.position = _originalCameraPosition;
    }
    
    private IEnumerator BouncingAnimation()
    {
        if (_bouncingObjects == null || _originalPositions == null) yield break;
        
        while (_isActive)
        {
            for (int i = 0; i < _bouncingObjects.Length; i++)
            {
                if (_bouncingObjects[i] != null && i < _originalPositions.Length)
                {
                    float bounce = Mathf.Sin(Time.time * _bounceSpeed + i * 0.5f) * _bounceHeight;
                    Vector3 newPosition = _originalPositions[i];
                    newPosition.y += bounce;
                    _bouncingObjects[i].position = newPosition;
                }
            }
            yield return null;
        }
    }
    
    private IEnumerator EndCelebration()
    {
        _isActive = false;
        
        StopParticleEffects();
        
        if (_audioSource != null)
        {
            float startVolume = _audioSource.volume;
            float elapsedTime = 0f;
            float fadeDuration = 2f;
            
            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, 0f, elapsedTime / fadeDuration);
                yield return null;
            }
            
            _audioSource.Stop();
            _audioSource.volume = startVolume;
        }
        
        if (_victoryCanvasGroup != null)
            yield return StartCoroutine(FadeOutUI());
            
        if (_victoryPanel != null)
            _victoryPanel.SetActive(false);
        
        RestoreOriginalValues();
        
        OnVictoryEnd?.Invoke();
    }
    
    private void StopParticleEffects()
    {
        if (_confettiSystems != null)
        {
            foreach (var system in _confettiSystems)
            {
                if (system != null)
                    system.Stop();
            }
        }
        
        if (_fireworkSystems != null)
        {
            foreach (var system in _fireworkSystems)
            {
                if (system != null)
                    system.Stop();
            }
        }
    }
    
    private IEnumerator FadeOutUI()
    {
        float elapsedTime = 0f;
        float startAlpha = _victoryCanvasGroup.alpha;
        
        while (elapsedTime < 1f / _uiFadeSpeed)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime * _uiFadeSpeed;
            _victoryCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }
        
        _victoryCanvasGroup.alpha = 0f;
    }
    
    private void RestoreOriginalValues()
    {
        if (_bouncingObjects != null && _originalPositions != null)
        {
            for (int i = 0; i < _bouncingObjects.Length && i < _originalPositions.Length; i++)
            {
                if (_bouncingObjects[i] != null)
                    _bouncingObjects[i].position = _originalPositions[i];
            }
        }
        
        if (_celebrationLights != null && _originalLightColors != null && _originalLightIntensities != null)
        {
            for (int i = 0; i < _celebrationLights.Length && i < _originalLightColors.Length; i++)
            {
                if (_celebrationLights[i] != null)
                {
                    _celebrationLights[i].color = _originalLightColors[i];
                    _celebrationLights[i].intensity = _originalLightIntensities[i];
                }
            }
        }
        
        if (_mainCamera != null)
            _mainCamera.transform.position = _originalCameraPosition;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCelebration();
        }
    }
    
    private void OnDestroy()
    {
        if (_celebrationCoroutine != null)
        {
            StopCoroutine(_celebrationCoroutine);
        }
    }
}