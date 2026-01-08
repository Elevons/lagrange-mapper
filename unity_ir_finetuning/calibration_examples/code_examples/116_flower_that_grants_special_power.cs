// Prompt: flower that grants special power
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class MagicalFlower : MonoBehaviour
{
    [Header("Flower Settings")]
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private float _powerDuration = 10f;
    [SerializeField] private bool _singleUse = true;
    [SerializeField] private float _respawnTime = 30f;
    
    [Header("Power Effects")]
    [SerializeField] private PowerType _powerType = PowerType.SpeedBoost;
    [SerializeField] private float _powerMultiplier = 2f;
    [SerializeField] private Color _powerColor = Color.magenta;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _idleParticles;
    [SerializeField] private ParticleSystem _collectParticles;
    [SerializeField] private GameObject _flowerModel;
    [SerializeField] private Light _magicalLight;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _collectSound;
    
    [Header("Animation")]
    [SerializeField] private float _bobSpeed = 1f;
    [SerializeField] private float _bobHeight = 0.2f;
    [SerializeField] private float _rotationSpeed = 30f;
    
    [Header("Events")]
    public UnityEvent<PowerType, float, float> OnPowerGranted;
    public UnityEvent OnFlowerCollected;
    public UnityEvent OnFlowerRespawned;
    
    private bool _isAvailable = true;
    private Vector3 _originalPosition;
    private Transform _currentPlayer;
    private Coroutine _respawnCoroutine;
    
    public enum PowerType
    {
        SpeedBoost,
        JumpBoost,
        StrengthBoost,
        Invisibility,
        Shield,
        DoubleJump,
        SlowMotion
    }
    
    private void Start()
    {
        _originalPosition = transform.position;
        
        if (_magicalLight != null)
        {
            _magicalLight.color = _powerColor;
        }
        
        if (_idleParticles != null)
        {
            var main = _idleParticles.main;
            main.startColor = _powerColor;
        }
        
        if (_collectParticles != null)
        {
            var main = _collectParticles.main;
            main.startColor = _powerColor;
        }
    }
    
    private void Update()
    {
        if (!_isAvailable) return;
        
        AnimateFlower();
        CheckForPlayer();
    }
    
    private void AnimateFlower()
    {
        float bobOffset = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = _originalPosition + Vector3.up * bobOffset;
        
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }
    
    private void CheckForPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionRange);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                GrantPower(col.gameObject);
                break;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isAvailable) return;
        
        if (other.CompareTag("Player"))
        {
            GrantPower(other.gameObject);
        }
    }
    
    private void GrantPower(GameObject player)
    {
        if (!_isAvailable) return;
        
        _currentPlayer = player.transform;
        
        ApplyPowerToPlayer(player);
        
        OnPowerGranted?.Invoke(_powerType, _powerMultiplier, _powerDuration);
        OnFlowerCollected?.Invoke();
        
        PlayCollectEffects();
        
        _isAvailable = false;
        
        if (_singleUse && _respawnTime <= 0)
        {
            gameObject.SetActive(false);
        }
        else
        {
            HideFlower();
            if (_respawnCoroutine != null)
                StopCoroutine(_respawnCoroutine);
            _respawnCoroutine = StartCoroutine(RespawnCoroutine());
        }
    }
    
    private void ApplyPowerToPlayer(GameObject player)
    {
        PowerEffect powerEffect = player.GetComponent<PowerEffect>();
        if (powerEffect == null)
        {
            powerEffect = player.AddComponent<PowerEffect>();
        }
        
        powerEffect.ApplyPower(_powerType, _powerMultiplier, _powerDuration, _powerColor);
    }
    
    private void PlayCollectEffects()
    {
        if (_collectParticles != null)
        {
            _collectParticles.Play();
        }
        
        if (_audioSource != null && _collectSound != null)
        {
            _audioSource.PlayOneShot(_collectSound);
        }
        
        if (_magicalLight != null)
        {
            StartCoroutine(FlashLight());
        }
    }
    
    private System.Collections.IEnumerator FlashLight()
    {
        float originalIntensity = _magicalLight.intensity;
        
        for (int i = 0; i < 3; i++)
        {
            _magicalLight.intensity = originalIntensity * 3f;
            yield return new WaitForSeconds(0.1f);
            _magicalLight.intensity = originalIntensity;
            yield return new WaitForSeconds(0.1f);
        }
    }
    
    private void HideFlower()
    {
        if (_flowerModel != null)
            _flowerModel.SetActive(false);
        
        if (_idleParticles != null)
            _idleParticles.Stop();
        
        if (_magicalLight != null)
            _magicalLight.enabled = false;
    }
    
    private void ShowFlower()
    {
        if (_flowerModel != null)
            _flowerModel.SetActive(true);
        
        if (_idleParticles != null)
            _idleParticles.Play();
        
        if (_magicalLight != null)
            _magicalLight.enabled = true;
    }
    
    private System.Collections.IEnumerator RespawnCoroutine()
    {
        yield return new WaitForSeconds(_respawnTime);
        
        _isAvailable = true;
        ShowFlower();
        OnFlowerRespawned?.Invoke();
        
        _respawnCoroutine = null;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }
}

