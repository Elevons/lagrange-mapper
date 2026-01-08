// Prompt: swimming with oxygen meter
// Type: general

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SwimmingController : MonoBehaviour
{
    [System.Serializable]
    public class SwimmingEvents
    {
        public UnityEvent OnStartSwimming;
        public UnityEvent OnStopSwimming;
        public UnityEvent OnOxygenDepleted;
        public UnityEvent OnOxygenRestored;
    }

    [Header("Swimming Settings")]
    [SerializeField] private float _swimSpeed = 3f;
    [SerializeField] private float _swimUpForce = 5f;
    [SerializeField] private float _swimDownForce = 3f;
    [SerializeField] private float _waterDrag = 5f;
    [SerializeField] private float _waterAngularDrag = 5f;
    [SerializeField] private LayerMask _waterLayer = 1;

    [Header("Oxygen System")]
    [SerializeField] private float _maxOxygen = 100f;
    [SerializeField] private float _oxygenDepletionRate = 10f;
    [SerializeField] private float _oxygenRestoreRate = 20f;
    [SerializeField] private float _lowOxygenThreshold = 25f;
    [SerializeField] private bool _canDrownWhenOxygenDepleted = true;

    [Header("UI References")]
    [SerializeField] private Slider _oxygenMeter;
    [SerializeField] private Image _oxygenFill;
    [SerializeField] private Color _normalOxygenColor = Color.cyan;
    [SerializeField] private Color _lowOxygenColor = Color.red;
    [SerializeField] private GameObject _oxygenUI;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _splashSound;
    [SerializeField] private AudioClip _breathingSound;
    [SerializeField] private AudioClip _drowningSound;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private ParticleSystem _splashEffect;

    [Header("Events")]
    [SerializeField] private SwimmingEvents _events;

    private Rigidbody _rigidbody;
    private bool _isSwimming = false;
    private bool _isUnderwater = false;
    private float _currentOxygen;
    private float _originalDrag;
    private float _originalAngularDrag;
    private bool _isDrowning = false;
    private float _breathingTimer = 0f;
    private float _breathingInterval = 2f;

    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        _originalDrag = _rigidbody.drag;
        _originalAngularDrag = _rigidbody.angularDrag;
        _currentOxygen = _maxOxygen;

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }

        InitializeUI();
    }

    private void Update()
    {
        HandleSwimmingInput();
        UpdateOxygenSystem();
        UpdateUI();
        UpdateEffects();
    }

    private void HandleSwimmingInput()
    {
        if (!_isSwimming) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        bool swimUp = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.E);
        bool swimDown = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.Q);

        Vector3 swimDirection = Vector3.zero;

        // Horizontal movement
        swimDirection += transform.right * horizontal;
        swimDirection += transform.forward * vertical;

        // Vertical movement
        if (swimUp)
        {
            swimDirection += Vector3.up;
        }
        else if (swimDown)
        {
            swimDirection += Vector3.down;
        }

        // Apply swimming force
        if (swimDirection.magnitude > 0.1f)
        {
            swimDirection.Normalize();
            Vector3 force = swimDirection * _swimSpeed;
            
            if (swimDirection.y > 0)
            {
                force.y *= _swimUpForce / _swimSpeed;
            }
            else if (swimDirection.y < 0)
            {
                force.y *= _swimDownForce / _swimSpeed;
            }

            _rigidbody.AddForce(force, ForceMode.Acceleration);
        }
    }

    private void UpdateOxygenSystem()
    {
        if (_isUnderwater)
        {
            _currentOxygen -= _oxygenDepletionRate * Time.deltaTime;
            _currentOxygen = Mathf.Max(0f, _currentOxygen);

            if (_currentOxygen <= 0f && !_isDrowning)
            {
                StartDrowning();
            }

            // Breathing sound effect
            _breathingTimer += Time.deltaTime;
            if (_breathingTimer >= _breathingInterval && _breathingSound != null)
            {
                _audioSource.PlayOneShot(_breathingSound, 0.3f);
                _breathingTimer = 0f;
            }
        }
        else if (_isSwimming && !_isUnderwater)
        {
            // Restore oxygen when at surface
            _currentOxygen += _oxygenRestoreRate * Time.deltaTime;
            _currentOxygen = Mathf.Min(_maxOxygen, _currentOxygen);

            if (_isDrowning && _currentOxygen > _lowOxygenThreshold)
            {
                StopDrowning();
            }
        }
    }

    private void UpdateUI()
    {
        if (_oxygenMeter != null)
        {
            _oxygenMeter.value = _currentOxygen / _maxOxygen;
        }

        if (_oxygenFill != null)
        {
            _oxygenFill.color = _currentOxygen <= _lowOxygenThreshold ? _lowOxygenColor : _normalOxygenColor;
        }

        if (_oxygenUI != null)
        {
            _oxygenUI.SetActive(_isSwimming);
        }
    }

    private void UpdateEffects()
    {
        if (_bubbleEffect != null)
        {
            if (_isUnderwater && _currentOxygen > 0f)
            {
                if (!_bubbleEffect.isPlaying)
                {
                    _bubbleEffect.Play();
                }
            }
            else
            {
                if (_bubbleEffect.isPlaying)
                {
                    _bubbleEffect.Stop();
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsWaterLayer(other.gameObject.layer))
        {
            StartSwimming();
            
            if (_splashEffect != null)
            {
                _splashEffect.transform.position = other.ClosestPoint(transform.position);
                _splashEffect.Play();
            }

            if (_splashSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_splashSound);
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsWaterLayer(other.gameObject.layer))
        {
            // Check if player is fully submerged
            Vector3 waterSurface = other.bounds.max;
            waterSurface.x = transform.position.x;
            waterSurface.z = transform.position.z;

            _isUnderwater = transform.position.y < waterSurface.y - 0.5f;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsWaterLayer(other.gameObject.layer))
        {
            StopSwimming();
            _isUnderwater = false;

            if (_splashEffect != null)
            {
                _splashEffect.transform.position = other.ClosestPoint(transform.position);
                _splashEffect.Play();
            }

            if (_splashSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_splashSound);
            }
        }
    }

    private void StartSwimming()
    {
        if (_isSwimming) return;

        _isSwimming = true;
        _rigidbody.drag = _waterDrag;
        _rigidbody.angularDrag = _waterAngularDrag;
        
        _events?.OnStartSwimming?.Invoke();
    }

    private void StopSwimming()
    {
        if (!_isSwimming) return;

        _isSwimming = false;
        _rigidbody.drag = _originalDrag;
        _rigidbody.angularDrag = _originalAngularDrag;
        
        if (_isDrowning)
        {
            StopDrowning();
        }

        _events?.OnStopSwimming?.Invoke();
    }

    private void StartDrowning()
    {
        _isDrowning = true;
        
        if (_drowningSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_drowningSound);
        }

        _events?.OnOxygenDepleted?.Invoke();

        if (_canDrownWhenOxygenDepleted)
        {
            // Apply damage or death logic here
            Debug.Log("Player is drowning!");
        }
    }

    private void StopDrowning()
    {
        _isDrowning = false;
        _events?.OnOxygenRestored?.Invoke();
    }

    private bool IsWaterLayer(int layer)
    {
        return (_waterLayer.value & (1 << layer)) != 0;
    }

    private void InitializeUI()
    {
        if (_oxygenMeter != null)
        {
            _oxygenMeter.minValue = 0f;
            _oxygenMeter.maxValue = 1f;
            _oxygenMeter.value = 1f;
        }

        if (_oxygenUI != null)
        {
            _oxygenUI.SetActive(false);
        }
    }

    public float GetOxygenPercentage()
    {
        return _currentOxygen / _maxOxygen;
    }

    public bool IsSwimming()
    {
        return _isSwimming;
    }

    public bool IsUnderwater()
    {
        return _isUnderwater;
    }

    public bool IsDrowning()
    {
        return _isDrowning;
    }

    public void RestoreOxygen(float amount)
    {
        _currentOxygen = Mathf.Min(_maxOxygen, _currentOxygen + amount);
    }

    public void SetMaxOxygen(float newMaxOxygen)
    {
        _maxOxygen = newMaxOxygen;
        _currentOxygen = Mathf.Min(_currentOxygen, _maxOxygen);
    }
}