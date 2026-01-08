// Prompt: input prompt icons
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class InputPromptIcons : MonoBehaviour
{
    [System.Serializable]
    public class InputPrompt
    {
        [Header("Input Configuration")]
        public string actionName;
        public InputActionReference inputAction;
        
        [Header("Visual Elements")]
        public Image iconImage;
        public Text promptText;
        public GameObject promptContainer;
        
        [Header("Icon Sprites")]
        public Sprite keyboardSprite;
        public Sprite gamepadSprite;
        public Sprite mouseSprite;
        
        [Header("Settings")]
        public bool showOnlyWhenRelevant = true;
        public float fadeSpeed = 2f;
        
        [HideInInspector]
        public CanvasGroup canvasGroup;
        [HideInInspector]
        public bool isVisible;
    }
    
    [Header("Input Prompts")]
    [SerializeField] private List<InputPrompt> _inputPrompts = new List<InputPrompt>();
    
    [Header("Detection Settings")]
    [SerializeField] private float _deviceCheckInterval = 0.5f;
    [SerializeField] private bool _autoDetectInputDevice = true;
    [SerializeField] private bool _hideInactivePrompts = true;
    
    [Header("Animation Settings")]
    [SerializeField] private bool _enableFadeAnimation = true;
    [SerializeField] private float _defaultFadeSpeed = 3f;
    [SerializeField] private AnimationCurve _fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Layout Settings")]
    [SerializeField] private bool _autoArrangePrompts = true;
    [SerializeField] private float _promptSpacing = 10f;
    [SerializeField] private bool _useHorizontalLayout = true;
    
    private enum InputDeviceType
    {
        Keyboard,
        Gamepad,
        Mouse
    }
    
    private InputDeviceType _currentDeviceType = InputDeviceType.Keyboard;
    private float _lastDeviceCheckTime;
    private Dictionary<string, InputPrompt> _promptLookup = new Dictionary<string, InputPrompt>();
    
    private void Start()
    {
        InitializePrompts();
        SetupCanvasGroups();
        
        if (_autoDetectInputDevice)
        {
            DetectInputDevice();
        }
        
        UpdateAllPrompts();
        
        if (_autoArrangePrompts)
        {
            ArrangePrompts();
        }
    }
    
    private void Update()
    {
        if (_autoDetectInputDevice && Time.time - _lastDeviceCheckTime >= _deviceCheckInterval)
        {
            DetectInputDevice();
            _lastDeviceCheckTime = Time.time;
        }
        
        UpdatePromptVisibility();
        HandleFadeAnimations();
    }
    
    private void InitializePrompts()
    {
        _promptLookup.Clear();
        
        foreach (var prompt in _inputPrompts)
        {
            if (!string.IsNullOrEmpty(prompt.actionName))
            {
                _promptLookup[prompt.actionName] = prompt;
            }
            
            if (prompt.promptContainer != null && prompt.canvasGroup == null)
            {
                prompt.canvasGroup = prompt.promptContainer.GetComponent<CanvasGroup>();
                if (prompt.canvasGroup == null)
                {
                    prompt.canvasGroup = prompt.promptContainer.AddComponent<CanvasGroup>();
                }
            }
        }
    }
    
    private void SetupCanvasGroups()
    {
        foreach (var prompt in _inputPrompts)
        {
            if (prompt.canvasGroup != null)
            {
                prompt.canvasGroup.alpha = prompt.isVisible ? 1f : 0f;
                prompt.canvasGroup.interactable = false;
                prompt.canvasGroup.blocksRaycasts = false;
            }
        }
    }
    
    private void DetectInputDevice()
    {
        InputDeviceType newDeviceType = _currentDeviceType;
        
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            newDeviceType = InputDeviceType.Gamepad;
        }
        else if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            newDeviceType = InputDeviceType.Keyboard;
        }
        else if (Mouse.current != null && (Mouse.current.leftButton.wasPressedThisFrame || 
                 Mouse.current.rightButton.wasPressedThisFrame || Mouse.current.middleButton.wasPressedThisFrame))
        {
            newDeviceType = InputDeviceType.Mouse;
        }
        
        if (newDeviceType != _currentDeviceType)
        {
            _currentDeviceType = newDeviceType;
            UpdateAllPrompts();
        }
    }
    
    private void UpdateAllPrompts()
    {
        foreach (var prompt in _inputPrompts)
        {
            UpdatePromptIcon(prompt);
            UpdatePromptText(prompt);
        }
    }
    
    private void UpdatePromptIcon(InputPrompt prompt)
    {
        if (prompt.iconImage == null) return;
        
        Sprite targetSprite = null;
        
        switch (_currentDeviceType)
        {
            case InputDeviceType.Keyboard:
                targetSprite = prompt.keyboardSprite;
                break;
            case InputDeviceType.Gamepad:
                targetSprite = prompt.gamepadSprite;
                break;
            case InputDeviceType.Mouse:
                targetSprite = prompt.mouseSprite;
                break;
        }
        
        if (targetSprite != null)
        {
            prompt.iconImage.sprite = targetSprite;
            prompt.iconImage.gameObject.SetActive(true);
        }
        else
        {
            prompt.iconImage.gameObject.SetActive(false);
        }
    }
    
    private void UpdatePromptText(InputPrompt prompt)
    {
        if (prompt.promptText == null || prompt.inputAction == null) return;
        
        string bindingText = GetBindingDisplayString(prompt.inputAction);
        prompt.promptText.text = bindingText;
    }
    
    private string GetBindingDisplayString(InputActionReference actionRef)
    {
        if (actionRef == null || actionRef.action == null) return "";
        
        var action = actionRef.action;
        int bindingIndex = GetBindingIndexForCurrentDevice(action);
        
        if (bindingIndex >= 0)
        {
            return action.GetBindingDisplayString(bindingIndex);
        }
        
        return action.GetBindingDisplayString();
    }
    
    private int GetBindingIndexForCurrentDevice(InputAction action)
    {
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            
            switch (_currentDeviceType)
            {
                case InputDeviceType.Keyboard:
                    if (binding.path.Contains("Keyboard"))
                        return i;
                    break;
                case InputDeviceType.Gamepad:
                    if (binding.path.Contains("Gamepad"))
                        return i;
                    break;
                case InputDeviceType.Mouse:
                    if (binding.path.Contains("Mouse"))
                        return i;
                    break;
            }
        }
        
        return -1;
    }
    
    private void UpdatePromptVisibility()
    {
        foreach (var prompt in _inputPrompts)
        {
            bool shouldBeVisible = ShouldPromptBeVisible(prompt);
            
            if (prompt.isVisible != shouldBeVisible)
            {
                prompt.isVisible = shouldBeVisible;
            }
        }
    }
    
    private bool ShouldPromptBeVisible(InputPrompt prompt)
    {
        if (!prompt.showOnlyWhenRelevant) return true;
        if (prompt.inputAction == null) return false;
        
        var action = prompt.inputAction.action;
        if (action == null) return false;
        
        return action.enabled && HasValidBindingForCurrentDevice(action);
    }
    
    private bool HasValidBindingForCurrentDevice(InputAction action)
    {
        return GetBindingIndexForCurrentDevice(action) >= 0;
    }
    
    private void HandleFadeAnimations()
    {
        if (!_enableFadeAnimation) return;
        
        foreach (var prompt in _inputPrompts)
        {
            if (prompt.canvasGroup == null) continue;
            
            float targetAlpha = prompt.isVisible ? 1f : 0f;
            float fadeSpeed = prompt.fadeSpeed > 0 ? prompt.fadeSpeed : _defaultFadeSpeed;
            
            if (Mathf.Abs(prompt.canvasGroup.alpha - targetAlpha) > 0.01f)
            {
                float newAlpha = Mathf.MoveTowards(prompt.canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
                newAlpha = _fadeCurve.Evaluate(newAlpha);
                prompt.canvasGroup.alpha = newAlpha;
            }
            
            if (_hideInactivePrompts && prompt.promptContainer != null)
            {
                prompt.promptContainer.SetActive(prompt.canvasGroup.alpha > 0.01f);
            }
        }
    }
    
    private void ArrangePrompts()
    {
        Vector3 currentPosition = transform.position;
        
        foreach (var prompt in _inputPrompts)
        {
            if (prompt.promptContainer == null) continue;
            
            prompt.promptContainer.transform.position = currentPosition;
            
            if (_useHorizontalLayout)
            {
                RectTransform rectTransform = prompt.promptContainer.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    currentPosition.x += rectTransform.rect.width + _promptSpacing;
                }
                else
                {
                    currentPosition.x += _promptSpacing;
                }
            }
            else
            {
                RectTransform rectTransform = prompt.promptContainer.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    currentPosition.y -= rectTransform.rect.height + _promptSpacing;
                }
                else
                {
                    currentPosition.y -= _promptSpacing;
                }
            }
        }
    }
    
    public void ShowPrompt(string actionName)
    {
        if (_promptLookup.TryGetValue(actionName, out InputPrompt prompt))
        {
            prompt.isVisible = true;
            if (prompt.promptContainer != null)
            {
                prompt.promptContainer.SetActive(true);
            }
        }
    }
    
    public void HidePrompt(string actionName)
    {
        if (_promptLookup.TryGetValue(actionName, out InputPrompt prompt))
        {
            prompt.isVisible = false;
        }
    }
    
    public void ShowAllPrompts()
    {
        foreach (var prompt in _inputPrompts)
        {
            prompt.isVisible = true;
            if (prompt.promptContainer != null)
            {
                prompt.promptContainer.SetActive(true);
            }
        }
    }
    
    public void HideAllPrompts()
    {
        foreach (var prompt in _inputPrompts)
        {
            prompt.isVisible = false;
        }
    }
    
    public void SetDeviceType(int deviceType)
    {
        if (deviceType >= 0 && deviceType < System.Enum.GetValues(typeof(InputDeviceType)).Length)
        {
            _currentDeviceType = (InputDeviceType)deviceType;
            UpdateAllPrompts();
        }
    }
    
    public void RefreshPrompts()
    {
        InitializePrompts();
        UpdateAllPrompts();
        
        if (_autoArrangePrompts)
        {
            ArrangePrompts();
        }
    }
}