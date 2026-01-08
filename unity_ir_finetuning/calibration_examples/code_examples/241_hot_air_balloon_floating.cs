// Prompt: hot air balloon floating
// Type: combat

using UnityEngine;

public class HotAirBalloon : MonoBehaviour
{
    [Header("Balloon Physics")]
    [SerializeField] private float _baseFloatForce = 10f;
    [SerializeField] private float _maxAltitude = 50f;
    [SerializeField] private float _windStrength = 2f;
    [SerializeField] private float _bobAmplitude = 0.5f;
    [SerializeField] private float _bobFrequency = 1f;
    
    [Header("Heat Control")]
    [SerializeField] private float _heatDecayRate = 1f;
    [SerializeField] private float _maxHeat = 100f;
    [SerializeField] private float _minFloatHeat = 20f;
    [SerializeField] private KeyCode _heatKey = KeyCode.Space;
    [SerializeField] private float _heatIncreaseRate = 30f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _flameParticles;
    [SerializeField] private Transform _basket;
    [SerializeField] private Transform _balloon;
    [SerializeField] private float _basketSwayAmount = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _flameAudioSource;
    [SerializeField] private AudioClip _flameSound;
    
    private Rigidbody _rigidbody;
    private float _currentHeat;
    private float _bobTimer;
    private Vector3 _windDirection;
    private float _windTimer;
    private Vector3 _initialBasketPosition;
    private Vector3 _initialBalloonPosition;
    private bool _isPlayerControlled;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.drag = 2f;
        _rigidbody.angularDrag = 5f;
        
        _currentHeat = _maxHeat * 0.7f;
        _windDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
        
        if (_basket != null)
            _initialBasketPosition = _basket.localPosition;
        if (_balloon != null)
            _initialBalloonPosition = _balloon.localPosition;
            
        if (_flameAudioSource == null && _flameSound != null)
        {
            _flameAudioSource = gameObject.AddComponent<AudioSource>();
            _flameAudioSource.clip = _flameSound;
            _flameAudioSource.loop = true;
            _flameAudioSource.playOnAwake = false;
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateHeat();
        UpdateVisualEffects();
        UpdateAudio();
        _bobTimer += Time.deltaTime;
        _windTimer += Time.deltaTime;
        
        if (_windTimer >= 5f)
        {
            _windDirection = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
            _windTimer = 0f;
        }
    }
    
    private void FixedUpdate()
    {
        ApplyFloatForce();
        ApplyWindForce();
        ApplyBobbing();
        LimitAltitude();
        UpdateBasketSway();
    }
    
    private void HandleInput()
    {
        if (Input.GetKey(_heatKey))
        {
            _currentHeat = Mathf.Min(_currentHeat + _heatIncreaseRate * Time.deltaTime, _maxHeat);
            _isPlayerControlled = true;
        }
        else
        {
            _isPlayerControlled = false;
        }
    }
    
    private void UpdateHeat()
    {
        _currentHeat = Mathf.Max(_currentHeat - _heatDecayRate * Time.deltaTime, 0f);
    }
    
    private void ApplyFloatForce()
    {
        if (_currentHeat > _minFloatHeat)
        {
            float heatRatio = (_currentHeat - _minFloatHeat) / (_maxHeat - _minFloatHeat);
            float floatForce = _baseFloatForce * heatRatio;
            
            Vector3 upwardForce = Vector3.up * floatForce;
            _rigidbody.AddForce(upwardForce, ForceMode.Force);
        }
    }
    
    private void ApplyWindForce()
    {
        Vector3 windForce = _windDirection * _windStrength;
        windForce.y = 0f;
        _rigidbody.AddForce(windForce, ForceMode.Force);
    }
    
    private void ApplyBobbing()
    {
        float bobOffset = Mathf.Sin(_bobTimer * _bobFrequency) * _bobAmplitude;
        Vector3 bobForce = Vector3.up * bobOffset;
        _rigidbody.AddForce(bobForce, ForceMode.Force);
    }
    
    private void LimitAltitude()
    {
        if (transform.position.y > _maxAltitude)
        {
            Vector3 downwardForce = Vector3.down * (_baseFloatForce * 2f);
            _rigidbody.AddForce(downwardForce, ForceMode.Force);
        }
    }
    
    private void UpdateBasketSway()
    {
        if (_basket != null)
        {
            Vector3 velocity = _rigidbody.velocity;
            float swayX = -velocity.x * _basketSwayAmount * 0.1f;
            float swayZ = -velocity.z * _basketSwayAmount * 0.1f;
            
            Vector3 targetPosition = _initialBasketPosition + new Vector3(swayX, 0, swayZ);
            _basket.localPosition = Vector3.Lerp(_basket.localPosition, targetPosition, Time.deltaTime * 2f);
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_flameParticles != null)
        {
            var emission = _flameParticles.emission;
            
            if (_isPlayerControlled && _currentHeat < _maxHeat)
            {
                if (!_flameParticles.isPlaying)
                    _flameParticles.Play();
                    
                emission.rateOverTime = Mathf.Lerp(10f, 50f, (_currentHeat / _maxHeat));
            }
            else
            {
                if (_flameParticles.isPlaying)
                    _flameParticles.Stop();
            }
        }
    }
    
    private void UpdateAudio()
    {
        if (_flameAudioSource != null)
        {
            if (_isPlayerControlled && _currentHeat < _maxHeat)
            {
                if (!_flameAudioSource.isPlaying)
                    _flameAudioSource.Play();
                    
                _flameAudioSource.volume = Mathf.Lerp(0.1f, 0.8f, (_currentHeat / _maxHeat));
            }
            else
            {
                if (_flameAudioSource.isPlaying)
                    _flameAudioSource.Stop();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerControlled = true;
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerControlled = false;
        }
    }
    
    public float GetHeatPercentage()
    {
        return _currentHeat / _maxHeat;
    }
    
    public bool IsFloating()
    {
        return _currentHeat > _minFloatHeat;
    }
    
    public void AddHeat(float amount)
    {
        _currentHeat = Mathf.Min(_currentHeat + amount, _maxHeat);
    }
}