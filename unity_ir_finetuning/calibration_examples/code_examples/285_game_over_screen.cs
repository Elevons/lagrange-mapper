// Prompt: game over screen
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverScreen : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject _gameOverPanel;
    [SerializeField] private Text _gameOverText;
    [SerializeField] private Text _finalScoreText;
    [SerializeField] private Text _highScoreText;
    [SerializeField] private Button _restartButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _quitButton;
    
    [Header("Animation Settings")]
    [SerializeField] private float _fadeInDuration = 1f;
    [SerializeField] private float _delayBeforeShow = 0.5f;
    [SerializeField] private AnimationCurve _fadeInCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _gameOverSound;
    [SerializeField] private AudioClip _buttonClickSound;
    
    [Header("Scene Management")]
    [SerializeField] private string _gameSceneName = "GameScene";
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    
    private CanvasGroup _canvasGroup;
    private int _currentScore;
    private int _highScore;
    private bool _isShowing = false;
    
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetupButtons();
        HideGameOverScreen();
    }
    
    private void Start()
    {
        LoadHighScore();
    }
    
    private void SetupButtons()
    {
        if (_restartButton != null)
            _restartButton.onClick.AddListener(RestartGame);
            
        if (_mainMenuButton != null)
            _mainMenuButton.onClick.AddListener(GoToMainMenu);
            
        if (_quitButton != null)
            _quitButton.onClick.AddListener(QuitGame);
    }
    
    public void ShowGameOverScreen(int finalScore = 0)
    {
        if (_isShowing) return;
        
        _currentScore = finalScore;
        UpdateHighScore();
        StartCoroutine(ShowGameOverCoroutine());
    }
    
    private IEnumerator ShowGameOverCoroutine()
    {
        _isShowing = true;
        
        yield return new WaitForSeconds(_delayBeforeShow);
        
        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(true);
            
        PlayGameOverSound();
        UpdateScoreTexts();
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _fadeInDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            float normalizedTime = elapsedTime / _fadeInDuration;
            float curveValue = _fadeInCurve.Evaluate(normalizedTime);
            
            _canvasGroup.alpha = curveValue;
            
            yield return null;
        }
        
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
    }
    
    public void HideGameOverScreen()
    {
        _isShowing = false;
        
        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(false);
            
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
    }
    
    private void UpdateScoreTexts()
    {
        if (_finalScoreText != null)
            _finalScoreText.text = "Final Score: " + _currentScore.ToString();
            
        if (_highScoreText != null)
            _highScoreText.text = "High Score: " + _highScore.ToString();
            
        if (_gameOverText != null && _currentScore >= _highScore && _currentScore > 0)
            _gameOverText.text = "NEW HIGH SCORE!";
    }
    
    private void UpdateHighScore()
    {
        if (_currentScore > _highScore)
        {
            _highScore = _currentScore;
            SaveHighScore();
        }
    }
    
    private void LoadHighScore()
    {
        _highScore = PlayerPrefs.GetInt("HighScore", 0);
    }
    
    private void SaveHighScore()
    {
        PlayerPrefs.SetInt("HighScore", _highScore);
        PlayerPrefs.Save();
    }
    
    private void PlayGameOverSound()
    {
        if (_audioSource != null && _gameOverSound != null)
            _audioSource.PlayOneShot(_gameOverSound);
    }
    
    private void PlayButtonClickSound()
    {
        if (_audioSource != null && _buttonClickSound != null)
            _audioSource.PlayOneShot(_buttonClickSound);
    }
    
    public void RestartGame()
    {
        PlayButtonClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene(_gameSceneName);
    }
    
    public void GoToMainMenu()
    {
        PlayButtonClickSound();
        Time.timeScale = 1f;
        SceneManager.LoadScene(_mainMenuSceneName);
    }
    
    public void QuitGame()
    {
        PlayButtonClickSound();
        
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
    private void OnDestroy()
    {
        if (_restartButton != null)
            _restartButton.onClick.RemoveAllListeners();
            
        if (_mainMenuButton != null)
            _mainMenuButton.onClick.RemoveAllListeners();
            
        if (_quitButton != null)
            _quitButton.onClick.RemoveAllListeners();
    }
    
    public void SetScore(int score)
    {
        _currentScore = score;
    }
    
    public int GetHighScore()
    {
        return _highScore;
    }
    
    public bool IsShowing()
    {
        return _isShowing;
    }
}