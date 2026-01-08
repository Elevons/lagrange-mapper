// Prompt: rotating blade obstacle
// Type: general

using UnityEngine;

public class RotatingBlade : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 360f;
    [SerializeField] private Vector3 _rotationAxis = Vector3.forward;
    [SerializeField] private bool _clockwise = true;
    
    [Header("Damage Settings")]
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private LayerMask _damageableLayers = -1;
    
    [Header("Effects")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rotationSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private ParticleSystem _sparkParticles;
    [SerializeField] private bool _playRotationSound = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private float _hitFlashDuration = 0.1f;
    [SerializeField] private Color _hitFlashColor = Color.red;
    
    private Renderer _renderer;
    private Color _originalColor;
    private bool _isFlashing = false;
    private float _flashTimer = 0f;
    
    private System.Collections.Generic.Dictionary<GameObject, float> _lastDamageTime = 
        new System.Collections.Generic.Dictionary<GameObject, float>();

    private void Start()
    {
        _renderer = GetComponent<Renderer>();
        if (_renderer != null && _renderer.material != null)
        {
            _originalColor = _renderer.material.color;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_audioSource != null && _rotationSound != null && _playRotationSound)
        {
            _audioSource.clip = _rotationSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    private void Update()
    {
        RotateBlade();
        HandleFlashEffect();
    }

    private void RotateBlade()
    {
        float rotationDirection = _clockwise ? 1f : -1f;
        Vector3 rotation = _rotationAxis * _rotationSpeed * rotationDirection * Time.deltaTime;
        transform.Rotate(rotation, Space.Self);
    }

    private void HandleFlashEffect()
    {
        if (_isFlashing)
        {
            _flashTimer -= Time.deltaTime;
            if (_flashTimer <= 0f)
            {
                _isFlashing = false;
                if (_renderer != null && _renderer.material != null)
                {
                    _renderer.material.color = _originalColor;
                }
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnTriggerStay(Collider other)
    {
        HandleCollision(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision.gameObject);
    }

    private void HandleCollision(GameObject target)
    {
        if (target == null) return;
        
        if (!IsInDamageableLayer(target)) return;
        
        if (!CanDamageTarget(target)) return;
        
        DealDamage(target);
        PlayHitEffects(target);
        UpdateLastDamageTime(target);
    }

    private bool IsInDamageableLayer(GameObject target)
    {
        return (_damageableLayers.value & (1 << target.layer)) != 0;
    }

    private bool CanDamageTarget(GameObject target)
    {
        if (!_lastDamageTime.ContainsKey(target))
        {
            return true;
        }
        
        return Time.time - _lastDamageTime[target] >= _damageInterval;
    }

    private void DealDamage(GameObject target)
    {
        if (target.CompareTag("Player"))
        {
            var playerScript = target.GetComponent<MonoBehaviour>();
            if (playerScript != null)
            {
                playerScript.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            }
        }
        else
        {
            var healthComponent = target.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                healthComponent.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            }
        }
    }

    private void PlayHitEffects(GameObject target)
    {
        if (_hitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_hitSound);
        }
        
        if (_sparkParticles != null)
        {
            _sparkParticles.transform.position = GetClosestPoint(target.transform.position);
            _sparkParticles.Play();
        }
        
        TriggerFlashEffect();
    }

    private void TriggerFlashEffect()
    {
        if (_renderer != null && _renderer.material != null)
        {
            _renderer.material.color = _hitFlashColor;
            _isFlashing = true;
            _flashTimer = _hitFlashDuration;
        }
    }

    private Vector3 GetClosestPoint(Vector3 targetPosition)
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.ClosestPoint(targetPosition);
        }
        return transform.position;
    }

    private void UpdateLastDamageTime(GameObject target)
    {
        _lastDamageTime[target] = Time.time;
    }

    private void OnDisable()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    private void OnDestroy()
    {
        _lastDamageTime.Clear();
    }

    public void SetRotationSpeed(float speed)
    {
        _rotationSpeed = speed;
    }

    public void SetClockwise(bool clockwise)
    {
        _clockwise = clockwise;
    }

    public void SetDamage(int damage)
    {
        _damage = Mathf.Max(0, damage);
    }

    public void ToggleRotation()
    {
        enabled = !enabled;
        
        if (_audioSource != null)
        {
            if (enabled && _playRotationSound)
            {
                _audioSource.Play();
            }
            else
            {
                _audioSource.Stop();
            }
        }
    }
}