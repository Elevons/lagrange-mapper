// Prompt: lobby room system
// Type: general

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class LobbyRoomSystem : MonoBehaviour
{
    [System.Serializable]
    public class Room
    {
        public string roomName;
        public string roomId;
        public int currentPlayers;
        public int maxPlayers;
        public bool isPrivate;
        public string password;
        public bool isInGame;
        public List<string> playerNames;

        public Room(string name, string id, int maxPlayers, bool isPrivate = false, string password = "")
        {
            this.roomName = name;
            this.roomId = id;
            this.maxPlayers = maxPlayers;
            this.isPrivate = isPrivate;
            this.password = password;
            this.currentPlayers = 0;
            this.isInGame = false;
            this.playerNames = new List<string>();
        }

        public bool CanJoin()
        {
            return currentPlayers < maxPlayers && !isInGame;
        }

        public bool AddPlayer(string playerName)
        {
            if (CanJoin() && !playerNames.Contains(playerName))
            {
                playerNames.Add(playerName);
                currentPlayers++;
                return true;
            }
            return false;
        }

        public bool RemovePlayer(string playerName)
        {
            if (playerNames.Contains(playerName))
            {
                playerNames.Remove(playerName);
                currentPlayers--;
                return true;
            }
            return false;
        }
    }

    [System.Serializable]
    public class RoomEvents
    {
        public UnityEvent<Room> OnRoomCreated;
        public UnityEvent<Room> OnRoomJoined;
        public UnityEvent<Room> OnRoomLeft;
        public UnityEvent<Room> OnRoomUpdated;
        public UnityEvent<string> OnRoomDeleted;
        public UnityEvent<string> OnError;
    }

    [Header("Room Settings")]
    [SerializeField] private int _maxRoomsCount = 50;
    [SerializeField] private int _defaultMaxPlayers = 4;
    [SerializeField] private float _roomUpdateInterval = 1f;

    [Header("UI References")]
    [SerializeField] private Transform _roomListParent;
    [SerializeField] private GameObject _roomItemPrefab;
    [SerializeField] private Button _createRoomButton;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private InputField _roomNameInput;
    [SerializeField] private InputField _maxPlayersInput;
    [SerializeField] private Toggle _privateRoomToggle;
    [SerializeField] private InputField _passwordInput;
    [SerializeField] private GameObject _createRoomPanel;
    [SerializeField] private GameObject _passwordPanel;
    [SerializeField] private InputField _passwordEntryInput;
    [SerializeField] private Button _joinWithPasswordButton;
    [SerializeField] private Button _cancelPasswordButton;

    [Header("Current Room UI")]
    [SerializeField] private GameObject _currentRoomPanel;
    [SerializeField] private Text _currentRoomNameText;
    [SerializeField] private Text _currentRoomPlayersText;
    [SerializeField] private Transform _playerListParent;
    [SerializeField] private GameObject _playerItemPrefab;
    [SerializeField] private Button _leaveRoomButton;
    [SerializeField] private Button _startGameButton;

    [Header("Events")]
    [SerializeField] private RoomEvents _roomEvents;

    private Dictionary<string, Room> _rooms = new Dictionary<string, Room>();
    private List<GameObject> _roomUIItems = new List<GameObject>();
    private List<GameObject> _playerUIItems = new List<GameObject>();
    private Room _currentRoom;
    private string _currentPlayerName = "Player";
    private Room _pendingJoinRoom;
    private float _lastUpdateTime;

    private void Start()
    {
        InitializeUI();
        SetCurrentPlayerName();
        RefreshRoomList();
    }

    private void Update()
    {
        if (Time.time - _lastUpdateTime >= _roomUpdateInterval)
        {
            UpdateRoomList();
            _lastUpdateTime = Time.time;
        }
    }

    private void InitializeUI()
    {
        if (_createRoomButton != null)
            _createRoomButton.onClick.AddListener(ShowCreateRoomPanel);

        if (_refreshButton != null)
            _refreshButton.onClick.AddListener(RefreshRoomList);

        if (_leaveRoomButton != null)
            _leaveRoomButton.onClick.AddListener(LeaveCurrentRoom);

        if (_startGameButton != null)
            _startGameButton.onClick.AddListener(StartGame);

        if (_joinWithPasswordButton != null)
            _joinWithPasswordButton.onClick.AddListener(JoinRoomWithPassword);

        if (_cancelPasswordButton != null)
            _cancelPasswordButton.onClick.AddListener(CancelPasswordEntry);

        if (_createRoomPanel != null)
            _createRoomPanel.SetActive(false);

        if (_passwordPanel != null)
            _passwordPanel.SetActive(false);

        if (_currentRoomPanel != null)
            _currentRoomPanel.SetActive(false);
    }

    private void SetCurrentPlayerName()
    {
        _currentPlayerName = "Player_" + Random.Range(1000, 9999);
    }

    public void ShowCreateRoomPanel()
    {
        if (_createRoomPanel != null)
            _createRoomPanel.SetActive(true);
    }

    public void HideCreateRoomPanel()
    {
        if (_createRoomPanel != null)
            _createRoomPanel.SetActive(false);
    }

    public void CreateRoom()
    {
        string roomName = _roomNameInput != null ? _roomNameInput.text : "New Room";
        int maxPlayers = _defaultMaxPlayers;

        if (_maxPlayersInput != null && !string.IsNullOrEmpty(_maxPlayersInput.text))
        {
            if (!int.TryParse(_maxPlayersInput.text, out maxPlayers))
                maxPlayers = _defaultMaxPlayers;
        }

        maxPlayers = Mathf.Clamp(maxPlayers, 1, 8);

        bool isPrivate = _privateRoomToggle != null && _privateRoomToggle.isOn;
        string password = isPrivate && _passwordInput != null ? _passwordInput.text : "";

        if (string.IsNullOrEmpty(roomName.Trim()))
        {
            _roomEvents.OnError?.Invoke("Room name cannot be empty");
            return;
        }

        if (_rooms.Count >= _maxRoomsCount)
        {
            _roomEvents.OnError?.Invoke("Maximum number of rooms reached");
            return;
        }

        string roomId = System.Guid.NewGuid().ToString();
        Room newRoom = new Room(roomName.Trim(), roomId, maxPlayers, isPrivate, password);

        _rooms[roomId] = newRoom;
        newRoom.AddPlayer(_currentPlayerName);
        _currentRoom = newRoom;

        _roomEvents.OnRoomCreated?.Invoke(newRoom);
        UpdateCurrentRoomUI();
        HideCreateRoomPanel();
        RefreshRoomList();
    }

    public void JoinRoom(string roomId)
    {
        if (!_rooms.ContainsKey(roomId))
        {
            _roomEvents.OnError?.Invoke("Room not found");
            return;
        }

        Room room = _rooms[roomId];

        if (!room.CanJoin())
        {
            _roomEvents.OnError?.Invoke("Cannot join room");
            return;
        }

        if (room.isPrivate)
        {
            _pendingJoinRoom = room;
            ShowPasswordPanel();
            return;
        }

        JoinRoomInternal(room);
    }

    private void JoinRoomInternal(Room room)
    {
        if (_currentRoom != null)
            LeaveCurrentRoom();

        if (room.AddPlayer(_currentPlayerName))
        {
            _currentRoom = room;
            _roomEvents.OnRoomJoined?.Invoke(room);
            UpdateCurrentRoomUI();
            RefreshRoomList();
        }
        else
        {
            _roomEvents.OnError?.Invoke("Failed to join room");
        }
    }

    private void ShowPasswordPanel()
    {
        if (_passwordPanel != null)
        {
            _passwordPanel.SetActive(true);
            if (_passwordEntryInput != null)
                _passwordEntryInput.text = "";
        }
    }

    private void JoinRoomWithPassword()
    {
        if (_pendingJoinRoom == null)
            return;

        string enteredPassword = _passwordEntryInput != null ? _passwordEntryInput.text : "";

        if (_pendingJoinRoom.password == enteredPassword)
        {
            JoinRoomInternal(_pendingJoinRoom);
            _passwordPanel.SetActive(false);
            _pendingJoinRoom = null;
        }
        else
        {
            _roomEvents.OnError?.Invoke("Incorrect password");
        }
    }

    private void CancelPasswordEntry()
    {
        _pendingJoinRoom = null;
        if (_passwordPanel != null)
            _passwordPanel.SetActive(false);
    }

    public void LeaveCurrentRoom()
    {
        if (_currentRoom == null)
            return;

        _currentRoom.RemovePlayer(_currentPlayerName);

        if (_currentRoom.currentPlayers == 0)
        {
            _rooms.Remove(_currentRoom.roomId);
            _roomEvents.OnRoomDeleted?.Invoke(_currentRoom.roomId);
        }
        else
        {
            _roomEvents.OnRoomUpdated?.Invoke(_currentRoom);
        }

        _roomEvents.OnRoomLeft?.Invoke(_currentRoom);
        _currentRoom = null;

        if (_currentRoomPanel != null)
            _currentRoomPanel.SetActive(false);

        RefreshRoomList();
    }

    public void StartGame()
    {
        if (_currentRoom == null)
            return;

        _currentRoom.isInGame = true;
        _roomEvents.OnRoomUpdated?.Invoke(_currentRoom);
        RefreshRoomList();
    }

    public void RefreshRoomList()
    {
        ClearRoomUI();
        CreateRoomUIItems();
    }

    private void UpdateRoomList()
    {
        if (_currentRoom != null)
            UpdateCurrentRoomUI();
    }

    private void ClearRoomUI()
    {
        foreach (GameObject item in _roomUIItems)
        {
            if (item != null)
                DestroyImmediate(item);
        }
        _roomUIItems.Clear();
    }

    private void CreateRoomUIItems()
    {
        if (_roomListParent == null || _roomItemPrefab == null)
            return;

        foreach (Room room in _rooms.Values)
        {
            GameObject roomItem = Instantiate(_roomItemPrefab, _roomListParent);
            _roomUIItems.Add(roomItem);

            Text[] texts = roomItem.GetComponentsInChildren<Text>();
            if (texts.Length >= 3)
            {
                texts[0].text = room.roomName;
                texts[1].text = $"{room.currentPlayers}/{room.maxPlayers}";
                texts[2].text = room.isInGame ? "In Game" : "Waiting";
            }

            Button joinButton = roomItem.GetComponentInChildren<Button>();
            if (joinButton != null)
            {
                string roomId = room.roomId;
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(() => JoinRoom(roomId));
                joinButton.interactable = room.CanJoin();
            }

            Image lockIcon = roomItem.transform.Find("LockIcon")?.GetComponent<Image>();
            if (lockIcon != null)
                lockIcon.gameObject.SetActive(room.isPrivate);
        }
    }

    private void UpdateCurrentRoomUI()
    {
        if (_currentRoom == null)
        {
            if (_currentRoomPanel != null)
                _currentRoomPanel.SetActive(false);
            return;
        }

        if (_currentRoomPanel != null)
            _currentRoomPanel.SetActive(true);

        if (_currentRoomNameText != null)
            _currentRoomNameText.text = _currentRoom.roomName;

        if (_currentRoomPlayersText != null)
            _currentRoomPlayersText.text = $"Players: {_currentRoom.currentPlayers}/{_currentRoom.maxPlayers}";

        UpdatePlayerList();

        if (_startGameButton != null)
            _startGameButton.interactable = _currentRoom.currentPlayers > 1 && !_currentRoom.isInGame;
    }

    private void UpdatePlayerList()
    {
        ClearPlayerUI();

        if (_playerListParent == null || _playerItemPrefab == null || _currentRoom == null)
            return;

        foreach (string playerName in _currentRoom.playerNames)
        {
            GameObject playerItem = Instantiate(_playerItemPrefab, _playerListParent);
            _playerUIItems.Add(playerItem);

            Text playerText = playerItem.GetComponentInChildren<Text>();
            if (playerText != null)
                playerText.text = playerName;
        }
    }

    private void ClearPlayerUI()
    {
        foreach (GameObject item in _playerUIItems)
        {
            if (item != null)
                DestroyImmediate(item);
        }
        _playerUIItems.Clear();
    }

    public Room GetCurrentRoom()
    {
        return _currentRoom;
    }

    public Dictionary<string, Room> GetAllRooms()
    {
        return new Dictionary<string, Room>(_rooms);
    }

    public void SetPlayerName(string playerName)
    {
        if (!string.IsNullOrEmpty(playerName))
            _currentPlayerName = playerName;
    }

    public string GetPlayerName()
    {
        return _currentPlayerName;
    }
}