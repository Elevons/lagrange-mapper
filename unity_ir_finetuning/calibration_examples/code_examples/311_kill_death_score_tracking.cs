// Prompt: kill death score tracking
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class ScoreTracker : MonoBehaviour
{
    [Header("Score Settings")]
    [SerializeField] private int _killScore = 100;
    [SerializeField] private int _deathPenalty = 50;
    [SerializeField] private bool _allowNegativeScore = false;
    
    [Header("Score Display")]
    [SerializeField] private UnityEngine.UI.Text _scoreText;
    [SerializeField] private UnityEngine.UI.Text _killsText;
    [SerializeField] private UnityEngine.UI.Text _deathsText;
    [SerializeField] private string _scorePrefix = "Score: ";
    [SerializeField] private string _killsPrefix = "Kills: ";
    [SerializeField] private string _deathsPrefix = "Deaths: ";
    
    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnKillCountChanged;
    public UnityEvent<int> OnDeathCountChanged;
    public UnityEvent<PlayerStats> OnStatsUpdated;
    
    [Header("Persistence")]
    [SerializeField] private bool _saveToPlayerPrefs = true;
    [SerializeField] private string _scoreKey = "PlayerScore";
    [SerializeField] private string _killsKey = "PlayerKills";
    [SerializeField] private string _deathsKey = "PlayerDeaths";
    
    private int _currentScore;
    private int _totalKills;
    private int _totalDeaths;
    private Dictionary<string, int> _killsByType = new Dictionary<string, int>();
    
    public int CurrentScore => _currentScore;
    public int TotalKills => _totalKills;
    public int TotalDeaths => _totalDeaths;
    public float KillDeathRatio => _totalDeaths > 0 ? (float)_totalKills / _totalDeaths : _totalKills;
    
    [System.Serializable]
    public class PlayerStats
    {
        public int score;
        public int kills;
        public int deaths;
        public float killDeathRatio;
        
        public PlayerStats(int score, int kills, int deaths, float kdr)
        {
            this.score = score;
            this.kills = kills;
            this.deaths = deaths;
            this.killDeathRatio = kdr;
        }
    }
    
    private void Start()
    {
        LoadStats();
        UpdateUI();
    }
    
    private void OnEnable()
    {
        FindAndSubscribeToHealthComponents();
    }
    
    private void OnDisable()
    {
        UnsubscribeFromHealthComponents();
    }
    
    private void FindAndSubscribeToHealthComponents()
    {
        HealthComponent[] healthComponents = FindObjectsOfType<HealthComponent>();
        foreach (var health in healthComponents)
        {
            health.OnDeath.AddListener(OnEntityDeath);
        }
    }
    
    private void UnsubscribeFromHealthComponents()
    {
        HealthComponent[] healthComponents = FindObjectsOfType<HealthComponent>();
        foreach (var health in healthComponents)
        {
            if (health != null)
                health.OnDeath.RemoveListener(OnEntityDeath);
        }
    }
    
    public void RegisterKill(string killedEntityTag = "Enemy", string killerTag = "Player")
    {
        if (killerTag == "Player")
        {
            _totalKills++;
            _currentScore += _killScore;
            
            if (!_killsByType.ContainsKey(killedEntityTag))
                _killsByType[killedEntityTag] = 0;
            _killsByType[killedEntityTag]++;
            
            OnKillCountChanged?.Invoke(_totalKills);
            OnScoreChanged?.Invoke(_currentScore);
            
            UpdateUI();
            SaveStats();
            
            OnStatsUpdated?.Invoke(new PlayerStats(_currentScore, _totalKills, _totalDeaths, KillDeathRatio));
        }
    }
    
    public void RegisterDeath(string deadEntityTag = "Player")
    {
        if (deadEntityTag == "Player")
        {
            _totalDeaths++;
            _currentScore -= _deathPenalty;
            
            if (!_allowNegativeScore && _currentScore < 0)
                _currentScore = 0;
            
            OnDeathCountChanged?.Invoke(_totalDeaths);
            OnScoreChanged?.Invoke(_currentScore);
            
            UpdateUI();
            SaveStats();
            
            OnStatsUpdated?.Invoke(new PlayerStats(_currentScore, _totalKills, _totalDeaths, KillDeathRatio));
        }
    }
    
    private void OnEntityDeath(GameObject deadEntity, GameObject killer)
    {
        if (killer != null && killer.CompareTag("Player") && !deadEntity.CompareTag("Player"))
        {
            RegisterKill(deadEntity.tag, killer.tag);
        }
        else if (deadEntity.CompareTag("Player"))
        {
            RegisterDeath(deadEntity.tag);
        }
    }
    
    public void AddScore(int points)
    {
        _currentScore += points;
        if (!_allowNegativeScore && _currentScore < 0)
            _currentScore = 0;
        
        OnScoreChanged?.Invoke(_currentScore);
        UpdateUI();
        SaveStats();
    }
    
    public void ResetStats()
    {
        _currentScore = 0;
        _totalKills = 0;
        _totalDeaths = 0;
        _killsByType.Clear();
        
        OnScoreChanged?.Invoke(_currentScore);
        OnKillCountChanged?.Invoke(_totalKills);
        OnDeathCountChanged?.Invoke(_totalDeaths);
        
        UpdateUI();
        SaveStats();
        
        OnStatsUpdated?.Invoke(new PlayerStats(_currentScore, _totalKills, _totalDeaths, KillDeathRatio));
    }
    
    public int GetKillsByType(string entityType)
    {
        return _killsByType.ContainsKey(entityType) ? _killsByType[entityType] : 0;
    }
    
    private void UpdateUI()
    {
        if (_scoreText != null)
            _scoreText.text = _scorePrefix + _currentScore.ToString();
        
        if (_killsText != null)
            _killsText.text = _killsPrefix + _totalKills.ToString();
        
        if (_deathsText != null)
            _deathsText.text = _deathsPrefix + _totalDeaths.ToString();
    }
    
    private void SaveStats()
    {
        if (!_saveToPlayerPrefs) return;
        
        PlayerPrefs.SetInt(_scoreKey, _currentScore);
        PlayerPrefs.SetInt(_killsKey, _totalKills);
        PlayerPrefs.SetInt(_deathsKey, _totalDeaths);
        
        foreach (var kvp in _killsByType)
        {
            PlayerPrefs.SetInt("Kills_" + kvp.Key, kvp.Value);
        }
        
        PlayerPrefs.Save();
    }
    
    private void LoadStats()
    {
        if (!_saveToPlayerPrefs) return;
        
        _currentScore = PlayerPrefs.GetInt(_scoreKey, 0);
        _totalKills = PlayerPrefs.GetInt(_killsKey, 0);
        _totalDeaths = PlayerPrefs.GetInt(_deathsKey, 0);
        
        string[] enemyTypes = { "Enemy", "Boss", "Minion", "Elite" };
        foreach (string type in enemyTypes)
        {
            int kills = PlayerPrefs.GetInt("Kills_" + type, 0);
            if (kills > 0)
                _killsByType[type] = kills;
        }
    }
}

