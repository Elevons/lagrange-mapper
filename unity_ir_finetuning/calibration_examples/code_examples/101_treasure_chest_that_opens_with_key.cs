// Prompt: treasure chest that opens with key
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class TreasureChest : MonoBehaviour
{
    [Header("Chest Settings")]
    [SerializeField] private string requiredKeyTag = "Key";
    [SerializeField] private float interactionRange = 2f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    
    [Header("Animation")]
    [SerializeField] private Animator chestAnimator;
    [SerializeField] private string openAnimationTrigger = "Open";
    [SerializeField] private AudioClip openSound;
    [SerializeField] private AudioClip lockedSound;
    
    [Header("Treasure Contents")]
    [SerializeField] private GameObject[] treasureItems;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private float spawnForce = 5f;
    
    [Header("UI")]
    [SerializeField] private GameObject interactionPrompt;
    [SerializeField] private GameObject lockedPrompt;
    
    [Header("Events")]
    public UnityEvent OnChestOpened;
    public UnityEvent OnChestLocked;
    
    private bool _isOpened = false;
    private bool _playerInRange = false;
    private GameObject _playerObject;
    private AudioSource _audioSource;
    private PlayerInventory _playerInventory;
    
    [System.Serializable]
    private class PlayerInventory
    {
        private bool _hasKey = false;
        
        public bool HasKey => _hasKey;
        
        public void AddKey()
        {
            _hasKey = true;
        }
        
        public bool UseKey()
        {
            if (_hasKey)
            {
                _hasKey = false;
                return true;
            }
            return false;
        }
    }
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);
            
        if (lockedPrompt != null)
            lockedPrompt.SetActive(false);
            
        if (spawnPoint == null)
            spawnPoint = transform;
    }
    
    private void Update()
    {
        if (_playerInRange && !_isOpened && Input.GetKeyDown(interactionKey))
        {
            TryOpenChest();
        }
        
        UpdateUI();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
            _playerObject = other.gameObject;
            
            _playerInventory = other.GetComponent<PlayerInventory>();
            if (_playerInventory == null)
            {
                _playerInventory = other.gameObject.AddComponent<PlayerInventory>();
            }
        }
        else if (other.CompareTag(requiredKeyTag))
        {
            CollectKey(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
            _playerObject = null;
            _playerInventory = null;
            
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
                
            if (lockedPrompt != null)
                lockedPrompt.SetActive(false);
        }
    }
    
    private void CollectKey(GameObject keyObject)
    {
        if (_playerInventory != null)
        {
            _playerInventory.AddKey();
            Destroy(keyObject);
        }
    }
    
    private void TryOpenChest()
    {
        if (_playerInventory != null && _playerInventory.HasKey)
        {
            if (_playerInventory.UseKey())
            {
                OpenChest();
            }
        }
        else
        {
            PlayLockedSound();
            OnChestLocked?.Invoke();
        }
    }
    
    private void OpenChest()
    {
        _isOpened = true;
        
        if (chestAnimator != null)
            chestAnimator.SetTrigger(openAnimationTrigger);
            
        PlayOpenSound();
        SpawnTreasure();
        OnChestOpened?.Invoke();
    }
    
    private void SpawnTreasure()
    {
        if (treasureItems == null || treasureItems.Length == 0)
            return;
            
        foreach (GameObject treasureItem in treasureItems)
        {
            if (treasureItem != null)
            {
                Vector3 spawnPosition = spawnPoint.position + Random.insideUnitSphere * 0.5f;
                GameObject spawnedItem = Instantiate(treasureItem, spawnPosition, Random.rotation);
                
                Rigidbody rb = spawnedItem.GetComponent<Rigidbody>();
                if (rb == null)
                    rb = spawnedItem.AddComponent<Rigidbody>();
                    
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y);
                rb.AddForce(randomDirection * spawnForce, ForceMode.Impulse);
            }
        }
    }
    
    private void UpdateUI()
    {
        if (!_playerInRange || _isOpened)
        {
            if (interactionPrompt != null)
                interactionPrompt.SetActive(false);
            if (lockedPrompt != null)
                lockedPrompt.SetActive(false);
            return;
        }
        
        bool hasKey = _playerInventory != null && _playerInventory.HasKey;
        
        if (interactionPrompt != null)
            interactionPrompt.SetActive(hasKey);
            
        if (lockedPrompt != null)
            lockedPrompt.SetActive(!hasKey);
    }
    
    private void PlayOpenSound()
    {
        if (openSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(openSound);
        }
    }
    
    private void PlayLockedSound()
    {
        if (lockedSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(lockedSound);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);
        
        if (spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(spawnPoint.position, Vector3.one * 0.2f);
        }
    }
}