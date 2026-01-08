// Prompt: rocket ship launch
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class RocketShipLauncher : MonoBehaviour
{
    [Header("Launch Settings")]
    [SerializeField] private float _launchForce = 1000f;
    [SerializeField] private float _launchDelay = 3f;
    [SerializeField] private float _fuelBurnDuration = 5f;
    [SerializeField] private AnimationCurve _thrustCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _countdownSound;
    [SerializeField] private AudioClip _launchSound;
    [SerializeField] private AudioClip _thrusterSound;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _thrusterParticles;
    [SerializeField] private ParticleSystem _smokeParticles;
    [SerializeField] private Light _thrusterLight;
    [SerializeField] private Transform _rocketTransform;
    
    [Header("Launch Trigger")]
    [SerializeField] private KeyCode _launchKey = KeyCode.Space;
    [SerializeField] private bool _autoLaunch = false;
    [SerializeField] private float _autoLaunchDelay = 2f;
    
    [Header("Events")]
    public UnityEvent OnCountdownStart;
    public UnityEvent OnLaunchStart;
    public UnityEvent OnLaunchComplete;
    
    private Rigidbody _rigidbody;
    private bool _isLaunching = false;
    private bool _hasLaunched = false;
    private float _launchTimer = 0f;
    private float _thrustTimer = 0f;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (_rocketTransform == null)
        {
            _rocketTransform = transform;
        }
        
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        
        _rigidbody.isKinematic = true;
        
        if (_thrusterLight != null)
        {
            _thrusterLight.enabled = false;
        }
        
        if (_autoLaunch)
        {
            Invoke(nameof(StartLaunchSequence), _autoLaunchDelay);
        }
    }
    
    private void Update()
    {
        if (!_hasLaunched && !_isLaunching && Input.GetKeyDown(_launchKey))
        {
            StartLaunchSequence();
        }
        
        if (_isLaunching)
        {
            UpdateLaunchSequence();
        }
    }
    
    private void StartLaunchSequence()
    {
        if (_hasLaunched || _isLaunching) return;
        
        _isLaunching = true;
        _launchTimer = 0f;
        
        OnCountdownStart?.Invoke();
        
        if (_countdownSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_countdownSound);
        }
        
        if (_smokeParticles != null)
        {
            _smokeParticles.Play();
        }
    }
    
    private void UpdateLaunchSequence()
    {
        _launchTimer += Time.deltaTime;
        
        if (_launchTimer >= _launchDelay && !_hasLaunched)
        {
            Launch();
        }
        
        if (_hasLaunched)
        {
            UpdateThrust();
        }
    }
    
    private void Launch()
    {
        _hasLaunched = true;
        _thrustTimer = 0f;
        _rigidbody.isKinematic = false;
        
        OnLaunchStart?.Invoke();
        
        if (_launchSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_launchSound);
        }
        
        if (_thrusterSound != null && _audioSource != null)
        {
            _audioSource.clip = _thrusterSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        if (_thrusterParticles != null)
        {
            _thrusterParticles.Play();
        }
        
        if (_thrusterLight != null)
        {
            _thrusterLight.enabled = true;
        }
    }
    
    private void UpdateThrust()
    {
        _thrustTimer += Time.deltaTime;
        
        if (_thrustTimer <= _fuelBurnDuration)
        {
            float normalizedTime = _thrustTimer / _fuelBurnDuration;
            float thrustMultiplier = _thrustCurve.Evaluate(normalizedTime);
            
            Vector3 thrustForce = _rocketTransform.up * _launchForce * thrustMultiplier * Time.deltaTime;
            _rigidbody.AddForce(thrustForce, ForceMode.Force);
            
            if (_thrusterLight != null)
            {
                _thrusterLight.intensity = thrustMultiplier * 2f;
            }
        }
        else
        {
            CompleteLaunch();
        }
    }
    
    private void CompleteLaunch()
    {
        if (_audioSource != null && _audioSource.isPlaying && _audioSource.clip == _thrusterSound)
        {
            _audioSource.Stop();
        }
        
        if (_thrusterParticles != null && _thrusterParticles.isPlaying)
        {
            _thrusterParticles.Stop();
        }
        
        if (_thrusterLight != null)
        {
            _thrusterLight.enabled = false;
        }
        
        OnLaunchComplete?.Invoke();
    }
    
    public void ResetRocket()
    {
        _isLaunching = false;
        _hasLaunched = false;
        _launchTimer = 0f;
        _thrustTimer = 0f;
        
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
        
        _rigidbody.velocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
        
        if (_thrusterParticles != null && _thrusterParticles.isPlaying)
        {
            _thrusterParticles.Stop();
        }
        
        if (_smokeParticles != null && _smokeParticles.isPlaying)
        {
            _smokeParticles.Stop();
        }
        
        if (_thrusterLight != null)
        {
            _thrusterLight.enabled = false;
        }
    }
    
    public void SetLaunchForce(float force)
    {
        _launchForce = Mathf.Max(0f, force);
    }
    
    public void SetLaunchDelay(float delay)
    {
        _launchDelay = Mathf.Max(0f, delay);
    }
    
    public bool IsLaunching => _isLaunching;
    public bool HasLaunched => _hasLaunched;
    public float LaunchProgress => _hasLaunched ? _thrustTimer / _fuelBurnDuration : _launchTimer / _launchDelay;
}