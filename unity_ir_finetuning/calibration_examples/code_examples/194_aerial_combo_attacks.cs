// Prompt: aerial combo attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class AerialComboSystem : MonoBehaviour
{
    [System.Serializable]
    public class ComboAttack
    {
        public string name;
        public KeyCode inputKey;
        public float damage;
        public float knockbackForce;
        public Vector3 attackOffset;
        public float attackRadius;
        public AnimationClip animationClip;
        public ParticleSystem effectPrefab;
        public AudioClip soundEffect;
        public float cooldown;
        [HideInInspector] public float lastUsedTime;
    }

    [System.Serializable]
    public class ComboSequence
    {
        public string sequenceName;
        public List<int> attackIndices;
        public float maxTimeBetweenAttacks;
        public float bonusDamageMultiplier;
        public ParticleSystem finisherEffect;
        public AudioClip finisherSound;
    }

    [Header("Aerial Combat Settings")]
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private float _airborneThreshold = 0.5f;
    [SerializeField] private float _maxAirTime = 5f;
    [SerializeField] private bool _requireAirborne = true;

    [Header("Combo Attacks")]
    [SerializeField] private List<ComboAttack> _comboAttacks = new List<ComboAttack>();

    [Header("Combo Sequences")]
    [SerializeField] private List<ComboSequence> _comboSequences = new List<ComboSequence>();
    [SerializeField] private float _comboResetTime = 2f;

    [Header("Physics")]
    [SerializeField] private float _airControlForce = 10f;
    [SerializeField] private float _attackMomentum = 5f;
    [SerializeField] private bool _pauseGravityDuringAttack = true;

    [Header("Visual Effects")]
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private ParticleSystem _comboBuilderEffect;
    [SerializeField] private TrailRenderer _weaponTrail;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _comboStartSound;
    [SerializeField] private AudioClip _comboBreakSound;

    [Header("Events")]
    public UnityEvent<int> OnComboCountChanged;
    public UnityEvent<string> OnComboSequenceCompleted;
    public UnityEvent<float> OnAttackHit;
    public UnityEvent OnAerialCombatStart;
    public UnityEvent OnAerialCombatEnd;

    private Rigidbody _rigidbody;
    private Animator _animator;
    private Collider _collider;
    private bool _isAirborne;
    private float _airTime;
    private bool _isAttacking;
    private int _currentComboCount;
    private List<int> _currentComboSequence = new List<int>();
    private float _lastAttackTime;
    private Vector3 _originalGravityScale;
    private bool _gravityPaused;
    private Dictionary<GameObject, float> _hitTargets = new Dictionary<GameObject, float>();

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _collider = GetComponent<Collider>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_attackPoint == null)
            _attackPoint = transform;

        _originalGravityScale = Physics.gravity;
        
        ValidateComboAttacks();
    }

    private void Update()
    {
        CheckAirborneStatus();
        HandleComboInput();
        UpdateComboTimer();
        UpdateAirTime();
    }

    private void FixedUpdate()
    {
        if (_isAirborne && !_isAttacking)
        {
            HandleAirMovement();
        }
    }

    private void CheckAirborneStatus()
    {
        bool wasAirborne = _isAirborne;
        _isAirborne = !IsGrounded();

        if (_isAirborne && !wasAirborne)
        {
            OnAerialCombatStart?.Invoke();
            _airTime = 0f;
        }
        else if (!_isAirborne && wasAirborne)
        {
            OnAerialCombatEnd?.Invoke();
            ResetCombo();
            RestoreGravity();
        }
    }

    private bool IsGrounded()
    {
        if (_collider == null) return false;
        
        Vector3 center = _collider.bounds.center;
        Vector3 size = _collider.bounds.size;
        
        return Physics.CheckBox(
            center - Vector3.up * (size.y * 0.5f + _airborneThreshold),
            new Vector3(size.x * 0.4f, 0.1f, size.z * 0.4f),
            transform.rotation,
            _targetLayers
        );
    }

    private void HandleComboInput()
    {
        if (_requireAirborne && !_isAirborne) return;
        if (_isAttacking) return;

        for (int i = 0; i < _comboAttacks.Count; i++)
        {
            if (Input.GetKeyDown(_comboAttacks[i].inputKey))
            {
                if (CanPerformAttack(i))
                {
                    StartCoroutine(PerformAttack(i));
                    break;
                }
            }
        }
    }

    private bool CanPerformAttack(int attackIndex)
    {
        if (attackIndex < 0 || attackIndex >= _comboAttacks.Count) return false;
        
        ComboAttack attack = _comboAttacks[attackIndex];
        return Time.time >= attack.lastUsedTime + attack.cooldown;
    }

    private IEnumerator PerformAttack(int attackIndex)
    {
        _isAttacking = true;
        ComboAttack attack = _comboAttacks[attackIndex];
        attack.lastUsedTime = Time.time;

        // Add to combo sequence
        _currentComboSequence.Add(attackIndex);
        _currentComboCount++;
        _lastAttackTime = Time.time;
        OnComboCountChanged?.Invoke(_currentComboCount);

        // Pause gravity if enabled
        if (_pauseGravityDuringAttack && _isAirborne)
        {
            PauseGravity();
        }

        // Play animation
        if (_animator != null && attack.animationClip != null)
        {
            _animator.Play(attack.animationClip.name);
        }

        // Apply attack momentum
        if (_rigidbody != null)
        {
            Vector3 attackDirection = transform.forward;
            _rigidbody.AddForce(attackDirection * _attackMomentum, ForceMode.Impulse);
        }

        // Enable weapon trail
        if (_weaponTrail != null)
        {
            _weaponTrail.enabled = true;
        }

        // Play sound effect
        if (_audioSource != null && attack.soundEffect != null)
        {
            _audioSource.PlayOneShot(attack.soundEffect);
        }

        // Wait for attack timing
        float attackDuration = attack.animationClip != null ? attack.animationClip.length : 0.5f;
        yield return new WaitForSeconds(attackDuration * 0.3f);

        // Perform hit detection
        PerformHitDetection(attack);

        // Spawn effect
        if (attack.effectPrefab != null)
        {
            Vector3 effectPosition = _attackPoint.position + attack.attackOffset;
            ParticleSystem effect = Instantiate(attack.effectPrefab, effectPosition, _attackPoint.rotation);
            Destroy(effect.gameObject, 3f);
        }

        yield return new WaitForSeconds(attackDuration * 0.7f);

        // Disable weapon trail
        if (_weaponTrail != null)
        {
            _weaponTrail.enabled = false;
        }

        // Check for combo sequences
        CheckComboSequences();

        // Restore gravity
        if (_pauseGravityDuringAttack)
        {
            RestoreGravity();
        }

        _isAttacking = false;
    }

    private void PerformHitDetection(ComboAttack attack)
    {
        Vector3 attackPosition = _attackPoint.position + _attackPoint.TransformDirection(attack.attackOffset);
        Collider[] hitColliders = Physics.OverlapSphere(attackPosition, attack.attackRadius, _targetLayers);

        foreach (Collider hitCollider in hitColliders)
        {
            if (hitCollider.gameObject == gameObject) continue;

            GameObject target = hitCollider.gameObject;
            
            // Prevent multiple hits on same target in short time
            if (_hitTargets.ContainsKey(target) && Time.time - _hitTargets[target] < 0.5f)
                continue;

            _hitTargets[target] = Time.time;

            // Apply damage and knockback
            ApplyDamage(target, attack);
            ApplyKnockback(target, attack);

            OnAttackHit?.Invoke(attack.damage);
        }

        // Clean up old hit records
        List<GameObject> keysToRemove = new List<GameObject>();
        foreach (var kvp in _hitTargets)
        {
            if (kvp.Key == null || Time.time - kvp.Value > 2f)
                keysToRemove.Add(kvp.Key);
        }
        foreach (var key in keysToRemove)
        {
            _hitTargets.Remove(key);
        }
    }

    private void ApplyDamage(GameObject target, ComboAttack attack)
    {
        float finalDamage = attack.damage;

        // Apply combo bonus
        ComboSequence activeSequence = GetActiveComboSequence();
        if (activeSequence != null)
        {
            finalDamage *= activeSequence.bonusDamageMultiplier;
        }

        // Try to find health component or send message
        if (target.CompareTag("Player") || target.CompareTag("Enemy"))
        {
            target.SendMessage("TakeDamage", finalDamage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ApplyKnockback(GameObject target, ComboAttack attack)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackDirection = (target.transform.position - transform.position).normalized;
            knockbackDirection.y = 0.3f; // Add upward component
            targetRb.AddForce(knockbackDirection * attack.knockbackForce, ForceMode.Impulse);
        }
    }

    private void CheckComboSequences()
    {
        foreach (ComboSequence sequence in _comboSequences)
        {
            if (IsSequenceMatching(sequence))
            {
                CompleteComboSequence(sequence);
                break;
            }
        }
    }

    private bool IsSequenceMatching(ComboSequence sequence)
    {
        if (_currentComboSequence.Count < sequence.attackIndices.Count) return false;

        int startIndex = _currentComboSequence.Count - sequence.attackIndices.Count;
        for (int i = 0; i < sequence.attackIndices.Count; i++)
        {
            if (_currentComboSequence[startIndex + i] != sequence.attackIndices[i])
                return false;
        }

        return true;
    }

    private void CompleteComboSequence(ComboSequence sequence)
    {
        OnComboSequenceCompleted?.Invoke(sequence.sequenceName);

        // Play finisher effects
        if (sequence.finisherEffect != null)
        {
            ParticleSystem finisher = Instantiate(sequence.finisherEffect, _attackPoint.position, _attackPoint.rotation);
            Destroy(finisher.gameObject, 5f);
        }

        if (_audioSource != null && sequence.finisherSound != null)
        {
            _audioSource.PlayOneShot(sequence.finisherSound);
        }

        // Reset combo after sequence completion
        StartCoroutine(DelayedComboReset(1f));
    }

    private ComboSequence GetActiveComboSequence()
    {
        foreach (ComboSequence sequence in _comboSequences)
        {
            if (IsSequenceMatching(sequence))
                return sequence;
        }
        return null;
    }

    private void UpdateComboTimer()
    {
        if (_currentComboCount > 0 && Time.time - _lastAttackTime > _comboResetTime)
        {
            ResetCombo();
        }
    }

    private void UpdateAirTime()
    {
        if (_isAirborne)
        {
            _airTime += Time.deltaTime;
            if (_airTime > _maxAirTime)
            {
                // Force landing or reset
                ResetCombo();
            }
        }
    }

    private void HandleAirMovement()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, 0, vertical) * _airControlForce;
        movement = transform.TransformDirection(movement);
        
        _rigidbody.AddForce(movement, ForceMode.Force);
    }

    private void PauseGravity()
    {
        if (!_gravityPaused && _rigidbody != null)
        {
            _rigidbody.useGravity = false;
            _gravityPaused = true;
        }
    }

    private void RestoreGravity()
    {
        if (_gravityPaused && _rigidbody != null)
        {
            _rigidbody.useGravity = true;
            _gravityPaused = false;
        }
    }

    private void ResetCombo()
    {
        if (_currentComboCount > 0)
        {
            if (_audioSource != null && _comboBreakSound != null)
            {
                _audioSource.PlayOneShot(_comboBreakSound);
            }
        }

        _currentComboCount = 0;
        _currentComboSequence.Clear();
        OnComboCountChanged?.Invoke(0);

        if (_comboBuilderEffect != null)
        {
            _comboBuilderEffect.Stop();
        }
    }

    private IEnumerator DelayedComboReset(float delay)
    {
        yield return new WaitForSeconds(delay);
        ResetCombo();
    }

    private void ValidateComboAttacks()
    {
        for (int i = 0; i < _comboAttacks.Count; i++)
        {
            if (_comboAttacks[i].attackRadius <= 0)
                _comboAttacks[i].attackRadius = 1f;
            if (_comboAttacks[i].damage <= 0)
                _comboAttacks[i].damage = 10f;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_attackPoint == null) return;

        Gizmos.color = Color.red;
        foreach (