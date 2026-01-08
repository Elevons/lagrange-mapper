// Prompt: button that triggers event once
// Type: general

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class OneTimeButton : MonoBehaviour
{
    [Header("Button Settings")]
    [SerializeField] private Button _button;
    [SerializeField] private bool _disableAfterUse = true;
    [SerializeField] private bool _hideAfterUse = false;
    
    [Header("Visual Feedback")]
    [SerializeField] private string _usedText = "Used";
    [SerializeField] private Color _disabledColor = Color.gray;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onButtonPressed;
    
    private bool _hasBeenUsed = false;
    private Text _buttonText;
    private Image _buttonImage;
    private string _originalText;
    private Color _originalColor;
    
    private void Awake()
    {
        if (_button == null)
            _button = GetComponent<Button>();
            
        if (_button != null)
        {
            _buttonText = _button.GetComponentInChildren<Text>();
            _buttonImage = _button.GetComponent<Image>();
            
            if (_buttonText != null)
                _originalText = _buttonText.text;
                
            if (_buttonImage != null)
                _originalColor = _buttonImage.color;
        }
    }
    
    private void Start()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClick);
        }
    }
    
    private void OnButtonClick()
    {
        if (_hasBeenUsed) return;
        
        _hasBeenUsed = true;
        _onButtonPressed?.Invoke();
        
        HandlePostClickBehavior();
    }
    
    private void HandlePostClickBehavior()
    {
        if (_button == null) return;
        
        if (_disableAfterUse)
        {
            _button.interactable = false;
            
            if (_buttonImage != null)
                _buttonImage.color = _disabledColor;
        }
        
        if (_buttonText != null && !string.IsNullOrEmpty(_usedText))
        {
            _buttonText.text = _usedText;
        }
        
        if (_hideAfterUse)
        {
            gameObject.SetActive(false);
        }
    }
    
    public void ResetButton()
    {
        _hasBeenUsed = false;
        
        if (_button != null)
        {
            _button.interactable = true;
            
            if (_buttonImage != null)
                _buttonImage.color = _originalColor;
        }
        
        if (_buttonText != null && !string.IsNullOrEmpty(_originalText))
        {
            _buttonText.text = _originalText;
        }
        
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
    }
    
    public bool HasBeenUsed => _hasBeenUsed;
    
    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClick);
        }
    }
}