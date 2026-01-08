// Prompt: key that unlocks corresponding door
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class Key : MonoBehaviour
{
    [Header("Key Settings")]
    [SerializeField] private string _keyID = "DefaultKey";
    [SerializeField] private bool _destroyOnPickup = true;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private GameObject _pickupEffect;
    
    [Header("Visual Feedback")]
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    [SerializeField] private float _rotationSpeed = 90f;
    
    [Header("Events")]
    public UnityEvent<string> OnKeyPickedUp;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    private bool _isPickedUp = false;
    
    void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    void Update()
    {
        if (_isPickedUp) return;
        
        // Bobbing animation
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);
        
        // Rotation animation
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        
        if (other.CompareTag("Player"))
        {
            PickupKey(other.gameObject);
        }
    }
    
    private void PickupKey(GameObject player)
    {
        _isPickedUp = true;
        
        // Add key to player's inventory
        PlayerKeyInventory inventory = player.GetComponent<PlayerKeyInventory>();
        if (inventory == null)
        {
            inventory = player.AddComponent<PlayerKeyInventory>();
        }
        
        inventory.AddKey(_keyID);
        
        // Play pickup sound
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        // Spawn pickup effect
        if (_pickupEffect != null)
        {
            Instantiate(_pickupEffect, transform.position, transform.rotation);
        }
        
        // Invoke event
        OnKeyPickedUp?.Invoke(_keyID);
        
        // Destroy or hide the key
        if (_destroyOnPickup)
        {
            if (_audioSource != null && _pickupSound != null)
            {
                Destroy(gameObject, _pickupSound.length);
                GetComponent<Renderer>().enabled = false;
                GetComponent<Collider>().enabled = false;
            }
            else
            {
                Destroy(gameObject);
            }
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}

public class PlayerKeyInventory : MonoBehaviour
{
    private System.Collections.Generic.List<string> _keys = new System.Collections.Generic.List<string>();
    
    public void AddKey(string keyID)
    {
        if (!_keys.Contains(keyID))
        {
            _keys.Add(keyID);
        }
    }
    
    public bool HasKey(string keyID)
    {
        return _keys.Contains(keyID);
    }
    
    public void RemoveKey(string keyID)
    {
        _keys.Remove(keyID);
    }
    
    public System.Collections.Generic.List<string> GetAllKeys()
    {
        return new System.Collections.Generic.List<string>(_keys);
    }
}

public class Door : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private string _requiredKeyID = "DefaultKey";
    [SerializeField] private bool _consumeKeyOnUse = true;
    [SerializeField] private float _openSpeed = 2f;
    [SerializeField] private Vector3 _openOffset = new Vector3(0, 3f, 0);
    
    [Header("Audio")]
    [SerializeField] private AudioClip _unlockSound;
    [SerializeField] private AudioClip _lockedSound;
    [SerializeField] private AudioClip _openSound;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _lockedIndicator;
    [SerializeField] private GameObject _unlockedIndicator;
    
    [Header("Events")]
    public UnityEvent OnDoorUnlocked;
    public UnityEvent OnDoorOpened;
    public UnityEvent OnDoorLocked;
    
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isOpen = false;
    private bool _isMoving = false;
    private AudioSource _audioSource;
    
    void Start()
    {
        _closedPosition = transform.position;
        _openPosition = _closedPosition + _openOffset;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        UpdateVisualIndicators();
    }
    
    void Update()
    {
        if (_isMoving)
        {
            Vector3 targetPosition = _isOpen ? _openPosition : _closedPosition;
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, _openSpeed * Time.deltaTime);
            
            if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
            {
                transform.position = targetPosition;
                _isMoving = false;
                
                if (_isOpen)
                {
                    OnDoorOpened?.Invoke();
                }
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isMoving)
        {
            TryOpenDoor(other.gameObject);
        }
    }
    
    private void TryOpenDoor(GameObject player)
    {
        PlayerKeyInventory inventory = player.GetComponent<PlayerKeyInventory>();
        
        if (inventory != null && inventory.HasKey(_requiredKeyID))
        {
            UnlockAndOpenDoor(inventory);
        }
        else
        {
            PlayLockedFeedback();
        }
    }
    
    private void UnlockAndOpenDoor(PlayerKeyInventory inventory)
    {
        if (_consumeKeyOnUse)
        {
            inventory.RemoveKey(_requiredKeyID);
        }
        
        _isOpen = true;
        _isMoving = true;
        
        // Play unlock sound
        if (_unlockSound != null)
        {
            _audioSource.PlayOneShot(_unlockSound);
        }
        
        // Play open sound after a delay
        if (_openSound != null)
        {
            Invoke(nameof(PlayOpenSound), 0.5f);
        }
        
        UpdateVisualIndicators();
        OnDoorUnlocked?.Invoke();
    }
    
    private void PlayOpenSound()
    {
        if (_openSound != null)
        {
            _audioSource.PlayOneShot(_openSound);
        }
    }
    
    private void PlayLockedFeedback()
    {
        if (_lockedSound != null)
        {
            _audioSource.PlayOneShot(_lockedSound);
        }
        
        OnDoorLocked?.Invoke();
    }
    
    private void UpdateVisualIndicators()
    {
        if (_lockedIndicator != null)
        {
            _lockedIndicator.SetActive(!_isOpen);
        }
        
        if (_unlockedIndicator != null)
        {
            _unlockedIndicator.SetActive(_isOpen);
        }
    }
    
    public void CloseDoor()
    {
        if (_isOpen && !_isMoving)
        {
            _isOpen = false;
            _isMoving = true;
        }
    }
    
    public bool IsOpen()
    {
        return _isOpen;
    }
    
    public string GetRequiredKeyID()
    {
        return _requiredKeyID;
    }
}