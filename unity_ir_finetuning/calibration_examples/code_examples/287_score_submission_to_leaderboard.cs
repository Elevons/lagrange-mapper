// Prompt: score submission to leaderboard
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;
using TMPro;

public class LeaderboardManager : MonoBehaviour
{
    [System.Serializable]
    public class LeaderboardEntry
    {
        public string playerName;
        public int score;
        public string date;
        
        public LeaderboardEntry(string name, int playerScore)
        {
            playerName = name;
            score = playerScore;
            date = System.DateTime.Now.ToString("MM/dd/yyyy");
        }
    }
    
    [System.Serializable]
    public class ScoreSubmissionEvent : UnityEvent<string, int> { }
    
    [Header("UI References")]
    [SerializeField] private GameObject _leaderboardPanel;
    [SerializeField] private GameObject _scoreSubmissionPanel;
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private Button _submitButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private Transform _leaderboardContent;
    [SerializeField] private GameObject _leaderboardEntryPrefab;
    [SerializeField] private TextMeshProUGUI _currentScoreText;
    
    [Header("Settings")]
    [SerializeField] private int _maxLeaderboardEntries = 10;
    [SerializeField] private string _defaultPlayerName = "Anonymous";
    [SerializeField] private bool _saveToPlayerPrefs = true;
    [SerializeField] private string _leaderboardKey = "Leaderboard";
    
    [Header("Events")]
    public ScoreSubmissionEvent OnScoreSubmitted;
    public UnityEvent OnLeaderboardUpdated;
    
    private List<LeaderboardEntry> _leaderboardEntries = new List<LeaderboardEntry>();
    private int _currentScore = 0;
    private bool _isSubmissionActive = false;
    
    private void Start()
    {
        InitializeUI();
        LoadLeaderboard();
        UpdateLeaderboardDisplay();
    }
    
    private void InitializeUI()
    {
        if (_submitButton != null)
            _submitButton.onClick.AddListener(SubmitScore);
            
        if (_cancelButton != null)
            _cancelButton.onClick.AddListener(CancelSubmission);
            
        if (_playerNameInput != null)
        {
            _playerNameInput.text = _defaultPlayerName;
            _playerNameInput.onEndEdit.AddListener(OnNameInputChanged);
        }
        
        if (_scoreSubmissionPanel != null)
            _scoreSubmissionPanel.SetActive(false);
            
        if (_leaderboardPanel != null)
            _leaderboardPanel.SetActive(false);
    }
    
    public void ShowScoreSubmission(int score)
    {
        if (_isSubmissionActive) return;
        
        _currentScore = score;
        _isSubmissionActive = true;
        
        if (_currentScoreText != null)
            _currentScoreText.text = "Score: " + score.ToString();
            
        if (_scoreSubmissionPanel != null)
            _scoreSubmissionPanel.SetActive(true);
            
        if (_playerNameInput != null)
        {
            _playerNameInput.text = GetLastUsedPlayerName();
            _playerNameInput.Select();
            _playerNameInput.ActivateInputField();
        }
        
        UpdateSubmitButtonState();
    }
    
    public void ShowLeaderboard()
    {
        if (_leaderboardPanel != null)
            _leaderboardPanel.SetActive(true);
            
        UpdateLeaderboardDisplay();
    }
    
    public void HideLeaderboard()
    {
        if (_leaderboardPanel != null)
            _leaderboardPanel.SetActive(false);
    }
    
    private void SubmitScore()
    {
        if (!_isSubmissionActive || _playerNameInput == null) return;
        
        string playerName = _playerNameInput.text.Trim();
        if (string.IsNullOrEmpty(playerName))
            playerName = _defaultPlayerName;
            
        AddScoreToLeaderboard(playerName, _currentScore);
        SaveLastUsedPlayerName(playerName);
        
        OnScoreSubmitted?.Invoke(playerName, _currentScore);
        
        CancelSubmission();
        ShowLeaderboard();
    }
    
    private void CancelSubmission()
    {
        _isSubmissionActive = false;
        _currentScore = 0;
        
        if (_scoreSubmissionPanel != null)
            _scoreSubmissionPanel.SetActive(false);
    }
    
    private void OnNameInputChanged(string newName)
    {
        UpdateSubmitButtonState();
    }
    
    private void UpdateSubmitButtonState()
    {
        if (_submitButton == null || _playerNameInput == null) return;
        
        bool canSubmit = !string.IsNullOrEmpty(_playerNameInput.text.Trim()) || 
                        !string.IsNullOrEmpty(_defaultPlayerName);
        _submitButton.interactable = canSubmit;
    }
    
