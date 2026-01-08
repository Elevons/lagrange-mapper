// Prompt: statistics tracking
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System;

public class StatisticsTracker : MonoBehaviour
{
    [System.Serializable]
    public class StatisticEntry
    {
        public string name;
        public float value;
        public float maxValue;
        public bool trackMaximum;
        
        public StatisticEntry(string statName, float initialValue = 0f, bool trackMax = false)
        {
            name = statName;
            value = initialValue;
            maxValue = initialValue;
            trackMaximum = trackMax;
        }
    }

    [System.Serializable]
    public class StatisticEvent : UnityEvent<string, float> { }

    [Header("Statistics Configuration")]
    [SerializeField] private List<StatisticEntry> _statistics = new List<StatisticEntry>();
    [SerializeField] private bool _persistBetweenScenes = true;
    [SerializeField] private bool _saveToPlayerPrefs = true;
    [SerializeField] private string _savePrefix = "Stats_";

    [Header("Display Settings")]
    [SerializeField] private bool _showDebugInfo = false;
    [SerializeField] private Vector2 _debugPosition = new Vector2(10, 10);

    [Header("Events")]
    public StatisticEvent OnStatisticChanged;
    public StatisticEvent OnStatisticMaxReached;
    public UnityEvent OnStatisticsReset;

    private Dictionary<string, StatisticEntry> _statisticsDict = new Dictionary<string, StatisticEntry>();
    private Dictionary<string, float> _sessionStartValues = new Dictionary<string, float>();

