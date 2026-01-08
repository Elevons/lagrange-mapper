// Prompt: scroll that teaches new ability
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class AbilityScroll : MonoBehaviour
{
    [Header("Scroll Settings")]
    [SerializeField] private string _abilityName = "New Ability";
    [SerializeField] private string _abilityDescription = "You have learned a new ability!";
    [SerializeField] private Sprite _abilityIcon;
    [SerializeField] private KeyCode _abilityKey = KeyCode.Q;
    [SerializeField] private float _abilityCooldown = 5f;
    [SerializeField] private float _abilityDuration = 3f;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _glowEffect;
    [SerializeField] private ParticleSystem _learnEffect;
    [SerializeField] private AudioClip _learnSound;
    [SerializeField] private float _floatSpeed = 1f;
    [SerializeField] private float _floatHeight = 0.5f;
    
    [Header("UI")]
    [SerializeField] private Canvas _abilityUI;
    [SerializeField] private UnityEngine.UI.Text _abilityNameText;
    [SerializeField] private UnityEngine.UI.Text _abilityDescriptionText;
    [SerializeField] private UnityEngine.UI.Image _abilityIconImage;
    [SerializeField] private UnityEngine.UI.Text _keyBindText;
    [SerializeField] private float _uiDisplayTime = 3f;
    
    [Header("Events")]
    public UnityEvent<string> OnAbilityLearned;
    public UnityEvent OnScrollCollected;
    
    private Vector3 _startPosition;
    private bool _isLearned = false;
    private AudioSource _audioSource;
    private Collider _collider;
    private Renderer _renderer;
    private GameObject _currentPlayer;
    
    [System.Serializable]
    public class LearnedAbility
    {
        public string name;
        public string description;
        public KeyCode keyCode;
        public float cooldown;
        public float duration;
        public bool isActive;
        public float lastUsedTime;
        
        public bool CanUse()
        {
            return Time.time >= lastUsedTime + cooldown;
        }
        
        public void Use()
        {
            lastUsedTime = Time.time;
        }
    }
    
    private static System.Collections.Generic.List<LearnedAbility> _learnedAbilities = new System.Collections.Generic.List<LearnedAbility>();
    
    void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        _collider = GetComponent<Collider>();
        _renderer = GetComponent<Renderer>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<SphereCollider>();
            _collider.isTrigger = true;
        }
        
        if (_glowEffect != null)
        {
            _glowEffect.SetActive(true);
        }
        
        if (_abilityUI != null)
        {
            _abilityUI.gameObject.SetActive(false);
        }
    }
    
    void Update()
    {
        if (!_isLearned)
        {
            FloatAnimation();
        }
        
        HandleAbilityInput();
    }
    
    void FloatAnimation()
    {
        float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatHeight;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);
        
        if (_glowEffect != null)
        {
            _glowEffect.transform.Rotate(0, 50f * Time.deltaTime, 0);
        }
    }
    
    void HandleAbilityInput()
    {
        if (_currentPlayer == null) return;
        
        foreach (var ability in _learnedAbilities)
        {
            if (Input.GetKeyDown(ability.keyCode) && ability.CanUse())
            {
                ActivateAbility(ability);
            }
        }
    }
    
    void ActivateAbility(LearnedAbility ability)
    {
        ability.Use();
        ability.isActive = true;
        
        StartCoroutine(DeactivateAbilityAfterDuration(ability));
        
        Debug.Log($"Activated ability: {ability.name}");
    }
    
    System.Collections.IEnumerator DeactivateAbilityAfterDuration(LearnedAbility ability)
    {
        yield return new WaitForSeconds(ability.duration);
        ability.isActive = false;
        Debug.Log($"Deactivated ability: {ability.name}");
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_isLearned) return;
        
        if (other.CompareTag("Player"))
        {
            LearnAbility(other.gameObject);
        }
    }
    
    void LearnAbility(GameObject player)
    {
        _isLearned = true;
        _currentPlayer = player;
        
        LearnedAbility newAbility = new LearnedAbility
        {
            name = _abilityName,
            description = _abilityDescription,
            keyCode = _abilityKey,
            cooldown = _abilityCooldown,
            duration = _abilityDuration,
            isActive = false,
            lastUsedTime = -_abilityCooldown
        };
        
        _learnedAbilities.Add(newAbility);
        
        PlayLearnEffects();
        ShowAbilityUI();
        
        OnAbilityLearned?.Invoke(_abilityName);
        OnScrollCollected?.Invoke();
        
        if (_collider != null)
        {
            _collider.enabled = false;
        }
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        if (_glowEffect != null)
        {
            _glowEffect.SetActive(false);
        }
        
        Destroy(gameObject, 2f);
    }
    
    void PlayLearnEffects()
    {
        if (_learnEffect != null)
        {
            _learnEffect.Play();
        }
        
        if (_learnSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_learnSound);
        }
    }
    
    void ShowAbilityUI()
    {
        if (_abilityUI == null) return;
        
        _abilityUI.gameObject.SetActive(true);
        
        if (_abilityNameText != null)
        {
            _abilityNameText.text = _abilityName;
        }
        
        if (_abilityDescriptionText != null)
        {
            _abilityDescriptionText.text = _abilityDescription;
        }
        
        if (_abilityIconImage != null && _abilityIcon != null)
        {
            _abilityIconImage.sprite = _abilityIcon;
        }
        
        if (_keyBindText != null)
        {
            _keyBindText.text = $"Press {_abilityKey} to use";
        }
        
        StartCoroutine(HideUIAfterDelay());
    }
    
    System.Collections.IEnumerator HideUIAfterDelay()
    {
        yield return new WaitForSeconds(_uiDisplayTime);
        
        if (_abilityUI != null)
        {
            _abilityUI.gameObject.SetActive(false);
        }
    }
    
    public static bool HasAbility(string abilityName)
    {
        return _learnedAbilities.Exists(a => a.name == abilityName);
    }
    
    public static bool IsAbilityActive(string abilityName)
    {
        var ability = _learnedAbilities.Find(a => a.name == abilityName);
        return ability != null && ability.isActive;
    }
    
    public static float GetAbilityCooldownRemaining(string abilityName)
    {
        var ability = _learnedAbilities.Find(a => a.name == abilityName);
        if (ability == null) return 0f;
        
        float timeRemaining = (ability.lastUsedTime + ability.cooldown) - Time.time;
        return Mathf.Max(0f, timeRemaining);
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 1f);
        
        Gizmos.color = Color.blue;
        Vector3 floatPos = transform.position + Vector3.up * _floatHeight;
        Gizmos.DrawWireSphere(floatPos, 0.1f);
        
        Vector3 floatPosDown = transform.position - Vector3.up * _floatHeight;
        Gizmos.DrawWireSphere(floatPosDown, 0.1f);
    }
}