// Prompt: ammo box that refills weapon ammunition
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class AmmoBox : MonoBehaviour
{
    [System.Serializable]
    public class AmmoRefillEvent : UnityEvent<int> { }
    
    [System.Serializable]
    public class AmmoType
    {
        public string ammoName = "Bullets";
        public int refillAmount = 30;
        public bool unlimitedRefills = false;
        public int maxRefills = 1;
        [HideInInspector] public int currentRefills = 0;
    }
    
    [Header("Ammo Configuration")]
    [SerializeField] private AmmoType[] _ammoTypes = new AmmoType[] { new AmmoType() };
    [SerializeField] private bool _refillAllAmmoTypes = true;
    [SerializeField] private float _refillCooldown = 1f;
    
    [Header("Interaction")]
    [SerializeField] private bool _requireKeyPress = false;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private LayerMask _playerLayer = -1;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _visualEffect;
    [SerializeField] private AudioClip _refillSound;
    [SerializeField] private ParticleSystem _refillParticles;
    [SerializeField] private Animator _animator;
    [SerializeField] private string _refillAnimationTrigger = "Refill";
    
    [Header("Auto Destruction")]
    [SerializeField] private bool _destroyWhenEmpty = true;
    [SerializeField] private float _destroyDelay = 0.5f;
    
    [Header("Events")]
    public AmmoRefillEvent OnAmmoRefilled;
    public UnityEvent OnAmmoBoxEmpty;
    public UnityEvent OnPlayerInRange;
    public UnityEvent OnPlayerOutOfRange;
    
    private AudioSource _audioSource;
    private bool _playerInRange = false;
    private GameObject _currentPlayer;
    private float _lastRefillTime = -1f;
    private bool _isEmpty = false;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _refillSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Initialize refill counters
        foreach (var ammoType in _ammoTypes)
        {
            ammoType.currentRefills = 0;
        }
        
        // Ensure we have a collider for trigger detection
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<BoxCollider>();
        }
        col.isTrigger = true;
    }
    
    private void Update()
    {
        if (_isEmpty) return;
        
        if (_requireKeyPress && _playerInRange && _currentPlayer != null)
        {
            if (Input.GetKeyDown(_interactionKey))
            {
                TryRefillAmmo(_currentPlayer);
            }
        }
        
        // Check if player is still in range when using key interaction
        if (_requireKeyPress && _currentPlayer != null)
        {
            float distance = Vector3.Distance(transform.position, _currentPlayer.transform.position);
            if (distance > _interactionRange)
            {
                OnPlayerExitRange();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isEmpty) return;
        
        if (IsPlayer(other.gameObject))
        {
            if (_requireKeyPress)
            {
                OnPlayerEnterRange(other.gameObject);
            }
            else
            {
                TryRefillAmmo(other.gameObject);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject) && _requireKeyPress)
        {
            OnPlayerExitRange();
        }
    }
    
    private bool IsPlayer(GameObject obj)
    {
        return obj.CompareTag("Player") && ((_playerLayer.value & (1 << obj.layer)) != 0);
    }
    
    private void OnPlayerEnterRange(GameObject player)
    {
        _playerInRange = true;
        _currentPlayer = player;
        OnPlayerInRange?.Invoke();
    }
    
    private void OnPlayerExitRange()
    {
        _playerInRange = false;
        _currentPlayer = null;
        OnPlayerOutOfRange?.Invoke();
    }
    
    private void TryRefillAmmo(GameObject player)
    {
        if (_isEmpty || Time.time - _lastRefillTime < _refillCooldown)
            return;
        
        // Find weapon components on player
        WeaponAmmo[] weapons = player.GetComponentsInChildren<WeaponAmmo>();
        if (weapons.Length == 0)
        {
            // Try to find weapon on player directly
            WeaponAmmo weapon = player.GetComponent<WeaponAmmo>();
            if (weapon != null)
            {
                weapons = new WeaponAmmo[] { weapon };
            }
        }
        
        if (weapons.Length == 0) return;
        
        bool refillSuccessful = false;
        
        foreach (var ammoType in _ammoTypes)
        {
            if (!ammoType.unlimitedRefills && ammoType.currentRefills >= ammoType.maxRefills)
                continue;
            
            foreach (var weapon in weapons)
            {
                if (_refillAllAmmoTypes || weapon.AmmoTypeName == ammoType.ammoName)
                {
                    if (weapon.RefillAmmo(ammoType.refillAmount))
                    {
                        refillSuccessful = true;
                        ammoType.currentRefills++;
                        OnAmmoRefilled?.Invoke(ammoType.refillAmount);
                        
                        if (!_refillAllAmmoTypes) break;
                    }
                }
            }
        }
        
        if (refillSuccessful)
        {
            _lastRefillTime = Time.time;
            PlayRefillEffects();
            
            if (IsEmpty())
            {
                HandleEmptyAmmoBox();
            }
        }
    }
    
    private void PlayRefillEffects()
    {
        if (_refillSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_refillSound);
        }
        
        if (_refillParticles != null)
        {
            _refillParticles.Play();
        }
        
        if (_animator != null && !string.IsNullOrEmpty(_refillAnimationTrigger))
        {
            _animator.SetTrigger(_refillAnimationTrigger);
        }
        
        if (_visualEffect != null)
        {
            _visualEffect.SetActive(true);
            Invoke(nameof(DisableVisualEffect), 1f);
        }
    }
    
    private void DisableVisualEffect()
    {
        if (_visualEffect != null)
        {
            _visualEffect.SetActive(false);
        }
    }
    
    private bool IsEmpty()
    {
        foreach (var ammoType in _ammoTypes)
        {
            if (ammoType.unlimitedRefills || ammoType.currentRefills < ammoType.maxRefills)
            {
                return false;
            }
        }
        return true;
    }
    
    private void HandleEmptyAmmoBox()
    {
        _isEmpty = true;
        OnAmmoBoxEmpty?.Invoke();
        
        if (_destroyWhenEmpty)
        {
            Destroy(gameObject, _destroyDelay);
        }
    }
    
    public void RefillAmmoBox()
    {
        foreach (var ammoType in _ammoTypes)
        {
            ammoType.currentRefills = 0;
        }
        _isEmpty = false;
    }
    
    public int GetRemainingRefills(string ammoTypeName)
    {
        foreach (var ammoType in _ammoTypes)
        {
            if (ammoType.ammoName == ammoTypeName)
            {
                if (ammoType.unlimitedRefills) return -1;
                return ammoType.maxRefills - ammoType.currentRefills;
            }
        }
        return 0;
    }
}

