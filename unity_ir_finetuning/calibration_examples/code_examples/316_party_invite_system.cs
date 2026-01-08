// Prompt: party invite system
// Type: general

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class PartyInviteSystem : MonoBehaviour
{
    [System.Serializable]
    public class PartyMember
    {
        public string playerId;
        public string playerName;
        public bool isLeader;
        public bool isOnline;
        public DateTime joinTime;

        public PartyMember(string id, string name, bool leader = false)
        {
            playerId = id;
            playerName = name;
            isLeader = leader;
            isOnline = true;
            joinTime = DateTime.Now;
        }
    }

    [System.Serializable]
    public class PartyInvite
    {
        public string inviteId;
        public string fromPlayerId;
        public string fromPlayerName;
        public string toPlayerId;
        public string toPlayerName;
        public DateTime sentTime;
        public float expireTime;

        public PartyInvite(string from, string fromName, string to, string toName, float expire)
        {
            inviteId = Guid.NewGuid().ToString();
            fromPlayerId = from;
            fromPlayerName = fromName;
            toPlayerId = to;
            toPlayerName = toName;
            sentTime = DateTime.Now;
            expireTime = expire;
        }

        public bool IsExpired => (DateTime.Now - sentTime).TotalSeconds > expireTime;
    }

    [System.Serializable]
    public class PartyInviteEvent : UnityEvent<PartyInvite> { }

    [System.Serializable]
    public class PartyMemberEvent : UnityEvent<PartyMember> { }

    [System.Serializable]
    public class PartyEvent : UnityEvent<List<PartyMember>> { }

    [Header("Party Settings")]
    [SerializeField] private int _maxPartySize = 4;
    [SerializeField] private float _inviteExpireTime = 30f;
    [SerializeField] private bool _allowPublicJoin = false;
    [SerializeField] private string _currentPlayerId = "Player1";
    [SerializeField] private string _currentPlayerName = "LocalPlayer";

    [Header("UI References")]
    [SerializeField] private GameObject _invitePanel;
    [SerializeField] private Text _inviteText;
    [SerializeField] private Button _acceptButton;
    [SerializeField] private Button _declineButton;
    [SerializeField] private GameObject _partyPanel;
    [SerializeField] private Transform _memberListParent;
    [SerializeField] private GameObject _memberPrefab;
    [SerializeField] private Button _leavePartyButton;
    [SerializeField] private InputField _invitePlayerInput;
    [SerializeField] private Button _sendInviteButton;

    [Header("Events")]
    public PartyInviteEvent OnInviteReceived;
    public PartyInviteEvent OnInviteSent;
    public PartyMemberEvent OnMemberJoined;
    public PartyMemberEvent OnMemberLeft;
    public PartyEvent OnPartyUpdated;
    public UnityEvent OnPartyDisbanded;

    private List<PartyMember> _currentParty = new List<PartyMember>();
    private List<PartyInvite> _pendingInvites = new List<PartyInvite>();
    private List<PartyInvite> _sentInvites = new List<PartyInvite>();
    private PartyInvite _currentInviteDisplay;
    private Dictionary<string, GameObject> _memberUIElements = new Dictionary<string, GameObject>();

    public bool IsInParty => _currentParty.Count > 0;
    public bool IsPartyLeader => IsInParty && _currentParty.Find(m => m.playerId == _currentPlayerId)?.isLeader == true;
    public int PartySize => _currentParty.Count;
    public bool IsPartyFull => PartySize >= _maxPartySize;

    private void Start()
    {
        InitializeUI();
        StartCoroutine(CleanupExpiredInvites());
    }

    private void InitializeUI()
    {
        if (_invitePanel != null)
            _invitePanel.SetActive(false);

        if (_partyPanel != null)
            _partyPanel.SetActive(false);

        if (_acceptButton != null)
            _acceptButton.onClick.AddListener(AcceptCurrentInvite);

        if (_declineButton != null)
            _declineButton.onClick.AddListener(DeclineCurrentInvite);

        if (_leavePartyButton != null)
            _leavePartyButton.onClick.AddListener(LeaveParty);

        if (_sendInviteButton != null)
            _sendInviteButton.onClick.AddListener(SendInviteFromInput);

        UpdatePartyUI();
    }

    public void SendInvite(string targetPlayerId, string targetPlayerName)
    {
        if (string.IsNullOrEmpty(targetPlayerId) || string.IsNullOrEmpty(targetPlayerName))
        {
            Debug.LogWarning("Cannot send invite: Invalid target player information");
            return;
        }

        if (targetPlayerId == _currentPlayerId)
        {
            Debug.LogWarning("Cannot invite yourself to party");
            return;
        }

        if (IsPartyFull)
        {
            Debug.LogWarning("Cannot send invite: Party is full");
            return;
        }

        if (IsInParty && !IsPartyLeader)
        {
            Debug.LogWarning("Cannot send invite: Only party leader can invite members");
            return;
        }

        if (_currentParty.Exists(m => m.playerId == targetPlayerId))
        {
            Debug.LogWarning("Player is already in the party");
            return;
        }

        if (_sentInvites.Exists(i => i.toPlayerId == targetPlayerId && !i.IsExpired))
        {
            Debug.LogWarning("Invite already sent to this player");
            return;
        }

        PartyInvite invite = new PartyInvite(_currentPlayerId, _currentPlayerName, targetPlayerId, targetPlayerName, _inviteExpireTime);
        _sentInvites.Add(invite);

        OnInviteSent?.Invoke(invite);
        Debug.Log($"Party invite sent to {targetPlayerName}");

        // Simulate receiving invite on target (for demo purposes)
        if (targetPlayerId == "DemoPlayer")
        {
            SimulateReceiveInvite(invite);
        }
    }

    public void ReceiveInvite(PartyInvite invite)
    {
        if (invite == null || invite.IsExpired)
        {
            Debug.LogWarning("Received invalid or expired invite");
            return;
        }

        if (invite.toPlayerId != _currentPlayerId)
        {
            Debug.LogWarning("Received invite not intended for this player");
            return;
        }

        if (IsPartyFull)
        {
            Debug.LogWarning("Cannot accept invite: Already in a full party");
            return;
        }

        _pendingInvites.Add(invite);
        OnInviteReceived?.Invoke(invite);
        ShowInviteUI(invite);
    }

    public void AcceptInvite(string inviteId)
    {
        PartyInvite invite = _pendingInvites.Find(i => i.inviteId == inviteId);
        if (invite == null)
        {
            Debug.LogWarning("Invite not found");
            return;
        }

        if (invite.IsExpired)
        {
            Debug.LogWarning("Cannot accept expired invite");
            _pendingInvites.Remove(invite);
            return;
        }

        // Leave current party if in one
        if (IsInParty)
        {
            LeaveParty();
        }

        // Join the party
        JoinParty(invite.fromPlayerId, invite.fromPlayerName);
        _pendingInvites.Remove(invite);
        HideInviteUI();

        Debug.Log($"Accepted party invite from {invite.fromPlayerName}");
    }

    public void DeclineInvite(string inviteId)
    {
        PartyInvite invite = _pendingInvites.Find(i => i.inviteId == inviteId);
        if (invite != null)
        {
            _pendingInvites.Remove(invite);
            Debug.Log($"Declined party invite from {invite.fromPlayerName}");
        }
        HideInviteUI();
    }

    public void CreateParty()
    {
        if (IsInParty)
        {
            Debug.LogWarning("Already in a party");
            return;
        }

        _currentParty.Clear();
        PartyMember leader = new PartyMember(_currentPlayerId, _currentPlayerName, true);
        _currentParty.Add(leader);

        OnMemberJoined?.Invoke(leader);
        OnPartyUpdated?.Invoke(_currentParty);
        UpdatePartyUI();

        Debug.Log("Party created");
    }

    public void JoinParty(string leaderId, string leaderName)
    {
        if (IsInParty)
        {
            LeaveParty();
        }

        _currentParty.Clear();
        
        // Add leader
        PartyMember leader = new PartyMember(leaderId, leaderName, true);
        _currentParty.Add(leader);

        // Add self
        PartyMember self = new PartyMember(_currentPlayerId, _currentPlayerName, false);
        _currentParty.Add(self);

        OnMemberJoined?.Invoke(self);
        OnPartyUpdated?.Invoke(_currentParty);
        UpdatePartyUI();

        Debug.Log($"Joined party led by {leaderName}");
    }

    public void LeaveParty()
    {
        if (!IsInParty)
        {
            Debug.LogWarning("Not in a party");
            return;
        }

        PartyMember leavingMember = _currentParty.Find(m => m.playerId == _currentPlayerId);
        bool wasLeader = leavingMember?.isLeader == true;

        _currentParty.RemoveAll(m => m.playerId == _currentPlayerId);

        if (wasLeader && _currentParty.Count > 0)
        {
            // Transfer leadership to next member
            _currentParty[0].isLeader = true;
            Debug.Log($"Leadership transferred to {_currentParty[0].playerName}");
        }

        if (leavingMember != null)
        {
            OnMemberLeft?.Invoke(leavingMember);
        }

        if (_currentParty.Count == 0)
        {
            OnPartyDisbanded?.Invoke();
            Debug.Log("Party disbanded");
        }
        else
        {
            OnPartyUpdated?.Invoke(_currentParty);
        }

        UpdatePartyUI();
        Debug.Log("Left party");
    }

    public void KickMember(string playerId)
    {
        if (!IsPartyLeader)
        {
            Debug.LogWarning("Only party leader can kick members");
            return;
        }

        if (playerId == _currentPlayerId)
        {
            Debug.LogWarning("Cannot kick yourself");
            return;
        }

        PartyMember memberToKick = _currentParty.Find(m => m.playerId == playerId);
        if (memberToKick == null)
        {
            Debug.LogWarning("Member not found in party");
            return;
        }

        _currentParty.Remove(memberToKick);
        OnMemberLeft?.Invoke(memberToKick);
        OnPartyUpdated?.Invoke(_currentParty);
        UpdatePartyUI();

        Debug.Log($"Kicked {memberToKick.playerName} from party");
    }

    private void SendInviteFromInput()
    {
        if (_invitePlayerInput != null && !string.IsNullOrEmpty(_invitePlayerInput.text))
        {
            string targetName = _invitePlayerInput.text.Trim();
            string targetId = "Player_" + targetName; // Simple ID generation
            SendInvite(targetId, targetName);
            _invitePlayerInput.text = "";
        }
    }

    private void AcceptCurrentInvite()
    {
        if (_currentInviteDisplay != null)
        {
            AcceptInvite(_currentInviteDisplay.inviteId);
        }
    }

    private void DeclineCurrentInvite()
    {
        if (_currentInviteDisplay != null)
        {
            DeclineInvite(_currentInviteDisplay.inviteId);
        }
    }

    private void ShowInviteUI(PartyInvite invite)
    {
        _currentInviteDisplay = invite;
        
        if (_invitePanel != null)
        {
            _invitePanel.SetActive(true);
        }

        if (_inviteText != null)
        {
            _inviteText.text = $"{invite.fromPlayerName} invited you to join their party";
        }
    }

    private void HideInviteUI()
    {
        _currentInviteDisplay = null;
        
        if (_invitePanel != null)
        {
            _invitePanel.SetActive(false);
        }
    }

    private void UpdatePartyUI()
    {
        if (_partyPanel != null)
        {
            _partyPanel.SetActive(IsInParty);
        }

        if (_leavePartyButton != null)
        {
            _leavePartyButton.gameObject.SetActive(IsInParty);
        }

        UpdateMemberList();
    }

    private void UpdateMemberList()
    {
        if (_memberListParent == null || _memberPrefab == null)
            return;

        // Clear existing UI elements
        foreach (var kvp in _memberUIElements)
        {
            if (kvp.Value != null)
                DestroyImmediate(kvp.Value);
        }
        _memberUIElements.Clear();

        // Create UI elements for current party members
        foreach (PartyMember member in _currentParty)
        {
            GameObject memberUI = Instantiate(_memberPrefab, _memberListParent);
            _memberUIElements[member.playerId] = memberUI;

            Text memberText = memberUI.GetComponentInChildren<Text>();
            if (memberText != null)
            {
                string displayText = member.playerName;
                if (member.isLeader)
                    displayText += " (Leader)";
                if (member.playerId == _currentPlayerId)
                    displayText += " (You)";
                
                memberText.text = displayText;
            }

            Button kickButton = memberUI.GetComponentInChildren<Button>();
            if (kickButton != null)
            {
                bool canKick = IsPartyLeader && member.playerId != _currentPlayerId;
                kickButton.gameObject.SetActive(canKick);
                
                if (canKick)
                {
                    string memberIdToKick = member.playerId;
                    kickButton.onClick.RemoveAllListeners();
                    kickButton.onClick.AddListener(() => KickMember(memberIdToKick));
                }
            }
        }
    }

    private System.Collections.IEnumerator CleanupExpiredInvites()
    {
        while (true)
        {
            yield return new WaitForSeconds(5f);

            _pendingInvites.RemoveAll(i => i.IsExpired);
            _sentInvites.RemoveAll(i => i.IsExpired);