// Prompt: wizard with elemental attacks
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class WizardController : MonoBehaviour
{
    [System.Serializable]
    public enum ElementType
    {
        Fire,
        Ice,
        Lightning,
        Earth
    }

    [System.Serializable]
    public class ElementalSpell
    {
        public ElementType element;
        public GameObject projectilePrefab;
        public ParticleSystem castEffect;
        public AudioClip castSound;
        public float damage = 25f;
        public float manaCost = 20f;
        public float cooldown = 1f;
        public float projectileSpeed = 10f;
        [HideInInspector] public float lastCastTime;
    }

    [Header("Wizard Stats")]
    [SerializeField] private float _maxHealth = 100f;
    [SerializeField] private float _maxMana = 100f;
    [SerializeField] private float _manaRegenRate = 10f;
    [SerializeField] private float _movementSpeed = 5f;

    [Header("Combat")]
    [SerializeField] private ElementalSpell[] _spells = new ElementalSpell[4];
    [SerializeField] private Transform _castPoint;
    [SerializeField] private LayerMask _enemyLayers = -1;
    [SerializeField] private float _attackRange = 15f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    [Header("Events")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent<float> OnManaChanged;
    public UnityEvent<ElementType> OnSpellCast;
    public UnityEvent OnDeath;

    private float _currentHealth;
    private float _currentMana;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private Transform _currentTarget;
    private bool _isDead;
    private int _currentSpellIndex;

    private void Start()
    {
        _currentHealth = _maxHealth;
        _currentMana = _maxMana;
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_castPoint == null)
            _castPoint = transform;

        InitializeSpells();
        StartCoroutine(ManaRegeneration());
    }

    private void Update()
    {
        if (_isDead) return;

        HandleInput();
        FindTarget();
        UpdateAnimator();
    }

    private void InitializeSpells()
    {
        for (int i = 0; i < _spells.Length; i++)
        {
            if (_spells[i] == null)
            {
                _spells[i] = new ElementalSpell();
                _spells[i].element = (ElementType)i;
            }
        }
    }

    private void HandleInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        
        Vector3 movement = new Vector3(horizontal, 0, vertical) * _movementSpeed;
        _rigidbody.velocity = new Vector3(movement.x, _rigidbody.velocity.y, movement.z);

        if (Input.GetKeyDown(KeyCode.Alpha1)) CastSpell(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) CastSpell(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) CastSpell(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) CastSpell(3);
        
        if (Input.GetKeyDown(KeyCode.Tab)) CycleSpell();
        if (Input.GetMouseButtonDown(0)) CastSpell(_currentSpellIndex);
    }

    private void FindTarget()
    {
        Collider[] enemies = Physics.OverlapSphere(transform.position, _attackRange, _enemyLayers);
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (Collider enemy in enemies)
        {
            if (enemy.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, enemy.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestEnemy = enemy.transform;
            }
        }

        _currentTarget = closestEnemy;
        
        if (_currentTarget != null)
        {
            Vector3 direction = (_currentTarget.position - transform.position).normalized;
            direction.y = 0;
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private void CycleSpell()
    {
        _currentSpellIndex = (_currentSpellIndex + 1) % _spells.Length;
    }

    private void CastSpell(int spellIndex)
    {
        if (spellIndex < 0 || spellIndex >= _spells.Length) return;
        
        ElementalSpell spell = _spells[spellIndex];
        if (spell == null) return;

        if (Time.time - spell.lastCastTime < spell.cooldown) return;
        if (_currentMana < spell.manaCost) return;

        spell.lastCastTime = Time.time;
        _currentMana = Mathf.Max(0, _currentMana - spell.manaCost);
        OnManaChanged?.Invoke(_currentMana / _maxMana);

        StartCoroutine(PerformSpellCast(spell));
    }

    private IEnumerator PerformSpellCast(ElementalSpell spell)
    {
        if (_animator != null)
            _animator.SetTrigger("Cast");

        if (spell.castEffect != null)
            spell.castEffect.Play();

        if (spell.castSound != null && _audioSource != null)
            _audioSource.PlayOneShot(spell.castSound);

        yield return new WaitForSeconds(0.3f);

        CreateProjectile(spell);
        OnSpellCast?.Invoke(spell.element);
    }

    private void CreateProjectile(ElementalSpell spell)
    {
        if (spell.projectilePrefab == null) return;

        Vector3 spawnPosition = _castPoint.position;
        Vector3 direction = _currentTarget != null ? 
            (_currentTarget.position - spawnPosition).normalized : 
            transform.forward;

        GameObject projectile = Instantiate(spell.projectilePrefab, spawnPosition, Quaternion.LookRotation(direction));
        
        ElementalProjectile projectileScript = projectile.GetComponent<ElementalProjectile>();
        if (projectileScript == null)
            projectileScript = projectile.AddComponent<ElementalProjectile>();

        projectileScript.Initialize(spell.element, spell.damage, spell.projectileSpeed, direction);
    }

    private IEnumerator ManaRegeneration()
    {
        while (!_isDead)
        {
            if (_currentMana < _maxMana)
            {
                _currentMana = Mathf.Min(_maxMana, _currentMana + _manaRegenRate * Time.deltaTime);
                OnManaChanged?.Invoke(_currentMana / _maxMana);
            }
            yield return null;
        }
    }

    private void UpdateAnimator()
    {
        if (_animator == null) return;

        float speed = _rigidbody.velocity.magnitude;
        _animator.SetFloat("Speed", speed);
        _animator.SetBool("HasTarget", _currentTarget != null);
    }

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHealth = Mathf.Max(0, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth / _maxHealth);

        if (_animator != null)
            _animator.SetTrigger("Hit");

        if (_currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        _isDead = true;
        
        if (_animator != null)
            _animator.SetBool("Dead", true);

        OnDeath?.Invoke();
        
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        _rigidbody.isKinematic = true;
    }

    public float GetHealthPercentage() => _currentHealth / _maxHealth;
    public float GetManaPercentage() => _currentMana / _maxMana;
    public ElementType GetCurrentSpellElement() => _spells[_currentSpellIndex].element;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);
    }
}

