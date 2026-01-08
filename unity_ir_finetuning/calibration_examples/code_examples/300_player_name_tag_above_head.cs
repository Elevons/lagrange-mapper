// Prompt: player name tag above head
// Type: movement

using UnityEngine;
using UnityEngine.UI;

public class PlayerNameTag : MonoBehaviour
{
    [Header("Name Tag Settings")]
    [SerializeField] private string _playerName = "Player";
    [SerializeField] private float _heightOffset = 2.0f;
    [SerializeField] private bool _alwaysFaceCamera = true;
    [SerializeField] private float _fadeDistance = 20.0f;
    [SerializeField] private bool _hideWhenTooClose = true;
    [SerializeField] private float _minDistance = 2.0f;
    
    [Header("UI Components")]
    [SerializeField] private Canvas _nameTagCanvas;
    [SerializeField] private Text _nameText;
    [SerializeField] private CanvasGroup _canvasGroup;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _nameColor = Color.white;
    [SerializeField] private int _fontSize = 24;
    [SerializeField] private Font _font;
    
    private Camera _mainCamera;
    private Transform _playerTransform;
    private Vector3 _originalScale;
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        _playerTransform = transform;
        
        CreateNameTagUI();
        SetupCanvas();
        UpdateNameDisplay();
        
        if (_nameTagCanvas != null)
            _originalScale = _nameTagCanvas.transform.localScale;
    }
    
    private void CreateNameTagUI()
    {
        if (_nameTagCanvas == null)
        {
            GameObject canvasGO = new GameObject("NameTagCanvas");
            canvasGO.transform.SetParent(transform);
            canvasGO.transform.localPosition = Vector3.up * _heightOffset;
            
            _nameTagCanvas = canvasGO.AddComponent<Canvas>();
            _canvasGroup = canvasGO.AddComponent<CanvasGroup>();
            canvasGO.AddComponent<CanvasScaler>();
        }
        
        if (_nameText == null)
        {
            GameObject textGO = new GameObject("NameText");
            textGO.transform.SetParent(_nameTagCanvas.transform);
            
            _nameText = textGO.AddComponent<Text>();
            
            RectTransform rectTransform = _nameText.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
        }
    }
    
    private void SetupCanvas()
    {
        if (_nameTagCanvas == null) return;
        
        _nameTagCanvas.renderMode = RenderMode.WorldSpace;
        _nameTagCanvas.worldCamera = _mainCamera;
        
        RectTransform canvasRect = _nameTagCanvas.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(200, 50);
        canvasRect.localScale = Vector3.one * 0.01f;
    }
    
    private void UpdateNameDisplay()
    {
        if (_nameText == null) return;
        
        _nameText.text = _playerName;
        _nameText.color = _nameColor;
        _nameText.fontSize = _fontSize;
        _nameText.alignment = TextAnchor.MiddleCenter;
        
        if (_font != null)
            _nameText.font = _font;
    }
    
    private void Update()
    {
        if (_mainCamera == null || _nameTagCanvas == null) return;
        
        HandleCameraFacing();
        HandleDistanceFading();
        UpdatePosition();
    }
    
    private void HandleCameraFacing()
    {
        if (!_alwaysFaceCamera) return;
        
        Vector3 directionToCamera = _mainCamera.transform.position - _nameTagCanvas.transform.position;
        directionToCamera.y = 0;
        
        if (directionToCamera != Vector3.zero)
        {
            Quaternion lookRotation = Quaternion.LookRotation(-directionToCamera);
            _nameTagCanvas.transform.rotation = lookRotation;
        }
    }
    
    private void HandleDistanceFading()
    {
        if (_canvasGroup == null) return;
        
        float distance = Vector3.Distance(_mainCamera.transform.position, _playerTransform.position);
        
        if (_hideWhenTooClose && distance < _minDistance)
        {
            _canvasGroup.alpha = 0f;
            return;
        }
        
        if (distance > _fadeDistance)
        {
            _canvasGroup.alpha = 0f;
        }
        else
        {
            float fadeRatio = 1f - (distance / _fadeDistance);
            _canvasGroup.alpha = Mathf.Clamp01(fadeRatio);
        }
    }
    
    private void UpdatePosition()
    {
        if (_nameTagCanvas == null) return;
        
        Vector3 targetPosition = _playerTransform.position + Vector3.up * _heightOffset;
        _nameTagCanvas.transform.position = targetPosition;
    }
    
    public void SetPlayerName(string newName)
    {
        _playerName = newName;
        UpdateNameDisplay();
    }
    
    public void SetNameColor(Color newColor)
    {
        _nameColor = newColor;
        UpdateNameDisplay();
    }
    
    public void SetHeightOffset(float newOffset)
    {
        _heightOffset = newOffset;
    }
    
    public void ShowNameTag()
    {
        if (_nameTagCanvas != null)
            _nameTagCanvas.gameObject.SetActive(true);
    }
    
    public void HideNameTag()
    {
        if (_nameTagCanvas != null)
            _nameTagCanvas.gameObject.SetActive(false);
    }
    
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            UpdateNameDisplay();
        }
    }
}