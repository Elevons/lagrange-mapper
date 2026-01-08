// Prompt: terminal with hackable interface
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class HackableTerminal : MonoBehaviour
{
    [System.Serializable]
    public class TerminalFile
    {
        public string fileName;
        public string content;
        public bool isLocked;
        public string password;
    }

    [System.Serializable]
    public class HackingPuzzle
    {
        public string targetWord;
        public List<string> decoyWords;
        public int attempts;
    }

    [Header("Terminal UI")]
    [SerializeField] private Canvas _terminalCanvas;
    [SerializeField] private Text _terminalDisplay;
    [SerializeField] private InputField _inputField;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private Button _closeButton;

    [Header("Hacking Interface")]
    [SerializeField] private GameObject _hackingPanel;
    [SerializeField] private Text _hackingDisplay;
    [SerializeField] private Text _attemptsText;
    [SerializeField] private Transform _wordContainer;
    [SerializeField] private Button _wordButtonPrefab;

    [Header("Terminal Settings")]
    [SerializeField] private float _typewriterSpeed = 0.05f;
    [SerializeField] private int _maxDisplayLines = 20;
    [SerializeField] private string _terminalPrompt = "TERMINAL> ";
    [SerializeField] private Color _successColor = Color.green;
    [SerializeField] private Color _errorColor = Color.red;
    [SerializeField] private Color _normalColor = Color.white;

    [Header("Files and Security")]
    [SerializeField] private List<TerminalFile> _files = new List<TerminalFile>();
    [SerializeField] private HackingPuzzle _hackingPuzzle;
    [SerializeField] private bool _requiresHacking = true;
    [SerializeField] private float _interactionRange = 3f;

    [Header("Events")]
    public UnityEvent OnTerminalAccessed;
    public UnityEvent OnHackingSuccess;
    public UnityEvent OnHackingFailed;
    public UnityEvent OnTerminalClosed;

    private List<string> _displayLines = new List<string>();
    private bool _isPlayerNear = false;
    private bool _isTerminalOpen = false;
    private bool _isHacked = false;
    private bool _isHacking = false;
    private int _currentAttempts;
    private Transform _playerTransform;
    private Coroutine _typewriterCoroutine;

    private Dictionary<string, System.Action<string[]>> _commands;

    private void Start()
    {
        InitializeTerminal();
        InitializeCommands();
        SetupUI();
    }

    private void Update()
    {
        CheckPlayerProximity();
        HandleInput();
    }

    private void InitializeTerminal()
    {
        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(false);
        
        if (_hackingPanel != null)
            _hackingPanel.SetActive(false);

        _currentAttempts = _hackingPuzzle.attempts;
        
        if (_files.Count == 0)
        {
            _files.Add(new TerminalFile 
            { 
                fileName = "readme.txt", 
                content = "Welcome to the terminal system.", 
                isLocked = false 
            });
            _files.Add(new TerminalFile 
            { 
                fileName = "secure.dat", 
                content = "Classified information stored here.", 
                isLocked = true, 
                password = "admin123" 
            });
        }
    }

    private void InitializeCommands()
    {
        _commands = new Dictionary<string, System.Action<string[]>>
        {
            { "help", ShowHelp },
            { "ls", ListFiles },
            { "dir", ListFiles },
            { "cat", ReadFile },
            { "type", ReadFile },
            { "clear", ClearScreen },
            { "cls", ClearScreen },
            { "hack", StartHacking },
            { "login", AttemptLogin },
            { "exit", CloseTerminal },
            { "quit", CloseTerminal }
        };
    }

    private void SetupUI()
    {
        if (_inputField != null)
        {
            _inputField.onEndEdit.AddListener(ProcessCommand);
        }

        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(CloseTerminal);
        }

        AddLine("SYSTEM INITIALIZED");
        AddLine("Type 'help' for available commands");
        AddLine("");
    }

    private void CheckPlayerProximity()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }

        if (_playerTransform != null)
        {
            float distance = Vector3.Distance(transform.position, _playerTransform.position);
            bool wasNear = _isPlayerNear;
            _isPlayerNear = distance <= _interactionRange;

            if (_isPlayerNear && !wasNear)
            {
                ShowInteractionPrompt();
            }
            else if (!_isPlayerNear && wasNear)
            {
                HideInteractionPrompt();
            }
        }
    }

    private void HandleInput()
    {
        if (_isPlayerNear && Input.GetKeyDown(KeyCode.E) && !_isTerminalOpen)
        {
            OpenTerminal();
        }
        else if (_isTerminalOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseTerminal();
        }
    }

    private void ShowInteractionPrompt()
    {
        // This would typically show UI prompt - implement based on your UI system
        Debug.Log("Press E to access terminal");
    }

    private void HideInteractionPrompt()
    {
        // Hide interaction prompt
    }

    private void OpenTerminal()
    {
        if (_requiresHacking && !_isHacked)
        {
            StartHackingSequence();
            return;
        }

        _isTerminalOpen = true;
        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (_inputField != null)
        {
            _inputField.Select();
            _inputField.ActivateInputField();
        }

        OnTerminalAccessed?.Invoke();
        AddLine("Terminal access granted");
        AddLine("");
    }

    private void CloseTerminal()
    {
        _isTerminalOpen = false;
        _isHacking = false;

        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(false);
        
        if (_hackingPanel != null)
            _hackingPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        OnTerminalClosed?.Invoke();
    }

    private void ProcessCommand(string input)
    {
        if (string.IsNullOrEmpty(input) || _isHacking)
            return;

        AddLine(_terminalPrompt + input);
        
        string[] parts = input.ToLower().Split(' ');
        string command = parts[0];

        if (_commands.ContainsKey(command))
        {
            _commands[command](parts);
        }
        else
        {
            AddLine("Unknown command: " + command, _errorColor);
            AddLine("Type 'help' for available commands");
        }

        AddLine("");
        
        if (_inputField != null)
        {
            _inputField.text = "";
            _inputField.Select();
            _inputField.ActivateInputField();
        }
    }

    private void ShowHelp(string[] args)
    {
        AddLine("Available commands:");
        AddLine("help - Show this help message");
        AddLine("ls/dir - List files");
        AddLine("cat/type [filename] - Read file content");
        AddLine("clear/cls - Clear screen");
        if (!_isHacked && _requiresHacking)
            AddLine("hack - Start hacking sequence");
        AddLine("login [password] - Login with password");
        AddLine("exit/quit - Close terminal");
    }

    private void ListFiles(string[] args)
    {
        AddLine("Files:");
        foreach (var file in _files)
        {
            string status = file.isLocked ? " [LOCKED]" : "";
            AddLine("  " + file.fileName + status);
        }
    }

    private void ReadFile(string[] args)
    {
        if (args.Length < 2)
        {
            AddLine("Usage: cat [filename]", _errorColor);
            return;
        }

        string filename = args[1];
        var file = _files.FirstOrDefault(f => f.fileName.ToLower() == filename.ToLower());

        if (file == null)
        {
            AddLine("File not found: " + filename, _errorColor);
            return;
        }

        if (file.isLocked)
        {
            AddLine("Access denied: File is locked", _errorColor);
            return;
        }

        AddLine("--- " + file.fileName + " ---");
        string[] lines = file.content.Split('\n');
        foreach (string line in lines)
        {
            AddLine(line);
        }
        AddLine("--- End of file ---");
    }

    private void ClearScreen(string[] args)
    {
        _displayLines.Clear();
        UpdateDisplay();
    }

    private void StartHacking(string[] args)
    {
        if (_isHacked)
        {
            AddLine("System already compromised", _successColor);
            return;
        }

        StartHackingSequence();
    }

    private void AttemptLogin(string[] args)
    {
        if (args.Length < 2)
        {
            AddLine("Usage: login [password]", _errorColor);
            return;
        }

        string password = args[1];
        var lockedFile = _files.FirstOrDefault(f => f.isLocked && f.password == password);

        if (lockedFile != null)
        {
            lockedFile.isLocked = false;
            AddLine("File unlocked: " + lockedFile.fileName, _successColor);
        }
        else
        {
            AddLine("Invalid password", _errorColor);
        }
    }

    private void StartHackingSequence()
    {
        _isHacking = true;
        _currentAttempts = _hackingPuzzle.attempts;

        if (_hackingPanel != null)
            _hackingPanel.SetActive(true);

        SetupHackingPuzzle();
    }

    private void SetupHackingPuzzle()
    {
        if (_hackingDisplay != null)
        {
            _hackingDisplay.text = "BREACH PROTOCOL INITIATED\nSelect the correct access code:";
        }

        UpdateAttemptsDisplay();
        CreateWordButtons();
    }

    private void CreateWordButtons()
    {
        if (_wordContainer == null || _wordButtonPrefab == null)
            return;

        // Clear existing buttons
        foreach (Transform child in _wordContainer)
        {
            Destroy(child.gameObject);
        }

        // Create list of all words (target + decoys)
        List<string> allWords = new List<string>(_hackingPuzzle.decoyWords);
        allWords.Add(_hackingPuzzle.targetWord);
        
        // Shuffle the words
        for (int i = 0; i < allWords.Count; i++)
        {
            string temp = allWords[i];
            int randomIndex = Random.Range(i, allWords.Count);
            allWords[i] = allWords[randomIndex];
            allWords[randomIndex] = temp;
        }

        // Create buttons
        foreach (string word in allWords)
        {
            Button wordButton = Instantiate(_wordButtonPrefab, _wordContainer);
            Text buttonText = wordButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = word;

            wordButton.onClick.AddListener(() => OnWordSelected(word));
        }
    }

    private void OnWordSelected(string selectedWord)
    {
        if (selectedWord == _hackingPuzzle.targetWord)
        {
            HackingSuccess();
        }
        else
        {
            _currentAttempts--;
            UpdateAttemptsDisplay();

            if (_currentAttempts <= 0)
            {
                HackingFailed();
            }
            else
            {
                if (_hackingDisplay != null)
                {
                    _hackingDisplay.text = "ACCESS DENIED\nIncorrect code. Try again.";
                }
            }
        }
    }

    private void HackingSuccess()
    {
        _isHacked = true;
        _isHacking = false;

        if (_hackingDisplay != null)
        {
            _hackingDisplay.text = "ACCESS GRANTED\nSystem compromised successfully!";
        }

        // Unlock all files
        foreach (var file in _files)
        {
            file.isLocked = false;
        }

        OnHackingSuccess?.Invoke();
        
        StartCoroutine(DelayedOpenTerminal());
    }

    private void HackingFailed()
    {
        _isHacking = false;

        if (_hackingDisplay != null)
        {
            _hackingDisplay.text = "SECURITY BREACH DETECTED\nTerminal locked down.";
        }

        OnHackingFailed?.Invoke();
        
        StartCoroutine(DelayedCloseTerminal());
    }

    private IEnumerator DelayedOpenTerminal()
    {
        yield return new WaitForSeconds(2f);
        
        if (_hackingPanel != null)
            _hackingPanel.SetActive(false);
        
        OpenTerminal();
    }

    private IEnumerator DelayedCloseTerminal()
    {
        yield return new WaitForSeconds(2f);
        CloseTerminal();
    }

    private void UpdateAttemptsDisplay()
    {
        if (_attemptsText != null)
        {
            _attemptsText.text = "Attempts remaining: " + _currentAttempts;
        }
    }

    private void AddLine(string text, Color? color = null)
    {
        _displayLines.Add(text);
        
        if (_displayLines.Count > _maxDisplayLines)
        {
            _displayLines.RemoveAt(0);
        }

        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }

        _typewriterCoroutine = StartCoroutine(TypewriterEffect(text, color ?? _normalColor));
    }

    private IEnumerator TypewriterEffect(string text, Color color)
    {
        UpdateDisplay();
        
        if (_terminalDisplay != null)
        {
            _terminalDisplay.color = color;
        }

        yield return new WaitForSeconds(_typewriterSpeed);
        
        if (_scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            _scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    private void UpdateDisplay()
    {
        if (_terminalDisplay != null)
        {
            _terminalDisplay.text = string.Join("\n", _display