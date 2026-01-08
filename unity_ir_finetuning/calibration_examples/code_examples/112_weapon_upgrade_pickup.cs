// Prompt: weapon upgrade pickup
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class WeaponUpgradePickup : MonoBehaviour
{
    [Header("Upgrade Configuration")]
    [SerializeField] private UpgradeType _upgradeType = UpgradeType.Damage;
    [SerializeField] private float _upgradeValue = 10f;
    [SerializeField] private string _upgradeName = "Damage Boost";
    [SerializeField] private string _upgradeDescription = "Increases weapon damage";
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    
    [Header("Pickup Settings")]
    [SerializeField] private bool _destroyOnPickup = true;
    [SerializeField] private float _pickupCooldown = 0.5f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Events")]
    public UnityEvent<WeaponUpgrade> OnUpgradePickedUp;
    public UnityEvent OnPickupDestroyed;
    
    private Vector3 _startPosition;
    private bool _canBePickedUp = true;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Collider _collider;
    
    public enum UpgradeType
    {
        Damage,
        FireRate,
        Range,
        Accuracy,
        AmmoCapacity,
        ReloadSpeed,
        CriticalChance,
        Penetration
    }
    
    [System.Serializable]
    public class WeaponUpgrade
    {
        public UpgradeType type;
        public float value;
        public string name;
        public string description;
        
        public WeaponUpgrade(UpgradeType upgradeType, float upgradeValue, string upgradeName, string upgradeDescription)
        {
            type = upgradeType;
            value = upgradeValue;
            name = upgradeName;
            description = upgradeDescription;
        }
    }
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
        }
        
        ValidateUpgradeSettings();
    }
    
    private void Update()
    {
        if (_canBePickedUp)
        {
            AnimatePickup();
        }
    }
    
    private void AnimatePickup()
    {
        // Rotate the pickup
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        
        // Bob up and down
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_canBePickedUp) return;
        
        if (IsPlayer(other))
        {
            PickupUpgrade(other.gameObject);
        }
    }
    
    private bool IsPlayer(Collider other)
    {
        return other.CompareTag("Player") && ((_playerLayer.value & (1 << other.gameObject.layer)) != 0);
    }
    
    private void PickupUpgrade(GameObject player)
    {
        _canBePickedUp = false;
        
        WeaponUpgrade upgrade = new WeaponUpgrade(_upgradeType, _upgradeValue, _upgradeName, _upgradeDescription);
        
        // Apply upgrade to player's weapon
        ApplyUpgradeToPlayer(player, upgrade);
        
        // Trigger events
        OnUpgradePickedUp?.Invoke(upgrade);
        
        // Play effects
        PlayPickupEffects();
        
        // Handle pickup completion
        if (_destroyOnPickup)
        {
            Invoke(nameof(DestroyPickup), 0.1f);
        }
        else
        {
            StartCooldown();
        }
    }
    
    private void ApplyUpgradeToPlayer(GameObject player, WeaponUpgrade upgrade)
    {
        // Look for weapon components on player or children
        WeaponStats weaponStats = player.GetComponentInChildren<WeaponStats>();
        if (weaponStats == null)
        {
            weaponStats = player.GetComponent<WeaponStats>();
        }
        
        if (weaponStats != null)
        {
            weaponStats.ApplyUpgrade(upgrade);
        }
        else
        {
            Debug.LogWarning($"No WeaponStats component found on player {player.name}. Upgrade not applied.");
        }
    }
    
    private void PlayPickupEffects()
    {
        // Play sound
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        // Spawn visual effect
        if (_pickupEffect != null)
        {
            GameObject effect = Instantiate(_pickupEffect, transform.position, transform.rotation);
            Destroy(effect, 3f);
        }
        
        // Hide visual
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
    
    private void StartCooldown()
    {
        Invoke(nameof(ResetPickup), _pickupCooldown);
    }
    
    private void ResetPickup()
    {
        _canBePickedUp = true;
        
        if (_renderer != null)
        {
            _renderer.enabled = true;
        }
        
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
    
    private void DestroyPickup()
    {
        OnPickupDestroyed?.Invoke();
        Destroy(gameObject);
    }
    
    private void ValidateUpgradeSettings()
    {
        if (string.IsNullOrEmpty(_upgradeName))
        {
            _upgradeName = _upgradeType.ToString() + " Upgrade";
        }
        
        if (string.IsNullOrEmpty(_upgradeDescription))
        {
            _upgradeDescription = $"Improves {_upgradeType.ToString().ToLower()}";
        }
        
        if (_upgradeValue <= 0)
        {
            _upgradeValue = 1f;
        }
    }
    
    public void SetUpgrade(UpgradeType type, float value, string name = "", string description = "")
    {
        _upgradeType = type;
        _upgradeValue = value;
        
        if (!string.IsNullOrEmpty(name))
            _upgradeName = name;
        
        if (!string.IsNullOrEmpty(description))
            _upgradeDescription = description;
        
        ValidateUpgradeSettings();
    }
    
    public WeaponUpgrade GetUpgradeInfo()
    {
        return new WeaponUpgrade(_upgradeType, _upgradeValue, _upgradeName, _upgradeDescription);
    }
}

[System.Serializable]
public class WeaponStats : MonoBehaviour
{
    [Header("Base Stats")]
    [SerializeField] private float _baseDamage = 25f;
    [SerializeField] private float _baseFireRate = 1f;
    [SerializeField] private float _baseRange = 100f;
    [SerializeField] private float _baseAccuracy = 0.95f;
    [SerializeField] private int _baseAmmoCapacity = 30;
    [SerializeField] private float _baseReloadSpeed = 2f;
    [SerializeField] private float _baseCriticalChance = 0.1f;
    [SerializeField] private int _basePenetration = 1;
    
    [Header("Current Stats")]
    public float currentDamage;
    public float currentFireRate;
    public float currentRange;
    public float currentAccuracy;
    public int currentAmmoCapacity;
    public float currentReloadSpeed;
    public float currentCriticalChance;
    public int currentPenetration;
    
    [Header("Upgrade Multipliers")]
    [SerializeField] private float _damageMultiplier = 1.1f;
    [SerializeField] private float _fireRateMultiplier = 1.15f;
    [SerializeField] private float _rangeMultiplier = 1.2f;
    [SerializeField] private float _accuracyMultiplier = 1.05f;
    [SerializeField] private float _ammoMultiplier = 1.25f;
    [SerializeField] private float _reloadSpeedMultiplier = 0.9f;
    [SerializeField] private float _criticalChanceMultiplier = 1.1f;
    [SerializeField] private int _penetrationBonus = 1;
    
    private void Start()
    {
        InitializeStats();
    }
    
    private void InitializeStats()
    {
        currentDamage = _baseDamage;
        currentFireRate = _baseFireRate;
        currentRange = _baseRange;
        currentAccuracy = _baseAccuracy;
        currentAmmoCapacity = _baseAmmoCapacity;
        currentReloadSpeed = _baseReloadSpeed;
        currentCriticalChance = _baseCriticalChance;
        currentPenetration = _basePenetration;
    }
    
    public void ApplyUpgrade(WeaponUpgradePickup.WeaponUpgrade upgrade)
    {
        switch (upgrade.type)
        {
            case WeaponUpgradePickup.UpgradeType.Damage:
                currentDamage += upgrade.value;
                break;
            case WeaponUpgradePickup.UpgradeType.FireRate:
                currentFireRate *= _fireRateMultiplier;
                break;
            case WeaponUpgradePickup.UpgradeType.Range:
                currentRange *= _rangeMultiplier;
                break;
            case WeaponUpgradePickup.UpgradeType.Accuracy:
                currentAccuracy = Mathf.Min(1f, currentAccuracy * _accuracyMultiplier);
                break;
            case WeaponUpgradePickup.UpgradeType.AmmoCapacity:
                currentAmmoCapacity = Mathf.RoundToInt(currentAmmoCapacity * _ammoMultiplier);
                break;
            case WeaponUpgradePickup.UpgradeType.ReloadSpeed:
                currentReloadSpeed *= _reloadSpeedMultiplier;
                break;
            case WeaponUpgradePickup.UpgradeType.CriticalChance:
                currentCriticalChance = Mathf.Min(1f, currentCriticalChance * _criticalChanceMultiplier);
                break;
            case WeaponUpgradePickup.UpgradeType.Penetration:
                currentPenetration += _penetrationBonus;
                break;
        }
        
        Debug.Log($"Applied {upgrade.name}: {upgrade.description}");
    }
    
    public void ResetToBaseStats()
    {
        InitializeStats();
    }
}