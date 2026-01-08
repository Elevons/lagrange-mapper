// Prompt: settings preferences save
// Type: general

using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Events;
using System.Collections.Generic;

public class SettingsManager : MonoBehaviour
{
    [System.Serializable]
    public class SettingsData
    {
        public float masterVolume = 1f;
        public float musicVolume = 1f;
        public float sfxVolume = 1f;
        public int qualityLevel = 3;
        public bool fullscreen = true;
        public int resolutionIndex = 0;
        public float mouseSensitivity = 1f;
        public bool invertMouseY = false;
        public int vsyncCount = 1;
        public float brightness = 1f;
        public bool showFPS = false;
        public string language = "English";
        public KeyCode jumpKey = KeyCode.Space;
        public KeyCode interactKey = KeyCode.E;
        public KeyCode runKey = KeyCode.LeftShift;
    }

    [Header("Audio Settings")]
    [SerializeField] private AudioMixer _audioMixer;
    [SerializeField] private string _masterVolumeParameter = "MasterVolume";
    [SerializeField] private string _musicVolumeParameter = "MusicVolume";
    [SerializeField] private string _sfxVolumeParameter = "SFXVolume";

    [Header("Graphics Settings")]
    [SerializeField] private Camera _mainCamera;
    [SerializeField] private Light _mainLight;

    [Header("Events")]
    public UnityEvent<SettingsData> OnSettingsLoaded;
    public UnityEvent<SettingsData> OnSettingsSaved;
    public UnityEvent<float> OnVolumeChanged;
    public UnityEvent<int> OnQualityChanged;
    public UnityEvent<bool> OnFullscreenChanged;

    private SettingsData _currentSettings;
    private Resolution[] _availableResolutions;
    private const string SETTINGS_KEY = "GameSettings";

    public SettingsData CurrentSettings => _currentSettings;

    private void Awake()
    {
        _currentSettings = new SettingsData();
        _availableResolutions = Screen.resolutions;
    }

    private void Start()
    {
        LoadSettings();
        ApplyAllSettings();
    }

    public void SaveSettings()
    {
        string jsonData = JsonUtility.ToJson(_currentSettings, true);
        PlayerPrefs.SetString(SETTINGS_KEY, jsonData);
        PlayerPrefs.Save();
        
        OnSettingsSaved?.Invoke(_currentSettings);
        Debug.Log("Settings saved successfully");
    }

    public void LoadSettings()
    {
        if (PlayerPrefs.HasKey(SETTINGS_KEY))
        {
            string jsonData = PlayerPrefs.GetString(SETTINGS_KEY);
            try
            {
                _currentSettings = JsonUtility.FromJson<SettingsData>(jsonData);
                OnSettingsLoaded?.Invoke(_currentSettings);
                Debug.Log("Settings loaded successfully");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Failed to load settings: {e.Message}. Using defaults.");
                ResetToDefaults();
            }
        }
        else
        {
            Debug.Log("No saved settings found. Using defaults.");
            ResetToDefaults();
        }
    }

    public void ResetToDefaults()
    {
        _currentSettings = new SettingsData();
        ApplyAllSettings();
        SaveSettings();
    }

    private void ApplyAllSettings()
    {
        SetMasterVolume(_currentSettings.masterVolume);
        SetMusicVolume(_currentSettings.musicVolume);
        SetSFXVolume(_currentSettings.sfxVolume);
        SetQualityLevel(_currentSettings.qualityLevel);
        SetFullscreen(_currentSettings.fullscreen);
        SetResolution(_currentSettings.resolutionIndex);
        SetVSyncCount(_currentSettings.vsyncCount);
        SetBrightness(_currentSettings.brightness);
    }

    public void SetMasterVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        _currentSettings.masterVolume = volume;
        
        if (_audioMixer != null)
        {
            float dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
            _audioMixer.SetFloat(_masterVolumeParameter, dbValue);
        }
        
        OnVolumeChanged?.Invoke(volume);
    }

    public void SetMusicVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        _currentSettings.musicVolume = volume;
        
        if (_audioMixer != null)
        {
            float dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
            _audioMixer.SetFloat(_musicVolumeParameter, dbValue);
        }
    }

    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        _currentSettings.sfxVolume = volume;
        
        if (_audioMixer != null)
        {
            float dbValue = volume > 0 ? Mathf.Log10(volume) * 20 : -80f;
            _audioMixer.SetFloat(_sfxVolumeParameter, dbValue);
        }
    }

    public void SetQualityLevel(int qualityIndex)
    {
        qualityIndex = Mathf.Clamp(qualityIndex, 0, QualitySettings.names.Length - 1);
        _currentSettings.qualityLevel = qualityIndex;
        QualitySettings.SetQualityLevel(qualityIndex);
        OnQualityChanged?.Invoke(qualityIndex);
    }

    public void SetFullscreen(bool isFullscreen)
    {
        _currentSettings.fullscreen = isFullscreen;
        Screen.fullScreen = isFullscreen;
        OnFullscreenChanged?.Invoke(isFullscreen);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (_availableResolutions != null && resolutionIndex >= 0 && resolutionIndex < _availableResolutions.Length)
        {
            _currentSettings.resolutionIndex = resolutionIndex;
            Resolution resolution = _availableResolutions[resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, _currentSettings.fullscreen);
        }
    }

    public void SetMouseSensitivity(float sensitivity)
    {
        _currentSettings.mouseSensitivity = Mathf.Clamp(sensitivity, 0.1f, 5f);
    }

    public void SetInvertMouseY(bool invert)
    {
        _currentSettings.invertMouseY = invert;
    }

    public void SetVSyncCount(int vSyncCount)
    {
        vSyncCount = Mathf.Clamp(vSyncCount, 0, 4);
        _currentSettings.vsyncCount = vSyncCount;
        QualitySettings.vSyncCount = vSyncCount;
    }

    public void SetBrightness(float brightness)
    {
        brightness = Mathf.Clamp(brightness, 0.1f, 2f);
        _currentSettings.brightness = brightness;
        
        if (_mainLight != null)
        {
            _mainLight.intensity = brightness;
        }
        
        if (_mainCamera != null)
        {
            _mainCamera.backgroundColor = Color.black * brightness;
        }
    }

    public void SetShowFPS(bool showFPS)
    {
        _currentSettings.showFPS = showFPS;
    }

    public void SetLanguage(string language)
    {
        _currentSettings.language = language;
    }

    public void SetJumpKey(KeyCode keyCode)
    {
        _currentSettings.jumpKey = keyCode;
    }

    public void SetInteractKey(KeyCode keyCode)
    {
        _currentSettings.interactKey = keyCode;
    }

    public void SetRunKey(KeyCode keyCode)
    {
        _currentSettings.runKey = keyCode;
    }

    public Resolution[] GetAvailableResolutions()
    {
        return _availableResolutions;
    }

    public string[] GetQualityLevels()
    {
        return QualitySettings.names;
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveSettings();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveSettings();
        }
    }

    private void OnDestroy()
    {
        SaveSettings();
    }
}