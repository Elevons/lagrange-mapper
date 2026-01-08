// Prompt: alien with tentacle attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class AlienTentacleController : MonoBehaviour
{
    [System.Serializable]
    public class TentacleSegment
    {
        public Transform transform;
        public Vector3 targetPosition;
        public Vector3 velocity;
        public float damping = 0.8f;
    }

    [Header("Alien Settings")]
    [SerializeField] private float _health = 100f;
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private LayerMask _playerLayer = 1;

    [Header("Tentacle Settings")]
    [SerializeField] private List<TentacleSegment> _tentacles = new List<TentacleSegment>();
    [SerializeField] private float _tentacleSpeed = 8f;
    [SerializeField] private float _tentacleRange = 15f;
    [SerializeField] private float _tentacleDamage = 25f;
    [SerializeField] private float _attackCooldown = 2f;
    [SerializeField] private float _segmentDistance = 1f;
    [SerializeField] private AnimationCurve _tentacleMoveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attackSound;
    [SerializeField] private AudioClip _hurtSound;
    [SerializeField] private AudioClip _deathSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _attackEffect;
    [SerializeField] private ParticleSystem _deathEffect;

    [Header("Events")]
    public UnityEvent OnPlayerDetected;
    public UnityEvent OnAttack;
    public UnityEvent OnDeath;

    private Transform _player;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private bool _isAttacking = false;
    private bool _isDead = false;
    private float _lastAttackTime;
    private Vector3 _originalPosition;
    private Coroutine _currentAttackCoroutine;

    private enum AlienState
    {
        Idle,
        Chasing,
        Attacking,
        Dead
    }

    private AlienState _currentState = AlienState.Idle;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _audioSource = GetComponent<AudioSource>();
        _originalPosition = transform.position;

        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        InitializeTentacles();
        FindPlayer();
    }

    private void Update()
    {
        if (_isDead) return;

        UpdateState();
        UpdateTentacles();
        HandleAnimations();
    }

    private void FixedUpdate()
    {
        if (_isDead) return;

        HandleMovement();
    }

    private void InitializeTentacles()
    {
        for (int i = 0; i < _tentacles.Count; i++)
        {
            if (_tentacles[i].transform != null)
            {
                _tentacles[i].targetPosition = _tentacles[i].transform.position;
                _tentacles[i].velocity = Vector3.zero;
            }
        }
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }

    private void UpdateState()
    {
        if (_player == null)
        {
            FindPlayer();
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, _player.position);

        switch (_currentState)
        {
            case AlienState.Idle:
                if (distanceToPlayer <= _detectionRange)
                {
                    _currentState = AlienState.Chasing;
                    OnPlayerDetected?.Invoke();
                }
                break;

            case AlienState.Chasing:
                if (distanceToPlayer <= _tentacleRange && Time.time >= _lastAttackTime + _attackCooldown)
                {
                    _currentState = AlienState.Attacking;
                    StartTentacleAttack();
                }
                else if (distanceToPlayer > _detectionRange * 1.5f)
                {
                    _currentState = AlienState.Idle;
                }
                break;

            case AlienState.Attacking:
                if (!_isAttacking)
                {
                    _currentState = AlienState.Chasing;
                }
                break;
        }
    }

    private void HandleMovement()
    {
        if (_currentState == AlienState.Chasing && _player != null && !_isAttacking)
        {
            Vector3 direction = (_player.position - transform.position).normalized;
            direction.y = 0f;

            _rigidbody.MovePosition(transform.position + direction * _moveSpeed * Time.fixedDeltaTime);
            
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }
    }

    private void UpdateTentacles()
    {
        for (int i = 0; i < _tentacles.Count; i++)
        {
            if (_tentacles[i].transform == null) continue;

            Vector3 currentPos = _tentacles[i].transform.position;
            Vector3 targetPos = _tentacles[i].targetPosition;

            _tentacles[i].velocity += (targetPos - currentPos) * _tentacleSpeed * Time.deltaTime;
            _tentacles[i].velocity *= _tentacles[i].damping;

            _tentacles[i].transform.position = currentPos + _tentacles[i].velocity * Time.deltaTime;

            if (i > 0 && _tentacles[i - 1].transform != null)
            {
                Vector3 parentPos = _tentacles[i - 1].transform.position;
                Vector3 currentSegmentPos = _tentacles[i].transform.position;
                Vector3 direction = (currentSegmentPos - parentPos).normalized;
                
                _tentacles[i].transform.position = parentPos + direction * _segmentDistance;
            }
        }
    }

    private void StartTentacleAttack()
    {
        if (_isAttacking || _player == null) return;

        _isAttacking = true;
        _lastAttackTime = Time.time;
        
        OnAttack?.Invoke();
        
        if (_attackSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_attackSound);

        if (_currentAttackCoroutine != null)
            StopCoroutine(_currentAttackCoroutine);

        _currentAttackCoroutine = StartCoroutine(TentacleAttackSequence());
    }

    private IEnumerator TentacleAttackSequence()
    {
        Vector3 playerPosition = _player.position;
        
        for (int i = 0; i < _tentacles.Count; i++)
        {
            if (_tentacles[i].transform == null) continue;

            Vector3 attackPosition = playerPosition + Random.insideUnitSphere * 2f;
            attackPosition.y = playerPosition.y;

            StartCoroutine(MoveTentacleToPosition(i, attackPosition, 0.5f));
            
            yield return new WaitForSeconds(0.1f);
        }

        if (_attackEffect != null)
            _attackEffect.Play();

        yield return new WaitForSeconds(0.5f);

        CheckTentacleHit(playerPosition);

        yield return new WaitForSeconds(0.3f);

        for (int i = 0; i < _tentacles.Count; i++)
        {
            if (_tentacles[i].transform == null) continue;

            Vector3 restPosition = transform.position + transform.forward * (i + 1) * _segmentDistance;
            StartCoroutine(MoveTentacleToPosition(i, restPosition, 0.8f));
        }

        yield return new WaitForSeconds(0.8f);

        _isAttacking = false;
    }

    private IEnumerator MoveTentacleToPosition(int tentacleIndex, Vector3 targetPosition, float duration)
    {
        if (tentacleIndex >= _tentacles.Count || _tentacles[tentacleIndex].transform == null)
            yield break;

        Vector3 startPosition = _tentacles[tentacleIndex].targetPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            float curveValue = _tentacleMoveCurve.Evaluate(t);

            _tentacles[tentacleIndex].targetPosition = Vector3.Lerp(startPosition, targetPosition, curveValue);
            yield return null;
        }

        _tentacles[tentacleIndex].targetPosition = targetPosition;
    }

    private void CheckTentacleHit(Vector3 attackCenter)
    {
        Collider[] hitColliders = Physics.OverlapSphere(attackCenter, 3f, _playerLayer);
        
        foreach (Collider hit in hitColliders)
        {
            if (hit.CompareTag("Player"))
            {
                Rigidbody playerRb = hit.GetComponent<Rigidbody>();
                if (playerRb != null)
                {
                    Vector3 knockbackDirection = (hit.transform.position - transform.position).normalized;
                    playerRb.AddForce(knockbackDirection * 500f + Vector3.up * 200f);
                }

                MonoBehaviour[] playerScripts = hit.GetComponents<MonoBehaviour>();
                foreach (MonoBehaviour script in playerScripts)
                {
                    script.SendMessage("TakeDamage", _tentacleDamage, SendMessageOptions.DontRequireReceiver);
                }
            }
        }
    }

    private void HandleAnimations()
    {
        if (_animator == null) return;

        _animator.SetBool("IsChasing", _currentState == AlienState.Chasing);
        _animator.SetBool("IsAttacking", _isAttacking);
        _animator.SetBool("IsDead", _isDead);
    }

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _health -= damage;

        if (_hurtSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_hurtSound);

        if (_health <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        if (_isDead) return;

        _isDead = true;
        _currentState = AlienState.Dead;

        if (_currentAttackCoroutine != null)
        {
            StopCoroutine(_currentAttackCoroutine);
            _isAttacking = false;
        }

        if (_deathSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_deathSound);

        if (_deathEffect != null)
            _deathEffect.Play();

        OnDeath?.Invoke();

        if (_rigidbody != null)
            _rigidbody.isKinematic = true;

        Collider[] colliders = GetComponents<Collider>();
        foreach (Collider col in colliders)
        {
            col.enabled = false;
        }

        StartCoroutine(DestroyAfterDelay(3f));
    }

    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !_isDead)
        {
            if (_currentState == AlienState.Idle)
            {
                _currentState = AlienState.Chasing;
                OnPlayerDetected?.Invoke();
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _tentacleRange);

        for (int i = 0; i < _tentacles.Count; i++)
        {
            if (_tentacles[i].transform != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_tentacles[i].transform.position, 0.5f);
                
                if (i > 0 && _tentacles[i - 1].transform != null)
                {
                    Gizmos.DrawLine(_tentacles[i - 1].transform.position, _tentacles[i].transform.position);
                }
            }
        }
    }
}