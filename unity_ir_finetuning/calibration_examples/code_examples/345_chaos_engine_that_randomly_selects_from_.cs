// Prompt: chaos engine that randomly selects from 20 different behaviors every 3 seconds (spin, bounce, scale pulse, color shift, audio play, particle burst, force application, teleport, rotation etc) - never repeats same behavior twice in a row, after 60 seconds settles into calm meditative state
// Type: general

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class ChaosEngine : MonoBehaviour
{
    [Header("Chaos Settings")]
    [SerializeField] private float _behaviorInterval = 3f;
    [SerializeField] private float _chaosModeDuration = 60f;
    [SerializeField] private bool _startInChaosMode = true;
    
    [Header("Movement Behaviors")]
    [SerializeField] private float _spinSpeed = 360f;
    [SerializeField] private float _bounceForce = 10f;
    [SerializeField] private float _teleportRadius = 20f;
    [SerializeField] private float _forceStrength = 500f;
    
    [Header("Visual Behaviors")]
    [SerializeField] private AnimationCurve _scalePulseCurve = AnimationCurve.EaseInOut(0, 1, 1, 2);
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private Color[] _colorShiftColors = { Color.red, Color.blue, Color.green, Color.yellow, Color.magenta };
    [SerializeField] private float _colorShiftDuration = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] _chaosAudioClips;
    [SerializeField] private AudioClip _meditativeAudioClip;
    [SerializeField] private float _audioVolume = 0.5f;
    
    [Header("Particles")]
    [SerializeField] private ParticleSystem _particleSystem;
    [SerializeField] private int _burstParticleCount = 50;
    
    [Header("Meditative State")]
    [SerializeField] private float _meditativeRotationSpeed = 30f;
    [SerializeField] private Color _meditativeColor = Color.white;
    [SerializeField] private float _meditativeTransitionDuration = 3f;
    
    [Header("Events")]
    public UnityEvent OnChaosStart;
    public UnityEvent OnMeditativeStateEnter;
    
    private enum ChaoseBehavior
    {
        Spin, Bounce, ScalePulse, ColorShift, AudioPlay, ParticleBurst,
        ForceApplication, Teleport, RotationFlip, Shake, Orbit, Wobble,
        FlashScale, RandomJump, ColorCycle, SpeedBurst, GravityFlip,
        Vibrate, SizeOscillate, DirectionalSpin
    }
    
    private Rigidbody _rigidbody;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Material _originalMaterial;
    private Vector3 _originalScale;
    private Vector3 _originalPosition;
    private Color _originalColor;
    
    private bool _isInChaosMode = true;
    private bool _isInMeditativeState = false;
    private float _chaosTimer = 0f;
    private ChaoseBehavior _lastBehavior = ChaoseBehavior.Spin;
    private Coroutine _currentBehaviorCoroutine;
    private Coroutine _meditativeTransitionCoroutine;
    
    private List<ChaoseBehavior> _availableBehaviors;
    
    void Start()
    {
        InitializeComponents();
        InitializeBehaviors();
        
        if (_startInChaosMode)
        {
            StartChaosMode();
        }
    }
    
    void Update()
    {
        if (_isInChaosMode && !_isInMeditativeState)
        {
            _chaosTimer += Time.deltaTime;
            
            if (_chaosTimer >= _chaosModeDuration)
            {
                EnterMeditativeState();
            }
        }
    }
    
    private void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        _renderer = GetComponent<Renderer>();
        if (_renderer == null)
            _renderer = GetComponentInChildren<Renderer>();
            
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_particleSystem == null)
            _particleSystem = GetComponentInChildren<ParticleSystem>();
        
        _originalScale = transform.localScale;
        _originalPosition = transform.position;
        
        if (_renderer != null)
        {
            _originalMaterial = _renderer.material;
            _originalColor = _renderer.material.color;
        }
    }
    
    private void InitializeBehaviors()
    {
        _availableBehaviors = new List<ChaoseBehavior>();
        for (int i = 0; i < System.Enum.GetValues(typeof(ChaoseBehavior)).Length; i++)
        {
            _availableBehaviors.Add((ChaoseBehavior)i);
        }
    }
    
    private void StartChaosMode()
    {
        _isInChaosMode = true;
        _chaosTimer = 0f;
        OnChaosStart?.Invoke();
        StartCoroutine(ChaosLoop());
    }
    
    private IEnumerator ChaosLoop()
    {
        while (_isInChaosMode && !_isInMeditativeState)
        {
            ChaoseBehavior nextBehavior = GetRandomBehavior();
            ExecuteBehavior(nextBehavior);
            _lastBehavior = nextBehavior;
            
            yield return new WaitForSeconds(_behaviorInterval);
        }
    }
    
    private ChaoseBehavior GetRandomBehavior()
    {
        List<ChaoseBehavior> availableChoices = new List<ChaoseBehavior>(_availableBehaviors);
        availableChoices.Remove(_lastBehavior);
        
        return availableChoices[Random.Range(0, availableChoices.Count)];
    }
    
    private void ExecuteBehavior(ChaoseBehavior behavior)
    {
        if (_currentBehaviorCoroutine != null)
            StopCoroutine(_currentBehaviorCoroutine);
            
        switch (behavior)
        {
            case ChaoseBehavior.Spin:
                _currentBehaviorCoroutine = StartCoroutine(SpinBehavior());
                break;
            case ChaoseBehavior.Bounce:
                _currentBehaviorCoroutine = StartCoroutine(BounceBehavior());
                break;
            case ChaoseBehavior.ScalePulse:
                _currentBehaviorCoroutine = StartCoroutine(ScalePulseBehavior());
                break;
            case ChaoseBehavior.ColorShift:
                _currentBehaviorCoroutine = StartCoroutine(ColorShiftBehavior());
                break;
            case ChaoseBehavior.AudioPlay:
                _currentBehaviorCoroutine = StartCoroutine(AudioPlayBehavior());
                break;
            case ChaoseBehavior.ParticleBurst:
                _currentBehaviorCoroutine = StartCoroutine(ParticleBurstBehavior());
                break;
            case ChaoseBehavior.ForceApplication:
                _currentBehaviorCoroutine = StartCoroutine(ForceApplicationBehavior());
                break;
            case ChaoseBehavior.Teleport:
                _currentBehaviorCoroutine = StartCoroutine(TeleportBehavior());
                break;
            case ChaoseBehavior.RotationFlip:
                _currentBehaviorCoroutine = StartCoroutine(RotationFlipBehavior());
                break;
            case ChaoseBehavior.Shake:
                _currentBehaviorCoroutine = StartCoroutine(ShakeBehavior());
                break;
            case ChaoseBehavior.Orbit:
                _currentBehaviorCoroutine = StartCoroutine(OrbitBehavior());
                break;
            case ChaoseBehavior.Wobble:
                _currentBehaviorCoroutine = StartCoroutine(WobbleBehavior());
                break;
            case ChaoseBehavior.FlashScale:
                _currentBehaviorCoroutine = StartCoroutine(FlashScaleBehavior());
                break;
            case ChaoseBehavior.RandomJump:
                _currentBehaviorCoroutine = StartCoroutine(RandomJumpBehavior());
                break;
            case ChaoseBehavior.ColorCycle:
                _currentBehaviorCoroutine = StartCoroutine(ColorCycleBehavior());
                break;
            case ChaoseBehavior.SpeedBurst:
                _currentBehaviorCoroutine = StartCoroutine(SpeedBurstBehavior());
                break;
            case ChaoseBehavior.GravityFlip:
                _currentBehaviorCoroutine = StartCoroutine(GravityFlipBehavior());
                break;
            case ChaoseBehavior.Vibrate:
                _currentBehaviorCoroutine = StartCoroutine(VibrateBehavior());
                break;
            case ChaoseBehavior.SizeOscillate:
                _currentBehaviorCoroutine = StartCoroutine(SizeOscillateBehavior());
                break;
            case ChaoseBehavior.DirectionalSpin:
                _currentBehaviorCoroutine = StartCoroutine(DirectionalSpinBehavior());
                break;
        }
    }
    
    private IEnumerator SpinBehavior()
    {
        float duration = _behaviorInterval * 0.8f;
        float elapsed = 0f;
        Vector3 spinAxis = Random.insideUnitSphere.normalized;
        
        while (elapsed < duration)
        {
            transform.Rotate(spinAxis * _spinSpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    private IEnumerator BounceBehavior()
    {
        if (_rigidbody != null)
        {
            Vector3 bounceDirection = Vector3.up + Random.insideUnitSphere * 0.5f;
            _rigidbody.AddForce(bounceDirection.normalized * _bounceForce, ForceMode.Impulse);
        }
        yield return null;
    }
    
    private IEnumerator ScalePulseBehavior()
    {
        float duration = _behaviorInterval * 0.8f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float normalizedTime = elapsed / duration;
            float scaleMultiplier = _scalePulseCurve.Evaluate(normalizedTime * _pulseSpeed % 1f);
            transform.localScale = _originalScale * scaleMultiplier;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = _originalScale;
    }
    
    private IEnumerator ColorShiftBehavior()
    {
        if (_renderer != null && _colorShiftColors.Length > 0)
        {
            Color targetColor = _colorShiftColors[Random.Range(0, _colorShiftColors.Length)];
            Color startColor = _renderer.material.color;
            float elapsed = 0f;
            
            while (elapsed < _colorShiftDuration)
            {
                _renderer.material.color = Color.Lerp(startColor, targetColor, elapsed / _colorShiftDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        yield return null;
    }
    
    private IEnumerator AudioPlayBehavior()
    {
        if (_audioSource != null && _chaosAudioClips.Length > 0)
        {
            AudioClip clip = _chaosAudioClips[Random.Range(0, _chaosAudioClips.Length)];
            _audioSource.clip = clip;
            _audioSource.volume = _audioVolume;
            _audioSource.Play();
        }
        yield return null;
    }
    
    private IEnumerator ParticleBurstBehavior()
    {
        if (_particleSystem != null)
        {
            _particleSystem.Emit(_burstParticleCount);
        }
        yield return null;
    }
    
    private IEnumerator ForceApplicationBehavior()
    {
        if (_rigidbody != null)
        {
            Vector3 randomForce = Random.insideUnitSphere * _forceStrength;
            _rigidbody.AddForce(randomForce, ForceMode.Impulse);
        }
        yield return null;
    }
    
    private IEnumerator TeleportBehavior()
    {
        Vector3 randomOffset = Random.insideUnitSphere * _teleportRadius;
        randomOffset.y = Mathf.Abs(randomOffset.y);
        transform.position = _originalPosition + randomOffset;
        yield return null;
    }
    
    private IEnumerator RotationFlipBehavior()
    {
        Vector3 randomRotation = new Vector3(
            Random.Range(0, 360),
            Random.Range(0, 360),
            Random.Range(0, 360)
        );
        transform.rotation = Quaternion.Euler(randomRotation);
        yield return null;
    }
    
    private IEnumerator ShakeBehavior()
    {
        float duration = _behaviorInterval * 0.6f;
        float elapsed = 0f;
        Vector3 originalPos = transform.position;
        
        while (elapsed < duration)
        {
            Vector3 shakeOffset = Random.insideUnitSphere * 0.5f;
            transform.position = originalPos + shakeOffset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        transform.position = originalPos;
    }
    
    private IEnumerator OrbitBehavior()
    {
        float duration = _behaviorInterval * 0.8f;
        float elapsed = 0f;
        Vector3 center = _originalPosition;
        float radius = 5f;
        
        while (elapsed < duration)
        {
            float angle = (elapsed / duration) * 360f * 2f;
            Vector3 offset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                Mathf.Sin(angle * Mathf.Deg2Rad) * radius * 0.5f,
                Mathf.Sin(angle * Mathf.Deg2Rad * 0.5f) * radius
            );
            transform.position = center + offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
    
    private IEnumerator WobbleBehavior()
    {
        float duration = _behaviorInterval * 0.8f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            float wobbleX = Mathf.Sin(elapsed * 10f) * 0.3f;
            float wobbleZ = Mathf.Cos(elapsed * 8f) * 0.3f;
            transform.localScale = _originalScale + new Vector3(wobbleX, 0, wobbleZ);
            elapsed += Time.deltaTime;
            yield return null;