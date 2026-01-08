// Prompt: tutorial message system
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class TutorialMessageSystem : MonoBehaviour
{
    [System.Serializable]
    public class TutorialMessage
    {
        [Header("Message Content")]
        public string title;
        [TextArea(3, 6)]
        public string description;
        public Sprite icon;
        
        [Header("Display Settings")]
        public float displayDuration = 5f;
        public bool requiresInput = false;
        public KeyCode inputKey = KeyCode.Space;
        
        [Header("Trigger Settings")]
        public TriggerType triggerType = TriggerType.Manual;
        public string triggerTag = "Player";
        public float triggerDelay = 0f;
        
        [Header("Events")]
        public UnityEvent onMessageShow;
        public UnityEvent onMessageHide;
        
        [HideInInspector]
        public bool hasBeenShown = false;
    }
    
    public enum TriggerType
    {
        Manual,
        OnStart,
        OnTriggerEnter,
        OnCollisionEnter,
        OnKeyPress,
        Timed
    }
    
    [Header("Tutorial Messages")]
    [SerializeField] private List<TutorialMessage> _tutorialMessages = new List<TutorialMessage>();
    [SerializeField] private bool _showMessagesOnce = true;
    [SerializeField] private bool _showInOrder = false;
    
    [Header("UI References")]
    [SerializeField] private GameObject _messagePanel;
    [SerializeField] private Text _titleText;
    [SerializeField] private Text _descriptionText;
    [SerializeField] private Image _iconImage;
    [SerializeField] private Button _continueButton;
    [SerializeField] private Text _inputPromptText;
    
    [Header("Animation Settings")]
    [SerializeField] private bool _useAnimations = true;
    [SerializeField] private float _fadeInDuration = 0.5f;
    [SerializeField] private float _fadeOutDuration = 0.3f;
    [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _showMessageSound;
    [SerializeField] private AudioClip _hideMessageSound;
    
    [Header("Events")]
    public UnityEvent OnTutorialStart;
    public UnityEvent OnTutorialComplete;
    public UnityEvent<int> OnMessageChanged;
    
    private int _currentMessageIndex = 0;
    private bool _isShowingMessage = false;
    private Coroutine _currentDisplayCoroutine;
    private Coroutine _currentAnimationCoroutine;
    private CanvasGroup _panelCanvasGroup;
    private float _startTime;
    
    private void Awake()
    {
        InitializeComponents();
    }
    
    private void Start()
    {
        _startTime = Time.time;
        
        if (_messagePanel != null)
            _messagePanel.SetActive(false);
            
        CheckForStartTriggers();
    }
    
    private void Update()
    {
        CheckForKeyPressTriggers();
        CheckForTimedTriggers();
        HandleInputDuringMessage();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        CheckForTriggerMessages(other.gameObject, TriggerType.OnTriggerEnter);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        CheckForTriggerMessages(collision.gameObject, TriggerType.OnCollisionEnter);
    }
    
    private void InitializeComponents()
    {
        if (_messagePanel != null)
        {
            _panelCanvasGroup = _messagePanel.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
                _panelCanvasGroup = _messagePanel.AddComponent<CanvasGroup>();
        }
        
        if (_continueButton != null)
            _continueButton.onClick.AddListener(HideCurrentMessage);
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void CheckForStartTriggers()
    {
        for (int i = 0; i < _tutorialMessages.Count; i++)
        {
            if (_tutorialMessages[i].triggerType == TriggerType.OnStart)
            {
                if (_tutorialMessages[i].triggerDelay > 0)
                    StartCoroutine(ShowMessageDelayed(i, _tutorialMessages[i].triggerDelay));
                else
                    ShowMessage(i);
                    
                if (_showInOrder) break;
            }
        }
    }
    
    private void CheckForKeyPressTriggers()
    {
        for (int i = 0; i < _tutorialMessages.Count; i++)
        {
            var message = _tutorialMessages[i];
            if (message.triggerType == TriggerType.OnKeyPress && 
                Input.GetKeyDown(message.inputKey) &&
                (!_showMessagesOnce || !message.hasBeenShown))
            {
                ShowMessage(i);
                if (_showInOrder) break;
            }
        }
    }
    
    private void CheckForTimedTriggers()
    {
        float currentTime = Time.time - _startTime;
        
        for (int i = 0; i < _tutorialMessages.Count; i++)
        {
            var message = _tutorialMessages[i];
            if (message.triggerType == TriggerType.Timed && 
                currentTime >= message.triggerDelay &&
                (!_showMessagesOnce || !message.hasBeenShown))
            {
                ShowMessage(i);
                if (_showInOrder) break;
            }
        }
    }
    
    private void CheckForTriggerMessages(GameObject other, TriggerType triggerType)
    {
        for (int i = 0; i < _tutorialMessages.Count; i++)
        {
            var message = _tutorialMessages[i];
            if (message.triggerType == triggerType &&
                other.CompareTag(message.triggerTag) &&
                (!_showMessagesOnce || !message.hasBeenShown))
            {
                if (message.triggerDelay > 0)
                    StartCoroutine(ShowMessageDelayed(i, message.triggerDelay));
                else
                    ShowMessage(i);
                    
                if (_showInOrder) break;
            }
        }
    }
    
    private void HandleInputDuringMessage()
    {
        if (!_isShowingMessage) return;
        
        var currentMessage = _tutorialMessages[_currentMessageIndex];
        if (currentMessage.requiresInput && Input.GetKeyDown(currentMessage.inputKey))
        {
            HideCurrentMessage();
        }
    }
    
    public void ShowMessage(int messageIndex)
    {
        if (messageIndex < 0 || messageIndex >= _tutorialMessages.Count) return;
        if (_isShowingMessage) return;
        if (_showMessagesOnce && _tutorialMessages[messageIndex].hasBeenShown) return;
        
        _currentMessageIndex = messageIndex;
        var message = _tutorialMessages[messageIndex];
        
        _isShowingMessage = true;
        message.hasBeenShown = true;
        
        SetupMessageUI(message);
        
        if (_messagePanel != null)
            _messagePanel.SetActive(true);
            
        PlaySound(_showMessageSound);
        
        if (_useAnimations)
        {
            if (_currentAnimationCoroutine != null)
                StopCoroutine(_currentAnimationCoroutine);
            _currentAnimationCoroutine = StartCoroutine(AnimateMessageIn());
        }
        else if (_panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = 1f;
        }
        
        message.onMessageShow?.Invoke();
        OnMessageChanged?.Invoke(messageIndex);
        
        if (!message.requiresInput && message.displayDuration > 0)
        {
            _currentDisplayCoroutine = StartCoroutine(HideMessageAfterDelay(message.displayDuration));
        }
    }
    
    public void ShowNextMessage()
    {
        if (_showInOrder && _currentMessageIndex + 1 < _tutorialMessages.Count)
        {
            ShowMessage(_currentMessageIndex + 1);
        }
    }
    
    public void HideCurrentMessage()
    {
        if (!_isShowingMessage) return;
        
        var message = _tutorialMessages[_currentMessageIndex];
        
        if (_currentDisplayCoroutine != null)
        {
            StopCoroutine(_currentDisplayCoroutine);
            _currentDisplayCoroutine = null;
        }
        
        PlaySound(_hideMessageSound);
        
        if (_useAnimations)
        {
            if (_currentAnimationCoroutine != null)
                StopCoroutine(_currentAnimationCoroutine);
            _currentAnimationCoroutine = StartCoroutine(AnimateMessageOut());
        }
        else
        {
            CompleteHideMessage();
        }
        
        message.onMessageHide?.Invoke();
    }
    
    private void CompleteHideMessage()
    {
        _isShowingMessage = false;
        
        if (_messagePanel != null)
            _messagePanel.SetActive(false);
            
        if (_showInOrder)
            ShowNextMessage();
            
        CheckTutorialComplete();
    }
    
    private void SetupMessageUI(TutorialMessage message)
    {
        if (_titleText != null)
            _titleText.text = message.title;
            
        if (_descriptionText != null)
            _descriptionText.text = message.description;
            
        if (_iconImage != null)
        {
            _iconImage.sprite = message.icon;
            _iconImage.gameObject.SetActive(message.icon != null);
        }
        
        if (_continueButton != null)
            _continueButton.gameObject.SetActive(!message.requiresInput);
            
        if (_inputPromptText != null)
        {
            _inputPromptText.gameObject.SetActive(message.requiresInput);
            if (message.requiresInput)
                _inputPromptText.text = $"Press {message.inputKey} to continue";
        }
    }
    
    private void CheckTutorialComplete()
    {
        bool allShown = true;
        foreach (var message in _tutorialMessages)
        {
            if (!message.hasBeenShown)
            {
                allShown = false;
                break;
            }
        }
        
        if (allShown)
            OnTutorialComplete?.Invoke();
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }
    
    private IEnumerator ShowMessageDelayed(int messageIndex, float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowMessage(messageIndex);
    }
    
    private IEnumerator HideMessageAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideCurrentMessage();
    }
    
    private IEnumerator AnimateMessageIn()
    {
        if (_panelCanvasGroup == null) yield break;
        
        _panelCanvasGroup.alpha = 0f;
        float elapsed = 0f;
        
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / _fadeInDuration;
            _panelCanvasGroup.alpha = _animationCurve.Evaluate(progress);
            yield return null;
        }
        
        _panelCanvasGroup.alpha = 1f;
    }
    
    private IEnumerator AnimateMessageOut()
    {
        if (_panelCanvasGroup == null)
        {
            CompleteHideMessage();
            yield break;
        }
        
        float startAlpha = _panelCanvasGroup.alpha;
        float elapsed = 0f;
        
        while (elapsed < _fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / _fadeOutDuration;
            _panelCanvasGroup.alpha = startAlpha * (1f - _animationCurve.Evaluate(progress));
            yield return null;
        }
        
        _panelCanvasGroup.alpha = 0f;
        CompleteHideMessage();
    }
    
    public void ResetTutorial()
    {
        foreach (var message in _tutorialMessages)
        {
            message.hasBeenShown = false;
        }
        
        if (_isShowingMessage)
            HideCurrentMessage();
            
        _currentMessageIndex = 0;
        _startTime = Time.time;
    }
    
    public void SkipTutorial()
    {
        foreach (var message in _tutorialMessages)
        {
            message.hasBeenShown = true;
        }
        
        if (_isShowingMessage)
            HideCurrentMessage();
            
        OnTutorialComplete?.Invoke();
    }
}