// Prompt: save and load system
// Type: general

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class SaveLoadSystem : MonoBehaviour
{
    [System.Serializable]
    public class GameData
    {
        public Vector3 playerPosition;
        public Quaternion playerRotation;
        public float playerHealth = 100f;
        public int playerScore = 0;
        public int playerLevel = 1;
        public float playerExperience = 0f;
        public List<string> unlockedAchievements = new List<string>();
        public List<string> collectedItems = new List<string>();
        public Dictionary<string, bool> gameFlags = new Dictionary<string, bool>();
        public Dictionary<string, float> gameValues = new Dictionary<string, float>();
        public string currentScene = "";
        public float totalPlayTime = 0f;
        public DateTime lastSaveTime;
        
        public GameData()
        {
            lastSaveTime = DateTime.Now;
        }
    }

    [System.Serializable]
    public class SaveSlot
    {
        public string slotName;
        public GameData gameData;
        public DateTime saveTime;
        public string sceneName;
        public float playTime;
        public Texture2D screenshot;
    }

    [Header("Save Settings")]
    [SerializeField] private string _saveFileName = "GameSave";
    [SerializeField] private string _saveFileExtension = ".json";
    [SerializeField] private int _maxSaveSlots = 5;
    [SerializeField] private bool _autoSaveEnabled = true;
    [SerializeField] private float _autoSaveInterval = 300f;

    [Header("Player References")]
    [SerializeField] private Transform _playerTransform;
    [SerializeField] private GameObject _playerGameObject;

    [Header("Events")]
    public UnityEvent OnSaveStarted;
    public UnityEvent OnSaveCompleted;
    public UnityEvent OnLoadStarted;
    public UnityEvent OnLoadCompleted;
    public UnityEvent<string> OnSaveError;
    public UnityEvent<string> OnLoadError;

    private GameData _currentGameData;
    private List<SaveSlot> _saveSlots;
    private float _autoSaveTimer;
    private float _sessionStartTime;
    private string _saveFolderPath;

    public GameData CurrentGameData => _currentGameData;
    public List<SaveSlot> SaveSlots => _saveSlots;

    private void Awake()
    {
        _currentGameData = new GameData();
        _saveSlots = new List<SaveSlot>();
        _sessionStartTime = Time.time;
        
        _saveFolderPath = Path.Combine(Application.persistentDataPath, "Saves");
        
        if (!Directory.Exists(_saveFolderPath))
        {
            Directory.CreateDirectory(_saveFolderPath);
        }

        if (_playerTransform == null && _playerGameObject == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTransform = player.transform;
                _playerGameObject = player;
            }
        }
    }

    private void Start()
    {
        LoadSaveSlots();
        InitializeGameData();
    }

    private void Update()
    {
        if (_autoSaveEnabled)
        {
            _autoSaveTimer += Time.deltaTime;
            if (_autoSaveTimer >= _autoSaveInterval)
            {
                AutoSave();
                _autoSaveTimer = 0f;
            }
        }

        UpdatePlayTime();
    }

    private void InitializeGameData()
    {
        _currentGameData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (_playerTransform != null)
        {
            _currentGameData.playerPosition = _playerTransform.position;
            _currentGameData.playerRotation = _playerTransform.rotation;
        }
    }

    private void UpdatePlayTime()
    {
        _currentGameData.totalPlayTime = Time.time - _sessionStartTime;
    }

    public void SaveGame(int slotIndex = 0)
    {
        try
        {
            OnSaveStarted?.Invoke();
            
            UpdateCurrentGameData();
            
            SaveSlot saveSlot = new SaveSlot
            {
                slotName = $"Save Slot {slotIndex + 1}",
                gameData = _currentGameData,
                saveTime = DateTime.Now,
                sceneName = _currentGameData.currentScene,
                playTime = _currentGameData.totalPlayTime
            };

            string json = JsonUtility.ToJson(saveSlot, true);
            string filePath = GetSaveFilePath(slotIndex);
            
            File.WriteAllText(filePath, json);
            
            UpdateSaveSlot(slotIndex, saveSlot);
            
            OnSaveCompleted?.Invoke();
            Debug.Log($"Game saved to slot {slotIndex}");
        }
        catch (Exception e)
        {
            OnSaveError?.Invoke($"Save failed: {e.Message}");
            Debug.LogError($"Save failed: {e.Message}");
        }
    }

    public void LoadGame(int slotIndex = 0)
    {
        try
        {
            OnLoadStarted?.Invoke();
            
            string filePath = GetSaveFilePath(slotIndex);
            
            if (!File.Exists(filePath))
            {
                OnLoadError?.Invoke("Save file not found");
                return;
            }

            string json = File.ReadAllText(filePath);
            SaveSlot saveSlot = JsonUtility.FromJson<SaveSlot>(json);
            
            if (saveSlot?.gameData != null)
            {
                _currentGameData = saveSlot.gameData;
                ApplyGameData();
                OnLoadCompleted?.Invoke();
                Debug.Log($"Game loaded from slot {slotIndex}");
            }
            else
            {
                OnLoadError?.Invoke("Invalid save data");
            }
        }
        catch (Exception e)
        {
            OnLoadError?.Invoke($"Load failed: {e.Message}");
            Debug.LogError($"Load failed: {e.Message}");
        }
    }

    private void UpdateCurrentGameData()
    {
        _currentGameData.lastSaveTime = DateTime.Now;
        _currentGameData.currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        
        if (_playerTransform != null)
        {
            _currentGameData.playerPosition = _playerTransform.position;
            _currentGameData.playerRotation = _playerTransform.rotation;
        }
    }

    private void ApplyGameData()
    {
        if (_playerTransform != null)
        {
            _playerTransform.position = _currentGameData.playerPosition;
            _playerTransform.rotation = _currentGameData.playerRotation;
        }

        if (_currentGameData.currentScene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(_currentGameData.currentScene);
        }
    }

    public void AutoSave()
    {
        SaveGame(0);
        Debug.Log("Auto-save completed");
    }

    public void DeleteSave(int slotIndex)
    {
        try
        {
            string filePath = GetSaveFilePath(slotIndex);
            
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                
                if (slotIndex < _saveSlots.Count)
                {
                    _saveSlots[slotIndex] = null;
                }
                
                Debug.Log($"Save slot {slotIndex} deleted");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save: {e.Message}");
        }
    }

    public bool HasSave(int slotIndex)
    {
        string filePath = GetSaveFilePath(slotIndex);
        return File.Exists(filePath);
    }

    public SaveSlot GetSaveSlot(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _saveSlots.Count)
        {
            return _saveSlots[slotIndex];
        }
        return null;
    }

    private void LoadSaveSlots()
    {
        _saveSlots.Clear();
        
        for (int i = 0; i < _maxSaveSlots; i++)
        {
            string filePath = GetSaveFilePath(i);
            
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    SaveSlot saveSlot = JsonUtility.FromJson<SaveSlot>(json);
                    _saveSlots.Add(saveSlot);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to load save slot {i}: {e.Message}");
                    _saveSlots.Add(null);
                }
            }
            else
            {
                _saveSlots.Add(null);
            }
        }
    }

    private void UpdateSaveSlot(int slotIndex, SaveSlot saveSlot)
    {
        while (_saveSlots.Count <= slotIndex)
        {
            _saveSlots.Add(null);
        }
        
        _saveSlots[slotIndex] = saveSlot;
    }

    private string GetSaveFilePath(int slotIndex)
    {
        return Path.Combine(_saveFolderPath, $"{_saveFileName}_{slotIndex}{_saveFileExtension}");
    }

    public void SetGameFlag(string flagName, bool value)
    {
        _currentGameData.gameFlags[flagName] = value;
    }

    public bool GetGameFlag(string flagName, bool defaultValue = false)
    {
        return _currentGameData.gameFlags.ContainsKey(flagName) ? _currentGameData.gameFlags[flagName] : defaultValue;
    }

    public void SetGameValue(string valueName, float value)
    {
        _currentGameData.gameValues[valueName] = value;
    }

    public float GetGameValue(string valueName, float defaultValue = 0f)
    {
        return _currentGameData.gameValues.ContainsKey(valueName) ? _currentGameData.gameValues[valueName] : defaultValue;
    }

    public void AddCollectedItem(string itemId)
    {
        if (!_currentGameData.collectedItems.Contains(itemId))
        {
            _currentGameData.collectedItems.Add(itemId);
        }
    }

    public bool HasCollectedItem(string itemId)
    {
        return _currentGameData.collectedItems.Contains(itemId);
    }

    public void UnlockAchievement(string achievementId)
    {
        if (!_currentGameData.unlockedAchievements.Contains(achievementId))
        {
            _currentGameData.unlockedAchievements.Add(achievementId);
        }
    }

    public bool IsAchievementUnlocked(string achievementId)
    {
        return _currentGameData.unlockedAchievements.Contains(achievementId);
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _autoSaveEnabled)
        {
            AutoSave();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _autoSaveEnabled)
        {
            AutoSave();
        }
    }

    private void OnDestroy()
    {
        if (_autoSaveEnabled)
        {
            AutoSave();
        }
    }
}