    public static StatisticsTracker Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            if (_persistBetweenScenes)
            {
                DontDestroyOnLoad(gameObject);
            }
            InitializeStatistics();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        LoadStatistics();
        RecordSessionStartValues();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (!pauseStatus && _saveToPlayerPrefs)
        {
            SaveStatistics();
        }
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus && _saveToPlayerPrefs)
        {
            SaveStatistics();
        }
    }

    private void OnDestroy()
    {
        if (_saveToPlayerPrefs && Instance == this)
        {
            SaveStatistics();
        }
    }

    private void OnGUI()
    {
        if (!_showDebugInfo) return;

        GUILayout.BeginArea(new Rect(_debugPosition.x, _debugPosition.y, 300, 400));
        GUILayout.Label("Statistics Tracker", GUI.skin.box);
        
        foreach (var stat in _statistics)
        {
            string displayText = $"{stat.name}: {stat.value:F2}";
            if (stat.trackMaximum)
            {
                displayText += $" (Max: {stat.maxValue:F2})";
            }
            GUILayout.Label(displayText);
        }
        
        GUILayout.EndArea();
    }

    private void InitializeStatistics()
    {
        _statisticsDict.Clear();
        
        foreach (var stat in _statistics)
        {
            if (!_statisticsDict.ContainsKey(stat.name))
            {
                _statisticsDict[stat.name] = stat;
            }
        }

        // Add common default statistics if not already present
        AddDefaultStatistics();
    }

    private void AddDefaultStatistics()
    {
        string[] defaultStats = {
            "PlayTime", "Score", "Deaths", "Kills", "ItemsCollected",
            "JumpsPerformed", "DistanceTraveled", "DamageTaken", "DamageDealt"
        };

        foreach (string statName in defaultStats)
        {
            if (!_statisticsDict.ContainsKey(statName))
            {
                var newStat = new StatisticEntry(statName, 0f, true);
                _statistics.Add(newStat);
                _statisticsDict[statName] = newStat;
            }
        }
    }

    private void RecordSessionStartValues()
    {
        _sessionStartValues.Clear();
        foreach (var kvp in _statisticsDict)
        {
            _sessionStartValues[kvp.Key] = kvp.Value.value;
        }
    }

    public void AddStatistic(string statName, float initialValue = 0f, bool trackMaximum = false)
    {
        if (_statisticsDict.ContainsKey(statName)) return;

        var newStat = new StatisticEntry(statName, initialValue, trackMaximum);
        _statistics.Add(newStat);
        _statisticsDict[statName] = newStat;
    }

    public void SetStatistic(string statName, float value)
    {
        if (!_statisticsDict.ContainsKey(statName))
        {
            AddStatistic(statName, value);
            return;
        }

        var stat = _statisticsDict[statName];
        float oldValue = stat.value;
        stat.value = value;

        if (stat.trackMaximum && value > stat.maxValue)
        {
            stat.maxValue = value;
            OnStatisticMaxReached?.Invoke(statName, value);
        }

        if (Mathf.Abs(oldValue - value) > 0.001f)
        {
            OnStatisticChanged?.Invoke(statName, value);
        }
    }

    public void IncrementStatistic(string statName, float amount = 1f)
    {
        if (!_statisticsDict.ContainsKey(statName))
        {
            AddStatistic(statName, amount);
            return;
        }

        SetStatistic(statName, _statisticsDict[statName].value + amount);
    }

    public void DecrementStatistic(string statName, float amount = 1f)
    {
        IncrementStatistic(statName, -amount);
    }

    public float GetStatistic(string statName)
    {
        return _statisticsDict.ContainsKey(statName) ? _statisticsDict[statName].value : 0f;
    }

    public float GetMaxStatistic(string statName)
    {
        return _statisticsDict.ContainsKey(statName) ? _statisticsDict[statName].maxValue : 0f;
    }

    public float GetSessionChange(string statName)
    {
        if (!_statisticsDict.ContainsKey(statName) || !_sessionStartValues.ContainsKey(statName))
            return 0f;

        return _statisticsDict[statName].value - _sessionStartValues[statName];
    }

    public Dictionary<string, float> GetAllStatistics()
    {
        var result = new Dictionary<string, float>();
        foreach (var kvp in _statisticsDict)
        {
            result[kvp.Key] = kvp.Value.value;
        }
        return result;
    }

    public void ResetStatistic(string statName)
    {
        if (_statisticsDict.ContainsKey(statName))
        {
            _statisticsDict[statName].value = 0f;
            _statisticsDict[statName].maxValue = 0f;
            OnStatisticChanged?.Invoke(statName, 0f);
        }
    }

    public void ResetAllStatistics()
    {
        foreach (var stat in _statistics)
        {
            stat.value = 0f;
            stat.maxValue = 0f;
        }
        
        RecordSessionStartValues();
        OnStatisticsReset?.Invoke();
    }

    public void TrackPlayTime()
    {
        IncrementStatistic("PlayTime", Time.deltaTime);
    }

    private void Update()
    {
        TrackPlayTime();
    }

    public void SaveStatistics()
    {
        if (!_saveToPlayerPrefs) return;

        foreach (var kvp in _statisticsDict)
        {
            PlayerPrefs.SetFloat(_savePrefix + kvp.Key, kvp.Value.value);
            if (kvp.Value.trackMaximum)
            {
                PlayerPrefs.SetFloat(_savePrefix + kvp.Key + "_Max", kvp.Value.maxValue);
            }
        }
        PlayerPrefs.Save();
    }

    public void LoadStatistics()
    {
        if (!_saveToPlayerPrefs) return;

        foreach (var kvp in _statisticsDict)
        {
            if (PlayerPrefs.HasKey(_savePrefix + kvp.Key))
            {
                kvp.Value.value = PlayerPrefs.GetFloat(_savePrefix + kvp.Key, 0f);
            }
            
            if (kvp.Value.trackMaximum && PlayerPrefs.HasKey(_savePrefix + kvp.Key + "_Max"))
            {
                kvp.Value.maxValue = PlayerPrefs.GetFloat(_savePrefix + kvp.Key + "_Max", 0f);
            }
        }
    }

    public void DeleteSavedStatistics()
    {
        foreach (var kvp in _statisticsDict)
        {
            if (PlayerPrefs.HasKey(_savePrefix + kvp.Key))
            {
                PlayerPrefs.DeleteKey(_savePrefix + kvp.Key);
            }
            if (PlayerPrefs.HasKey(_savePrefix + kvp.Key + "_Max"))
            {
                PlayerPrefs.DeleteKey(_savePrefix + kvp.Key + "_Max");
            }
        }
        PlayerPrefs.Save();
    }

    // Event handlers for common game events
    public void OnPlayerDeath()
    {
        IncrementStatistic("Deaths");
    }

    public void OnEnemyKilled()
    {
        IncrementStatistic("Kills");
    }

    public void OnItemCollected()
    {
        IncrementStatistic("ItemsCollected");
    }

    public void OnJumpPerformed()
    {
        IncrementStatistic("JumpsPerformed");
    }

    public void OnDistanceTraveled(float distance)
    {
        IncrementStatistic("DistanceTraveled", distance);
    }

    public void OnDamageTaken(float damage)
    {
        IncrementStatistic("DamageTaken", damage);
    }

    public void OnDamageDealt(float damage)
    {
        IncrementStatistic("DamageDealt", damage);
    }

    public void OnScoreChanged(float newScore)
    {
        SetStatistic("Score", newScore);
    }
}