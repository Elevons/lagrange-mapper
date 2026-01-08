// Prompt: difficulty selection
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class DifficultySelector : MonoBehaviour
{
    [System.Serializable]
    public class DifficultySettings
    {
        public string name;
        public string description;
        public float enemyHealthMultiplier = 1f;
        public float enemyDamageMultiplier = 1f;
        public float enemySpeedMultiplier = 1f;
        public float playerHealthMultiplier = 1f;
        public float experienceMultiplier = 1f;
        public int maxLives = 3;
        public Color difficultyColor = Color.white;
    }

    [System.Serializable]
    public class DifficultySelectedEvent : UnityEvent<int, DifficultySettings> { }

    [Header("Difficulty Configuration")]
    [SerializeField] private List<DifficultySettings> _difficulties = new List<DifficultySettings>();
    [SerializeField] private int _defaultDifficultyIndex = 1;

    [Header("UI References")]
    [SerializeField] private Transform _buttonContainer;
    [SerializeField] private Button _difficultyButtonPrefab;
    [SerializeField] private Text _difficultyNameText;
    [SerializeField] private Text _difficultyDescriptionText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _backButton;

    [Header("Visual Settings")]
    [SerializeField] private Color _selectedButtonColor = Color.green;
    [SerializeField] private Color _normalButtonColor = Color.white;
    [SerializeField] private float _buttonScaleOnHover = 1.1f;

    [Header("Events")]
    public DifficultySelectedEvent OnDifficultySelected = new DifficultySelectedEvent();
    public UnityEvent OnBackPressed = new UnityEvent();

    private List<Button> _difficultyButtons = new List<Button>();
    private int _selectedDifficultyIndex = -1;
    private static int _savedDifficultyIndex = -1;

    private void Start()
    {
        InitializeDefaultDifficulties();
        CreateDifficultyButtons();
        SetupUIEvents();
        LoadSavedDifficulty();
    }

    private void InitializeDefaultDifficulties()
    {
        if (_difficulties.Count == 0)
        {
            _difficulties.Add(new DifficultySettings
            {
                name = "Easy",
                description = "Relaxed gameplay with weaker enemies and more health",
                enemyHealthMultiplier = 0.7f,
                enemyDamageMultiplier = 0.7f,
                enemySpeedMultiplier = 0.8f,
                playerHealthMultiplier = 1.5f,
                experienceMultiplier = 1.2f,
                maxLives = 5,
                difficultyColor = Color.green
            });

            _difficulties.Add(new DifficultySettings
            {
                name = "Normal",
                description = "Balanced gameplay for the intended experience",
                enemyHealthMultiplier = 1f,
                enemyDamageMultiplier = 1f,
                enemySpeedMultiplier = 1f,
                playerHealthMultiplier = 1f,
                experienceMultiplier = 1f,
                maxLives = 3,
                difficultyColor = Color.yellow
            });

            _difficulties.Add(new DifficultySettings
            {
                name = "Hard",
                description = "Challenging gameplay with stronger enemies",
                enemyHealthMultiplier = 1.5f,
                enemyDamageMultiplier = 1.3f,
                enemySpeedMultiplier = 1.2f,
                playerHealthMultiplier = 0.8f,
                experienceMultiplier = 1.5f,
                maxLives = 2,
                difficultyColor = Color.red
            });

            _difficulties.Add(new DifficultySettings
            {
                name = "Nightmare",
                description = "Extreme challenge for hardcore players only",
                enemyHealthMultiplier = 2f,
                enemyDamageMultiplier = 1.8f,
                enemySpeedMultiplier = 1.5f,
                playerHealthMultiplier = 0.5f,
                experienceMultiplier = 2f,
                maxLives = 1,
                difficultyColor = Color.magenta
            });
        }
    }

    private void CreateDifficultyButtons()
    {
        if (_buttonContainer == null || _difficultyButtonPrefab == null)
            return;

        ClearExistingButtons();

        for (int i = 0; i < _difficulties.Count; i++)
        {
            CreateDifficultyButton(i);
        }
    }

    private void ClearExistingButtons()
    {
        foreach (Button button in _difficultyButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        _difficultyButtons.Clear();
    }

    private void CreateDifficultyButton(int index)
    {
        Button button = Instantiate(_difficultyButtonPrefab, _buttonContainer);
        Text buttonText = button.GetComponentInChildren<Text>();
        
        if (buttonText != null)
        {
            buttonText.text = _difficulties[index].name;
            buttonText.color = _difficulties[index].difficultyColor;
        }

        int capturedIndex = index;
        button.onClick.AddListener(() => SelectDifficulty(capturedIndex));

        DifficultyButtonHover hoverEffect = button.gameObject.AddComponent<DifficultyButtonHover>();
        hoverEffect.Initialize(_buttonScaleOnHover);

        _difficultyButtons.Add(button);
    }

    private void SetupUIEvents()
    {
        if (_confirmButton != null)
            _confirmButton.onClick.AddListener(ConfirmSelection);

        if (_backButton != null)
            _backButton.onClick.AddListener(() => OnBackPressed.Invoke());

        UpdateConfirmButtonState();
    }

    private void LoadSavedDifficulty()
    {
        int savedIndex = PlayerPrefs.GetInt("SelectedDifficulty", _defaultDifficultyIndex);
        
        if (_savedDifficultyIndex != -1)
            savedIndex = _savedDifficultyIndex;

        if (savedIndex >= 0 && savedIndex < _difficulties.Count)
        {
            SelectDifficulty(savedIndex);
        }
    }

    public void SelectDifficulty(int index)
    {
        if (index < 0 || index >= _difficulties.Count)
            return;

        _selectedDifficultyIndex = index;
        UpdateButtonVisuals();
        UpdateDifficultyInfo();
        UpdateConfirmButtonState();
    }

    private void UpdateButtonVisuals()
    {
        for (int i = 0; i < _difficultyButtons.Count; i++)
        {
            if (_difficultyButtons[i] != null)
            {
                ColorBlock colors = _difficultyButtons[i].colors;
                colors.normalColor = (i == _selectedDifficultyIndex) ? _selectedButtonColor : _normalButtonColor;
                _difficultyButtons[i].colors = colors;
            }
        }
    }

    private void UpdateDifficultyInfo()
    {
        if (_selectedDifficultyIndex < 0 || _selectedDifficultyIndex >= _difficulties.Count)
            return;

        DifficultySettings selected = _difficulties[_selectedDifficultyIndex];

        if (_difficultyNameText != null)
            _difficultyNameText.text = selected.name;

        if (_difficultyDescriptionText != null)
            _difficultyDescriptionText.text = selected.description;
    }

    private void UpdateConfirmButtonState()
    {
        if (_confirmButton != null)
            _confirmButton.interactable = (_selectedDifficultyIndex >= 0);
    }

    private void ConfirmSelection()
    {
        if (_selectedDifficultyIndex < 0 || _selectedDifficultyIndex >= _difficulties.Count)
            return;

        PlayerPrefs.SetInt("SelectedDifficulty", _selectedDifficultyIndex);
        PlayerPrefs.Save();

        _savedDifficultyIndex = _selectedDifficultyIndex;

        OnDifficultySelected.Invoke(_selectedDifficultyIndex, _difficulties[_selectedDifficultyIndex]);
    }

    public DifficultySettings GetSelectedDifficulty()
    {
        if (_selectedDifficultyIndex >= 0 && _selectedDifficultyIndex < _difficulties.Count)
            return _difficulties[_selectedDifficultyIndex];
        
        return _difficulties[_defaultDifficultyIndex];
    }

    public static DifficultySettings GetCurrentDifficulty()
    {
        int savedIndex = PlayerPrefs.GetInt("SelectedDifficulty", 1);
        
        if (_savedDifficultyIndex != -1)
            savedIndex = _savedDifficultyIndex;

        DifficultySelector selector = FindObjectOfType<DifficultySelector>();
        if (selector != null && savedIndex >= 0 && savedIndex < selector._difficulties.Count)
            return selector._difficulties[savedIndex];

        return new DifficultySettings
        {
            name = "Normal",
            description = "Default difficulty",
            enemyHealthMultiplier = 1f,
            enemyDamageMultiplier = 1f,
            enemySpeedMultiplier = 1f,
            playerHealthMultiplier = 1f,
            experienceMultiplier = 1f,
            maxLives = 3,
            difficultyColor = Color.white
        };
    }

    public void ResetToDefault()
    {
        SelectDifficulty(_defaultDifficultyIndex);
    }

    private class DifficultyButtonHover : MonoBehaviour, UnityEngine.EventSystems.IPointerEnterHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        private Vector3 _originalScale;
        private float _hoverScale;

        public void Initialize(float hoverScale)
        {
            _originalScale = transform.localScale;
            _hoverScale = hoverScale;
        }

        public void OnPointerEnter(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.localScale = _originalScale * _hoverScale;
        }

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData)
        {
            transform.localScale = _originalScale;
        }
    }
}