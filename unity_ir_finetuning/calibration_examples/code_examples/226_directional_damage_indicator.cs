// Prompt: directional damage indicator
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class DirectionalDamageIndicator : MonoBehaviour
{
    [Header("Damage Indicator Settings")]
    [SerializeField] private GameObject _damageIndicatorPrefab;
    [SerializeField] private Transform _indicatorParent;
    [SerializeField] private float _indicatorDistance = 100f;
    [SerializeField] private float _indicatorDuration = 2f;
    [SerializeField] private float _fadeOutDuration = 0.5f;
    [SerializeField] private Color _damageColor = Color.red;
    [SerializeField] private Vector2 _indicatorSize = new Vector2(50f, 50f);
    
    [Header("Player Reference")]
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private Camera _playerCamera;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _damageSound;
    
    private List<DamageIndicatorInstance> _activeIndicators = new List<DamageIndicatorInstance>();
    private Canvas _canvas;
    
    [System.Serializable]
    private class DamageIndicatorInstance
    {
        public GameObject indicatorObject;
        public Image indicatorImage;
        public Vector3 damageSourcePosition;
        public float creationTime;
        public Coroutine fadeCoroutine;
    }
    
    private void Start()
    {
        InitializeComponents();
        CreateIndicatorPrefabIfNeeded();
    }
    
    private void InitializeComponents()
    {
        if (_playerTransform == null)
            _playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            
        if (_playerCamera == null)
            _playerCamera = Camera.main;
            
        if (_indicatorParent == null)
        {
            _canvas = FindObjectOfType<Canvas>();
            if (_canvas == null)
            {
                GameObject canvasGO = new GameObject("DamageIndicatorCanvas");
                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            _indicatorParent = _canvas.transform;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void CreateIndicatorPrefabIfNeeded()
    {
        if (_damageIndicatorPrefab == null)
        {
            _damageIndicatorPrefab = new GameObject("DamageIndicator");
            Image image = _damageIndicatorPrefab.AddComponent<Image>();
            
            Texture2D texture = new Texture2D(32, 32);
            Color[] pixels = new Color[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                int x = i % 32;
                int y = i / 32;
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(16, 16));
                if (distance < 12f)
                    pixels[i] = _damageColor;
                else
                    pixels[i] = Color.clear;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
            image.sprite = sprite;
            image.color = _damageColor;
            
            RectTransform rectTransform = _damageIndicatorPrefab.GetComponent<RectTransform>();
            rectTransform.sizeDelta = _indicatorSize;
        }
    }
    
    private void Update()
    {
        UpdateIndicatorPositions();
        CleanupExpiredIndicators();
    }
    
    private void UpdateIndicatorPositions()
    {
        if (_playerTransform == null || _playerCamera == null) return;
        
        for (int i = _activeIndicators.Count - 1; i >= 0; i--)
        {
            var indicator = _activeIndicators[i];
            if (indicator.indicatorObject == null) continue;
            
            Vector3 directionToDamage = (indicator.damageSourcePosition - _playerTransform.position).normalized;
            Vector3 screenCenter = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            
            Vector3 forward = _playerCamera.transform.forward;
            Vector3 right = _playerCamera.transform.right;
            
            float angle = Mathf.Atan2(Vector3.Dot(directionToDamage, right), Vector3.Dot(directionToDamage, forward)) * Mathf.Rad2Deg;
            
            Vector3 indicatorPosition = screenCenter + new Vector3(
                Mathf.Sin(angle * Mathf.Deg2Rad) * _indicatorDistance,
                Mathf.Cos(angle * Mathf.Deg2Rad) * _indicatorDistance,
                0f
            );
            
            RectTransform rectTransform = indicator.indicatorObject.GetComponent<RectTransform>();
            rectTransform.position = indicatorPosition;
            rectTransform.rotation = Quaternion.Euler(0f, 0f, -angle);
        }
    }
    
    private void CleanupExpiredIndicators()
    {
        for (int i = _activeIndicators.Count - 1; i >= 0; i--)
        {
            var indicator = _activeIndicators[i];
            if (Time.time - indicator.creationTime > _indicatorDuration)
            {
                if (indicator.indicatorObject != null)
                    Destroy(indicator.indicatorObject);
                _activeIndicators.RemoveAt(i);
            }
        }
    }
    
    public void ShowDamageIndicator(Vector3 damageSourcePosition)
    {
        if (_indicatorParent == null || _damageIndicatorPrefab == null) return;
        
        GameObject indicatorInstance = Instantiate(_damageIndicatorPrefab, _indicatorParent);
        Image indicatorImage = indicatorInstance.GetComponent<Image>();
        
        DamageIndicatorInstance newIndicator = new DamageIndicatorInstance
        {
            indicatorObject = indicatorInstance,
            indicatorImage = indicatorImage,
            damageSourcePosition = damageSourcePosition,
            creationTime = Time.time
        };
        
        _activeIndicators.Add(newIndicator);
        
        newIndicator.fadeCoroutine = StartCoroutine(FadeOutIndicator(newIndicator));
        
        PlayDamageSound();
    }
    
    private IEnumerator FadeOutIndicator(DamageIndicatorInstance indicator)
    {
        float fadeStartTime = _indicatorDuration - _fadeOutDuration;
        
        yield return new WaitForSeconds(fadeStartTime);
        
        if (indicator.indicatorImage != null)
        {
            Color originalColor = indicator.indicatorImage.color;
            float fadeTimer = 0f;
            
            while (fadeTimer < _fadeOutDuration && indicator.indicatorImage != null)
            {
                fadeTimer += Time.deltaTime;
                float alpha = Mathf.Lerp(originalColor.a, 0f, fadeTimer / _fadeOutDuration);
                indicator.indicatorImage.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
        }
    }
    
    private void PlayDamageSound()
    {
        if (_audioSource != null && _damageSound != null)
        {
            _audioSource.PlayOneShot(_damageSound);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Projectile") || other.CompareTag("Enemy"))
        {
            ShowDamageIndicator(other.transform.position);
        }
    }
    
    public void OnPlayerDamaged(Vector3 damageSourcePosition)
    {
        ShowDamageIndicator(damageSourcePosition);
    }
    
    private void OnDestroy()
    {
        foreach (var indicator in _activeIndicators)
        {
            if (indicator.indicatorObject != null)
                Destroy(indicator.indicatorObject);
        }
        _activeIndicators.Clear();
    }
}