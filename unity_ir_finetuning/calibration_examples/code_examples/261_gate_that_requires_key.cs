// Prompt: gate that requires key
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class KeyGate : MonoBehaviour
{
    [Header("Gate Settings")]
    [SerializeField] private string _requiredKeyTag = "Key";
    [SerializeField] private float _openHeight = 3f;
    [SerializeField] private float _openSpeed = 2f;
    [SerializeField] private bool _stayOpen = true;
    [SerializeField] private float _autoCloseDelay = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _unlockSound;
    [SerializeField] private AudioClip _lockedSound;
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _lockedIndicator;
    [SerializeField] private GameObject _unlockedIndicator;
    [SerializeField] private ParticleSystem _unlockEffect;
    
    [Header("Events")]
    public UnityEvent OnGateUnlocked;
    public UnityEvent OnGateOpened;
    public UnityEvent OnGateClosed;
    
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isUnlocked = false;
    private bool _isOpen = false;
    private bool _isMoving = false;
    private AudioSource _audioSource;
    private Collider _gateCollider;
    private float _autoCloseTimer;
    
    private void Start()
    {
        _closedPosition = transform.position;
        _openPosition = _closedPosition + Vector3.up * _openHeight;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _gateCollider = GetComponent<Collider>();
        if (_gateCollider == null)
        {
            _gateCollider = gameObject.AddComponent<BoxCollider>();
        }
        
        UpdateVisualFeedback();
    }
    
    private void Update()
    {
        HandleGateMovement();
        HandleAutoClose();
    }
    
    private void HandleGateMovement()
    {
        if (!_isMoving) return;
        
        Vector3 targetPosition = _isOpen ? _openPosition : _closedPosition;
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, _openSpeed * Time.deltaTime);
        
        if (Vector3.Distance(transform.position, targetPosition) < 0.01f)
        {
            transform.position = targetPosition;
            _isMoving = false;
            
            if (_isOpen)
            {
                OnGateOpened?.Invoke();
                if (!_stayOpen)
                {
                    _autoCloseTimer = _autoCloseDelay;
                }
            }
            else
            {
                OnGateClosed?.Invoke();
                if (_gateCollider != null)
                {
                    _gateCollider.enabled = true;
                }
            }
        }
    }
    
    private void HandleAutoClose()
    {
        if (!_stayOpen && _isOpen && !_isMoving && _autoCloseTimer > 0f)
        {
            _autoCloseTimer -= Time.deltaTime;
            if (_autoCloseTimer <= 0f)
            {
                CloseGate();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        if (!_isUnlocked)
        {
            if (PlayerHasKey(other))
            {
                UnlockGate(other);
            }
            else
            {
                PlayLockedFeedback();
            }
        }
        else if (!_isOpen && !_isMoving)
        {
            OpenGate();
        }
    }
    
    private bool PlayerHasKey(Collider player)
    {
        KeyItem[] keys = player.GetComponentsInChildren<KeyItem>();
        foreach (KeyItem key in keys)
        {
            if (key.CompareTag(_requiredKeyTag))
            {
                return true;
            }
        }
        
        GameObject[] childObjects = new GameObject[player.transform.childCount];
        for (int i = 0; i < player.transform.childCount; i++)
        {
            childObjects[i] = player.transform.GetChild(i).gameObject;
        }
        
        foreach (GameObject child in childObjects)
        {
            if (child.CompareTag(_requiredKeyTag))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private void UnlockGate(Collider player)
    {
        _isUnlocked = true;
        
        ConsumeKey(player);
        
        PlaySound(_unlockSound);
        UpdateVisualFeedback();
        
        if (_unlockEffect != null)
        {
            _unlockEffect.Play();
        }
        
        OnGateUnlocked?.Invoke();
        
        OpenGate();
    }
    
    private void ConsumeKey(Collider player)
    {
        KeyItem[] keys = player.GetComponentsInChildren<KeyItem>();
        foreach (KeyItem key in keys)
        {
            if (key.CompareTag(_requiredKeyTag))
            {
                Destroy(key.gameObject);
                return;
            }
        }
        
        for (int i = 0; i < player.transform.childCount; i++)
        {
            GameObject child = player.transform.GetChild(i).gameObject;
            if (child.CompareTag(_requiredKeyTag))
            {
                Destroy(child);
                return;
            }
        }
    }
    
    private void OpenGate()
    {
        if (_isOpen || _isMoving) return;
        
        _isOpen = true;
        _isMoving = true;
        
        if (_gateCollider != null)
        {
            _gateCollider.enabled = false;
        }
        
        PlaySound(_openSound);
    }
    
    private void CloseGate()
    {
        if (!_isOpen || _isMoving) return;
        
        _isOpen = false;
        _isMoving = true;
        
        PlaySound(_closeSound);
    }
    
    private void PlayLockedFeedback()
    {
        PlaySound(_lockedSound);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void UpdateVisualFeedback()
    {
        if (_lockedIndicator != null)
        {
            _lockedIndicator.SetActive(!_isUnlocked);
        }
        
        if (_unlockedIndicator != null)
        {
            _unlockedIndicator.SetActive(_isUnlocked);
        }
    }
    
    public void ForceUnlock()
    {
        _isUnlocked = true;
        UpdateVisualFeedback();
        OnGateUnlocked?.Invoke();
    }
    
    public void ForceOpen()
    {
        if (_isUnlocked)
        {
            OpenGate();
        }
    }
    
    public void ForceClose()
    {
        CloseGate();
    }
}

[System.Serializable]
public class KeyItem : MonoBehaviour
{
    [Header("Key Settings")]
    [SerializeField] private string _keyType = "DefaultKey";
    
    public string KeyType => _keyType;
}