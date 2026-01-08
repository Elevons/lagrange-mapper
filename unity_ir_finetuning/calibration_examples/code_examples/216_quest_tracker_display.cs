// Prompt: quest tracker display
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System;

public class QuestTrackerDisplay : MonoBehaviour
{
    [System.Serializable]
    public class Quest
    {
        public string questId;
        public string title;
        public string description;
        public List<QuestObjective> objectives;
        public bool isCompleted;
        public bool isActive;
        
        public Quest(string id, string questTitle, string questDescription)
        {
            questId = id;
            title = questTitle;
            description = questDescription;
            objectives = new List<QuestObjective>();
            isCompleted = false;
            isActive = false;
        }
    }
    
    [System.Serializable]
    public class QuestObjective
    {
        public string description;
        public int currentCount;
        public int targetCount;
        public bool isCompleted;
        
        public QuestObjective(string desc, int target)
        {
            description = desc;
            currentCount = 0;
            targetCount = target;
            isCompleted = false;
        }
        
        public void UpdateProgress(int amount)
        {
            currentCount = Mathf.Clamp(currentCount + amount, 0, targetCount);
            isCompleted = currentCount >= targetCount;
        }
    }
    
    [Header("UI References")]
    [SerializeField] private GameObject _questTrackerPanel;
    [SerializeField] private Transform _questContainer;
    [SerializeField] private GameObject _questEntryPrefab;
    [SerializeField] private Button _toggleButton;
    [SerializeField] private Text _noQuestsText;
    
    [Header("Display Settings")]
    [SerializeField] private int _maxDisplayedQuests = 5;
    [SerializeField] private bool _showCompletedQuests = false;
    [SerializeField] private bool _autoHideWhenEmpty = true;
    [SerializeField] private Color _completedQuestColor = Color.green;
    [SerializeField] private Color _activeQuestColor = Color.white;
    [SerializeField] private Color _objectiveCompletedColor = Color.gray;
    
    [Header("Animation")]
    [SerializeField] private bool _useAnimations = true;
    [SerializeField] private float _fadeSpeed = 2f;
    [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private List<Quest> _allQuests = new List<Quest>();
    private List<GameObject> _questEntryObjects = new List<GameObject>();
    private bool _isVisible = true;
    private CanvasGroup _canvasGroup;
    
    public static event Action<string> OnQuestCompleted;
    public static event Action<string, int> OnObjectiveUpdated;
    
    private void Awake()
    {
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }
    
    private void Start()
    {
        InitializeUI();
        RefreshDisplay();
        
        if (_toggleButton != null)
            _toggleButton.onClick.AddListener(ToggleVisibility);
    }
    
    private void InitializeUI()
    {
        if (_questTrackerPanel == null)
            _questTrackerPanel = gameObject;
            
        if (_noQuestsText != null)
            _noQuestsText.text = "No active quests";
    }
    
    public void AddQuest(string questId, string title, string description)
    {
        if (GetQuestById(questId) != null)
            return;
            
        Quest newQuest = new Quest(questId, title, description);
        newQuest.isActive = true;
        _allQuests.Add(newQuest);
        RefreshDisplay();
    }
    
    public void AddObjectiveToQuest(string questId, string objectiveDescription, int targetCount)
    {
        Quest quest = GetQuestById(questId);
        if (quest == null)
            return;
            
        QuestObjective objective = new QuestObjective(objectiveDescription, targetCount);
        quest.objectives.Add(objective);
        RefreshDisplay();
    }
    
    public void UpdateObjective(string questId, int objectiveIndex, int progressAmount)
    {
        Quest quest = GetQuestById(questId);
        if (quest == null || objectiveIndex < 0 || objectiveIndex >= quest.objectives.Count)
            return;
            
        QuestObjective objective = quest.objectives[objectiveIndex];
        objective.UpdateProgress(progressAmount);
        
        OnObjectiveUpdated?.Invoke(questId, objectiveIndex);
        
        CheckQuestCompletion(quest);
        RefreshDisplay();
    }
    
    public void CompleteQuest(string questId)
    {
        Quest quest = GetQuestById(questId);
        if (quest == null)
            return;
            
        quest.isCompleted = true;
        quest.isActive = false;
        
        foreach (var objective in quest.objectives)
        {
            objective.isCompleted = true;
            objective.currentCount = objective.targetCount;
        }
        
        OnQuestCompleted?.Invoke(questId);
        RefreshDisplay();
        
        if (_useAnimations)
            StartCoroutine(AnimateQuestCompletion(questId));
    }
    
    public void RemoveQuest(string questId)
    {
        Quest quest = GetQuestById(questId);
        if (quest == null)
            return;
            
        _allQuests.Remove(quest);
        RefreshDisplay();
    }
    
    private Quest GetQuestById(string questId)
    {
        return _allQuests.Find(q => q.questId == questId);
    }
    
    private void CheckQuestCompletion(Quest quest)
    {
        if (quest.isCompleted)
            return;
            
        bool allObjectivesComplete = true;
        foreach (var objective in quest.objectives)
        {
            if (!objective.isCompleted)
            {
                allObjectivesComplete = false;
                break;
            }
        }
        
        if (allObjectivesComplete && quest.objectives.Count > 0)
        {
            CompleteQuest(quest.questId);
        }
    }
    
    private void RefreshDisplay()
    {
        ClearQuestEntries();
        
        List<Quest> displayQuests = GetQuestsToDisplay();
        
        if (displayQuests.Count == 0)
        {
            ShowNoQuestsMessage();
            if (_autoHideWhenEmpty)
                SetVisibility(false);
            return;
        }
        
        HideNoQuestsMessage();
        
        foreach (Quest quest in displayQuests)
        {
            CreateQuestEntry(quest);
        }
        
        if (_autoHideWhenEmpty)
            SetVisibility(true);
    }
    
    private List<Quest> GetQuestsToDisplay()
    {
        List<Quest> displayQuests = new List<Quest>();
        
        foreach (Quest quest in _allQuests)
        {
            if (quest.isActive || (_showCompletedQuests && quest.isCompleted))
            {
                displayQuests.Add(quest);
            }
        }
        
        if (displayQuests.Count > _maxDisplayedQuests)
        {
            displayQuests = displayQuests.GetRange(0, _maxDisplayedQuests);
        }
        
        return displayQuests;
    }
    
    private void CreateQuestEntry(Quest quest)
    {
        if (_questEntryPrefab == null || _questContainer == null)
            return;
            
        GameObject entryObject = Instantiate(_questEntryPrefab, _questContainer);
        _questEntryObjects.Add(entryObject);
        
        QuestEntryUI entryUI = entryObject.GetComponent<QuestEntryUI>();
        if (entryUI == null)
            entryUI = entryObject.AddComponent<QuestEntryUI>();
            
        entryUI.SetupQuestEntry(quest, _completedQuestColor, _activeQuestColor, _objectiveCompletedColor);
        
        if (_useAnimations)
            StartCoroutine(AnimateEntryAppearance(entryObject));
    }
    
    private void ClearQuestEntries()
    {
        foreach (GameObject entry in _questEntryObjects)
        {
            if (entry != null)
                DestroyImmediate(entry);
        }
        _questEntryObjects.Clear();
    }
    
    private void ShowNoQuestsMessage()
    {
        if (_noQuestsText != null)
            _noQuestsText.gameObject.SetActive(true);
    }
    
    private void HideNoQuestsMessage()
    {
        if (_noQuestsText != null)
            _noQuestsText.gameObject.SetActive(false);
    }
    
    public void ToggleVisibility()
    {
        SetVisibility(!_isVisible);
    }
    
    public void SetVisibility(bool visible)
    {
        _isVisible = visible;
        
        if (_useAnimations)
        {
            StopAllCoroutines();
            StartCoroutine(AnimateVisibility(visible));
        }
        else
        {
            _questTrackerPanel.SetActive(visible);
        }
    }
    
    private System.Collections.IEnumerator AnimateVisibility(bool show)
    {
        float targetAlpha = show ? 1f : 0f;
        float startAlpha = _canvasGroup.alpha;
        
        if (show)
            _questTrackerPanel.SetActive(true);
        
        float elapsed = 0f;
        while (elapsed < 1f / _fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed * _fadeSpeed;
            _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, progress);
            yield return null;
        }
        
        _canvasGroup.alpha = targetAlpha;
        
        if (!show)
            _questTrackerPanel.SetActive(false);
    }
    