    public void AddScoreToLeaderboard(string playerName, int score)
    {
        LeaderboardEntry newEntry = new LeaderboardEntry(playerName, score);
        _leaderboardEntries.Add(newEntry);
        
        _leaderboardEntries = _leaderboardEntries
            .OrderByDescending(entry => entry.score)
            .Take(_maxLeaderboardEntries)
            .ToList();
            
        SaveLeaderboard();
        UpdateLeaderboardDisplay();
        OnLeaderboardUpdated?.Invoke();
    }
    
    public bool IsHighScore(int score)
    {
        if (_leaderboardEntries.Count < _maxLeaderboardEntries)
            return true;
            
        return score > _leaderboardEntries.Last().score;
    }
    
    public int GetHighScore()
    {
        if (_leaderboardEntries.Count == 0) return 0;
        return _leaderboardEntries.First().score;
    }
    
    public List<LeaderboardEntry> GetTopScores(int count = -1)
    {
        if (count == -1) count = _maxLeaderboardEntries;
        return _leaderboardEntries.Take(count).ToList();
    }
    
    private void UpdateLeaderboardDisplay()
    {
        if (_leaderboardContent == null || _leaderboardEntryPrefab == null) return;
        
        foreach (Transform child in _leaderboardContent)
        {
            if (child.gameObject != _leaderboardEntryPrefab)
                DestroyImmediate(child.gameObject);
        }
        
        for (int i = 0; i < _leaderboardEntries.Count; i++)
        {
            GameObject entryObj = Instantiate(_leaderboardEntryPrefab, _leaderboardContent);
            entryObj.SetActive(true);
            
            LeaderboardEntryDisplay display = entryObj.GetComponent<LeaderboardEntryDisplay>();
            if (display == null)
                display = entryObj.AddComponent<LeaderboardEntryDisplay>();
                
            display.SetupEntry(i + 1, _leaderboardEntries[i]);
        }
    }
    
    private void SaveLeaderboard()
    {
        if (!_saveToPlayerPrefs) return;
        
        string jsonData = JsonUtility.ToJson(new SerializableLeaderboard(_leaderboardEntries));
        PlayerPrefs.SetString(_leaderboardKey, jsonData);
        PlayerPrefs.Save();
    }
    
    private void LoadLeaderboard()
    {
        if (!_saveToPlayerPrefs) return;
        
        if (PlayerPrefs.HasKey(_leaderboardKey))
        {
            string jsonData = PlayerPrefs.GetString(_leaderboardKey);
            try
            {
                SerializableLeaderboard loadedData = JsonUtility.FromJson<SerializableLeaderboard>(jsonData);
                _leaderboardEntries = loadedData.entries ?? new List<LeaderboardEntry>();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Failed to load leaderboard: " + e.Message);
                _leaderboardEntries = new List<LeaderboardEntry>();
            }
        }
    }
    
    private void SaveLastUsedPlayerName(string playerName)
    {
        PlayerPrefs.SetString("LastPlayerName", playerName);
    }
    
    private string GetLastUsedPlayerName()
    {
        return PlayerPrefs.GetString("LastPlayerName", _defaultPlayerName);
    }
    
    public void ClearLeaderboard()
    {
        _leaderboardEntries.Clear();
        SaveLeaderboard();
        UpdateLeaderboardDisplay();
        OnLeaderboardUpdated?.Invoke();
    }
    
    [System.Serializable]
    private class SerializableLeaderboard
    {
        public List<LeaderboardEntry> entries;
        
        public SerializableLeaderboard(List<LeaderboardEntry> leaderboardEntries)
        {
            entries = leaderboardEntries;
        }
    }
}

public class LeaderboardEntryDisplay : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private TextMeshProUGUI _rankText;
    [SerializeField] private TextMeshProUGUI _nameText;
    [SerializeField] private TextMeshProUGUI _scoreText;
    [SerializeField] private TextMeshProUGUI _dateText;
    [SerializeField] private Image _backgroundImage;
    
    [Header("Styling")]
    [SerializeField] private Color _topRankColor = Color.yellow;
    [SerializeField] private Color _normalColor = Color.white;
    
    public void SetupEntry(int rank, LeaderboardManager.LeaderboardEntry entry)
    {
        if (_rankText != null)
            _rankText.text = rank.ToString();
            
        if (_nameText != null)
            _nameText.text = entry.playerName;
            
        if (_scoreText != null)
            _scoreText.text = entry.score.ToString();
            
        if (_dateText != null)
            _dateText.text = entry.date;
            
        if (_backgroundImage != null)
        {
            _backgroundImage.color = rank == 1 ? _topRankColor : _normalColor;
        }
    }
}