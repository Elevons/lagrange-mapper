// Prompt: minimap marker for objectives
// Type: general

using UnityEngine;
using UnityEngine.UI;

public class MinimapObjectiveMarker : MonoBehaviour
{
    [Header("Marker Settings")]
    [SerializeField] private GameObject _markerPrefab;
    [SerializeField] private Transform _minimapCanvas;
    [SerializeField] private Camera _minimapCamera;
    [SerializeField] private RectTransform _minimapRect;
    
    [Header("Marker Appearance")]
    [SerializeField] private Color _activeColor = Color.yellow;
    [SerializeField] private Color _completedColor = Color.green;
    [SerializeField] private Color _failedColor = Color.red;
    [SerializeField] private float _markerSize = 20f;
    [SerializeField] private bool _showDistance = true;
    
    [Header("Objective Settings")]
    [SerializeField] private string _objectiveTitle = "Objective";
    [SerializeField] private string _objectiveDescription = "Complete this objective";
    [SerializeField] private bool _isCompleted = false;
    [SerializeField] private bool _isFailed = false;
    [SerializeField] private float _completionRadius = 5f;
    
    [Header("Animation")]
    [SerializeField] private bool _pulseAnimation = true;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private float _pulseScale = 1.2f;
    
    private GameObject _markerInstance;
    private Image _markerImage;
    private Text _distanceText;
    private Transform _player;
    private RectTransform _markerRect;
    private Vector3 _originalScale;
    private bool _isVisible = true;
    
    public enum ObjectiveState
    {
        Active,
        Completed,
        Failed,
        Hidden
    }
    
    private ObjectiveState _currentState = ObjectiveState.Active;
    
    void Start()
    {
        InitializeMarker();
        FindPlayer();
        SetupMarkerAppearance();
    }
    
    void Update()
    {
        if (_markerInstance == null || _player == null) return;
        
        UpdateMarkerPosition();
        UpdateMarkerVisibility();
        UpdateDistanceDisplay();
        UpdateObjectiveCompletion();
        
        if (_pulseAnimation && _currentState == ObjectiveState.Active)
        {
            AnimateMarker();
        }
    }
    
    private void InitializeMarker()
    {
        if (_minimapCanvas == null)
        {
            _minimapCanvas = FindObjectOfType<Canvas>()?.transform;
        }
        
        if (_minimapCamera == null)
        {
            _minimapCamera = Camera.main;
        }
        
        if (_minimapRect == null)
        {
            _minimapRect = _minimapCanvas?.GetComponent<RectTransform>();
        }
        
        CreateMarkerInstance();
    }
    
    private void CreateMarkerInstance()
    {
        if (_minimapCanvas == null) return;
        
        if (_markerPrefab != null)
        {
            _markerInstance = Instantiate(_markerPrefab, _minimapCanvas);
        }
        else
        {
            _markerInstance = new GameObject("ObjectiveMarker");
            _markerInstance.transform.SetParent(_minimapCanvas);
            
            _markerImage = _markerInstance.AddComponent<Image>();
            _markerImage.sprite = CreateCircleSprite();
        }
        
        _markerRect = _markerInstance.GetComponent<RectTransform>();
        if (_markerRect == null)
        {
            _markerRect = _markerInstance.AddComponent<RectTransform>();
        }
        
        _markerRect.sizeDelta = Vector2.one * _markerSize;
        _originalScale = _markerRect.localScale;
        
        if (_markerImage == null)
        {
            _markerImage = _markerInstance.GetComponent<Image>();
        }
        
        SetupDistanceText();
    }
    
    private void SetupDistanceText()
    {
        if (!_showDistance) return;
        
        GameObject textObj = new GameObject("DistanceText");
        textObj.transform.SetParent(_markerInstance.transform);
        
        _distanceText = textObj.AddComponent<Text>();
        _distanceText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        _distanceText.fontSize = 12;
        _distanceText.color = Color.white;
        _distanceText.alignment = TextAnchor.MiddleCenter;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchoredPosition = Vector2.zero;
        textRect.sizeDelta = new Vector2(50, 20);
        textRect.anchoredPosition = new Vector2(0, -25);
    }
    
