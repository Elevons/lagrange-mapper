// Prompt: loading screen with progress
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Slider _progressBar;
    [SerializeField] private Text _progressText;
    [SerializeField] private Text _loadingStatusText;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private GameObject _loadingPanel;
    
    [Header("Loading Settings")]
    [SerializeField] private string _sceneToLoad = "MainScene";
    [SerializeField] private float _minimumLoadTime = 2f;
    [SerializeField] private float _progressUpdateSpeed = 2f;
    [SerializeField] private bool _allowSceneActivation = true;
    
    [Header("Visual Effects")]
    [SerializeField] private AnimationCurve _progressCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private Color _progressBarColor = Color.green;
    [SerializeField] private Color _backgroundColor = Color.black;
    
    [Header("Loading Messages")]
    [SerializeField] private string[] _loadingMessages = {
        "Loading assets...",
        "Preparing scene...",
        "Initializing systems...",
        "Almost ready..."
    };
    
    private AsyncOperation _loadOperation;
    private float _targetProgress;
    private float _currentProgress;
    private float _loadStartTime;
    private bool _isLoading;
    private int _currentMessageIndex;
    private Coroutine _messageUpdateCoroutine;
    
    private void Start()
    {
        InitializeLoadingScreen();
        StartLoading();
    }
    
    private void Update()
    {
        if (_isLoading)
        {
            UpdateProgress();
            UpdateLoadingStatus();
        }
    }
    
    private void InitializeLoadingScreen()
    {
        if (_loadingPanel != null)
            _loadingPanel.SetActive(true);
            
        if (_backgroundImage != null)
            _backgroundImage.color = _backgroundColor;
            
        if (_progressBar != null)
        {
            _progressBar.value = 0f;
            if (_progressBar.fillRect != null)
                _progressBar.fillRect.GetComponent<Image>().color = _progressBarColor;
        }
        
        if (_progressText != null)
            _progressText.text = "0%";
            
        if (_loadingStatusText != null && _loadingMessages.Length > 0)
            _loadingStatusText.text = _loadingMessages[0];
            
        _currentProgress = 0f;
        _targetProgress = 0f;
        _loadStartTime = Time.time;
        _currentMessageIndex = 0;
    }
    
    public void StartLoading()
    {
        if (_isLoading) return;
        
        _isLoading = true;
        _loadStartTime = Time.time;
        
        StartCoroutine(LoadSceneAsync());
        
        if (_loadingMessages.Length > 1)
            _messageUpdateCoroutine = StartCoroutine(UpdateLoadingMessages());
    }
    
    public void LoadScene(string sceneName)
    {
        _sceneToLoad = sceneName;
        StartLoading();
    }
    
    private IEnumerator LoadSceneAsync()
    {
        yield return new WaitForEndOfFrame();
        
        _loadOperation = SceneManager.LoadSceneAsync(_sceneToLoad);
        _loadOperation.allowSceneActivation = false;
        
        while (!_loadOperation.isDone)
        {
            float progress = Mathf.Clamp01(_loadOperation.progress / 0.9f);
            _targetProgress = progress;
            
            if (_loadOperation.progress >= 0.9f)
            {
                _targetProgress = 1f;
                
                float elapsedTime = Time.time - _loadStartTime;
                if (elapsedTime >= _minimumLoadTime && _allowSceneActivation)
                {
                    yield return new WaitForSeconds(0.5f);
                    _loadOperation.allowSceneActivation = true;
                }
            }
            
            yield return null;
        }
    }
    
    private void UpdateProgress()
    {
        if (_currentProgress < _targetProgress)
        {
            _currentProgress = Mathf.MoveTowards(_currentProgress, _targetProgress, 
                Time.deltaTime * _progressUpdateSpeed);
        }
        
        float curvedProgress = _progressCurve.Evaluate(_currentProgress);
        
        if (_progressBar != null)
            _progressBar.value = curvedProgress;
            
        if (_progressText != null)
        {
            int percentage = Mathf.RoundToInt(curvedProgress * 100f);
            _progressText.text = percentage + "%";
        }
    }
    
    private void UpdateLoadingStatus()
    {
        if (_loadingStatusText == null || _loadingMessages.Length == 0) return;
        
        int expectedMessageIndex = Mathf.FloorToInt(_currentProgress * (_loadingMessages.Length - 1));
        expectedMessageIndex = Mathf.Clamp(expectedMessageIndex, 0, _loadingMessages.Length - 1);
        
        if (expectedMessageIndex != _currentMessageIndex)
        {
            _currentMessageIndex = expectedMessageIndex;
            _loadingStatusText.text = _loadingMessages[_currentMessageIndex];
        }
    }
    
    private IEnumerator UpdateLoadingMessages()
    {
        while (_isLoading && _currentProgress < 1f)
        {
            yield return new WaitForSeconds(Random.Range(1f, 3f));
            
            if (_loadingStatusText != null && _loadingMessages.Length > 0)
            {
                int nextIndex = (_currentMessageIndex + 1) % _loadingMessages.Length;
                if (_currentProgress >= (float)nextIndex / _loadingMessages.Length)
                {
                    _currentMessageIndex = nextIndex;
                    _loadingStatusText.text = _loadingMessages[_currentMessageIndex];
                }
            }
        }
    }
    
    public void SetMinimumLoadTime(float time)
    {
        _minimumLoadTime = Mathf.Max(0f, time);
    }
    
    public void SetProgressUpdateSpeed(float speed)
    {
        _progressUpdateSpeed = Mathf.Max(0.1f, speed);
    }
    
    public void SetAllowSceneActivation(bool allow)
    {
        _allowSceneActivation = allow;
        if (_loadOperation != null && allow && _loadOperation.progress >= 0.9f)
        {
            _loadOperation.allowSceneActivation = true;
        }
    }
    
    private void OnDestroy()
    {
        if (_messageUpdateCoroutine != null)
        {
            StopCoroutine(_messageUpdateCoroutine);
        }
    }
    
    private void OnValidate()
    {
        _minimumLoadTime = Mathf.Max(0f, _minimumLoadTime);
        _progressUpdateSpeed = Mathf.Max(0.1f, _progressUpdateSpeed);
        
        if (_loadingMessages == null || _loadingMessages.Length == 0)
        {
            _loadingMessages = new string[] { "Loading..." };
        }
    }
}