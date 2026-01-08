// Prompt: object that gradually transforms over 30 seconds: smoothly rotates 360 degrees, scales from 1x to 2x and back, changes color through the rainbow spectrum, moves in a figure-8 pattern, and plays a continuous musical note that shifts pitch based on transformation phase
// Type: combat

using UnityEngine;

public class TransformingObject : MonoBehaviour
{
    [Header("Transformation Settings")]
    [SerializeField] private float _transformationDuration = 30f;
    [SerializeField] private bool _loopTransformation = true;
    
    [Header("Movement")]
    [SerializeField] private float _figure8Width = 5f;
    [SerializeField] private float _figure8Height = 3f;
    
    [Header("Audio")]
    [SerializeField] private float _basePitch = 1f;
    [SerializeField] private float _pitchRange = 2f;
    [SerializeField] private float _baseFrequency = 440f;
    
    private Vector3 _startPosition;
    private Vector3 _startScale;
    private Quaternion _startRotation;
    private Color _startColor;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private float _currentTime;
    private bool _isTransforming;
    
    private void Start()
    {
        _startPosition = transform.position;
        _startScale = transform.localScale;
        _startRotation = transform.rotation;
        
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
        {
            _renderer = GetComponentInChildren<Renderer>();
        }
        
        if (_renderer != null)
        {
            _startColor = _renderer.material.color;
        }
        
        SetupAudio();
        StartTransformation();
    }
    
    private void SetupAudio()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = GenerateToneClip(_baseFrequency, 1f);
        _audioSource.loop = true;
        _audioSource.volume = 0.3f;
        _audioSource.pitch = _basePitch;
        _audioSource.Play();
    }
    
    private AudioClip GenerateToneClip(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * 0.5f;
        }
        
        AudioClip clip = AudioClip.Create("GeneratedTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    private void Update()
    {
        if (!_isTransforming) return;
        
        _currentTime += Time.deltaTime;
        float normalizedTime = _currentTime / _transformationDuration;
        
        if (normalizedTime >= 1f)
        {
            if (_loopTransformation)
            {
                _currentTime = 0f;
                normalizedTime = 0f;
            }
            else
            {
                normalizedTime = 1f;
                _isTransforming = false;
            }
        }
        
        ApplyTransformations(normalizedTime);
    }
    
    private void ApplyTransformations(float t)
    {
        // Rotation - 360 degrees over duration
        float rotationAngle = t * 360f;
        transform.rotation = _startRotation * Quaternion.Euler(0, rotationAngle, 0);
        
        // Scale - 1x to 2x and back (sine wave)
        float scaleMultiplier = 1f + Mathf.Sin(t * Mathf.PI);
        transform.localScale = _startScale * scaleMultiplier;
        
        // Color - rainbow spectrum
        Color rainbowColor = GetRainbowColor(t);
        if (_renderer != null)
        {
            _renderer.material.color = rainbowColor;
        }
        
        // Movement - figure-8 pattern
        Vector3 figure8Position = GetFigure8Position(t);
        transform.position = _startPosition + figure8Position;
        
        // Audio pitch based on transformation phase
        if (_audioSource != null)
        {
            float pitchMultiplier = _basePitch + (Mathf.Sin(t * 2 * Mathf.PI) * _pitchRange * 0.5f);
            _audioSource.pitch = Mathf.Clamp(pitchMultiplier, 0.1f, 3f);
        }
    }
    
    private Color GetRainbowColor(float t)
    {
        float hue = t;
        return Color.HSVToRGB(hue, 1f, 1f);
    }
    
    private Vector3 GetFigure8Position(float t)
    {
        float angle = t * 2 * Mathf.PI;
        float x = _figure8Width * Mathf.Sin(angle);
        float z = _figure8Height * Mathf.Sin(2 * angle);
        return new Vector3(x, 0, z);
    }
    
    public void StartTransformation()
    {
        _isTransforming = true;
        _currentTime = 0f;
    }
    
    public void StopTransformation()
    {
        _isTransforming = false;
    }
    
    public void ResetToStart()
    {
        _currentTime = 0f;
        transform.position = _startPosition;
        transform.localScale = _startScale;
        transform.rotation = _startRotation;
        
        if (_renderer != null)
        {
            _renderer.material.color = _startColor;
        }
        
        if (_audioSource != null)
        {
            _audioSource.pitch = _basePitch;
        }
    }
    
    private void OnDestroy()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    private void OnValidate()
    {
        if (_transformationDuration <= 0f)
        {
            _transformationDuration = 30f;
        }
        
        if (_figure8Width < 0f)
        {
            _figure8Width = 0f;
        }
        
        if (_figure8Height < 0f)
        {
            _figure8Height = 0f;
        }
    }
}