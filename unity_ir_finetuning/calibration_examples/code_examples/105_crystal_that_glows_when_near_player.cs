// Prompt: crystal that glows when near player
// Type: movement

using UnityEngine;

public class GlowingCrystal : MonoBehaviour
{
    [Header("Glow Settings")]
    [SerializeField] private float _glowDistance = 5f;
    [SerializeField] private float _maxGlowIntensity = 2f;
    [SerializeField] private float _minGlowIntensity = 0.1f;
    [SerializeField] private Color _glowColor = Color.cyan;
    [SerializeField] private float _glowSpeed = 2f;
    
    [Header("Pulse Settings")]
    [SerializeField] private bool _enablePulse = true;
    [SerializeField] private float _pulseSpeed = 1f;
    [SerializeField] private float _pulseIntensity = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _glowSound;
    [SerializeField] private float _audioVolume = 0.5f;
    
    private Light _crystalLight;
    private Renderer _crystalRenderer;
    private Material _crystalMaterial;
    private AudioSource _audioSource;
    private Transform _player;
    private bool _isGlowing = false;
    private float _baseIntensity;
    private Color _originalEmissionColor;
    private float _currentGlowIntensity;
    
    void Start()
    {
        SetupComponents();
        FindPlayer();
        InitializeMaterial();
    }
    
    void Update()
    {
        if (_player != null)
        {
            UpdateGlow();
        }
        else
        {
            FindPlayer();
        }
    }
    
    void SetupComponents()
    {
        _crystalLight = GetComponent<Light>();
        if (_crystalLight == null)
        {
            _crystalLight = gameObject.AddComponent<Light>();
            _crystalLight.type = LightType.Point;
            _crystalLight.color = _glowColor;
            _crystalLight.intensity = _minGlowIntensity;
            _crystalLight.range = _glowDistance * 1.5f;
        }
        
        _baseIntensity = _crystalLight.intensity;
        _currentGlowIntensity = _minGlowIntensity;
        
        _crystalRenderer = GetComponent<Renderer>();
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        _audioSource.clip = _glowSound;
        _audioSource.volume = _audioVolume;
        _audioSource.playOnAwake = false;
        _audioSource.loop = true;
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    void InitializeMaterial()
    {
        if (_crystalRenderer != null)
        {
            _crystalMaterial = _crystalRenderer.material;
            if (_crystalMaterial.HasProperty("_EmissionColor"))
            {
                _originalEmissionColor = _crystalMaterial.GetColor("_EmissionColor");
            }
        }
    }
    
    void UpdateGlow()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        float normalizedDistance = Mathf.Clamp01(distanceToPlayer / _glowDistance);
        float glowFactor = 1f - normalizedDistance;
        
        bool shouldGlow = distanceToPlayer <= _glowDistance;
        
        if (shouldGlow && !_isGlowing)
        {
            StartGlowing();
        }
        else if (!shouldGlow && _isGlowing)
        {
            StopGlowing();
        }
        
        if (shouldGlow)
        {
            UpdateGlowIntensity(glowFactor);
        }
    }
    
    void StartGlowing()
    {
        _isGlowing = true;
        
        if (_glowSound != null && _audioSource != null)
        {
            _audioSource.Play();
        }
    }
    
    void StopGlowing()
    {
        _isGlowing = false;
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        _currentGlowIntensity = Mathf.Lerp(_currentGlowIntensity, _minGlowIntensity, Time.deltaTime * _glowSpeed);
        UpdateLightAndMaterial();
    }
    
    void UpdateGlowIntensity(float glowFactor)
    {
        float targetIntensity = Mathf.Lerp(_minGlowIntensity, _maxGlowIntensity, glowFactor);
        
        if (_enablePulse)
        {
            float pulseOffset = Mathf.Sin(Time.time * _pulseSpeed) * _pulseIntensity;
            targetIntensity += pulseOffset * glowFactor;
        }
        
        _currentGlowIntensity = Mathf.Lerp(_currentGlowIntensity, targetIntensity, Time.deltaTime * _glowSpeed);
        UpdateLightAndMaterial();
    }
    
    void UpdateLightAndMaterial()
    {
        if (_crystalLight != null)
        {
            _crystalLight.intensity = _currentGlowIntensity;
            _crystalLight.color = _glowColor;
        }
        
        if (_crystalMaterial != null && _crystalMaterial.HasProperty("_EmissionColor"))
        {
            Color emissionColor = _glowColor * _currentGlowIntensity;
            _crystalMaterial.SetColor("_EmissionColor", emissionColor);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _glowColor;
        Gizmos.DrawWireSphere(transform.position, _glowDistance);
    }
    
    void OnDestroy()
    {
        if (_crystalMaterial != null)
        {
            if (_crystalMaterial.HasProperty("_EmissionColor"))
            {
                _crystalMaterial.SetColor("_EmissionColor", _originalEmissionColor);
            }
        }
    }
}