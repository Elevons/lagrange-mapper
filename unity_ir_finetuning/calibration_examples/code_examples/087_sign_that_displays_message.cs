// Prompt: sign that displays message
// Type: general

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MessageSign : MonoBehaviour
{
    [Header("Sign Configuration")]
    [SerializeField] private string _signMessage = "Welcome to the game!";
    [SerializeField] private float _displayDistance = 3f;
    [SerializeField] private bool _requireInteraction = false;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    
    [Header("UI References")]
    [SerializeField] private Canvas _messageCanvas;
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private Text _legacyMessageText;
    [SerializeField] private GameObject _interactionPrompt;
    
    [Header("Display Settings")]
    [SerializeField] private float _fadeSpeed = 2f;
    [SerializeField] private bool _lookAtPlayer = true;
    [SerializeField] private Vector3 _canvasOffset = Vector3.up;
    
    private Transform _playerTransform;
    private bool _playerInRange = false;
    private bool _messageVisible = false;
    private CanvasGroup _canvasGroup;
    private Camera _mainCamera;
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        SetupCanvas();
        SetupMessageText();
        
        if (_messageCanvas != null)
            _messageCanvas.gameObject.SetActive(false);
            
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
    }
    
    private void SetupCanvas()
    {
        if (_messageCanvas == null)
        {
            GameObject canvasObj = new GameObject("MessageCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = _canvasOffset;
            
            _messageCanvas = canvasObj.AddComponent<Canvas>();
            _messageCanvas.renderMode = RenderMode.WorldSpace;
            _messageCanvas.worldCamera = _mainCamera;
            
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            
            RectTransform rectTransform = canvasObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(200, 100);
            rectTransform.localScale = Vector3.one * 0.01f;
        }
        
        _canvasGroup = _messageCanvas.GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = _messageCanvas.gameObject.AddComponent<CanvasGroup>();
            
        _canvasGroup.alpha = 0f;
    }
    
    private void SetupMessageText()
    {
        if (_messageText == null && _legacyMessageText == null)
        {
            GameObject textObj = new GameObject("MessageText");
            textObj.transform.SetParent(_messageCanvas.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            
            _messageText = textObj.AddComponent<TextMeshProUGUI>();
            if (_messageText != null)
            {
                _messageText.text = _signMessage;
                _messageText.fontSize = 24;
                _messageText.color = Color.white;
                _messageText.alignment = TextAlignmentOptions.Center;
            }
            else
            {
                _legacyMessageText = textObj.AddComponent<Text>();
                _legacyMessageText.text = _signMessage;
                _legacyMessageText.fontSize = 24;
                _legacyMessageText.color = Color.white;
                _legacyMessageText.alignment = TextAnchor.MiddleCenter;
                _legacyMessageText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
        }
        
        UpdateMessageText();
    }
    
    private void Update()
    {
        CheckForPlayer();
        HandleInteraction();
        UpdateCanvasRotation();
        UpdateCanvasAlpha();
    }
    
    private void CheckForPlayer()
    {
        if (_mainCamera == null) return;
        
        _playerTransform = _mainCamera.transform;
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        
        bool wasInRange = _playerInRange;
        _playerInRange = distanceToPlayer <= _displayDistance;
        
        if (_playerInRange && !wasInRange)
        {
            OnPlayerEnterRange();
        }
        else if (!_playerInRange && wasInRange)
        {
            OnPlayerExitRange();
        }
    }
    
    private void HandleInteraction()
    {
        if (!_playerInRange) return;
        
        if (_requireInteraction)
        {
            if (_interactionPrompt != null)
                _interactionPrompt.SetActive(true);
                
            if (Input.GetKeyDown(_interactionKey))
            {
                ToggleMessage();
            }
        }
        else
        {
            if (!_messageVisible)
                ShowMessage();
        }
    }
    
    private void UpdateCanvasRotation()
    {
        if (_messageCanvas == null || _playerTransform == null || !_lookAtPlayer) return;
        
        Vector3 directionToPlayer = _playerTransform.position - _messageCanvas.transform.position;
        directionToPlayer.y = 0;
        
        if (directionToPlayer != Vector3.zero)
        {
            _messageCanvas.transform.rotation = Quaternion.LookRotation(directionToPlayer);
        }
    }
    
    private void UpdateCanvasAlpha()
    {
        if (_canvasGroup == null) return;
        
        float targetAlpha = _messageVisible ? 1f : 0f;
        _canvasGroup.alpha = Mathf.MoveTowards(_canvasGroup.alpha, targetAlpha, _fadeSpeed * Time.deltaTime);
        
        if (_canvasGroup.alpha <= 0f && _messageCanvas.gameObject.activeSelf)
        {
            _messageCanvas.gameObject.SetActive(false);
        }
    }
    
    private void OnPlayerEnterRange()
    {
        if (!_requireInteraction)
        {
            ShowMessage();
        }
    }
    
    private void OnPlayerExitRange()
    {
        HideMessage();
        
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
    }
    
    private void ShowMessage()
    {
        if (_messageCanvas != null)
        {
            _messageCanvas.gameObject.SetActive(true);
            _messageVisible = true;
        }
    }
    
    private void HideMessage()
    {
        _messageVisible = false;
    }
    
    private void ToggleMessage()
    {
        if (_messageVisible)
            HideMessage();
        else
            ShowMessage();
    }
    
    private void UpdateMessageText()
    {
        if (_messageText != null)
            _messageText.text = _signMessage;
        else if (_legacyMessageText != null)
            _legacyMessageText.text = _signMessage;
    }
    
    public void SetMessage(string newMessage)
    {
        _signMessage = newMessage;
        UpdateMessageText();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _displayDistance);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position + _canvasOffset, Vector3.one * 0.5f);
    }
}