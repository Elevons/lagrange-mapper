// Prompt: dual daggers with fast combo
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class DualDaggerWeapon : MonoBehaviour
{
    [Header("Weapon Settings")]
    [SerializeField] private float _attackDamage = 25f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private float _comboWindow = 0.8f;
    [SerializeField] private int _maxComboCount = 4;
    [SerializeField] private float _comboDamageMultiplier = 1.2f;
    
    [Header("Attack Timing")]
    [SerializeField] private float _leftDaggerDelay = 0f;
    [SerializeField] private float _rightDaggerDelay = 0.15f;
    [SerializeField] private float _attackCooldown = 0.3f;
    
    [Header("Visual Effects")]
    [SerializeField] private Transform _leftDagger;
    [SerializeField] private Transform _rightDagger;
    [SerializeField] private ParticleSystem _slashEffect;
    [SerializeField] private TrailRenderer _leftTrail;
    [SerializeField] private TrailRenderer _rightTrail;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _attackSounds;
    [SerializeField] private AudioClip _comboFinishSound;
    
    [Header("Animation")]
    [SerializeField] private Animator _weaponAnimator;
    [SerializeField] private string _leftAttackTrigger = "LeftAttack";
    [SerializeField] private string _rightAttackTrigger = "RightAttack";
    [SerializeField] private string _comboTrigger = "ComboAttack";
    
    [Header("Events")]
    public UnityEvent<float> OnDamageDealt;
    public UnityEvent<int> OnComboChanged;
    public UnityEvent OnComboFinished;
    
    private int _currentCombo = 0;
    private float _lastAttackTime = 0f;
    private bool _canAttack = true;
    private bool _isAttacking = false;
    private Camera _playerCamera;
    private LayerMask _enemyLayerMask = -1;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_weaponAnimator == null)
            _weaponAnimator = GetComponent<Animator>();
            
        _enemyLayerMask = ~(1 << gameObject.layer);
        
        SetupTrails();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateComboTimer();
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && _canAttack && !_isAttacking)
        {
            PerformAttack();
        }
    }
    
    private void UpdateComboTimer()
    {
        if (_currentCombo > 0 && Time.time - _lastAttackTime > _comboWindow)
        {
            ResetCombo();
        }
    }
    
    private void PerformAttack()
    {
        if (!_canAttack) return;
        
        _isAttacking = true;
        _canAttack = false;
        _lastAttackTime = Time.time;
        
        _currentCombo = Mathf.Min(_currentCombo + 1, _maxComboCount);
        OnComboChanged?.Invoke(_currentCombo);
        
        bool useLeftDagger = (_currentCombo % 2 == 1);
        
        if (_weaponAnimator != null)
        {
            if (_currentCombo >= _maxComboCount)
            {
                _weaponAnimator.SetTrigger(_comboTrigger);
                StartCoroutine(ExecuteComboFinisher());
            }
            else
            {
                string trigger = useLeftDagger ? _leftAttackTrigger : _rightAttackTrigger;
                _weaponAnimator.SetTrigger(trigger);
                StartCoroutine(ExecuteSingleAttack(useLeftDagger));
            }
        }
        else
        {
            if (_currentCombo >= _maxComboCount)
            {
                StartCoroutine(ExecuteComboFinisher());
            }
            else
            {
                StartCoroutine(ExecuteSingleAttack(useLeftDagger));
            }
        }
    }
    
    private IEnumerator ExecuteSingleAttack(bool useLeftDagger)
    {
        float delay = useLeftDagger ? _leftDaggerDelay : _rightDaggerDelay;
        yield return new WaitForSeconds(delay);
        
        Transform activeDagger = useLeftDagger ? _leftDagger : _rightDagger;
        TrailRenderer activeTrail = useLeftDagger ? _leftTrail : _rightTrail;
        
        if (activeTrail != null)
        {
            activeTrail.enabled = true;
            activeTrail.Clear();
        }
        
        PlayAttackSound();
        PerformDamageCheck(activeDagger, false);
        
        if (_slashEffect != null && activeDagger != null)
        {
            _slashEffect.transform.position = activeDagger.position;
            _slashEffect.Play();
        }
        
        yield return new WaitForSeconds(0.2f);
        
        if (activeTrail != null)
            activeTrail.enabled = false;
            
        yield return new WaitForSeconds(_attackCooldown - 0.2f);
        
        _canAttack = true;
        _isAttacking = false;
    }
    
    private IEnumerator ExecuteComboFinisher()
    {
        yield return new WaitForSeconds(_leftDaggerDelay);
        
        if (_leftTrail != null)
        {
            _leftTrail.enabled = true;
            _leftTrail.Clear();
        }
        if (_rightTrail != null)
        {
            _rightTrail.enabled = true;
            _rightTrail.Clear();
        }
        
        PlayComboSound();
        
        PerformDamageCheck(_leftDagger, true);
        yield return new WaitForSeconds(0.1f);
        PerformDamageCheck(_rightDagger, true);
        
        if (_slashEffect != null)
        {
            if (_leftDagger != null)
            {
                _slashEffect.transform.position = _leftDagger.position;
                _slashEffect.Play();
            }
            yield return new WaitForSeconds(0.1f);
            if (_rightDagger != null)
            {
                _slashEffect.transform.position = _rightDagger.position;
                _slashEffect.Play();
            }
        }
        
        yield return new WaitForSeconds(0.3f);
        
        if (_leftTrail != null) _leftTrail.enabled = false;
        if (_rightTrail != null) _rightTrail.enabled = false;
        
        OnComboFinished?.Invoke();
        ResetCombo();
        
        yield return new WaitForSeconds(_attackCooldown);
        
        _canAttack = true;
        _isAttacking = false;
    }
    
    private void PerformDamageCheck(Transform dagger, bool isComboFinisher)
    {
        if (dagger == null) return;
        
        Vector3 attackOrigin = dagger.position;
        Vector3 attackDirection = dagger.forward;
        
        RaycastHit[] hits = Physics.SphereCastAll(attackOrigin, 0.5f, attackDirection, _attackRange, _enemyLayerMask);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            
            float finalDamage = _attackDamage;
            
            if (isComboFinisher)
            {
                finalDamage *= _comboDamageMultiplier * _maxComboCount;
            }
            else if (_currentCombo > 1)
            {
                finalDamage *= Mathf.Pow(_comboDamageMultiplier, _currentCombo - 1);
            }
            
            IDamageable damageable = hit.collider.GetComponent<IDamageable>();
            if (damageable != null)
            {
                damageable.TakeDamage(finalDamage);
            }
            else
            {
                Rigidbody rb = hit.collider.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 forceDirection = (hit.point - attackOrigin).normalized;
                    rb.AddForce(forceDirection * finalDamage * 10f, ForceMode.Impulse);
                }
            }
            
            OnDamageDealt?.Invoke(finalDamage);
        }
    }
    
    private void PlayAttackSound()
    {
        if (_audioSource != null && _attackSounds != null && _attackSounds.Length > 0)
        {
            AudioClip clip = _attackSounds[Random.Range(0, _attackSounds.Length)];
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void PlayComboSound()
    {
        if (_audioSource != null && _comboFinishSound != null)
        {
            _audioSource.PlayOneShot(_comboFinishSound);
        }
    }
    
    private void ResetCombo()
    {
        _currentCombo = 0;
        OnComboChanged?.Invoke(_currentCombo);
    }
    
    private void SetupTrails()
    {
        if (_leftTrail != null)
        {
            _leftTrail.enabled = false;
            _leftTrail.time = 0.3f;
        }
        if (_rightTrail != null)
        {
            _rightTrail.enabled = false;
            _rightTrail.time = 0.3f;
        }
    }
    
    public int GetCurrentCombo()
    {
        return _currentCombo;
    }
    
    public bool IsAttacking()
    {
        return _isAttacking;
    }
    
    public void SetAttackDamage(float damage)
    {
        _attackDamage = damage;
    }
    
    public void SetComboMultiplier(float multiplier)
    {
        _comboDamageMultiplier = multiplier;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_leftDagger != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_leftDagger.position, 0.5f);
            Gizmos.DrawRay(_leftDagger.position, _leftDagger.forward * _attackRange);
        }
        
        if (_rightDagger != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_rightDagger.position, 0.5f);
            Gizmos.DrawRay(_rightDagger.position, _rightDagger.forward * _attackRange);
        }
    }
}

public interface IDamageable
{
    void TakeDamage(float damage);
}