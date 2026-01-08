// Prompt: fishing spot
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class FishingSpot : MonoBehaviour
{
    [Header("Fishing Configuration")]
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private float _fishingDuration = 5f;
    [SerializeField] private float _catchChance = 0.7f;
    [SerializeField] private int _maxFishCount = 10;
    [SerializeField] private float _respawnTime = 30f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _rippleEffect;
    [SerializeField] private ParticleSystem _bubbleParticles;
    [SerializeField] private Animator _waterAnimator;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _castSound;
    [SerializeField] private AudioClip _catchSound;
    [SerializeField] private AudioClip _splashSound;
    
    [Header("Fish Types")]
    [SerializeField] private FishData[] _availableFish;
    
    [Header("Events")]
    public UnityEvent<FishData> OnFishCaught;
    public UnityEvent OnFishingStarted;
    public UnityEvent OnFishingFailed;
    public UnityEvent OnSpotDepleted;
    
    private Transform _playerTransform;
    private bool _isPlayerNearby;
    private bool _isFishing;
    private bool _isDepleted;
    private int _currentFishCount;
    private Coroutine _fishingCoroutine;
    private Coroutine _respawnCoroutine;
    
    [System.Serializable]
    public class FishData
    {
        public string fishName;
        public Sprite fishIcon;
        public float rarity; // 0-1, lower is rarer
        public int value;
        public float size;
    }
    
    private void Start()
    {
        _currentFishCount = _maxFishCount;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_bubbleParticles != null && !_isDepleted)
            _bubbleParticles.Play();
    }
    
    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
    }
    
    private void CheckPlayerProximity()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            _isPlayerNearby = distance <= _interactionRange && !_isDepleted;
        }
        else
        {
            _isPlayerNearby = false;
        }
    }
    
    private void HandleInput()
    {
        if (_isPlayerNearby && !_isFishing && Input.GetKeyDown(KeyCode.E))
        {
            StartFishing();
        }
        else if (_isFishing && Input.GetKeyDown(KeyCode.E))
        {
            StopFishing();
        }
    }
    
    private void StartFishing()
    {
        if (_isDepleted || _currentFishCount <= 0)
        {
            Debug.Log("Fishing spot is depleted!");
            return;
        }
        
        _isFishing = true;
        OnFishingStarted.Invoke();
        
        PlaySound(_castSound);
        
        if (_waterAnimator != null)
            _waterAnimator.SetBool("IsFishing", true);
            
        _fishingCoroutine = StartCoroutine(FishingProcess());
    }
    
    private void StopFishing()
    {
        if (_fishingCoroutine != null)
        {
            StopCoroutine(_fishingCoroutine);
            _fishingCoroutine = null;
        }
        
        _isFishing = false;
        
        if (_waterAnimator != null)
            _waterAnimator.SetBool("IsFishing", false);
    }
    
    private IEnumerator FishingProcess()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < _fishingDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Random chance for early bite
            if (Random.value < 0.1f)
            {
                yield return new WaitForSeconds(Random.Range(0.5f, 2f));
                break;
            }
            
            yield return null;
        }
        
        // Determine if catch is successful
        bool catchSuccessful = Random.value < _catchChance;
        
        if (catchSuccessful)
        {
            CatchFish();
        }
        else
        {
            FailedCatch();
        }
        
        _isFishing = false;
        
        if (_waterAnimator != null)
            _waterAnimator.SetBool("IsFishing", false);
    }
    
    private void CatchFish()
    {
        if (_availableFish.Length == 0) return;
        
        FishData caughtFish = SelectRandomFish();
        _currentFishCount--;
        
        PlaySound(_catchSound);
        CreateRippleEffect();
        
        OnFishCaught.Invoke(caughtFish);
        
        Debug.Log($"Caught: {caughtFish.fishName}!");
        
        if (_currentFishCount <= 0)
        {
            DepleteFishingSpot();
        }
    }
    
    private void FailedCatch()
    {
        PlaySound(_splashSound);
        OnFishingFailed.Invoke();
        Debug.Log("The fish got away!");
    }
    
    private FishData SelectRandomFish()
    {
        float totalWeight = 0f;
        foreach (var fish in _availableFish)
        {
            totalWeight += fish.rarity;
        }
        
        float randomValue = Random.value * totalWeight;
        float currentWeight = 0f;
        
        foreach (var fish in _availableFish)
        {
            currentWeight += fish.rarity;
            if (randomValue <= currentWeight)
            {
                return fish;
            }
        }
        
        return _availableFish[0];
    }
    
    private void CreateRippleEffect()
    {
        if (_rippleEffect != null)
        {
            GameObject ripple = Instantiate(_rippleEffect, transform.position, Quaternion.identity);
            Destroy(ripple, 3f);
        }
    }
    
    private void DepleteFishingSpot()
    {
        _isDepleted = true;
        
        if (_bubbleParticles != null)
            _bubbleParticles.Stop();
            
        OnSpotDepleted.Invoke();
        
        if (_respawnTime > 0)
        {
            _respawnCoroutine = StartCoroutine(RespawnFish());
        }
    }
    
    private IEnumerator RespawnFish()
    {
        yield return new WaitForSeconds(_respawnTime);
        
        _currentFishCount = _maxFishCount;
        _isDepleted = false;
        
        if (_bubbleParticles != null)
            _bubbleParticles.Play();
            
        Debug.Log("Fishing spot has been replenished!");
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
        
        if (_isDepleted)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + Vector3.up, Vector3.one * 0.5f);
        }
    }
    
    public bool IsAvailable()
    {
        return !_isDepleted && _currentFishCount > 0;
    }
    
    public int GetRemainingFish()
    {
        return _currentFishCount;
    }
    
    public void RefillSpot()
    {
        _currentFishCount = _maxFishCount;
        _isDepleted = false;
        
        if (_bubbleParticles != null)
            _bubbleParticles.Play();
    }
}