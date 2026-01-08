// Prompt: fuel canister for vehicle refueling
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class FuelCanister : MonoBehaviour
{
    [Header("Fuel Settings")]
    [SerializeField] private float _fuelAmount = 50f;
    [SerializeField] private float _refuelRate = 10f;
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private bool _isConsumableOnUse = true;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _refuelSound;
    [SerializeField] private AudioClip _emptySound;
    [SerializeField] private AudioClip _pickupSound;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _fuelParticles;
    [SerializeField] private Material _emptyMaterial;
    [SerializeField] private float _bobHeight = 0.5f;
    [SerializeField] private float _bobSpeed = 2f;
    
    [Header("UI")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private Canvas _fuelAmountUI;
    [SerializeField] private UnityEngine.UI.Text _fuelAmountText;
    
    [Header("Events")]
    public UnityEvent<float> OnFuelUsed;
    public UnityEvent OnCanisterEmpty;
    public UnityEvent OnCanisterPickedUp;
    
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Material _originalMaterial;
    private Vector3 _originalPosition;
    private bool _isEmpty = false;
    private bool _isRefueling = false;
    private GameObject _currentVehicle;
    private Collider _collider;
    
    [System.Serializable]
    public class VehicleFuelSystem
    {
        public float currentFuel;
        public float maxFuel;
        public bool needsFuel;
        
        public VehicleFuelSystem(float maxFuelCapacity)
        {
            maxFuel = maxFuelCapacity;
            currentFuel = 0f;
            needsFuel = true;
        }
        
        public float AddFuel(float amount)
        {
            float previousFuel = currentFuel;
            currentFuel = Mathf.Clamp(currentFuel + amount, 0f, maxFuel);
            needsFuel = currentFuel < maxFuel;
            return currentFuel - previousFuel;
        }
        
        public bool IsFull()
        {
            return currentFuel >= maxFuel;
        }
        
        public float GetFuelPercentage()
        {
            return currentFuel / maxFuel;
        }
    }
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
            _originalMaterial = _renderer.material;
            
        _collider = GetComponent<Collider>();
        _originalPosition = transform.position;
        
        UpdateUI();
        
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
    }
    
    private void Update()
    {
        HandleBobAnimation();
        HandlePlayerInteraction();
        UpdateUI();
    }
    
    private void HandleBobAnimation()
    {
        if (!_isEmpty)
        {
            float newY = _originalPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = new Vector3(_originalPosition.x, newY, _originalPosition.z);
        }
    }
    
    private void HandlePlayerInteraction()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        bool playerInRange = distanceToPlayer <= _interactionRange;
        
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(playerInRange && !_isEmpty);
        
        if (playerInRange && Input.GetKeyDown(KeyCode.E) && !_isEmpty)
        {
            PickupCanister();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null)
                vehicle = other.GetComponent<VehicleController>();
                
            if (vehicle != null)
            {
                _currentVehicle = vehicle.gameObject;
            }
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (_isRefueling || _isEmpty) return;
        
        if (other.CompareTag("Player") && Input.GetKey(KeyCode.F))
        {
            VehicleController vehicle = other.GetComponentInParent<VehicleController>();
            if (vehicle == null)
                vehicle = other.GetComponent<VehicleController>();
                
            if (vehicle != null)
            {
                RefuelVehicle(vehicle);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _currentVehicle = null;
            _isRefueling = false;
        }
    }
    
    public void RefuelVehicle(VehicleController vehicle)
    {
        if (_isEmpty || _fuelAmount <= 0f) return;
        
        VehicleFuelSystem fuelSystem = vehicle.GetComponent<VehicleFuelSystem>();
        if (fuelSystem == null)
        {
            fuelSystem = vehicle.gameObject.AddComponent<VehicleFuelSystem>();
        }
        
        _isRefueling = true;
        
        float fuelToTransfer = Mathf.Min(_refuelRate * Time.deltaTime, _fuelAmount);
        float actualFuelTransferred = fuelSystem.AddFuel(fuelToTransfer);
        
        _fuelAmount -= actualFuelTransferred;
        OnFuelUsed?.Invoke(actualFuelTransferred);
        
        if (_fuelParticles != null && !_fuelParticles.activeInHierarchy)
            _fuelParticles.SetActive(true);
        
        PlayRefuelSound();
        
        if (_fuelAmount <= 0f)
        {
            SetEmpty();
        }
        
        if (fuelSystem.IsFull())
        {
            _isRefueling = false;
            if (_fuelParticles != null)
                _fuelParticles.SetActive(false);
        }
    }
    
    private void PlayRefuelSound()
    {
        if (_audioSource != null && _refuelSound != null && !_audioSource.isPlaying)
        {
            _audioSource.clip = _refuelSound;
            _audioSource.Play();
        }
    }
    
    private void SetEmpty()
    {
        _isEmpty = true;
        _isRefueling = false;
        
        if (_renderer != null && _emptyMaterial != null)
            _renderer.material = _emptyMaterial;
        
        if (_fuelParticles != null)
            _fuelParticles.SetActive(false);
        
        if (_audioSource != null && _emptySound != null)
        {
            _audioSource.clip = _emptySound;
            _audioSource.Play();
        }
        
        OnCanisterEmpty?.Invoke();
        
        if (_isConsumableOnUse)
        {
            Invoke(nameof(DestroyCanister), 2f);
        }
    }
    
    private void PickupCanister()
    {
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.clip = _pickupSound;
            _audioSource.Play();
        }
        
        OnCanisterPickedUp?.Invoke();
        
        if (_collider != null)
            _collider.enabled = false;
        
        gameObject.SetActive(false);
    }
    
    private void UpdateUI()
    {
        if (_fuelAmountText != null)
        {
            _fuelAmountText.text = $"Fuel: {_fuelAmount:F1}L";
        }
        
        if (_fuelAmountUI != null)
        {
            _fuelAmountUI.gameObject.SetActive(!_isEmpty);
        }
    }
    
    private void DestroyCanister()
    {
        Destroy(gameObject);
    }
    
    public float GetFuelAmount()
    {
        return _fuelAmount;
    }
    
    public bool IsEmpty()
    {
        return _isEmpty;
    }
    
    public void SetFuelAmount(float amount)
    {
        _fuelAmount = Mathf.Max(0f, amount);
        _isEmpty = _fuelAmount <= 0f;
        
        if (_isEmpty)
            SetEmpty();
    }
    
    public void RefillCanister(float amount)
    {
        _fuelAmount += amount;
        _isEmpty = false;
        
        if (_renderer != null && _originalMaterial != null)
            _renderer.material = _originalMaterial;
    }
}

public class VehicleController : MonoBehaviour
{
    [Header("Vehicle Fuel")]
    [SerializeField] private float _maxFuel = 100f;
    [SerializeField] private float _currentFuel = 50f;
    [SerializeField] private float _fuelConsumptionRate = 5f;
    [SerializeField] private UnityEngine.UI.Slider _fuelGauge;
    
    private FuelCanister.VehicleFuelSystem _fuelSystem;
    
    private void Start()
    {
        _fuelSystem = new FuelCanister.VehicleFuelSystem(_maxFuel);
        _fuelSystem.currentFuel = _currentFuel;
    }
    
    private void Update()
    {
        ConsumeFuel();
        UpdateFuelGauge();
    }
    
    private void ConsumeFuel()
    {
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S))
        {
            _fuelSystem.AddFuel(-_fuelConsumptionRate * Time.deltaTime);
        }
    }
    
    private void UpdateFuelGauge()
    {
        if (_fuelGauge != null)
        {
            _fuelGauge.value = _fuelSystem.GetFuelPercentage();
        }
    }
    
    public FuelCanister.VehicleFuelSystem GetFuelSystem()
    {
        return _fuelSystem;
    }
}