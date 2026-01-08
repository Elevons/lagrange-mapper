// Prompt: checkpoint respawn
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class CheckpointRespawn : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    [SerializeField] private Transform _respawnPoint;
    [SerializeField] private bool _isActiveCheckpoint = false;
    [SerializeField] private float _respawnDelay = 2f;
    [SerializeField] private LayerMask _playerLayer = -1;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _activeIndicator;
    [SerializeField] private GameObject _inactiveIndicator;
    [SerializeField] private ParticleSystem _activationEffect;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _respawnSound;
    
    [Header("Events")]
    public UnityEvent OnCheckpointActivated;
    public UnityEvent OnPlayerRespawned;
    
    private static CheckpointRespawn _currentActiveCheckpoint;
    private static Dictionary<GameObject, Vector3> _playerLastPositions = new Dictionary<GameObject, Vector3>();
    private AudioSource _audioSource;
    private bool _hasBeenActivated = false;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_respawnPoint == null)
            _respawnPoint = transform;
            
        UpdateVisuals();
        
        if (_isActiveCheckpoint && _currentActiveCheckpoint == null)
        {
            SetAsActiveCheckpoint();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject) && !_hasBeenActivated)
        {
            ActivateCheckpoint(other.gameObject);
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other.gameObject) && !_hasBeenActivated)
        {
            ActivateCheckpoint(other.gameObject);
        }
    }
    
    private bool IsPlayer(GameObject obj)
    {
        return obj.CompareTag("Player") && ((1 << obj.layer) & _playerLayer) != 0;
    }
    
    private void ActivateCheckpoint(GameObject player)
    {
        if (_currentActiveCheckpoint != null && _currentActiveCheckpoint != this)
        {
            _currentActiveCheckpoint.DeactivateCheckpoint();
        }
        
        SetAsActiveCheckpoint();
        _playerLastPositions[player] = _respawnPoint.position;
        
        PlayActivationEffects();
        OnCheckpointActivated?.Invoke();
        
        _hasBeenActivated = true;
    }
    
    private void SetAsActiveCheckpoint()
    {
        _currentActiveCheckpoint = this;
        _isActiveCheckpoint = true;
        UpdateVisuals();
    }
    
    private void DeactivateCheckpoint()
    {
        _isActiveCheckpoint = false;
        UpdateVisuals();
    }
    
    private void UpdateVisuals()
    {
        if (_activeIndicator != null)
            _activeIndicator.SetActive(_isActiveCheckpoint);
            
        if (_inactiveIndicator != null)
            _inactiveIndicator.SetActive(!_isActiveCheckpoint);
    }
    
    private void PlayActivationEffects()
    {
        if (_activationEffect != null)
            _activationEffect.Play();
            
        if (_activationSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_activationSound);
    }
    
    public static void RespawnPlayer(GameObject player)
    {
        if (_currentActiveCheckpoint == null)
        {
            Debug.LogWarning("No active checkpoint found for respawn!");
            return;
        }
        
        _currentActiveCheckpoint.StartCoroutine(_currentActiveCheckpoint.RespawnPlayerCoroutine(player));
    }
    
    private System.Collections.IEnumerator RespawnPlayerCoroutine(GameObject player)
    {
        yield return new WaitForSeconds(_respawnDelay);
        
        Vector3 respawnPosition = _playerLastPositions.ContainsKey(player) 
            ? _playerLastPositions[player] 
            : _respawnPoint.position;
        
        // Reset player position
        if (player.GetComponent<Rigidbody>() != null)
        {
            Rigidbody rb = player.GetComponent<Rigidbody>();
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        if (player.GetComponent<Rigidbody2D>() != null)
        {
            Rigidbody2D rb2d = player.GetComponent<Rigidbody2D>();
            rb2d.velocity = Vector2.zero;
            rb2d.angularVelocity = 0f;
        }
        
        player.transform.position = respawnPosition;
        player.transform.rotation = _respawnPoint.rotation;
        
        // Re-enable player if disabled
        if (!player.activeInHierarchy)
            player.SetActive(true);
            
        // Reset player components
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
            playerCollider.enabled = true;
            
        Collider2D playerCollider2D = player.GetComponent<Collider2D>();
        if (playerCollider2D != null)
            playerCollider2D.enabled = true;
        
        PlayRespawnEffects();
        OnPlayerRespawned?.Invoke();
    }
    
    private void PlayRespawnEffects()
    {
        if (_respawnSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_respawnSound);
    }
    
    public static Vector3 GetCurrentRespawnPosition()
    {
        return _currentActiveCheckpoint != null ? _currentActiveCheckpoint._respawnPoint.position : Vector3.zero;
    }
    
    public static CheckpointRespawn GetCurrentActiveCheckpoint()
    {
        return _currentActiveCheckpoint;
    }
    
    public void ForceActivateCheckpoint()
    {
        if (_currentActiveCheckpoint != null && _currentActiveCheckpoint != this)
        {
            _currentActiveCheckpoint.DeactivateCheckpoint();
        }
        
        SetAsActiveCheckpoint();
        PlayActivationEffects();
        OnCheckpointActivated?.Invoke();
        _hasBeenActivated = true;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_respawnPoint != null)
        {
            Gizmos.color = _isActiveCheckpoint ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(_respawnPoint.position, 1f);
            Gizmos.DrawLine(_respawnPoint.position, _respawnPoint.position + _respawnPoint.forward * 2f);
        }
    }
}