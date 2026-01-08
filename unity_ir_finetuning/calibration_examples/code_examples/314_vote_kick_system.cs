// Prompt: vote kick system
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Collections;

public class VoteKickSystem : MonoBehaviour
{
    [System.Serializable]
    public class Player
    {
        public string playerName;
        public int playerId;
        public bool isConnected;
        public bool hasVoted;
        public bool votedYes;
        
        public Player(string name, int id)
        {
            playerName = name;
            playerId = id;
            isConnected = true;
            hasVoted = false;
            votedYes = false;
        }
    }
    
    [System.Serializable]
    public class VoteKickEvent : UnityEvent<string, bool> { }
    
    [Header("Vote Settings")]
    [SerializeField] private float _voteDuration = 30f;
    [SerializeField] private float _votePassThreshold = 0.6f;
    [SerializeField] private int _minimumPlayersRequired = 3;
    [SerializeField] private float _cooldownBetweenVotes = 60f;
    
    [Header("UI References")]
    [SerializeField] private GameObject _voteKickPanel;
    [SerializeField] private Text _voteTargetText;
    [SerializeField] private Text _voteTimerText;
    [SerializeField] private Text _voteCountText;
    [SerializeField] private Button _yesButton;
    [SerializeField] private Button _noButton;
    [SerializeField] private Button _initiateVoteButton;
    [SerializeField] private Dropdown _playerDropdown;
    [SerializeField] private Text _statusText;
    
    [Header("Events")]
    public VoteKickEvent OnVoteKickComplete;
    public UnityEvent OnVoteKickStarted;
    public UnityEvent OnVoteKickCancelled;
    
    private List<Player> _players = new List<Player>();
    private bool _voteInProgress = false;
    private float _voteTimer = 0f;
    private string _targetPlayerName = "";
    private int _targetPlayerId = -1;
    private int _yesVotes = 0;
    private int _noVotes = 0;
    private bool _isOnCooldown = false;
    private float _cooldownTimer = 0f;
    private int _localPlayerId = 0;
    
    private void Start()
    {
        InitializeUI();
        InitializePlayers();
        UpdatePlayerDropdown();
    }
    
    private void Update()
    {
        HandleVoteTimer();
        HandleCooldownTimer();
        UpdateUI();
    }
    
    private void InitializeUI()
    {
        if (_voteKickPanel != null)
            _voteKickPanel.SetActive(false);
            
        if (_yesButton != null)
            _yesButton.onClick.AddListener(() => CastVote(true));
            
        if (_noButton != null)
            _noButton.onClick.AddListener(() => CastVote(false));
            
        if (_initiateVoteButton != null)
            _initiateVoteButton.onClick.AddListener(InitiateVoteKick);
    }
    
    private void InitializePlayers()
    {
        _players.Clear();
        _players.Add(new Player("Player1", 0));
        _players.Add(new Player("Player2", 1));
        _players.Add(new Player("Player3", 2));
        _players.Add(new Player("Player4", 3));
        
        _localPlayerId = 0;
    }
    
    private void UpdatePlayerDropdown()
    {
        if (_playerDropdown == null) return;
        
        _playerDropdown.ClearOptions();
        List<string> playerNames = new List<string>();
        
        foreach (Player player in _players)
        {
            if (player.isConnected && player.playerId != _localPlayerId)
            {
                playerNames.Add(player.playerName);
            }
        }
        
        _playerDropdown.AddOptions(playerNames);
    }
    
    public void InitiateVoteKick()
    {
        if (_voteInProgress)
        {
            UpdateStatusText("Vote already in progress!");
            return;
        }
        
        if (_isOnCooldown)
        {
            UpdateStatusText($"Vote cooldown active: {_cooldownTimer:F0}s remaining");
            return;
        }
        
        if (GetConnectedPlayerCount() < _minimumPlayersRequired)
        {
            UpdateStatusText($"Need at least {_minimumPlayersRequired} players to vote");
            return;
        }
        
        if (_playerDropdown != null && _playerDropdown.options.Count > 0)
        {
            string selectedPlayerName = _playerDropdown.options[_playerDropdown.value].text;
            Player targetPlayer = GetPlayerByName(selectedPlayerName);
            
            if (targetPlayer != null)
            {
                StartVoteKick(targetPlayer.playerName, targetPlayer.playerId);
            }
        }
    }
    
    public void StartVoteKick(string targetName, int targetId)
    {
        if (_voteInProgress) return;
        
        _voteInProgress = true;
        _targetPlayerName = targetName;
        _targetPlayerId = targetId;
        _voteTimer = _voteDuration;
        _yesVotes = 0;
        _noVotes = 0;
        
        ResetPlayerVotes();
        
        if (_voteKickPanel != null)
            _voteKickPanel.SetActive(true);
            
        UpdateStatusText($"Vote to kick {targetName} started!");
        OnVoteKickStarted?.Invoke();
    }
    
    public void CastVote(bool voteYes)
    {
        if (!_voteInProgress) return;
        
        Player localPlayer = GetPlayerById(_localPlayerId);
        if (localPlayer == null || localPlayer.hasVoted) return;
        
        localPlayer.hasVoted = true;
        localPlayer.votedYes = voteYes;
        
        if (voteYes)
            _yesVotes++;
        else
            _noVotes++;
            
        UpdateStatusText($"Vote cast: {(voteYes ? "Yes" : "No")}");
        
        if (_yesButton != null)
            _yesButton.interactable = false;
        if (_noButton != null)
            _noButton.interactable = false;
            
        CheckVoteCompletion();
    }
    
