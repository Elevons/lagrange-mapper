// Prompt: credits scroll
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;

public class CreditsScroll : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private float _scrollSpeed = 50f;
    [SerializeField] private float _startDelay = 1f;
    [SerializeField] private bool _autoStart = true;
    [SerializeField] private bool _loopCredits = false;
    
    [Header("Boundaries")]
    [SerializeField] private float _startYPosition = -500f;
    [SerializeField] private float _endYPosition = 1000f;
    [SerializeField] private bool _useScreenBounds = true;
    
    [Header("Control Settings")]
    [SerializeField] private KeyCode _skipKey = KeyCode.Escape;
    [SerializeField] private KeyCode _pauseKey = KeyCode.Space;
    [SerializeField] private float _mouseScrollSensitivity = 100f;
    [SerializeField] private bool _allowManualControl = true;
    
    [Header("Events")]
    [SerializeField] private UnityEvent _onCreditsStart = new UnityEvent();
    [SerializeField] private UnityEvent _onCreditsComplete = new UnityEvent();
    [SerializeField] private UnityEvent _onCreditsSkipped = new UnityEvent();
    
    private RectTransform _rectTransform;
    private Canvas _parentCanvas;
    private bool _isScrolling = false;
    private bool _isPaused = false;
    private float _currentSpeed;
    private Vector3 _initialPosition;
    private Coroutine _scrollCoroutine;
    
    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        if (_rectTransform == null)
        {
            Debug.LogError("CreditsScroll requires a RectTransform component!");
            enabled = false;
            return;
        }
        
        _parentCanvas = GetComponentInParent<Canvas>();
        _currentSpeed = _scrollSpeed;
        _initialPosition = _rectTransform.anchoredPosition;
    }
    
    private void Start()
    {
        SetupInitialPosition();
        
        if (_autoStart)
        {
            StartCredits();
        }
    }
    
    private void Update()
    {
        if (!_isScrolling) return;
        
        HandleInput();
        
        if (_allowManualControl && !_isPaused)
        {
            HandleManualScroll();
        }
    }
    
    private void SetupInitialPosition()
    {
        if (_useScreenBounds && _parentCanvas != null)
        {
            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                _startYPosition = -canvasRect.rect.height / 2 - _rectTransform.rect.height / 2;
                _endYPosition = canvasRect.rect.height / 2 + _rectTransform.rect.height / 2;
            }
        }
        
        Vector3 startPos = _rectTransform.anchoredPosition;
        startPos.y = _startYPosition;
        _rectTransform.anchoredPosition = startPos;
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_skipKey))
        {
            SkipCredits();
        }
        
        if (Input.GetKeyDown(_pauseKey))
        {
            TogglePause();
        }
    }
    
    private void HandleManualScroll()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollInput) > 0.01f)
        {
            Vector3 currentPos = _rectTransform.anchoredPosition;
            currentPos.y += scrollInput * _mouseScrollSensitivity;
            currentPos.y = Mathf.Clamp(currentPos.y, _startYPosition, _endYPosition);
            _rectTransform.anchoredPosition = currentPos;
        }
    }
    
    public void StartCredits()
    {
        if (_isScrolling) return;
        
        _isScrolling = true;
        _isPaused = false;
        _onCreditsStart.Invoke();
        
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
        }
        
        _scrollCoroutine = StartCoroutine(ScrollCreditsCoroutine());
    }
    
    public void StopCredits()
    {
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = null;
        }
        
        _isScrolling = false;
        _isPaused = false;
    }
    
    public void SkipCredits()
    {
        if (!_isScrolling) return;
        
        StopCredits();
        _onCreditsSkipped.Invoke();
        
        if (_loopCredits)
        {
            ResetPosition();
            StartCredits();
        }
    }
    
    public void TogglePause()
    {
        if (!_isScrolling) return;
        
        _isPaused = !_isPaused;
    }
    
    public void SetScrollSpeed(float speed)
    {
        _scrollSpeed = Mathf.Max(0f, speed);
        _currentSpeed = _scrollSpeed;
    }
    
    public void ResetPosition()
    {
        Vector3 resetPos = _rectTransform.anchoredPosition;
        resetPos.y = _startYPosition;
        _rectTransform.anchoredPosition = resetPos;
    }
    
    private IEnumerator ScrollCreditsCoroutine()
    {
        yield return new WaitForSeconds(_startDelay);
        
        while (_isScrolling)
        {
            if (!_isPaused)
            {
                Vector3 currentPos = _rectTransform.anchoredPosition;
                currentPos.y += _currentSpeed * Time.deltaTime;
                _rectTransform.anchoredPosition = currentPos;
                
                if (currentPos.y >= _endYPosition)
                {
                    _onCreditsComplete.Invoke();
                    
                    if (_loopCredits)
                    {
                        ResetPosition();
                        yield return new WaitForSeconds(_startDelay);
                    }
                    else
                    {
                        StopCredits();
                        break;
                    }
                }
            }
            
            yield return null;
        }
    }
    
    private void OnValidate()
    {
        if (_scrollSpeed < 0f)
            _scrollSpeed = 0f;
            
        if (_mouseScrollSensitivity < 0f)
            _mouseScrollSensitivity = 0f;
            
        if (_startDelay < 0f)
            _startDelay = 0f;
    }
    
    private void OnDisable()
    {
        StopCredits();
    }
}