[System.Serializable]
public class WeaponAmmo : MonoBehaviour
{
    [Header("Ammo Settings")]
    [SerializeField] private string _ammoTypeName = "Bullets";
    [SerializeField] private int _currentAmmo = 30;
    [SerializeField] private int _maxAmmo = 120;
    [SerializeField] private int _clipSize = 30;
    [SerializeField] private int _currentClip = 30;
    
    public string AmmoTypeName => _ammoTypeName;
    public int CurrentAmmo => _currentAmmo;
    public int MaxAmmo => _maxAmmo;
    public int CurrentClip => _currentClip;
    
    public bool RefillAmmo(int amount)
    {
        if (_currentAmmo >= _maxAmmo) return false;
        
        int ammoToAdd = Mathf.Min(amount, _maxAmmo - _currentAmmo);
        _currentAmmo += ammoToAdd;
        
        return ammoToAdd > 0;
    }
    
    public bool UseAmmo(int amount = 1)
    {
        if (_currentClip < amount) return false;
        
        _currentClip -= amount;
        return true;
    }
    
    public bool Reload()
    {
        if (_currentAmmo <= 0 || _currentClip >= _clipSize) return false;
        
        int ammoNeeded = _clipSize - _currentClip;
        int ammoToReload = Mathf.Min(ammoNeeded, _currentAmmo);
        
        _currentClip += ammoToReload;
        _currentAmmo -= ammoToReload;
        
        return ammoToReload > 0;
    }
}