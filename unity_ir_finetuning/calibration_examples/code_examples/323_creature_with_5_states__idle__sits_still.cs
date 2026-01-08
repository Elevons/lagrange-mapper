// Prompt: creature with 5 states: idle (sits still, plays ambient breathing), curious (slowly approaches player if within 10 units, plays questioning sound), scared (runs away from player, plays panic sound, color turns red), aggressive (chases player, plays roar, color turns dark), and exhausted (stops moving, plays tired sound, color fades to gray)
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class CreatureStateMachine : MonoBehaviour
{
    [Header("Detection Settings")]
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private float _scareRange = 5f;
    [SerializeField] private float _aggressionRange = 3f;
    [SerializeField] private LayerMask _playerLayerMask = -1;
    
    [Header("Movement Settings")]
    [SerializeField] private float _curiousSpeed = 2f;
    [SerializeField] private float _scaredSpeed = 8f;
    [SerializeField] private float _aggressiveSpeed = 6f;
    [SerializeField] private float _rotationSpeed = 5f;
    
    [Header("State Duration Settings")]
    [SerializeField] private float _curiousStateDuration = 5f;
    [SerializeField] private float _scaredStateDuration = 3f;
    [SerializeField] private float _aggressiveStateDuration = 8f;
    [SerializeField] private float _exhaustedStateDuration = 4f;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip _breathingSound;
    [SerializeField] private AudioClip _questioningSound;
    [SerializeField] private AudioClip _panicSound;
    [SerializeField] private AudioClip _roarSound;
    [SerializeField] private AudioClip _tiredSound;
    [SerializeField] private float _ambientBreathingInterval = 3f;
    
    [Header("Visual Settings")]
    [SerializeField] private Renderer _creatureRenderer;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _scaredColor = Color.red;
    [SerializeField] private Color _aggressiveColor = Color.black;
    [SerializeField] private Color _exhaustedColor = Color.gray;
    [SerializeField] private float _colorTransitionSpeed = 2f;
    
    [Header("Events")]
    public UnityEvent OnStateChanged;
    public UnityEvent<CreatureState> OnSpecificStateEntered;
    
    public enum CreatureState
    {
        Idle,
        Curious,
        Scared,
        Aggressive,
        Exhausted
    }
    
    private CreatureState _currentState = CreatureState.Idle;
    private Transform _player;
    private AudioSource _audioSource;
    private Rigidbody _rigidbody;
    private Vector3 _initialPosition;
    private float _stateTimer;
    private float _breathingTimer;
    private Color _targetColor;
    private Color _currentColor;
    private bool _hasPlayedStateSound;
    
    public CreatureState CurrentState => _currentState;
    
    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_creatureRenderer == null)
        {
            _creatureRenderer = GetComponent<Renderer>();
        }
        
        _initialPosition = transform.position;
        _targetColor = _normalColor;
        _currentColor = _normalColor;
        
        if (_creatureRenderer != null)
        {
            _creatureRenderer.material.color = _currentColor;
        }
        
        FindPlayer();
        ChangeState(CreatureState.Idle);
    }
    
    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
        }
        
        UpdateStateLogic();
        UpdateVisuals();
        UpdateAudio();
        
        _stateTimer += Time.deltaTime;
        _breathingTimer += Time.deltaTime;
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    void UpdateStateLogic()
    {
        if (_player == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        
        switch (_currentState)
        {
            case CreatureState.Idle:
                HandleIdleState(distanceToPlayer);
                break;
                
            case CreatureState.Curious:
                HandleCuriousState(distanceToPlayer);
                break;
                
            case CreatureState.Scared:
                HandleScaredState(distanceToPlayer);
                break;
                
            case CreatureState.Aggressive:
                HandleAggressiveState(distanceToPlayer);
                break;
                
            case CreatureState.Exhausted:
                HandleExhaustedState();
                break;
        }
    }
    
    void HandleIdleState(float distanceToPlayer)
    {
        if (distanceToPlayer <= _detectionRange && distanceToPlayer > _scareRange)
        {
            ChangeState(CreatureState.Curious);
        }
        else if (distanceToPlayer <= _scareRange && distanceToPlayer > _aggressionRange)
        {
            ChangeState(CreatureState.Scared);
        }
        else if (distanceToPlayer <= _aggressionRange)
        {
            ChangeState(CreatureState.Aggressive);
        }
    }
    
    void HandleCuriousState(float distanceToPlayer)
    {
        if (distanceToPlayer <= _scareRange && distanceToPlayer > _aggressionRange)
        {
            ChangeState(CreatureState.Scared);
        }
        else if (distanceToPlayer <= _aggressionRange)
        {
            ChangeState(CreatureState.Aggressive);
        }
        else if (distanceToPlayer > _detectionRange || _stateTimer >= _curiousStateDuration)
        {
            ChangeState(CreatureState.Idle);
        }
        else
        {
            MoveTowardsPlayer(_curiousSpeed);
        }
    }
    
    void HandleScaredState(float distanceToPlayer)
    {
        if (_stateTimer >= _scaredStateDuration)
        {
            ChangeState(CreatureState.Exhausted);
        }
        else if (distanceToPlayer <= _aggressionRange)
        {
            ChangeState(CreatureState.Aggressive);
        }
        else
        {
            MoveAwayFromPlayer(_scaredSpeed);
        }
    }
    
    void HandleAggressiveState(float distanceToPlayer)
    {
        if (_stateTimer >= _aggressiveStateDuration)
        {
            ChangeState(CreatureState.Exhausted);
        }
        else if (distanceToPlayer > _detectionRange)
        {
            ChangeState(CreatureState.Idle);
        }
        else
        {
            MoveTowardsPlayer(_aggressiveSpeed);
        }
    }
    
    void HandleExhaustedState()
    {
        if (_stateTimer >= _exhaustedStateDuration)
        {
            ChangeState(CreatureState.Idle);
        }
    }
    
    void MoveTowardsPlayer(float speed)
    {
        if (_player == null) return;
        
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0;
        
        _rigidbody.velocity = new Vector3(direction.x * speed, _rigidbody.velocity.y, direction.z * speed);
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    void MoveAwayFromPlayer(float speed)
    {
        if (_player == null) return;
        
        Vector3 direction = (transform.position - _player.position).normalized;
        direction.y = 0;
        
        _rigidbody.velocity = new Vector3(direction.x * speed, _rigidbody.velocity.y, direction.z * speed);
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    void UpdateVisuals()
    {
        if (_creatureRenderer == null) return;
        
        _currentColor = Color.Lerp(_currentColor, _targetColor, _colorTransitionSpeed * Time.deltaTime);
        _creatureRenderer.material.color = _currentColor;
    }
    
    void UpdateAudio()
    {
        if (_currentState == CreatureState.Idle && _breathingTimer >= _ambientBreathingInterval)
        {
            PlaySound(_breathingSound);
            _breathingTimer = 0f;
        }
    }
    
    void ChangeState(CreatureState newState)
    {
        if (_currentState == newState) return;
        
        _currentState = newState;
        _stateTimer = 0f;
        _hasPlayedStateSound = false;
        
        switch (newState)
        {
            case CreatureState.Idle:
                _targetColor = _normalColor;
                _rigidbody.velocity = Vector3.zero;
                break;
                
            case CreatureState.Curious:
                _targetColor = _normalColor;
                PlaySound(_questioningSound);
                break;
                
            case CreatureState.Scared:
                _targetColor = _scaredColor;
                PlaySound(_panicSound);
                break;
                
            case CreatureState.Aggressive:
                _targetColor = _aggressiveColor;
                PlaySound(_roarSound);
                break;
                
            case CreatureState.Exhausted:
                _targetColor = _exhaustedColor;
                _rigidbody.velocity = Vector3.zero;
                PlaySound(_tiredSound);
                break;
        }
        
        OnStateChanged?.Invoke();
        OnSpecificStateEntered?.Invoke(newState);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.orange;
        Gizmos.DrawWireSphere(transform.position, _scareRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _aggressionRange);
    }
}