// Prompt: whip with long range
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Whip : MonoBehaviour
{
    [Header("Whip Settings")]
    [SerializeField] private float _whipRange = 8f;
    [SerializeField] private float _whipDamage = 25f;
    [SerializeField] private float _whipCooldown = 1.5f;
    [SerializeField] private float _whipDuration = 0.3f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private LineRenderer _whipRenderer;
    [SerializeField] private Transform _whipOrigin;
    [SerializeField] private AnimationCurve _whipCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float _whipWidth = 0.1f;
    [SerializeField] private Color _whipColor = Color.brown;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _whipCrackSound;
    [SerializeField] private AudioClip _whipHitSound;
    
    [Header("Input")]
    [SerializeField] private KeyCode _whipKey = KeyCode.Mouse0;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnWhipHit;
    public UnityEvent OnWhipCrack;
    
    private Camera _playerCamera;
    private bool _isWhipping;
    private float _lastWhipTime;
    private Vector3 _whipTargetPosition;
    private float _whipAnimationTime;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_whipRenderer == null)
            _whipRenderer = GetComponent<LineRenderer>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_whipOrigin == null)
            _whipOrigin = transform;
            
        SetupWhipRenderer();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateWhipAnimation();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_whipKey) && CanWhip())
        {
            PerformWhip();
        }
    }
    
    private bool CanWhip()
    {
        return !_isWhipping && Time.time >= _lastWhipTime + _whipCooldown;
    }
    
    private void PerformWhip()
    {
        _isWhipping = true;
        _lastWhipTime = Time.time;
        _whipAnimationTime = 0f;
        
        Vector3 whipDirection = GetWhipDirection();
        _whipTargetPosition = _whipOrigin.position + whipDirection * _whipRange;
        
        CheckWhipHit(whipDirection);
        PlayWhipSound();
        OnWhipCrack?.Invoke();
    }
    
    private Vector3 GetWhipDirection()
    {
        if (_playerCamera != null)
        {
            Ray ray = _playerCamera.ScreenPointToRay(Input.mousePosition);
            return ray.direction;
        }
        else
        {
            return transform.forward;
        }
    }
    
    private void CheckWhipHit(Vector3 direction)
    {
        RaycastHit[] hits = Physics.RaycastAll(_whipOrigin.position, direction, _whipRange, _targetLayers);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject != gameObject)
            {
                ProcessHit(hit.collider.gameObject, hit.point);
            }
        }
    }
    
    private void ProcessHit(GameObject hitObject, Vector3 hitPoint)
    {
        // Apply damage if target has health component
        var healthComponent = hitObject.GetComponent<Health>();
        if (healthComponent != null)
        {
            healthComponent.TakeDamage(_whipDamage);
        }
        
        // Apply knockback if target has rigidbody
        var rb = hitObject.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 knockbackDirection = (hitPoint - _whipOrigin.position).normalized;
            rb.AddForce(knockbackDirection * 500f, ForceMode.Impulse);
        }
        
        PlayHitSound();
        OnWhipHit?.Invoke(hitObject);
    }
    
    private void UpdateWhipAnimation()
    {
        if (!_isWhipping)
        {
            _whipRenderer.enabled = false;
            return;
        }
        
        _whipRenderer.enabled = true;
        _whipAnimationTime += Time.deltaTime;
        
        float progress = _whipAnimationTime / _whipDuration;
        
        if (progress >= 1f)
        {
            _isWhipping = false;
            _whipRenderer.enabled = false;
            return;
        }
        
        UpdateWhipVisual(progress);
    }
    
    private void UpdateWhipVisual(float progress)
    {
        float curveValue = _whipCurve.Evaluate(progress);
        Vector3 currentTarget = Vector3.Lerp(_whipOrigin.position, _whipTargetPosition, curveValue);
        
        int segments = 20;
        _whipRenderer.positionCount = segments + 1;
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 point = Vector3.Lerp(_whipOrigin.position, currentTarget, t);
            
            // Add some curve to make it look more whip-like
            float height = Mathf.Sin(t * Mathf.PI) * 0.5f * curveValue;
            point += Vector3.up * height;
            
            _whipRenderer.SetPosition(i, point);
        }
    }
    
    private void SetupWhipRenderer()
    {
        if (_whipRenderer != null)
        {
            _whipRenderer.material = new Material(Shader.Find("Sprites/Default"));
            _whipRenderer.color = _whipColor;
            _whipRenderer.startWidth = _whipWidth;
            _whipRenderer.endWidth = _whipWidth * 0.3f;
            _whipRenderer.useWorldSpace = true;
            _whipRenderer.enabled = false;
        }
    }
    
    private void PlayWhipSound()
    {
        if (_audioSource != null && _whipCrackSound != null)
        {
            _audioSource.PlayOneShot(_whipCrackSound);
        }
    }
    
    private void PlayHitSound()
    {
        if (_audioSource != null && _whipHitSound != null)
        {
            _audioSource.PlayOneShot(_whipHitSound);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_whipOrigin != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_whipOrigin.position, _whipRange);
            
            if (_isWhipping)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_whipOrigin.position, _whipTargetPosition);
            }
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
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        OnHealthChanged?.Invoke(_currentHealth);
        
        if (_currentHealth <= 0f)
        {
            OnDeath?.Invoke();
        }
    }
    
    public void Heal(float amount)
    {
        _currentHealth = Mathf.Min(_maxHealth, _currentHealth + amount);
        OnHealthChanged?.Invoke(_currentHealth);
    }
    
    public float GetHealthPercentage()
    {
        return _currentHealth / _maxHealth;
    }
}