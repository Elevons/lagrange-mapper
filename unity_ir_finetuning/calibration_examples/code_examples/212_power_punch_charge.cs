// Prompt: power punch charge
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class PowerPunchCharge : MonoBehaviour
{
    [Header("Charge Settings")]
    [SerializeField] private float _maxChargeTime = 3f;
    [SerializeField] private float _minChargeTime = 0.5f;
    [SerializeField] private AnimationCurve _chargeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Input")]
    [SerializeField] private KeyCode _chargeKey = KeyCode.Mouse0;
    [SerializeField] private bool _holdToCharge = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _chargeEffect;
    [SerializeField] private Transform _chargePoint;
    [SerializeField] private GameObject _chargeIndicator;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _chargeSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Feedback")]
    [SerializeField] private float _cameraShakeIntensity = 0.1f;
    [SerializeField] private Color _minChargeColor = Color.yellow;
    [SerializeField] private Color _maxChargeColor = Color.red;
    
    [Header("Events")]
    public UnityEvent<float> OnChargeStarted;
    public UnityEvent<float> OnChargeUpdated;
    public UnityEvent<float> OnPunchReleased;
    public UnityEvent OnChargeCancelled;
    
    private float _currentChargeTime;
    private float _chargePercentage;
    private bool _isCharging;
    private bool _canCharge = true;
    private Camera _mainCamera;
    private Vector3 _originalCameraPosition;
    private Renderer _indicatorRenderer;
    private Light _chargeLight;
    
    [System.Serializable]
    public class ChargeLevel
    {
        public float threshold;
        public float damageMultiplier;
        public float forceMultiplier;
        public Color effectColor;
        public string levelName;
    }
    
    [Header("Charge Levels")]
    [SerializeField] private ChargeLevel[] _chargeLevels = new ChargeLevel[]
    {
        new ChargeLevel { threshold = 0.33f, damageMultiplier = 1.5f, forceMultiplier = 1.2f, effectColor = Color.yellow, levelName = "Light" },
        new ChargeLevel { threshold = 0.66f, damageMultiplier = 2f, forceMultiplier = 1.5f, effectColor = Color.orange, levelName = "Medium" },
        new ChargeLevel { threshold = 1f, damageMultiplier = 3f, forceMultiplier = 2f, effectColor = Color.red, levelName = "Heavy" }
    };
    
    private void Start()
    {
        _mainCamera = Camera.main;
        if (_mainCamera != null)
            _originalCameraPosition = _mainCamera.transform.localPosition;
            
        if (_chargeIndicator != null)
        {
            _indicatorRenderer = _chargeIndicator.GetComponent<Renderer>();
            _chargeLight = _chargeIndicator.GetComponent<Light>();
            _chargeIndicator.SetActive(false);
        }
        
        if (_chargeEffect != null)
            _chargeEffect.Stop();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCharge();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        if (!_canCharge) return;
        
        bool inputPressed = Input.GetKeyDown(_chargeKey);
        bool inputHeld = Input.GetKey(_chargeKey);
        bool inputReleased = Input.GetKeyUp(_chargeKey);
        
        if (inputPressed && !_isCharging)
        {
            StartCharge();
        }
        else if (_holdToCharge && inputHeld && _isCharging)
        {
            ContinueCharge();
        }
        else if (inputReleased && _isCharging)
        {
            ReleasePunch();
        }
        else if (!_holdToCharge && inputPressed && _isCharging)
        {
            ReleasePunch();
        }
    }
    
    private void StartCharge()
    {
        _isCharging = true;
        _currentChargeTime = 0f;
        _chargePercentage = 0f;
        
        if (_chargeIndicator != null)
            _chargeIndicator.SetActive(true);
            
        if (_chargeEffect != null)
            _chargeEffect.Play();
            
        if (_audioSource != null && _chargeSound != null)
            _audioSource.PlayOneShot(_chargeSound);
            
        OnChargeStarted?.Invoke(_chargePercentage);
    }
    
    private void ContinueCharge()
    {
        _currentChargeTime += Time.deltaTime;
        _chargePercentage = Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
        
        OnChargeUpdated?.Invoke(_chargePercentage);
    }
    
    private void UpdateCharge()
    {
        if (!_isCharging) return;
        
        if (!_holdToCharge)
        {
            _currentChargeTime += Time.deltaTime;
            _chargePercentage = Mathf.Clamp01(_currentChargeTime / _maxChargeTime);
            OnChargeUpdated?.Invoke(_chargePercentage);
        }
        
        // Apply charge curve
        float curvedCharge = _chargeCurve.Evaluate(_chargePercentage);
        
        // Camera shake based on charge level
        if (_mainCamera != null && _chargePercentage > 0.3f)
        {
            float shakeAmount = curvedCharge * _cameraShakeIntensity;
            Vector3 shakeOffset = Random.insideUnitSphere * shakeAmount;
            _mainCamera.transform.localPosition = _originalCameraPosition + shakeOffset;
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (!_isCharging) return;
        
        // Update indicator color
        if (_indicatorRenderer != null)
        {
            Color currentColor = Color.Lerp(_minChargeColor, _maxChargeColor, _chargePercentage);
            _indicatorRenderer.material.color = currentColor;
        }
        
        // Update light intensity
        if (_chargeLight != null)
        {
            _chargeLight.intensity = _chargePercentage * 2f;
            _chargeLight.color = Color.Lerp(_minChargeColor, _maxChargeColor, _chargePercentage);
        }
        
        // Update particle effect
        if (_chargeEffect != null)
        {
            var main = _chargeEffect.main;
            main.startColor = Color.Lerp(_minChargeColor, _maxChargeColor, _chargePercentage);
            
            var emission = _chargeEffect.emission;
            emission.rateOverTime = _chargePercentage * 50f;
        }
    }
    
    private void ReleasePunch()
    {
        if (_currentChargeTime < _minChargeTime)
        {
            CancelCharge();
            return;
        }
        
        float finalChargePercentage = _chargePercentage;
        ChargeLevel currentLevel = GetChargeLevel(finalChargePercentage);
        
        // Reset camera position
        if (_mainCamera != null)
            _mainCamera.transform.localPosition = _originalCameraPosition;
            
        // Stop effects
        if (_chargeIndicator != null)
            _chargeIndicator.SetActive(false);
            
        if (_chargeEffect != null)
            _chargeEffect.Stop();
            
        if (_audioSource != null && _releaseSound != null)
            _audioSource.PlayOneShot(_releaseSound);
            
        // Perform punch logic
        PerformPunch(currentLevel, finalChargePercentage);
        
        OnPunchReleased?.Invoke(finalChargePercentage);
        
        _isCharging = false;
        _currentChargeTime = 0f;
        _chargePercentage = 0f;
    }
    
    private void CancelCharge()
    {
        _isCharging = false;
        _currentChargeTime = 0f;
        _chargePercentage = 0f;
        
        if (_mainCamera != null)
            _mainCamera.transform.localPosition = _originalCameraPosition;
            
        if (_chargeIndicator != null)
            _chargeIndicator.SetActive(false);
            
        if (_chargeEffect != null)
            _chargeEffect.Stop();
            
        OnChargeCancelled?.Invoke();
    }
    
    private ChargeLevel GetChargeLevel(float chargePercentage)
    {
        ChargeLevel selectedLevel = _chargeLevels[0];
        
        for (int i = 0; i < _chargeLevels.Length; i++)
        {
            if (chargePercentage >= _chargeLevels[i].threshold)
                selectedLevel = _chargeLevels[i];
        }
        
        return selectedLevel;
    }
    
    private void PerformPunch(ChargeLevel level, float chargePercentage)
    {
        // Raycast or sphere cast to detect targets
        Vector3 punchDirection = transform.forward;
        Vector3 punchOrigin = _chargePoint != null ? _chargePoint.position : transform.position;
        
        float punchRange = 2f + (chargePercentage * 3f);
        RaycastHit[] hits = Physics.SphereCastAll(punchOrigin, 0.5f, punchDirection, punchRange);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            
            // Apply force to rigidbodies
            Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 forceDirection = (hit.point - punchOrigin).normalized;
                float force = level.forceMultiplier * chargePercentage * 1000f;
                targetRb.AddForce(forceDirection * force, ForceMode.Impulse);
            }
            
            // Damage destructible objects
            if (hit.collider.CompareTag("Destructible"))
            {
                Destroy(hit.collider.gameObject);
            }
        }
    }
    
    public void SetCanCharge(bool canCharge)
    {
        _canCharge = canCharge;
        if (!canCharge && _isCharging)
            CancelCharge();
    }
    
    public float GetCurrentChargePercentage()
    {
        return _chargePercentage;
    }
    
    public bool IsCharging()
    {
        return _isCharging;
    }
    
    public ChargeLevel GetCurrentChargeLevel()
    {
        return GetChargeLevel(_chargePercentage);
    }
    
    private void OnDrawGizmosSelected()
    {
        Vector3 origin = _chargePoint != null ? _chargePoint.position : transform.position;
        Vector3 direction = transform.forward;
        
        Gizmos.color = Color.red;
        Gizmos.DrawRay(origin, direction * (2f + (_chargePercentage * 3f)));
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin, 0.5f);
    }
}