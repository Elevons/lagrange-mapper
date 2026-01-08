// Prompt: flame jet trap
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class FlameJetTrap : MonoBehaviour
{
    [Header("Flame Settings")]
    [SerializeField] private float _flameDuration = 2f;
    [SerializeField] private float _cooldownDuration = 3f;
    [SerializeField] private float _damage = 25f;
    [SerializeField] private float _damageInterval = 0.5f;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRadius = 5f;
    [SerializeField] private LayerMask _targetLayerMask = -1;
    [SerializeField] private bool _autoActivate = true;
    [SerializeField] private float _activationDelay = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _flameParticles;
    [SerializeField] private Light _flameLight;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _igniteSound;
    [SerializeField] private AudioClip _flameLoopSound;
    [SerializeField] private AudioClip _extinguishSound;
    
    [Header("Warning System")]
    [SerializeField] private GameObject _warningIndicator;
    [SerializeField] private float _warningDuration = 1f;
    [SerializeField] private AudioClip _warningSound;
    
    [Header("Events")]
    public UnityEvent OnFlameActivated;
    public UnityEvent OnFlameDeactivated;
    public UnityEvent OnTargetDamaged;
    
    private enum TrapState
    {
        Idle,
        Warning,
        Active,
        Cooldown
    }
    
    private TrapState _currentState = TrapState.Idle;
    private float _stateTimer;
    private float _damageTimer;
    private bool _playerInRange;
    private Collider _trapCollider;
    
    private void Start()
    {
        _trapCollider = GetComponent<Collider>();
        if (_trapCollider == null)
        {
            _trapCollider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)_trapCollider).radius = _detectionRadius;
            _trapCollider.isTrigger = true;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        InitializeComponents();
        SetState(TrapState.Idle);
    }
    
    private void InitializeComponents()
    {
        if (_flameParticles != null)
        {
            var main = _flameParticles.main;
            main.loop = true;
            _flameParticles.Stop();
        }
        
        if (_flameLight != null)
            _flameLight.enabled = false;
        
        if (_warningIndicator != null)
            _warningIndicator.SetActive(false);
    }
    
    private void Update()
    {
        UpdateStateMachine();
        
        if (_currentState == TrapState.Active)
        {
            UpdateDamage();
        }
    }
    
    private void UpdateStateMachine()
    {
        _stateTimer -= Time.deltaTime;
        
        switch (_currentState)
        {
            case TrapState.Idle:
                if (_autoActivate && _playerInRange)
                {
                    SetState(TrapState.Warning);
                }
                break;
                
            case TrapState.Warning:
                if (_stateTimer <= 0f)
                {
                    SetState(TrapState.Active);
                }
                break;
                
            case TrapState.Active:
                if (_stateTimer <= 0f)
                {
                    SetState(TrapState.Cooldown);
                }
                break;
                
            case TrapState.Cooldown:
                if (_stateTimer <= 0f)
                {
                    SetState(TrapState.Idle);
                }
                break;
        }
    }
    
    private void SetState(TrapState newState)
    {
        _currentState = newState;
        
        switch (newState)
        {
            case TrapState.Idle:
                _stateTimer = 0f;
                DeactivateFlame();
                DeactivateWarning();
                break;
                
            case TrapState.Warning:
                _stateTimer = _warningDuration;
                ActivateWarning();
                break;
                
            case TrapState.Active:
                _stateTimer = _flameDuration;
                _damageTimer = 0f;
                ActivateFlame();
                DeactivateWarning();
                break;
                
            case TrapState.Cooldown:
                _stateTimer = _cooldownDuration;
                DeactivateFlame();
                break;
        }
    }
    
    private void ActivateWarning()
    {
        if (_warningIndicator != null)
            _warningIndicator.SetActive(true);
        
        if (_audioSource != null && _warningSound != null)
            _audioSource.PlayOneShot(_warningSound);
    }
    
    private void DeactivateWarning()
    {
        if (_warningIndicator != null)
            _warningIndicator.SetActive(false);
    }
    
    private void ActivateFlame()
    {
        if (_flameParticles != null)
            _flameParticles.Play();
        
        if (_flameLight != null)
            _flameLight.enabled = true;
        
        if (_audioSource != null)
        {
            if (_igniteSound != null)
                _audioSource.PlayOneShot(_igniteSound);
            
            if (_flameLoopSound != null)
            {
                _audioSource.clip = _flameLoopSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
        }
        
        OnFlameActivated?.Invoke();
    }
    
    private void DeactivateFlame()
    {
        if (_flameParticles != null)
            _flameParticles.Stop();
        
        if (_flameLight != null)
            _flameLight.enabled = false;
        
        if (_audioSource != null)
        {
            if (_audioSource.isPlaying && _audioSource.clip == _flameLoopSound)
                _audioSource.Stop();
            
            if (_extinguishSound != null)
                _audioSource.PlayOneShot(_extinguishSound);
        }
        
        OnFlameDeactivated?.Invoke();
    }
    
    private void UpdateDamage()
    {
        _damageTimer -= Time.deltaTime;
        
        if (_damageTimer <= 0f)
        {
            _damageTimer = _damageInterval;
            DamageTargetsInRange();
        }
    }
    
    private void DamageTargetsInRange()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, _detectionRadius, _targetLayerMask);
        
        foreach (Collider target in targets)
        {
            if (target.CompareTag("Player"))
            {
                // Apply damage to player
                var playerHealth = target.GetComponent<MonoBehaviour>();
                if (playerHealth != null)
                {
                    // Send damage message
                    target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
                }
                
                OnTargetDamaged?.Invoke();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & _targetLayerMask) != 0)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = true;
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (((1 << other.gameObject.layer) & _targetLayerMask) != 0)
        {
            if (other.CompareTag("Player"))
            {
                _playerInRange = false;
            }
        }
    }
    
    public void ManualActivate()
    {
        if (_currentState == TrapState.Idle)
        {
            SetState(TrapState.Warning);
        }
    }
    
    public void ForceActivate()
    {
        SetState(TrapState.Active);
    }
    
    public void ForceDeactivate()
    {
        SetState(TrapState.Idle);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        if (_currentState == TrapState.Active)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position, 0.5f);
        }
    }
}