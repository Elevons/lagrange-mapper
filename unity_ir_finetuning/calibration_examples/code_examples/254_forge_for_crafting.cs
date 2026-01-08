// Prompt: forge for crafting
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class Forge : MonoBehaviour
{
    [System.Serializable]
    public class CraftingMaterial
    {
        public string materialName;
        public Sprite icon;
        public int quantity;
        
        public CraftingMaterial(string name, Sprite sprite, int qty)
        {
            materialName = name;
            icon = sprite;
            quantity = qty;
        }
    }
    
    [System.Serializable]
    public class CraftingRecipe
    {
        public string recipeName;
        public Sprite resultIcon;
        public List<CraftingMaterial> requiredMaterials = new List<CraftingMaterial>();
        public GameObject resultPrefab;
        public int craftingTime = 3;
        public AudioClip craftingSound;
        
        public bool CanCraft(List<CraftingMaterial> availableMaterials)
        {
            foreach (var required in requiredMaterials)
            {
                var available = availableMaterials.FirstOrDefault(m => m.materialName == required.materialName);
                if (available == null || available.quantity < required.quantity)
                    return false;
            }
            return true;
        }
    }
    
    [Header("Forge Settings")]
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private Transform _craftingPoint;
    [SerializeField] private ParticleSystem _forgeFlames;
    [SerializeField] private ParticleSystem _craftingEffect;
    [SerializeField] private Light _forgeLight;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _forgeIdleSound;
    [SerializeField] private AudioClip _craftingCompleteSound;
    
    [Header("Recipes")]
    [SerializeField] private List<CraftingRecipe> _availableRecipes = new List<CraftingRecipe>();
    
    [Header("UI")]
    [SerializeField] private Canvas _forgeUI;
    [SerializeField] private GameObject _recipeButtonPrefab;
    [SerializeField] private Transform _recipeContainer;
    [SerializeField] private UnityEngine.UI.Button _closeButton;
    
    [Header("Events")]
    public UnityEvent<string> OnItemCrafted;
    public UnityEvent OnForgeOpened;
    public UnityEvent OnForgeClosed;
    
    private List<CraftingMaterial> _playerMaterials = new List<CraftingMaterial>();
    private bool _isPlayerNearby = false;
    private bool _isCrafting = false;
    private float _craftingTimer = 0f;
    private CraftingRecipe _currentRecipe;
    private Transform _player;
    
    private void Start()
    {
        InitializeForge();
        SetupUI();
    }
    
    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
        UpdateCrafting();
        UpdateEffects();
    }
    
    private void InitializeForge()
    {
        if (_forgeUI != null)
            _forgeUI.gameObject.SetActive(false);
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_craftingPoint == null)
            _craftingPoint = transform;
            
        if (_forgeFlames != null)
            _forgeFlames.Play();
            
        PlayIdleSound();
    }
    
    private void SetupUI()
    {
        if (_closeButton != null)
            _closeButton.onClick.AddListener(CloseForgeUI);
    }
    
    private void CheckPlayerProximity()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        
        float distance = Vector3.Distance(transform.position, player.transform.position);
        bool wasNearby = _isPlayerNearby;
        _isPlayerNearby = distance <= _interactionRange;
        _player = player.transform;
        
        if (_isPlayerNearby && !wasNearby)
        {
            ShowInteractionPrompt();
        }
        else if (!_isPlayerNearby && wasNearby)
        {
            HideInteractionPrompt();
            CloseForgeUI();
        }
    }
    
    private void HandleInput()
    {
        if (_isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            if (_forgeUI != null && _forgeUI.gameObject.activeInHierarchy)
                CloseForgeUI();
            else
                OpenForgeUI();
        }
    }
    
    private void UpdateCrafting()
    {
        if (!_isCrafting) return;
        
        _craftingTimer -= Time.deltaTime;
        
        if (_craftingTimer <= 0f)
        {
            CompleteCrafting();
        }
    }
    
    private void UpdateEffects()
    {
        if (_forgeLight != null)
        {
            float intensity = _isCrafting ? 2f : 1f;
            _forgeLight.intensity = Mathf.Lerp(_forgeLight.intensity, intensity, Time.deltaTime * 2f);
        }
        
        if (_craftingEffect != null)
        {
            if (_isCrafting && !_craftingEffect.isPlaying)
                _craftingEffect.Play();
            else if (!_isCrafting && _craftingEffect.isPlaying)
                _craftingEffect.Stop();
        }
    }
    
    private void OpenForgeUI()
    {
        if (_forgeUI == null) return;
        
        _forgeUI.gameObject.SetActive(true);
        UpdatePlayerMaterials();
        PopulateRecipes();
        OnForgeOpened?.Invoke();
        
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    private void CloseForgeUI()
    {
        if (_forgeUI == null) return;
        
        _forgeUI.gameObject.SetActive(false);
        OnForgeClosed?.Invoke();
        
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    private void UpdatePlayerMaterials()
    {
        _playerMaterials.Clear();
        
        if (_player == null) return;
        
        // Simulate getting materials from player inventory
        // In a real implementation, this would interface with an inventory system
        _playerMaterials.Add(new CraftingMaterial("Iron Ore", null, 5));
        _playerMaterials.Add(new CraftingMaterial("Wood", null, 10));
        _playerMaterials.Add(new CraftingMaterial("Coal", null, 3));
    }
    
    private void PopulateRecipes()
    {
        if (_recipeContainer == null || _recipeButtonPrefab == null) return;
        
        // Clear existing recipe buttons
        foreach (Transform child in _recipeContainer)
        {
            if (child.gameObject != _recipeButtonPrefab)
                Destroy(child.gameObject);
        }
        
        // Create recipe buttons
        foreach (var recipe in _availableRecipes)
        {
            GameObject buttonObj = Instantiate(_recipeButtonPrefab, _recipeContainer);
            UnityEngine.UI.Button button = buttonObj.GetComponent<UnityEngine.UI.Button>();
            
            if (button != null)
            {
                button.onClick.AddListener(() => StartCrafting(recipe));
                
                // Update button appearance based on availability
                bool canCraft = recipe.CanCraft(_playerMaterials);
                button.interactable = canCraft && !_isCrafting;
                
                // Update button text/image if components exist
                UnityEngine.UI.Text buttonText = button.GetComponentInChildren<UnityEngine.UI.Text>();
                if (buttonText != null)
                    buttonText.text = recipe.recipeName;
                    
                UnityEngine.UI.Image buttonImage = button.GetComponent<UnityEngine.UI.Image>();
                if (buttonImage != null && recipe.resultIcon != null)
                    buttonImage.sprite = recipe.resultIcon;
            }
            
            buttonObj.SetActive(true);
        }
    }
    
    private void StartCrafting(CraftingRecipe recipe)
    {
        if (_isCrafting || !recipe.CanCraft(_playerMaterials)) return;
        
        _currentRecipe = recipe;
        _isCrafting = true;
        _craftingTimer = recipe.craftingTime;
        
        // Consume materials
        foreach (var required in recipe.requiredMaterials)
        {
            var material = _playerMaterials.FirstOrDefault(m => m.materialName == required.materialName);
            if (material != null)
                material.quantity -= required.quantity;
        }
        
        // Play crafting sound
        if (_audioSource != null && recipe.craftingSound != null)
            _audioSource.PlayOneShot(recipe.craftingSound);
            
        // Update UI
        PopulateRecipes();
    }
    
    private void CompleteCrafting()
    {
        if (_currentRecipe == null) return;
        
        _isCrafting = false;
        
        // Spawn crafted item
        if (_currentRecipe.resultPrefab != null)
        {
            Vector3 spawnPosition = _craftingPoint.position + Vector3.up * 0.5f;
            GameObject craftedItem = Instantiate(_currentRecipe.resultPrefab, spawnPosition, Quaternion.identity);
            
            // Add some upward force to the crafted item
            Rigidbody rb = craftedItem.GetComponent<Rigidbody>();
            if (rb != null)
                rb.AddForce(Vector3.up * 3f, ForceMode.Impulse);
        }
        
        // Play completion sound
        if (_audioSource != null && _craftingCompleteSound != null)
            _audioSource.PlayOneShot(_craftingCompleteSound);
        
        // Invoke event
        OnItemCrafted?.Invoke(_currentRecipe.recipeName);
        
        _currentRecipe = null;
        
        // Update UI
        PopulateRecipes();
        
        // Resume idle sound
        Invoke(nameof(PlayIdleSound), 1f);
    }
    
    private void PlayIdleSound()
    {
        if (_audioSource != null && _forgeIdleSound != null && !_isCrafting)
        {
            _audioSource.clip = _forgeIdleSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void ShowInteractionPrompt()
    {
        // This would typically show a UI prompt
        Debug.Log("Press E to use Forge");
    }
    
    private void HideInteractionPrompt()
    {
        // This would typically hide the UI prompt
    }
    
    public void AddMaterial(string materialName, int quantity)
    {
        var existing = _playerMaterials.FirstOrDefault(m => m.materialName == materialName);
        if (existing != null)
            existing.quantity += quantity;
        else
            _playerMaterials.Add(new CraftingMaterial(materialName, null, quantity));
    }
    
    public bool HasMaterial(string materialName, int quantity)
    {
        var material = _playerMaterials.FirstOrDefault(m => m.materialName == materialName);
        return material != null && material.quantity >= quantity;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
        
        if (_craftingPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(_craftingPoint.position, Vector3.one * 0.5f);
        }
    }
}