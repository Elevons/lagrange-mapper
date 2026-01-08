// Prompt: bed for sleeping
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Bed : MonoBehaviour
{
    [Header("Sleep Settings")]
    [SerializeField] private float _sleepDuration = 8f;
    [SerializeField] private bool _requireNightTime = true;
    [SerializeField] private float _nightStartHour = 20f;
    [SerializeField] private float _nightEndHour = 6f;
    
    [Header("Interaction")]
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    [SerializeField] private string _playerTag = "Player";
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private Renderer _bedRenderer;
    [SerializeField] private Material _occupiedMaterial;
    [SerializeField] private Material _defaultMaterial;
    
    [Header("Sleep Effects")]
    [SerializeField] private GameObject _sleepParticles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _sleepSound;
    [SerializeField] private AudioClip _wakeUpSound;
    
    [Header("Events")]
    public UnityEvent OnSleepStart;
    public UnityEvent OnSleepEnd;
    public UnityEvent<float> OnSleepProgress;
    
    private bool _isOccupied = false;
    private bool _isSleeping = false;
    private float _sleepTimer = 0f;
    private Transform _sleepingPlayer;
    private Vector3 _originalPlayerPosition;
    private bool _playerInRange = false;
    private Camera _playerCamera;
    private bool _originalCameraState;
    
    [System.Serializable]
    public class SleepData
    {
        public bool isComplete;
        public float timeSlept;
        public float restBonus;
    }
    
    private void Start()
    {
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
            
        if (_sleepParticles != null)
            _sleepParticles.SetActive(false);
            
        if (_bedRenderer != null && _defaultMaterial != null)
            _bedRenderer.material = _defaultMaterial;
    }
    
    private void Update()
    {
        CheckPlayerProximity();
        HandleInteraction();
        UpdateSleep();
    }
    
    private void CheckPlayerProximity()
    {
        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null) return;
        
        float distance = Vector3.Distance(transform.position, player.transform.position);
        bool wasInRange = _playerInRange;
        _playerInRange = distance <= _interactionRange && !_isSleeping;
        
        if (_playerInRange && !wasInRange)
        {
            ShowInteractionPrompt(true);
        }
        else if (!_playerInRange && wasInRange)
        {
            ShowInteractionPrompt(false);
        }
    }
    
    private void HandleInteraction()
    {
        if (!_playerInRange || !Input.GetKeyDown(_interactionKey)) return;
        
        if (!_isSleeping && !_isOccupied)
        {
            if (CanSleep())
            {
                StartSleep();
            }
        }
        else if (_isSleeping)
        {
            WakeUp();
        }
    }
    
    private bool CanSleep()
    {
        if (!_requireNightTime) return true;
        
        float currentHour = System.DateTime.Now.Hour;
        
        if (_nightStartHour > _nightEndHour)
        {
            return currentHour >= _nightStartHour || currentHour <= _nightEndHour;
        }
        else
        {
            return currentHour >= _nightStartHour && currentHour <= _nightEndHour;
        }
    }
    
    private void StartSleep()
    {
        GameObject player = GameObject.FindGameObjectWithTag(_playerTag);
        if (player == null) return;
        
        _sleepingPlayer = player.transform;
        _originalPlayerPosition = _sleepingPlayer.position;
        _isSleeping = true;
        _isOccupied = true;
        _sleepTimer = 0f;
        
        // Position player on bed
        Vector3 bedPosition = transform.position + Vector3.up * 0.5f;
        _sleepingPlayer.position = bedPosition;
        
        // Disable player movement
        MonoBehaviour[] playerScripts = _sleepingPlayer.GetComponents<MonoBehaviour>();
        foreach (var script in playerScripts)
        {
            if (script.GetType().Name.Contains("Movement") || 
                script.GetType().Name.Contains("Controller"))
            {
                script.enabled = false;
            }
        }
        
        // Handle camera
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_playerCamera != null)
        {
            _originalCameraState = _playerCamera.enabled;
            _playerCamera.enabled = false;
        }
        
        // Visual and audio feedback
        UpdateBedVisuals(true);
        PlaySound(_sleepSound);
        
        if (_sleepParticles != null)
            _sleepParticles.SetActive(true);
            
        ShowInteractionPrompt(false);
        OnSleepStart?.Invoke();
    }
    
    private void UpdateSleep()
    {
        if (!_isSleeping) return;
        
        _sleepTimer += Time.deltaTime;
        float progress = _sleepTimer / _sleepDuration;
        
        OnSleepProgress?.Invoke(progress);
        
        if (_sleepTimer >= _sleepDuration)
        {
            CompleteSleep();
        }
    }
    
    private void CompleteSleep()
    {
        WakeUp();
        
        // Provide sleep benefits
        SleepData sleepData = new SleepData
        {
            isComplete = true,
            timeSlept = _sleepDuration,
            restBonus = 1f
        };
        
        // You could send this data to other systems here
        Debug.Log($"Sleep completed! Rested for {_sleepDuration} seconds");
    }
    
    private void WakeUp()
    {
        if (!_isSleeping) return;
        
        _isSleeping = false;
        _isOccupied = false;
        
        if (_sleepingPlayer != null)
        {
            // Re-enable player movement
            MonoBehaviour[] playerScripts = _sleepingPlayer.GetComponents<MonoBehaviour>();
            foreach (var script in playerScripts)
            {
                if (script.GetType().Name.Contains("Movement") || 
                    script.GetType().Name.Contains("Controller"))
                {
                    script.enabled = true;
                }
            }
            
            // Restore camera
            if (_playerCamera != null)
                _playerCamera.enabled = _originalCameraState;
        }
        
        // Visual and audio feedback
        UpdateBedVisuals(false);
        PlaySound(_wakeUpSound);
        
        if (_sleepParticles != null)
            _sleepParticles.SetActive(false);
            
        _sleepingPlayer = null;
        OnSleepEnd?.Invoke();
    }
    
    private void UpdateBedVisuals(bool occupied)
    {
        if (_bedRenderer == null) return;
        
        if (occupied && _occupiedMaterial != null)
        {
            _bedRenderer.material = _occupiedMaterial;
        }
        else if (!occupied && _defaultMaterial != null)
        {
            _bedRenderer.material = _defaultMaterial;
        }
    }
    
    private void ShowInteractionPrompt(bool show)
    {
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(show);
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public bool IsOccupied()
    {
        return _isOccupied;
    }
    
    public bool IsSleeping()
    {
        return _isSleeping;
    }
    
    public float GetSleepProgress()
    {
        return _isSleeping ? _sleepTimer / _sleepDuration : 0f;
    }
    
    public void ForceWakeUp()
    {
        if (_isSleeping)
            WakeUp();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }
}