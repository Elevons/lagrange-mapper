// Prompt: splash screen display
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SplashScreen : MonoBehaviour
{
    [Header("Splash Screen Settings")]
    [SerializeField] private float _displayDuration = 3f;
    [SerializeField] private float _fadeInDuration = 1f;
    [SerializeField] private float _fadeOutDuration = 1f;
    [SerializeField] private bool _skipOnInput = true;
    [SerializeField] private string _nextSceneName = "MainMenu";
    
    [Header("UI Components")]
    [SerializeField] private Image _splashImage;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private Text _skipText;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Multiple Splash Screens")]
    [SerializeField] private Sprite[] _splashSprites;
    [SerializeField] private float _timeBetweenSplashes = 0.5f;
    
    private bool _isTransitioning = false;
    private int _currentSplashIndex = 0;
    private Coroutine _splashCoroutine;
    
    private void Start()
    {
        InitializeSplashScreen();
        _splashCoroutine = StartCoroutine(SplashSequence());
    }
    
    private void Update()
    {
        if (_skipOnInput && !_isTransitioning && Input.anyKeyDown)
        {
            SkipSplashScreen();
        }
    }
    
    private void InitializeSplashScreen()
    {
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
        
        if (_canvasGroup != null)
            _canvasGroup.alpha = 0f;
        
        if (_skipText != null)
            _skipText.gameObject.SetActive(_skipOnInput);
        
        if (_splashImage != null && _splashSprites != null && _splashSprites.Length > 0)
            _splashImage.sprite = _splashSprites[0];
    }
    
    private IEnumerator SplashSequence()
    {
        if (_splashSprites != null && _splashSprites.Length > 1)
        {
            yield return StartCoroutine(MultipleSplashSequence());
        }
        else
        {
            yield return StartCoroutine(SingleSplashSequence());
        }
        
        LoadNextScene();
    }
    
    private IEnumerator SingleSplashSequence()
    {
        // Fade in
        yield return StartCoroutine(FadeCanvasGroup(0f, 1f, _fadeInDuration));
        
        // Display
        yield return new WaitForSeconds(_displayDuration);
        
        // Fade out
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, _fadeOutDuration));
    }
    
    private IEnumerator MultipleSplashSequence()
    {
        for (_currentSplashIndex = 0; _currentSplashIndex < _splashSprites.Length; _currentSplashIndex++)
        {
            if (_splashImage != null)
                _splashImage.sprite = _splashSprites[_currentSplashIndex];
            
            // Fade in
            yield return StartCoroutine(FadeCanvasGroup(0f, 1f, _fadeInDuration));
            
            // Display
            yield return new WaitForSeconds(_displayDuration);
            
            // Fade out (except for last splash)
            if (_currentSplashIndex < _splashSprites.Length - 1)
            {
                yield return StartCoroutine(FadeCanvasGroup(1f, 0f, _fadeOutDuration));
                yield return new WaitForSeconds(_timeBetweenSplashes);
            }
        }
        
        // Final fade out
        yield return StartCoroutine(FadeCanvasGroup(1f, 0f, _fadeOutDuration));
    }
    
    private IEnumerator FadeCanvasGroup(float startAlpha, float endAlpha, float duration)
    {
        if (_canvasGroup == null)
            yield break;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / duration;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, normalizedTime);
            yield return null;
        }
        
        _canvasGroup.alpha = endAlpha;
    }
    
    private void SkipSplashScreen()
    {
        if (_splashCoroutine != null)
        {
            StopCoroutine(_splashCoroutine);
            _splashCoroutine = null;
        }
        
        _isTransitioning = true;
        StartCoroutine(QuickFadeOut());
    }
    
    private IEnumerator QuickFadeOut()
    {
        yield return StartCoroutine(FadeCanvasGroup(_canvasGroup != null ? _canvasGroup.alpha : 1f, 0f, 0.3f));
        LoadNextScene();
    }
    
    private void LoadNextScene()
    {
        if (_isTransitioning)
            return;
        
        _isTransitioning = true;
        
        if (string.IsNullOrEmpty(_nextSceneName))
        {
            int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
            int nextSceneIndex = currentSceneIndex + 1;
            
            if (nextSceneIndex < SceneManager.sceneCountInBuildSettings)
            {
                SceneManager.LoadScene(nextSceneIndex);
            }
            else
            {
                Debug.LogWarning("No next scene available. Staying on current scene.");
            }
        }
        else
        {
            SceneManager.LoadScene(_nextSceneName);
        }
    }
    
    private void PlayAudio()
    {
        if (_audioSource != null && _audioSource.clip != null)
        {
            _audioSource.Play();
        }
    }
    
    private void OnValidate()
    {
        _displayDuration = Mathf.Max(0f, _displayDuration);
        _fadeInDuration = Mathf.Max(0f, _fadeInDuration);
        _fadeOutDuration = Mathf.Max(0f, _fadeOutDuration);
        _timeBetweenSplashes = Mathf.Max(0f, _timeBetweenSplashes);
    }
}