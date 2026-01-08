// Prompt: food item that restores stamina
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class FoodItem : MonoBehaviour
{
    [Header("Food Properties")]
    [SerializeField] private float _staminaRestoreAmount = 25f;
    [SerializeField] private bool _destroyOnUse = true;
    [SerializeField] private float _interactionRange = 2f;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _consumeEffect;
    [SerializeField] private AudioClip _consumeSound;
    [SerializeField] private Color _highlightColor = Color.yellow;
    
    [Header("UI")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    
    [Header("Events")]
    public UnityEvent<float> OnStaminaRestore;
    public UnityEvent OnFoodConsumed;
    
    private Renderer _renderer;
    private Color _originalColor;
    private AudioSource _audioSource;
    private bool _isPlayerInRange = false;
    private GameObject _currentPlayer;
    private Camera _mainCamera;
    
    [System.Serializable]
    public class PlayerStamina
    {
        public float currentStamina = 100f;
        public float maxStamina = 100f;
        
        public void RestoreStamina(float amount)
        {
            currentStamina = Mathf.Min(currentStamina + amount, maxStamina);
        }
        
        public float GetStaminaPercentage()
        {
            return currentStamina / maxStamina;
        }
    }
    
    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _originalColor = _renderer.material.color;
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _mainCamera = Camera.main;
        
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }
        
        SetupCollider();
    }
    
    private void SetupCollider()
    {
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = _interactionRange;
        }
        else
        {
            col.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (_isPlayerInRange && _currentPlayer != null)
        {
            if (Input.GetKeyDown(_interactionKey))
            {
                ConsumeFoodItem();
            }
            
            UpdateInteractionPromptPosition();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = true;
            _currentPlayer = other.gameObject;
            ShowInteractionPrompt();
            HighlightItem(true);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerInRange = false;
            _currentPlayer = null;
            HideInteractionPrompt();
            HighlightItem(false);
        }
    }
    
    private void ConsumeFoodItem()
    {
        if (_currentPlayer == null) return;
        
        PlayerStamina playerStamina = _currentPlayer.GetComponent<PlayerStamina>();
        if (playerStamina == null)
        {
            playerStamina = _currentPlayer.AddComponent<PlayerStamina>();
        }
        
        float previousStamina = playerStamina.currentStamina;
        playerStamina.RestoreStamina(_staminaRestoreAmount);
        float actualRestored = playerStamina.currentStamina - previousStamina;
        
        OnStaminaRestore?.Invoke(actualRestored);
        OnFoodConsumed?.Invoke();
        
        PlayConsumeEffects();
        
        Debug.Log($"Restored {actualRestored} stamina. Current: {playerStamina.currentStamina}/{playerStamina.maxStamina}");
        
        if (_destroyOnUse)
        {
            HideInteractionPrompt();
            Destroy(gameObject, 0.1f);
        }
    }
    
    private void PlayConsumeEffects()
    {
        if (_consumeEffect != null)
        {
            GameObject effect = Instantiate(_consumeEffect, transform.position, transform.rotation);
            Destroy(effect, 3f);
        }
        
        if (_consumeSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_consumeSound);
        }
    }
    
    private void HighlightItem(bool highlight)
    {
        if (_renderer == null) return;
        
        if (highlight)
        {
            _renderer.material.color = _highlightColor;
        }
        else
        {
            _renderer.material.color = _originalColor;
        }
    }
    
    private void ShowInteractionPrompt()
    {
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(true);
        }
    }
    
    private void HideInteractionPrompt()
    {
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }
    }
    
    private void UpdateInteractionPromptPosition()
    {
        if (_interactionPrompt == null || _mainCamera == null) return;
        
        Vector3 screenPos = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * 2f);
        _interactionPrompt.transform.position = screenPos;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }
    
    public void SetStaminaRestoreAmount(float amount)
    {
        _staminaRestoreAmount = Mathf.Max(0f, amount);
    }
    
    public float GetStaminaRestoreAmount()
    {
        return _staminaRestoreAmount;
    }
}