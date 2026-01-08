// Prompt: notification popup
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class NotificationPopup : MonoBehaviour
{
    [System.Serializable]
    public class NotificationData
    {
        public string title;
        public string message;
        public Sprite icon;
        public Color backgroundColor = Color.white;
        public float duration = 3f;
        public NotificationType type = NotificationType.Info;
    }

    [System.Serializable]
    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }

    [System.Serializable]
    public class NotificationTypeSettings
    {
        public NotificationType type;
        public Color backgroundColor;
        public Color textColor;
        public Sprite defaultIcon;
    }

    [Header("UI References")]
    [SerializeField] private GameObject _notificationPrefab;
    [SerializeField] private Transform _notificationContainer;
    [SerializeField] private Canvas _canvas;

    [Header("Animation Settings")]
    [SerializeField] private AnimationCurve _slideInCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve _slideOutCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    [SerializeField] private float _animationDuration = 0.5f;
    [SerializeField] private Vector2 _slideOffset = new Vector2(300f, 0f);

    [Header("Notification Settings")]
    [SerializeField] private int _maxNotifications = 5;
    [SerializeField] private float _defaultDuration = 3f;
    [SerializeField] private float _notificationSpacing = 10f;
    [SerializeField] private NotificationTypeSettings[] _typeSettings;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _notificationSound;

    [Header("Events")]
    public UnityEvent<string> OnNotificationShown;
    public UnityEvent<string> OnNotificationClosed;

    private Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
    private List<GameObject> _activeNotifications = new List<GameObject>();
    private Dictionary<NotificationType, NotificationTypeSettings> _typeSettingsDict;
    private bool _isProcessingQueue = false;

    private void Awake()
    {
        InitializeTypeSettings();
        SetupCanvas();
    }

    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }

    private void InitializeTypeSettings()
    {
        _typeSettingsDict = new Dictionary<NotificationType, NotificationTypeSettings>();
        
        if (_typeSettings != null)
        {
            foreach (var setting in _typeSettings)
            {
                _typeSettingsDict[setting.type] = setting;
            }
        }

        // Add default settings if not provided
        if (!_typeSettingsDict.ContainsKey(NotificationType.Info))
            _typeSettingsDict[NotificationType.Info] = new NotificationTypeSettings 
            { 
                type = NotificationType.Info, 
                backgroundColor = new Color(0.2f, 0.6f, 1f, 0.9f), 
                textColor = Color.white 
            };

        if (!_typeSettingsDict.ContainsKey(NotificationType.Warning))
            _typeSettingsDict[NotificationType.Warning] = new NotificationTypeSettings 
            { 
                type = NotificationType.Warning, 
                backgroundColor = new Color(1f, 0.8f, 0f, 0.9f), 
                textColor = Color.black 
            };

        if (!_typeSettingsDict.ContainsKey(NotificationType.Error))
            _typeSettingsDict[NotificationType.Error] = new NotificationTypeSettings 
            { 
                type = NotificationType.Error, 
                backgroundColor = new Color(1f, 0.3f, 0.3f, 0.9f), 
                textColor = Color.white 
            };

        if (!_typeSettingsDict.ContainsKey(NotificationType.Success))
            _typeSettingsDict[NotificationType.Success] = new NotificationTypeSettings 
            { 
                type = NotificationType.Success, 
                backgroundColor = new Color(0.3f, 0.8f, 0.3f, 0.9f), 
                textColor = Color.white 
            };
    }

    private void SetupCanvas()
    {
        if (_canvas == null)
            _canvas = GetComponentInParent<Canvas>();

        if (_canvas == null)
        {
            GameObject canvasGO = new GameObject("NotificationCanvas");
            canvasGO.transform.SetParent(transform);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 1000;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();
        }

        if (_notificationContainer == null)
        {
            GameObject containerGO = new GameObject("NotificationContainer");
            containerGO.transform.SetParent(_canvas.transform);
            _notificationContainer = containerGO.transform;
            
            RectTransform containerRect = containerGO.AddComponent<RectTransform>();
            containerRect.anchorMin = new Vector2(1f, 1f);
            containerRect.anchorMax = new Vector2(1f, 1f);
            containerRect.pivot = new Vector2(1f, 1f);
            containerRect.anchoredPosition = new Vector2(-20f, -20f);
            containerRect.sizeDelta = new Vector2(400f, 600f);

            VerticalLayoutGroup layoutGroup = containerGO.AddComponent<VerticalLayoutGroup>();
            layoutGroup.childAlignment = TextAnchor.UpperRight;
            layoutGroup.spacing = _notificationSpacing;
            layoutGroup.childControlHeight = false;
            layoutGroup.childControlWidth = false;
            layoutGroup.childForceExpandHeight = false;
            layoutGroup.childForceExpandWidth = false;

            ContentSizeFitter sizeFitter = containerGO.AddComponent<ContentSizeFitter>();
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
    }

    public void ShowNotification(string message)
    {
        ShowNotification("", message, null, NotificationType.Info, _defaultDuration);
    }

    public void ShowNotification(string title, string message)
    {
        ShowNotification(title, message, null, NotificationType.Info, _defaultDuration);
    }

    public void ShowNotification(string title, string message, NotificationType type)
    {
        ShowNotification(title, message, null, type, _defaultDuration);
    }

    public void ShowNotification(string title, string message, Sprite icon, NotificationType type, float duration = -1f)
    {
        if (duration < 0f)
            duration = _defaultDuration;

        NotificationData data = new NotificationData
        {
            title = title,
            message = message,
            icon = icon,
            type = type,
            duration = duration
        };

        if (_typeSettingsDict.ContainsKey(type))
        {
            data.backgroundColor = _typeSettingsDict[type].backgroundColor;
            if (icon == null)
                data.icon = _typeSettingsDict[type].defaultIcon;
        }

        _notificationQueue.Enqueue(data);
        
        if (!_isProcessingQueue)
            StartCoroutine(ProcessNotificationQueue());
    }

    private IEnumerator ProcessNotificationQueue()
    {
        _isProcessingQueue = true;

        while (_notificationQueue.Count > 0)
        {
            if (_activeNotifications.Count >= _maxNotifications)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }

            NotificationData data = _notificationQueue.Dequeue();
            yield return StartCoroutine(DisplayNotification(data));
        }

        _isProcessingQueue = false;
    }

    private IEnumerator DisplayNotification(NotificationData data)
    {
        GameObject notification = CreateNotificationObject(data);
        if (notification == null)
            yield break;

        _activeNotifications.Add(notification);
        
        PlayNotificationSound();
        OnNotificationShown?.Invoke(data.message);

        // Animate in
        yield return StartCoroutine(AnimateNotificationIn(notification));

        // Wait for duration
        yield return new WaitForSeconds(data.duration);

        // Animate out and destroy
        yield return StartCoroutine(AnimateNotificationOut(notification));
        
        _activeNotifications.Remove(notification);
        OnNotificationClosed?.Invoke(data.message);
        
        if (notification != null)
            Destroy(notification);
    }

    private GameObject CreateNotificationObject(NotificationData data)
    {
        GameObject notification;

        if (_notificationPrefab != null)
        {
            notification = Instantiate(_notificationPrefab, _notificationContainer);
        }
        else
        {
            notification = CreateDefaultNotification();
        }

        SetupNotificationContent(notification, data);
        return notification;
    }

    private GameObject CreateDefaultNotification()
    {
        GameObject notification = new GameObject("Notification");
        notification.transform.SetParent(_notificationContainer);

        RectTransform rect = notification.AddComponent<RectTransform>();
        rect.sizeDelta = new Vector2(350f, 80f);

        Image background = notification.AddComponent<Image>();
        background.color = Color.white;

        // Title
        GameObject titleGO = new GameObject("Title");
        titleGO.transform.SetParent(notification.transform);
        RectTransform titleRect = titleGO.AddComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.1f, 0.6f);
        titleRect.anchorMax = new Vector2(0.9f, 0.9f);
        titleRect.offsetMin = Vector2.zero;
        titleRect.offsetMax = Vector2.zero;

        TextMeshProUGUI titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.text = "";
        titleText.fontSize = 14f;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = Color.black;

        // Message
        GameObject messageGO = new GameObject("Message");
        messageGO.transform.SetParent(notification.transform);
        RectTransform messageRect = messageGO.AddComponent<RectTransform>();
        messageRect.anchorMin = new Vector2(0.1f, 0.1f);
        messageRect.anchorMax = new Vector2(0.9f, 0.6f);
        messageRect.offsetMin = Vector2.zero;
        messageRect.offsetMax = Vector2.zero;

        TextMeshProUGUI messageText = messageGO.AddComponent<TextMeshProUGUI>();
        messageText.text = "";
        messageText.fontSize = 12f;
        messageText.color = Color.black;

        // Icon
        GameObject iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(notification.transform);
        RectTransform iconRect = iconGO.AddComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0.02f, 0.2f);
        iconRect.anchorMax = new Vector2(0.08f, 0.8f);
        iconRect.offsetMin = Vector2.zero;
        iconRect.offsetMax = Vector2.zero;

        Image iconImage = iconGO.AddComponent<Image>();
        iconImage.color = Color.white;

        // Close button
        GameObject closeGO = new GameObject("CloseButton");
        closeGO.transform.SetParent(notification.transform);
        RectTransform closeRect = closeGO.AddComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(0.92f, 0.7f);
        closeRect.anchorMax = new Vector2(0.98f, 0.9f);
        closeRect.offsetMin = Vector2.zero;
        closeRect.offsetMax = Vector2.zero;

        Button closeButton = closeGO.AddComponent<Button>();
        Image closeImage = closeGO.AddComponent<Image>();
        closeImage.color = new Color(0.8f, 0.2f, 0.2f, 0.8f);

        closeButton.onClick.AddListener(() => StartCoroutine(CloseNotification(notification)));

        return notification;
    }

    private void SetupNotificationContent(GameObject notification, NotificationData data)
    {
        // Set background color
        Image background = notification.GetComponent<Image>();
        if (background != null)
            background.color = data.backgroundColor;

        // Set title
        TextMeshProUGUI titleText = notification.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
        if (titleText != null)
        {
            titleText.text = data.title;
            if (_typeSettingsDict.ContainsKey(data.type))
                titleText.color = _typeSettingsDict[data.type].textColor;
        }

        // Set message
        TextMeshProUGUI messageText = notification.transform.Find("Message")?.GetComponent<TextMeshProUGUI>();
        if (messageText != null)
        {
            messageText.text = data.message;
            if (_typeSettingsDict.ContainsKey(data.type))
                messageText.color = _typeSettingsDict[data.type].textColor;
        }

        // Set icon
        Image iconImage = notification.transform.Find("Icon")?.GetComponent<Image>();
        if (iconImage != null)
        {
            if (data.icon != null)
            {
                iconImage.sprite = data.icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false);
            }
        }
    }

    private IEnumerator AnimateNotificationIn(GameObject notification)
    {
        RectTransform rect = notification.GetComponent<RectTransform>();
        Vector2 targetPosition = rect.anchoredPosition;
        Vector2 startPosition = targetPosition + _slideOffset;
        
        rect.anchoredPosition = startPosition;
        
        float elapsed = 0f;
        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / _animationDuration;
            float curveValue = _slideInCurve.Evaluate(progress);
            
            rect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, curveValue);
            
            yield return null;
        }
        
        rect.anchoredPosition = targetPosition;
    }

    private IEnumerator AnimateNotificationOut(GameObject notification)
    {
        if (notification == null)
            yield break;

        RectTransform rect = notification.GetComponent<RectTransform>();
        Vector2 startPosition = rect.anchoredPosition;
        Vector2 targetPosition = startPosition + _slideOffset;
        
        float elapsed = 0f;
        while (elapsed < _animationDuration && notification != null)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / _animationDuration;
            float curveValue = _slideOutCurve.Evaluate(progress);