public class PowerEffect : MonoBehaviour
{
    private MagicalFlower.PowerType _currentPowerType;
    private float _powerMultiplier;
    private float _remainingTime;
    private Color _powerColor;
    private bool _hasPower;
    
    private CharacterController _characterController;
    private Rigidbody _rigidbody;
    private Renderer _renderer;
    private Material _originalMaterial;
    private Material _powerMaterial;
    
    private float _originalSpeed;
    private bool _canDoubleJump;
    private bool _hasDoubleJumped;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        _renderer = GetComponentInChildren<Renderer>();
        
        if (_renderer != null)
        {
            _originalMaterial = _renderer.material;
        }
    }
    
    private void Update()
    {
        if (_hasPower)
        {
            _remainingTime -= Time.deltaTime;
            
            if (_remainingTime <= 0)
            {
                RemovePower();
            }
            else
            {
                UpdatePowerEffects();
            }
        }
    }
    
    public void ApplyPower(MagicalFlower.PowerType powerType, float multiplier, float duration, Color color)
    {
        if (_hasPower)
        {
            RemovePower();
        }
        
        _currentPowerType = powerType;
        _powerMultiplier = multiplier;
        _remainingTime = duration;
        _powerColor = color;
        _hasPower = true;
        
        ApplyVisualEffects();
        ApplyPowerSpecificEffects();
    }
    
    private void ApplyVisualEffects()
    {
        if (_renderer != null && _originalMaterial != null)
        {
            _powerMaterial = new Material(_originalMaterial);
            _powerMaterial.color = _powerColor;
            _powerMaterial.SetFloat("_Metallic", 0.8f);
            _powerMaterial.EnableKeyword("_EMISSION");
            _powerMaterial.SetColor("_EmissionColor", _powerColor * 0.5f);
            _renderer.material = _powerMaterial;
        }
    }
    
    private void ApplyPowerSpecificEffects()
    {
        switch (_currentPowerType)
        {
            case MagicalFlower.PowerType.SpeedBoost:
                if (_characterController != null)
                {
                    // Speed boost would be handled by movement script
                }
                break;
                
            case MagicalFlower.PowerType.DoubleJump:
                _canDoubleJump = true;
                _hasDoubleJumped = false;
                break;
                
            case MagicalFlower.PowerType.Invisibility:
                if (_renderer != null)
                {
                    Color invisColor = _renderer.material.color;
                    invisColor.a = 0.3f;
                    _renderer.material.color = invisColor;
                }
                break;
                
            case MagicalFlower.PowerType.SlowMotion:
                Time.timeScale = 0.5f;
                break;
        }
    }
    
    private void UpdatePowerEffects()
    {
        if (_renderer != null && _powerMaterial != null)
        {
            float pulse = Mathf.Sin(Time.time * 5f) * 0.3f + 0.7f;
            _powerMaterial.SetColor("_EmissionColor", _powerColor * pulse);
        }
    }
    
    private void RemovePower()
    {
        _hasPower = false;
        
        if (_renderer != null && _originalMaterial != null)
        {
            _renderer.material = _originalMaterial;
        }
        
        if (_powerMaterial != null)
        {
            DestroyImmediate(_powerMaterial);
        }
        
        switch (_currentPowerType)
        {
            case MagicalFlower.PowerType.DoubleJump:
                _canDoubleJump = false;
                break;
                
            case MagicalFlower.PowerType.SlowMotion:
                Time.timeScale = 1f;
                break;
        }
    }
    
    public bool HasPower()
    {
        return _hasPower;
    }
    
    public MagicalFlower.PowerType GetCurrentPowerType()
    {
        return _currentPowerType;
    }
    
    public float GetPowerMultiplier()
    {
        return _powerMultiplier;
    }
    
    public float GetRemainingTime()
    {
        return _remainingTime;
    }
    
    private void OnDestroy()
    {
        if (_currentPowerType == MagicalFlower.PowerType.SlowMotion)
        {
            Time.timeScale = 1f;
        }
        
        if (_powerMaterial != null)
        {
            DestroyImmediate(_powerMaterial);
        }
    }
}