// Prompt: host migration handling
// Type: general

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

public class HostMigrationManager : MonoBehaviour
{
    [System.Serializable]
    public class HostMigrationEvent : UnityEvent<bool> { }
    
    [System.Serializable]
    public class PlayerData
    {
        public string playerId;
        public string playerName;
        public Vector3 position;
        public Quaternion rotation;
        public float health;
        public int score;
        public bool isActive;
        
        public PlayerData(string id, string name, Vector3 pos, Quaternion rot, float hp, int sc)
        {
            playerId = id;
            playerName = name;
            position = pos;
            rotation = rot;
            health = hp;
            score = sc;
            isActive = true;
        }
    }
    
    [System.Serializable]
    public class GameStateData
    {
        public float gameTime;
        public int gamePhase;
        public List<PlayerData> players;
        public Dictionary<string, object> customData;
        
        public GameStateData()
        {
            players = new List<PlayerData>();
            customData = new Dictionary<string, object>();
        }
    }
    
    [Header("Host Migration Settings")]
    [SerializeField] private float _migrationTimeout = 10f;
    [SerializeField] private float _heartbeatInterval = 2f;
    [SerializeField] private int _maxMigrationAttempts = 3;
    [SerializeField] private bool _enableAutomaticMigration = true;
    
    [Header("Events")]
    public HostMigrationEvent OnHostMigrationStarted;
    public HostMigrationEvent OnHostMigrationCompleted;
    public UnityEvent OnHostDisconnected;
    public UnityEvent OnBecameNewHost;
    public UnityEvent OnMigrationFailed;
    
    private bool _isHost;
    private bool _isMigrating;
    private string _currentHostId;
    private List<string> _connectedClients;
    private GameStateData _gameState;
    private Coroutine _heartbeatCoroutine;
    private Coroutine _migrationCoroutine;
    private float _lastHeartbeatTime;
    private int _migrationAttempts;
    private Dictionary<string, float> _clientPriorities;
    
    private void Start()
    {
        InitializeHostMigration();
    }
    
    private void Update()
    {
        if (!_isMigrating)
        {
            CheckHostConnection();
            UpdateGameState();
        }
    }
    
    private void InitializeHostMigration()
    {
        _connectedClients = new List<string>();
        _gameState = new GameStateData();
        _clientPriorities = new Dictionary<string, float>();
        _lastHeartbeatTime = Time.time;
        _migrationAttempts = 0;
        
        DetermineHostStatus();
        
        if (_isHost)
        {
            StartHostHeartbeat();
        }
    }
    
    private void DetermineHostStatus()
    {
        _isHost = Network.isServer || (Network.connections.Length == 0);
        _currentHostId = _isHost ? Network.player.ToString() : "";
        
        if (_isHost)
        {
            Debug.Log("This client is the host");
        }
    }
    
