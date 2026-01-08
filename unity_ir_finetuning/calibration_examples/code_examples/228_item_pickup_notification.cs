// Prompt: item pickup notification
// Type: pickup

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class ItemPickupNotification : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Canvas _notificationCanvas;
    [SerializeField] private GameObject _notificationPrefab;
    [SerializeField] private Transform _notificationParent;
    
    [Header("Notification Settings")]
    [SerializeField] private float _displayDuration = 3f;
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.5f;
    [SerializeField] private Vector3 _slideOffset = new Vector3(0, 50f, 0);
    [SerializeField] private int _maxNotifications = 5;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pickupSound;
    
    private Queue<NotificationData> _notificationQueue = new Queue<NotificationData>();
    private List<GameObject> _activeNotifications = new List<GameObject>();
    
    [System.Serializable]
    public class NotificationData
    {
        public string itemName;
        public Sprite itemIcon;
        public Color backgroundColor = Color.white;
        public string description;
        
        public NotificationData(string name, Sprite icon, string desc = "")
        {
            itemName = name;
            itemIcon = icon;
            description = desc;
        }
    }
    
    private void Start()
    {
        if (_notificationCanvas == null)
            _notificationCanvas = FindObjectOfType<Canvas>();
            
        if (_notificationParent == null && _notificationCanvas != null)
            _notificationParent = _notificationCanvas.transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        CreateNotificationPrefab();
    }
    
    private void CreateNotificationPrefab()
    {
        if (_notificationPrefab != null) return;
        
        _notificationPrefab = new GameObject("NotificationPrefab");
        _notificationPrefab.transform.SetParent(_notificationParent, false);
        
        RectTransform rectTransform = _notificationPrefab.AddComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300f, 80f);
        
        Image background = _notificationPrefab.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        GameObject iconObject = new GameObject("Icon");
        iconObject.transform.SetParent(_notificationPrefab.transform, false);
        RectTransform iconRect = iconObject.AddComponent<RectTransform>();
        iconRect.sizeDelta = new Vector2(60f, 60f);
        iconRect.anchoredPosition = new Vector2(-100f, 0f);
        Image iconImage = iconObject.AddComponent<Image>();
        
        GameObject textObject = new GameObject("Text");
        textObject.transform.SetParent(_notificationPrefab.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.sizeDelta = new Vector2(200f, 60f);
        textRect.anchoredPosition = new Vector2(50f, 10f);
        Text itemText = textObject.AddComponent<Text>();
        itemText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        itemText.fontSize = 16;
        itemText.color = Color.white;
        itemText.alignment = TextAnchor.MiddleLeft;
        
        GameObject descObject = new GameObject("Description");
        descObject.transform.SetParent(_notificationPrefab.transform, false);
        RectTransform descRect = descObject.AddComponent<RectTransform>();
        descRect.sizeDelta = new Vector2(200f, 30f);
        descRect.anchoredPosition = new Vector2(50f, -15f);
        Text descText = descObject.AddComponent<Text>();
        descText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        descText.fontSize = 12;
        descText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        descText.alignment = TextAnchor.MiddleLeft;
        
        CanvasGroup canvasGroup = _notificationPrefab.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        
        _notificationPrefab.SetActive(false);
    }
    
    public void ShowNotification(string itemName, Sprite itemIcon = null, string description = "")
    {
        NotificationData data = new NotificationData(itemName, itemIcon, description);
        _notificationQueue.Enqueue(data);
        
        if (_audioSource != null && _pickupSound != null)
            _audioSource.PlayOneShot(_pickupSound);
            
        ProcessNotificationQueue();
    }
    
    public void ShowNotification(NotificationData data)
    {
        _notificationQueue.Enqueue(data);
        
        if (_audioSource != null && _pickupSound != null)
            _audioSource.PlayOneShot(_pickupSound);
            
        ProcessNotificationQueue();
    }
    
    private void ProcessNotificationQueue()
    {
        if (_notificationQueue.Count == 0) return;
        if (_activeNotifications.Count >= _maxNotifications) return;
        
        NotificationData data = _notificationQueue.Dequeue();
        GameObject notification = CreateNotification(data);
        
        if (notification != null)
        {
            _activeNotifications.Add(notification);
            StartCoroutine(AnimateNotification(notification));
        }
    }
    
    private GameObject CreateNotification(NotificationData data)
    {
        if (_notificationPrefab == null || _notificationParent == null) return null;
        
        GameObject notification = Instantiate(_notificationPrefab, _notificationParent);
        notification.SetActive(true);
        
        RectTransform rectTransform = notification.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(1f, 1f);
        rectTransform.anchorMax = new Vector2(1f, 1f);
        rectTransform.pivot = new Vector2(1f, 1f);
        
        float yOffset = -10f - (_activeNotifications.Count * 90f);
        rectTransform.anchoredPosition = new Vector2(-10f, yOffset);
        
        Image background = notification.GetComponent<Image>();
        if (background != null)
            background.color = data.backgroundColor;
        
        Transform iconTransform = notification.transform.Find("Icon");
        if (iconTransform != null)
        {
            Image iconImage = iconTransform.GetComponent<Image>();
            if (iconImage != null && data.itemIcon != null)
            {
                iconImage.sprite = data.itemIcon;
                iconImage.color = Color.white;
            }
            else if (iconImage != null)
            {
                iconImage.color = Color.clear;
            }
        }
        
        Transform textTransform = notification.transform.Find("Text");
        if (textTransform != null)
        {
            Text itemText = textTransform.GetComponent<Text>();
            if (itemText != null)
                itemText.text = data.itemName;
        }
        
        Transform descTransform = notification.transform.Find("Description");
        if (descTransform != null)
        {
            Text descText = descTransform.GetComponent<Text>();
            if (descText != null)
                descText.text = data.description;
        }
        
        return notification;
    }
    
    private IEnumerator AnimateNotification(GameObject notification)
    {
        if (notification == null) yield break;
        
        CanvasGroup canvasGroup = notification.GetComponent<CanvasGroup>();
        RectTransform rectTransform = notification.GetComponent<RectTransform>();
        
        if (canvasGroup == null || rectTransform == null) yield break;
        
        Vector3 originalPosition = rectTransform.anchoredPosition;
        Vector3 startPosition = originalPosition + _slideOffset;
        rectTransform.anchoredPosition = startPosition;
        
        float elapsedTime = 0f;
        while (elapsedTime < _fadeInDuration)
        {
            if (notification == null) yield break;
            
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _fadeInDuration;
            
            canvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            rectTransform.anchoredPosition = Vector3.Lerp(startPosition, originalPosition, progress);
            
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        rectTransform.anchoredPosition = originalPosition;
        
        yield return new WaitForSeconds(_displayDuration);
        
        elapsedTime = 0f;
        while (elapsedTime < _fadeOutDuration)
        {
            if (notification == null) yield break;
            
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _fadeOutDuration;
            
            canvasGroup.alpha = Mathf.Lerp(1f, 0f, progress);
            rectTransform.anchoredPosition = Vector3.Lerp(originalPosition, startPosition, progress);
            
            yield return null;
        }
        
        if (notification != null)
        {
            _activeNotifications.Remove(notification);
            Destroy(notification);
            
            UpdateNotificationPositions();
            ProcessNotificationQueue();
        }
    }
    
    private void UpdateNotificationPositions()
    {
        for (int i = 0; i < _activeNotifications.Count; i++)
        {
            if (_activeNotifications[i] != null)
            {
                RectTransform rectTransform = _activeNotifications[i].GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float yOffset = -10f - (i * 90f);
                    Vector2 targetPosition = new Vector2(-10f, yOffset);
                    StartCoroutine(SmoothMoveToPosition(rectTransform, targetPosition, 0.3f));
                }
            }
        }
    }
    
    private IEnumerator SmoothMoveToPosition(RectTransform rectTransform, Vector2 targetPosition, float duration)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            if (rectTransform == null) yield break;
            
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / duration;
            
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, progress);
            
            yield return null;
        }
        
        if (rectTransform != null)
            rectTransform.anchoredPosition = targetPosition;
    }
    
    public void ClearAllNotifications()
    {
        StopAllCoroutines();
        
        foreach (GameObject notification in _activeNotifications)
        {
            if (notification != null)
                Destroy(notification);
        }
        
        _activeNotifications.Clear();
        _notificationQueue.Clear();
    }
    
    private void OnDestroy()
    {
        ClearAllNotifications();
    }
}