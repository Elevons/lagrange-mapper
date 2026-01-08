// Prompt: achievement unlock banner
// Type: general

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class AchievementBanner : MonoBehaviour
{
    [System.Serializable]
    public class Achievement
    {
        public string title;
        public string description;
        public Sprite icon;
        public Color backgroundColor = Color.white;
        public AudioClip unlockSound;
    }

    [Header("UI References")]
    [SerializeField] private GameObject _bannerPanel;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Image _iconImage;
    [SerializeField] private TextMeshProUGUI _titleText;
    [SerializeField] private TextMeshProUGUI _descriptionText;
    [SerializeField] private Button _closeButton;

    [Header("Animation Settings")]
    [SerializeField] private float _slideInDuration = 0.5f;
    [SerializeField] private float _displayDuration = 3f;
    [SerializeField] private float _slideOutDuration = 0.5f;
    [SerializeField] private AnimationCurve _slideInCurve = AnimationCurve.EaseOut(0, 0, 1, 1);
    [SerializeField] private AnimationCurve _slideOutCurve = AnimationCurve.EaseIn(0, 0, 1, 1);

    [Header("Position Settings")]
    [SerializeField] private Vector2 _hiddenPosition = new Vector2(0, 100);
    [SerializeField] private Vector2 _visiblePosition = Vector2.zero;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _defaultUnlockSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _celebrationParticles;
    [SerializeField] private bool _useScreenShake = true;
    [SerializeField] private float _shakeIntensity = 0.1f;
    [SerializeField] private float _shakeDuration = 0.2f;

    private RectTransform _bannerRect;
    private Queue<Achievement> _achievementQueue = new Queue<Achievement>();
    private bool _isDisplaying = false;
    private Coroutine _displayCoroutine;
    private Vector3 _originalCameraPosition;
    private Camera _mainCamera;

    private void Awake()
    {
        _bannerRect = _bannerPanel.GetComponent<RectTransform>();
        _mainCamera = Camera.main;
        
        if (_mainCamera != null)
            _originalCameraPosition = _mainCamera.transform.position;

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_closeButton != null)
            _closeButton.onClick.AddListener(DismissBanner);
    }

    private void Start()
    {
        if (_bannerPanel != null)
        {
            _bannerPanel.SetActive(false);
            _bannerRect.anchoredPosition = _hiddenPosition;
        }
    }

    public void UnlockAchievement(Achievement achievement)
    {
        if (achievement == null) return;

        _achievementQueue.Enqueue(achievement);
        
        if (!_isDisplaying)
        {
            ProcessNextAchievement();
        }
    }

    public void UnlockAchievement(string title, string description, Sprite icon = null, Color? backgroundColor = null, AudioClip unlockSound = null)
    {
        Achievement achievement = new Achievement
        {
            title = title,
            description = description,
            icon = icon,
            backgroundColor = backgroundColor ?? Color.white,
            unlockSound = unlockSound
        };

        UnlockAchievement(achievement);
    }

    private void ProcessNextAchievement()
    {
        if (_achievementQueue.Count == 0) return;

        Achievement achievement = _achievementQueue.Dequeue();
        _displayCoroutine = StartCoroutine(DisplayAchievementCoroutine(achievement));
    }

    private IEnumerator DisplayAchievementCoroutine(Achievement achievement)
    {
        _isDisplaying = true;

        SetupBannerContent(achievement);
        _bannerPanel.SetActive(true);

        // Play unlock sound
        PlayUnlockSound(achievement.unlockSound);

        // Screen shake effect
        if (_useScreenShake && _mainCamera != null)
        {
            StartCoroutine(ScreenShakeCoroutine());
        }

        // Slide in animation
        yield return StartCoroutine(SlideBannerCoroutine(_hiddenPosition, _visiblePosition, _slideInDuration, _slideInCurve));

        // Trigger celebration particles
        if (_celebrationParticles != null)
        {
            _celebrationParticles.Play();
        }

        // Display duration
        yield return new WaitForSeconds(_displayDuration);

        // Slide out animation
        yield return StartCoroutine(SlideBannerCoroutine(_visiblePosition, _hiddenPosition, _slideOutDuration, _slideOutCurve));

        _bannerPanel.SetActive(false);
        _isDisplaying = false;

        // Process next achievement if any
        if (_achievementQueue.Count > 0)
        {
            yield return new WaitForSeconds(0.2f);
            ProcessNextAchievement();
        }
    }

    private void SetupBannerContent(Achievement achievement)
    {
        if (_titleText != null)
            _titleText.text = achievement.title;

        if (_descriptionText != null)
            _descriptionText.text = achievement.description;

        if (_iconImage != null)
        {
            _iconImage.sprite = achievement.icon;
            _iconImage.gameObject.SetActive(achievement.icon != null);
        }

        if (_backgroundImage != null)
            _backgroundImage.color = achievement.backgroundColor;
    }

    private IEnumerator SlideBannerCoroutine(Vector2 startPos, Vector2 endPos, float duration, AnimationCurve curve)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = curve.Evaluate(t);
            
            _bannerRect.anchoredPosition = Vector2.Lerp(startPos, endPos, curveValue);
            
            yield return null;
        }

        _bannerRect.anchoredPosition = endPos;
    }

    private IEnumerator ScreenShakeCoroutine()
    {
        if (_mainCamera == null) yield break;

        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            Vector3 randomOffset = Random.insideUnitSphere * _shakeIntensity;
            randomOffset.z = 0f;
            
            _mainCamera.transform.position = _originalCameraPosition + randomOffset;
            
            yield return null;
        }

        _mainCamera.transform.position = _originalCameraPosition;
    }

    private void PlayUnlockSound(AudioClip customSound)
    {
        if (_audioSource == null) return;

        AudioClip soundToPlay = customSound != null ? customSound : _defaultUnlockSound;
        
        if (soundToPlay != null)
        {
            _audioSource.PlayOneShot(soundToPlay);
        }
    }

    public void DismissBanner()
    {
        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
        }

        StartCoroutine(DismissBannerCoroutine());
    }

    private IEnumerator DismissBannerCoroutine()
    {
        yield return StartCoroutine(SlideBannerCoroutine(_bannerRect.anchoredPosition, _hiddenPosition, _slideOutDuration * 0.5f, _slideOutCurve));
        
        _bannerPanel.SetActive(false);
        _isDisplaying = false;

        if (_achievementQueue.Count > 0)
        {
            yield return new WaitForSeconds(0.2f);
            ProcessNextAchievement();
        }
    }

    public void ClearQueue()
    {
        _achievementQueue.Clear();
        
        if (_isDisplaying)
        {
            DismissBanner();
        }
    }

    public int GetQueueCount()
    {
        return _achievementQueue.Count;
    }

    public bool IsDisplaying()
    {
        return _isDisplaying;
    }

    private void OnDestroy()
    {
        if (_closeButton != null)
            _closeButton.onClick.RemoveListener(DismissBanner);
    }
}