[System.Serializable]
public class HealthComponent : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _maxHealth = 100;
    [SerializeField] private int _currentHealth;
    [SerializeField] private bool _destroyOnDeath = true;
    
    [Header("Events")]
    public UnityEvent<int> OnHealthChanged;
    public UnityEvent<GameObject, GameObject> OnDeath;
    public UnityEvent<int> OnDamageTaken;
    
    public int MaxHealth => _maxHealth;
    public int CurrentHealth => _currentHealth;
    public bool IsDead => _currentHealth <= 0;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    public void TakeDamage(int damage, GameObject attacker = null)
    {
        if (IsDead) return;
        
        _currentHealth -= damage;
        _currentHealth = Mathf.Max(0, _currentHealth);
        
        OnDamageTaken?.Invoke(damage);
        OnHealthChanged?.Invoke(_currentHealth);
        
        if (_currentHealth <= 0)
        {
            Die(attacker);
        }
    }
    
    public void Heal(int amount)
    {
        if (IsDead) return;
        
        _currentHealth += amount;
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth);
        
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    private void Die(GameObject killer)
    {
        OnDeath?.Invoke(gameObject, killer);
        
        if (_destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Damage"))
        {
            DamageDealer dealer = other.GetComponent<DamageDealer>();
            if (dealer != null)
            {
                TakeDamage(dealer.Damage, dealer.Owner);
            }
        }
    }
}

[System.Serializable]
public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private int _damage = 10;
    [SerializeField] private GameObject _owner;
    [SerializeField] private bool _destroyOnHit = true;
    
    public int Damage => _damage;
    public GameObject Owner => _owner;
    
    public void SetOwner(GameObject owner)
    {
        _owner = owner;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        HealthComponent health = other.GetComponent<HealthComponent>();
        if (health != null && other.gameObject != _owner)
        {
            health.TakeDamage(_damage, _owner);
            
            if (_destroyOnHit)
            {
                Destroy(gameObject);
            }
        }
    }
}