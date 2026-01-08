// Prompt: ninja that teleports behind player
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class TeleportingNinja : MonoBehaviour
{
    [Header("Teleport Settings")]
    [SerializeField] private float _teleportRange = 10f;
    [SerializeField] private float _teleportCooldown = 3f;
    [SerializeField] private float _detectionRange = 15f;
    [SerializeField] private float _teleportDistance = 2f;
    
    [Header("Combat Settings")]
    [SerializeField] private float _attackDamage = 25f;
    [SerializeField] private float _attackRange = 1.5f;
    [SerializeField] private float _attackCooldown = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _teleportEffect;
    [SerializeField] private AudioClip _teleportSound;
    [SerializeField] private AudioClip _attackSound;
    
    [Header("Events")]
    public UnityEvent OnTeleport;
    public UnityEvent OnAttack;
    public UnityEvent<float> OnDamageDealt;
    
    private Transform _player;
    private float _lastTeleportTime;
    private float _lastAttackTime;
    private bool _isPlayerInRange;
    private AudioSource _audioSource;
    private Animator _animator;
    private Rigidbody _rigidbody;
    
    private enum NinjaState
    {
        Idle,
        Stalking,
        Teleporting,
        Attacking
    }
    
    private NinjaState _currentState = NinjaState.Idle;
    
    void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _animator = GetComponent<Animator>();
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_rigidbody != null)
        {
            _rigidbody.freezeRotation = true;
        }
        
        FindPlayer();
    }
    
    void Update()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        _isPlayerInRange = distanceToPlayer <= _detectionRange;
        
        UpdateState(distanceToPlayer);
        HandleBehavior();
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }
    
    void UpdateState(float distanceToPlayer)
    {
        switch (_currentState)
        {
            case NinjaState.Idle:
                if (_isPlayerInRange)
                    _currentState = NinjaState.Stalking;
                break;
                
            case NinjaState.Stalking:
                if (!_isPlayerInRange)
                    _currentState = NinjaState.Idle;
                else if (CanTeleport() && distanceToPlayer > _teleportDistance)
                    _currentState = NinjaState.Teleporting;
                else if (distanceToPlayer <= _attackRange && CanAttack())
                    _currentState = NinjaState.Attacking;
                break;
                
            case NinjaState.Teleporting:
                _currentState = NinjaState.Stalking;
                break;
                
            case NinjaState.Attacking:
                _currentState = NinjaState.Stalking;
                break;
        }
    }
    
    void HandleBehavior()
    {
        switch (_currentState)
        {
            case NinjaState.Teleporting:
                TeleportBehindPlayer();
                break;
                
            case NinjaState.Attacking:
                AttackPlayer();
                break;
                
            case NinjaState.Stalking:
                LookAtPlayer();
                break;
        }
    }
    
    bool CanTeleport()
    {
        return Time.time >= _lastTeleportTime + _teleportCooldown;
    }
    
    bool CanAttack()
    {
        return Time.time >= _lastAttackTime + _attackCooldown;
    }
    
    void TeleportBehindPlayer()
    {
        if (_player == null || !CanTeleport()) return;
        
        Vector3 playerForward = _player.forward;
        Vector3 teleportPosition = _player.position - playerForward * _teleportDistance;
        
        // Ensure teleport position is on the ground
        RaycastHit hit;
        if (Physics.Raycast(teleportPosition + Vector3.up * 5f, Vector3.down, out hit, 10f))
        {
            teleportPosition.y = hit.point.y;
        }
        
        // Check if teleport position is valid (not inside walls)
        if (!Physics.CheckSphere(teleportPosition, 0.5f))
        {
            // Play teleport effect at current position
            if (_teleportEffect != null)
                _teleportEffect.Play();
                
            // Play sound
            if (_teleportSound != null && _audioSource != null)
                _audioSource.PlayOneShot(_teleportSound);
            
            // Teleport
            transform.position = teleportPosition;
            transform.LookAt(_player);
            
            // Play teleport effect at new position
            if (_teleportEffect != null)
            {
                Invoke(nameof(PlayTeleportEffectDelayed), 0.1f);
            }
            
            _lastTeleportTime = Time.time;
            
            if (_animator != null)
                _animator.SetTrigger("Teleport");
                
            OnTeleport?.Invoke();
        }
    }
    
    void PlayTeleportEffectDelayed()
    {
        if (_teleportEffect != null)
            _teleportEffect.Play();
    }
    
    void AttackPlayer()
    {
        if (_player == null || !CanAttack()) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
        if (distanceToPlayer <= _attackRange)
        {
            // Play attack animation
            if (_animator != null)
                _animator.SetTrigger("Attack");
            
            // Play attack sound
            if (_attackSound != null && _audioSource != null)
                _audioSource.PlayOneShot(_attackSound);
            
            // Deal damage to player
            DealDamageToPlayer();
            
            _lastAttackTime = Time.time;
            OnAttack?.Invoke();
        }
    }
    
    void DealDamageToPlayer()
    {
        if (_player == null) return;
        
        // Try to find health component on player
        var healthComponent = _player.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use SendMessage as a generic way to deal damage
            _player.SendMessage("TakeDamage", _attackDamage, SendMessageOptions.DontRequireReceiver);
        }
        
        OnDamageDealt?.Invoke(_attackDamage);
    }
    
    void LookAtPlayer()
    {
        if (_player == null) return;
        
        Vector3 direction = (_player.position - transform.position).normalized;
        direction.y = 0; // Keep ninja upright
        
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        // Draw attack range
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
        
        // Draw teleport range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _teleportRange);
        
        // Show teleport position if player exists
        if (_player != null)
        {
            Vector3 teleportPos = _player.position - _player.forward * _teleportDistance;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(teleportPos, 0.5f);
            Gizmos.DrawLine(transform.position, teleportPos);
        }
    }
}