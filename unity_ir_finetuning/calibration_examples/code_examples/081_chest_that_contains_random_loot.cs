// Prompt: chest that contains random loot
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class LootChest : MonoBehaviour
{
    [System.Serializable]
    public class LootItem
    {
        public GameObject itemPrefab;
        public int minQuantity = 1;
        public int maxQuantity = 1;
        [Range(0f, 100f)]
        public float dropChance = 100f;
    }

    [Header("Chest Settings")]
    [SerializeField] private bool _isOpen = false;
    [SerializeField] private bool _canReopen = false;
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    [Header("Loot Configuration")]
    [SerializeField] private List<LootItem> _possibleLoot = new List<LootItem>();
    [SerializeField] private int _minItemsToSpawn = 1;
    [SerializeField] private int _maxItemsToSpawn = 3;
    [SerializeField] private float _lootSpawnRadius = 1.5f;
    [SerializeField] private float _lootSpawnForce = 5f;

    [Header("Visual Effects")]
    [SerializeField] private Animator _chestAnimator;
    [SerializeField] private ParticleSystem _openEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _alreadyOpenSound;

    [Header("UI")]
    [SerializeField] private GameObject _interactionPrompt;

    [Header("Events")]
    public UnityEvent OnChestOpened;
    public UnityEvent OnChestAlreadyOpen;

    private Transform _player;
    private bool _playerInRange = false;

    private void Start()
    {
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);

        if (_chestAnimator == null)
            _chestAnimator = GetComponent<Animator>();

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        UpdateChestVisuals();
    }

    private void Update()
    {
        CheckForPlayer();
        HandleInteraction();
    }

    private void CheckForPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _player = player.transform;
            float distance = Vector3.Distance(transform.position, _player.position);
            bool wasInRange = _playerInRange;
            _playerInRange = distance <= _interactionRange;

            if (_playerInRange != wasInRange)
            {
                UpdateInteractionPrompt();
            }
        }
        else
        {
            if (_playerInRange)
            {
                _playerInRange = false;
                UpdateInteractionPrompt();
            }
        }
    }

    private void HandleInteraction()
    {
        if (_playerInRange && Input.GetKeyDown(_interactionKey))
        {
            OpenChest();
        }
    }

    private void UpdateInteractionPrompt()
    {
        if (_interactionPrompt != null)
        {
            bool shouldShow = _playerInRange && (!_isOpen || _canReopen);
            _interactionPrompt.SetActive(shouldShow);
        }
    }

    public void OpenChest()
    {
        if (_isOpen && !_canReopen)
        {
            PlayAlreadyOpenFeedback();
            return;
        }

        _isOpen = true;
        UpdateChestVisuals();
        SpawnLoot();
        PlayOpenEffects();
        OnChestOpened?.Invoke();
        UpdateInteractionPrompt();
    }

    private void SpawnLoot()
    {
        if (_possibleLoot.Count == 0) return;

        int itemsToSpawn = Random.Range(_minItemsToSpawn, _maxItemsToSpawn + 1);
        
        for (int i = 0; i < itemsToSpawn; i++)
        {
            LootItem selectedLoot = SelectRandomLoot();
            if (selectedLoot != null && selectedLoot.itemPrefab != null)
            {
                SpawnLootItem(selectedLoot);
            }
        }
    }

    private LootItem SelectRandomLoot()
    {
        List<LootItem> availableLoot = new List<LootItem>();
        
        foreach (LootItem loot in _possibleLoot)
        {
            if (Random.Range(0f, 100f) <= loot.dropChance)
            {
                availableLoot.Add(loot);
            }
        }

        if (availableLoot.Count == 0) return null;
        
        return availableLoot[Random.Range(0, availableLoot.Count)];
    }

    private void SpawnLootItem(LootItem lootItem)
    {
        int quantity = Random.Range(lootItem.minQuantity, lootItem.maxQuantity + 1);
        
        for (int i = 0; i < quantity; i++)
        {
            Vector3 spawnPosition = GetRandomSpawnPosition();
            GameObject spawnedItem = Instantiate(lootItem.itemPrefab, spawnPosition, Random.rotation);
            
            Rigidbody rb = spawnedItem.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 randomDirection = Random.insideUnitSphere;
                randomDirection.y = Mathf.Abs(randomDirection.y);
                rb.AddForce(randomDirection * _lootSpawnForce, ForceMode.Impulse);
            }
        }
    }

    private Vector3 GetRandomSpawnPosition()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _lootSpawnRadius;
        Vector3 spawnOffset = new Vector3(randomCircle.x, 1f, randomCircle.y);
        return transform.position + spawnOffset;
    }

    private void UpdateChestVisuals()
    {
        if (_chestAnimator != null)
        {
            _chestAnimator.SetBool("IsOpen", _isOpen);
        }
    }

    private void PlayOpenEffects()
    {
        if (_openEffect != null)
        {
            _openEffect.Play();
        }

        if (_audioSource != null && _openSound != null)
        {
            _audioSource.PlayOneShot(_openSound);
        }
    }

    private void PlayAlreadyOpenFeedback()
    {
        if (_audioSource != null && _alreadyOpenSound != null)
        {
            _audioSource.PlayOneShot(_alreadyOpenSound);
        }

        OnChestAlreadyOpen?.Invoke();
    }

    public void ResetChest()
    {
        _isOpen = false;
        UpdateChestVisuals();
        UpdateInteractionPrompt();
    }

    public void AddLootItem(GameObject itemPrefab, int minQty = 1, int maxQty = 1, float chance = 100f)
    {
        LootItem newLoot = new LootItem
        {
            itemPrefab = itemPrefab,
            minQuantity = minQty,
            maxQuantity = maxQty,
            dropChance = chance
        };
        _possibleLoot.Add(newLoot);
    }

    public void ClearLoot()
    {
        _possibleLoot.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _lootSpawnRadius);
    }
}