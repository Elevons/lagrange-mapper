// Prompt: chat message bubble
// Type: general

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class ChatMessageBubble : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI _messageText;
    [SerializeField] private TextMeshProUGUI _senderNameText;
    [SerializeField] private Image _bubbleBackground;
    [SerializeField] private Image _avatarImage;
    [SerializeField] private RectTransform _bubbleRect;
    
    [Header("Bubble Settings")]
    [SerializeField] private Color _playerBubbleColor = new Color(0.2f, 0.6f, 1f, 0.8f);
    [SerializeField] private Color _otherBubbleColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
    [SerializeField] private Color _playerTextColor = Color.white;
    [SerializeField] private Color _otherTextColor = Color.black;
    [SerializeField] private float _maxBubbleWidth = 300f;
    [SerializeField] private float _minBubbleWidth = 100f;
    [SerializeField] private Vector2 _bubblePadding = new Vector2(20f, 15f);
    
    [Header("Animation Settings")]
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _scaleInDuration = 0.2f;
    [SerializeField] private AnimationCurve _scaleInCurve = AnimationCurve.EaseOutQuart(0, 0, 1, 1);
    [SerializeField] private bool _animateOnStart = true;
    
    [Header("Auto Destroy")]
    [SerializeField] private bool _autoDestroy = false;
    [SerializeField] private float _destroyDelay = 5f;
    
    private CanvasGroup _canvasGroup;
    private bool _isPlayerMessage;
    private string _messageContent;
    private string _senderName;
    
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (_bubbleRect == null)
        {
            _bubbleRect = GetComponent<RectTransform>();
        }
    }
    
    private void Start()
    {
        if (_animateOnStart)
        {
            AnimateIn();
        }
        
        if (_autoDestroy)
        {
            StartCoroutine(AutoDestroyCoroutine());
        }
    }
    
    public void SetMessage(string message, string senderName, bool isPlayerMessage, Sprite avatarSprite = null)
    {
        _messageContent = message;
        _senderName = senderName;
        _isPlayerMessage = isPlayerMessage;
        
        UpdateMessageDisplay();
        UpdateBubbleAppearance();
        UpdateBubbleSize();
        
        if (avatarSprite != null && _avatarImage != null)
        {
            _avatarImage.sprite = avatarSprite;
        }
    }
    
    private void UpdateMessageDisplay()
    {
        if (_messageText != null)
        {
            _messageText.text = _messageContent;
            _messageText.color = _isPlayerMessage ? _playerTextColor : _otherTextColor;
        }
        
        if (_senderNameText != null)
        {
            _senderNameText.text = _senderName;
            _senderNameText.color = _isPlayerMessage ? _playerTextColor : _otherTextColor;
        }
    }
    
    private void UpdateBubbleAppearance()
    {
        if (_bubbleBackground != null)
        {
            _bubbleBackground.color = _isPlayerMessage ? _playerBubbleColor : _otherBubbleColor;
        }
        
        // Flip bubble alignment for player vs other messages
        if (_bubbleRect != null)
        {
            Vector2 anchorMin = _bubbleRect.anchorMin;
            Vector2 anchorMax = _bubbleRect.anchorMax;
            Vector2 pivot = _bubbleRect.pivot;
            
            if (_isPlayerMessage)
            {
                // Align to right
                anchorMin.x = 1f;
                anchorMax.x = 1f;
                pivot.x = 1f;
            }
            else
            {
                // Align to left
                anchorMin.x = 0f;
                anchorMax.x = 0f;
                pivot.x = 0f;
            }
            
            _bubbleRect.anchorMin = anchorMin;
            _bubbleRect.anchorMax = anchorMax;
            _bubbleRect.pivot = pivot;
        }
    }
    
    private void UpdateBubbleSize()
    {
        if (_messageText == null || _bubbleRect == null) return;
        
        // Force text to update
        _messageText.ForceMeshUpdate();
        
        // Calculate preferred width based on text
        float preferredWidth = _messageText.preferredWidth + _bubblePadding.x * 2;
        preferredWidth = Mathf.Clamp(preferredWidth, _minBubbleWidth, _maxBubbleWidth);
        
        // Calculate preferred height based on text
        float preferredHeight = _messageText.preferredHeight + _bubblePadding.y * 2;
        
        // Add height for sender name if present
        if (_senderNameText != null && !string.IsNullOrEmpty(_senderName))
        {
            preferredHeight += _senderNameText.preferredHeight + 5f;
        }
        
        // Set the bubble size
        _bubbleRect.sizeDelta = new Vector2(preferredWidth, preferredHeight);
    }
    
    public void AnimateIn()
    {
        if (_canvasGroup != null)
        {
            StartCoroutine(FadeInCoroutine());
        }
        
        StartCoroutine(ScaleInCoroutine());
    }
    
    public void AnimateOut(System.Action onComplete = null)
    {
        StartCoroutine(AnimateOutCoroutine(onComplete));
    }
    
    private IEnumerator FadeInCoroutine()
    {
        _canvasGroup.alpha = 0f;
        float elapsedTime = 0f;
        
        while (elapsedTime < _fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            _canvasGroup.alpha = Mathf.Lerp(0f, 1f, elapsedTime / _fadeInDuration);
            yield return null;
        }
        
        _canvasGroup.alpha = 1f;
    }
    
    private IEnumerator ScaleInCoroutine()
    {
        Vector3 originalScale = transform.localScale;
        transform.localScale = Vector3.zero;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _scaleInDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _scaleInDuration;
            float curveValue = _scaleInCurve.Evaluate(progress);
            transform.localScale = Vector3.Lerp(Vector3.zero, originalScale, curveValue);
            yield return null;
        }
        
        transform.localScale = originalScale;
    }
    
    private IEnumerator AnimateOutCoroutine(System.Action onComplete)
    {
        float duration = 0.2f;
        Vector3 originalScale = transform.localScale;
        float originalAlpha = _canvasGroup != null ? _canvasGroup.alpha : 1f;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            transform.localScale = Vector3.Lerp(originalScale, Vector3.zero, progress);
            
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = Mathf.Lerp(originalAlpha, 0f, progress);
            }
            
            yield return null;
        }
        
        onComplete?.Invoke();
    }
    
    private IEnumerator AutoDestroyCoroutine()
    {
        yield return new WaitForSeconds(_destroyDelay);
        
        AnimateOut(() => {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        });
    }
    
    public void SetAutoDestroy(bool enable, float delay = 5f)
    {
        _autoDestroy = enable;
        _destroyDelay = delay;
        
        if (enable)
        {
            StopCoroutine(AutoDestroyCoroutine());
            StartCoroutine(AutoDestroyCoroutine());
        }
    }
    
    public void UpdateBubbleColors(Color bubbleColor, Color textColor)
    {
        if (_bubbleBackground != null)
        {
            _bubbleBackground.color = bubbleColor;
        }
        
        if (_messageText != null)
        {
            _messageText.color = textColor;
        }
        
        if (_senderNameText != null)
        {
            _senderNameText.color = textColor;
        }
    }
    
    public string GetMessage()
    {
        return _messageContent;
    }
    
    public string GetSenderName()
    {
        return _senderName;
    }
    
    public bool IsPlayerMessage()
    {
        return _isPlayerMessage;
    }
}