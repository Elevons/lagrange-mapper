// Prompt: audio volume controls
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;

public class AudioVolumeControls : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup _masterMixerGroup;
    [SerializeField] private AudioMixerGroup _musicMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;
    
    [Header("Volume Sliders")]
    [SerializeField] private Slider _masterVolumeSlider;
    [SerializeField] private Slider _musicVolumeSlider;
    [SerializeField] private Slider _sfxVolumeSlider;
    
    [Header("Volume Labels")]
    [SerializeField] private Text _masterVolumeLabel;
    [SerializeField] private Text _musicVolumeLabel;
    [SerializeField] private Text _sfxVolumeLabel;
    
    [Header("Mute Toggles")]
    [SerializeField] private Toggle _masterMuteToggle;
    [SerializeField] private Toggle _musicMuteToggle;
    [SerializeField] private Toggle _sfxMuteToggle;
    
    [Header("Settings")]
    [SerializeField] private float _minVolume = -80f;
    [SerializeField] private float _maxVolume = 0f;
    [SerializeField] private bool _saveToPlayerPrefs = true;
    
    private const string MASTER_VOLUME_KEY = "MasterVolume";
    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string MASTER_MUTE_KEY = "MasterMute";
    private const string MUSIC_MUTE_KEY = "MusicMute";
    private const string SFX_MUTE_KEY = "SFXMute";
    
    private const string MASTER_MIXER_PARAM = "MasterVolume";
    private const string MUSIC_MIXER_PARAM = "MusicVolume";
    private const string SFX_MIXER_PARAM = "SFXVolume";
    
    private float _masterVolume = 1f;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;
    
    private bool _masterMuted = false;
    private bool _musicMuted = false;
    private bool _sfxMuted = false;
    
    private void Start()
    {
        LoadVolumeSettings();
        InitializeSliders();
        InitializeToggles();
        ApplyVolumeSettings();
    }
    
    private void LoadVolumeSettings()
    {
        if (_saveToPlayerPrefs)
        {
            _masterVolume = PlayerPrefs.GetFloat(MASTER_VOLUME_KEY, 1f);
            _musicVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
            _sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
            
            _masterMuted = PlayerPrefs.GetInt(MASTER_MUTE_KEY, 0) == 1;
            _musicMuted = PlayerPrefs.GetInt(MUSIC_MUTE_KEY, 0) == 1;
            _sfxMuted = PlayerPrefs.GetInt(SFX_MUTE_KEY, 0) == 1;
        }
    }
    
    private void InitializeSliders()
    {
        if (_masterVolumeSlider != null)
        {
            _masterVolumeSlider.minValue = 0f;
            _masterVolumeSlider.maxValue = 1f;
            _masterVolumeSlider.value = _masterVolume;
            _masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }
        
        if (_musicVolumeSlider != null)
        {
            _musicVolumeSlider.minValue = 0f;
            _musicVolumeSlider.maxValue = 1f;
            _musicVolumeSlider.value = _musicVolume;
            _musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }
        
        if (_sfxVolumeSlider != null)
        {
            _sfxVolumeSlider.minValue = 0f;
            _sfxVolumeSlider.maxValue = 1f;
            _sfxVolumeSlider.value = _sfxVolume;
            _sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }
    }
    
    private void InitializeToggles()
    {
        if (_masterMuteToggle != null)
        {
            _masterMuteToggle.isOn = _masterMuted;
            _masterMuteToggle.onValueChanged.AddListener(OnMasterMuteToggled);
        }
        
        if (_musicMuteToggle != null)
        {
            _musicMuteToggle.isOn = _musicMuted;
            _musicMuteToggle.onValueChanged.AddListener(OnMusicMuteToggled);
        }
        
        if (_sfxMuteToggle != null)
        {
            _sfxMuteToggle.isOn = _sfxMuted;
            _sfxMuteToggle.onValueChanged.AddListener(OnSFXMuteToggled);
        }
    }
    
    public void OnMasterVolumeChanged(float value)
    {
        _masterVolume = value;
        SetMasterVolume(value);
        UpdateVolumeLabel(_masterVolumeLabel, value);
        SaveVolumeSettings();
    }
    
    public void OnMusicVolumeChanged(float value)
    {
        _musicVolume = value;
        SetMusicVolume(value);
        UpdateVolumeLabel(_musicVolumeLabel, value);
        SaveVolumeSettings();
    }
    
    public void OnSFXVolumeChanged(float value)
    {
        _sfxVolume = value;
        SetSFXVolume(value);
        UpdateVolumeLabel(_sfxVolumeLabel, value);
        SaveVolumeSettings();
    }
    
    public void OnMasterMuteToggled(bool muted)
    {
        _masterMuted = muted;
        SetMasterVolume(muted ? 0f : _masterVolume);
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.interactable = !muted;
        SaveVolumeSettings();
    }
    
    public void OnMusicMuteToggled(bool muted)
    {
        _musicMuted = muted;
        SetMusicVolume(muted ? 0f : _musicVolume);
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.interactable = !muted;
        SaveVolumeSettings();
    }
    
    public void OnSFXMuteToggled(bool muted)
    {
        _sfxMuted = muted;
        SetSFXVolume(muted ? 0f : _sfxVolume);
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.interactable = !muted;
        SaveVolumeSettings();
    }
    
    private void SetMasterVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        if (_masterMixerGroup != null && _masterMixerGroup.audioMixer != null)
        {
            _masterMixerGroup.audioMixer.SetFloat(MASTER_MIXER_PARAM, dbVolume);
        }
        else
        {
            AudioListener.volume = volume;
        }
    }
    
    private void SetMusicVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        if (_musicMixerGroup != null && _musicMixerGroup.audioMixer != null)
        {
            _musicMixerGroup.audioMixer.SetFloat(MUSIC_MIXER_PARAM, dbVolume);
        }
    }
    
    private void SetSFXVolume(float volume)
    {
        float dbVolume = ConvertToDecibels(volume);
        if (_sfxMixerGroup != null && _sfxMixerGroup.audioMixer != null)
        {
            _sfxMixerGroup.audioMixer.SetFloat(SFX_MIXER_PARAM, dbVolume);
        }
    }
    
    private float ConvertToDecibels(float volume)
    {
        if (volume <= 0f)
            return _minVolume;
        
        return Mathf.Lerp(_minVolume, _maxVolume, volume);
    }
    
    private void UpdateVolumeLabel(Text label, float volume)
    {
        if (label != null)
        {
            label.text = Mathf.RoundToInt(volume * 100f) + "%";
        }
    }
    
    private void ApplyVolumeSettings()
    {
        SetMasterVolume(_masterMuted ? 0f : _masterVolume);
        SetMusicVolume(_musicMuted ? 0f : _musicVolume);
        SetSFXVolume(_sfxMuted ? 0f : _sfxVolume);
        
        UpdateVolumeLabel(_masterVolumeLabel, _masterVolume);
        UpdateVolumeLabel(_musicVolumeLabel, _musicVolume);
        UpdateVolumeLabel(_sfxVolumeLabel, _sfxVolume);
        
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.interactable = !_masterMuted;
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.interactable = !_musicMuted;
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.interactable = !_sfxMuted;
    }
    
    private void SaveVolumeSettings()
    {
        if (_saveToPlayerPrefs)
        {
            PlayerPrefs.SetFloat(MASTER_VOLUME_KEY, _masterVolume);
            PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, _musicVolume);
            PlayerPrefs.SetFloat(SFX_VOLUME_KEY, _sfxVolume);
            
            PlayerPrefs.SetInt(MASTER_MUTE_KEY, _masterMuted ? 1 : 0);
            PlayerPrefs.SetInt(MUSIC_MUTE_KEY, _musicMuted ? 1 : 0);
            PlayerPrefs.SetInt(SFX_MUTE_KEY, _sfxMuted ? 1 : 0);
            
            PlayerPrefs.Save();
        }
    }
    
    public void ResetToDefaults()
    {
        _masterVolume = 1f;
        _musicVolume = 1f;
        _sfxVolume = 1f;
        
        _masterMuted = false;
        _musicMuted = false;
        _sfxMuted = false;
        
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.value = _masterVolume;
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.value = _musicVolume;
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.value = _sfxVolume;
        
        if (_masterMuteToggle != null)
            _masterMuteToggle.isOn = _masterMuted;
        if (_musicMuteToggle != null)
            _musicMuteToggle.isOn = _musicMuted;
        if (_sfxMuteToggle != null)
            _sfxMuteToggle.isOn = _sfxMuted;
        
        ApplyVolumeSettings();
        SaveVolumeSettings();
    }
    
    private void OnDestroy()
    {
        if (_masterVolumeSlider != null)
            _masterVolumeSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);
        if (_musicVolumeSlider != null)
            _musicVolumeSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);
        if (_sfxVolumeSlider != null)
            _sfxVolumeSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
        
        if (_masterMuteToggle != null)
            _masterMuteToggle.onValueChanged.RemoveListener(OnMasterMuteToggled);
        if (_musicMuteToggle != null)
            _musicMuteToggle.onValueChanged.RemoveListener(OnMusicMuteToggled);
        if (_sfxMuteToggle != null)
            _sfxMuteToggle.onValueChanged.RemoveListener(OnSFXMuteToggled);
    }
}