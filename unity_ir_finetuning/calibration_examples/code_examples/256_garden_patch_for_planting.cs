// Prompt: garden patch for planting
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class GardenPatch : MonoBehaviour
{
    [System.Serializable]
    public class PlantData
    {
        public string plantName;
        public GameObject plantPrefab;
        public float growthTime;
        public int harvestAmount;
        public Sprite plantIcon;
    }

    [System.Serializable]
    public class GrowthStage
    {
        public GameObject stagePrefab;
        public float stageTime;
    }

    [Header("Garden Configuration")]
    [SerializeField] private List<PlantData> _availablePlants = new List<PlantData>();
    [SerializeField] private List<GrowthStage> _growthStages = new List<GrowthStage>();
    [SerializeField] private Transform _plantSpawnPoint;
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private LayerMask _playerLayer = -1;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject _soilPrefab;
    [SerializeField] private GameObject _wateredSoilPrefab;
    [SerializeField] private GameObject _highlightEffect;
    [SerializeField] private ParticleSystem _plantingEffect;
    [SerializeField] private ParticleSystem _harvestEffect;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _plantingSound;
    [SerializeField] private AudioClip _harvestSound;
    [SerializeField] private AudioClip _wateringSound;

    [Header("Events")]
    public UnityEvent<string> OnPlantPlanted;
    public UnityEvent<string, int> OnPlantHarvested;
    public UnityEvent OnPatchWatered;

    private enum PatchState
    {
        Empty,
        Planted,
        Growing,
        ReadyToHarvest,
        Watered
    }

    private PatchState _currentState = PatchState.Empty;
    private PlantData _currentPlant;
    private GameObject _currentPlantObject;
    private GameObject _currentSoilObject;
    private float _plantingTime;
    private float _wateringTime;
    private int _currentGrowthStage;
    private bool _isWatered;
    private bool _playerInRange;

    private void Start()
    {
        InitializePatch();
        SetupAudioSource();
    }

    private void Update()
    {
        CheckPlayerProximity();
        HandleGrowth();
        HandleWatering();
        UpdateVisuals();
        HandleInput();
    }

    private void InitializePatch()
    {
        if (_plantSpawnPoint == null)
            _plantSpawnPoint = transform;

        if (_soilPrefab != null)
        {
            _currentSoilObject = Instantiate(_soilPrefab, _plantSpawnPoint.position, _plantSpawnPoint.rotation);
            _currentSoilObject.transform.SetParent(transform);
        }

        if (_highlightEffect != null)
            _highlightEffect.SetActive(false);
    }

    private void SetupAudioSource()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }

    private void CheckPlayerProximity()
    {
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, _interactionRange, _playerLayer);
        bool playerNearby = false;

        foreach (Collider col in playersInRange)
        {
            if (col.CompareTag("Player"))
            {
                playerNearby = true;
                break;
            }
        }

        if (playerNearby != _playerInRange)
        {
            _playerInRange = playerNearby;
            if (_highlightEffect != null)
                _highlightEffect.SetActive(_playerInRange);
        }
    }

    private void HandleGrowth()
    {
        if (_currentState != PatchState.Growing)
            return;

        float growthProgress = (Time.time - _plantingTime) / _currentPlant.growthTime;
        
        if (_isWatered)
            growthProgress *= 1.5f;

        UpdateGrowthStage(growthProgress);

        if (growthProgress >= 1f)
        {
            _currentState = PatchState.ReadyToHarvest;
            ShowFinalGrowthStage();
        }
    }

    private void UpdateGrowthStage(float progress)
    {
        if (_growthStages.Count == 0)
            return;

        int targetStage = Mathf.FloorToInt(progress * _growthStages.Count);
        targetStage = Mathf.Clamp(targetStage, 0, _growthStages.Count - 1);

        if (targetStage != _currentGrowthStage)
        {
            _currentGrowthStage = targetStage;
            ShowGrowthStage(targetStage);
        }
    }

    private void ShowGrowthStage(int stageIndex)
    {
        if (_currentPlantObject != null)
            DestroyImmediate(_currentPlantObject);

        if (stageIndex < _growthStages.Count && _growthStages[stageIndex].stagePrefab != null)
        {
            _currentPlantObject = Instantiate(_growthStages[stageIndex].stagePrefab, 
                _plantSpawnPoint.position, _plantSpawnPoint.rotation);
            _currentPlantObject.transform.SetParent(transform);
        }
    }

    private void ShowFinalGrowthStage()
    {
        if (_currentPlantObject != null)
            DestroyImmediate(_currentPlantObject);

        if (_currentPlant.plantPrefab != null)
        {
            _currentPlantObject = Instantiate(_currentPlant.plantPrefab, 
                _plantSpawnPoint.position, _plantSpawnPoint.rotation);
            _currentPlantObject.transform.SetParent(transform);
        }
    }

    private void HandleWatering()
    {
        if (_isWatered && Time.time - _wateringTime > 300f) // 5 minutes
        {
            _isWatered = false;
            UpdateSoilVisual();
        }
    }

    private void UpdateVisuals()
    {
        // Update soil visual based on watering state
        if (_currentState == PatchState.Empty || _currentState == PatchState.Watered)
        {
            UpdateSoilVisual();
        }
    }

    private void UpdateSoilVisual()
    {
        if (_currentSoilObject != null)
            DestroyImmediate(_currentSoilObject);

        GameObject soilPrefab = _isWatered ? _wateredSoilPrefab : _soilPrefab;
        if (soilPrefab != null)
        {
            _currentSoilObject = Instantiate(soilPrefab, _plantSpawnPoint.position, _plantSpawnPoint.rotation);
            _currentSoilObject.transform.SetParent(transform);
        }
    }

    private void HandleInput()
    {
        if (!_playerInRange)
            return;

        if (Input.GetKeyDown(KeyCode.E))
        {
            InteractWithPatch();
        }
        else if (Input.GetKeyDown(KeyCode.R))
        {
            WaterPatch();
        }
    }

    public void InteractWithPatch()
    {
        switch (_currentState)
        {
            case PatchState.Empty:
            case PatchState.Watered:
                TryPlantSeed();
                break;
            case PatchState.ReadyToHarvest:
                HarvestPlant();
                break;
        }
    }

    public void PlantSeed(int plantIndex)
    {
        if (plantIndex < 0 || plantIndex >= _availablePlants.Count)
            return;

        if (_currentState != PatchState.Empty && _currentState != PatchState.Watered)
            return;

        _currentPlant = _availablePlants[plantIndex];
        _currentState = PatchState.Growing;
        _plantingTime = Time.time;
        _currentGrowthStage = -1;

        PlaySound(_plantingSound);
        PlayEffect(_plantingEffect);
        OnPlantPlanted?.Invoke(_currentPlant.plantName);

        if (_currentSoilObject != null)
            _currentSoilObject.SetActive(false);
    }

    private void TryPlantSeed()
    {
        if (_availablePlants.Count > 0)
        {
            PlantSeed(0); // Plant first available seed
        }
    }

    public void HarvestPlant()
    {
        if (_currentState != PatchState.ReadyToHarvest || _currentPlant == null)
            return;

        string harvestedPlant = _currentPlant.plantName;
        int harvestAmount = _currentPlant.harvestAmount;

        // Clean up current plant
        if (_currentPlantObject != null)
            DestroyImmediate(_currentPlantObject);

        // Reset patch state
        _currentState = PatchState.Empty;
        _currentPlant = null;
        _currentGrowthStage = -1;
        _isWatered = false;

        // Show soil again
        if (_currentSoilObject != null)
            _currentSoilObject.SetActive(true);
        else
            UpdateSoilVisual();

        PlaySound(_harvestSound);
        PlayEffect(_harvestEffect);
        OnPlantHarvested?.Invoke(harvestedPlant, harvestAmount);
    }

    public void WaterPatch()
    {
        if (_isWatered)
            return;

        _isWatered = true;
        _wateringTime = Time.time;

        if (_currentState == PatchState.Empty)
            _currentState = PatchState.Watered;

        UpdateSoilVisual();
        PlaySound(_wateringSound);
        OnPatchWatered?.Invoke();
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }

    private void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
            effect.Play();
    }

    public bool CanPlant()
    {
        return _currentState == PatchState.Empty || _currentState == PatchState.Watered;
    }

    public bool CanHarvest()
    {
        return _currentState == PatchState.ReadyToHarvest;
    }

    public bool IsWatered()
    {
        return _isWatered;
    }

    public float GetGrowthProgress()
    {
        if (_currentState != PatchState.Growing || _currentPlant == null)
            return 0f;

        float progress = (Time.time - _plantingTime) / _currentPlant.growthTime;
        if (_isWatered)
            progress *= 1.5f;

        return Mathf.Clamp01(progress);
    }

    public string GetCurrentPlantName()
    {
        return _currentPlant?.plantName ?? "";
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }
}