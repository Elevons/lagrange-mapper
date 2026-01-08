// Prompt: computer terminal with text
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class ComputerTerminal : MonoBehaviour
{
    [Header("Terminal Display")]
    [SerializeField] private Canvas _terminalCanvas;
    [SerializeField] private Text _terminalText;
    [SerializeField] private ScrollRect _scrollRect;
    [SerializeField] private InputField _inputField;
    [SerializeField] private Button _submitButton;
    
    [Header("Terminal Settings")]
    [SerializeField] private float _typewriterSpeed = 0.05f;
    [SerializeField] private int _maxLines = 20;
    [SerializeField] private string _promptSymbol = "> ";
    [SerializeField] private Color _inputColor = Color.green;
    [SerializeField] private Color _outputColor = Color.white;
    [SerializeField] private Color _errorColor = Color.red;
    
    [Header("Interaction")]
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    [SerializeField] private GameObject _interactionPrompt;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _keyPressSound;
    [SerializeField] private AudioClip _enterSound;
    [SerializeField] private AudioClip _errorSound;
    
    [Header("Events")]
    public UnityEvent OnTerminalActivated;
    public UnityEvent OnTerminalDeactivated;
    public UnityEvent<string> OnCommandExecuted;
    
    private List<string> _terminalLines = new List<string>();
    private Dictionary<string, System.Action<string[]>> _commands = new Dictionary<string, System.Action<string[]>>();
    private Transform _player;
    private bool _isActive = false;
    private bool _isTyping = false;
    private Coroutine _typewriterCoroutine;
    
    [System.Serializable]
    public class TerminalCommand
    {
        public string command;
        public string description;
        public string response;
    }
    
    [Header("Predefined Commands")]
    [SerializeField] private TerminalCommand[] _predefinedCommands = new TerminalCommand[]
    {
        new TerminalCommand { command = "help", description = "Show available commands", response = "" },
        new TerminalCommand { command = "clear", description = "Clear terminal screen", response = "" },
        new TerminalCommand { command = "status", description = "Show system status", response = "System Status: ONLINE\nCPU Usage: 23%\nMemory: 4.2GB/8GB" },
        new TerminalCommand { command = "date", description = "Show current date", response = "" },
        new TerminalCommand { command = "whoami", description = "Show current user", response = "User: Administrator" }
    };
    
    void Start()
    {
        InitializeTerminal();
        SetupCommands();
        
        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(false);
            
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
            
        _player = GameObject.FindGameObjectWithTag("Player")?.transform;
        
        if (_submitButton != null)
            _submitButton.onClick.AddListener(ProcessInput);
            
        if (_inputField != null)
        {
            _inputField.onEndEdit.AddListener(OnInputEndEdit);
        }
    }
    
    void Update()
    {
        CheckPlayerDistance();
        HandleInput();
    }
    
    void InitializeTerminal()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        AddLine("Terminal initialized...", _outputColor);
        AddLine("Type 'help' for available commands", _outputColor);
        AddLine("", _outputColor);
    }
    
    void SetupCommands()
    {
        _commands.Clear();
        _commands.Add("help", ExecuteHelp);
        _commands.Add("clear", ExecuteClear);
        _commands.Add("status", ExecuteStatus);
        _commands.Add("date", ExecuteDate);
        _commands.Add("whoami", ExecuteWhoAmI);
        _commands.Add("echo", ExecuteEcho);
        _commands.Add("exit", ExecuteExit);
        
        foreach (var cmd in _predefinedCommands)
        {
            if (!_commands.ContainsKey(cmd.command.ToLower()))
            {
                _commands.Add(cmd.command.ToLower(), (args) => ExecuteCustomCommand(cmd));
            }
        }
    }
    
    void CheckPlayerDistance()
    {
        if (_player == null) return;
        
        float distance = Vector3.Distance(transform.position, _player.position);
        bool inRange = distance <= _interactionDistance;
        
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(inRange && !_isActive);
    }
    
    void HandleInput()
    {
        if (_player == null) return;
        
        float distance = Vector3.Distance(transform.position, _player.position);
        
        if (distance <= _interactionDistance && Input.GetKeyDown(_interactionKey))
        {
            if (!_isActive)
                ActivateTerminal();
            else
                DeactivateTerminal();
        }
        
        if (_isActive && Input.GetKeyDown(KeyCode.Escape))
        {
            DeactivateTerminal();
        }
    }
    
    void OnInputEndEdit(string input)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            ProcessInput();
        }
    }
    
    void ActivateTerminal()
    {
        _isActive = true;
        
        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(true);
            
        if (_interactionPrompt != null)
            _interactionPrompt.SetActive(false);
            
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        if (_inputField != null)
        {
            _inputField.Select();
            _inputField.ActivateInputField();
        }
        
        OnTerminalActivated.Invoke();
    }
    
    void DeactivateTerminal()
    {
        _isActive = false;
        
        if (_terminalCanvas != null)
            _terminalCanvas.gameObject.SetActive(false);
            
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        OnTerminalDeactivated.Invoke();
    }
    
    void ProcessInput()
    {
        if (_inputField == null || string.IsNullOrEmpty(_inputField.text.Trim()) || _isTyping)
            return;
            
        string input = _inputField.text.Trim();
        AddLine(_promptSymbol + input, _inputColor);
        
        PlaySound(_enterSound);
        
        string[] parts = input.ToLower().Split(' ');
        string command = parts[0];
        
        if (_commands.ContainsKey(command))
        {
            _commands[command](parts);
            OnCommandExecuted.Invoke(input);
        }
        else
        {
            AddLine($"Command '{command}' not recognized. Type 'help' for available commands.", _errorColor);
            PlaySound(_errorSound);
        }
        
        _inputField.text = "";
        _inputField.Select();
        _inputField.ActivateInputField();
    }
    
    void AddLine(string text, Color color)
    {
        if (_isTyping && _typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _isTyping = false;
        }
        
        _terminalLines.Add($"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>");
        
        while (_terminalLines.Count > _maxLines)
        {
            _terminalLines.RemoveAt(0);
        }
        
        if (_typewriterSpeed > 0)
        {
            _typewriterCoroutine = StartCoroutine(TypewriterEffect());
        }
        else
        {
            UpdateTerminalDisplay();
        }
    }
    
    void AddLineWithTypewriter(string text, Color color)
    {
        _typewriterCoroutine = StartCoroutine(TypewriterLine(text, color));
    }
    
    IEnumerator TypewriterLine(string text, Color color)
    {
        _isTyping = true;
        string coloredText = $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>";
        _terminalLines.Add(coloredText);
        
        for (int i = 0; i < text.Length; i++)
        {
            _terminalLines[_terminalLines.Count - 1] = coloredText + text.Substring(0, i + 1) + "</color>";
            UpdateTerminalDisplay();
            
            if (_keyPressSound != null && i % 3 == 0)
                PlaySound(_keyPressSound);
                
            yield return new WaitForSeconds(_typewriterSpeed);
        }
        
        _isTyping = false;
    }
    
    IEnumerator TypewriterEffect()
    {
        _isTyping = true;
        yield return new WaitForSeconds(_typewriterSpeed);
        UpdateTerminalDisplay();
        _isTyping = false;
    }
    
    void UpdateTerminalDisplay()
    {
        if (_terminalText != null)
        {
            _terminalText.text = string.Join("\n", _terminalLines);
            
            if (_scrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
    
    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    // Command Implementations
    void ExecuteHelp(string[] args)
    {
        AddLine("Available Commands:", _outputColor);
        AddLine("==================", _outputColor);
        
        foreach (var cmd in _predefinedCommands)
        {
            AddLine($"{cmd.command.PadRight(12)} - {cmd.description}", _outputColor);
        }
        
        AddLine("echo [text]  - Echo text back", _outputColor);
        AddLine("exit         - Close terminal", _outputColor);
    }
    
    void ExecuteClear(string[] args)
    {
        _terminalLines.Clear();
        UpdateTerminalDisplay();
    }
    
    void ExecuteStatus(string[] args)
    {
        var statusCmd = System.Array.Find(_predefinedCommands, cmd => cmd.command == "status");
        if (statusCmd != null && !string.IsNullOrEmpty(statusCmd.response))
        {
            string[] lines = statusCmd.response.Split('\n');
            foreach (string line in lines)
            {
                AddLine(line, _outputColor);
            }
        }
    }
    
    void ExecuteDate(string[] args)
    {
        AddLine(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), _outputColor);
    }
    
    void ExecuteWhoAmI(string[] args)
    {
        var whoamiCmd = System.Array.Find(_predefinedCommands, cmd => cmd.command == "whoami");
        if (whoamiCmd != null && !string.IsNullOrEmpty(whoamiCmd.response))
        {
            AddLine(whoamiCmd.response, _outputColor);
        }
    }
    
    void ExecuteEcho(string[] args)
    {
        if (args.Length > 1)
        {
            string echoText = string.Join(" ", args, 1, args.Length - 1);
            AddLine(echoText, _outputColor);
        }
        else
        {
            AddLine("Usage: echo [text]", _errorColor);
        }
    }
    
    void ExecuteExit(string[] args)
    {
        AddLine("Closing terminal...", _outputColor);
        StartCoroutine(DelayedDeactivate());
    }
    
    void ExecuteCustomCommand(TerminalCommand cmd)
    {
        if (!string.IsNullOrEmpty(cmd.response))
        {
            string[] lines = cmd.response.Split('\n');
            foreach (string line in lines)
            {
                AddLine(line, _outputColor);
            }
        }
    }
    
    IEnumerator DelayedDeactivate()
    {
        yield return new WaitForSeconds(1f);
        DeactivateTerminal();
    }
    
    public void AddCustomCommand(string command, string description, string response)
    {
        var newCommand = new TerminalCommand
        {
            command = command.ToLower(),
            description = description,
            response = response
        };
        
        _commands[command.ToLower()] = (args) => ExecuteCustomCommand(newCommand);
    }
    
    public void ExecuteCommand(string commandLine)
    {
        if (_inputField != null)
        {
            _inputField.text = commandLine;
            ProcessInput();
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _interactionDistance);
    }
}