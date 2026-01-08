// Prompt: fireplace with warmth zone
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class Fireplace : MonoBehaviour
{
    [Header("Fire Settings")]
    [SerializeField] private GameObject fireEffect;
    [SerializeField] private AudioSource fireAudioSource;
    [SerializeField] private Light fireLight;
    [SerializeField] private bool isLit = true;
    [SerializeField] private float fuelDuration = 300f;
    [SerializeField] private float currentFuel = 300f;
    
    [Header("Warmth Zone")]
    [SerializeField] private float warmthRadius = 5f;
    [SerializeField] private float maxWarmthStrength = 100f;
    [SerializeField] private float warmthFalloffRate = 2f;
    [SerializeField] private LayerMask playerLayer = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private AnimationCurve flameSizeCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private AnimationCurve lightIntensityCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float baseFireScale = 1f;
    [SerializeField] private float baseLightIntensity = 2f;
    [SerializeField] private Color warmLightColor = Color.yellow;
    [SerializeField] private Color coolLightColor = Color.red;
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] cracklingSounds;
    [SerializeField] private float crackleInterval = 3f;
    [SerializeField] private float baseVolume = 0.5f;
    
    [Header("Fuel System")]
    [SerializeField] private bool requiresFuel = true;
    [SerializeField] private string fuelTag = "Wood";
    [SerializeField] private float fuelAddAmount = 60f;
    [SerializeField] private float interactionRange = 2f;
    
    [Header("Events")]
    public UnityEvent OnFireLit;
    public UnityEvent OnFireExtinguished;
    public UnityEvent OnPlayerEnterWarmth;
    public UnityEvent OnPlayerExitWarmth;
    
    private List<GameObject> _playersInWarmth = new List<GameObject>();
    private float _crackleTimer;
    private bool _playerNearby;
    private Vector3 _originalFireScale;
    private float _originalLightIntensity;
    
    [System.Serializable]
    public class WarmthData
    {
        public GameObject player;
        public float warmthLevel;
        public float timeInWarmth;
        
        public WarmthData(GameObject p)
        {
            player = p;
            warmthLevel = 0f;
            timeInWarmth = 0f;
        }
    }
    
    private List<WarmthData> _warmthDataList = new List<WarmthData>();
    
    void Start()
    {
        InitializeFireplace();
        SetupComponents();
    }
    
    void Update()
    {
        if (requiresFuel && isLit)
        {
            ConsumeFuel();
        }
        
        UpdateFireEffects();
        UpdateWarmthZone();
        HandleAudio();
        CheckForPlayerInteraction();
    }
    
    void InitializeFireplace()
    {
        if (fireEffect != null)
        {
            _originalFireScale = fireEffect.transform.localScale;
        }
        
        if (fireLight != null)
        {
            _originalLightIntensity = fireLight.intensity;
        }
        
        currentFuel = Mathf.Clamp(currentFuel, 0f, fuelDuration);
        
        if (currentFuel <= 0f)
        {
            ExtinguishFire();
        }
    }
    
    void SetupComponents()
    {
        if (fireAudioSource == null)
        {
            fireAudioSource = GetComponent<AudioSource>();
            if (fireAudioSource == null)
            {
                fireAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (fireAudioSource != null)
        {
            fireAudioSource.loop = true;
            fireAudioSource.volume = baseVolume;
        }
    }
    
    void ConsumeFuel()
    {
        currentFuel -= Time.deltaTime;
        currentFuel = Mathf.Max(0f, currentFuel);
        
        if (currentFuel <= 0f && isLit)
        {
            ExtinguishFire();
        }
    }
    
    void UpdateFireEffects()
    {
        float fuelPercentage = requiresFuel ? (currentFuel / fuelDuration) : 1f;
        
        if (fireEffect != null)
        {
            fireEffect.SetActive(isLit);
            if (isLit)
            {
                float scaleMultiplier = flameSizeCurve.Evaluate(fuelPercentage);
                fireEffect.transform.localScale = _originalFireScale * scaleMultiplier * baseFireScale;
            }
        }
        
        if (fireLight != null)
        {
            fireLight.enabled = isLit;
            if (isLit)
            {
                float intensityMultiplier = lightIntensityCurve.Evaluate(fuelPercentage);
                fireLight.intensity = _originalLightIntensity * intensityMultiplier * baseLightIntensity;
                fireLight.color = Color.Lerp(coolLightColor, warmLightColor, fuelPercentage);
                
                // Add flickering effect
                fireLight.intensity += Mathf.Sin(Time.time * 10f) * 0.1f * intensityMultiplier;
            }
        }
    }
    
    void UpdateWarmthZone()
    {
        if (!isLit) return;
        
        Collider[] playersInRange = Physics.OverlapSphere(transform.position, warmthRadius, playerLayer);
        
        // Remove players who left the warmth zone
        for (int i = _warmthDataList.Count - 1; i >= 0; i--)
        {
            bool stillInRange = false;
            foreach (var collider in playersInRange)
            {
                if (collider.gameObject == _warmthDataList[i].player)
                {
                    stillInRange = true;
                    break;
                }
            }
            
            if (!stillInRange)
            {
                OnPlayerExitWarmth.Invoke();
                _warmthDataList.RemoveAt(i);
            }
        }
        
        // Add new players and update existing ones
        foreach (var collider in playersInRange)
        {
            if (collider.CompareTag("Player"))
            {
                WarmthData existingData = _warmthDataList.Find(w => w.player == collider.gameObject);
                
                if (existingData == null)
                {
                    _warmthDataList.Add(new WarmthData(collider.gameObject));
                    OnPlayerEnterWarmth.Invoke();
                }
                else
                {
                    UpdatePlayerWarmth(existingData, collider.gameObject);
                }
            }
        }
    }
    
    void UpdatePlayerWarmth(WarmthData warmthData, GameObject player)
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        float warmthStrength = Mathf.Clamp01(1f - (distance / warmthRadius));
        warmthStrength = Mathf.Pow(warmthStrength, warmthFalloffRate);
        
        warmthData.warmthLevel = warmthStrength * maxWarmthStrength;
        warmthData.timeInWarmth += Time.deltaTime;
        
        // Apply warmth effect to player (you can extend this based on your needs)
        ApplyWarmthToPlayer(player, warmthData.warmthLevel);
    }
    
    void ApplyWarmthToPlayer(GameObject player, float warmthLevel)
    {
        // Send message to player about warmth level
        player.SendMessage("ReceiveWarmth", warmthLevel, SendMessageOptions.DontRequireReceiver);
    }
    
    void HandleAudio()
    {
        if (fireAudioSource == null) return;
        
        if (isLit)
        {
            float fuelPercentage = requiresFuel ? (currentFuel / fuelDuration) : 1f;
            fireAudioSource.volume = baseVolume * fuelPercentage;
            
            _crackleTimer -= Time.deltaTime;
            if (_crackleTimer <= 0f && cracklingSounds.Length > 0)
            {
                AudioClip randomCrackle = cracklingSounds[Random.Range(0, cracklingSounds.Length)];
                AudioSource.PlayClipAtPoint(randomCrackle, transform.position, baseVolume * fuelPercentage);
                _crackleTimer = crackleInterval + Random.Range(-1f, 1f);
            }
        }
        else
        {
            fireAudioSource.volume = 0f;
        }
    }
    
    void CheckForPlayerInteraction()
    {
        bool playerWasNearby = _playerNearby;
        _playerNearby = false;
        
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, interactionRange);
        foreach (var collider in nearbyObjects)
        {
            if (collider.CompareTag("Player"))
            {
                _playerNearby = true;
                
                if (Input.GetKeyDown(KeyCode.E))
                {
                    TryAddFuel(collider.gameObject);
                }
                break;
            }
        }
    }
    
    void TryAddFuel(GameObject player)
    {
        if (!requiresFuel) return;
        
        // Look for fuel in player's vicinity or inventory
        Collider[] nearbyItems = Physics.OverlapSphere(player.transform.position, 2f);
        foreach (var item in nearbyItems)
        {
            if (item.CompareTag(fuelTag))
            {
                AddFuel();
                Destroy(item.gameObject);
                break;
            }
        }
    }
    
    public void AddFuel()
    {
        currentFuel = Mathf.Min(currentFuel + fuelAddAmount, fuelDuration);
        
        if (!isLit && currentFuel > 0f)
        {
            LightFire();
        }
    }
    
    public void LightFire()
    {
        if (requiresFuel && currentFuel <= 0f) return;
        
        isLit = true;
        OnFireLit.Invoke();
    }
    
    public void ExtinguishFire()
    {
        isLit = false;
        OnFireExtinguished.Invoke();
        
        // Clear warmth data
        _warmthDataList.Clear();
    }
    
    public float GetWarmthAtPosition(Vector3 position)
    {
        if (!isLit) return 0f;
        
        float distance = Vector3.Distance(transform.position, position);
        if (distance > warmthRadius) return 0f;
        
        float warmthStrength = Mathf.Clamp01(1f - (distance / warmthRadius));
        return Mathf.Pow(warmthStrength, warmthFalloffRate) * maxWarmthStrength;
    }
    
    public bool IsPlayerInWarmth(GameObject player)
    {
        return _warmthDataList.Exists(w => w.player == player);
    }
    
    public float GetFuelPercentage()
    {
        return requiresFuel ? (currentFuel / fuelDuration) : 1f;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw warmth radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, warmthRadius);
        
        // Draw interaction range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
    }
}