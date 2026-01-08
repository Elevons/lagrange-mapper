// Prompt: buff and debuff icons
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class BuffDebuffIconManager : MonoBehaviour
{
    [System.Serializable]
    public class BuffDebuffData
    {
        public string name;
        public Sprite icon;
        public Color iconColor = Color.white;
        public bool isBuff = true;
        public float duration;
        public string description;
    }

    [System.Serializable]
    public class ActiveEffect
    {
        public BuffDebuffData data;
        public float remainingTime;
        public GameObject iconObject;
        public Image iconImage;
        public Image fillImage;
        public Text durationText;
        
        public ActiveEffect(BuffDebuffData effectData, float time)
        {
            data = effectData;
            remainingTime = time;
        }
    }

    [Header("UI References")]
    [SerializeField] private Transform _iconContainer;
    [SerializeField] private GameObject _iconPrefab;
    [SerializeField] private Canvas _canvas;

    [Header("Layout Settings")]
    [SerializeField] private float _iconSize = 50f;
    [SerializeField] private float _iconSpacing = 5f;
    [SerializeField] private int _maxIconsPerRow = 8;
    [SerializeField] private bool _separateBuffsDebuffs = true;

    [Header("Animation Settings")]
    [SerializeField] private float _fadeInDuration = 0.3f;
    [SerializeField] private float _fadeOutDuration = 0.2f;
    [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0, 0.8f, 1, 1f);

    [Header("Visual Settings")]
    [SerializeField] private Color _buffBorderColor = Color.green;
    [SerializeField] private Color _debuffBorderColor = Color.red;
    [SerializeField] private bool _showDurationText = true;
    [SerializeField] private bool _showTooltips = true;

    [Header("Available Effects")]
    [SerializeField] private List<BuffDebuffData> _availableEffects = new List<BuffDebuffData>();

    private List<ActiveEffect> _activeEffects = new List<ActiveEffect>();
    private Dictionary<string, ActiveEffect> _effectLookup = new Dictionary<string, ActiveEffect>();
    private Coroutine _updateCoroutine;

    private void Start()
    {
        InitializeIconContainer();
        _updateCoroutine = StartCoroutine(UpdateEffectsCoroutine());
    }

    private void InitializeIconContainer()
    {
        if (_iconContainer == null)
        {
            GameObject container = new GameObject("BuffDebuffContainer");
            container.transform.SetParent(transform);
            _iconContainer = container.transform;
            
            RectTransform rectTransform = container.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 1f);
            rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rectTransform.pivot = new Vector2(0.5f, 1f);
            rectTransform.anchoredPosition = new Vector2(0, -10f);
        }

        if (_canvas == null)
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas == null)
            {
                _canvas = FindObjectOfType<Canvas>();
            }
        }

        CreateIconPrefabIfNeeded();
    }

    private void CreateIconPrefabIfNeeded()
    {
        if (_iconPrefab == null)
        {
            _iconPrefab = new GameObject("BuffDebuffIcon");
            
            RectTransform rectTransform = _iconPrefab.AddComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(_iconSize, _iconSize);
            
            Image backgroundImage = _iconPrefab.AddComponent<Image>();
            backgroundImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            
            GameObject iconChild = new GameObject("Icon");
            iconChild.transform.SetParent(_iconPrefab.transform);
            RectTransform iconRect = iconChild.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = Vector2.one * 2f;
            iconRect.offsetMax = Vector2.one * -2f;
            Image iconImage = iconChild.AddComponent<Image>();
            
            GameObject fillChild = new GameObject("Fill");
            fillChild.transform.SetParent(_iconPrefab.transform);
            RectTransform fillRect = fillChild.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fillChild.AddComponent<Image>();
            fillImage.color = new Color(1f, 1f, 1f, 0.3f);
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Radial360;
            
            if (_showDurationText)
            {
                GameObject textChild = new GameObject("Duration");
                textChild.transform.SetParent(_iconPrefab.transform);
                RectTransform textRect = textChild.AddComponent<RectTransform>();
                textRect.anchorMin = new Vector2(0f, 0f);
                textRect.anchorMax = new Vector2(1f, 0.3f);
                textRect.offsetMin = Vector2.zero;
                textRect.offsetMax = Vector2.zero;
                Text durationText = textChild.AddComponent<Text>();
                durationText.text = "";
                durationText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                durationText.fontSize = 10;
                durationText.color = Color.white;
                durationText.alignment = TextAnchor.MiddleCenter;
            }
            
            _iconPrefab.SetActive(false);
        }
    }

    public void AddEffect(string effectName, float duration = -1f)
    {
        BuffDebuffData effectData = GetEffectData(effectName);
        if (effectData == null)
        {
            Debug.LogWarning($"Effect '{effectName}' not found in available effects.");
            return;
        }

        float effectDuration = duration > 0 ? duration : effectData.duration;
        
        if (_effectLookup.ContainsKey(effectName))
        {
            RefreshEffect(effectName, effectDuration);
        }
        else
        {
            CreateNewEffect(effectData, effectDuration);
        }
        
        UpdateIconLayout();
    }

    public void RemoveEffect(string effectName)
    {
        if (_effectLookup.ContainsKey(effectName))
        {
            ActiveEffect effect = _effectLookup[effectName];
            StartCoroutine(RemoveEffectCoroutine(effect));
        }
    }

    public void RemoveAllEffects()
    {
        List<ActiveEffect> effectsToRemove = new List<ActiveEffect>(_activeEffects);
        foreach (ActiveEffect effect in effectsToRemove)
        {
            StartCoroutine(RemoveEffectCoroutine(effect));
        }
    }

    public bool HasEffect(string effectName)
    {
        return _effectLookup.ContainsKey(effectName);
    }

    public float GetEffectRemainingTime(string effectName)
    {
        if (_effectLookup.ContainsKey(effectName))
        {
            return _effectLookup[effectName].remainingTime;
        }
        return 0f;
    }

    private BuffDebuffData GetEffectData(string effectName)
    {
        return _availableEffects.Find(effect => effect.name == effectName);
    }

    private void RefreshEffect(string effectName, float newDuration)
    {
        ActiveEffect effect = _effectLookup[effectName];
        effect.remainingTime = newDuration;
        
        if (effect.iconObject != null)
        {
            StartCoroutine(RefreshEffectAnimation(effect.iconObject));
        }
    }

    private void CreateNewEffect(BuffDebuffData effectData, float duration)
    {
        GameObject iconObject = Instantiate(_iconPrefab, _iconContainer);
        iconObject.SetActive(true);
        
        ActiveEffect newEffect = new ActiveEffect(effectData, duration);
        newEffect.iconObject = iconObject;
        
        SetupIconComponents(newEffect);
        _activeEffects.Add(newEffect);
        _effectLookup[effectData.name] = newEffect;
        
        StartCoroutine(FadeInEffect(newEffect));
    }

    private void SetupIconComponents(ActiveEffect effect)
    {
        Image backgroundImage = effect.iconObject.GetComponent<Image>();
        backgroundImage.color = effect.data.isBuff ? _buffBorderColor : _debuffBorderColor;
        
        Transform iconTransform = effect.iconObject.transform.Find("Icon");
        if (iconTransform != null)
        {
            effect.iconImage = iconTransform.GetComponent<Image>();
            effect.iconImage.sprite = effect.data.icon;
            effect.iconImage.color = effect.data.iconColor;
        }
        
        Transform fillTransform = effect.iconObject.transform.Find("Fill");
        if (fillTransform != null)
        {
            effect.fillImage = fillTransform.GetComponent<Image>();
        }
        
        if (_showDurationText)
        {
            Transform textTransform = effect.iconObject.transform.Find("Duration");
            if (textTransform != null)
            {
                effect.durationText = textTransform.GetComponent<Text>();
            }
        }
        
        if (_showTooltips)
        {
            SetupTooltip(effect);
        }
    }

    private void SetupTooltip(ActiveEffect effect)
    {
        TooltipTrigger tooltip = effect.iconObject.GetComponent<TooltipTrigger>();
        if (tooltip == null)
        {
            tooltip = effect.iconObject.AddComponent<TooltipTrigger>();
        }
        tooltip.tooltipText = $"{effect.data.name}\n{effect.data.description}";
    }

    private IEnumerator UpdateEffectsCoroutine()
    {
        while (true)
        {
            UpdateActiveEffects();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void UpdateActiveEffects()
    {
        List<ActiveEffect> effectsToRemove = new List<ActiveEffect>();
        
        foreach (ActiveEffect effect in _activeEffects)
        {
            if (effect.data.duration > 0)
            {
                effect.remainingTime -= 0.1f;
                
                if (effect.remainingTime <= 0)
                {
                    effectsToRemove.Add(effect);
                }
                else
                {
                    UpdateEffectVisuals(effect);
                }
            }
        }
        
        foreach (ActiveEffect effect in effectsToRemove)
        {
            StartCoroutine(RemoveEffectCoroutine(effect));
        }
    }

    private void UpdateEffectVisuals(ActiveEffect effect)
    {
        if (effect.fillImage != null && effect.data.duration > 0)
        {
            float fillAmount = effect.remainingTime / effect.data.duration;
            effect.fillImage.fillAmount = fillAmount;
        }
        
        if (effect.durationText != null && _showDurationText)
        {
            if (effect.remainingTime > 60f)
            {
                effect.durationText.text = $"{Mathf.CeilToInt(effect.remainingTime / 60f)}m";
            }
            else if (effect.remainingTime > 0)
            {
                effect.durationText.text = Mathf.CeilToInt(effect.remainingTime).ToString();
            }
        }
    }

    private void UpdateIconLayout()
    {
        if (_separateBuffsDebuffs)
        {
            LayoutSeparatedIcons();
        }
        else
        {
            LayoutMixedIcons();
        }
    }

    private void LayoutSeparatedIcons()
    {
        List<ActiveEffect> buffs = _activeEffects.FindAll(e => e.data.isBuff);
        List<ActiveEffect> debuffs = _activeEffects.FindAll(e => !e.data.isBuff);
        
        float currentY = 0f;
        
        if (buffs.Count > 0)
        {
            LayoutEffectRow(buffs, ref currentY);
            currentY -= _iconSize + _iconSpacing;
        }
        
        if (debuffs.Count > 0)
        {
            LayoutEffectRow(debuffs, ref currentY);
        }
    }

    private void LayoutMixedIcons()
    {
        float currentY = 0f;
        LayoutEffectRow(_activeEffects, ref currentY);
    }

    private void LayoutEffectRow(List<ActiveEffect> effects, ref float currentY)
    {
        int iconsInRow = 0;
        float currentX = 0f;
        
        for (int i = 0; i < effects.Count; i++)
        {
            if (iconsInRow >= _maxIconsPerRow)
            {
                iconsInRow = 0;
                currentY -= _iconSize + _iconSpacing;
                currentX = 0f;
            }
            
            if (iconsInRow == 0)
            {
                int iconsThisRow = Mathf.Min(_maxIconsPerRow, effects.Count - i);
                float totalWidth = iconsThisRow * _iconSize + (iconsThisRow - 1) * _iconSpacing;
                currentX = -totalWidth * 0.5f + _iconSize * 0.5f;
            }
            
            RectTransform iconRect = effects[i].iconObject.GetComponent<RectTransform>();
            iconRect.anchoredPosition = new Vector2(currentX, currentY);
            
            currentX += _iconSize + _iconSpacing;
            iconsInRow++;
        }
    }

    private IEnumerator FadeInEffect(ActiveEffect effect)
    {
        CanvasGroup canvasGroup = effect.iconObject.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = effect.iconObject.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.alpha = 0f;
        effect.iconObject.transform.localScale = Vector3.one * 0.8f;
        
        float elapsed = 0f;
        while (elapsed < _fadeInDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / _fadeInDuration;
            
            canvasGroup.alpha = progress;
            effect.iconObject.transform.localScale = Vector3.one * _scaleCurve.Evaluate(progress);
            
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        effect.iconObject.transform.localScale = Vector3.one;
    }

    private IEnumerator RemoveEffectCoroutine(ActiveEffect effect)
    {
        _activeEffects.Remove(effect);
        _effectLookup.Remove(effect.data.name);