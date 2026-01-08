// Prompt: graphics quality settings
// Type: general

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;

public class GraphicsQualitySettings : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Dropdown _qualityDropdown;
    [SerializeField] private Dropdown _resolutionDropdown;
    [SerializeField] private Toggle _fullscreenToggle;
    [SerializeField] private Toggle _vsyncToggle;
    [SerializeField] private Slider _shadowDistanceSlider;
    [SerializeField] private Text _shadowDistanceText;
    [SerializeField] private Dropdown _shadowQualityDropdown;
    [SerializeField] private Dropdown _textureQualityDropdown;
    [SerializeField] private Dropdown _antiAliasingDropdown;
    [SerializeField] private Slider _renderScaleSlider;
    [SerializeField] private Text _renderScaleText;
    [SerializeField] private Button _applyButton;
    [SerializeField] private Button _resetButton;

    [Header("Settings")]
    [SerializeField] private bool _autoApplyChanges = true;
    [SerializeField] private float _maxShadowDistance = 150f;

    private Resolution[] _availableResolutions;
    private int _currentResolutionIndex;
    private bool _isInitialized = false;

    private struct GraphicsSettings
    {
        public int qualityLevel;
        public int resolutionIndex;
        public bool fullscreen;
        public bool vsync;
        public float shadowDistance;
        public ShadowQuality shadowQuality;
        public int textureQuality;
        public int antiAliasing;
        public float renderScale;
    }

    private GraphicsSettings _currentSettings;
    private GraphicsSettings _originalSettings;

    private void Start()
    {
        InitializeResolutions();
        InitializeDropdowns();
        LoadCurrentSettings();
        SetupUICallbacks();
        _isInitialized = true;
    }

    private void InitializeResolutions()
    {
        _availableResolutions = Screen.resolutions;
        List<string> resolutionOptions = new List<string>();

        for (int i = 0; i < _availableResolutions.Length; i++)
        {
            string option = _availableResolutions[i].width + " x " + _availableResolutions[i].height + " @ " + _availableResolutions[i].refreshRate + "Hz";
            resolutionOptions.Add(option);

            if (_availableResolutions[i].width == Screen.currentResolution.width &&
                _availableResolutions[i].height == Screen.currentResolution.height)
            {
                _currentResolutionIndex = i;
            }
        }

        if (_resolutionDropdown != null)
        {
            _resolutionDropdown.ClearOptions();
            _resolutionDropdown.AddOptions(resolutionOptions);
            _resolutionDropdown.value = _currentResolutionIndex;
        }
    }

    private void InitializeDropdowns()
    {
        if (_qualityDropdown != null)
        {
            _qualityDropdown.ClearOptions();
            List<string> qualityOptions = new List<string>();
            string[] qualityNames = QualitySettings.names;
            for (int i = 0; i < qualityNames.Length; i++)
            {
                qualityOptions.Add(qualityNames[i]);
            }
            _qualityDropdown.AddOptions(qualityOptions);
        }

        if (_shadowQualityDropdown != null)
        {
            _shadowQualityDropdown.ClearOptions();
            _shadowQualityDropdown.AddOptions(new List<string> { "Disabled", "Hard Shadows", "Soft Shadows" });
        }

        if (_textureQualityDropdown != null)
        {
            _textureQualityDropdown.ClearOptions();
            _textureQualityDropdown.AddOptions(new List<string> { "Full Res", "Half Res", "Quarter Res", "Eighth Res" });
        }

        if (_antiAliasingDropdown != null)
        {
            _antiAliasingDropdown.ClearOptions();
            _antiAliasingDropdown.AddOptions(new List<string> { "Disabled", "2x Multi Sampling", "4x Multi Sampling", "8x Multi Sampling" });
        }
    }

    private void LoadCurrentSettings()
    {
        _currentSettings.qualityLevel = QualitySettings.GetQualityLevel();
        _currentSettings.resolutionIndex = _currentResolutionIndex;
        _currentSettings.fullscreen = Screen.fullScreen;
        _currentSettings.vsync = QualitySettings.vSyncCount > 0;
        _currentSettings.shadowDistance = QualitySettings.shadowDistance;
        _currentSettings.shadowQuality = QualitySettings.shadows;
        _currentSettings.textureQuality = QualitySettings.globalTextureMipmapLimit;
        _currentSettings.antiAliasing = QualitySettings.antiAliasing;
        _currentSettings.renderScale = QualitySettings.renderPipeline != null ? 1.0f : 1.0f;

        _originalSettings = _currentSettings;

        UpdateUI();
    }

    private void UpdateUI()
    {
        if (_qualityDropdown != null)
            _qualityDropdown.value = _currentSettings.qualityLevel;

        if (_resolutionDropdown != null)
            _resolutionDropdown.value = _currentSettings.resolutionIndex;

        if (_fullscreenToggle != null)
            _fullscreenToggle.isOn = _currentSettings.fullscreen;

        if (_vsyncToggle != null)
            _vsyncToggle.isOn = _currentSettings.vsync;

        if (_shadowDistanceSlider != null)
        {
            _shadowDistanceSlider.value = _currentSettings.shadowDistance / _maxShadowDistance;
            if (_shadowDistanceText != null)
                _shadowDistanceText.text = _currentSettings.shadowDistance.ToString("F0") + "m";
        }

        if (_shadowQualityDropdown != null)
            _shadowQualityDropdown.value = (int)_currentSettings.shadowQuality;

        if (_textureQualityDropdown != null)
            _textureQualityDropdown.value = _currentSettings.textureQuality;

        if (_antiAliasingDropdown != null)
        {
            int aaIndex = 0;
            switch (_currentSettings.antiAliasing)
            {
                case 0: aaIndex = 0; break;
                case 2: aaIndex = 1; break;
                case 4: aaIndex = 2; break;
                case 8: aaIndex = 3; break;
            }
            _antiAliasingDropdown.value = aaIndex;
        }

        if (_renderScaleSlider != null)
        {
            _renderScaleSlider.value = _currentSettings.renderScale;
            if (_renderScaleText != null)
                _renderScaleText.text = (_currentSettings.renderScale * 100f).ToString("F0") + "%";
        }
    }

    private void SetupUICallbacks()
    {
        if (_qualityDropdown != null)
            _qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (_resolutionDropdown != null)
            _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (_fullscreenToggle != null)
            _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        if (_vsyncToggle != null)
            _vsyncToggle.onValueChanged.AddListener(OnVSyncChanged);

        if (_shadowDistanceSlider != null)
            _shadowDistanceSlider.onValueChanged.AddListener(OnShadowDistanceChanged);

        if (_shadowQualityDropdown != null)
            _shadowQualityDropdown.onValueChanged.AddListener(OnShadowQualityChanged);

        if (_textureQualityDropdown != null)
            _textureQualityDropdown.onValueChanged.AddListener(OnTextureQualityChanged);

        if (_antiAliasingDropdown != null)
            _antiAliasingDropdown.onValueChanged.AddListener(OnAntiAliasingChanged);

        if (_renderScaleSlider != null)
            _renderScaleSlider.onValueChanged.AddListener(OnRenderScaleChanged);

        if (_applyButton != null)
            _applyButton.onClick.AddListener(ApplySettings);

        if (_resetButton != null)
            _resetButton.onClick.AddListener(ResetToDefaults);
    }

    private void OnQualityChanged(int value)
    {
        if (!_isInitialized) return;
        _currentSettings.qualityLevel = value;
        if (_autoApplyChanges) ApplyQualityLevel();
    }

    private void OnResolutionChanged(int value)
    {
        if (!_isInitialized) return;
        _currentSettings.resolutionIndex = value;
        if (_autoApplyChanges) ApplyResolution();
    }

    private void OnFullscreenChanged(bool value)
    {
        if (!_isInitialized) return;
        _currentSettings.fullscreen = value;
        if (_autoApplyChanges) ApplyFullscreen();
    }

    private void OnVSyncChanged(bool value)
    {
        if (!_isInitialized) return;
        _currentSettings.vsync = value;
        if (_autoApplyChanges) ApplyVSync();
    }

    private void OnShadowDistanceChanged(float value)
    {
        if (!_isInitialized) return;
        _currentSettings.shadowDistance = value * _maxShadowDistance;
        if (_shadowDistanceText != null)
            _shadowDistanceText.text = _currentSettings.shadowDistance.ToString("F0") + "m";
        if (_autoApplyChanges) ApplyShadowDistance();
    }

    private void OnShadowQualityChanged(int value)
    {
        if (!_isInitialized) return;
        _currentSettings.shadowQuality = (ShadowQuality)value;
        if (_autoApplyChanges) ApplyShadowQuality();
    }

    private void OnTextureQualityChanged(int value)
    {
        if (!_isInitialized) return;
        _currentSettings.textureQuality = value;
        if (_autoApplyChanges) ApplyTextureQuality();
    }

    private void OnAntiAliasingChanged(int value)
    {
        if (!_isInitialized) return;
        int[] aaValues = { 0, 2, 4, 8 };
        _currentSettings.antiAliasing = aaValues[Mathf.Clamp(value, 0, aaValues.Length - 1)];
        if (_autoApplyChanges) ApplyAntiAliasing();
    }

    private void OnRenderScaleChanged(float value)
    {
        if (!_isInitialized) return;
        _currentSettings.renderScale = value;
        if (_renderScaleText != null)
            _renderScaleText.text = (value * 100f).ToString("F0") + "%";
        if (_autoApplyChanges) ApplyRenderScale();
    }

    public void ApplySettings()
    {
        ApplyQualityLevel();
        ApplyResolution();
        ApplyFullscreen();
        ApplyVSync();
        ApplyShadowDistance();
        ApplyShadowQuality();
        ApplyTextureQuality();
        ApplyAntiAliasing();
        ApplyRenderScale();
    }

    private void ApplyQualityLevel()
    {
        QualitySettings.SetQualityLevel(_currentSettings.qualityLevel, true);
    }

    private void ApplyResolution()
    {
        if (_currentSettings.resolutionIndex >= 0 && _currentSettings.resolutionIndex < _availableResolutions.Length)
        {
            Resolution resolution = _availableResolutions[_currentSettings.resolutionIndex];
            Screen.SetResolution(resolution.width, resolution.height, _currentSettings.fullscreen, resolution.refreshRate);
        }
    }

    private void ApplyFullscreen()
    {
        Screen.fullScreen = _currentSettings.fullscreen;
    }

    private void ApplyVSync()
    {
        QualitySettings.vSyncCount = _currentSettings.vsync ? 1 : 0;
    }

    private void ApplyShadowDistance()
    {
        QualitySettings.shadowDistance = _currentSettings.shadowDistance;
    }

    private void ApplyShadowQuality()
    {
        QualitySettings.shadows = _currentSettings.shadowQuality;
    }

    private void ApplyTextureQuality()
    {
        QualitySettings.globalTextureMipmapLimit = _currentSettings.textureQuality;
    }

    private void ApplyAntiAliasing()
    {
        QualitySettings.antiAliasing = _currentSettings.antiAliasing;
    }

    private void ApplyRenderScale()
    {
        // Note: Render scale implementation depends on render pipeline
        // This is a placeholder for custom render scale logic
    }

    public void ResetToDefaults()
    {
        _currentSettings = _originalSettings;
        UpdateUI();
        if (_autoApplyChanges) ApplySettings();
    }

    public void SaveSettings()
    {
        PlayerPrefs.SetInt("QualityLevel", _currentSettings.qualityLevel);
        PlayerPrefs.SetInt("ResolutionIndex", _currentSettings.resolutionIndex);
        PlayerPrefs.SetInt("Fullscreen", _currentSettings.fullscreen ? 1 : 0);
        PlayerPrefs.SetInt("VSync", _currentSettings.vsync ? 1 : 0);
        PlayerPrefs.SetFloat("ShadowDistance", _currentSettings.shadowDistance);
        PlayerPrefs.SetInt("ShadowQuality", (int)_currentSettings.shadowQuality);
        PlayerPrefs.SetInt("TextureQuality", _currentSettings.textureQuality);
        PlayerPrefs.SetInt("AntiAliasing", _currentSettings.antiAliasing);
        PlayerPrefs.SetFloat("RenderScale", _currentSettings.renderScale);
        PlayerPrefs.Save();
    }

    public void LoadSettings()
    {
        if (PlayerPrefs.HasKey("QualityLevel"))
        {
            _currentSettings.qualityLevel = PlayerPrefs.GetInt("QualityLevel");
            _currentSettings.resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", _currentResolutionIndex);
            _currentSettings.fullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;
            _currentSettings.vsync = PlayerPrefs.GetInt("VSync", 1) == 1;
            _currentSettings.shadowDistance = PlayerPrefs.GetFloat("ShadowDistance", 50f);
            _currentSettings.shadowQuality = (ShadowQuality)PlayerPrefs.GetInt("ShadowQuality", 2);
            _currentSettings.textureQuality = PlayerPrefs.GetInt("TextureQuality", 0);
            _currentSettings.antiAliasing = PlayerPrefs.GetInt("AntiAliasing", 0);
            _currentSettings.renderScale = PlayerPrefs.GetFloat("RenderScale", 1.0f);

            UpdateUI();
            ApplySettings();
        }
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveSettings();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveSettings();
    }