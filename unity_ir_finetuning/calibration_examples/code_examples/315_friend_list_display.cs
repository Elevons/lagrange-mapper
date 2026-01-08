// Prompt: friend list display
// Type: general

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class FriendListDisplay : MonoBehaviour
{
    [System.Serializable]
    public class Friend
    {
        public string username;
        public string displayName;
        public bool isOnline;
        public DateTime lastSeen;
        public Sprite avatar;
        public int level;
        
        public Friend(string username, string displayName)
        {
            this.username = username;
            this.displayName = displayName;
            this.isOnline = false;
            this.lastSeen = DateTime.Now;
            this.level = 1;
        }
    }
    
    [System.Serializable]
    public class FriendSelectedEvent : UnityEvent<Friend> { }
    
    [Header("UI References")]
    [SerializeField] private Transform _friendListParent;
    [SerializeField] private GameObject _friendItemPrefab;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Text _friendCountText;
    [SerializeField] private Button _refreshButton;
    [SerializeField] private InputField _searchField;
    
    [Header("Display Settings")]
    [SerializeField] private bool _showOnlineFirst = true;
    [SerializeField] private bool _showLastSeen = true;
    [SerializeField] private Color _onlineColor = Color.green;
    [SerializeField] private Color _offlineColor = Color.gray;
    [SerializeField] private int _maxDisplayedFriends = 100;
    
    [Header("Events")]
    [SerializeField] private FriendSelectedEvent _onFriendSelected;
    [SerializeField] private UnityEvent _onFriendListRefreshed;
    
    private List<Friend> _allFriends = new List<Friend>();
    private List<Friend> _filteredFriends = new List<Friend>();
    private List<GameObject> _friendItemObjects = new List<GameObject>();
    private string _currentSearchFilter = "";
    
    private void Start()
    {
        InitializeUI();
        LoadSampleFriends();
        RefreshFriendList();
    }
    
    private void InitializeUI()
    {
        if (_refreshButton != null)
            _refreshButton.onClick.AddListener(RefreshFriendList);
            
        if (_searchField != null)
            _searchField.onValueChanged.AddListener(OnSearchChanged);
    }
    
    private void LoadSampleFriends()
    {
        _allFriends.Clear();
        
        // Add sample friends for demonstration
        AddFriend("player1", "Alex Thunder", true, 25);
        AddFriend("player2", "Sarah Storm", false, 18);
        AddFriend("player3", "Mike Lightning", true, 32);
        AddFriend("player4", "Emma Frost", false, 12);
        AddFriend("player5", "Jake Fire", true, 45);
        AddFriend("player6", "Luna Moon", false, 8);
        AddFriend("player7", "Rex Dragon", true, 67);
        AddFriend("player8", "Zoe Star", false, 23);
    }
    
    public void AddFriend(string username, string displayName, bool isOnline = false, int level = 1)
    {
        Friend newFriend = new Friend(username, displayName)
        {
            isOnline = isOnline,
            level = level,
            lastSeen = isOnline ? DateTime.Now : DateTime.Now.AddHours(-UnityEngine.Random.Range(1, 72))
        };
        
        _allFriends.Add(newFriend);
    }
    
    public void RemoveFriend(string username)
    {
        _allFriends.RemoveAll(f => f.username == username);
        RefreshFriendList();
    }
    
    public void RefreshFriendList()
    {
        FilterFriends();
        SortFriends();
        UpdateFriendDisplay();
        UpdateFriendCount();
        _onFriendListRefreshed?.Invoke();
    }
    
    private void FilterFriends()
    {
        _filteredFriends.Clear();
        
        foreach (Friend friend in _allFriends)
        {
            if (string.IsNullOrEmpty(_currentSearchFilter) || 
                friend.displayName.ToLower().Contains(_currentSearchFilter.ToLower()) ||
                friend.username.ToLower().Contains(_currentSearchFilter.ToLower()))
            {
                _filteredFriends.Add(friend);
            }
        }
        
        if (_filteredFriends.Count > _maxDisplayedFriends)
        {
            _filteredFriends = _filteredFriends.GetRange(0, _maxDisplayedFriends);
        }
    }
    
    private void SortFriends()
    {
        if (_showOnlineFirst)
        {
            _filteredFriends.Sort((a, b) =>
            {
                if (a.isOnline && !b.isOnline) return -1;
                if (!a.isOnline && b.isOnline) return 1;
                return string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
            });
        }
        else
        {
            _filteredFriends.Sort((a, b) => 
                string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
        }
    }
    
    private void UpdateFriendDisplay()
    {
        ClearFriendItems();
        
        if (_friendListParent == null || _friendItemPrefab == null)
            return;
            
        foreach (Friend friend in _filteredFriends)
        {
            GameObject friendItem = Instantiate(_friendItemPrefab, _friendListParent);
            SetupFriendItem(friendItem, friend);
            _friendItemObjects.Add(friendItem);
        }
        
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 1f;
        }
    }
    
    private void SetupFriendItem(GameObject friendItem, Friend friend)
    {
        // Setup display name
        Text nameText = friendItem.transform.Find("NameText")?.GetComponent<Text>();
        if (nameText != null)
            nameText.text = friend.displayName;
            
        // Setup online status
        Image statusImage = friendItem.transform.Find("StatusImage")?.GetComponent<Image>();
        if (statusImage != null)
            statusImage.color = friend.isOnline ? _onlineColor : _offlineColor;
            
        // Setup level text
        Text levelText = friendItem.transform.Find("LevelText")?.GetComponent<Text>();
        if (levelText != null)
            levelText.text = $"Lv.{friend.level}";
            
        // Setup last seen text
        if (_showLastSeen)
        {
            Text lastSeenText = friendItem.transform.Find("LastSeenText")?.GetComponent<Text>();
            if (lastSeenText != null)
            {
                if (friend.isOnline)
                {
                    lastSeenText.text = "Online";
                    lastSeenText.color = _onlineColor;
                }
                else
                {
                    TimeSpan timeDiff = DateTime.Now - friend.lastSeen;
                    lastSeenText.text = FormatLastSeen(timeDiff);
                    lastSeenText.color = _offlineColor;
                }
            }
        }
        
        // Setup avatar
        Image avatarImage = friendItem.transform.Find("AvatarImage")?.GetComponent<Image>();
        if (avatarImage != null && friend.avatar != null)
            avatarImage.sprite = friend.avatar;
            
        // Setup click handler
        Button friendButton = friendItem.GetComponent<Button>();
        if (friendButton != null)
        {
            friendButton.onClick.AddListener(() => OnFriendSelected(friend));
        }
    }
    
    private string FormatLastSeen(TimeSpan timeDiff)
    {
        if (timeDiff.TotalMinutes < 1)
            return "Just now";
        else if (timeDiff.TotalHours < 1)
            return $"{(int)timeDiff.TotalMinutes}m ago";
        else if (timeDiff.TotalDays < 1)
            return $"{(int)timeDiff.TotalHours}h ago";
        else if (timeDiff.TotalDays < 7)
            return $"{(int)timeDiff.TotalDays}d ago";
        else
            return "Long ago";
    }
    
    private void ClearFriendItems()
    {
        foreach (GameObject item in _friendItemObjects)
        {
            if (item != null)
                DestroyImmediate(item);
        }
        _friendItemObjects.Clear();
    }
    
    private void UpdateFriendCount()
    {
        if (_friendCountText != null)
        {
            int onlineCount = 0;
            foreach (Friend friend in _allFriends)
            {
                if (friend.isOnline) onlineCount++;
            }
            
            _friendCountText.text = $"Friends ({onlineCount}/{_allFriends.Count})";
        }
    }
    
    private void OnSearchChanged(string searchText)
    {
        _currentSearchFilter = searchText;
        RefreshFriendList();
    }
    
    private void OnFriendSelected(Friend friend)
    {
        _onFriendSelected?.Invoke(friend);
    }
    
    public void SetFriendOnlineStatus(string username, bool isOnline)
    {
        Friend friend = _allFriends.Find(f => f.username == username);
        if (friend != null)
        {
            friend.isOnline = isOnline;
            if (!isOnline)
                friend.lastSeen = DateTime.Now;
            RefreshFriendList();
        }
    }
    
    public void UpdateFriendLevel(string username, int newLevel)
    {
        Friend friend = _allFriends.Find(f => f.username == username);
        if (friend != null)
        {
            friend.level = newLevel;
            RefreshFriendList();
        }
    }
    
    public Friend GetFriend(string username)
    {
        return _allFriends.Find(f => f.username == username);
    }
    
    public List<Friend> GetOnlineFriends()
    {
        return _allFriends.FindAll(f => f.isOnline);
    }
    
    public int GetFriendCount()
    {
        return _allFriends.Count;
    }
    
    public int GetOnlineFriendCount()
    {
        return _allFriends.FindAll(f => f.isOnline).Count;
    }
    
    private void OnDestroy()
    {
        ClearFriendItems();
    }
}