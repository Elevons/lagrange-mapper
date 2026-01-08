// Prompt: spear with thrust attack
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class Spear : MonoBehaviour
{
    [Header("Spear Settings")]
    [SerializeField] private float _thrustDistance = 2f;
    [SerializeField] private float _thrustSpeed = 10f;
    [SerializeField] private float _thrustDuration = 0.3f;
    [SerializeField] private float _retractSpeed = 8f;
    [SerializeField] private float _cooldownTime = 1f;
    [SerializeField] private int _damage = 25;
    
    [Header("Input")]
    [SerializeField] private KeyCode _thrustKey = KeyCode.Mouse0;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _thrustSound;
    [SerializeField] private AudioClip _hitSound;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _thrustEffect;
    [SerializeField] private GameObject _hitEffect;
    
    [Header("Events")]
    public UnityEvent OnThrustStart;
    public UnityEvent OnThrustHit;
    public UnityEvent OnThrustComplete;
    
    private Vector3 _originalPosition;
    private Vector3 _thrustTargetPosition;
    private bool _isThrusting = false;
    private bool _isRetracting = false;
    private bool _canThrust = true;
    private float _thrustTimer = 0f;
    private float _cooldownTimer = 0f;
    
    private AudioSource _audioSource;
    private Collider _spearCollider;
    private Rigidbody _rigidbody;
    
    private void Start()
    {
        _originalPosition = transform.localPosition;
        _audioSource = GetComponent<AudioSource>();
        _spearCollider = GetComponent<Collider>();
        _rigidbody = GetComponent<Rigidbody>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_spearCollider == null)
        {
            _spearCollider = gameObject.AddComponent<CapsuleCollider>();
            _spearCollider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCooldown();
        UpdateThrustMovement();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_thrustKey) && _canThrust && !_isThrusting && !_isRetracting)
        {
            StartThrust();
        }
    }
    
    private void UpdateCooldown()
    {
        if (!_canThrust)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _canThrust = true;
            }
        }
    }
    
    private void UpdateThrustMovement()
    {
        if (_isThrusting)
        {
            _thrustTimer += Time.deltaTime;
            
            float progress = _thrustTimer / _thrustDuration;
            transform.localPosition = Vector3.Lerp(_originalPosition, _thrustTargetPosition, progress);
            
            if (progress >= 1f)
            {
                _isThrusting = false;
                _isRetracting = true;
                _thrustTimer = 0f;
            }
        }
        else if (_isRetracting)
        {
            float retractProgress = _retractSpeed * Time.deltaTime;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, _originalPosition, retractProgress);
            
            if (Vector3.Distance(transform.localPosition, _originalPosition) < 0.01f)
            {
                transform.localPosition = _originalPosition;
                _isRetracting = false;
                StartCooldown();
                OnThrustComplete?.Invoke();
            }
        }
    }
    
    private void StartThrust()
    {
        _isThrusting = true;
        _thrustTimer = 0f;
        _thrustTargetPosition = _originalPosition + transform.forward * _thrustDistance;
        
        PlaySound(_thrustSound);
        PlayThrustEffect();
        OnThrustStart?.Invoke();
    }
    
    private void StartCooldown()
    {
        _canThrust = false;
        _cooldownTimer = _cooldownTime;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void PlayThrustEffect()
    {
        if (_thrustEffect != null)
        {
            _thrustEffect.Play();
        }
    }
    
    private void PlayHitEffect(Vector3 hitPoint)
    {
        if (_hitEffect != null)
        {
            GameObject effect = Instantiate(_hitEffect, hitPoint, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isThrusting && other != null)
        {
            if (other.CompareTag("Enemy"))
            {
                DealDamage(other.gameObject);
                PlaySound(_hitSound);
                PlayHitEffect(other.ClosestPoint(transform.position));
                OnThrustHit?.Invoke();
            }
        }
    }
    
    private void DealDamage(GameObject target)
    {
        if (target == null) return;
        
        // Try different common health component patterns
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use SendMessage as a fallback for damage dealing
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
        }
        
        // Alternative: Destroy enemies with specific tags
        if (target.CompareTag("Enemy"))
        {
            Destroy(target);
        }
    }
    
    public void SetDamage(int newDamage)
    {
        _damage = Mathf.Max(0, newDamage);
    }
    
    public void SetThrustDistance(float newDistance)
    {
        _thrustDistance = Mathf.Max(0.1f, newDistance);
    }
    
    public void SetCooldownTime(float newCooldown)
    {
        _cooldownTime = Mathf.Max(0f, newCooldown);
    }
    
    public bool IsThrusting()
    {
        return _isThrusting;
    }
    
    public bool CanThrust()
    {
        return _canThrust && !_isThrusting && !_isRetracting;
    }
    
    public float GetCooldownProgress()
    {
        if (_canThrust) return 1f;
        return 1f - (_cooldownTimer / _cooldownTime);
    }
}