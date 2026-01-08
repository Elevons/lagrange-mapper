// Prompt: tooltip on hover over item
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class TooltipOnHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    [Header("Tooltip Settings")]
    [SerializeField] private string _tooltipTitle = "Item Name";
    [SerializeField] private string _tooltipDescription = "Item description goes here";
    [SerializeField] private Sprite _tooltipIcon;
    [SerializeField] private float _showDelay = 0.5f;
    [SerializeField] private Vector2 _offset = new Vector2(10f, 10f);
    
    [Header("Tooltip Prefab")]
    [SerializeField] private GameObject _tooltipPrefab;
    
    private static TooltipDisplay _currentTooltip;
    private static Canvas _tooltipCanvas;
    private Coroutine _showTooltipCoroutine;
    
    private void Start()
    {
        if (_tooltipCanvas == null)
        {
            CreateTooltipCanvas();
        }
    }
    
    private void CreateTooltipCanvas()
    {
        GameObject canvasGO = new GameObject("TooltipCanvas");
        _tooltipCanvas = canvasGO.AddComponent<Canvas>();
        _tooltipCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _tooltipCanvas.sortingOrder = 1000;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasGO.AddComponent<GraphicRaycaster>();
        DontDestroyOnLoad(canvasGO);
    }
    
    public void OnPointerEnter(PointerEventData eventData)
    {
        _showTooltipCoroutine = StartCoroutine(ShowTooltipAfterDelay());
    }
    
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_showTooltipCoroutine != null)
        {
            StopCoroutine(_showTooltipCoroutine);
            _showTooltipCoroutine = null;
        }
        HideTooltip();
    }
    
    public void OnPointerMove(PointerEventData eventData)
    {
        if (_currentTooltip != null)
        {
            UpdateTooltipPosition(eventData.position);
        }
    }
    
    private System.Collections.IEnumerator ShowTooltipAfterDelay()
    {
        yield return new WaitForSeconds(_showDelay);
        ShowTooltip();
    }
    
    private void ShowTooltip()
    {
        if (_tooltipCanvas == null) return;
        
        HideTooltip();
        
        GameObject tooltipGO;
        if (_tooltipPrefab != null)
        {
            tooltipGO = Instantiate(_tooltipPrefab, _tooltipCanvas.transform);
        }
        else
        {
            tooltipGO = CreateDefaultTooltip();
        }
        
        _currentTooltip = tooltipGO.GetComponent<TooltipDisplay>();
        if (_currentTooltip == null)
        {
            _currentTooltip = tooltipGO.AddComponent<TooltipDisplay>();
        }
        
        _currentTooltip.SetupTooltip(_tooltipTitle, _tooltipDescription, _tooltipIcon);
        UpdateTooltipPosition(Input.mousePosition);
    }
    
    private GameObject CreateDefaultTooltip()
    {
        GameObject tooltipGO = new GameObject("Tooltip");
        tooltipGO.transform.SetParent(_tooltipCanvas.transform);
        
        Image background = tooltipGO.AddComponent<Image>();
        background.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
        
        RectTransform rectTransform = tooltipGO.GetComponent<RectTransform>();
        rectTransform.sizeDelta = new Vector2(300, 100);
        
        GameObject textGO = new GameObject("Text");
        textGO.transform.SetParent(tooltipGO.transform);
        
        TextMeshProUGUI text = textGO.AddComponent<TextMeshProUGUI>();
        text.text = _tooltipTitle + "\n" + _tooltipDescription;
        text.fontSize = 14;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.TopLeft;
        
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
        
        return tooltipGO;
    }
    
    private void UpdateTooltipPosition(Vector2 mousePosition)
    {
        if (_currentTooltip == null) return;
        
        RectTransform tooltipRect = _currentTooltip.GetComponent<RectTransform>();
        Vector2 position = mousePosition + _offset;
        
        Vector2 screenSize = new Vector2(Screen.width, Screen.height);
        Vector2 tooltipSize = tooltipRect.sizeDelta;
        
        if (position.x + tooltipSize.x > screenSize.x)
        {
            position.x = mousePosition.x - tooltipSize.x - _offset.x;
        }
        
        if (position.y - tooltipSize.y < 0)
        {
            position.y = mousePosition.y + tooltipSize.y + _offset.y;
        }
        
        tooltipRect.position = position;
    }
    
    private void HideTooltip()
    {
        if (_currentTooltip != null)
        {
            Destroy(_currentTooltip.gameObject);
            _currentTooltip = null;
        }
    }
    
    public void SetTooltipContent(string title, string description, Sprite icon = null)
    {
        _tooltipTitle = title;
        _tooltipDescription = description;
        _tooltipIcon = icon;
    }
}

public class TooltipDisplay : MonoBehaviour
{
    private TextMeshProUGUI _titleText;
    private TextMeshProUGUI _descriptionText;
    private Image _iconImage;
    
    private void Awake()
    {
        SetupComponents();
    }
    
    private void SetupComponents()
    {
        Transform titleTransform = transform.Find("Title");
        if (titleTransform != null)
        {
            _titleText = titleTransform.GetComponent<TextMeshProUGUI>();
        }
        
        Transform descTransform = transform.Find("Description");
        if (descTransform != null)
        {
            _descriptionText = descTransform.GetComponent<TextMeshProUGUI>();
        }
        
        Transform iconTransform = transform.Find("Icon");
        if (iconTransform != null)
        {
            _iconImage = iconTransform.GetComponent<Image>();
        }
    }
    
    public void SetupTooltip(string title, string description, Sprite icon)
    {
        if (_titleText != null)
        {
            _titleText.text = title;
        }
        
        if (_descriptionText != null)
        {
            _descriptionText.text = description;
        }
        
        if (_iconImage != null && icon != null)
        {
            _iconImage.sprite = icon;
            _iconImage.gameObject.SetActive(true);
        }
        else if (_iconImage != null)
        {
            _iconImage.gameObject.SetActive(false);
        }
        
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());
    }
}