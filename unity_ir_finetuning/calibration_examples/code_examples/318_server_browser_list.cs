// Prompt: server browser list
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;

public class ServerBrowserList : MonoBehaviour
{
    [System.Serializable]
    public class ServerInfo
    {
        public string serverName;
        public string ipAddress;
        public int port;
        public int currentPlayers;
        public int maxPlayers;
        public int ping;
        public string gameMode;
        public string mapName;
        public bool isPasswordProtected;
        
        public ServerInfo(string name, string ip, int p, int current, int max, string mode, string map, bool password = false)
        {
            serverName = name;
            ipAddress = ip;
            port = p;
            currentPlayers = current;
            maxPlayers = max;
            ping = 0;
            gameMode = mode;
            mapName = map;
            isPasswordProtected = password;
        }
    }
    
    [System.Serializable]
    public class ServerSelectedEvent : UnityEvent<ServerInfo> { }
    
    [Header("UI References")]
    [SerializeField] private Transform _serverListParent;
    [SerializeField] private GameObject _serverItemPrefab;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private Button _joinButton;
    [SerializeField] private InputField _passwordInputField;
    [SerializeField] private GameObject _passwordPanel;
    [SerializeField] private Text _statusText;
    [SerializeField] private ScrollRect _scrollRect;
    
    [Header("Server Discovery")]
    [SerializeField] private int _broadcastPort = 7777;
    [SerializeField] private int _discoveryTimeout = 3000;
    [SerializeField] private string _discoveryMessage = "SERVER_DISCOVERY";
    
    [Header("Filters")]
    [SerializeField] private Toggle _showFullServersToggle;
    [SerializeField] private Toggle _showPasswordProtectedToggle;
    [SerializeField] private InputField _serverNameFilter;
    [SerializeField] private Dropdown _gameModeFilter;
    
    [Header("Events")]
    public ServerSelectedEvent OnServerSelected;
    public UnityEvent OnRefreshStarted;
    public UnityEvent OnRefreshCompleted;
    
    private List<ServerInfo> _allServers = new List<ServerInfo>();
    private List<ServerInfo> _filteredServers = new List<ServerInfo>();
    private List<GameObject> _serverItemObjects = new List<GameObject>();
    private ServerInfo _selectedServer;
    private UdpClient _udpClient;
    private Thread _discoveryThread;
    private bool _isRefreshing = false;
    private bool _shouldStopDiscovery = false;
    
    private void Start()
    {
        InitializeUI();
        AddDummyServers();
        RefreshServerList();
    }
    
    private void InitializeUI()
    {
        if (_refreshButton != null)
            _refreshButton.onClick.AddListener(RefreshServerList);
            
        if (_joinButton != null)
        {
            _joinButton.onClick.AddListener(JoinSelectedServer);
            _joinButton.interactable = false;
        }
        
        if (_showFullServersToggle != null)
            _showFullServersToggle.onValueChanged.AddListener(OnFilterChanged);
            
        if (_showPasswordProtectedToggle != null)
            _showPasswordProtectedToggle.onValueChanged.AddListener(OnFilterChanged);
            
        if (_serverNameFilter != null)
            _serverNameFilter.onValueChanged.AddListener(OnServerNameFilterChanged);
            
        if (_gameModeFilter != null)
            _gameModeFilter.onValueChanged.AddListener(OnGameModeFilterChanged);
            
        if (_passwordPanel != null)
            _passwordPanel.SetActive(false);
            
        UpdateStatusText("Ready");
    }
    
    private void AddDummyServers()
    {
        _allServers.Add(new ServerInfo("Official Server #1", "192.168.1.100", 7777, 12, 16, "Deathmatch", "Desert_Map"));
        _allServers.Add(new ServerInfo("Community Server", "192.168.1.101", 7778, 8, 12, "Team Deathmatch", "City_Map", true));
        _allServers.Add(new ServerInfo("Noob Friendly", "192.168.1.102", 7779, 4, 8, "Capture Flag", "Forest_Map"));
        _allServers.Add(new ServerInfo("Pro Players Only", "192.168.1.103", 7780, 16, 16, "Battle Royale", "Island_Map", true));
        _allServers.Add(new ServerInfo("Training Ground", "192.168.1.104", 7781, 2, 10, "Practice", "Training_Map"));
    }
    
