// Prompt: guard that alerts others when attacked
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class GuardAlertSystem : MonoBehaviour
{
    [Header("Guard Settings")]
    [SerializeField] private float _health = 100f;
    [SerializeField] private float _alertRadius = 15f;
    [SerializeField] private LayerMask _guardLayerMask = -1;
    [SerializeField] private float _alertDuration = 10f;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _alertIndicator;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _alertedColor = Color.red;
    [SerializeField] private Renderer _guardRenderer;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _alertSound;
    [SerializeField] private AudioClip _damageSound;
    
    [Header("Events")]
    public UnityEvent OnGuardAttacked;
    public UnityEvent OnGuardAlerted;
    public UnityEvent OnGuardDeath;
    
    private bool _isAlerted = false;
    private bool _isDead = false;
    private float _maxHealth;
    private float _alertTimer = 0f;
    private List<GuardAlertSystem> _nearbyGuards = new List<GuardAlertSystem>();
    
    public bool IsAlerted => _isAlerted;
    public bool IsDead => _isDead;
    public float HealthPercentage => _health / _maxHealth;
    
    private void Start()
    {
        _maxHealth = _health;
        
        if (_guardRenderer == null)
            _guardRenderer = GetComponent<Renderer>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_alertIndicator != null)
            _alertIndicator.SetActive(false);
            
        UpdateVisualState();
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        if (_isAlerted)
        {
            _alertTimer -= Time.deltaTime;
            if (_alertTimer <= 0f)
            {
                CalmDown();
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _health -= damage;
        
        PlaySound(_damageSound);
        
        if (_health <= 0f)
        {
            Die();
        }
        else
        {
            TriggerAlert();
        }
        
        OnGuardAttacked?.Invoke();
    }
    
    private void TriggerAlert()
    {
        if (_isAlerted) return;
        
        _isAlerted = true;
        _alertTimer = _alertDuration;
        
        PlaySound(_alertSound);
        UpdateVisualState();
        
        AlertNearbyGuards();
        OnGuardAlerted?.Invoke();
    }
    
    public void ReceiveAlert()
    {
        if (_isDead || _isAlerted) return;
        
        _isAlerted = true;
        _alertTimer = _alertDuration;
        
        UpdateVisualState();
        OnGuardAlerted?.Invoke();
    }
    
    private void AlertNearbyGuards()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _alertRadius, _guardLayerMask);
        
        _nearbyGuards.Clear();
        
        foreach (Collider col in nearbyColliders)
        {
            if (col.gameObject == gameObject) continue;
            
            GuardAlertSystem otherGuard = col.GetComponent<GuardAlertSystem>();
            if (otherGuard != null && !otherGuard.IsDead)
            {
                _nearbyGuards.Add(otherGuard);
                otherGuard.ReceiveAlert();
            }
        }
    }
    
    private void CalmDown()
    {
        _isAlerted = false;
        _alertTimer = 0f;
        UpdateVisualState();
    }
    
    private void Die()
    {
        _isDead = true;
        _health = 0f;
        _isAlerted = false;
        
        UpdateVisualState();
        OnGuardDeath?.Invoke();
        
        if (GetComponent<Collider>() != null)
            GetComponent<Collider>().enabled = false;
    }
    
    private void UpdateVisualState()
    {
        if (_guardRenderer != null)
        {
            if (_isDead)
            {
                _guardRenderer.material.color = Color.gray;
            }
            else if (_isAlerted)
            {
                _guardRenderer.material.color = _alertedColor;
            }
            else
            {
                _guardRenderer.material.color = _normalColor;
            }
        }
        
        if (_alertIndicator != null)
        {
            _alertIndicator.SetActive(_isAlerted && !_isDead);
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isDead) return;
        
        if (other.CompareTag("Player") && _isAlerted)
        {
            // Player detected while alerted - could trigger additional behavior
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _alertRadius);
        
        if (_isAlerted)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, _alertRadius * 0.5f);
        }
    }
    
    public void Heal(float amount)
    {
        if (_isDead) return;
        
        _health = Mathf.Min(_health + amount, _maxHealth);
    }
    
    public void SetAlertState(bool alerted)
    {
        if (_isDead) return;
        
        if (alerted && !_isAlerted)
        {
            TriggerAlert();
        }
        else if (!alerted && _isAlerted)
        {
            CalmDown();
        }
    }
    
    public List<GuardAlertSystem> GetNearbyGuards()
    {
        return new List<GuardAlertSystem>(_nearbyGuards);
    }
}