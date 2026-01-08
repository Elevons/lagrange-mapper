// Prompt: language localization
// Type: general

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[System.Serializable]
public class LocalizedString
{
    public string key;
    public string defaultValue;
    
    public LocalizedString(string key, string defaultValue = "")
    {
        this.key = key;
        this.defaultValue = defaultValue;
    }
}

[System.Serializable]
public class LanguageData
{
    public string languageCode;
    public string languageName;
    public Dictionary<string, string> translations = new Dictionary<string, string>();
}

[System.Serializable]
public class TranslationEntry
{
    public string key;
    public string value;
}

[System.Serializable]
public class LanguageFile
{
    public string languageCode;
    public string languageName;
    public List<TranslationEntry> translations = new List<TranslationEntry>();
}

public class LocalizationManager : MonoBehaviour
{
    [Header("Language Settings")]
    [SerializeField] private string _currentLanguageCode = "en";
    [SerializeField] private string _fallbackLanguageCode = "en";
    [SerializeField] private bool _autoDetectSystemLanguage = true;
    
    [Header("Language Files")]
    [SerializeField] private List<TextAsset> _languageFiles = new List<TextAsset>();
    
    [Header("Events")]
    public UnityEvent<string> OnLanguageChanged = new UnityEvent<string>();
    
    private Dictionary<string, LanguageData> _languages = new Dictionary<string, LanguageData>();
    private Dictionary<string, string> _currentTranslations = new Dictionary<string, string>();
    
    public static LocalizationManager Instance { get; private set; }
    
    public string CurrentLanguage => _currentLanguageCode;
    public List<string> AvailableLanguages => new List<string>(_languages.Keys);
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeLocalization();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        LoadLanguageFiles();
        
        if (_autoDetectSystemLanguage)
        {
            DetectSystemLanguage();
        }
        