public class ElementalProjectile : MonoBehaviour
{
    private WizardController.ElementType _element;
    private float _damage;
    private float _speed;
    private Vector3 _direction;
    private Rigidbody _rigidbody;
    private bool _initialized;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        
        _rigidbody.useGravity = false;
        Destroy(gameObject, 5f);
    }

    private void FixedUpdate()
    {
        if (_initialized && _rigidbody != null)
        {
            _rigidbody.velocity = _direction * _speed;
        }
    }

    public void Initialize(WizardController.ElementType element, float damage, float speed, Vector3 direction)
    {
        _element = element;
        _damage = damage;
        _speed = speed;
        _direction = direction.normalized;
        _initialized = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) return;

        ApplyElementalEffect(other);
        CreateImpactEffect();
        Destroy(gameObject);
    }

    private void ApplyElementalEffect(Collider target)
    {
        switch (_element)
        {
            case WizardController.ElementType.Fire:
                ApplyBurnEffect(target);
                break;
            case WizardController.ElementType.Ice:
                ApplyFreezeEffect(target);
                break;
            case WizardController.ElementType.Lightning:
                ApplyShockEffect(target);
                break;
            case WizardController.ElementType.Earth:
                ApplyKnockbackEffect(target);
                break;
        }
    }

    private void ApplyBurnEffect(Collider target)
    {
        // Apply damage over time
        StartCoroutine(BurnDamage(target));
    }

    private void ApplyFreezeEffect(Collider target)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.velocity *= 0.1f;
        }
    }

    private void ApplyShockEffect(Collider target)
    {
        // Chain lightning to nearby enemies
        Collider[] nearbyEnemies = Physics.OverlapSphere(target.transform.position, 5f);
        foreach (Collider enemy in nearbyEnemies)
        {
            if (enemy != target && !enemy.CompareTag("Player"))
            {
                // Apply reduced damage to chained targets
            }
        }
    }

    private void ApplyKnockbackEffect(Collider target)
    {
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 knockback = (target.transform.position - transform.position).normalized * 10f;
            targetRb.AddForce(knockback, ForceMode.Impulse);
        }
    }

    private IEnumerator BurnDamage(Collider target)
    {
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSeconds(1f);
            if (target != null)
            {
                // Apply burn damage
            }
        }
    }

    private void CreateImpactEffect()
    {
        // Create particle effect based on element type
        GameObject effect = new GameObject("Impact Effect");
        effect.transform.position = transform.position;
        
        ParticleSystem particles = effect.AddComponent<ParticleSystem>();
        var main = particles.main;
        
        switch (_element)
        {
            case WizardController.ElementType.Fire:
                main.startColor = Color.red;
                break;
            case WizardController.ElementType.Ice:
                main.startColor = Color.cyan;
                break;
            case WizardController.ElementType.Lightning:
                main.startColor = Color.yellow;
                break;
            case WizardController.ElementType.Earth:
                main.startColor = Color.brown;
                break;
        }
        
        Destroy(effect, 2f);
    }
}