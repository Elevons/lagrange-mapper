// Prompt: main menu navigation
// Type: combat

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Audio;
using System.Collections;

public class MainMenuNavigation : MonoBehaviour
{
    [Header("Menu Panels")]
    [SerializeField] private GameObject _mainMenuPanel;
    [SerializeField] private GameObject _settingsPanel;
    [SerializeField] private GameObject _creditsPanel;
    [SerializeField] private GameObject _loadingPanel;
    
    [Header("Main Menu Buttons")]
    [SerializeField] private Button _playButton;
    [SerializeField] private Button _settingsButton;
    [SerializeField] private Button _creditsButton;
    [SerializeField] private Button _quitButton;
    
    [Header("Settings UI")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Dropdown _resolutionDropdown;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Button _settingsBackButton;
    
    [Header("Credits UI")]
    [SerializeField] private Button _creditsBackButton;
    [SerializeField] private ScrollRect _creditsScrollRect;
    
    [Header("Loading UI")]
    [SerializeField] private Slider _loadingProgressBar;
    [SerializeField] private Text _loadingText;
    
    [Header("Audio")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private AudioSource _buttonClickSound;
    [SerializeField] private AudioSource _backgroundMusic;
    
    [Header("Scene Management")]
    [SerializeField] private string _gameSceneName = "GameScene";
    [SerializeField] private float _loadingDelay = 1f;
    
    [Header("Animation")]
    [SerializeField] private float _panelTransitionSpeed = 0.3f;
    [SerializeField] private AnimationCurve _transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private Resolution[] _availableResolutions;
    private bool _isTransitioning = false;
    
    private void Start()
    {
        InitializeMenu();
        SetupButtonListeners();
        LoadSettings();
        SetupResolutions();
    }
    
    private void InitializeMenu()
    {
        ShowPanel(_mainMenuPanel);
        HidePanel(_settingsPanel);
        HidePanel(_creditsPanel);
        HidePanel(_loadingPanel);
        
        if (_backgroundMusic != null && !_backgroundMusic.isPlaying)
        {
            _backgroundMusic.Play();
        }
    }
    
    private void SetupButtonListeners()
    {
        if (_playButton != null)
            _playButton.onClick.AddListener(OnPlayButtonClicked);
        
        if (_settingsButton != null)
            _settingsButton.onClick.AddListener(OnSettingsButtonClicked);
        
        if (_creditsButton != null)
            _creditsButton.onClick.AddListener(OnCreditsButtonClicked);
        
        if (_quitButton != null)
            _quitButton.onClick.AddListener(OnQuitButtonClicked);
        
        if (_settingsBackButton != null)
            _settingsBackButton.onClick.AddListener(OnSettingsBackButtonClicked);
        
        if (_creditsBackButton != null)
            _creditsBackButton.onClick.AddListener(OnCreditsBackButtonClicked);
        
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        
        if (_resolutionDropdown != null)
            _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
        
        if (_fullscreenToggle != null)
            _fullscreenToggle.onValueChanged.AddListener(OnFullscreenToggled);
    }
    
    private void SetupResolutions()
    {
        if (_resolutionDropdown == null) return;
        
        _availableResolutions = Screen.resolutions;
        _resolutionDropdown.ClearOptions();
        
        var options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;
        
        for (int i = 0; i < _availableResolutions.Length; i++)
        {
            string option = _availableResolutions[i].width + " x " + _availableResolutions[i].height;
            options.Add(option);
            
            if (_availableResolutions[i].width == Screen.currentResolution.width &&
                _availableResolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }
        
        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.value = currentResolutionIndex;
        _resolutionDropdown.RefreshShownValue();
    }
    
    private void LoadSettings()
    {
        if (_masterVolumeSlider != null)
        {
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
            _masterVolumeSlider.value = masterVolume;
            OnMasterVolumeChanged(masterVolume);
        }
        
        if (_musicVolumeSlider != null)
        {
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
            _musicVolumeSlider.value = musicVolume;
            OnMusicVolumeChanged(musicVolume);
        }
        
        if (_sfxVolumeSlider != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
            _sfxVolumeSlider.value = sfxVolume;
            OnSFXVolumeChanged(sfxVolume);
        }
        
        if (_fullscreenToggle != null)
        {
            bool isFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            _fullscreenToggle.isOn = isFullscreen;
        }
    }
    
    private void SaveSettings()
    {
        if (_masterVolumeSlider != null)
            PlayerPrefs.SetFloat("MasterVolume", _masterVolumeSlider.value);
        
        if (_musicVolumeSlider != null)
            PlayerPrefs.SetFloat("MusicVolume", _musicVolumeSlider.value);
        
        if (_sfxVolumeSlider != null)
            PlayerPrefs.SetFloat("SFXVolume", _sfxVolumeSlider.value);
        
        if (_fullscreenToggle != null)
            PlayerPrefs.SetInt("Fullscreen", _fullscreenToggle.isOn ? 1 : 0);
        
        PlayerPrefs.Save();
    }
    
    public void OnPlayButtonClicked()
    {
        if (_isTransitioning) return;
        
        PlayButtonSound();
        StartCoroutine(LoadGameScene());
    }
    
    public void OnSettingsButtonClicked()
    {
        if (_isTransitioning) return;
        
        PlayButtonSound();
        StartCoroutine(TransitionToPanel(_settingsPanel));
    }
    
    public void OnCreditsButtonClicked()
    {
        if (_isTransitioning) return;
        
        PlayButtonSound();
        StartCoroutine(TransitionToPanel(_creditsPanel));
    }
    
    public void OnQuitButtonClicked()
    {
        PlayButtonSound();
        
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
    
    public void OnSettingsBackButtonClicked()
    {
        if (_isTransitioning) return;
        
        PlayButtonSound();
        SaveSettings();
        StartCoroutine(TransitionToPanel(_mainMenuPanel));
    }
    
    public void OnCreditsBackButtonClicked()
    {
        if (_isTransitioning) return;
        
        PlayButtonSound();
        StartCoroutine(TransitionToPanel(_mainMenuPanel));
    }
    
    public void OnMasterVolumeChanged(float value)
    {
        if (_audioMixer != null)
        {
            float dbValue = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
            _audioMixer.SetFloat("MasterVolume", dbValue);
        }
    }
    
    public void OnMusicVolumeChanged(float value)
    {
        if (_audioMixer != null)
        {
            float dbValue = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
            _audioMixer.SetFloat("MusicVolume", dbValue);
        }
    }
    
    public void OnSFXVolumeChanged(float value)
    {
        if (_audioMixer != null)
        {
            float dbValue = Mathf.Log10(Mathf.Clamp(value, 0.0001f, 1f)) * 20f;
            _audioMixer.SetFloat("SFXVolume", dbValue);
        }
    }
    
    public void OnResolutionChanged(int resolutionIndex)
    {
        if (_availableResolutions != null && resolutionIndex < _availableResolutions.Length)
        {
            Resolution resolution = _availableResolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        }
    }
    
    public void OnFullscreenToggled(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
    
    private void PlayButtonSound()
    {
        if (_buttonClickSound != null)
        {
            _buttonClickSound.Play();
        }
    }
    
    private void ShowPanel(GameObject panel)
    {
        if (panel != null)
        {
            panel.SetActive(true);
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
    }
    
    private void HidePanel(GameObject panel)
    {
        if (panel != null)
        {
            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            panel.SetActive(false);
        }
    }
    
    private IEnumerator TransitionToPanel(GameObject targetPanel)
    {
        _isTransitioning = true;
        
        GameObject currentPanel = GetCurrentActivePanel();
        
        if (currentPanel != null)
        {
            yield return StartCoroutine(FadeOutPanel(currentPanel));
        }
        
        if (targetPanel != null)
        {
            yield return StartCoroutine(FadeInPanel(targetPanel));
        }
        
        _isTransitioning = false;
    }
    
    private GameObject GetCurrentActivePanel()
    {
        if (_mainMenuPanel != null && _mainMenuPanel.activeInHierarchy) return _mainMenuPanel;
        if (_settingsPanel != null && _settingsPanel.activeInHierarchy) return _settingsPanel;
        if (_creditsPanel != null && _creditsPanel.activeInHierarchy) return _creditsPanel;
        return null;
    }
    
    private IEnumerator FadeOutPanel(GameObject panel)
    {
        var canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.interactable = false;
        
        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;
        
        while (elapsedTime < _panelTransitionSpeed)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _panelTransitionSpeed;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, _transitionCurve.Evaluate(progress));
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        panel.SetActive(false);
    }
    
    private IEnumerator FadeInPanel(GameObject panel)
    {
        panel.SetActive(true);
        
        var canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.AddComponent<CanvasGroup>();
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = true;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _panelTransitionSpeed)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _panelTransitionSpeed;
            canvasGroup.alpha = _transitionCurve.Evaluate(progress);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
    }
    
    private IEnumerator LoadGameScene()
    {
        ShowPanel(_loadingPanel);
        HidePanel(_mainMenuPanel);
        
        yield return new WaitForSeconds(_loadingDelay);
        
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(_gameSceneName);
        asyncLoad.allowSceneActivation = false;
        
        while (!asyncLoad.isDone)
        {
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            
            if (_loadingProgressBar != null)
            {
                _loadingProgressBar.value = progress;
            }
            
            if (_loadingText != null)
            {
                _loadingText.text = "Loading... " + Mathf.Round(progress * 100f) + "%";
            }
            
            if (asyncLoad.progress >= 0.9f)
            {
                if (_loadingText != null)
                {
                    _loadingText.text = "Press any key to continue...";
                }
                
                if (Input.anyKeyDown)
                {
                    asyncLoad.allowSceneActivation = true;
                }
            }
            
            yield return null;
        }
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            HandleEscapeKey();
        }
    }
    
    private void HandleEscapeKey()
    {
        if (_isTransitioning) return;
        
        if (_settingsPanel != null && _settingsPanel.activeInHier