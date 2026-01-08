// Prompt: campfire that restores health over time
// Type: pickup

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Campfire : MonoBehaviour
{
    [Header("Healing Settings")]
    [SerializeField] private float _healingRate = 10f;
    [SerializeField] private float _healingInterval = 1f;
    [SerializeField] private float _healingRadius = 3f;
    [SerializeField] private float _maxHealth = 100f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _fireParticles;
    [SerializeField] private Light _fireLight;
    [SerializeField] private AudioSource _fireAudioSource;
    [SerializeField] private GameObject _healingEffect;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnPlayerEnterHealing;
    public UnityEvent<GameObject> OnPlayerExitHealing;
    public UnityEvent<GameObject, float> OnPlayerHealed;
    
    private bool _isActive = true;
    private Coroutine _healingCoroutine;
    
    [System.Serializable]
    public class PlayerHealthData
    {
        public GameObject player;
        public float currentHealth;
        public bool isHealing;
        
        public PlayerHealthData(GameObject playerObj)
        {
            player = playerObj;
            currentHealth = 100f;
            isHealing = false;
        }
    }
    
    private System.Collections.Generic.List<PlayerHealthData> _playersInRange = 
        new System.Collections.Generic.List<PlayerHealthData>();
    
    private void Start()
    {
        SetupComponents();
        StartHealing();
    }
    
    private void SetupComponents()
    {
        if (_fireParticles == null)
            _fireParticles = GetComponentInChildren<ParticleSystem>();
            
        if (_fireLight == null)
            _fireLight = GetComponentInChildren<Light>();
            
        if (_fireAudioSource == null)
            _fireAudioSource = GetComponent<AudioSource>();
            
        if (_fireAudioSource != null)
        {
            _fireAudioSource.loop = true;
            _fireAudioSource.Play();
        }
        
        SphereCollider trigger = GetComponent<SphereCollider>();
        if (trigger == null)
        {
            trigger = gameObject.AddComponent<SphereCollider>();
        }
        trigger.isTrigger = true;
        trigger.radius = _healingRadius;
    }
    
    private void StartHealing()
    {
        if (_healingCoroutine == null)
        {
            _healingCoroutine = StartCoroutine(HealingLoop());
        }
    }
    
    private void StopHealing()
    {
        if (_healingCoroutine != null)
        {
            StopCoroutine(_healingCoroutine);
            _healingCoroutine = null;
        }
    }
    
    private IEnumerator HealingLoop()
    {
        while (_isActive)
        {
            yield return new WaitForSeconds(_healingInterval);
            
            for (int i = _playersInRange.Count - 1; i >= 0; i--)
            {
                if (_playersInRange[i].player == null)
                {
                    _playersInRange.RemoveAt(i);
                    continue;
                }
                
                HealPlayer(_playersInRange[i]);
            }
        }
    }
    
    private void HealPlayer(PlayerHealthData playerData)
    {
        if (playerData.currentHealth >= _maxHealth) return;
        
        float previousHealth = playerData.currentHealth;
        playerData.currentHealth = Mathf.Min(playerData.currentHealth + _healingRate, _maxHealth);
        
        float healedAmount = playerData.currentHealth - previousHealth;
        if (healedAmount > 0)
        {
            OnPlayerHealed?.Invoke(playerData.player, healedAmount);
            ShowHealingEffect(playerData.player);
        }
    }
    
    private void ShowHealingEffect(GameObject player)
    {
        if (_healingEffect != null)
        {
            GameObject effect = Instantiate(_healingEffect, player.transform.position + Vector3.up, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        PlayerHealthData existingData = _playersInRange.Find(p => p.player == other.gameObject);
        if (existingData == null)
        {
            PlayerHealthData newPlayerData = new PlayerHealthData(other.gameObject);
            _playersInRange.Add(newPlayerData);
            OnPlayerEnterHealing?.Invoke(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        PlayerHealthData playerData = _playersInRange.Find(p => p.player == other.gameObject);
        if (playerData != null)
        {
            _playersInRange.Remove(playerData);
            OnPlayerExitHealing?.Invoke(other.gameObject);
        }
    }
    
    public void SetActive(bool active)
    {
        _isActive = active;
        
        if (_fireParticles != null)
        {
            if (active)
                _fireParticles.Play();
            else
                _fireParticles.Stop();
        }
        
        if (_fireLight != null)
            _fireLight.enabled = active;
            
        if (_fireAudioSource != null)
        {
            if (active)
                _fireAudioSource.Play();
            else
                _fireAudioSource.Stop();
        }
        
        if (active)
            StartHealing();
        else
            StopHealing();
    }
    
    public float GetPlayerHealth(GameObject player)
    {
        PlayerHealthData playerData = _playersInRange.Find(p => p.player == player);
        return playerData?.currentHealth ?? 0f;
    }
    
    public void SetPlayerHealth(GameObject player, float health)
    {
        PlayerHealthData playerData = _playersInRange.Find(p => p.player == player);
        if (playerData != null)
        {
            playerData.currentHealth = Mathf.Clamp(health, 0f, _maxHealth);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _healingRadius);
    }
    
    private void OnDestroy()
    {
        StopHealing();
    }
}