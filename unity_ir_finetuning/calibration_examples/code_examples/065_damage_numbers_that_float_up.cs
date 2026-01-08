// Prompt: damage numbers that float up
// Type: general

using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DamageNumber : MonoBehaviour
{
    [Header("Damage Display")]
    [SerializeField] private TextMeshProUGUI _damageText;
    [SerializeField] private float _damage;
    
    [Header("Animation Settings")]
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _fadeSpeed = 1f;
    [SerializeField] private float _lifetime = 2f;
    [SerializeField] private Vector3 _floatDirection = Vector3.up;
    [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1f);
    [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    
    [Header("Visual Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _criticalColor = Color.red;
    [SerializeField] private Color _healColor = Color.green;
    [SerializeField] private float _criticalThreshold = 50f;
    [SerializeField] private bool _randomizeDirection = true;
    [SerializeField] private float _randomAngle = 30f;
    
    private Vector3 _startPosition;
    private float _timer;
    private CanvasGroup _canvasGroup;
    private RectTransform _rectTransform;
    private bool _isInitialized;
    
    public enum DamageType
    {
        Normal,
        Critical,
        Heal
    }
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        
        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        if (_damageText == null)
        {
            _damageText = GetComponent<TextMeshProUGUI>();
            if (_damageText == null)
            {
                _damageText = GetComponentInChildren<TextMeshProUGUI>();
            }
        }
    }
    
    private void Start()
    {
        if (!_isInitialized)
        {
            Initialize(10f, DamageType.Normal);
        }
    }
    
    public void Initialize(float damage, DamageType damageType = DamageType.Normal)
    {
        _damage = damage;
        _startPosition = transform.position;
        _timer = 0f;
        _isInitialized = true;
        
        SetupText(damageType);
        SetupDirection();
        
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = 1f;
        }
        
        transform.localScale = Vector3.zero;
    }
    
    private void SetupText(DamageType damageType)
    {
        if (_damageText == null) return;
        
        string displayText = Mathf.Abs(_damage).ToString("F0");
        Color textColor = _normalColor;
        
        switch (damageType)
        {
            case DamageType.Critical:
                displayText = "CRIT! " + displayText;
                textColor = _criticalColor;
                break;
            case DamageType.Heal:
                displayText = "+" + displayText;
                textColor = _healColor;
                break;
            case DamageType.Normal:
                if (_damage >= _criticalThreshold)
                {
                    textColor = _criticalColor;
                }
                break;
        }
        
        _damageText.text = displayText;
        _damageText.color = textColor;
    }
    
    private void SetupDirection()
    {
        if (_randomizeDirection)
        {
            float randomX = Random.Range(-_randomAngle, _randomAngle);
            float randomY = Random.Range(-_randomAngle * 0.5f, _randomAngle);
            
            Vector3 randomDirection = Quaternion.Euler(0, 0, randomX) * _floatDirection;
            randomDirection.y += randomY * 0.01f;
            _floatDirection = randomDirection.normalized;
        }
    }
    
    private void Update()
    {
        if (!_isInitialized) return;
        
        _timer += Time.deltaTime;
        float normalizedTime = _timer / _lifetime;
        
        if (normalizedTime >= 1f)
        {
            DestroyDamageNumber();
            return;
        }
        
        UpdatePosition(normalizedTime);
        UpdateScale(normalizedTime);
        UpdateAlpha(normalizedTime);
    }
    
    private void UpdatePosition(float normalizedTime)
    {
        float movementProgress = _movementCurve.Evaluate(normalizedTime);
        Vector3 offset = _floatDirection * (_floatSpeed * movementProgress * _timer);
        transform.position = _startPosition + offset;
    }
    
    private void UpdateScale(float normalizedTime)
    {
        float scaleValue = _scaleCurve.Evaluate(normalizedTime);
        transform.localScale = Vector3.one * scaleValue;
    }
    
    private void UpdateAlpha(float normalizedTime)
    {
        if (_canvasGroup == null) return;
        
        float alpha = 1f - (normalizedTime * _fadeSpeed);
        alpha = Mathf.Clamp01(alpha);
        _canvasGroup.alpha = alpha;
    }
    
    private void DestroyDamageNumber()
    {
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }
    
    public static DamageNumber Create(Vector3 worldPosition, float damage, DamageType damageType = DamageType.Normal, Transform parent = null)
    {
        GameObject damageNumberPrefab = Resources.Load<GameObject>("DamageNumber");
        
        if (damageNumberPrefab == null)
        {
            GameObject newObj = new GameObject("DamageNumber");
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas != null)
            {
                newObj.transform.SetParent(canvas.transform, false);
            }
            
            TextMeshProUGUI text = newObj.AddComponent<TextMeshProUGUI>();
            text.text = "0";
            text.fontSize = 24;
            text.alignment = TextAlignmentOptions.Center;
            
            DamageNumber damageNumber = newObj.AddComponent<DamageNumber>();
            damageNumber._damageText = text;
            
            RectTransform rect = newObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 50);
            
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPosition);
            rect.position = screenPos;
            
            damageNumber.Initialize(damage, damageType);
            return damageNumber;
        }
        else
        {
            GameObject instance = Instantiate(damageNumberPrefab, parent);
            instance.transform.position = worldPosition;
            
            DamageNumber damageNumber = instance.GetComponent<DamageNumber>();
            if (damageNumber != null)
            {
                damageNumber.Initialize(damage, damageType);
            }
            
            return damageNumber;
        }
    }
}