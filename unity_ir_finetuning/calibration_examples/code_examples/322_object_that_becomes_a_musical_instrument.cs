// Prompt: object that becomes a musical instrument - when you collide with it, it plays a note based on where you hit it (top plays high notes, bottom plays low notes), the object's color shifts to match the note's frequency, it vibrates with the sound, and leaves a trail of particles that fade out
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class MusicalInstrument : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private float _baseFrequency = 220f;
    [SerializeField] private float _frequencyRange = 880f;
    [SerializeField] private float _noteDuration = 1f;
    [SerializeField] private AnimationCurve _volumeCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Visual Settings")]
    [SerializeField] private Renderer _objectRenderer;
    [SerializeField] private Color _lowNoteColor = Color.red;
    [SerializeField] private Color _highNoteColor = Color.blue;
    [SerializeField] private float _colorTransitionSpeed = 2f;
    
    [Header("Vibration Settings")]
    [SerializeField] private float _vibrationIntensity = 0.1f;
    [SerializeField] private float _vibrationFrequency = 20f;
    [SerializeField] private AnimationCurve _vibrationCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    
    [Header("Particle Settings")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private int _particleCount = 50;
    [SerializeField] private float _particleLifetime = 2f;
    [SerializeField] private float _particleSpeed = 5f;
    [SerializeField] private Gradient _particleColorGradient;
    
    private Collider _collider;
    private Vector3 _originalPosition;
    private Color _originalColor;
    private Color _targetColor;
    private float _currentColorLerp = 0f;
    private bool _isPlaying = false;
    private Coroutine _playNoteCoroutine;
    
    private void Start()
    {
        InitializeComponents();
        SetupAudioSource();
        SetupParticleSystem();
        _originalPosition = transform.position;
        _originalColor = _objectRenderer.material.color;
        _targetColor = _originalColor;
    }
    
    private void InitializeComponents()
    {
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
        }
        
        if (_objectRenderer == null)
            _objectRenderer = GetComponent<Renderer>();
            
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_particleSystem == null)
        {
            GameObject particleObj = new GameObject("ParticleSystem");
            particleObj.transform.SetParent(transform);
            particleObj.transform.localPosition = Vector3.zero;
            _particleSystem = particleObj.AddComponent<ParticleSystem>();
        }
    }
    
    private void SetupAudioSource()
    {
        _audioSource.clip = null;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        _audioSource.volume = 0.5f;
    }
    
    private void SetupParticleSystem()
    {
        var main = _particleSystem.main;
        main.startLifetime = _particleLifetime;
        main.startSpeed = _particleSpeed;
        main.maxParticles = _particleCount;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = _particleSystem.emission;
        emission.enabled = false;
        
        var shape = _particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        
        var colorOverLifetime = _particleSystem.colorOverLifetime;
        colorOverLifetime.enabled = true;
        if (_particleColorGradient == null)
        {
            _particleColorGradient = new Gradient();
            _particleColorGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
        }
        colorOverLifetime.color = _particleColorGradient;
    }
    
    private void Update()
    {
        UpdateColorTransition();
    }
    
    private void UpdateColorTransition()
    {
        if (_currentColorLerp > 0f)
        {
            _currentColorLerp -= Time.deltaTime * _colorTransitionSpeed;
            _currentColorLerp = Mathf.Max(0f, _currentColorLerp);
            
            Color currentColor = Color.Lerp(_originalColor, _targetColor, _currentColorLerp);
            _objectRenderer.material.color = currentColor;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Vector3 hitPoint = other.ClosestPoint(transform.position);
            PlayNoteAtPosition(hitPoint);
        }
    }
    
    private void PlayNoteAtPosition(Vector3 hitPosition)
    {
        if (_isPlaying)
        {
            if (_playNoteCoroutine != null)
                StopCoroutine(_playNoteCoroutine);
        }
        
        float normalizedHeight = CalculateNormalizedHeight(hitPosition);
        float frequency = _baseFrequency + (normalizedHeight * _frequencyRange);
        
        _playNoteCoroutine = StartCoroutine(PlayNoteCoroutine(frequency, normalizedHeight));
    }
    
    private float CalculateNormalizedHeight(Vector3 hitPosition)
    {
        Bounds bounds = _collider.bounds;
        float relativeY = hitPosition.y - bounds.min.y;
        float normalizedHeight = relativeY / bounds.size.y;
        return Mathf.Clamp01(normalizedHeight);
    }
    
    private IEnumerator PlayNoteCoroutine(float frequency, float normalizedHeight)
    {
        _isPlaying = true;
        
        // Set color based on frequency
        _targetColor = Color.Lerp(_lowNoteColor, _highNoteColor, normalizedHeight);
        _currentColorLerp = 1f;
        
        // Generate and play audio
        AudioClip noteClip = GenerateTone(frequency, _noteDuration);
        _audioSource.clip = noteClip;
        _audioSource.Play();
        
        // Emit particles
        EmitParticles(normalizedHeight);
        
        // Start vibration
        StartCoroutine(VibrateObject());
        
        yield return new WaitForSeconds(_noteDuration);
        
        _isPlaying = false;
        
        // Cleanup
        if (noteClip != null)
            DestroyImmediate(noteClip);
    }
    
    private AudioClip GenerateTone(float frequency, float duration)
    {
        int sampleRate = 44100;
        int sampleCount = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        
        for (int i = 0; i < sampleCount; i++)
        {
            float time = (float)i / sampleRate;
            float volume = _volumeCurve.Evaluate(time / duration);
            samples[i] = Mathf.Sin(2 * Mathf.PI * frequency * time) * volume;
        }
        
        AudioClip clip = AudioClip.Create("GeneratedTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
    
    private void EmitParticles(float normalizedHeight)
    {
        var emission = _particleSystem.emission;
        emission.enabled = true;
        
        // Update particle color based on note
        var main = _particleSystem.main;
        Color particleColor = Color.Lerp(_lowNoteColor, _highNoteColor, normalizedHeight);
        main.startColor = particleColor;
        
        // Emit burst
        _particleSystem.Emit(_particleCount);
        
        StartCoroutine(DisableEmissionAfterDelay());
    }
    
    private IEnumerator DisableEmissionAfterDelay()
    {
        yield return new WaitForSeconds(0.1f);
        var emission = _particleSystem.emission;
        emission.enabled = false;
    }
    
    private IEnumerator VibrateObject()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _noteDuration)
        {
            float normalizedTime = elapsedTime / _noteDuration;
            float vibrationAmount = _vibrationCurve.Evaluate(normalizedTime) * _vibrationIntensity;
            
            Vector3 randomOffset = new Vector3(
                Mathf.Sin(Time.time * _vibrationFrequency) * vibrationAmount,
                Mathf.Cos(Time.time * _vibrationFrequency * 1.1f) * vibrationAmount,
                Mathf.Sin(Time.time * _vibrationFrequency * 0.9f) * vibrationAmount
            );
            
            transform.position = _originalPosition + randomOffset;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.position = _originalPosition;
    }
    
    private void OnDestroy()
    {
        if (_playNoteCoroutine != null)
            StopCoroutine(_playNoteCoroutine);
    }
}