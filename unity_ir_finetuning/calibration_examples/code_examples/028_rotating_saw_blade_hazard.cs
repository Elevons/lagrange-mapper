// Prompt: rotating saw blade hazard
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class RotatingSawBlade : MonoBehaviour
{
    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 360f;
    [SerializeField] private Vector3 _rotationAxis = Vector3.forward;
    [SerializeField] private bool _clockwise = true;
    
    [Header("Movement Settings")]
    [SerializeField] private bool _enableMovement = false;
    [SerializeField] private Transform[] _waypoints;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private bool _loopMovement = true;
    [SerializeField] private bool _pingPongMovement = false;
    
    [Header("Damage Settings")]
    [SerializeField] private int _damage = 1;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private LayerMask _damageableLayers = -1;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rotationSound;
    [SerializeField] private AudioClip _hitSound;
    [SerializeField] private float _rotationVolume = 0.5f;
    [SerializeField] private float _hitVolume = 1f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _sparkParticles;
    [SerializeField] private GameObject _hitEffect;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnPlayerHit;
    public UnityEvent<Collision> OnHitObject;
    
    private int _currentWaypointIndex = 0;
    private bool _movingForward = true;
    private System.Collections.Generic.Dictionary<GameObject, float> _lastDamageTime = new System.Collections.Generic.Dictionary<GameObject, float>();
    
    private void Start()
    {
        InitializeAudio();
        InitializeParticles();
        ValidateWaypoints();
    }
    
    private void Update()
    {
        RotateBlade();
        
        if (_enableMovement && _waypoints != null && _waypoints.Length > 1)
        {
            MoveBlade();
        }
    }
    
    private void InitializeAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (_rotationSound != null && _audioSource != null)
        {
            _audioSource.clip = _rotationSound;
            _audioSource.loop = true;
            _audioSource.volume = _rotationVolume;
            _audioSource.Play();
        }
    }
    
    private void InitializeParticles()
    {
        if (_sparkParticles == null)
        {
            _sparkParticles = GetComponentInChildren<ParticleSystem>();
        }
    }
    
    private void ValidateWaypoints()
    {
        if (_enableMovement && (_waypoints == null || _waypoints.Length < 2))
        {
            Debug.LogWarning($"RotatingSawBlade '{gameObject.name}' has movement enabled but insufficient waypoints!");
            _enableMovement = false;
        }
    }
    
    private void RotateBlade()
    {
        float rotationDirection = _clockwise ? -1f : 1f;
        Vector3 rotation = _rotationAxis * _rotationSpeed * rotationDirection * Time.deltaTime;
        transform.Rotate(rotation, Space.Self);
    }
    
    private void MoveBlade()
    {
        Transform targetWaypoint = _waypoints[_currentWaypointIndex];
        if (targetWaypoint == null) return;
        
        Vector3 direction = (targetWaypoint.position - transform.position).normalized;
        float distance = Vector3.Distance(transform.position, targetWaypoint.position);
        
        if (distance < 0.1f)
        {
            UpdateWaypointIndex();
        }
        else
        {
            transform.position += direction * _moveSpeed * Time.deltaTime;
        }
    }
    
    private void UpdateWaypointIndex()
    {
        if (_pingPongMovement)
        {
            if (_movingForward)
            {
                _currentWaypointIndex++;
                if (_currentWaypointIndex >= _waypoints.Length)
                {
                    _currentWaypointIndex = _waypoints.Length - 2;
                    _movingForward = false;
                }
            }
            else
            {
                _currentWaypointIndex--;
                if (_currentWaypointIndex < 0)
                {
                    _currentWaypointIndex = 1;
                    _movingForward = true;
                }
            }
        }
        else
        {
            _currentWaypointIndex++;
            if (_currentWaypointIndex >= _waypoints.Length)
            {
                if (_loopMovement)
                {
                    _currentWaypointIndex = 0;
                }
                else
                {
                    _currentWaypointIndex = _waypoints.Length - 1;
                    _enableMovement = false;
                }
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other.gameObject, other.transform.position);
    }
    
    private void OnTriggerStay(Collider other)
    {
        HandleCollision(other.gameObject, other.transform.position);
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision.gameObject, collision.contacts[0].point);
        OnHitObject?.Invoke(collision);
    }
    
    private void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision.gameObject, collision.contacts[0].point);
    }
    
    private void HandleCollision(GameObject hitObject, Vector3 hitPoint)
    {
        if (!IsInDamageableLayer(hitObject)) return;
        
        if (CanDamageObject(hitObject))
        {
            DealDamage(hitObject);
            PlayHitEffects(hitPoint);
            
            if (hitObject.CompareTag("Player"))
            {
                OnPlayerHit?.Invoke(hitObject);
            }
            
            _lastDamageTime[hitObject] = Time.time;
        }
    }
    
    private bool IsInDamageableLayer(GameObject obj)
    {
        return (_damageableLayers.value & (1 << obj.layer)) != 0;
    }
    
    private bool CanDamageObject(GameObject obj)
    {
        if (!_lastDamageTime.ContainsKey(obj))
        {
            return true;
        }
        
        return Time.time - _lastDamageTime[obj] >= _damageInterval;
    }
    
    private void DealDamage(GameObject target)
    {
        // Try different common health interfaces
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use SendMessage as a fallback for common health methods
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("Damage", _damage, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("Hit", _damage, SendMessageOptions.DontRequireReceiver);
        }
        
        // If it's a rigidbody, add some force
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (target.transform.position - transform.position).normalized;
            rb.AddForce(forceDirection * 10f, ForceMode.Impulse);
        }
    }
    
    private void PlayHitEffects(Vector3 hitPoint)
    {
        // Play hit sound
        if (_hitSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_hitSound, _hitVolume);
        }
        
        // Play spark particles
        if (_sparkParticles != null)
        {
            _sparkParticles.transform.position = hitPoint;
            _sparkParticles.Play();
        }
        
        // Instantiate hit effect
        if (_hitEffect != null)
        {
            GameObject effect = Instantiate(_hitEffect, hitPoint, Quaternion.identity);
            Destroy(effect, 2f);
        }
    }
    
    public void SetRotationSpeed(float speed)
    {
        _rotationSpeed = speed;
    }
    
    public void SetMoveSpeed(float speed)
    {
        _moveSpeed = speed;
    }
    
    public void ToggleMovement()
    {
        _enableMovement = !_enableMovement;
    }
    
    public void StopBlade()
    {
        _rotationSpeed = 0f;
        _enableMovement = false;
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    public void StartBlade()
    {
        _rotationSpeed = Mathf.Abs(_rotationSpeed) == 0 ? 360f : Mathf.Abs(_rotationSpeed);
        
        if (_rotationSound != null && _audioSource != null)
        {
            _audioSource.Play();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_enableMovement && _waypoints != null && _waypoints.Length > 1)
        {
            Gizmos.color = Color.yellow;
            
            for (int i = 0; i < _waypoints.Length; i++)
            {
                if (_waypoints[i] == null) continue;
                
                Gizmos.DrawWireSphere(_waypoints[i].position, 0.3f);
                
                if (i < _waypoints.Length - 1 && _waypoints[i + 1] != null)
                {
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[i + 1].position);
                }
                else if (_loopMovement && !_pingPongMovement && _waypoints[0] != null)
                {
                    Gizmos.DrawLine(_waypoints[i].position, _waypoints[0].position);
                }
            }
        }
        
        // Draw rotation axis
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position, _rotationAxis.normalized);
    }
}