        SetLanguage(_currentLanguageCode);
    }
    
    private void InitializeLocalization()
    {
        _languages.Clear();
        _currentTranslations.Clear();
        
        // Load saved language preference
        string savedLanguage = PlayerPrefs.GetString("LocalizationLanguage", "");
        if (!string.IsNullOrEmpty(savedLanguage))
        {
            _currentLanguageCode = savedLanguage;
        }
    }
    
    private void LoadLanguageFiles()
    {
        foreach (TextAsset languageFile in _languageFiles)
        {
            if (languageFile == null) continue;
            
            try
            {
                LanguageFile langFile = JsonUtility.FromJson<LanguageFile>(languageFile.text);
                
                if (!_languages.ContainsKey(langFile.languageCode))
                {
                    _languages[langFile.languageCode] = new LanguageData
                    {
                        languageCode = langFile.languageCode,
                        languageName = langFile.languageName
                    };
                }
                
                foreach (TranslationEntry entry in langFile.translations)
                {
                    _languages[langFile.languageCode].translations[entry.key] = entry.value;
                }
                
                Debug.Log($"Loaded language: {langFile.languageName} ({langFile.languageCode}) with {langFile.translations.Count} translations");
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load language file {languageFile.name}: {e.Message}");
            }
        }
    }
    
    private void DetectSystemLanguage()
    {
        SystemLanguage systemLang = Application.systemLanguage;
        string detectedCode = GetLanguageCode(systemLang);
        
        if (_languages.ContainsKey(detectedCode))
        {
            _currentLanguageCode = detectedCode;
        }
    }
    
    private string GetLanguageCode(SystemLanguage systemLanguage)
    {
        switch (systemLanguage)
        {
            case SystemLanguage.English: return "en";
            case SystemLanguage.Spanish: return "es";
            case SystemLanguage.French: return "fr";
            case SystemLanguage.German: return "de";
            case SystemLanguage.Italian: return "it";
            case SystemLanguage.Portuguese: return "pt";
            case SystemLanguage.Russian: return "ru";
            case SystemLanguage.Japanese: return "ja";
            case SystemLanguage.Korean: return "ko";
            case SystemLanguage.Chinese: return "zh";
            case SystemLanguage.ChineseSimplified: return "zh-CN";
            case SystemLanguage.ChineseTraditional: return "zh-TW";
            case SystemLanguage.Dutch: return "nl";
            case SystemLanguage.Polish: return "pl";
            case SystemLanguage.Swedish: return "sv";
            case SystemLanguage.Norwegian: return "no";
            case SystemLanguage.Danish: return "da";
            case SystemLanguage.Finnish: return "fi";
            case SystemLanguage.Turkish: return "tr";
            case SystemLanguage.Arabic: return "ar";
            case SystemLanguage.Hebrew: return "he";
            case SystemLanguage.Thai: return "th";
            case SystemLanguage.Vietnamese: return "vi";
            default: return "en";
        }
    }
    
    public bool SetLanguage(string languageCode)
    {
        if (!_languages.ContainsKey(languageCode))
        {
            Debug.LogWarning($"Language '{languageCode}' not found. Using fallback language '{_fallbackLanguageCode}'");
            languageCode = _fallbackLanguageCode;
            
            if (!_languages.ContainsKey(languageCode))
            {
                Debug.LogError($"Fallback language '{_fallbackLanguageCode}' not found!");
                return false;
            }
        }
        
        _currentLanguageCode = languageCode;
        _currentTranslations = new Dictionary<string, string>(_languages[languageCode].translations);
        
        // Save language preference
        PlayerPrefs.SetString("LocalizationLanguage", languageCode);
        PlayerPrefs.Save();
        
        OnLanguageChanged.Invoke(languageCode);
        
        Debug.Log($"Language changed to: {GetLanguageName(languageCode)} ({languageCode})");
        return true;
    }
    
    public string GetLocalizedString(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key))
        {
            return "";
        }
        
        string localizedText = "";
        
        // Try current language
        if (_currentTranslations.ContainsKey(key))
        {
            localizedText = _currentTranslations[key];
        }
        // Try fallback language
        else if (_languages.ContainsKey(_fallbackLanguageCode) && 
                 _languages[_fallbackLanguageCode].translations.ContainsKey(key))
        {
            localizedText = _languages[_fallbackLanguageCode].translations[key];
        }
        // Return key if no translation found
        else
        {
            Debug.LogWarning($"Translation not found for key: {key}");
            return $"[{key}]";
        }
        
        // Format string with arguments if provided
        if (args != null && args.Length > 0)
        {
            try
            {
                localizedText = string.Format(localizedText, args);
            }
            catch (FormatException e)
            {
                Debug.LogError($"String formatting error for key '{key}': {e.Message}");
                return localizedText;
            }
        }
        
        return localizedText;
    }
    
    public string GetLocalizedString(LocalizedString localizedString, params object[] args)
    {
        if (localizedString == null)
        {
            return "";
        }
        
        string result = GetLocalizedString(localizedString.key, args);
        
        // Return default value if translation not found and default is provided
        if (result.StartsWith("[") && result.EndsWith("]") && !string.IsNullOrEmpty(localizedString.defaultValue))
        {
            return localizedString.defaultValue;
        }
        
        return result;
    }
    
    public string GetLanguageName(string languageCode)
    {
        if (_languages.ContainsKey(languageCode))
        {
            return _languages[languageCode].languageName;
        }
        return languageCode;
    }
    
    public bool HasTranslation(string key)
    {
        return _currentTranslations.ContainsKey(key) || 
               (_languages.ContainsKey(_fallbackLanguageCode) && 
                _languages[_fallbackLanguageCode].translations.ContainsKey(key));
    }
    
    public void AddTranslation(string languageCode, string key, string value)
    {
        if (!_languages.ContainsKey(languageCode))
        {
            _languages[languageCode] = new LanguageData
            {
                languageCode = languageCode,
                languageName = languageCode
            };
        }
        
        _languages[languageCode].translations[key] = value;
        
        // Update current translations if this is the active language
        if (languageCode == _currentLanguageCode)
        {
            _currentTranslations[key] = value;
        }
    }
    
    public void RemoveTranslation(string key)
    {
        foreach (var language in _languages.Values)
        {
            language.translations.Remove(key);
        }
        
        _currentTranslations.Remove(key);
    }
    
    public Dictionary<string, string> GetAllTranslations(string languageCode)
    {
        if (_languages.ContainsKey(languageCode))
        {
            return new Dictionary<string, string>(_languages[languageCode].translations);
        }
        return new Dictionary<string, string>();
    }
    
    public void RefreshAllLocalizedComponents()
    {
        LocalizedText[] localizedTexts = FindObjectsOfType<LocalizedText>();
        foreach (LocalizedText localizedText in localizedTexts)
        {
            localizedText.UpdateText();
        }
    }
    
    [ContextMenu("Reload Language Files")]
    private void ReloadLanguageFiles()
    {
        LoadLanguageFiles();
        SetLanguage(_currentLanguageCode);
        RefreshAllLocalizedComponents();
    }
}