    private System.Collections.IEnumerator AnimateEntryAppearance(GameObject entry)
    {
        Transform entryTransform = entry.transform;
        Vector3 originalScale = entryTransform.localScale;
        entryTransform.localScale = Vector3.zero;
        
        float elapsed = 0f;
        float duration = 0.3f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            float scaleValue = _scaleCurve.Evaluate(progress);
            entryTransform.localScale = originalScale * scaleValue;
            yield return null;
        }
        
        entryTransform.localScale = originalScale;
    }
    
    private System.Collections.IEnumerator AnimateQuestCompletion(string questId)
    {
        yield return new WaitForSeconds(2f);
        
        if (!_showCompletedQuests)
        {
            RemoveQuest(questId);
        }
    }
}

public class QuestEntryUI : MonoBehaviour
{
    private Text _titleText;
    private Text _descriptionText;
    private Transform _objectivesContainer;
    private GameObject _objectivePrefab;
    
    private void Awake()
    {
        FindUIComponents();
    }
    
    private void FindUIComponents()
    {
        _titleText = transform.Find("Title")?.GetComponent<Text>();
        _descriptionText = transform.Find("Description")?.GetComponent<Text>();
        _objectivesContainer = transform.Find("Objectives");
        
        if (_objectivesContainer != null && _objectivesContainer.childCount > 0)
        {
            _objectivePrefab = _objectivesContainer.GetChild(0).gameObject;
            _objectivePrefab.SetActive(false);
        }
    }
    
    public void SetupQuestEntry(QuestTrackerDisplay.Quest quest, Color completedColor, Color activeColor, Color objectiveCompletedColor)
    {
        if (_titleText != null)
        {
            _titleText.text = quest.title;
            _titleText.color = quest.isCompleted ? completedColor : activeColor;
        }
        
        if (_descriptionText != null)
        {
            _descriptionText.text = quest.description;
            _descriptionText.color = quest.isCompleted ? completedColor : activeColor;
        }
        
        SetupObjectives(quest.objectives, objectiveCompletedColor);
    }
    
    private void SetupObjectives(List<QuestTrackerDisplay.QuestObjective> objectives, Color completedColor)
    {
        if (_objectivesContainer == null || _objectivePrefab == null)
            return;
            
        foreach (Transform child in _objectivesContainer)
        {
            if (child.gameObject != _objectivePrefab)
                DestroyImmediate(child.gameObject);
        }
        
        foreach (var objective in objectives)
        {
            GameObject objEntry = Instantiate(_objectivePrefab, _objectivesContainer);
            objEntry.SetActive(true);
            
            Text objText = objEntry.GetComponent<Text>();
            if (objText != null)
            {
                string progressText = objective.targetCount > 1 ? 
                    $" ({objective.currentCount}/{objective.targetCount})" : "";
                objText.text = "• " + objective.description + progressText;
                objText.color = objective.isCompleted ? completedColor : Color.white;
            }
        }
    }
}