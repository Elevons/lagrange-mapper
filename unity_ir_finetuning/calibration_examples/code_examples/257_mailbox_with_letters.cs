// Prompt: mailbox with letters
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class Mailbox : MonoBehaviour
{
    [System.Serializable]
    public class Letter
    {
        public string senderName;
        public string subject;
        [TextArea(3, 10)]
        public string content;
        public bool isRead;
        public Sprite letterIcon;
    }

    [System.Serializable]
    public class MailboxEvent : UnityEvent<Letter> { }

    [Header("Mailbox Settings")]
    [SerializeField] private List<Letter> _letters = new List<Letter>();
    [SerializeField] private Transform _letterSpawnPoint;
    [SerializeField] private GameObject _letterPrefab;
    [SerializeField] private float _interactionRange = 2f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    [Header("Visual Feedback")]
    [SerializeField] private GameObject _hasMailIndicator;
    [SerializeField] private AudioClip _openMailboxSound;
    [SerializeField] private AudioClip _newLetterSound;
    [SerializeField] private ParticleSystem _mailDeliveryEffect;

    [Header("Animation")]
    [SerializeField] private Animator _mailboxAnimator;
    [SerializeField] private string _openAnimationTrigger = "Open";
    [SerializeField] private string _closeAnimationTrigger = "Close";

    [Header("Events")]
    public MailboxEvent OnLetterRead;
    public UnityEvent OnMailboxOpened;
    public UnityEvent OnMailboxClosed;
    public UnityEvent OnNewLetterReceived;

    private AudioSource _audioSource;
    private bool _isOpen = false;
    private bool _playerInRange = false;
    private GameObject _currentPlayer;
    private Canvas _mailboxUI;
    private bool _hasUnreadMail = false;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        CreateMailboxUI();
        UpdateMailIndicator();
    }

    private void Update()
    {
        CheckForPlayerInteraction();
        HandleInput();
    }

    private void CheckForPlayerInteraction()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _interactionRange);
        bool playerFound = false;

        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                if (!_playerInRange)
                {
                    _playerInRange = true;
                    _currentPlayer = col.gameObject;
                    ShowInteractionPrompt(true);
                }
                playerFound = true;
                break;
            }
        }

        if (!playerFound && _playerInRange)
        {
            _playerInRange = false;
            _currentPlayer = null;
            ShowInteractionPrompt(false);
            if (_isOpen)
                CloseMailbox();
        }
    }

    private void HandleInput()
    {
        if (_playerInRange && Input.GetKeyDown(_interactionKey))
        {
            if (_isOpen)
                CloseMailbox();
            else
                OpenMailbox();
        }
    }

    private void OpenMailbox()
    {
        if (_isOpen) return;

        _isOpen = true;
        
        if (_mailboxAnimator != null)
            _mailboxAnimator.SetTrigger(_openAnimationTrigger);

        PlaySound(_openMailboxSound);
        ShowMailboxUI(true);
        OnMailboxOpened?.Invoke();
    }

    private void CloseMailbox()
    {
        if (!_isOpen) return;

        _isOpen = false;
        
        if (_mailboxAnimator != null)
            _mailboxAnimator.SetTrigger(_closeAnimationTrigger);

        ShowMailboxUI(false);
        OnMailboxClosed?.Invoke();
    }

    public void AddLetter(Letter newLetter)
    {
        _letters.Add(newLetter);
        PlaySound(_newLetterSound);
        
        if (_mailDeliveryEffect != null)
            _mailDeliveryEffect.Play();

        UpdateMailIndicator();
        OnNewLetterReceived?.Invoke();

        if (_letterPrefab != null && _letterSpawnPoint != null)
        {
            GameObject letterObj = Instantiate(_letterPrefab, _letterSpawnPoint.position, _letterSpawnPoint.rotation);
            LetterItem letterItem = letterObj.GetComponent<LetterItem>();
            if (letterItem == null)
                letterItem = letterObj.AddComponent<LetterItem>();
            letterItem.Initialize(newLetter);
        }
    }

    public void ReadLetter(int letterIndex)
    {
        if (letterIndex >= 0 && letterIndex < _letters.Count)
        {
            _letters[letterIndex].isRead = true;
            OnLetterRead?.Invoke(_letters[letterIndex]);
            UpdateMailIndicator();
        }
    }

    public void RemoveLetter(int letterIndex)
    {
        if (letterIndex >= 0 && letterIndex < _letters.Count)
        {
            _letters.RemoveAt(letterIndex);
            UpdateMailIndicator();
        }
    }

    private void UpdateMailIndicator()
    {
        _hasUnreadMail = false;
        foreach (Letter letter in _letters)
        {
            if (!letter.isRead)
            {
                _hasUnreadMail = true;
                break;
            }
        }

        if (_hasMailIndicator != null)
            _hasMailIndicator.SetActive(_hasUnreadMail);
    }

    private void CreateMailboxUI()
    {
        GameObject uiObject = new GameObject("MailboxUI");
        _mailboxUI = uiObject.AddComponent<Canvas>();
        _mailboxUI.renderMode = RenderMode.WorldSpace;
        _mailboxUI.worldCamera = Camera.main;
        
        uiObject.transform.SetParent(transform);
        uiObject.transform.localPosition = Vector3.up * 2f;
        uiObject.transform.localScale = Vector3.one * 0.01f;
        
        _mailboxUI.gameObject.SetActive(false);
    }

    private void ShowMailboxUI(bool show)
    {
        if (_mailboxUI != null)
            _mailboxUI.gameObject.SetActive(show);
    }

    private void ShowInteractionPrompt(bool show)
    {
        // This would typically show/hide UI prompt
        // Implementation depends on your UI system
    }

    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
            _audioSource.PlayOneShot(clip);
    }

    public int GetLetterCount()
    {
        return _letters.Count;
    }

    public int GetUnreadLetterCount()
    {
        int count = 0;
        foreach (Letter letter in _letters)
        {
            if (!letter.isRead)
                count++;
        }
        return count;
    }

    public List<Letter> GetAllLetters()
    {
        return new List<Letter>(_letters);
    }

    public List<Letter> GetUnreadLetters()
    {
        List<Letter> unreadLetters = new List<Letter>();
        foreach (Letter letter in _letters)
        {
            if (!letter.isRead)
                unreadLetters.Add(letter);
        }
        return unreadLetters;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }
}

public class LetterItem : MonoBehaviour
{
    private Mailbox.Letter _letterData;
    private bool _isCollected = false;

    public void Initialize(Mailbox.Letter letter)
    {
        _letterData = letter;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isCollected)
        {
            CollectLetter();
        }
    }

    private void CollectLetter()
    {
        _isCollected = true;
        
        // Add visual/audio feedback here
        Destroy(gameObject, 0.1f);
    }

    public Mailbox.Letter GetLetterData()
    {
        return _letterData;
    }
}