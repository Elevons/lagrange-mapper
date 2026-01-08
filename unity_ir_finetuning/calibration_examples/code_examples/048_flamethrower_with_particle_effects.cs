// Prompt: flamethrower with particle effects
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Flamethrower : MonoBehaviour
{
    [Header("Flame Settings")]
    [SerializeField] private float _maxRange = 10f;
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _damageRate = 0.1f;
    [SerializeField] private float _fuelConsumption = 10f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Fuel System")]
    [SerializeField] private float _maxFuel = 100f;
    [SerializeField] private float _currentFuel = 100f;
    [SerializeField] private float _refuelRate = 20f;
    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem _flameParticles;
    [SerializeField] private ParticleSystem _smokeParticles;
    [SerializeField] private ParticleSystem _sparkParticles;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _igniteSound;
    [SerializeField] private AudioClip _flameLoopSound;
    [SerializeField] private AudioClip _extinguishSound;
    
    [Header("Visual Effects")]
    [SerializeField] private Light _flameLight;
    [SerializeField] private float _lightIntensity = 2f;
    [SerializeField] private Color _flameColor = Color.red;
    [SerializeField] private AnimationCurve _lightFlicker = AnimationCurve.Linear(0, 0.8f, 1, 1.2f);
    
    [Header("Input")]
    [SerializeField] private KeyCode _fireKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode _refuelKey = KeyCode.R;
    
    [Header("Events")]
    public UnityEvent OnIgnite;
    public UnityEvent OnExtinguish;
    public UnityEvent OnFuelEmpty;
    public UnityEvent<float> OnFuelChanged;
    
    private bool _isFiring;
    private float _damageTimer;
    private float _lightFlickerTime;
    private System.Collections.Generic.HashSet<Collider> _targetsInRange = new System.Collections.Generic.HashSet<Collider>();
    private ParticleSystem.MainModule _flameMain;
    private ParticleSystem.MainModule _smokeMain;
    private ParticleSystem.MainModule _sparkMain;
    private float _originalLightIntensity;
    
    private void Start()
    {
        _currentFuel = _maxFuel;
        
        if (_flameParticles != null)
        {
            _flameMain = _flameParticles.main;
            _flameParticles.Stop();
        }
        
        if (_smokeParticles != null)
        {
            _smokeMain = _smokeParticles.main;
            _smokeParticles.Stop();
        }
        
        if (_sparkParticles != null)
        {
            _sparkMain = _sparkParticles.main;
            _sparkParticles.Stop();
        }
        
        if (_flameLight != null)
        {
            _originalLightIntensity = _flameLight.intensity;
            _flameLight.intensity = 0f;
            _flameLight.color = _flameColor;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateFlamethrower();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        bool shouldFire = Input.GetKey(_fireKey) && _currentFuel > 0f;
        
        if (shouldFire && !_isFiring)
        {
            StartFiring();
        }
        else if (!shouldFire && _isFiring)
        {
            StopFiring();
        }
        
        if (Input.GetKeyDown(_refuelKey))
        {
            Refuel();
        }
    }
    
    private void UpdateFlamethrower()
    {
        if (_isFiring)
        {
            ConsumeFuel();
            DealDamage();
            DetectTargets();
        }
        
        _lightFlickerTime += Time.deltaTime * 5f;
    }
    
    private void UpdateVisualEffects()
    {
        if (_flameLight != null)
        {
            if (_isFiring)
            {
                float flickerValue = _lightFlicker.Evaluate(Mathf.PingPong(_lightFlickerTime, 1f));
                _flameLight.intensity = _lightIntensity * flickerValue;
            }
            else
            {
                _flameLight.intensity = Mathf.Lerp(_flameLight.intensity, 0f, Time.deltaTime * 5f);
            }
        }
    }
    
    private void StartFiring()
    {
        _isFiring = true;
        
        if (_flameParticles != null)
            _flameParticles.Play();
        
        if (_smokeParticles != null)
            _smokeParticles.Play();
        
        if (_sparkParticles != null)
            _sparkParticles.Play();
        
        PlaySound(_igniteSound);
        
        if (_flameLoopSound != null && _audioSource != null)
        {
            _audioSource.clip = _flameLoopSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        OnIgnite?.Invoke();
    }
    
    private void StopFiring()
    {
        _isFiring = false;
        
        if (_flameParticles != null)
            _flameParticles.Stop();
        
        if (_smokeParticles != null)
            _smokeParticles.Stop();
        
        if (_sparkParticles != null)
            _sparkParticles.Stop();
        
        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();
        
        PlaySound(_extinguishSound);
        
        _targetsInRange.Clear();
        OnExtinguish?.Invoke();
    }
    
    private void ConsumeFuel()
    {
        _currentFuel -= _fuelConsumption * Time.deltaTime;
        _currentFuel = Mathf.Max(0f, _currentFuel);
        
        OnFuelChanged?.Invoke(_currentFuel / _maxFuel);
        
        if (_currentFuel <= 0f)
        {
            StopFiring();
            OnFuelEmpty?.Invoke();
        }
    }
    
    private void DetectTargets()
    {
        _targetsInRange.Clear();
        
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            1f,
            transform.forward,
            _maxRange,
            _targetLayers
        );
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider != null && hit.collider.gameObject != gameObject)
            {
                _targetsInRange.Add(hit.collider);
            }
        }
    }
    
    private void DealDamage()
    {
        _damageTimer += Time.deltaTime;
        
        if (_damageTimer >= _damageRate)
        {
            _damageTimer = 0f;
            
            foreach (Collider target in _targetsInRange)
            {
                if (target == null) continue;
                
                // Apply damage to objects with health components
                var healthComponent = target.GetComponent<Health>();
                if (healthComponent != null)
                {
                    healthComponent.TakeDamage(_damage);
                }
                
                // Apply fire effect to burnable objects
                var burnableComponent = target.GetComponent<Burnable>();
                if (burnableComponent != null)
                {
                    burnableComponent.Ignite();
                }
                
                // Push rigidbodies away
                var rb = target.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 pushDirection = (target.transform.position - transform.position).normalized;
                    rb.AddForce(pushDirection * 5f, ForceMode.Impulse);
                }
            }
        }
    }
    
    private void Refuel()
    {
        _currentFuel = _maxFuel;
        OnFuelChanged?.Invoke(1f);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void SetFuel(float amount)
    {
        _currentFuel = Mathf.Clamp(amount, 0f, _maxFuel);
        OnFuelChanged?.Invoke(_currentFuel / _maxFuel);
    }
    
    public float GetFuelPercentage()
    {
        return _currentFuel / _maxFuel;
    }
    
    public bool IsFiring()
    {
        return _isFiring;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * _maxRange, 1f);
        Gizmos.DrawRay(transform.position, transform.forward * _maxRange);
    }
}

