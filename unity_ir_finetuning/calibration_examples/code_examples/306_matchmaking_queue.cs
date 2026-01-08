// Prompt: matchmaking queue
// Type: general

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class MatchmakingQueue : MonoBehaviour
{
    [System.Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public int skillRating;
        public float queueTime;
        public bool isReady;
        
        public PlayerData(string id, string name, int rating)
        {
            playerId = id;
            playerName = name;
            skillRating = rating;
            queueTime = 0f;
            isReady = false;
        }
    }
    
    [System.Serializable]
    public class Match
    {
        public List<PlayerData> players;
        public string matchId;
        public float averageSkillRating;
        
        public Match()
        {
            players = new List<PlayerData>();
            matchId = System.Guid.NewGuid().ToString();
        }
    }
    
    [System.Serializable]
    public class MatchmakingEvents
    {
        public UnityEvent<PlayerData> OnPlayerJoinedQueue;
        public UnityEvent<PlayerData> OnPlayerLeftQueue;
        public UnityEvent<Match> OnMatchFound;
        public UnityEvent<string> OnMatchmakingStatusChanged;
    }
    
    [Header("Queue Settings")]
    [SerializeField] private int _maxPlayersPerMatch = 4;
    [SerializeField] private int _minPlayersPerMatch = 2;
    [SerializeField] private float _maxSkillRatingDifference = 200f;
    [SerializeField] private float _queueTimeExpansionRate = 50f;
    [SerializeField] private float _maxQueueTime = 300f;
    
    [Header("Matchmaking Intervals")]
    [SerializeField] private float _matchmakingInterval = 2f;
    [SerializeField] private float _queueUpdateInterval = 1f;
    
    [Header("UI References")]
    [SerializeField] private Text _queueStatusText;
    [SerializeField] private Text _playersInQueueText;
    [SerializeField] private Text _estimatedWaitTimeText;
    [SerializeField] private Button _joinQueueButton;
    [SerializeField] private Button _leaveQueueButton;
    [SerializeField] private Slider _queueProgressSlider;
    
    [Header("Events")]
    [SerializeField] private MatchmakingEvents _events;
    
    private List<PlayerData> _queuedPlayers = new List<PlayerData>();
    private List<Match> _activeMatches = new List<Match>();
    private PlayerData _localPlayer;
    private bool _isInQueue = false;
    private bool _isMatchmakingActive = true;
    private Coroutine _matchmakingCoroutine;
    private Coroutine _queueUpdateCoroutine;
    
    public bool IsInQueue => _isInQueue;
    public int PlayersInQueue => _queuedPlayers.Count;
    public float LocalPlayerQueueTime => _localPlayer?.queueTime ?? 0f;
    
    private void Start()
    {
        InitializeUI();
        StartMatchmaking();
    }
    
    private void InitializeUI()
    {
        if (_joinQueueButton != null)
            _joinQueueButton.onClick.AddListener(JoinQueue);
            
        if (_leaveQueueButton != null)
            _leaveQueueButton.onClick.AddListener(LeaveQueue);
            
        UpdateUI();
    }
    
    private void StartMatchmaking()
    {
        if (_matchmakingCoroutine != null)
            StopCoroutine(_matchmakingCoroutine);
            
        if (_queueUpdateCoroutine != null)
            StopCoroutine(_queueUpdateCoroutine);
            
        _matchmakingCoroutine = StartCoroutine(MatchmakingLoop());
        _queueUpdateCoroutine = StartCoroutine(QueueUpdateLoop());
    }
    
    public void JoinQueue()
    {
        if (_isInQueue) return;
        
        string playerId = SystemInfo.deviceUniqueIdentifier;
        string playerName = "Player_" + UnityEngine.Random.Range(1000, 9999);
        int skillRating = UnityEngine.Random.Range(1000, 2000);
        
        JoinQueue(playerId, playerName, skillRating);
    }
    
    public void JoinQueue(string playerId, string playerName, int skillRating)
    {
        if (_isInQueue) return;
        
        _localPlayer = new PlayerData(playerId, playerName, skillRating);
        _queuedPlayers.Add(_localPlayer);
        _isInQueue = true;
        
        _events.OnPlayerJoinedQueue?.Invoke(_localPlayer);
        _events.OnMatchmakingStatusChanged?.Invoke("Joined queue");
        
        UpdateUI();
    }
    
    public void LeaveQueue()
    {
        if (!_isInQueue || _localPlayer == null) return;
        
        _queuedPlayers.Remove(_localPlayer);
        _events.OnPlayerLeftQueue?.Invoke(_localPlayer);
        _events.OnMatchmakingStatusChanged?.Invoke("Left queue");
        
        _localPlayer = null;
        _isInQueue = false;
        
        UpdateUI();
    }
    
    public void AddBotToQueue()
    {
        string botId = "Bot_" + System.Guid.NewGuid().ToString();
        string botName = "Bot_" + UnityEngine.Random.Range(100, 999);
        int botRating = UnityEngine.Random.Range(1000, 2000);
        
        PlayerData bot = new PlayerData(botId, botName, botRating);
        _queuedPlayers.Add(bot);
        
        _events.OnPlayerJoinedQueue?.Invoke(bot);
        UpdateUI();
    }
    
    private IEnumerator MatchmakingLoop()
    {
        while (_isMatchmakingActive)
        {
            if (_queuedPlayers.Count >= _minPlayersPerMatch)
            {
                TryCreateMatches();
            }
            
            yield return new WaitForSeconds(_matchmakingInterval);
        }
    }
    
    private IEnumerator QueueUpdateLoop()
    {
        while (_isMatchmakingActive)
        {
            UpdateQueueTimes();
            UpdateUI();
            
            yield return new WaitForSeconds(_queueUpdateInterval);
        }
    }
    
    private void UpdateQueueTimes()
    {
        for (int i = 0; i < _queuedPlayers.Count; i++)
        {
            _queuedPlayers[i].queueTime += _queueUpdateInterval;
            
            if (_queuedPlayers[i].queueTime > _maxQueueTime)
            {
                RemovePlayerFromQueue(_queuedPlayers[i]);
                i--;
            }
        }
    }
    
    private void TryCreateMatches()
    {
        List<PlayerData> availablePlayers = new List<PlayerData>(_queuedPlayers);
        availablePlayers.Sort((a, b) => a.queueTime.CompareTo(b.queueTime));
        
        while (availablePlayers.Count >= _minPlayersPerMatch)
        {
            Match match = CreateMatch(availablePlayers);
            if (match != null && match.players.Count >= _minPlayersPerMatch)
            {
                _activeMatches.Add(match);
                
                foreach (PlayerData player in match.players)
                {
                    _queuedPlayers.Remove(player);
                    availablePlayers.Remove(player);
                }
                
                _events.OnMatchFound?.Invoke(match);
                _events.OnMatchmakingStatusChanged?.Invoke($"Match found with {match.players.Count} players");
                
                if (_localPlayer != null && match.players.Contains(_localPlayer))
                {
                    _isInQueue = false;
                    _localPlayer = null;
                }
            }
            else
            {
                break;
            }
        }
    }
    
    private Match CreateMatch(List<PlayerData> availablePlayers)
    {
        if (availablePlayers.Count < _minPlayersPerMatch) return null;
        
        Match match = new Match();
        PlayerData anchor = availablePlayers[0];
        match.players.Add(anchor);
        availablePlayers.RemoveAt(0);
        
        float expandedSkillRange = _maxSkillRatingDifference + (anchor.queueTime * _queueTimeExpansionRate);
        
        for (int i = availablePlayers.Count - 1; i >= 0 && match.players.Count < _maxPlayersPerMatch; i--)
        {
            PlayerData candidate = availablePlayers[i];
            
            if (IsPlayerCompatible(match, candidate, expandedSkillRange))
            {
                match.players.Add(candidate);
                availablePlayers.RemoveAt(i);
            }
        }
        
        if (match.players.Count >= _minPlayersPerMatch)
        {
            CalculateMatchAverageRating(match);
            return match;
        }
        
        return null;
    }
    
    private bool IsPlayerCompatible(Match match, PlayerData candidate, float skillRange)
    {
        foreach (PlayerData player in match.players)
        {
            if (Mathf.Abs(player.skillRating - candidate.skillRating) > skillRange)
            {
                return false;
            }
        }
        return true;
    }
    
    private void CalculateMatchAverageRating(Match match)
    {
        float totalRating = 0f;
        foreach (PlayerData player in match.players)
        {
            totalRating += player.skillRating;
        }
        match.averageSkillRating = totalRating / match.players.Count;
    }
    
    private void RemovePlayerFromQueue(PlayerData player)
    {
        if (_queuedPlayers.Remove(player))
        {
            _events.OnPlayerLeftQueue?.Invoke(player);
            
            if (player == _localPlayer)
            {
                _isInQueue = false;
                _localPlayer = null;
                _events.OnMatchmakingStatusChanged?.Invoke("Removed from queue (timeout)");
            }
        }
    }
    
    private void UpdateUI()
    {
        if (_queueStatusText != null)
        {
            if (_isInQueue)
            {
                _queueStatusText.text = $"In Queue ({LocalPlayerQueueTime:F0}s)";
            }
            else
            {
                _queueStatusText.text = "Not in queue";
            }
        }
        
        if (_playersInQueueText != null)
        {
            _playersInQueueText.text = $"Players in queue: {PlayersInQueue}";
        }
        
        if (_estimatedWaitTimeText != null)
        {
            float estimatedWait = CalculateEstimatedWaitTime();
            _estimatedWaitTimeText.text = $"Estimated wait: {estimatedWait:F0}s";
        }
        
        if (_joinQueueButton != null)
        {
            _joinQueueButton.interactable = !_isInQueue;
        }
        
        if (_leaveQueueButton != null)
        {
            _leaveQueueButton.interactable = _isInQueue;
        }
        
        if (_queueProgressSlider != null)
        {
            if (_isInQueue && _localPlayer != null)
            {
                _queueProgressSlider.value = Mathf.Clamp01(_localPlayer.queueTime / _maxQueueTime);
            }
            else
            {
                _queueProgressSlider.value = 0f;
            }
        }
    }
    
    private float CalculateEstimatedWaitTime()
    {
        if (PlayersInQueue < _minPlayersPerMatch)
        {
            return 60f;
        }
        
        float averageQueueTime = 0f;
        foreach (PlayerData player in _queuedPlayers)
        {
            averageQueueTime += player.queueTime;
        }
        averageQueueTime /= _queuedPlayers.Count;
        
        return Mathf.Max(10f, 30f - (PlayersInQueue * 2f));
    }
    
    public void SetMatchmakingActive(bool active)
    {
        _isMatchmakingActive = active;
        
        if (active)
        {
            StartMatchmaking();
        }
        else
        {
            if (_matchmakingCoroutine != null)
                StopCoroutine(_matchmakingCoroutine);
            if (_queueUpdateCoroutine != null)
                StopCoroutine(_queueUpdateCoroutine);
        }
    }
    
    public List<PlayerData> GetQueuedPlayers()
    {
        return new List<PlayerData>(_queuedPlayers);
    }
    
    public List<Match> GetActiveMatches()
    {
        return new List<Match>(_activeMatches);
    }
    
    private void OnDestroy()
    {
        SetMatchmakingActive(false);
        
        if (_joinQueueButton != null)
            _joinQueueButton.onClick.RemoveAllListeners();
        if (_leaveQueueButton != null)
            _leaveQueueButton.onClick.RemoveAllListeners();
    }
}