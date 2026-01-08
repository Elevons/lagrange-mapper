// Prompt: phone with conversation
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class Phone : MonoBehaviour
{
    [System.Serializable]
    public class ConversationLine
    {
        public string speakerName;
        [TextArea(2, 4)]
        public string text;
        public float displayDuration = 3f;
        public AudioClip voiceClip;
    }

    [System.Serializable]
    public class Conversation
    {
        public string conversationName;
        public List<ConversationLine> lines = new List<ConversationLine>();
        public bool canSkip = true;
        public bool autoAdvance = true;
    }

    [Header("Phone Settings")]
    [SerializeField] private GameObject _phoneUI;
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private KeyCode _interactKey = KeyCode.E;
    [SerializeField] private LayerMask _playerLayer = 1;

    [Header("UI Components")]
    [SerializeField] private Text _speakerNameText;
    [SerializeField] private Text _conversationText;
    [SerializeField] private Button _nextButton;
    [SerializeField] private Button _skipButton;
    [SerializeField] private Button _hangUpButton;
    [SerializeField] private Image _phoneScreen;
    [SerializeField] private GameObject _interactionPrompt;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _ringTone;
    [SerializeField] private AudioClip _dialTone;
    [SerializeField] private AudioClip _hangUpSound;
    [SerializeField] private float _ringVolume = 0.7f;

    [Header("Conversations")]
    [SerializeField] private List<Conversation> _conversations = new List<Conversation>();
    [SerializeField] private int _currentConversationIndex = 0;
    [SerializeField] private bool _randomizeConversations = false;

    [Header("Visual Effects")]
    [SerializeField] private GameObject _ringingEffect;
    [SerializeField] private Color _activeScreenColor = Color.green;
    [SerializeField] private Color _inactiveScreenColor = Color.black;

    [Header("Events")]
    public UnityEvent OnPhoneRing;
    public UnityEvent OnConversationStart;
    public UnityEvent OnConversationEnd;
    public UnityEvent OnPlayerNearby;
    public UnityEvent OnPlayerLeft;

    private bool _isRinging = false;
    private bool _isInConversation = false;
    private bool _playerInRange = false;
    private int _currentLineIndex = 0;
    private Conversation _currentConversation;
    private Coroutine _conversationCoroutine;
    private Coroutine _ringingCoroutine;
    private Transform _playerTransform;

    private void Start()
    {
        InitializePhone();
        SetupUI();
        
        if (_conversations.Count > 0)
        {
            StartRinging();
        }
    }

    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
    }

    private void InitializePhone()
    {
        if (_phoneUI != null)
            _phoneUI.SetActive(false);

        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);

        if (_ringingEffect != null)
            _ringingEffect.SetActive(false);

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        UpdateScreenColor(_inactiveScreenColor);
    }

    private void SetupUI()
    {
        if (_nextButton != null)
            _nextButton.onClick.AddListener(NextLine);

        if (_skipButton != null)
            _skipButton.onClick.AddListener(SkipConversation);

        if (_hangUpButton != null)
            _hangUpButton.onClick.AddListener(HangUp);
    }

    private void CheckPlayerProximity()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.transform.position);
        bool wasInRange = _playerInRange;
        _playerInRange = distance <= _interactionRange;

        if (_playerInRange && !wasInRange)
        {
            _playerTransform = player.transform;
            OnPlayerNearby.Invoke();
            ShowInteractionPrompt(true);
        }
        else if (!_playerInRange && wasInRange)
        {
            _playerTransform = null;
            OnPlayerLeft.Invoke();
            ShowInteractionPrompt(false);
        }
    }

    private void HandleInput()
    {
        if (!_playerInRange) return;

        if (Input.GetKeyDown(_interactKey))
        {
            if (_isRinging && !_isInConversation)
            {
                AnswerPhone();
            }
            else if (_isInConversation)
            {
                NextLine();
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape) && _isInConversation)
        {
            HangUp();
        }
    }

    private void ShowInteractionPrompt(bool show)
    {
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(show && (_isRinging || _isInConversation));
    }

    public void StartRinging()
    {
        if (_isInConversation || _conversations.Count == 0) return;

        _isRinging = true;
        
        if (_ringingEffect != null)
            _ringingEffect.SetActive(true);

        OnPhoneRing.Invoke();
        ShowInteractionPrompt(_playerInRange);

        if (_ringingCoroutine != null)
            StopCoroutine(_ringingCoroutine);
        
        _ringingCoroutine = StartCoroutine(PlayRingTone());
    }

    public void AnswerPhone()
    {
        if (!_isRinging || _isInConversation) return;

        StopRinging();
        StartConversation();
    }

    private void StopRinging()
    {
        _isRinging = false;
        
        if (_ringingEffect != null)
            _ringingEffect.SetActive(false);

        if (_ringingCoroutine != null)
        {
            StopCoroutine(_ringingCoroutine);
            _ringingCoroutine = null;
        }

        _audioSource.Stop();
    }

    private void StartConversation()
    {
        if (_conversations.Count == 0) return;

        _isInConversation = true;
        _currentLineIndex = 0;

        if (_randomizeConversations)
        {
            _currentConversationIndex = Random.Range(0, _conversations.Count);
        }

        _currentConversation = _conversations[_currentConversationIndex];
        
        if (_phoneUI != null)
            _phoneUI.SetActive(true);

        UpdateScreenColor(_activeScreenColor);
        OnConversationStart.Invoke();

        if (_dialTone != null)
        {
            _audioSource.PlayOneShot(_dialTone);
        }

        ShowInteractionPrompt(_playerInRange);
        DisplayCurrentLine();
    }

    private void DisplayCurrentLine()
    {
        if (_currentConversation == null || _currentLineIndex >= _currentConversation.lines.Count)
        {
            EndConversation();
            return;
        }

        ConversationLine currentLine = _currentConversation.lines[_currentLineIndex];

        if (_speakerNameText != null)
            _speakerNameText.text = currentLine.speakerName;

        if (_conversationText != null)
            _conversationText.text = currentLine.text;

        if (currentLine.voiceClip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(currentLine.voiceClip);
        }

        UpdateUIButtons();

        if (_currentConversation.autoAdvance)
        {
            if (_conversationCoroutine != null)
                StopCoroutine(_conversationCoroutine);
            
            _conversationCoroutine = StartCoroutine(AutoAdvanceLine(currentLine.displayDuration));
        }
    }

    private void UpdateUIButtons()
    {
        bool isLastLine = _currentLineIndex >= _currentConversation.lines.Count - 1;
        
        if (_nextButton != null)
            _nextButton.gameObject.SetActive(!isLastLine || !_currentConversation.autoAdvance);

        if (_skipButton != null)
            _skipButton.gameObject.SetActive(_currentConversation.canSkip);
    }

    public void NextLine()
    {
        if (!_isInConversation) return;

        if (_conversationCoroutine != null)
        {
            StopCoroutine(_conversationCoroutine);
            _conversationCoroutine = null;
        }

        _currentLineIndex++;
        DisplayCurrentLine();
    }

    public void SkipConversation()
    {
        if (!_isInConversation || !_currentConversation.canSkip) return;

        EndConversation();
    }

    public void HangUp()
    {
        if (_isInConversation)
        {
            EndConversation();
        }
        else if (_isRinging)
        {
            StopRinging();
        }
    }

    private void EndConversation()
    {
        _isInConversation = false;
        
        if (_conversationCoroutine != null)
        {
            StopCoroutine(_conversationCoroutine);
            _conversationCoroutine = null;
        }

        if (_phoneUI != null)
            _phoneUI.SetActive(false);

        UpdateScreenColor(_inactiveScreenColor);
        ShowInteractionPrompt(false);

        if (_hangUpSound != null)
        {
            _audioSource.PlayOneShot(_hangUpSound);
        }

        OnConversationEnd.Invoke();

        // Move to next conversation for future calls
        _currentConversationIndex = (_currentConversationIndex + 1) % _conversations.Count;
    }

    private IEnumerator AutoAdvanceLine(float duration)
    {
        yield return new WaitForSeconds(duration);
        NextLine();
    }

    private IEnumerator PlayRingTone()
    {
        while (_isRinging)
        {
            if (_ringTone != null)
            {
                _audioSource.PlayOneShot(_ringTone, _ringVolume);
                yield return new WaitForSeconds(_ringTone.length + 1f);
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }
    }

    private void UpdateScreenColor(Color color)
    {
        if (_phoneScreen != null)
            _phoneScreen.color = color;
    }

    public void AddConversation(Conversation conversation)
    {
        _conversations.Add(conversation);
    }

    public void TriggerCall()
    {
        if (!_isRinging && !_isInConversation)
        {
            StartRinging();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }

    private void OnDisable()
    {
        if (_conversationCoroutine != null)
            StopCoroutine(_conversationCoroutine);
        
        if (_ringingCoroutine != null)
            StopCoroutine(_ringingCoroutine);
    }
}