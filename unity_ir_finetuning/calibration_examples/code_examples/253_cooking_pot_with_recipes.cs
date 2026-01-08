// Prompt: cooking pot with recipes
// Type: general

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class CookingPot : MonoBehaviour
{
    [System.Serializable]
    public class Ingredient
    {
        public string name;
        public GameObject prefab;
        public int quantity;
        
        public Ingredient(string itemName, GameObject itemPrefab, int itemQuantity = 1)
        {
            name = itemName;
            prefab = itemPrefab;
            quantity = itemQuantity;
        }
    }
    
    [System.Serializable]
    public class Recipe
    {
        public string recipeName;
        public List<Ingredient> requiredIngredients = new List<Ingredient>();
        public GameObject resultPrefab;
        public float cookingTime = 3f;
        public string description;
    }
    
    [Header("Cooking Configuration")]
    [SerializeField] private List<Recipe> _availableRecipes = new List<Recipe>();
    [SerializeField] private Transform _ingredientDropZone;
    [SerializeField] private Transform _resultSpawnPoint;
    [SerializeField] private float _interactionRange = 2f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _cookingParticles;
    [SerializeField] private ParticleSystem _completeParticles;
    [SerializeField] private Animator _potAnimator;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _cookingSound;
    [SerializeField] private AudioClip _completeSound;
    [SerializeField] private AudioClip _failSound;
    
    [Header("UI")]
    [SerializeField] private Canvas _uiCanvas;
    [SerializeField] private GameObject _recipePanel;
    [SerializeField] private UnityEngine.UI.Text _statusText;
    [SerializeField] private UnityEngine.UI.Button _cookButton;
    [SerializeField] private UnityEngine.UI.Text _recipeListText;
    
    [Header("Events")]
    public UnityEvent<string> OnRecipeCompleted;
    public UnityEvent OnCookingStarted;
    public UnityEvent OnCookingFailed;
    
    private List<Ingredient> _currentIngredients = new List<Ingredient>();
    private bool _isCooking = false;
    private float _cookingTimer = 0f;
    private Recipe _currentRecipe;
    private Transform _playerTransform;
    private bool _playerInRange = false;
    
    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_cookButton != null)
            _cookButton.onClick.AddListener(StartCooking);
            
        if (_uiCanvas != null)
            _uiCanvas.gameObject.SetActive(false);
            
        UpdateUI();
        DisplayAvailableRecipes();
    }
    
    private void Update()
    {
        HandlePlayerInteraction();
        HandleCooking();
        UpdateUI();
    }
    
    private void HandlePlayerInteraction()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }
        
        if (_playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            bool wasInRange = _playerInRange;
            _playerInRange = distance <= _interactionRange;
            
            if (_playerInRange && !wasInRange)
            {
                ShowUI();
            }
            else if (!_playerInRange && wasInRange)
            {
                HideUI();
            }
            
            if (_playerInRange && Input.GetKeyDown(KeyCode.E) && !_isCooking)
            {
                ToggleRecipePanel();
            }
        }
    }
    
    private void HandleCooking()
    {
        if (_isCooking)
        {
            _cookingTimer += Time.deltaTime;
            
            if (_cookingTimer >= _currentRecipe.cookingTime)
            {
                CompleteCooking();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ingredient") || other.CompareTag("Item"))
        {
            AddIngredient(other.gameObject);
        }
    }
    
    private void AddIngredient(GameObject ingredientObject)
    {
        if (_isCooking) return;
        
        string ingredientName = ingredientObject.name.Replace("(Clone)", "").Trim();
        
        Ingredient existingIngredient = _currentIngredients.Find(i => i.name == ingredientName);
        if (existingIngredient != null)
        {
            existingIngredient.quantity++;
        }
        else
        {
            _currentIngredients.Add(new Ingredient(ingredientName, ingredientObject, 1));
        }
        
        Destroy(ingredientObject);
        UpdateUI();
    }
    
    public void StartCooking()
    {
        if (_isCooking) return;
        
        Recipe matchedRecipe = FindMatchingRecipe();
        if (matchedRecipe != null)
        {
            _currentRecipe = matchedRecipe;
            _isCooking = true;
            _cookingTimer = 0f;
            
            OnCookingStarted?.Invoke();
            
            if (_cookingParticles != null)
                _cookingParticles.Play();
                
            if (_potAnimator != null)
                _potAnimator.SetBool("IsCooking", true);
                
            if (_audioSource != null && _cookingSound != null)
                _audioSource.PlayOneShot(_cookingSound);
        }
        else
        {
            OnCookingFailed?.Invoke();
            
            if (_audioSource != null && _failSound != null)
                _audioSource.PlayOneShot(_failSound);
                
            ClearIngredients();
        }
    }
    
    private Recipe FindMatchingRecipe()
    {
        foreach (Recipe recipe in _availableRecipes)
        {
            if (IngredientsMatch(recipe.requiredIngredients, _currentIngredients))
            {
                return recipe;
            }
        }
        return null;
    }
    
    private bool IngredientsMatch(List<Ingredient> required, List<Ingredient> current)
    {
        if (required.Count != current.Count) return false;
        
        foreach (Ingredient requiredIngredient in required)
        {
            Ingredient currentIngredient = current.Find(i => i.name == requiredIngredient.name);
            if (currentIngredient == null || currentIngredient.quantity < requiredIngredient.quantity)
            {
                return false;
            }
        }
        
        return true;
    }
    
    private void CompleteCooking()
    {
        _isCooking = false;
        
        if (_cookingParticles != null)
            _cookingParticles.Stop();
            
        if (_completeParticles != null)
            _completeParticles.Play();
            
        if (_potAnimator != null)
            _potAnimator.SetBool("IsCooking", false);
            
        if (_audioSource != null && _completeSound != null)
            _audioSource.PlayOneShot(_completeSound);
        
        SpawnResult();
        OnRecipeCompleted?.Invoke(_currentRecipe.recipeName);
        
        ClearIngredients();
        _currentRecipe = null;
    }
    
    private void SpawnResult()
    {
        if (_currentRecipe.resultPrefab != null && _resultSpawnPoint != null)
        {
            Instantiate(_currentRecipe.resultPrefab, _resultSpawnPoint.position, _resultSpawnPoint.rotation);
        }
    }
    
    private void ClearIngredients()
    {
        _currentIngredients.Clear();
        UpdateUI();
    }
    
    private void ShowUI()
    {
        if (_uiCanvas != null)
            _uiCanvas.gameObject.SetActive(true);
    }
    
    private void HideUI()
    {
        if (_uiCanvas != null)
            _uiCanvas.gameObject.SetActive(false);
            
        if (_recipePanel != null)
            _recipePanel.SetActive(false);
    }
    
    private void ToggleRecipePanel()
    {
        if (_recipePanel != null)
            _recipePanel.SetActive(!_recipePanel.activeSelf);
    }
    
    private void UpdateUI()
    {
        if (_statusText != null)
        {
            if (_isCooking)
            {
                float progress = _cookingTimer / _currentRecipe.cookingTime;
                _statusText.text = $"Cooking {_currentRecipe.recipeName}... {progress:P0}";
            }
            else if (_currentIngredients.Count > 0)
            {
                _statusText.text = "Ingredients added. Press Cook to start!";
            }
            else
            {
                _statusText.text = "Drop ingredients here. Press E for recipes.";
            }
        }
        
        if (_cookButton != null)
        {
            _cookButton.interactable = !_isCooking && _currentIngredients.Count > 0;
        }
    }
    
    private void DisplayAvailableRecipes()
    {
        if (_recipeListText != null)
        {
            string recipeText = "Available Recipes:\n\n";
            
            foreach (Recipe recipe in _availableRecipes)
            {
                recipeText += $"<b>{recipe.recipeName}</b>\n";
                recipeText += $"{recipe.description}\n";
                recipeText += "Ingredients:\n";
                
                foreach (Ingredient ingredient in recipe.requiredIngredients)
                {
                    recipeText += $"- {ingredient.name} x{ingredient.quantity}\n";
                }
                
                recipeText += $"Cooking Time: {recipe.cookingTime}s\n\n";
            }
            
            _recipeListText.text = recipeText;
        }
    }
    
    public void AddRecipe(Recipe newRecipe)
    {
        if (!_availableRecipes.Contains(newRecipe))
        {
            _availableRecipes.Add(newRecipe);
            DisplayAvailableRecipes();
        }
    }
    
    public void RemoveRecipe(string recipeName)
    {
        _availableRecipes.RemoveAll(r => r.recipeName == recipeName);
        DisplayAvailableRecipes();
    }
    
    public bool HasIngredient(string ingredientName)
    {
        return _currentIngredients.Exists(i => i.name == ingredientName);
    }
    
    public int GetIngredientCount(string ingredientName)
    {
        Ingredient ingredient = _currentIngredients.Find(i => i.name == ingredientName);
        return ingredient != null ? ingredient.quantity : 0;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
        
        if (_ingredientDropZone != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_ingredientDropZone.position, Vector3.one);
        }
        
        if (_resultSpawnPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_resultSpawnPoint.position, 0.5f);
        }
    }
}