// Prompt: electrified floor that pulses
// Type: general

using UnityEngine;
using System.Collections;

public class ElectrifiedFloor : MonoBehaviour
{
    [Header("Visual Effects")]
    [SerializeField] private Material _electricMaterial;
    [SerializeField] private Color _baseColor = Color.blue;
    [SerializeField] private Color _pulseColor = Color.white;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private AnimationCurve _pulseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Electrical Effects")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private float _knockbackForce = 500f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _electricHumSound;
    [SerializeField] private AudioClip _zapSound;
    [SerializeField] private float _humVolume = 0.3f;
    [SerializeField] private float _zapVolume = 0.7f;
    
    [Header("Particle Effects")]
    [SerializeField] private ParticleSystem _sparkParticles;
    [SerializeField] private Light _electricLight;
    
    private Renderer _renderer;
    private Collider _floorCollider;
    private float _pulseTimer;
    private bool _isActive = true;
    private System.Collections.Generic.HashSet<GameObject> _objectsOnFloor = new System.Collections.Generic.HashSet<GameObject>();
    private System.Collections.Generic.Dictionary<GameObject, float> _lastDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();

    private void Start()
    {
        InitializeComponents();
        StartCoroutine(PulseEffect());
        
        if (_audioSource && _electricHumSound)
        {
            _audioSource.clip = _electricHumSound;
            _audioSource.volume = _humVolume;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    private void InitializeComponents()
    {
        _renderer = GetComponent<Renderer>();
        _floorCollider = GetComponent<Collider>();
        
        if (!_floorCollider)
        {
            _floorCollider = gameObject.AddComponent<BoxCollider>();
            _floorCollider.isTrigger = true;
        }
        
        if (!_audioSource)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_renderer && _electricMaterial)
        {
            _renderer.material = _electricMaterial;
        }
        
        if (_sparkParticles)
        {
            _sparkParticles.Play();
        }
    }

    private void Update()
    {
        if (_isActive)
        {
            DamageObjectsOnFloor();
            UpdateLightIntensity();
        }
    }

    private IEnumerator PulseEffect()
    {
        while (true)
        {
            _pulseTimer += Time.deltaTime * _pulseSpeed;
            
            if (_renderer && _renderer.material)
            {
                float pulseValue = _pulseCurve.Evaluate(Mathf.PingPong(_pulseTimer, 1f));
                Color currentColor = Color.Lerp(_baseColor, _pulseColor, pulseValue);
                
                if (_renderer.material.HasProperty("_Color"))
                {
                    _renderer.material.color = currentColor;
                }
                else if (_renderer.material.HasProperty("_BaseColor"))
                {
                    _renderer.material.SetColor("_BaseColor", currentColor);
                }
                
                if (_renderer.material.HasProperty("_EmissionColor"))
                {
                    _renderer.material.SetColor("_EmissionColor", currentColor * 0.5f);
                }
            }
            
            yield return null;
        }
    }

    private void UpdateLightIntensity()
    {
        if (_electricLight)
        {
            float pulseValue = _pulseCurve.Evaluate(Mathf.PingPong(_pulseTimer, 1f));
            _electricLight.intensity = Mathf.Lerp(0.5f, 2f, pulseValue);
            _electricLight.color = Color.Lerp(_baseColor, _pulseColor, pulseValue);
        }
    }

    private void DamageObjectsOnFloor()
    {
        foreach (GameObject obj in _objectsOnFloor)
        {
            if (obj == null) continue;
            
            if (!_lastDamageTime.ContainsKey(obj))
            {
                _lastDamageTime[obj] = 0f;
            }
            
            if (Time.time - _lastDamageTime[obj] >= _damageInterval)
            {
                ApplyElectricalDamage(obj);
                _lastDamageTime[obj] = Time.time;
            }
        }
    }

    private void ApplyElectricalDamage(GameObject target)
    {
        // Apply damage if target has health component
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Try to find common health method names
            var healthType = healthComponent.GetType();
            var takeDamageMethod = healthType.GetMethod("TakeDamage");
            var damageMethod = healthType.GetMethod("Damage");
            
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(healthComponent, new object[] { _damageAmount });
            }
            else if (damageMethod != null)
            {
                damageMethod.Invoke(healthComponent, new object[] { _damageAmount });
            }
        }
        
        // Apply knockback
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 knockbackDirection = (target.transform.position - transform.position).normalized;
            knockbackDirection.y = 0.5f; // Add upward force
            rb.AddForce(knockbackDirection * _knockbackForce, ForceMode.Impulse);
        }
        
        // Play zap sound
        if (_audioSource && _zapSound)
        {
            _audioSource.PlayOneShot(_zapSound, _zapVolume);
        }
        
        // Spawn spark effect at target position
        if (_sparkParticles)
        {
            _sparkParticles.transform.position = target.transform.position;
            _sparkParticles.Emit(10);
        }
        
        // Screen shake effect for player
        if (target.CompareTag("Player"))
        {
            StartCoroutine(ScreenShake());
        }
    }

    private IEnumerator ScreenShake()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) yield break;
        
        Vector3 originalPosition = mainCamera.transform.position;
        float shakeDuration = 0.2f;
        float shakeIntensity = 0.1f;
        
        float elapsed = 0f;
        while (elapsed < shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            
            mainCamera.transform.position = originalPosition + new Vector3(x, y, 0);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        mainCamera.transform.position = originalPosition;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsValidTarget(other.gameObject))
        {
            _objectsOnFloor.Add(other.gameObject);
            
            // Immediate damage on entry
            if (_isActive)
            {
                ApplyElectricalDamage(other.gameObject);
                _lastDamageTime[other.gameObject] = Time.time;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (_objectsOnFloor.Contains(other.gameObject))
        {
            _objectsOnFloor.Remove(other.gameObject);
            _lastDamageTime.Remove(other.gameObject);
        }
    }

    private bool IsValidTarget(GameObject target)
    {
        return (_affectedLayers.value & (1 << target.layer)) != 0;
    }

    public void SetActive(bool active)
    {
        _isActive = active;
        
        if (_sparkParticles)
        {
            if (active)
                _sparkParticles.Play();
            else
                _sparkParticles.Stop();
        }
        
        if (_electricLight)
        {
            _electricLight.enabled = active;
        }
        
        if (_audioSource)
        {
            if (active && !_audioSource.isPlaying)
                _audioSource.Play();
            else if (!active && _audioSource.isPlaying)
                _audioSource.Stop();
        }
    }

    public void SetDamageAmount(float damage)
    {
        _damageAmount = damage;
    }

    public void SetPulseSpeed(float speed)
    {
        _pulseSpeed = speed;
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        _objectsOnFloor.Clear();
        _lastDamageTime.Clear();
    }
}