    private Sprite CreateCircleSprite()
    {
        Texture2D texture = new Texture2D(32, 32);
        Color[] pixels = new Color[32 * 32];
        Vector2 center = new Vector2(16, 16);
        
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * 32 + x] = distance <= 15 ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));
    }
    
    private void FindPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _player = playerObj.transform;
        }
    }
    
    private void SetupMarkerAppearance()
    {
        if (_markerImage == null) return;
        
        switch (_currentState)
        {
            case ObjectiveState.Active:
                _markerImage.color = _activeColor;
                break;
            case ObjectiveState.Completed:
                _markerImage.color = _completedColor;
                break;
            case ObjectiveState.Failed:
                _markerImage.color = _failedColor;
                break;
            case ObjectiveState.Hidden:
                _markerInstance.SetActive(false);
                break;
        }
    }
    
    private void UpdateMarkerPosition()
    {
        if (_minimapCamera == null || _minimapRect == null) return;
        
        Vector3 worldPos = transform.position;
        Vector3 screenPos = _minimapCamera.WorldToScreenPoint(worldPos);
        
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _minimapRect, screenPos, null, out localPos);
        
        _markerRect.anchoredPosition = localPos;
    }
    
    private void UpdateMarkerVisibility()
    {
        if (_markerInstance == null) return;
        
        bool shouldBeVisible = _currentState != ObjectiveState.Hidden && _isVisible;
        _markerInstance.SetActive(shouldBeVisible);
    }
    
    private void UpdateDistanceDisplay()
    {
        if (_distanceText == null || _player == null) return;
        
        float distance = Vector3.Distance(transform.position, _player.position);
        _distanceText.text = $"{distance:F0}m";
    }
    
    private void UpdateObjectiveCompletion()
    {
        if (_player == null || _currentState != ObjectiveState.Active) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        if (distanceToPlayer <= _completionRadius && !_isCompleted)
        {
            CompleteObjective();
        }
    }
    
    private void AnimateMarker()
    {
        if (_markerRect == null) return;
        
        float scale = 1f + Mathf.Sin(Time.time * _pulseSpeed) * (_pulseScale - 1f) * 0.5f;
        _markerRect.localScale = _originalScale * scale;
    }
    
    public void SetObjectiveState(ObjectiveState state)
    {
        _currentState = state;
        
        switch (state)
        {
            case ObjectiveState.Completed:
                _isCompleted = true;
                break;
            case ObjectiveState.Failed:
                _isFailed = true;
                break;
        }
        
        SetupMarkerAppearance();
    }
    
    public void CompleteObjective()
    {
        SetObjectiveState(ObjectiveState.Completed);
        
        if (_pulseAnimation)
        {
            _markerRect.localScale = _originalScale;
        }
    }
    
    public void FailObjective()
    {
        SetObjectiveState(ObjectiveState.Failed);
        
        if (_pulseAnimation)
        {
            _markerRect.localScale = _originalScale;
        }
    }
    
    public void ShowMarker()
    {
        _isVisible = true;
        if (_currentState != ObjectiveState.Hidden)
        {
            _markerInstance?.SetActive(true);
        }
    }
    
    public void HideMarker()
    {
        _isVisible = false;
        _markerInstance?.SetActive(false);
    }
    
    public void SetMarkerColor(Color color)
    {
        if (_markerImage != null)
        {
            _markerImage.color = color;
        }
    }
    
    public void SetObjectiveTitle(string title)
    {
        _objectiveTitle = title;
    }
    
    public void SetObjectiveDescription(string description)
    {
        _objectiveDescription = description;
    }
    
    public string GetObjectiveTitle()
    {
        return _objectiveTitle;
    }
    
    public string GetObjectiveDescription()
    {
        return _objectiveDescription;
    }
    
    public ObjectiveState GetCurrentState()
    {
        return _currentState;
    }
    
    public bool IsCompleted()
    {
        return _isCompleted;
    }
    
    public bool IsFailed()
    {
        return _isFailed;
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _completionRadius);
    }
    
    void OnDestroy()
    {
        if (_markerInstance != null)
        {
            DestroyImmediate(_markerInstance);
        }
    }
}