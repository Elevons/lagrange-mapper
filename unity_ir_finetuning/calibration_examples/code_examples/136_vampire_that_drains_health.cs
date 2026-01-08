// Prompt: vampire that drains health
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Vampire : MonoBehaviour
{
    [Header("Vampire Settings")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    [SerializeField] private float _drainAmount = 10f;
    [SerializeField] private float _drainInterval = 1f;
    [SerializeField] private float _drainRange = 3f;
    [SerializeField] private float _healPercentage = 0.5f;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _chaseSpeed = 4f;
    [SerializeField] private float _detectionRange = 5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _drainEffect;
    [SerializeField] private LineRenderer _drainBeam;
    [SerializeField] private Color _drainColor = Color.red;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _drainSound;
    [SerializeField] private AudioClip _deathSound;
    
    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    public UnityEvent<GameObject> OnStartDraining;
    public UnityEvent<GameObject> OnStopDraining;
    
    private Transform _target;
    private bool _isDraining;
    private Coroutine _drainCoroutine;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private bool _isDead;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_drainBeam != null)
        {
            _drainBeam.enabled = false;
            _drainBeam.startColor = _drainColor;
            _drainBeam.endColor = _drainColor;
            _drainBeam.startWidth = 0.1f;
            _drainBeam.endWidth = 0.05f;
        }
        
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    private void Update()
    {
        if (_isDead) return;
        
        FindTarget();
        HandleMovement();
        HandleDraining();
    }
    
    private void FindTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        Transform closestTarget = null;
        float closestDistance = _detectionRange;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = player.transform;
            }
        }
        
        _target = closestTarget;
    }
    
    private void HandleMovement()
    {
        if (_target == null || _isDraining) return;
        
        Vector3 direction = (_target.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, _target.position);
        
        if (distance > _drainRange)
        {
            Vector3 movement = direction * _chaseSpeed * Time.deltaTime;
            transform.position += movement;
            transform.LookAt(_target);
            
            if (_animator != null)
                _animator.SetBool("IsMoving", true);
        }
        else
        {
            if (_animator != null)
                _animator.SetBool("IsMoving", false);
        }
    }
    
    private void HandleDraining()
    {
        if (_target == null) return;
        
        float distance = Vector3.Distance(transform.position, _target.position);
        
        if (distance <= _drainRange && !_isDraining)
        {
            StartDraining();
        }
        else if (distance > _drainRange && _isDraining)
        {
            StopDraining();
        }
    }
    
    private void StartDraining()
    {
        if (_isDraining || _target == null) return;
        
        _isDraining = true;
        _drainCoroutine = StartCoroutine(DrainHealthCoroutine());
        
        if (_drainEffect != null)
            _drainEffect.Play();
            
        if (_drainBeam != null)
        {
            _drainBeam.enabled = true;
            _drainBeam.SetPosition(0, transform.position + Vector3.up);
            _drainBeam.SetPosition(1, _target.position + Vector3.up);
        }
        
        if (_animator != null)
            _animator.SetBool("IsDraining", true);
            
        OnStartDraining?.Invoke(_target.gameObject);
    }
    
    private void StopDraining()
    {
        if (!_isDraining) return;
        
        _isDraining = false;
        
        if (_drainCoroutine != null)
        {
            StopCoroutine(_drainCoroutine);
            _drainCoroutine = null;
        }
        
        if (_drainEffect != null)
            _drainEffect.Stop();
            
        if (_drainBeam != null)
            _drainBeam.enabled = false;
            
        if (_animator != null)
            _animator.SetBool("IsDraining", false);
            
        OnStopDraining?.Invoke(_target?.gameObject);
    }
    
    private IEnumerator DrainHealthCoroutine()
    {
        while (_isDraining && _target != null)
        {
            DrainTargetHealth();
            
            if (_drainBeam != null)
            {
                _drainBeam.SetPosition(0, transform.position + Vector3.up);
                _drainBeam.SetPosition(1, _target.position + Vector3.up);
            }
            
            yield return new WaitForSeconds(_drainInterval);
        }
    }
    
    private void DrainTargetHealth()
    {
        if (_target == null) return;
        
        // Try to find a health component or use a simple health system
        PlayerHealthComponent targetHealth = _target.GetComponent<PlayerHealthComponent>();
        if (targetHealth != null)
        {
            targetHealth.TakeDamage(_drainAmount);
        }
        else
        {
            // Send damage message to target
            _target.SendMessage("TakeDamage", _drainAmount, SendMessageOptions.DontRequireReceiver);
        }
        
        // Heal vampire
        float healAmount = _drainAmount * _healPercentage;
        Heal(healAmount);
        
        // Play drain sound
        if (_audioSource != null && _drainSound != null)
            _audioSource.PlayOneShot(_drainSound);
    }
    
    public void TakeDamage(float damage)
    {
        if (_isDead) return;
        
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth);
        
        if (_currentHealth <= 0f)
        {
            Die();
        }
    }
    
    public void Heal(float amount)
    {
        if (_isDead) return;
        
        _currentHealth += amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    private void Die()
    {
        if (_isDead) return;
        
        _isDead = true;
        StopDraining();
        
        if (_audioSource != null && _deathSound != null)
            _audioSource.PlayOneShot(_deathSound);
            
        if (_animator != null)
            _animator.SetTrigger("Die");
            
        OnDeath?.Invoke();
        
        // Disable components
        if (_rigidbody != null)
            _rigidbody.isKinematic = true;
            
        GetComponent<Collider>().enabled = false;
        
        // Destroy after delay
        Destroy(gameObject, 3f);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        // Draw drain range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _drainRange);
    }
}

[System.Serializable]
public class PlayerHealthComponent : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;
    
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;
    
    private void Start()
    {
        _currentHealth = _maxHealth;
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth);
        
        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }
    
    public void Heal(float amount)
    {
        _currentHealth += amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    public float GetCurrentHealth()
    {
        return _currentHealth;
    }
    
    public float GetMaxHealth()
    {
        return _maxHealth;
    }
}