    private void StartHostHeartbeat()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
        }
        _heartbeatCoroutine = StartCoroutine(HostHeartbeatCoroutine());
    }
    
    private IEnumerator HostHeartbeatCoroutine()
    {
        while (_isHost && !_isMigrating)
        {
            SendHeartbeat();
            yield return new WaitForSeconds(_heartbeatInterval);
        }
    }
    
    private void SendHeartbeat()
    {
        if (!_isHost) return;
        
        // Simulate sending heartbeat to all clients
        foreach (string clientId in _connectedClients)
        {
            // In a real implementation, this would send network messages
            Debug.Log($"Sending heartbeat to client: {clientId}");
        }
        
        _lastHeartbeatTime = Time.time;
    }
    
    private void CheckHostConnection()
    {
        if (_isHost) return;
        
        float timeSinceLastHeartbeat = Time.time - _lastHeartbeatTime;
        
        if (timeSinceLastHeartbeat > _migrationTimeout && !_isMigrating)
        {
            Debug.Log("Host connection lost, initiating migration");
            OnHostDisconnected?.Invoke();
            
            if (_enableAutomaticMigration)
            {
                StartHostMigration();
            }
        }
    }
    
    public void StartHostMigration()
    {
        if (_isMigrating) return;
        
        _isMigrating = true;
        _migrationAttempts++;
        
        Debug.Log($"Starting host migration (attempt {_migrationAttempts})");
        OnHostMigrationStarted?.Invoke(true);
        
        if (_migrationCoroutine != null)
        {
            StopCoroutine(_migrationCoroutine);
        }
        _migrationCoroutine = StartCoroutine(HostMigrationCoroutine());
    }
    
    private IEnumerator HostMigrationCoroutine()
    {
        yield return new WaitForSeconds(1f); // Brief pause for network stabilization
        
        // Collect game state from all clients
        yield return StartCoroutine(CollectGameStateCoroutine());
        
        // Determine new host based on priority
        string newHostId = DetermineNewHost();
        
        if (newHostId == Network.player.ToString())
        {
            // This client becomes the new host
            yield return StartCoroutine(BecomeNewHostCoroutine());
        }
        else
        {
            // Wait for new host to be established
            yield return StartCoroutine(WaitForNewHostCoroutine(newHostId));
        }
        
        CompleteMigration();
    }
    
    private IEnumerator CollectGameStateCoroutine()
    {
        Debug.Log("Collecting game state from all clients");
        
        // Simulate collecting game state
        _gameState.gameTime = Time.time;
        _gameState.gamePhase = 1;
        
        // Collect player data
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        _gameState.players.Clear();
        
        foreach (GameObject player in players)
        {
            if (player != null)
            {
                PlayerData playerData = new PlayerData(
                    player.name,
                    player.name,
                    player.transform.position,
                    player.transform.rotation,
                    100f, // Default health
                    0     // Default score
                );
                _gameState.players.Add(playerData);
            }
        }
        
        yield return new WaitForSeconds(0.5f);
    }
    
    private string DetermineNewHost()
    {
        string newHostId = "";
        float highestPriority = -1f;
        
        // Calculate priorities for all connected clients
        foreach (string clientId in _connectedClients)
        {
            float priority = CalculateClientPriority(clientId);
            _clientPriorities[clientId] = priority;
            
            if (priority > highestPriority)
            {
                highestPriority = priority;
                newHostId = clientId;
            }
        }
        
        // Include self in consideration
        string selfId = Network.player.ToString();
        float selfPriority = CalculateClientPriority(selfId);
        
        if (selfPriority > highestPriority)
        {
            newHostId = selfId;
        }
        
        Debug.Log($"New host determined: {newHostId} with priority: {highestPriority}");
        return newHostId;
    }
    
    private float CalculateClientPriority(string clientId)
    {
        float priority = 0f;
        
        // Base priority on connection stability
        priority += Random.Range(0.5f, 1f);
        
        // Bonus for lower ping (simulated)
        priority += Random.Range(0f, 0.3f);
        
        // Bonus for being the original host
        if (clientId == _currentHostId)
        {
            priority += 0.2f;
        }
        
        return priority;
    }
    
    private IEnumerator BecomeNewHostCoroutine()
    {
        Debug.Log("Becoming new host");
        
        _isHost = true;
        _currentHostId = Network.player.ToString();
        
        // Restore game state
        yield return StartCoroutine(RestoreGameStateCoroutine());
        
        // Notify all clients of new host
        NotifyClientsOfNewHost();
        
        // Start host responsibilities
        StartHostHeartbeat();
        
        OnBecameNewHost?.Invoke();
        yield return null;
    }
    
    private IEnumerator WaitForNewHostCoroutine(string newHostId)
    {
        Debug.Log($"Waiting for new host: {newHostId}");
        
        float waitTime = 0f;
        bool hostEstablished = false;
        
        while (waitTime < _migrationTimeout && !hostEstablished)
        {
            // Check if new host is responding
            hostEstablished = CheckNewHostResponse(newHostId);
            
            if (!hostEstablished)
            {
                yield return new WaitForSeconds(0.5f);
                waitTime += 0.5f;
            }
        }
        
        if (!hostEstablished)
        {
            Debug.Log("New host failed to establish, retrying migration");
            if (_migrationAttempts < _maxMigrationAttempts)
            {
                yield return new WaitForSeconds(1f);
                StartHostMigration();
            }
            else
            {
                OnMigrationFailed?.Invoke();
            }
        }
        else
        {
            _currentHostId = newHostId;
            _lastHeartbeatTime = Time.time;
        }
    }
    
    private bool CheckNewHostResponse(string hostId)
    {
        // Simulate checking for new host response
        return Random.Range(0f, 1f) > 0.3f; // 70% chance of success
    }
    
    private IEnumerator RestoreGameStateCoroutine()
    {
        Debug.Log("Restoring game state");
        
        // Restore player positions and data
        foreach (PlayerData playerData in _gameState.players)
        {
            GameObject player = GameObject.Find(playerData.playerId);
            if (player != null)
            {
                player.transform.position = playerData.position;
                player.transform.rotation = playerData.rotation;
            }
        }
        
        yield return new WaitForSeconds(0.2f);
    }
    
    private void NotifyClientsOfNewHost()
    {
        Debug.Log("Notifying clients of new host");
        
        // In a real implementation, this would send network messages
        foreach (string clientId in _connectedClients)
        {
            Debug.Log($"Notifying client {clientId} of new host");
        }
    }
    
    private void CompleteMigration()
    {
        _isMigrating = false;
        _migrationAttempts = 0;
        
        Debug.Log("Host migration completed successfully");
        OnHostMigrationCompleted?.Invoke(true);
    }
    
    private void UpdateGameState()
    {
        if (!_isHost) return;
        
        _gameState.gameTime = Time.time;
        
        // Update player data
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        _gameState.players.Clear();
        
        foreach (GameObject player in players)
        {
            if (player != null)
            {
                PlayerData playerData = new PlayerData(
                    player.name,
                    player.name,
                    player.transform.position,
                    player.transform.rotation,
                    100f,
                    0
                );
                _gameState.players.Add(playerData);
            }
        }
    }
    
    public void AddClient(string clientId)
    {
        if (!_connectedClients.Contains(clientId))
        {
            _connectedClients.Add(clientId);
            Debug.Log($"Client added: {clientId}");
        }
    }
    
    public void RemoveClient(string clientId)
    {
        if (_connectedClients.Contains(clientId))
        {
            _connectedClients.Remove(clientId);
            Debug.Log($"Client removed: {clientId}");
            
            if (clientId == _currentHostId && !_isMigrating)
            {
                OnHostDisconnected?.Invoke();
                if (_enableAutomaticMigration)
                {
                    StartHostMigration();
                }
            }
        }
    }
    
    public void ForceHostMigration()
    {
        if (!_isMigrating)
        {
            StartHostMigration();
        }
    }
    
    public bool IsHost()
    {
        return _isHost;
    }
    
    public bool IsMigrating()
    {
        return _isMigrating;
    }
    
    public string GetCurrentHostId()
    {
        return _currentHostId;
    }
    
    public GameStateData GetGameState()
    {
        return _gameState;
    }
    
    private void OnDestroy()
    {
        if (_heartbeatCoroutine != null)
        {
            StopCoroutine(_heartbeatCoroutine);
        }
        
        if (_migrationCoroutine != null)
        {
            StopCoroutine(_migrationCoroutine);
        }
    }
}