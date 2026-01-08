// Prompt: size change ability
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class SizeChangeAbility : MonoBehaviour
{
    [System.Serializable]
    public class SizeChangeEvent : UnityEvent<float> { }
    
    [Header("Size Settings")]
    [SerializeField] private float _minSize = 0.5f;
    [SerializeField] private float _maxSize = 3.0f;
    [SerializeField] private float _defaultSize = 1.0f;
    [SerializeField] private float _sizeChangeSpeed = 2.0f;
    [SerializeField] private AnimationCurve _sizeTransitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Input")]
    [SerializeField] private KeyCode _growKey = KeyCode.E;
    [SerializeField] private KeyCode _shrinkKey = KeyCode.Q;
    [SerializeField] private KeyCode _resetKey = KeyCode.R;
    [SerializeField] private bool _useMouseWheel = true;
    [SerializeField] private float _mouseWheelSensitivity = 0.5f;
    
    [Header("Energy System")]
    [SerializeField] private bool _useEnergySystem = true;
    [SerializeField] private float _maxEnergy = 100f;
    [SerializeField] private float _energyDrainRate = 10f;
    [SerializeField] private float _energyRegenRate = 5f;
    [SerializeField] private float _minEnergyToActivate = 10f;
    
    [Header("Physics")]
    [SerializeField] private bool _adjustMass = true;
    [SerializeField] private float _baseMass = 1f;
    [SerializeField] private bool _adjustCollider = true;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _sizeChangeEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _growSound;
    [SerializeField] private AudioClip _shrinkSound;
    [SerializeField] private AudioClip _resetSound;
    
    [Header("Events")]
    public SizeChangeEvent OnSizeChanged;
    public UnityEvent OnGrow;
    public UnityEvent OnShrink;
    public UnityEvent OnReset;
    public UnityEvent OnEnergyDepleted;
    
    private float _currentSize;
    private float _targetSize;
    private Vector3 _originalScale;
    private float _currentEnergy;
    private bool _isChangingSize;
    private float _sizeChangeTimer;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Vector3 _originalColliderSize;
    private Vector3 _originalColliderCenter;
    
    private void Start()
    {
        _originalScale = transform.localScale;
        _currentSize = _defaultSize;
        _targetSize = _defaultSize;
        _currentEnergy = _maxEnergy;
        
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        
        if (_collider != null)
        {
            if (_collider is BoxCollider boxCollider)
            {
                _originalColliderSize = boxCollider.size;
                _originalColliderCenter = boxCollider.center;
            }
            else if (_collider is SphereCollider sphereCollider)
            {
                _originalColliderSize = Vector3.one * sphereCollider.radius;
                _originalColliderCenter = sphereCollider.center;
            }
            else if (_collider is CapsuleCollider capsuleCollider)
            {
                _originalColliderSize = new Vector3(capsuleCollider.radius, capsuleCollider.height, capsuleCollider.radius);
                _originalColliderCenter = capsuleCollider.center;
            }
        }
        
        ApplySize(_currentSize);
    }
    
    private void Update()
    {
        HandleInput();
        UpdateEnergy();
        UpdateSizeTransition();
    }
    
    private void HandleInput()
    {
        if (!_useEnergySystem || _currentEnergy >= _minEnergyToActivate)
        {
            if (Input.GetKeyDown(_growKey))
            {
                Grow();
            }
            else if (Input.GetKeyDown(_shrinkKey))
            {
                Shrink();
            }
            
            if (_useMouseWheel)
            {
                float scroll = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    ChangeSize(scroll * _mouseWheelSensitivity);
                }
            }
        }
        
        if (Input.GetKeyDown(_resetKey))
        {
            ResetSize();
        }
    }
    
    private void UpdateEnergy()
    {
        if (!_useEnergySystem) return;
        
        if (_isChangingSize && _currentSize != _defaultSize)
        {
            _currentEnergy -= _energyDrainRate * Time.deltaTime;
            _currentEnergy = Mathf.Max(0, _currentEnergy);
            
            if (_currentEnergy <= 0)
            {
                OnEnergyDepleted?.Invoke();
                ResetSize();
            }
        }
        else if (!_isChangingSize || _currentSize == _defaultSize)
        {
            _currentEnergy += _energyRegenRate * Time.deltaTime;
            _currentEnergy = Mathf.Min(_maxEnergy, _currentEnergy);
        }
    }
    
    private void UpdateSizeTransition()
    {
        if (_isChangingSize)
        {
            _sizeChangeTimer += Time.deltaTime * _sizeChangeSpeed;
            float progress = _sizeChangeTimer;
            
            if (progress >= 1f)
            {
                progress = 1f;
                _isChangingSize = false;
            }
            
            float curveValue = _sizeTransitionCurve.Evaluate(progress);
            float newSize = Mathf.Lerp(_currentSize, _targetSize, curveValue);
            
            ApplySize(newSize);
            
            if (!_isChangingSize)
            {
                _currentSize = _targetSize;
            }
        }
    }
    
    public void Grow()
    {
        ChangeSize(0.5f);
        OnGrow?.Invoke();
        PlaySound(_growSound);
    }
    
    public void Shrink()
    {
        ChangeSize(-0.5f);
        OnShrink?.Invoke();
        PlaySound(_shrinkSound);
    }
    
    public void ChangeSize(float amount)
    {
        if (_useEnergySystem && _currentEnergy < _minEnergyToActivate && amount != 0) return;
        
        float newTargetSize = _targetSize + amount;
        newTargetSize = Mathf.Clamp(newTargetSize, _minSize, _maxSize);
        
        if (Mathf.Abs(newTargetSize - _targetSize) > 0.01f)
        {
            _targetSize = newTargetSize;
            _isChangingSize = true;
            _sizeChangeTimer = 0f;
            
            PlayEffect();
        }
    }
    
    public void SetSize(float size)
    {
        size = Mathf.Clamp(size, _minSize, _maxSize);
        _targetSize = size;
        _isChangingSize = true;
        _sizeChangeTimer = 0f;
        
        PlayEffect();
    }
    
    public void ResetSize()
    {
        _targetSize = _defaultSize;
        _isChangingSize = true;
        _sizeChangeTimer = 0f;
        
        OnReset?.Invoke();
        PlaySound(_resetSound);
        PlayEffect();
    }
    
    private void ApplySize(float size)
    {
        Vector3 newScale = _originalScale * size;
        transform.localScale = newScale;
        
        if (_adjustMass && _rigidbody != null)
        {
            _rigidbody.mass = _baseMass * (size * size * size);
        }
        
        if (_adjustCollider && _collider != null)
        {
            if (_collider is BoxCollider boxCollider)
            {
                boxCollider.size = _originalColliderSize * size;
                boxCollider.center = _originalColliderCenter * size;
            }
            else if (_collider is SphereCollider sphereCollider)
            {
                sphereCollider.radius = _originalColliderSize.x * size;
                sphereCollider.center = _originalColliderCenter * size;
            }
            else if (_collider is CapsuleCollider capsuleCollider)
            {
                capsuleCollider.radius = _originalColliderSize.x * size;
                capsuleCollider.height = _originalColliderSize.y * size;
                capsuleCollider.center = _originalColliderCenter * size;
            }
        }
        
        OnSizeChanged?.Invoke(size);
    }
    
    private void PlayEffect()
    {
        if (_sizeChangeEffect != null)
        {
            _sizeChangeEffect.Play();
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public float GetCurrentSize()
    {
        return _currentSize;
    }
    
    public float GetTargetSize()
    {
        return _targetSize;
    }
    
    public float GetCurrentEnergy()
    {
        return _currentEnergy;
    }
    
    public float GetEnergyPercentage()
    {
        return _useEnergySystem ? _currentEnergy / _maxEnergy : 1f;
    }
    
    public bool IsChangingSize()
    {
        return _isChangingSize;
    }
    
    public bool CanChangeSize()
    {
        return !_useEnergySystem || _currentEnergy >= _minEnergyToActivate;
    }
    
    public void SetEnergy(float energy)
    {
        _currentEnergy = Mathf.Clamp(energy, 0, _maxEnergy);
    }
    
    public void AddEnergy(float amount)
    {
        _currentEnergy = Mathf.Clamp(_currentEnergy + amount, 0, _maxEnergy);
    }
}