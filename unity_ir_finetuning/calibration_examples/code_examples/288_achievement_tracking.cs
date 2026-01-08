// Prompt: achievement tracking
// Type: general

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class AchievementTracker : MonoBehaviour
{
    [System.Serializable]
    public class Achievement
    {
        [Header("Achievement Info")]
        public string id;
        public string title;
        [TextArea(2, 4)]
        public string description;
        public Sprite icon;
        public int targetValue;
        public bool isUnlocked;
        public bool isHidden;
        
        [Header("Progress")]
        public int currentValue;
        public DateTime unlockedDate;
        
        public float Progress => targetValue > 0 ? Mathf.Clamp01((float)currentValue / targetValue) : 0f;
        public bool IsCompleted => currentValue >= targetValue;
    }

    [System.Serializable]
    public class AchievementEvent : UnityEvent<Achievement> { }

    [Header("Achievement Settings")]
    [SerializeField] private List<Achievement> _achievements = new List<Achievement>();
    [SerializeField] private bool _saveToPlayerPrefs = true;
    [SerializeField] private string _savePrefix = "Achievement_";

    [Header("Events")]
    public AchievementEvent OnAchievementUnlocked;
    public AchievementEvent OnAchievementProgress;
    public UnityEvent<int> OnTotalAchievementsChanged;

    [Header("Statistics")]
    [SerializeField] private int _totalKills;
    [SerializeField] private int _totalDeaths;
    [SerializeField] private int _totalCoinsCollected;
    [SerializeField] private float _totalPlayTime;
    [SerializeField] private int _levelsCompleted;
    [SerializeField] private int _enemiesDefeated;
    [SerializeField] private float _distanceTraveled;

    private Dictionary<string, Achievement> _achievementDict;
    private float _sessionStartTime;

    private void Awake()
    {
        _achievementDict = new Dictionary<string, Achievement>();
        
        foreach (var achievement in _achievements)
        {
            if (!string.IsNullOrEmpty(achievement.id))
            {
                _achievementDict[achievement.id] = achievement;
            }
        }
    }

    private void Start()
    {
        _sessionStartTime = Time.time;
        
        if (_saveToPlayerPrefs)
        {
            LoadAchievements();
        }
        
        InitializeDefaultAchievements();
    }

    private void Update()
    {
        _totalPlayTime += Time.deltaTime;
        UpdatePlayTimeAchievements();
    }

    private void InitializeDefaultAchievements()
    {
        if (_achievements.Count == 0)
        {
            AddDefaultAchievements();
        }
    }

    private void AddDefaultAchievements()
    {
        var defaultAchievements = new List<Achievement>
        {
            new Achievement
            {
                id = "first_kill",
                title = "First Blood",
                description = "Defeat your first enemy",
                targetValue = 1,
                isHidden = false
            },
            new Achievement
            {
                id = "kill_streak_10",
                title = "Killing Spree",
                description = "Defeat 10 enemies",
                targetValue = 10,
                isHidden = false
            },
            new Achievement
            {
                id = "coin_collector",
                title = "Coin Collector",
                description = "Collect 100 coins",
                targetValue = 100,
                isHidden = false
            },
            new Achievement
            {
                id = "survivor",
                title = "Survivor",
                description = "Play for 10 minutes without dying",
                targetValue = 600,
                isHidden = false
            },
            new Achievement
            {
                id = "explorer",
                title = "Explorer",
                description = "Travel 1000 units",
                targetValue = 1000,
                isHidden = false
            }
        };

        foreach (var achievement in defaultAchievements)
        {
            AddAchievement(achievement);
        }
    }

    public void AddAchievement(Achievement achievement)
    {
        if (string.IsNullOrEmpty(achievement.id)) return;
        
        if (!_achievementDict.ContainsKey(achievement.id))
        {
            _achievements.Add(achievement);
            _achievementDict[achievement.id] = achievement;
        }
    }

    public void IncrementStat(string statName, int value = 1)
    {
        switch (statName.ToLower())
        {
            case "kills":
            case "enemies_defeated":
                _totalKills += value;
                _enemiesDefeated += value;
                CheckKillAchievements();
                break;
            case "deaths":
                _totalDeaths += value;
                break;
            case "coins":
            case "coins_collected":
                _totalCoinsCollected += value;
                CheckCoinAchievements();
                break;
            case "levels_completed":
                _levelsCompleted += value;
                CheckLevelAchievements();
                break;
        }
    }

    public void AddDistance(float distance)
    {
        _distanceTraveled += distance;
        CheckDistanceAchievements();
    }

    public void UnlockAchievement(string achievementId)
    {
        if (_achievementDict.TryGetValue(achievementId, out Achievement achievement))
        {
            if (!achievement.isUnlocked)
            {
                achievement.isUnlocked = true;
                achievement.unlockedDate = DateTime.Now;
                achievement.currentValue = achievement.targetValue;
                
                OnAchievementUnlocked?.Invoke(achievement);
                
                if (_saveToPlayerPrefs)
                {
                    SaveAchievement(achievement);
                }
                
                Debug.Log($"Achievement Unlocked: {achievement.title}");
            }
        }
    }

    public void UpdateAchievementProgress(string achievementId, int currentValue)
    {
        if (_achievementDict.TryGetValue(achievementId, out Achievement achievement))
        {
            if (!achievement.isUnlocked)
            {
                int previousValue = achievement.currentValue;
                achievement.currentValue = currentValue;
                
                OnAchievementProgress?.Invoke(achievement);
                
                if (achievement.IsCompleted && !achievement.isUnlocked)
                {
                    UnlockAchievement(achievementId);
                }
                else if (_saveToPlayerPrefs && previousValue != currentValue)
                {
                    SaveAchievement(achievement);
                }
            }
        }
    }

    private void CheckKillAchievements()
    {
        UpdateAchievementProgress("first_kill", _totalKills);
        UpdateAchievementProgress("kill_streak_10", _totalKills);
        UpdateAchievementProgress("kill_streak_50", _totalKills);
        UpdateAchievementProgress("kill_streak_100", _totalKills);
    }

    private void CheckCoinAchievements()
    {
        UpdateAchievementProgress("coin_collector", _totalCoinsCollected);
        UpdateAchievementProgress("rich_player", _totalCoinsCollected);
    }

    private void CheckLevelAchievements()
    {
        UpdateAchievementProgress("level_complete", _levelsCompleted);
        UpdateAchievementProgress("master_player", _levelsCompleted);
    }

    private void CheckDistanceAchievements()
    {
        UpdateAchievementProgress("explorer", Mathf.FloorToInt(_distanceTraveled));
        UpdateAchievementProgress("marathon_runner", Mathf.FloorToInt(_distanceTraveled));
    }

    private void UpdatePlayTimeAchievements()
    {
        UpdateAchievementProgress("survivor", Mathf.FloorToInt(_totalPlayTime));
        UpdateAchievementProgress("dedicated_player", Mathf.FloorToInt(_totalPlayTime));
    }

    public Achievement GetAchievement(string achievementId)
    {
        _achievementDict.TryGetValue(achievementId, out Achievement achievement);
        return achievement;
    }

    public List<Achievement> GetAllAchievements()
    {
        return new List<Achievement>(_achievements);
    }

    public List<Achievement> GetUnlockedAchievements()
    {
        return _achievements.FindAll(a => a.isUnlocked);
    }

    public List<Achievement> GetLockedAchievements()
    {
        return _achievements.FindAll(a => !a.isUnlocked && !a.isHidden);
    }

    public int GetUnlockedCount()
    {
        return _achievements.FindAll(a => a.isUnlocked).Count;
    }

    public float GetCompletionPercentage()
    {
        if (_achievements.Count == 0) return 0f;
        return (float)GetUnlockedCount() / _achievements.Count * 100f;
    }

    private void SaveAchievements()
    {
        foreach (var achievement in _achievements)
        {
            SaveAchievement(achievement);
        }
        
        PlayerPrefs.SetInt(_savePrefix + "TotalKills", _totalKills);
        PlayerPrefs.SetInt(_savePrefix + "TotalDeaths", _totalDeaths);
        PlayerPrefs.SetInt(_savePrefix + "TotalCoins", _totalCoinsCollected);
        PlayerPrefs.SetFloat(_savePrefix + "TotalPlayTime", _totalPlayTime);
        PlayerPrefs.SetInt(_savePrefix + "LevelsCompleted", _levelsCompleted);
        PlayerPrefs.SetFloat(_savePrefix + "DistanceTraveled", _distanceTraveled);
        
        PlayerPrefs.Save();
    }

    private void SaveAchievement(Achievement achievement)
    {
        string prefix = _savePrefix + achievement.id + "_";
        PlayerPrefs.SetInt(prefix + "Unlocked", achievement.isUnlocked ? 1 : 0);
        PlayerPrefs.SetInt(prefix + "CurrentValue", achievement.currentValue);
        
        if (achievement.isUnlocked)
        {
            PlayerPrefs.SetString(prefix + "UnlockedDate", achievement.unlockedDate.ToBinary().ToString());
        }
    }

    private void LoadAchievements()
    {
        foreach (var achievement in _achievements)
        {
            LoadAchievement(achievement);
        }
        
        _totalKills = PlayerPrefs.GetInt(_savePrefix + "TotalKills", 0);
        _totalDeaths = PlayerPrefs.GetInt(_savePrefix + "TotalDeaths", 0);
        _totalCoinsCollected = PlayerPrefs.GetInt(_savePrefix + "TotalCoins", 0);
        _totalPlayTime = PlayerPrefs.GetFloat(_savePrefix + "TotalPlayTime", 0f);
        _levelsCompleted = PlayerPrefs.GetInt(_savePrefix + "LevelsCompleted", 0);
        _distanceTraveled = PlayerPrefs.GetFloat(_savePrefix + "DistanceTraveled", 0f);
    }

    private void LoadAchievement(Achievement achievement)
    {
        string prefix = _savePrefix + achievement.id + "_";
        achievement.isUnlocked = PlayerPrefs.GetInt(prefix + "Unlocked", 0) == 1;
        achievement.currentValue = PlayerPrefs.GetInt(prefix + "CurrentValue", 0);
        
        if (achievement.isUnlocked)
        {
            string dateString = PlayerPrefs.GetString(prefix + "UnlockedDate", "");
            if (!string.IsNullOrEmpty(dateString) && long.TryParse(dateString, out long dateBinary))
            {
                achievement.unlockedDate = DateTime.FromBinary(dateBinary);
            }
        }
    }

    public void ResetAllAchievements()
    {
        foreach (var achievement in _achievements)
        {
            achievement.isUnlocked = false;
            achievement.currentValue = 0;
            achievement.unlockedDate = default(DateTime);
        }
        
        _totalKills = 0;
        _totalDeaths = 0;
        _totalCoinsCollected = 0;
        _totalPlayTime = 0f;
        _levelsCompleted = 0;
        _distanceTraveled = 0f;
        
        if (_saveToPlayerPrefs)
        {
            ClearSavedData();
        }
    }

    private void ClearSavedData()
    {
        foreach (var achievement in _achievements)
        {
            string prefix = _savePrefix + achievement.id + "_";
            PlayerPrefs.DeleteKey(prefix + "Unlocked");
            PlayerPrefs.DeleteKey(prefix + "CurrentValue");
            PlayerPrefs.DeleteKey(prefix + "UnlockedDate");
        }
        
        PlayerPrefs.DeleteKey(_savePrefix + "TotalKills");
        PlayerPrefs.DeleteKey(_savePrefix + "TotalDeaths");
        PlayerPrefs.DeleteKey(_savePrefix + "TotalCoins");
        PlayerPrefs.DeleteKey(_savePrefix + "TotalPlayTime");
        PlayerPrefs.DeleteKey(_savePrefix + "LevelsCompleted");
        PlayerPrefs.DeleteKey(_savePrefix + "DistanceTraveled");
        
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _saveToPlayerPrefs)
        {
            SaveAchievements();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _saveToPlayerPrefs)
        {
            SaveAchievements();
        }
    }

    private void OnDestroy()
    {
        if (_saveToPlayerPrefs)
        {
            SaveAchievements();
        }
    }
}