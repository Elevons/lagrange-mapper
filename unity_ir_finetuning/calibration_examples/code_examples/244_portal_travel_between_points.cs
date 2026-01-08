// Prompt: portal travel between points
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Portal : MonoBehaviour
{
    [Header("Portal Configuration")]
    [SerializeField] private Portal _destinationPortal;
    [SerializeField] private Transform _spawnPoint;
    [SerializeField] private float _teleportCooldown = 1f;
    [SerializeField] private bool _requiresActivation = false;
    [SerializeField] private KeyCode _activationKey = KeyCode.E;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _portalEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _teleportSound;
    [SerializeField] private GameObject _activationPrompt;
    
    [Header("Portal Behavior")]
    [SerializeField] private LayerMask _teleportLayers = -1;
    [SerializeField] private float _teleportForce = 5f;
    [SerializeField] private bool _maintainVelocity = true;
    [SerializeField] private bool _faceDestinationDirection = true;
    
    [Header("Events")]
    public UnityEvent OnTeleportStart;
    public UnityEvent OnTeleportComplete;
    public UnityEvent<GameObject> OnObjectTeleported;
    
    private bool _isActive = true;
    private float _lastTeleportTime;
    private GameObject _playerInRange;
    private Collider _portalCollider;
    
    private void Start()
    {
        _portalCollider = GetComponent<Collider>();
        if (_portalCollider == null)
        {
            _portalCollider = gameObject.AddComponent<BoxCollider>();
            ((BoxCollider)_portalCollider).isTrigger = true;
        }
        
        if (_spawnPoint == null)
        {
            GameObject spawnPoint = new GameObject("SpawnPoint");
            spawnPoint.transform.SetParent(transform);
            spawnPoint.transform.localPosition = Vector3.forward * 2f;
            _spawnPoint = spawnPoint.transform;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_activationPrompt != null)
            _activationPrompt.SetActive(false);
    }
    
    private void Update()
    {
        if (_requiresActivation && _playerInRange != null)
        {
            if (Input.GetKeyDown(_activationKey))
            {
                TeleportObject(_playerInRange);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive || Time.time - _lastTeleportTime < _teleportCooldown)
            return;
            
        if (!IsValidTeleportTarget(other.gameObject))
            return;
            
        if (other.CompareTag("Player"))
        {
            _playerInRange = other.gameObject;
            if (_requiresActivation && _activationPrompt != null)
                _activationPrompt.SetActive(true);
        }
        
        if (!_requiresActivation)
        {
            TeleportObject(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = null;
            if (_activationPrompt != null)
                _activationPrompt.SetActive(false);
        }
    }
    
    private bool IsValidTeleportTarget(GameObject target)
    {
        if (_destinationPortal == null)
            return false;
            
        int targetLayer = 1 << target.layer;
        return (_teleportLayers.value & targetLayer) != 0;
    }
    
    private void TeleportObject(GameObject target)
    {
        if (!IsValidTeleportTarget(target) || !_isActive)
            return;
            
        StartCoroutine(PerformTeleport(target));
    }
    
    private IEnumerator PerformTeleport(GameObject target)
    {
        _isActive = false;
        _lastTeleportTime = Time.time;
        
        OnTeleportStart?.Invoke();
        
        if (_portalEffect != null)
            _portalEffect.Play();
            
        if (_audioSource != null && _teleportSound != null)
            _audioSource.PlayOneShot(_teleportSound);
        
        Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();
        Vector3 originalVelocity = Vector3.zero;
        
        if (targetRigidbody != null && _maintainVelocity)
            originalVelocity = targetRigidbody.velocity;
        
        CharacterController characterController = target.GetComponent<CharacterController>();
        if (characterController != null)
            characterController.enabled = false;
        
        Vector3 destinationPosition = _destinationPortal._spawnPoint.position;
        target.transform.position = destinationPosition;
        
        if (_faceDestinationDirection)
        {
            target.transform.rotation = _destinationPortal._spawnPoint.rotation;
        }
        
        yield return new WaitForFixedUpdate();
        
        if (characterController != null)
            characterController.enabled = true;
        
        if (targetRigidbody != null)
        {
            if (_maintainVelocity)
            {
                Vector3 adjustedVelocity = _destinationPortal.transform.TransformDirection(
                    transform.InverseTransformDirection(originalVelocity));
                targetRigidbody.velocity = adjustedVelocity;
            }
            else
            {
                Vector3 forwardForce = _destinationPortal._spawnPoint.forward * _teleportForce;
                targetRigidbody.velocity = forwardForce;
            }
        }
        
        if (_destinationPortal._portalEffect != null)
            _destinationPortal._portalEffect.Play();
        
        OnObjectTeleported?.Invoke(target);
        OnTeleportComplete?.Invoke();
        
        _destinationPortal._lastTeleportTime = Time.time;
        
        yield return new WaitForSeconds(_teleportCooldown);
        
        _isActive = true;
    }
    
    public void SetDestination(Portal destination)
    {
        _destinationPortal = destination;
    }
    
    public void ActivatePortal()
    {
        _isActive = true;
    }
    
    public void DeactivatePortal()
    {
        _isActive = false;
    }
    
    public void TogglePortal()
    {
        _isActive = !_isActive;
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_destinationPortal != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _destinationPortal.transform.position);
            Gizmos.DrawWireSphere(_destinationPortal.transform.position, 0.5f);
        }
        
        if (_spawnPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_spawnPoint.position, 0.3f);
            Gizmos.DrawRay(_spawnPoint.position, _spawnPoint.forward * 2f);
        }
    }
}