// Prompt: crosshair that changes on hover
// Type: combat

using UnityEngine;
using UnityEngine.UI;

public class DynamicCrosshair : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [SerializeField] private Image _crosshairImage;
    [SerializeField] private Sprite _defaultCrosshair;
    [SerializeField] private Sprite _hoverCrosshair;
    [SerializeField] private Color _defaultColor = Color.white;
    [SerializeField] private Color _hoverColor = Color.red;
    
    [Header("Raycast Settings")]
    [SerializeField] private Camera _playerCamera;
    [SerializeField] private float _raycastDistance = 100f;
    [SerializeField] private LayerMask _interactableLayerMask = -1;
    [SerializeField] private string[] _interactableTags = { "Interactable", "Enemy", "Pickup" };
    
    [Header("Animation Settings")]
    [SerializeField] private bool _useAnimation = true;
    [SerializeField] private float _transitionSpeed = 5f;
    [SerializeField] private float _hoverScale = 1.2f;
    
    private bool _isHovering = false;
    private Vector3 _defaultScale;
    private Color _currentColor;
    private Vector3 _targetScale;
    
    private void Start()
    {
        if (_crosshairImage == null)
            _crosshairImage = GetComponent<Image>();
            
        if (_playerCamera == null)
            _playerCamera = Camera.main;
            
        if (_crosshairImage != null)
        {
            _defaultScale = _crosshairImage.transform.localScale;
            _targetScale = _defaultScale;
            _currentColor = _defaultColor;
            _crosshairImage.color = _currentColor;
            
            if (_defaultCrosshair != null)
                _crosshairImage.sprite = _defaultCrosshair;
        }
    }
    
    private void Update()
    {
        CheckForHoverTarget();
        UpdateCrosshairAppearance();
    }
    
    private void CheckForHoverTarget()
    {
        if (_playerCamera == null) return;
        
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f));
        RaycastHit hit;
        
        bool wasHovering = _isHovering;
        _isHovering = false;
        
        if (Physics.Raycast(ray, out hit, _raycastDistance, _interactableLayerMask))
        {
            GameObject hitObject = hit.collider.gameObject;
            
            foreach (string tag in _interactableTags)
            {
                if (hitObject.CompareTag(tag))
                {
                    _isHovering = true;
                    break;
                }
            }
        }
        
        if (_isHovering != wasHovering)
        {
            OnHoverStateChanged(_isHovering);
        }
    }
    
    private void OnHoverStateChanged(bool hovering)
    {
        if (_crosshairImage == null) return;
        
        if (hovering)
        {
            if (_hoverCrosshair != null)
                _crosshairImage.sprite = _hoverCrosshair;
                
            _targetScale = _defaultScale * _hoverScale;
        }
        else
        {
            if (_defaultCrosshair != null)
                _crosshairImage.sprite = _defaultCrosshair;
                
            _targetScale = _defaultScale;
        }
    }
    
    private void UpdateCrosshairAppearance()
    {
        if (_crosshairImage == null) return;
        
        Color targetColor = _isHovering ? _hoverColor : _defaultColor;
        
        if (_useAnimation)
        {
            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * _transitionSpeed);
            _crosshairImage.transform.localScale = Vector3.Lerp(_crosshairImage.transform.localScale, _targetScale, Time.deltaTime * _transitionSpeed);
        }
        else
        {
            _currentColor = targetColor;
            _crosshairImage.transform.localScale = _targetScale;
        }
        
        _crosshairImage.color = _currentColor;
    }
    
    public void SetCrosshairSprites(Sprite defaultSprite, Sprite hoverSprite)
    {
        _defaultCrosshair = defaultSprite;
        _hoverCrosshair = hoverSprite;
        
        if (_crosshairImage != null && !_isHovering)
            _crosshairImage.sprite = _defaultCrosshair;
    }
    
    public void SetCrosshairColors(Color defaultColor, Color hoverColor)
    {
        _defaultColor = defaultColor;
        _hoverColor = hoverColor;
    }
    
    public void SetRaycastDistance(float distance)
    {
        _raycastDistance = Mathf.Max(0f, distance);
    }
    
    public void AddInteractableTag(string tag)
    {
        if (System.Array.IndexOf(_interactableTags, tag) == -1)
        {
            string[] newTags = new string[_interactableTags.Length + 1];
            _interactableTags.CopyTo(newTags, 0);
            newTags[_interactableTags.Length] = tag;
            _interactableTags = newTags;
        }
    }
    
    public bool IsHoveringInteractable()
    {
        return _isHovering;
    }
}