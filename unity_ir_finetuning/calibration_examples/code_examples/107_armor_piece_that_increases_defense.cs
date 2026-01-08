// Prompt: armor piece that increases defense
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class ArmorPiece : MonoBehaviour
{
    [Header("Armor Properties")]
    [SerializeField] private string _armorName = "Basic Armor";
    [SerializeField] private int _defenseBonus = 10;
    [SerializeField] private ArmorType _armorType = ArmorType.Chest;
    [SerializeField] private bool _isEquipped = false;
    
    [Header("Visual Settings")]
    [SerializeField] private GameObject _visualModel;
    [SerializeField] private Material _equippedMaterial;
    [SerializeField] private Material _unequippedMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _unequipSound;
    
    [Header("Events")]
    public UnityEvent<int> OnDefenseChanged;
    public UnityEvent<string> OnArmorEquipped;
    public UnityEvent<string> OnArmorUnequipped;
    
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Collider _collider;
    private GameObject _currentWearer;
    
    public enum ArmorType
    {
        Helmet,
        Chest,
        Legs,
        Boots,
        Gloves,
        Shield
    }
    
    public int DefenseBonus => _defenseBonus;
    public ArmorType Type => _armorType;
    public bool IsEquipped => _isEquipped;
    public string ArmorName => _armorName;
    public GameObject CurrentWearer => _currentWearer;
    
    private void Awake()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        
        if (_collider == null)
        {
            _collider = gameObject.AddComponent<BoxCollider>();
            _collider.isTrigger = true;
        }
    }
    
    private void Start()
    {
        UpdateVisualState();
        
        if (!_isEquipped && _collider != null)
            _collider.enabled = true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isEquipped && other.CompareTag("Player"))
        {
            TryEquipArmor(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (_isEquipped && other.CompareTag("Player") && other.gameObject == _currentWearer)
        {
            // Optional: Auto-unequip when player moves away
            // UnequipArmor();
        }
    }
    
    public bool TryEquipArmor(GameObject wearer)
    {
        if (_isEquipped || wearer == null)
            return false;
            
        // Check if wearer already has this armor type equipped
        ArmorPiece[] existingArmor = wearer.GetComponentsInChildren<ArmorPiece>();
        foreach (ArmorPiece armor in existingArmor)
        {
            if (armor != this && armor.IsEquipped && armor.Type == _armorType)
            {
                armor.UnequipArmor();
                break;
            }
        }
        
        EquipArmor(wearer);
        return true;
    }
    
    public void EquipArmor(GameObject wearer)
    {
        if (_isEquipped || wearer == null)
            return;
            
        _isEquipped = true;
        _currentWearer = wearer;
        
        // Attach to wearer
        transform.SetParent(wearer.transform);
        transform.localPosition = GetArmorPosition();
        transform.localRotation = Quaternion.identity;
        
        // Disable collider when equipped
        if (_collider != null)
            _collider.enabled = false;
            
        // Apply defense bonus
        ApplyDefenseBonus(wearer, _defenseBonus);
        
        // Update visuals and audio
        UpdateVisualState();
        PlayEquipSound();
        
        // Trigger events
        OnDefenseChanged?.Invoke(_defenseBonus);
        OnArmorEquipped?.Invoke(_armorName);
        
        Debug.Log($"{_armorName} equipped! Defense bonus: +{_defenseBonus}");
    }
    
    public void UnequipArmor()
    {
        if (!_isEquipped)
            return;
            
        GameObject previousWearer = _currentWearer;
        
        // Remove defense bonus
        if (previousWearer != null)
            ApplyDefenseBonus(previousWearer, -_defenseBonus);
            
        _isEquipped = false;
        _currentWearer = null;
        
        // Detach from wearer
        transform.SetParent(null);
        
        // Re-enable collider
        if (_collider != null)
            _collider.enabled = true;
            
        // Update visuals and audio
        UpdateVisualState();
        PlayUnequipSound();
        
        // Trigger events
        OnDefenseChanged?.Invoke(-_defenseBonus);
        OnArmorUnequipped?.Invoke(_armorName);
        
        Debug.Log($"{_armorName} unequipped! Defense bonus removed: -{_defenseBonus}");
    }
    
    private void ApplyDefenseBonus(GameObject target, int bonus)
    {
        // Store defense bonus in a component on the target
        DefenseTracker tracker = target.GetComponent<DefenseTracker>();
        if (tracker == null)
            tracker = target.AddComponent<DefenseTracker>();
            
        tracker.ModifyDefense(bonus);
    }
    
    private Vector3 GetArmorPosition()
    {
        switch (_armorType)
        {
            case ArmorType.Helmet:
                return new Vector3(0, 1.8f, 0);
            case ArmorType.Chest:
                return new Vector3(0, 1.2f, 0);
            case ArmorType.Legs:
                return new Vector3(0, 0.6f, 0);
            case ArmorType.Boots:
                return new Vector3(0, 0.1f, 0);
            case ArmorType.Gloves:
                return new Vector3(0.3f, 1.0f, 0);
            case ArmorType.Shield:
                return new Vector3(-0.5f, 1.0f, 0);
            default:
                return Vector3.zero;
        }
    }
    
    private void UpdateVisualState()
    {
        if (_renderer != null)
        {
            if (_isEquipped && _equippedMaterial != null)
                _renderer.material = _equippedMaterial;
            else if (!_isEquipped && _unequippedMaterial != null)
                _renderer.material = _unequippedMaterial;
        }
        
        if (_visualModel != null)
            _visualModel.SetActive(_isEquipped);
    }
    
    private void PlayEquipSound()
    {
        if (_audioSource != null && _equipSound != null)
        {
            _audioSource.clip = _equipSound;
            _audioSource.Play();
        }
    }
    
    private void PlayUnequipSound()
    {
        if (_audioSource != null && _unequipSound != null)
        {
            _audioSource.clip = _unequipSound;
            _audioSource.Play();
        }
    }
    
    private void OnValidate()
    {
        _defenseBonus = Mathf.Max(0, _defenseBonus);
    }
    
    [System.Serializable]
    public class DefenseTracker : MonoBehaviour
    {
        [SerializeField] private int _totalDefense = 0;
        
        public int TotalDefense => _totalDefense;
        
        public void ModifyDefense(int amount)
        {
            _totalDefense += amount;
            _totalDefense = Mathf.Max(0, _totalDefense);
        }
        
        public void SetDefense(int value)
        {
            _totalDefense = Mathf.Max(0, value);
        }
    }
}