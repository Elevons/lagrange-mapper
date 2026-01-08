// Prompt: pause menu with options
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject _pauseMenuPanel;
    [SerializeField] private GameObject _optionsPanel;
    [SerializeField] private Button _resumeButton;
    [SerializeField] private Button _optionsButton;
    [SerializeField] private Button _mainMenuButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _backButton;
    
    [Header("Audio Options")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    [SerializeField] private Toggle _muteToggle;
    
    [Header("Graphics Options")]
    [SerializeField] private Dropdown _qualityDropdown;
    [SerializeField] private Dropdown _resolutionDropdown;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Slider _brightnessSlider;
    
    [Header("Gameplay Options")]
    [SerializeField] private Slider _mouseSensitivitySlider;
    [SerializeField] private Toggle _invertYToggle;
    [SerializeField] private Dropdown _difficultyDropdown;
    
    [Header("Settings")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";
    [SerializeField] private KeyCode _pauseKey = KeyCode.Escape;
    
    private bool _isPaused = false;
    private Resolution[] _resolutions;
    private float _originalTimeScale;
    
    private void Start()
    {
        _originalTimeScale = Time.timeScale;
        InitializeUI();
        SetupResolutions();
        LoadSettings();
        
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(false);
        if (_optionsPanel != null)
            _optionsPanel.SetActive(false);
    }
    
    private void Update()
    {
        if (Input.GetKeyDown(_pauseKey))
        {
            if (_isPaused)
                ResumeGame();
            else
                PauseGame();
        }
    }
    
    private void InitializeUI()
    {
        if (_resumeButton != null)
            _resumeButton.onClick.AddListener(ResumeGame);
        if (_optionsButton != null)
            _optionsButton.onClick.AddListener(OpenOptions);
        if (_mainMenuButton != null)
            _mainMenuButton.onClick.AddListener(LoadMainMenu);
        if (_quitButton != null)
            _quitButton.onClick.AddListener(QuitGame);
        if (_backButton != null)
            _backButton.onClick.AddListener(CloseOptions);
        
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        if (_muteToggle != null)
            _muteToggle.onValueChanged.AddListener(SetMute);
        
        if (_qualityDropdown != null)
        {
            _qualityDropdown.ClearOptions();
            _qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(QualitySettings.names));
            _qualityDropdown.value = QualitySettings.GetQualityLevel();
            _qualityDropdown.onValueChanged.AddListener(SetQuality);
        }
        
        if (_fullscreenToggle != null)
        {
            _fullscreenToggle.isOn = Screen.fullScreen;
            _fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        }
        
        if (_brightnessSlider != null)
            _brightnessSlider.onValueChanged.AddListener(SetBrightness);
        
        if (_mouseSensitivitySlider != null)
            _mouseSensitivitySlider.onValueChanged.AddListener(SetMouseSensitivity);
        if (_invertYToggle != null)
            _invertYToggle.onValueChanged.AddListener(SetInvertY);
        
        if (_difficultyDropdown != null)
        {
            _difficultyDropdown.ClearOptions();
            _difficultyDropdown.AddOptions(new System.Collections.Generic.List<string> { "Easy", "Normal", "Hard" });
            _difficultyDropdown.onValueChanged.AddListener(SetDifficulty);
        }
        
        if (_resolutionDropdown != null)
            _resolutionDropdown.onValueChanged.AddListener(SetResolution);
    }
    
    private void SetupResolutions()
    {
        if (_resolutionDropdown == null) return;
        
        _resolutions = Screen.resolutions;
        _resolutionDropdown.ClearOptions();
        
        System.Collections.Generic.List<string> options = new System.Collections.Generic.List<string>();
        int currentResolutionIndex = 0;
        
        for (int i = 0; i < _resolutions.Length; i++)
        {
            string option = _resolutions[i].width + " x " + _resolutions[i].height;
            options.Add(option);
            
            if (_resolutions[i].width == Screen.currentResolution.width &&
                _resolutions[i].height == Screen.currentResolution.height)
            {
                currentResolutionIndex = i;
            }
        }
        
        _resolutionDropdown.AddOptions(options);
        _resolutionDropdown.value = currentResolutionIndex;
        _resolutionDropdown.RefreshShownValue();
    }
    
    public void PauseGame()
    {
        _isPaused = true;
        Time.timeScale = 0f;
        
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(true);
        
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
    
    public void ResumeGame()
    {
        _isPaused = false;
        Time.timeScale = _originalTimeScale;
        
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(false);
        if (_optionsPanel != null)
            _optionsPanel.SetActive(false);
        
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    public void OpenOptions()
    {
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(false);
        if (_optionsPanel != null)
            _optionsPanel.SetActive(true);
    }
    
    public void CloseOptions()
    {
        if (_optionsPanel != null)
            _optionsPanel.SetActive(false);
        if (_pauseMenuPanel != null)
            _pauseMenuPanel.SetActive(true);
    }
    
    public void LoadMainMenu()
    {
        Time.timeScale = _originalTimeScale;
        SceneManager.LoadScene(_mainMenuSceneName);
    }
    
    public void QuitGame()
    {
        SaveSettings();
        Application.Quit();
    }
    
    public void SetMasterVolume(float volume)
    {
        if (_audioMixer != null)
            _audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20);
    }
    
    public void SetMusicVolume(float volume)
    {
        if (_audioMixer != null)
            _audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
    }
    
    public void SetSFXVolume(float volume)
    {
        if (_audioMixer != null)
            _audioMixer.SetFloat("SFXVolume", Mathf.Log10(volume) * 20);
    }
    
    public void SetMute(bool isMuted)
    {
        if (_audioMixer != null)
        {
            if (isMuted)
                _audioMixer.SetFloat("MasterVolume", -80f);
            else if (_masterVolumeSlider != null)
                SetMasterVolume(_masterVolumeSlider.value);
        }
    }
    
    public void SetQuality(int qualityIndex)
    {
        QualitySettings.SetQualityLevel(qualityIndex);
    }
    
    public void SetFullscreen(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
    }
    
    public void SetResolution(int resolutionIndex)
    {
        if (_resolutions != null && resolutionIndex < _resolutions.Length)
        {
            Resolution resolution = _resolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);
        }
    }
    
    public void SetBrightness(float brightness)
    {
        RenderSettings.ambientIntensity = brightness;
    }
    
    public void SetMouseSensitivity(float sensitivity)
    {
        PlayerPrefs.SetFloat("MouseSensitivity", sensitivity);
    }
    
    public void SetInvertY(bool invert)
    {
        PlayerPrefs.SetInt("InvertY", invert ? 1 : 0);
    }
    
    public void SetDifficulty(int difficulty)
    {
        PlayerPrefs.SetInt("Difficulty", difficulty);
    }
    
    private void LoadSettings()
    {
        if (_masterVolumeSlider != null)
        {
            float masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.75f);
            _masterVolumeSlider.value = masterVolume;
            SetMasterVolume(masterVolume);
        }
        
        if (_musicVolumeSlider != null)
        {
            float musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.75f);
            _musicVolumeSlider.value = musicVolume;
            SetMusicVolume(musicVolume);
        }
        
        if (_sfxVolumeSlider != null)
        {
            float sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.75f);
            _sfxVolumeSlider.value = sfxVolume;
            SetSFXVolume(sfxVolume);
        }
        
        if (_muteToggle != null)
        {
            bool isMuted = PlayerPrefs.GetInt("Mute", 0) == 1;
            _muteToggle.isOn = isMuted;
            SetMute(isMuted);
        }
        
        if (_brightnessSlider != null)
        {
            float brightness = PlayerPrefs.GetFloat("Brightness", 1f);
            _brightnessSlider.value = brightness;
            SetBrightness(brightness);
        }
        
        if (_mouseSensitivitySlider != null)
        {
            float sensitivity = PlayerPrefs.GetFloat("MouseSensitivity", 2f);
            _mouseSensitivitySlider.value = sensitivity;
        }
        
        if (_invertYToggle != null)
        {
            bool invertY = PlayerPrefs.GetInt("InvertY", 0) == 1;
            _invertYToggle.isOn = invertY;
        }
        
        if (_difficultyDropdown != null)
        {
            int difficulty = PlayerPrefs.GetInt("Difficulty", 1);
            _difficultyDropdown.value = difficulty;
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
        if (_muteToggle != null)
            PlayerPrefs.SetInt("Mute", _muteToggle.isOn ? 1 : 0);
        if (_brightnessSlider != null)
            PlayerPrefs.SetFloat("Brightness", _brightnessSlider.value);
        if (_mouseSensitivitySlider != null)
            PlayerPrefs.SetFloat("MouseSensitivity", _mouseSensitivitySlider.value);
        if (_invertYToggle != null)
            PlayerPrefs.SetInt("InvertY", _invertYToggle.isOn ? 1 : 0);
        if (_difficultyDropdown != null)
            PlayerPrefs.SetInt("Difficulty", _difficultyDropdown.value);
        
        PlayerPrefs.Save();
    }
    
    private void OnDestroy()
    {
        SaveSettings();
    }
}