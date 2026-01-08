// Prompt: torch that lights up area
// Type: general

using UnityEngine;

public class Torch : MonoBehaviour
{
    [Header("Light Settings")]
    [SerializeField] private Light torchLight;
    [SerializeField] private float lightIntensity = 2f;
    [SerializeField] private float lightRange = 10f;
    [SerializeField] private Color lightColor = Color.orange;
    
    [Header("Flame Effect")]
    [SerializeField] private ParticleSystem flameParticles;
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private AudioClip fireSound;
    
    [Header("Flickering")]
    [SerializeField] private bool enableFlickering = true;
    [SerializeField] private float flickerSpeed = 5f;
    [SerializeField] private float flickerIntensity = 0.3f;
    [SerializeField] private AnimationCurve flickerCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Interaction")]
    [SerializeField] private bool canBeToggled = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.E;
    [SerializeField] private float interactionRange = 3f;
    
    [Header("Fuel System")]
    [SerializeField] private bool useFuelSystem = false;
    [SerializeField] private float maxFuelTime = 300f;
    [SerializeField] private float currentFuel = 300f;
    
    private bool _isLit = true;
    private float _baseIntensity;
    private float _flickerTime;
    private Transform _playerTransform;
    private bool _playerInRange;
    
    private void Start()
    {
        SetupTorchLight();
        SetupAudio();
        SetupParticles();
        FindPlayer();
        
        _baseIntensity = lightIntensity;
        currentFuel = maxFuelTime;
    }
    
    private void Update()
    {
        HandlePlayerInteraction();
        UpdateFlickering();
        UpdateFuelSystem();
    }
    
    private void SetupTorchLight()
    {
        if (torchLight == null)
        {
            torchLight = GetComponentInChildren<Light>();
            if (torchLight == null)
            {
                GameObject lightObject = new GameObject("TorchLight");
                lightObject.transform.SetParent(transform);
                lightObject.transform.localPosition = Vector3.up * 0.5f;
                torchLight = lightObject.AddComponent<Light>();
            }
        }
        
        torchLight.type = LightType.Point;
        torchLight.intensity = lightIntensity;
        torchLight.range = lightRange;
        torchLight.color = lightColor;
        torchLight.shadows = LightShadows.Soft;
    }
    
    private void SetupAudio()
    {
        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponent<AudioSource>();
            if (fireAudioSource == null)
            {
                fireAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        fireAudioSource.clip = fireSound;
        fireAudioSource.loop = true;
        fireAudioSource.volume = 0.3f;
        fireAudioSource.spatialBlend = 1f;
        
        if (_isLit && fireSound != null)
        {
            fireAudioSource.Play();
        }
    }
    
    private void SetupParticles()
    {
        if (flameParticles == null)
        {
            flameParticles = GetComponentInChildren<ParticleSystem>();
        }
        
        if (flameParticles != null)
        {
            var emission = flameParticles.emission;
            emission.enabled = _isLit;
        }
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }
    
    private void HandlePlayerInteraction()
    {
        if (!canBeToggled || _playerTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        _playerInRange = distanceToPlayer <= interactionRange;
        
        if (_playerInRange && Input.GetKeyDown(toggleKey))
        {
            ToggleTorch();
        }
    }
    
    private void UpdateFlickering()
    {
        if (!enableFlickering || !_isLit) return;
        
        _flickerTime += Time.deltaTime * flickerSpeed;
        float flickerValue = flickerCurve.Evaluate(Mathf.PingPong(_flickerTime, 1f));
        float flickerOffset = (flickerValue - 0.5f) * flickerIntensity;
        
        torchLight.intensity = _baseIntensity + flickerOffset;
        
        if (flameParticles != null)
        {
            var velocityOverLifetime = flameParticles.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(2f + flickerOffset * 0.5f);
        }
    }
    
    private void UpdateFuelSystem()
    {
        if (!useFuelSystem || !_isLit) return;
        
        currentFuel -= Time.deltaTime;
        
        if (currentFuel <= 0f)
        {
            currentFuel = 0f;
            ExtinguishTorch();
        }
        else if (currentFuel < maxFuelTime * 0.1f)
        {
            // Torch is running low on fuel, increase flickering
            flickerIntensity = Mathf.Lerp(0.3f, 0.8f, 1f - (currentFuel / (maxFuelTime * 0.1f)));
        }
    }
    
    public void ToggleTorch()
    {
        if (_isLit)
        {
            ExtinguishTorch();
        }
        else
        {
            LightTorch();
        }
    }
    
    public void LightTorch()
    {
        if (useFuelSystem && currentFuel <= 0f) return;
        
        _isLit = true;
        torchLight.enabled = true;
        
        if (flameParticles != null)
        {
            var emission = flameParticles.emission;
            emission.enabled = true;
            flameParticles.Play();
        }
        
        if (fireAudioSource != null && fireSound != null)
        {
            fireAudioSource.Play();
        }
    }
    
    public void ExtinguishTorch()
    {
        _isLit = false;
        torchLight.enabled = false;
        
        if (flameParticles != null)
        {
            var emission = flameParticles.emission;
            emission.enabled = false;
            flameParticles.Stop();
        }
        
        if (fireAudioSource != null)
        {
            fireAudioSource.Stop();
        }
    }
    
    public void AddFuel(float fuelAmount)
    {
        if (!useFuelSystem) return;
        
        currentFuel = Mathf.Min(currentFuel + fuelAmount, maxFuelTime);
    }
    
    public bool IsLit()
    {
        return _isLit;
    }
    
    public float GetFuelPercentage()
    {
        if (!useFuelSystem) return 1f;
        return currentFuel / maxFuelTime;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw light range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, lightRange);
        
        // Draw interaction range
        if (canBeToggled)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, interactionRange);
        }
    }
}