    private void HandleVoteTimer()
    {
        if (!_voteInProgress) return;
        
        _voteTimer -= Time.deltaTime;
        
        if (_voteTimer <= 0f)
        {
            CompleteVote();
        }
    }
    
    private void HandleCooldownTimer()
    {
        if (!_isOnCooldown) return;
        
        _cooldownTimer -= Time.deltaTime;
        
        if (_cooldownTimer <= 0f)
        {
            _isOnCooldown = false;
            UpdateStatusText("Vote cooldown expired. You can initiate votes again.");
        }
    }
    
    private void CheckVoteCompletion()
    {
        int connectedPlayers = GetConnectedPlayerCount();
        int totalVotes = _yesVotes + _noVotes;
        
        if (totalVotes >= connectedPlayers - 1)
        {
            CompleteVote();
        }
        else
        {
            float requiredYesVotes = (connectedPlayers - 1) * _votePassThreshold;
            if (_yesVotes >= requiredYesVotes)
            {
                CompleteVote();
            }
        }
    }
    
    private void CompleteVote()
    {
        if (!_voteInProgress) return;
        
        int connectedPlayers = GetConnectedPlayerCount();
        float yesPercentage = (float)_yesVotes / (connectedPlayers - 1);
        bool votesPassed = yesPercentage >= _votePassThreshold;
        
        if (votesPassed)
        {
            UpdateStatusText($"{_targetPlayerName} has been kicked!");
            KickPlayer(_targetPlayerId);
        }
        else
        {
            UpdateStatusText($"Vote to kick {_targetPlayerName} failed.");
        }
        
        OnVoteKickComplete?.Invoke(_targetPlayerName, votesPassed);
        EndVote();
        StartCooldown();
    }
    
    private void KickPlayer(int playerId)
    {
        Player playerToKick = GetPlayerById(playerId);
        if (playerToKick != null)
        {
            playerToKick.isConnected = false;
            UpdatePlayerDropdown();
        }
    }
    
    private void EndVote()
    {
        _voteInProgress = false;
        _targetPlayerName = "";
        _targetPlayerId = -1;
        _yesVotes = 0;
        _noVotes = 0;
        
        if (_voteKickPanel != null)
            _voteKickPanel.SetActive(false);
            
        if (_yesButton != null)
            _yesButton.interactable = true;
        if (_noButton != null)
            _noButton.interactable = true;
            
        ResetPlayerVotes();
    }
    
    private void StartCooldown()
    {
        _isOnCooldown = true;
        _cooldownTimer = _cooldownBetweenVotes;
    }
    
    private void ResetPlayerVotes()
    {
        foreach (Player player in _players)
        {
            player.hasVoted = false;
            player.votedYes = false;
        }
    }
    
    private void UpdateUI()
    {
        if (_voteTargetText != null && _voteInProgress)
            _voteTargetText.text = $"Kick {_targetPlayerName}?";
            
        if (_voteTimerText != null && _voteInProgress)
            _voteTimerText.text = $"Time: {_voteTimer:F0}s";
            
        if (_voteCountText != null && _voteInProgress)
        {
            int totalEligibleVoters = GetConnectedPlayerCount() - 1;
            _voteCountText.text = $"Yes: {_yesVotes} | No: {_noVotes} | Total: {totalEligibleVoters}";
        }
        
        if (_initiateVoteButton != null)
        {
            _initiateVoteButton.interactable = !_voteInProgress && !_isOnCooldown && 
                                             GetConnectedPlayerCount() >= _minimumPlayersRequired;
        }
    }
    
    private void UpdateStatusText(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
            
        StartCoroutine(ClearStatusAfterDelay(3f));
    }
    
    private IEnumerator ClearStatusAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_statusText != null && !_voteInProgress)
            _statusText.text = "";
    }
    
    private Player GetPlayerById(int id)
    {
        return _players.Find(p => p.playerId == id);
    }
    
    private Player GetPlayerByName(string name)
    {
        return _players.Find(p => p.playerName == name);
    }
    
    private int GetConnectedPlayerCount()
    {
        int count = 0;
        foreach (Player player in _players)
        {
            if (player.isConnected)
                count++;
        }
        return count;
    }
    
    public void AddPlayer(string playerName, int playerId)
    {
        if (GetPlayerById(playerId) == null)
        {
            _players.Add(new Player(playerName, playerId));
            UpdatePlayerDropdown();
        }
    }
    
    public void RemovePlayer(int playerId)
    {
        Player player = GetPlayerById(playerId);
        if (player != null)
        {
            player.isConnected = false;
            UpdatePlayerDropdown();
            
            if (_voteInProgress && playerId == _targetPlayerId)
            {
                UpdateStatusText("Target player disconnected. Vote cancelled.");
                OnVoteKickCancelled?.Invoke();
                EndVote();
            }
        }
    }
    
    public void CancelCurrentVote()
    {
        if (_voteInProgress)
        {
            UpdateStatusText("Vote cancelled by administrator.");
            OnVoteKickCancelled?.Invoke();
            EndVote();
        }
    }
    
    public bool IsVoteInProgress()
    {
        return _voteInProgress;
    }
    
    public float GetRemainingVoteTime()
    {
        return _voteInProgress ? _voteTimer : 0f;
    }
    
    public bool IsOnCooldown()
    {
        return _isOnCooldown;
    }
    
    public float GetRemainingCooldownTime()
    {
        return _isOnCooldown ? _cooldownTimer : 0f;
    }
}