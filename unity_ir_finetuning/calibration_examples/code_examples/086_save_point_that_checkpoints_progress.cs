// Prompt: save point that checkpoints progress
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System;

public class SavePoint : MonoBehaviour
{
    [Header("Save Point Settings")]
    [SerializeField] private float _activationRange = 2f;
    [SerializeField] private bool _autoSave = true;
    [SerializeField] private float _cooldownTime = 1f;
    [SerializeField] private string _savePointId = "";
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _activeIndicator;
    [SerializeField] private ParticleSystem _saveEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _saveSound;
    [SerializeField] private Color _activeColor = Color.green;
    [SerializeField] private Color _inactiveColor = Color.gray;
    
    [Header("Events")]
    public UnityEvent OnPlayerSaved;
    public UnityEvent OnSavePointActivated;
    
    private bool _isActivated = false;
    private bool _isOnCooldown = false;
    private float _cooldownTimer = 0f;
    private Transform _player;
    private Renderer _renderer;
    private Material _material;
    private Color _originalColor;
    
    [System.Serializable]
    public class SaveData
    {
        public Vector3 position;
        public Quaternion rotation;
        public string savePointId;
        public float timestamp;
        public int health;
        public int score;
        
        public SaveData(Vector3 pos, Quaternion rot, string id)
        {
            position = pos;
            rotation = rot;
            savePointId = id;
            timestamp = Time.time;
            health = 100;
            score = 0;
        }
    }
    
    private void Start()
    {
        InitializeSavePoint();
        LoadSavePointState();
    }
    
    private void Update()
    {
        HandleCooldown();
        CheckPlayerProximity();
        UpdateVisuals();
    }
    
    private void InitializeSavePoint()
    {
        if (string.IsNullOrEmpty(_savePointId))
        {
            _savePointId = "SavePoint_" + transform.position.ToString();
        }
        
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _material = _renderer.material;
            _originalColor = _material.color;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_activeIndicator != null)
        {
            _activeIndicator.SetActive(false);
        }
    }
    
    private void HandleCooldown()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
            }
        }
    }
    
    private void CheckPlayerProximity()
    {
        if (_player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.transform;
            }
            return;
        }
        
        float distance = Vector3.Distance(transform.position, _player.position);
        
        if (distance <= _activationRange && !_isOnCooldown)
        {
            if (_autoSave && !_isActivated)
            {
                ActivateSavePoint();
            }
        }
    }
    
    private void UpdateVisuals()
    {
        if (_material != null)
        {
            Color targetColor = _isActivated ? _activeColor : _inactiveColor;
            _material.color = Color.Lerp(_material.color, targetColor, Time.deltaTime * 2f);
        }
        
        if (_activeIndicator != null)
        {
            _activeIndicator.SetActive(_isActivated);
        }
    }
    
    public void ActivateSavePoint()
    {
        if (_isOnCooldown) return;
        
        _isActivated = true;
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
        
        SavePlayerProgress();
        PlaySaveEffects();
        
        OnSavePointActivated.Invoke();
        OnPlayerSaved.Invoke();
    }
    
    private void SavePlayerProgress()
    {
        if (_player == null) return;
        
        SaveData saveData = new SaveData(_player.position, _player.rotation, _savePointId);
        
        // Save additional player data if available
        GameObject playerObject = _player.gameObject;
        
        // Try to get health from various possible components
        var healthComponent = playerObject.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use reflection to find health-related fields
            var fields = healthComponent.GetType().GetFields();
            foreach (var field in fields)
            {
                if (field.Name.ToLower().Contains("health") && field.FieldType == typeof(int))
                {
                    saveData.health = (int)field.GetValue(healthComponent);
                    break;
                }
            }
        }
        
        string jsonData = JsonUtility.ToJson(saveData);
        PlayerPrefs.SetString("SavePoint_" + _savePointId, jsonData);
        PlayerPrefs.SetString("LastSavePoint", _savePointId);
        PlayerPrefs.Save();
        
        Debug.Log($"Progress saved at {_savePointId}");
    }
    
    public void LoadPlayerProgress()
    {
        string jsonData = PlayerPrefs.GetString("SavePoint_" + _savePointId, "");
        
        if (!string.IsNullOrEmpty(jsonData))
        {
            SaveData saveData = JsonUtility.FromJson<SaveData>(jsonData);
            
            if (_player != null)
            {
                // Disable character controller if present
                var characterController = _player.GetComponent<CharacterController>();
                if (characterController != null)
                {
                    characterController.enabled = false;
                }
                
                _player.position = saveData.position;
                _player.rotation = saveData.rotation;
                
                // Re-enable character controller
                if (characterController != null)
                {
                    characterController.enabled = true;
                }
                
                Debug.Log($"Progress loaded from {_savePointId}");
            }
        }
    }
    
    private void LoadSavePointState()
    {
        string lastSavePoint = PlayerPrefs.GetString("LastSavePoint", "");
        if (lastSavePoint == _savePointId)
        {
            _isActivated = true;
        }
    }
    
    private void PlaySaveEffects()
    {
        if (_saveEffect != null)
        {
            _saveEffect.Play();
        }
        
        if (_audioSource != null && _saveSound != null)
        {
            _audioSource.PlayOneShot(_saveSound);
        }
    }
    
    public static void LoadLastSavePoint()
    {
        string lastSavePointId = PlayerPrefs.GetString("LastSavePoint", "");
        
        if (!string.IsNullOrEmpty(lastSavePointId))
        {
            SavePoint[] savePoints = FindObjectsOfType<SavePoint>();
            
            foreach (SavePoint savePoint in savePoints)
            {
                if (savePoint._savePointId == lastSavePointId)
                {
                    savePoint.LoadPlayerProgress();
                    break;
                }
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_autoSave && !_isOnCooldown)
        {
            ActivateSavePoint();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _activeColor;
        Gizmos.DrawWireSphere(transform.position, _activationRange);
    }
    
    public bool IsActivated => _isActivated;
    public string SavePointId => _savePointId;
}