[System.Serializable]
public class Health : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0f, _currentHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
        
        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }
    
    public void Heal(float amount)
    {
        _currentHealth += amount;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }
}

[System.Serializable]
public class Burnable : MonoBehaviour
{
    [SerializeField] private float _burnDuration = 5f;
    [SerializeField] private float _burnDamage = 10f;
    [SerializeField] private ParticleSystem _fireEffect;
    
    public UnityEvent OnIgnited;
    public UnityEvent OnExtinguished;
    
    private bool _isBurning;
    private float _burnTimer;
    private Health _health;
    
    private void Start()
    {
        _health = GetComponent<Health>();
    }
    
    private void Update()
    {
        if (_isBurning)
        {
            _burnTimer -= Time.deltaTime;
            
            if (_health != null)
            {
                _health.TakeDamage(_burnDamage * Time.deltaTime);
            }
            
            if (_burnTimer <= 0f)
            {
                Extinguish();
            }
        }
    }
    
    public void Ignite()
    {
        if (!_isBurning)
        {
            _isBurning = true;
            _burnTimer = _burnDuration;
            
            if (_fireEffect != null)
                _fireEffect.Play();
            
            OnIgnited?.Invoke();
        }
        else
        {
            _burnTimer = _burnDuration; // Reset burn timer
        }
    }
    
    public void Extinguish()
    {
        if (_isBurning)
        {
            _isBurning = false;
            
            if (_fireEffect != null)
                _fireEffect.Stop();
            
            OnExtinguished?.Invoke();
        }
    }
}