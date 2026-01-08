// Prompt: sequence memory game
// Type: general

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SequenceMemoryGame : MonoBehaviour
{
    [System.Serializable]
    public class GameButton
    {
        public Button button;
        public Color normalColor = Color.white;
        public Color highlightColor = Color.yellow;
        public AudioClip soundClip;
        [HideInInspector] public Image buttonImage;
        [HideInInspector] public AudioSource audioSource;
    }

    [Header("Game Setup")]
    [SerializeField] private GameButton[] _gameButtons;
    [SerializeField] private float _sequenceDisplayTime = 1f;
    [SerializeField] private float _buttonHighlightDuration = 0.5f;
    [SerializeField] private float _timeBetweenButtons = 0.3f;
    [SerializeField] private int _startingSequenceLength = 3;
    [SerializeField] private int _maxSequenceLength = 10;

    [Header("UI Elements")]
    [SerializeField] private Button _startButton;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Text _scoreText;
    [SerializeField] private Text _levelText;
    [SerializeField] private Text _statusText;
    [SerializeField] private GameObject _gameOverPanel;

    [Header("Audio")]
    [SerializeField] private AudioClip _successSound;
    [SerializeField] private AudioClip _failSound;
    [SerializeField] private AudioSource _mainAudioSource;

    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent OnLevelComplete;
    public UnityEvent<int> OnScoreChanged;

    private List<int> _currentSequence = new List<int>();
    private List<int> _playerInput = new List<int>();
    private int _currentLevel = 1;
    private int _score = 0;
    private bool _isShowingSequence = false;
    private bool _isWaitingForInput = false;
    private bool _gameActive = false;
    private Coroutine _sequenceCoroutine;

    private void Start()
    {
        InitializeGame();
    }

    private void InitializeGame()
    {
        // Initialize button components
        for (int i = 0; i < _gameButtons.Length; i++)
        {
            if (_gameButtons[i].button != null)
            {
                _gameButtons[i].buttonImage = _gameButtons[i].button.GetComponent<Image>();
                _gameButtons[i].audioSource = _gameButtons[i].button.GetComponent<AudioSource>();
                
                if (_gameButtons[i].audioSource == null)
                {
                    _gameButtons[i].audioSource = _gameButtons[i].button.gameObject.AddComponent<AudioSource>();
                }

                int buttonIndex = i;
                _gameButtons[i].button.onClick.AddListener(() => OnButtonPressed(buttonIndex));
                
                if (_gameButtons[i].buttonImage != null)
                {
                    _gameButtons[i].buttonImage.color = _gameButtons[i].normalColor;
                }
            }
        }

        // Setup UI buttons
        if (_startButton != null)
            _startButton.onClick.AddListener(StartGame);
        
        if (_resetButton != null)
            _resetButton.onClick.AddListener(ResetGame);

        // Initialize UI
        UpdateUI();
        SetGameButtonsInteractable(false);
        
        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(false);
    }

    public void StartGame()
    {
        _gameActive = true;
        _currentLevel = 1;
        _score = 0;
        _currentSequence.Clear();
        _playerInput.Clear();

        if (_startButton != null)
            _startButton.interactable = false;

        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(false);

        UpdateUI();
        OnGameStart?.Invoke();
        
        StartNewLevel();
    }

    public void ResetGame()
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        _gameActive = false;
        _isShowingSequence = false;
        _isWaitingForInput = false;
        _currentLevel = 1;
        _score = 0;
        _currentSequence.Clear();
        _playerInput.Clear();

        if (_startButton != null)
            _startButton.interactable = true;

        SetGameButtonsInteractable(false);
        ResetButtonColors();
        UpdateUI();

        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(false);
    }

    private void StartNewLevel()
    {
        if (!_gameActive) return;

        _playerInput.Clear();
        GenerateSequence();
        
        if (_statusText != null)
            _statusText.text = "Watch the sequence...";

        _sequenceCoroutine = StartCoroutine(ShowSequence());
    }

    private void GenerateSequence()
    {
        int sequenceLength = Mathf.Min(_startingSequenceLength + _currentLevel - 1, _maxSequenceLength);
        
        if (_currentLevel == 1)
        {
            _currentSequence.Clear();
        }
        else
        {
            // Add one more button to existing sequence
        }

        while (_currentSequence.Count < sequenceLength)
        {
            int randomButton = Random.Range(0, _gameButtons.Length);
            _currentSequence.Add(randomButton);
        }
    }

    private IEnumerator ShowSequence()
    {
        _isShowingSequence = true;
        SetGameButtonsInteractable(false);
        
        yield return new WaitForSeconds(_sequenceDisplayTime);

        for (int i = 0; i < _currentSequence.Count; i++)
        {
            int buttonIndex = _currentSequence[i];
            yield return StartCoroutine(HighlightButton(buttonIndex));
            yield return new WaitForSeconds(_timeBetweenButtons);
        }

        _isShowingSequence = false;
        _isWaitingForInput = true;
        SetGameButtonsInteractable(true);
        
        if (_statusText != null)
            _statusText.text = "Repeat the sequence!";
    }

    private IEnumerator HighlightButton(int buttonIndex)
    {
        if (buttonIndex < 0 || buttonIndex >= _gameButtons.Length) yield break;

        GameButton gameButton = _gameButtons[buttonIndex];
        
        // Highlight button
        if (gameButton.buttonImage != null)
        {
            gameButton.buttonImage.color = gameButton.highlightColor;
        }

        // Play sound
        if (gameButton.audioSource != null && gameButton.soundClip != null)
        {
            gameButton.audioSource.PlayOneShot(gameButton.soundClip);
        }

        yield return new WaitForSeconds(_buttonHighlightDuration);

        // Return to normal color
        if (gameButton.buttonImage != null)
        {
            gameButton.buttonImage.color = gameButton.normalColor;
        }
    }

    private void OnButtonPressed(int buttonIndex)
    {
        if (!_isWaitingForInput || !_gameActive) return;

        _playerInput.Add(buttonIndex);
        StartCoroutine(HighlightButton(buttonIndex));

        // Check if input matches sequence so far
        bool isCorrect = true;
        for (int i = 0; i < _playerInput.Count; i++)
        {
            if (_playerInput[i] != _currentSequence[i])
            {
                isCorrect = false;
                break;
            }
        }

        if (!isCorrect)
        {
            GameOver();
            return;
        }

        // Check if sequence is complete
        if (_playerInput.Count == _currentSequence.Count)
        {
            LevelComplete();
        }
    }

    private void LevelComplete()
    {
        _isWaitingForInput = false;
        SetGameButtonsInteractable(false);
        
        _score += _currentLevel * 10;
        _currentLevel++;
        
        OnScoreChanged?.Invoke(_score);
        OnLevelComplete?.Invoke();

        if (_mainAudioSource != null && _successSound != null)
        {
            _mainAudioSource.PlayOneShot(_successSound);
        }

        if (_statusText != null)
            _statusText.text = "Correct! Next level...";

        UpdateUI();
        
        Invoke(nameof(StartNewLevel), 2f);
    }

    private void GameOver()
    {
        _gameActive = false;
        _isWaitingForInput = false;
        SetGameButtonsInteractable(false);
        
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
            _sequenceCoroutine = null;
        }

        if (_mainAudioSource != null && _failSound != null)
        {
            _mainAudioSource.PlayOneShot(_failSound);
        }

        if (_statusText != null)
            _statusText.text = "Game Over!";

        if (_startButton != null)
            _startButton.interactable = true;

        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(true);

        OnGameOver?.Invoke();
    }

    private void SetGameButtonsInteractable(bool interactable)
    {
        foreach (GameButton gameButton in _gameButtons)
        {
            if (gameButton.button != null)
            {
                gameButton.button.interactable = interactable;
            }
        }
    }

    private void ResetButtonColors()
    {
        foreach (GameButton gameButton in _gameButtons)
        {
            if (gameButton.buttonImage != null)
            {
                gameButton.buttonImage.color = gameButton.normalColor;
            }
        }
    }

    private void UpdateUI()
    {
        if (_scoreText != null)
            _scoreText.text = "Score: " + _score;

        if (_levelText != null)
            _levelText.text = "Level: " + _currentLevel;

        if (_statusText != null && !_gameActive)
            _statusText.text = "Press Start to begin!";
    }

    private void OnDestroy()
    {
        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
        }
    }
}