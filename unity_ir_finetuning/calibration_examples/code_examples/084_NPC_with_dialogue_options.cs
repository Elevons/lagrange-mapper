// Prompt: NPC with dialogue options
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections.Generic;

public class DialogueNPC : MonoBehaviour
{
    [System.Serializable]
    public class DialogueOption
    {
        [TextArea(2, 4)]
        public string optionText;
        [TextArea(3, 6)]
        public string responseText;
        public bool endsConversation;
        public UnityEvent onOptionSelected;
    }

    [System.Serializable]
    public class DialogueSequence
    {
        [TextArea(3, 6)]
        public string initialText;
        public List<DialogueOption> options = new List<DialogueOption>();
    }

    [Header("NPC Settings")]
    [SerializeField] private string _npcName = "NPC";
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;

    [Header("Dialogue Content")]
    [SerializeField] private List<DialogueSequence> _dialogueSequences = new List<DialogueSequence>();
    [SerializeField] private int _currentSequenceIndex = 0;
    [SerializeField] private bool _canRepeatDialogue = true;

    [Header("UI References")]
    [SerializeField] private Canvas _dialogueCanvas;
    [SerializeField] private Text _npcNameText;
    [SerializeField] private Text _dialogueText;
    [SerializeField] private Transform _optionsParent;
    [SerializeField] private Button _optionButtonPrefab;
    [SerializeField] private Button _closeButton;

    [Header("Interaction Prompt")]
    [SerializeField] private Canvas _promptCanvas;
    [SerializeField] private Text _promptText;

    [Header("Events")]
    public UnityEvent OnDialogueStart;
    public UnityEvent OnDialogueEnd;

    private Transform _player;
    private bool _playerInRange = false;
    private bool _dialogueActive = false;
    private List<Button> _currentOptionButtons = new List<Button>();

    private void Start()
    {
        InitializeUI();
        SetupDefaultDialogue();
    }

    private void Update()
    {
        CheckPlayerDistance();
        HandleInput();
    }

    private void InitializeUI()
    {
        if (_dialogueCanvas != null)
            _dialogueCanvas.gameObject.SetActive(false);

        if (_promptCanvas != null)
            _promptCanvas.gameObject.SetActive(false);

        if (_closeButton != null)
            _closeButton.onClick.AddListener(EndDialogue);

        if (_npcNameText != null)
            _npcNameText.text = _npcName;

        if (_promptText != null)
            _promptText.text = $"Press {_interactionKey} to talk";
    }

    private void SetupDefaultDialogue()
    {
        if (_dialogueSequences.Count == 0)
        {
            DialogueSequence defaultSequence = new DialogueSequence();
            defaultSequence.initialText = "Hello there! How can I help you?";
            
            DialogueOption option1 = new DialogueOption();
            option1.optionText = "Just looking around.";
            option1.responseText = "Feel free to explore! Let me know if you need anything.";
            option1.endsConversation = true;
            
            DialogueOption option2 = new DialogueOption();
            option2.optionText = "What is this place?";
            option2.responseText = "This is a peaceful village where travelers often stop to rest.";
            option2.endsConversation = false;
            
            defaultSequence.options.Add(option1);
            defaultSequence.options.Add(option2);
            _dialogueSequences.Add(defaultSequence);
        }
    }

    private void CheckPlayerDistance()
    {
        if (_player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                _player = playerObj.transform;
            return;
        }

        float distance = Vector3.Distance(transform.position, _player.position);
        bool wasInRange = _playerInRange;
        _playerInRange = distance <= _interactionRange;

        if (_playerInRange && !wasInRange)
            ShowPrompt();
        else if (!_playerInRange && wasInRange)
            HidePrompt();
    }

    private void HandleInput()
    {
        if (_playerInRange && !_dialogueActive && Input.GetKeyDown(_interactionKey))
        {
            StartDialogue();
        }
    }

    private void ShowPrompt()
    {
        if (_promptCanvas != null && !_dialogueActive)
            _promptCanvas.gameObject.SetActive(true);
    }

    private void HidePrompt()
    {
        if (_promptCanvas != null)
            _promptCanvas.gameObject.SetActive(false);
    }

    public void StartDialogue()
    {
        if (_dialogueActive || _dialogueSequences.Count == 0)
            return;

        _dialogueActive = true;
        HidePrompt();

        if (_dialogueCanvas != null)
            _dialogueCanvas.gameObject.SetActive(true);

        DisplayCurrentSequence();
        OnDialogueStart?.Invoke();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void DisplayCurrentSequence()
    {
        if (_currentSequenceIndex >= _dialogueSequences.Count)
        {
            if (_canRepeatDialogue)
                _currentSequenceIndex = 0;
            else
            {
                EndDialogue();
                return;
            }
        }

        DialogueSequence currentSequence = _dialogueSequences[_currentSequenceIndex];
        
        if (_dialogueText != null)
            _dialogueText.text = currentSequence.initialText;

        ClearOptionButtons();
        CreateOptionButtons(currentSequence.options);
    }

    private void ClearOptionButtons()
    {
        foreach (Button button in _currentOptionButtons)
        {
            if (button != null)
                DestroyImmediate(button.gameObject);
        }
        _currentOptionButtons.Clear();
    }

    private void CreateOptionButtons(List<DialogueOption> options)
    {
        if (_optionsParent == null || _optionButtonPrefab == null)
            return;

        for (int i = 0; i < options.Count; i++)
        {
            DialogueOption option = options[i];
            Button optionButton = Instantiate(_optionButtonPrefab, _optionsParent);
            
            Text buttonText = optionButton.GetComponentInChildren<Text>();
            if (buttonText != null)
                buttonText.text = option.optionText;

            int optionIndex = i;
            optionButton.onClick.AddListener(() => SelectOption(optionIndex));
            
            _currentOptionButtons.Add(optionButton);
        }
    }

    private void SelectOption(int optionIndex)
    {
        if (_currentSequenceIndex >= _dialogueSequences.Count)
            return;

        DialogueSequence currentSequence = _dialogueSequences[_currentSequenceIndex];
        if (optionIndex >= currentSequence.options.Count)
            return;

        DialogueOption selectedOption = currentSequence.options[optionIndex];
        
        if (_dialogueText != null)
            _dialogueText.text = selectedOption.responseText;

        selectedOption.onOptionSelected?.Invoke();

        ClearOptionButtons();

        if (selectedOption.endsConversation)
        {
            Invoke(nameof(EndDialogue), 2f);
        }
        else
        {
            _currentSequenceIndex++;
            Invoke(nameof(DisplayCurrentSequence), 2f);
        }
    }

    public void EndDialogue()
    {
        if (!_dialogueActive)
            return;

        _dialogueActive = false;
        ClearOptionButtons();

        if (_dialogueCanvas != null)
            _dialogueCanvas.gameObject.SetActive(false);

        if (_playerInRange)
            ShowPrompt();

        OnDialogueEnd?.Invoke();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void SetDialogueSequence(int sequenceIndex)
    {
        if (sequenceIndex >= 0 && sequenceIndex < _dialogueSequences.Count)
            _currentSequenceIndex = sequenceIndex;
    }

    public void AddDialogueSequence(DialogueSequence newSequence)
    {
        _dialogueSequences.Add(newSequence);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionRange);
    }

    private void OnDisable()
    {
        if (_dialogueActive)
            EndDialogue();
    }
}