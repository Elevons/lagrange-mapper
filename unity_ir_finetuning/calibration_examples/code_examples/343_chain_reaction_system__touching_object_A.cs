// Prompt: chain reaction system: touching object A explodes and spawns 5 objects of type B in circle, each B when touched spawns 3 objects of type C, each C spawns 2 of type D, each D spawns 1 of type E which plays victory sound
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class ChainReactionSystem : MonoBehaviour
{
    [System.Serializable]
    public class ReactionLevel
    {
        [Header("Spawn Settings")]
        public GameObject prefab;
        public int spawnCount;
        public float spawnRadius = 2f;
        public float spawnForce = 5f;
        
        [Header("Visual Effects")]
        public GameObject explosionEffect;
        public AudioClip explosionSound;
        
        [Header("Timing")]
        public float destructionDelay = 0.1f;
    }
    
    [Header("Chain Reaction Configuration")]
    [SerializeField] private ReactionLevel[] _reactionLevels = new ReactionLevel[5];
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _victorySound;
    
    [Header("Detection")]
    [SerializeField] private LayerMask _triggerLayers = -1;
    [SerializeField] private string _triggerTag = "Player";
    
    [Header("Current Level")]
    [SerializeField] private int _currentLevel = 0;
    
    private bool _hasTriggered = false;
    private Rigidbody _rigidbody;
    private Collider _collider;
    
    public UnityEvent OnChainReactionTriggered;
    public UnityEvent OnVictoryReached;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        SetupCollider();
        InitializeReactionLevels();
    }
    
    private void SetupCollider()
    {
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
        }
        
        if (!_collider.isTrigger)
            _collider.isTrigger = true;
    }
    
    private void InitializeReactionLevels()
    {
        if (_reactionLevels == null || _reactionLevels.Length == 0)
        {
            _reactionLevels = new ReactionLevel[5];
            for (int i = 0; i < _reactionLevels.Length; i++)
            {
                _reactionLevels[i] = new ReactionLevel();
            }
        }
        
        // Set default spawn counts if not configured
        if (_reactionLevels.Length >= 1 && _reactionLevels[0].spawnCount == 0)
            _reactionLevels[0].spawnCount = 5; // A spawns 5 B
            
        if (_reactionLevels.Length >= 2 && _reactionLevels[1].spawnCount == 0)
            _reactionLevels[1].spawnCount = 3; // B spawns 3 C
            
        if (_reactionLevels.Length >= 3 && _reactionLevels[2].spawnCount == 0)
            _reactionLevels[2].spawnCount = 2; // C spawns 2 D
            
        if (_reactionLevels.Length >= 4 && _reactionLevels[3].spawnCount == 0)
            _reactionLevels[3].spawnCount = 1; // D spawns 1 E
            
        if (_reactionLevels.Length >= 5 && _reactionLevels[4].spawnCount == 0)
            _reactionLevels[4].spawnCount = 0; // E is final level
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasTriggered)
            return;
            
        if (!IsValidTrigger(other))
            return;
            
        TriggerChainReaction();
    }
    
    private bool IsValidTrigger(Collider other)
    {
        // Check layer mask
        int otherLayer = 1 << other.gameObject.layer;
        if ((_triggerLayers.value & otherLayer) == 0)
            return false;
            
        // Check tag if specified
        if (!string.IsNullOrEmpty(_triggerTag) && !other.CompareTag(_triggerTag))
            return false;
            
        return true;
    }
    
    private void TriggerChainReaction()
    {
        if (_hasTriggered)
            return;
            
        _hasTriggered = true;
        
        OnChainReactionTriggered?.Invoke();
        
        // Check if this is the final level (E)
        if (_currentLevel >= _reactionLevels.Length - 1 || _reactionLevels[_currentLevel].spawnCount == 0)
        {
            PlayVictorySound();
            OnVictoryReached?.Invoke();
        }
        else
        {
            StartCoroutine(ExecuteReaction());
        }
    }
    
    private IEnumerator ExecuteReaction()
    {
        ReactionLevel currentReaction = _reactionLevels[_currentLevel];
        
        // Play explosion sound
        if (currentReaction.explosionSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(currentReaction.explosionSound);
        }
        
        // Spawn explosion effect
        if (currentReaction.explosionEffect != null)
        {
            Instantiate(currentReaction.explosionEffect, transform.position, transform.rotation);
        }
        
        // Spawn next level objects
        if (currentReaction.prefab != null && currentReaction.spawnCount > 0)
        {
            SpawnNextLevelObjects(currentReaction);
        }
        
        // Wait before destroying this object
        yield return new WaitForSeconds(currentReaction.destructionDelay);
        
        Destroy(gameObject);
    }
    
    private void SpawnNextLevelObjects(ReactionLevel reaction)
    {
        float angleStep = 360f / reaction.spawnCount;
        
        for (int i = 0; i < reaction.spawnCount; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            Vector3 spawnDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 spawnPosition = transform.position + spawnDirection * reaction.spawnRadius;
            
            GameObject spawnedObject = Instantiate(reaction.prefab, spawnPosition, Quaternion.identity);
            
            // Set up the spawned object's chain reaction component
            ChainReactionSystem chainReaction = spawnedObject.GetComponent<ChainReactionSystem>();
            if (chainReaction == null)
            {
                chainReaction = spawnedObject.AddComponent<ChainReactionSystem>();
            }
            
            // Configure the next level
            chainReaction.SetupAsNextLevel(_currentLevel + 1, _reactionLevels, _victorySound);
            
            // Apply spawn force
            Rigidbody spawnedRb = spawnedObject.GetComponent<Rigidbody>();
            if (spawnedRb != null && reaction.spawnForce > 0)
            {
                Vector3 forceDirection = spawnDirection + Vector3.up * 0.3f;
                spawnedRb.AddForce(forceDirection.normalized * reaction.spawnForce, ForceMode.Impulse);
            }
        }
    }
    
    public void SetupAsNextLevel(int level, ReactionLevel[] reactionLevels, AudioClip victorySound)
    {
        _currentLevel = level;
        _reactionLevels = reactionLevels;
        _victorySound = victorySound;
        _hasTriggered = false;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    private void PlayVictorySound()
    {
        if (_victorySound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_victorySound);
        }
    }
    
    public void ResetChainReaction()
    {
        _hasTriggered = false;
    }
    
    public void SetCurrentLevel(int level)
    {
        _currentLevel = Mathf.Clamp(level, 0, _reactionLevels.Length - 1);
    }
    
    public int GetCurrentLevel()
    {
        return _currentLevel;
    }
    
    public bool HasTriggered()
    {
        return _hasTriggered;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_currentLevel < _reactionLevels.Length)
        {
            ReactionLevel currentReaction = _reactionLevels[_currentLevel];
            
            // Draw spawn radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCircle(transform.position, currentReaction.spawnRadius);
            
            // Draw spawn positions
            if (currentReaction.spawnCount > 0)
            {
                Gizmos.color = Color.green;
                float angleStep = 360f / currentReaction.spawnCount;
                
                for (int i = 0; i < currentReaction.spawnCount; i++)
                {
                    float angle = i * angleStep * Mathf.Deg2Rad;
                    Vector3 spawnDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                    Vector3 spawnPosition = transform.position + spawnDirection * currentReaction.spawnRadius;
                    
                    Gizmos.DrawWireSphere(spawnPosition, 0.2f);
                }
            }
        }
    }
}