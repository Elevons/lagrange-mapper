// Prompt: visualizer that reacts to audio: object's scale increases proportionally to audio volume, color shifts based on audio frequency (low red, mid green, high blue), rotates based on audio waveform, spawns particles that move outward in sync with beat
// Type: movement

using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class AudioVisualizerSettings
{
    [Header("Scale Settings")]
    public float baseScale = 1f;
    public float scaleMultiplier = 2f;
    public float scaleSmoothing = 5f;
    
    [Header("Color Settings")]
    public float colorSensitivity = 1f;
    public float colorSmoothing = 3f;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 100f;
    public Vector3 rotationAxis = Vector3.up;
    
    [Header("Particle Settings")]
    public int maxParticles = 50;
    public float particleSpeed = 5f;
    public float particleLifetime = 2f;
    public float beatThreshold = 0.7f;
    public float beatCooldown = 0.2f;
}

public class AudioVisualizer : MonoBehaviour
{
    [Header("Audio Source")]
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Visualization Settings")]
    [SerializeField] private AudioVisualizerSettings _settings = new AudioVisualizerSettings();
    
    [Header("Components")]
    [SerializeField] private Renderer _renderer;
    [SerializeField] private ParticleSystem _particleSystem;
    
    [Header("Audio Analysis")]
    [SerializeField] private int _sampleSize = 1024;
    [SerializeField] private FFTWindow _fftWindow = FFTWindow.Blackman;
    
    private float[] _audioSamples;
    private float[] _frequencyBands;
    private float _currentVolume;
    private Color _targetColor;
    private Color _currentColor;
    private float _targetScale;
    private float _currentScale;
    private float _lastBeatTime;
    private Material _material;
    private Vector3 _baseScale;
    private float _rotationAccumulator;
    
    private void Start()
    {
        InitializeComponents();
        InitializeAudioAnalysis();
        InitializeVisualization();
    }
    
    private void InitializeComponents()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();
        
        if (_particleSystem == null)
            _particleSystem = GetComponent<ParticleSystem>();
        
        if (_audioSource == null)
        {
            Debug.LogError("AudioSource component required for AudioVisualizer");
            enabled = false;
            return;
        }
        
        if (_renderer != null)
        {
            _material = _renderer.material;
            _baseScale = transform.localScale;
        }
    }
    
    private void InitializeAudioAnalysis()
    {
        _audioSamples = new float[_sampleSize];
        _frequencyBands = new float[3]; // Low, Mid, High
        _currentScale = _settings.baseScale;
        _targetScale = _settings.baseScale;
        _currentColor = Color.white;
        _targetColor = Color.white;
    }
    
    private void InitializeVisualization()
    {
        if (_particleSystem != null)
        {
            var main = _particleSystem.main;
            main.maxParticles = _settings.maxParticles;
            main.startLifetime = _settings.particleLifetime;
            main.startSpeed = _settings.particleSpeed;
            
            var emission = _particleSystem.emission;
            emission.enabled = false;
            
            var shape = _particleSystem.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;
        }
    }
    
    private void Update()
    {
        if (_audioSource == null || !_audioSource.isPlaying)
            return;
        
        AnalyzeAudio();
        UpdateScale();
        UpdateColor();
        UpdateRotation();
        CheckForBeat();
    }
    
    private void AnalyzeAudio()
    {
        _audioSource.GetSpectrumData(_audioSamples, 0, _fftWindow);
        
        // Calculate volume
        float sum = 0f;
        for (int i = 0; i < _audioSamples.Length; i++)
        {
            sum += _audioSamples[i] * _audioSamples[i];
        }
        _currentVolume = Mathf.Sqrt(sum / _audioSamples.Length);
        
        // Calculate frequency bands
        CalculateFrequencyBands();
    }
    
    private void CalculateFrequencyBands()
    {
        int count = 0;
        
        // Low frequencies (0-170 Hz approximately)
        for (int i = 0; i < 8; i++)
        {
            _frequencyBands[0] += _audioSamples[i];
            count++;
        }
        if (count > 0) _frequencyBands[0] /= count;
        
        count = 0;
        // Mid frequencies (170-4000 Hz approximately)
        for (int i = 8; i < 128; i++)
        {
            _frequencyBands[1] += _audioSamples[i];
            count++;
        }
        if (count > 0) _frequencyBands[1] /= count;
        
        count = 0;
        // High frequencies (4000+ Hz approximately)
        for (int i = 128; i < _audioSamples.Length / 4; i++)
        {
            _frequencyBands[2] += _audioSamples[i];
            count++;
        }
        if (count > 0) _frequencyBands[2] /= count;
    }
    
    private void UpdateScale()
    {
        _targetScale = _settings.baseScale + (_currentVolume * _settings.scaleMultiplier);
        _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * _settings.scaleSmoothing);
        
        if (_renderer != null)
        {
            transform.localScale = _baseScale * _currentScale;
        }
    }
    
    private void UpdateColor()
    {
        float red = _frequencyBands[0] * _settings.colorSensitivity;
        float green = _frequencyBands[1] * _settings.colorSensitivity;
        float blue = _frequencyBands[2] * _settings.colorSensitivity;
        
        _targetColor = new Color(red, green, blue, 1f);
        _currentColor = Color.Lerp(_currentColor, _targetColor, Time.deltaTime * _settings.colorSmoothing);
        
        if (_material != null)
        {
            _material.color = _currentColor;
        }
    }
    
    private void UpdateRotation()
    {
        float waveformIntensity = 0f;
        for (int i = 0; i < _audioSamples.Length; i += 10)
        {
            waveformIntensity += Mathf.Abs(_audioSamples[i]);
        }
        waveformIntensity /= (_audioSamples.Length / 10);
        
        _rotationAccumulator += waveformIntensity * _settings.rotationSpeed * Time.deltaTime;
        transform.rotation = Quaternion.AngleAxis(_rotationAccumulator, _settings.rotationAxis);
    }
    
    private void CheckForBeat()
    {
        if (_currentVolume > _settings.beatThreshold && 
            Time.time - _lastBeatTime > _settings.beatCooldown)
        {
            TriggerBeatEffect();
            _lastBeatTime = Time.time;
        }
    }
    
    private void TriggerBeatEffect()
    {
        if (_particleSystem != null)
        {
            var emission = _particleSystem.emission;
            emission.Emit(10);
        }
    }
    
    private void OnValidate()
    {
        if (_settings.baseScale <= 0f)
            _settings.baseScale = 1f;
        
        if (_settings.scaleMultiplier < 0f)
            _settings.scaleMultiplier = 0f;
        
        if (_settings.beatThreshold < 0f)
            _settings.beatThreshold = 0f;
        
        if (_settings.beatThreshold > 1f)
            _settings.beatThreshold = 1f;
    }
    
    private void OnDestroy()
    {
        if (_material != null && Application.isPlaying)
        {
            Destroy(_material);
        }
    }
}