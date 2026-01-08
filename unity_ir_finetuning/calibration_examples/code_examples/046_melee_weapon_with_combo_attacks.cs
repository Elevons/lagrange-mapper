// Prompt: melee weapon with combo attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class MeleeWeapon : MonoBehaviour
{
    [System.Serializable]
    public class ComboAttack
    {
        public string attackName;
        public float damage;
        public float range;
        public float attackDuration;
        public float comboWindow;
        public AnimationClip attackAnimation;
        public AudioClip attackSound;
        public GameObject hitEffect;
        public Vector3 attackOffset;
        public float knockbackForce;
    }

    [Header("Weapon Settings")]
    [SerializeField] private Transform _attackPoint;
    [SerializeField] private LayerMask _enemyLayers = -1;
    [SerializeField] private float _baseDamage = 10f;
    
    [Header("Combo System")]
    [SerializeField] private List<ComboAttack> _comboAttacks = new List<ComboAttack>();
    [SerializeField] private float _comboResetTime = 2f;
    [SerializeField] private bool _canCancelCombo = true;
    
    [Header("Visual Effects")]
    [SerializeField] private TrailRenderer _weaponTrail;
    [SerializeField] private ParticleSystem _hitParticles;
    [SerializeField] private GameObject _slashEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _swingSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Events")]
    public UnityEvent<float> OnAttackHit;
    public UnityEvent OnComboComplete;
    public UnityEvent<int> OnComboStep;

    private Animator _animator;
    private int _currentComboIndex = 0;
    private bool _isAttacking = false;
    private bool _canAttack = true;
    private float _lastAttackTime;
    private Coroutine _comboResetCoroutine;
    private List<Collider> _hitTargets = new List<Collider>();

    private void Start()
    {
        _animator = GetComponentInParent<Animator>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_attackPoint == null)
            _attackPoint = transform;
            
        SetupDefaultCombo();
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && _canAttack)
        {
            PerformAttack();
        }
    }

    private void SetupDefaultCombo()
    {
        if (_comboAttacks.Count == 0)
        {
            _comboAttacks.Add(new ComboAttack
            {
                attackName = "Light Attack",
                damage = _baseDamage,
                range = 2f,
                attackDuration = 0.5f,
                comboWindow = 1f,
                attackOffset = Vector3.forward,
                knockbackForce = 5f
            });
            
            _comboAttacks.Add(new ComboAttack
            {
                attackName = "Medium Attack",
                damage = _baseDamage * 1.5f,
                range = 2.5f,
                attackDuration = 0.7f,
                comboWindow = 1.2f,
                attackOffset = Vector3.forward,
                knockbackForce = 8f
            });
            
            _comboAttacks.Add(new ComboAttack
            {
                attackName = "Heavy Attack",
                damage = _baseDamage * 2f,
                range = 3f,
                attackDuration = 1f,
                comboWindow = 0.5f,
                attackOffset = Vector3.forward,
                knockbackForce = 12f
            });
        }
    }

    public void PerformAttack()
    {
        if (!_canAttack || _isAttacking) return;

        ComboAttack currentAttack = _comboAttacks[_currentComboIndex];
        StartCoroutine(ExecuteAttack(currentAttack));
    }

    private IEnumerator ExecuteAttack(ComboAttack attack)
    {
        _isAttacking = true;
        _canAttack = false;
        _hitTargets.Clear();

        PlayAttackAnimation(attack);
        PlayAttackSound(attack);
        ShowVisualEffects(attack);

        yield return new WaitForSeconds(attack.attackDuration * 0.3f);

        PerformHitDetection(attack);

        yield return new WaitForSeconds(attack.attackDuration * 0.7f);

        _isAttacking = false;
        
        float currentTime = Time.time;
        bool withinComboWindow = (currentTime - _lastAttackTime) <= attack.comboWindow;
        
        if (withinComboWindow && _currentComboIndex < _comboAttacks.Count - 1)
        {
            _currentComboIndex++;
            _canAttack = true;
            OnComboStep?.Invoke(_currentComboIndex);
        }
        else
        {
            if (_currentComboIndex >= _comboAttacks.Count - 1)
            {
                OnComboComplete?.Invoke();
            }
            ResetCombo();
        }

        _lastAttackTime = currentTime;
        StartComboResetTimer();
    }

    private void PerformHitDetection(ComboAttack attack)
    {
        Vector3 attackPosition = _attackPoint.position + transform.TransformDirection(attack.attackOffset);
        Collider[] hitColliders = Physics.OverlapSphere(attackPosition, attack.range, _enemyLayers);

        foreach (Collider hitCollider in hitColliders)
        {
            if (_hitTargets.Contains(hitCollider)) continue;
            
            _hitTargets.Add(hitCollider);
            
            if (hitCollider.CompareTag("Enemy"))
            {
                DealDamage(hitCollider, attack);
                ApplyKnockback(hitCollider, attack);
                SpawnHitEffect(hitCollider.transform.position, attack);
                PlayHitSound();
                OnAttackHit?.Invoke(attack.damage);
            }
        }
    }

    private void DealDamage(Collider target, ComboAttack attack)
    {
        var healthComponent = target.GetComponent<Health>();
        if (healthComponent != null)
        {
            healthComponent.TakeDamage(attack.damage);
        }
        else
        {
            target.SendMessage("TakeDamage", attack.damage, SendMessageOptions.DontRequireReceiver);
        }
    }

    private void ApplyKnockback(Collider target, ComboAttack attack)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockbackDirection = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(knockbackDirection * attack.knockbackForce, ForceMode.Impulse);
        }
    }

    private void PlayAttackAnimation(ComboAttack attack)
    {
        if (_animator != null)
        {
            _animator.SetTrigger("Attack");
            _animator.SetInteger("ComboIndex", _currentComboIndex);
        }
    }

    private void PlayAttackSound(ComboAttack attack)
    {
        if (_audioSource != null)
        {
            AudioClip soundToPlay = attack.attackSound != null ? attack.attackSound : _swingSound;
            if (soundToPlay != null)
            {
                _audioSource.PlayOneShot(soundToPlay);
            }
        }
    }

    private void PlayHitSound()
    {
        if (_audioSource != null && _hitSound != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
    }

    private void ShowVisualEffects(ComboAttack attack)
    {
        if (_weaponTrail != null)
        {
            _weaponTrail.enabled = true;
            StartCoroutine(DisableTrailAfterDelay(0.3f));
        }

        if (_slashEffect != null)
        {
            GameObject effect = Instantiate(_slashEffect, _attackPoint.position, _attackPoint.rotation);
            Destroy(effect, 2f);
        }
    }

    private void SpawnHitEffect(Vector3 position, ComboAttack attack)
    {
        if (_hitParticles != null)
        {
            _hitParticles.transform.position = position;
            _hitParticles.Play();
        }

        if (attack.hitEffect != null)
        {
            GameObject effect = Instantiate(attack.hitEffect, position, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }

    private IEnumerator DisableTrailAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (_weaponTrail != null)
        {
            _weaponTrail.enabled = false;
        }
    }

    private void StartComboResetTimer()
    {
        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
        }
        _comboResetCoroutine = StartCoroutine(ComboResetTimer());
    }

    private IEnumerator ComboResetTimer()
    {
        yield return new WaitForSeconds(_comboResetTime);
        ResetCombo();
    }

    private void ResetCombo()
    {
        _currentComboIndex = 0;
        _canAttack = true;
        _isAttacking = false;
        
        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
            _comboResetCoroutine = null;
        }
    }

    public void CancelCombo()
    {
        if (_canCancelCombo)
        {
            ResetCombo();
        }
    }

    public bool IsAttacking()
    {
        return _isAttacking;
    }

    public int GetCurrentComboStep()
    {
        return _currentComboIndex;
    }

    public int GetMaxComboSteps()
    {
        return _comboAttacks.Count;
    }

    private void OnDrawGizmosSelected()
    {
        if (_attackPoint == null) return;

        Gizmos.color = Color.red;
        for (int i = 0; i < _comboAttacks.Count; i++)
        {
            ComboAttack attack = _comboAttacks[i];
            Vector3 attackPos = _attackPoint.position + transform.TransformDirection(attack.attackOffset);
            Gizmos.DrawWireSphere(attackPos, attack.range);
        }
    }
}

[System.Serializable]
public class Health : MonoBehaviour
{
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _currentHealth;

    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnDeath;

    private void Start()
    {
        _currentHealth = _maxHealth;
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
        
        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        _currentHealth += amount;
        _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);
    }

    public float GetCurrentHealth()
    {
        return _currentHealth;
    }

    public float GetMaxHealth()
    {
        return _maxHealth;
    }

    public bool IsAlive()
    {
        return _currentHealth > 0f;
    }
}