    public void RefreshServerList()
    {
        if (_isRefreshing) return;
        
        _isRefreshing = true;
        OnRefreshStarted?.Invoke();
        UpdateStatusText("Searching for servers...");
        
        if (_refreshButton != null)
            _refreshButton.interactable = false;
            
        StartServerDiscovery();
    }
    
    private void StartServerDiscovery()
    {
        _shouldStopDiscovery = false;
        _discoveryThread = new Thread(DiscoverServers);
        _discoveryThread.Start();
        
        Invoke(nameof(StopServerDiscovery), _discoveryTimeout / 1000f);
    }
    
    private void DiscoverServers()
    {
        try
        {
            _udpClient = new UdpClient();
            _udpClient.EnableBroadcast = true;
            
            IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, _broadcastPort);
            byte[] data = Encoding.UTF8.GetBytes(_discoveryMessage);
            
            _udpClient.Send(data, data.Length, broadcastEndPoint);
            
            IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
            _udpClient.Client.ReceiveTimeout = 1000;
            
            while (!_shouldStopDiscovery)
            {
                try
                {
                    byte[] receivedData = _udpClient.Receive(ref remoteEndPoint);
                    string response = Encoding.UTF8.GetString(receivedData);
                    
                    ParseServerResponse(response, remoteEndPoint.Address.ToString());
                }
                catch (SocketException)
                {
                    // Timeout or other socket exception, continue listening
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Server discovery error: " + ex.Message);
        }
        finally
        {
            _udpClient?.Close();
        }
    }
    
    private void ParseServerResponse(string response, string ipAddress)
    {
        try
        {
            string[] parts = response.Split('|');
            if (parts.Length >= 6)
            {
                string serverName = parts[0];
                int port = int.Parse(parts[1]);
                int currentPlayers = int.Parse(parts[2]);
                int maxPlayers = int.Parse(parts[3]);
                string gameMode = parts[4];
                string mapName = parts[5];
                bool isPasswordProtected = parts.Length > 6 && bool.Parse(parts[6]);
                
                ServerInfo serverInfo = new ServerInfo(serverName, ipAddress, port, currentPlayers, maxPlayers, gameMode, mapName, isPasswordProtected);
                
                // Calculate ping (simplified)
                serverInfo.ping = UnityEngine.Random.Range(20, 200);
                
                lock (_allServers)
                {
                    _allServers.Add(serverInfo);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Error parsing server response: " + ex.Message);
        }
    }
    
    private void StopServerDiscovery()
    {
        _shouldStopDiscovery = true;
        
        if (_discoveryThread != null && _discoveryThread.IsAlive)
        {
            _discoveryThread.Join(1000);
        }
        
        _isRefreshing = false;
        OnRefreshCompleted?.Invoke();
        
        if (_refreshButton != null)
            _refreshButton.interactable = true;
            
        ApplyFilters();
        UpdateServerListUI();
        UpdateStatusText($"Found {_filteredServers.Count} servers");
    }
    
    private void ApplyFilters()
    {
        _filteredServers.Clear();
        
        foreach (ServerInfo server in _allServers)
        {
            bool passesFilter = true;
            
            // Show full servers filter
            if (_showFullServersToggle != null && !_showFullServersToggle.isOn)
            {
                if (server.currentPlayers >= server.maxPlayers)
                    passesFilter = false;
            }
            
            // Show password protected filter
            if (_showPasswordProtectedToggle != null && !_showPasswordProtectedToggle.isOn)
            {
                if (server.isPasswordProtected)
                    passesFilter = false;
            }
            
            // Server name filter
            if (_serverNameFilter != null && !string.IsNullOrEmpty(_serverNameFilter.text))
            {
                if (!server.serverName.ToLower().Contains(_serverNameFilter.text.ToLower()))
                    passesFilter = false;
            }
            
            // Game mode filter
            if (_gameModeFilter != null && _gameModeFilter.value > 0)
            {
                string selectedMode = _gameModeFilter.options[_gameModeFilter.value].text;
                if (server.gameMode != selectedMode)
                    passesFilter = false;
            }
            
            if (passesFilter)
                _filteredServers.Add(server);
        }
        
        // Sort by ping
        _filteredServers.Sort((a, b) => a.ping.CompareTo(b.ping));
    }
    
    private void UpdateServerListUI()
    {
        ClearServerList();
        
        foreach (ServerInfo server in _filteredServers)
        {
            CreateServerItem(server);
        }
        
        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = 1f;
    }
    
    private void ClearServerList()
    {
        foreach (GameObject item in _serverItemObjects)
        {
            if (item != null)
                DestroyImmediate(item);
        }
        _serverItemObjects.Clear();
    }
    
    private void CreateServerItem(ServerInfo server)
    {
        if (_serverItemPrefab == null || _serverListParent == null) return;
        
        GameObject serverItem = Instantiate(_serverItemPrefab, _serverListParent);
        _serverItemObjects.Add(serverItem);
        
        ServerListItem itemComponent = serverItem.GetComponent<ServerListItem>();
        if (itemComponent == null)
            itemComponent = serverItem.AddComponent<ServerListItem>();
            
        itemComponent.Initialize(server, OnServerItemClicked);
    }
    
    private void OnServerItemClicked(ServerInfo server)
    {
        _selectedServer = server;
        
        // Update selection visual
        foreach (GameObject item in _serverItemObjects)
        {
            ServerListItem itemComponent = item.GetComponent<ServerListItem>();
            if (itemComponent != null)
            {
                itemComponent.SetSelected(itemComponent.ServerInfo == server);
            }
        }
        
        if (_joinButton != null)
            _joinButton.interactable = true;
            
        OnServerSelected?.Invoke(server);
    }
    
    private void JoinSelectedServer()
    {
        if (_selectedServer == null) return;
        
        if (_selectedServer.isPasswordProtected)
        {
            ShowPasswordPanel();
        }
        else
        {
            ConnectToServer(_selectedServer, "");
        }
    }
    
    private void ShowPasswordPanel()
    {
        if (_passwordPanel != null)
        {
            _passwordPanel.SetActive(true);
            
            Button confirmButton = _passwordPanel.GetComponentInChildren<Button>();
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveAllListeners();
                confirmButton.onClick.AddListener(() => {
                    string password = _passwordInputField != null ? _passwordInputField.text : "";
                    ConnectToServer(_selectedServer, password);
                    _passwordPanel.SetActive(false);
                });
            }
        }
    }
    
    private void ConnectToServer(ServerInfo server, string password)
    {
        UpdateStatusText($"Connecting to {server.serverName}...");
        Debug.Log($"Connecting to server: {server.serverName} at {server.ipAddress}:{server.port}");
        
        // Here you would implement actual connection logic
        // For now, just simulate connection
        Invoke(nameof(SimulateConnectionResult), 2f);
    }
    
    private void SimulateConnectionResult()
    {
        bool success = UnityEngine.Random.Range(0f, 1f) > 0.2f; // 80% success rate
        
        if (success)
        {
            UpdateStatusText("Connected successfully!");
            Debug.Log("Successfully connected to server");
        }
        else
        {
            UpdateStatusText("Connection failed");
            Debug.LogError("Failed to connect to server");
        }
    }
    
    private void OnFilterChanged(bool value)
    {
        ApplyFilters();
        UpdateServerListUI();
        UpdateStatusText($"Found {_filteredServers.Count} servers");
    }
    
    private void OnServerNameFilterChanged(string value)
    {
        ApplyFilters();
        UpdateServerListUI();
        UpdateStatusText($"Found {_filteredServers.Count} servers");
    }
    
    private void OnGameModeFilterChanged(int value)
    {
        ApplyFilters();
        UpdateServerListUI();
        UpdateStatusText($"Found {_filteredServers.Count} servers");
    }
    
    private void UpdateStatusText(string message)
    {
        if (_statusText != null)
            _statusText.text = message;
    }
    
    private void OnDestroy()
    {
        _shouldStopDiscovery = true;
        
        if (_discoveryThread != null && _discoveryThread.IsAlive)
        {
            _discoveryThread.Join(1000);
        }
        
        _udpClient?.Close();
    }
}

public class ServerListItem : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Text _serverNameText;
    [SerializeField] private Text _playersText;
    [SerializeField] private Text _pingText;
    [SerializeField] private Text _gameModeText;
    [SerializeField] private Text _mapNameText;
    [SerializeField] private GameObject _passwordIcon;
    [SerializeField] private Image _backgroundImage;
    [SerializeField] private Button _itemButton;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _selectedColor = Color.cyan;
    [SerializeField] private Color _fullServerColor = Color.red;
    
    public ServerBrowserList.ServerInfo ServerInfo { get; private set; }
    private System.Action<ServerBrowserList.ServerInfo> _onClick