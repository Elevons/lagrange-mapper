// Prompt: control remapping
// Type: general

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class InputRemapper : MonoBehaviour
{
    [System.Serializable]
    public class InputAction
    {
        public string actionName;
        public KeyCode defaultKey;
        public KeyCode currentKey;
        public string description;
        
        public InputAction(string name, KeyCode key, string desc)
        {
            actionName = name;
            defaultKey = key;
            currentKey = key;
            description = desc;
        }
    }

    [System.Serializable]
    public class InputBinding
    {
        public Button remapButton;
        public Text actionLabel;
        public Text keyLabel;
        public string actionName;
    }

    [Header("Input Actions")]
    [SerializeField] private List<InputAction> _inputActions = new List<InputAction>();
    
    [Header("UI References")]
    [SerializeField] private GameObject _remapPanel;
    [SerializeField] private List<InputBinding> _inputBindings = new List<InputBinding>();
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Text _statusText;
    [SerializeField] private GameObject _listeningOverlay;
    [SerializeField] private Text _listeningText;
    
    [Header("Settings")]
    [SerializeField] private string _saveKey = "InputMappings";
    [SerializeField] private float _listeningTimeout = 10f;
    
    private Dictionary<string, KeyCode> _keyMappings = new Dictionary<string, KeyCode>();
    private bool _isListening = false;
    private string _currentRemappingAction = "";
    private float _listeningTimer = 0f;
    private Dictionary<string, KeyCode> _originalMappings = new Dictionary<string, KeyCode>();
    
    public UnityEvent<string, KeyCode> OnKeyRemapped = new UnityEvent<string, KeyCode>();
    public UnityEvent OnMappingsReset = new UnityEvent();
    public UnityEvent OnMappingsSaved = new UnityEvent();

    private void Start()
    {
        InitializeDefaultActions();
        LoadMappings();
        SetupUI();
        UpdateUI();
    }

    private void Update()
    {
        if (_isListening)
        {
            HandleKeyListening();
        }
        
        HandleInputActions();
    }

    private void InitializeDefaultActions()
    {
        if (_inputActions.Count == 0)
        {
            _inputActions.Add(new InputAction("Move Forward", KeyCode.W, "Move character forward"));
            _inputActions.Add(new InputAction("Move Backward", KeyCode.S, "Move character backward"));
            _inputActions.Add(new InputAction("Move Left", KeyCode.A, "Move character left"));
            _inputActions.Add(new InputAction("Move Right", KeyCode.D, "Move character right"));
            _inputActions.Add(new InputAction("Jump", KeyCode.Space, "Make character jump"));
            _inputActions.Add(new InputAction("Run", KeyCode.LeftShift, "Make character run"));
            _inputActions.Add(new InputAction("Interact", KeyCode.E, "Interact with objects"));
            _inputActions.Add(new InputAction("Inventory", KeyCode.Tab, "Open inventory"));
            _inputActions.Add(new InputAction("Pause", KeyCode.Escape, "Pause the game"));
        }

        foreach (var action in _inputActions)
        {
            _keyMappings[action.actionName] = action.currentKey;
            _originalMappings[action.actionName] = action.currentKey;
        }
    }

    private void SetupUI()
    {
        if (_resetButton != null)
            _resetButton.onClick.AddListener(ResetToDefaults);
            
        if (_saveButton != null)
            _saveButton.onClick.AddListener(SaveMappings);
            
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(CancelRemapping);

        foreach (var binding in _inputBindings)
        {
            if (binding.remapButton != null)
            {
                string actionName = binding.actionName;
                binding.remapButton.onClick.AddListener(() => StartRemapping(actionName));
            }
        }
    }

    private void HandleKeyListening()
    {
        _listeningTimer += Time.unscaledDeltaTime;
        
        if (_listeningTimer >= _listeningTimeout)
        {
            CancelRemapping();
            return;
        }

        if (_listeningText != null)
        {
            float remainingTime = _listeningTimeout - _listeningTimer;
            _listeningText.text = $"Press a key for '{_currentRemappingAction}'\nTimeout: {remainingTime:F1}s";
        }

        foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(key))
            {
                if (key == KeyCode.Escape)
                {
                    CancelRemapping();
                    return;
                }

                if (IsValidKey(key))
                {
                    RemapKey(_currentRemappingAction, key);
                    StopListening();
                }
                else
                {
                    ShowStatus("Invalid key selected", Color.red);
                }
                return;
            }
        }
    }

    private void HandleInputActions()
    {
        // This method can be used by other scripts to check for input
        // Example usage in other scripts: if (inputRemapper.IsActionPressed("Jump"))
    }

    public bool IsActionPressed(string actionName)
    {
        if (_keyMappings.ContainsKey(actionName))
        {
            return Input.GetKeyDown(_keyMappings[actionName]);
        }
        return false;
    }

    public bool IsActionHeld(string actionName)
    {
        if (_keyMappings.ContainsKey(actionName))
        {
            return Input.GetKey(_keyMappings[actionName]);
        }
        return false;
    }

    public KeyCode GetKeyForAction(string actionName)
    {
        return _keyMappings.ContainsKey(actionName) ? _keyMappings[actionName] : KeyCode.None;
    }

    public void StartRemapping(string actionName)
    {
        if (_isListening) return;

        _currentRemappingAction = actionName;
        _isListening = true;
        _listeningTimer = 0f;

        if (_listeningOverlay != null)
            _listeningOverlay.SetActive(true);

        ShowStatus($"Listening for new key for '{actionName}'...", Color.yellow);
    }

    private void RemapKey(string actionName, KeyCode newKey)
    {
        if (IsKeyAlreadyMapped(newKey, actionName))
        {
            ShowStatus($"Key '{newKey}' is already mapped to another action", Color.red);
            return;
        }

        KeyCode oldKey = _keyMappings[actionName];
        _keyMappings[actionName] = newKey;

        var action = _inputActions.Find(a => a.actionName == actionName);
        if (action != null)
        {
            action.currentKey = newKey;
        }

        OnKeyRemapped.Invoke(actionName, newKey);
        ShowStatus($"'{actionName}' remapped to '{newKey}'", Color.green);
        UpdateUI();
    }

    private void StopListening()
    {
        _isListening = false;
        _currentRemappingAction = "";
        _listeningTimer = 0f;

        if (_listeningOverlay != null)
            _listeningOverlay.SetActive(false);
    }

    private void CancelRemapping()
    {
        StopListening();
        ShowStatus("Remapping cancelled", Color.gray);
    }

    private bool IsValidKey(KeyCode key)
    {
        return key != KeyCode.None && 
               key != KeyCode.Mouse0 && 
               key != KeyCode.Mouse1 && 
               key != KeyCode.Mouse2;
    }

    private bool IsKeyAlreadyMapped(KeyCode key, string excludeAction = "")
    {
        foreach (var mapping in _keyMappings)
        {
            if (mapping.Value == key && mapping.Key != excludeAction)
            {
                return true;
            }
        }
        return false;
    }

    private void UpdateUI()
    {
        foreach (var binding in _inputBindings)
        {
            if (binding.actionLabel != null)
            {
                var action = _inputActions.Find(a => a.actionName == binding.actionName);
                if (action != null)
                {
                    binding.actionLabel.text = action.description;
                }
            }

            if (binding.keyLabel != null && _keyMappings.ContainsKey(binding.actionName))
            {
                binding.keyLabel.text = _keyMappings[binding.actionName].ToString();
            }
        }
    }

    public void ResetToDefaults()
    {
        foreach (var action in _inputActions)
        {
            action.currentKey = action.defaultKey;
            _keyMappings[action.actionName] = action.defaultKey;
        }

        OnMappingsReset.Invoke();
        ShowStatus("Controls reset to defaults", Color.green);
        UpdateUI();
    }

    public void SaveMappings()
    {
        string json = JsonUtility.ToJson(new SerializableKeyMappings(_keyMappings));
        PlayerPrefs.SetString(_saveKey, json);
        PlayerPrefs.Save();

        foreach (var mapping in _keyMappings)
        {
            _originalMappings[mapping.Key] = mapping.Value;
        }

        OnMappingsSaved.Invoke();
        ShowStatus("Controls saved successfully", Color.green);
    }

    private void LoadMappings()
    {
        if (PlayerPrefs.HasKey(_saveKey))
        {
            string json = PlayerPrefs.GetString(_saveKey);
            try
            {
                var loadedMappings = JsonUtility.FromJson<SerializableKeyMappings>(json);
                if (loadedMappings != null && loadedMappings.keys != null)
                {
                    for (int i = 0; i < loadedMappings.keys.Count; i++)
                    {
                        string actionName = loadedMappings.keys[i];
                        KeyCode keyCode = loadedMappings.values[i];
                        
                        if (_keyMappings.ContainsKey(actionName))
                        {
                            _keyMappings[actionName] = keyCode;
                            var action = _inputActions.Find(a => a.actionName == actionName);
                            if (action != null)
                            {
                                action.currentKey = keyCode;
                            }
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load input mappings: {e.Message}");
            }
        }
    }

    public void OpenRemapPanel()
    {
        if (_remapPanel != null)
        {
            _remapPanel.SetActive(true);
            UpdateUI();
        }
    }

    public void CloseRemapPanel()
    {
        if (_remapPanel != null)
        {
            _remapPanel.SetActive(false);
        }
        
        if (_isListening)
        {
            CancelRemapping();
        }
    }

    private void ShowStatus(string message, Color color)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
            _statusText.color = color;
            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 3f);
        }
    }

    private void ClearStatus()
    {
        if (_statusText != null)
        {
            _statusText.text = "";
        }
    }

    [System.Serializable]
    private class SerializableKeyMappings
    {
        public List<string> keys = new List<string>();
        public List<KeyCode> values = new List<KeyCode>();

        public SerializableKeyMappings(Dictionary<string, KeyCode> mappings)
        {
            foreach (var mapping in mappings)
            {
                keys.Add(mapping.Key);
                values.Add(mapping.Value);
            }
        }
    }

    private void OnDestroy()
    {
        if (_resetButton != null)
            _resetButton.onClick.RemoveAllListeners();
            
        if (_saveButton != null)
            _saveButton.onClick.RemoveAllListeners();
            
        if (_cancelButton != null)
            _cancelButton.onClick.RemoveAllListeners();

        foreach (var binding in _inputBindings)
        {
            if (binding.remapButton != null)
                binding.remapButton.onClick.RemoveAllListeners();
        }
    }
}