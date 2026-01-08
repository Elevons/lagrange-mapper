// Prompt: gravity gun that pulls objects
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class GravityGun : MonoBehaviour
{
    [Header("Gravity Gun Settings")]
    [SerializeField] private float _pullForce = 10f;
    [SerializeField] private float _maxRange = 15f;
    [SerializeField] private float _holdDistance = 3f;
    [SerializeField] private LayerMask _pullableLayers = -1;
    [SerializeField] private float _pullSpeed = 5f;
    [SerializeField] private float _rotationDamping = 5f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _pullKey = KeyCode.Mouse0;
    [SerializeField] private KeyCode _releaseKey = KeyCode.Mouse1;
    
    [Header("Visual Effects")]
    [SerializeField] private LineRenderer _beamRenderer;
    [SerializeField] private Transform _holdPoint;
    [SerializeField] private ParticleSystem _pullEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pullSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectGrabbed;
    public UnityEvent<GameObject> OnObjectReleased;
    
    private Camera _playerCamera;
    private Rigidbody _heldObject;
    private Transform _heldTransform;
    private bool _isPulling;
    private Vector3 _originalGravity;
    private float _originalDrag;
    private float _originalAngularDrag;
    private bool _wasKinematic;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_holdPoint == null)
        {
            GameObject holdPointObj = new GameObject("HoldPoint");
            holdPointObj.transform.SetParent(transform);
            holdPointObj.transform.localPosition = Vector3.forward * _holdDistance;
            _holdPoint = holdPointObj.transform;
        }
        
        if (_beamRenderer != null)
        {
            _beamRenderer.enabled = false;
            _beamRenderer.positionCount = 2;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateHeldObject();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_pullKey))
        {
            if (_heldObject == null)
                TryGrabObject();
        }
        
        if (Input.GetKeyUp(_pullKey) || Input.GetKeyDown(_releaseKey))
        {
            if (_heldObject != null)
                ReleaseObject();
        }
    }
    
    private void TryGrabObject()
    {
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        
        if (Physics.Raycast(ray, out RaycastHit hit, _maxRange, _pullableLayers))
        {
            Rigidbody targetRb = hit.collider.GetComponent<Rigidbody>();
            
            if (targetRb != null && !targetRb.isKinematic)
            {
                GrabObject(targetRb);
            }
        }
    }
    
    private void GrabObject(Rigidbody target)
    {
        _heldObject = target;
        _heldTransform = target.transform;
        _isPulling = true;
        
        // Store original physics properties
        _originalDrag = _heldObject.drag;
        _originalAngularDrag = _heldObject.angularDrag;
        _wasKinematic = _heldObject.isKinematic;
        
        // Modify physics for better control
        _heldObject.drag = 10f;
        _heldObject.angularDrag = 10f;
        _heldObject.useGravity = false;
        
        // Play effects
        PlaySound(_pullSound);
        if (_pullEffect != null)
            _pullEffect.Play();
            
        OnObjectGrabbed?.Invoke(_heldObject.gameObject);
    }
    
    private void ReleaseObject()
    {
        if (_heldObject != null)
        {
            // Restore original physics properties
            _heldObject.drag = _originalDrag;
            _heldObject.angularDrag = _originalAngularDrag;
            _heldObject.useGravity = true;
            _heldObject.isKinematic = _wasKinematic;
            
            GameObject releasedObject = _heldObject.gameObject;
            
            _heldObject = null;
            _heldTransform = null;
            _isPulling = false;
            
            // Play effects
            PlaySound(_releaseSound);
            if (_pullEffect != null)
                _pullEffect.Stop();
                
            OnObjectReleased?.Invoke(releasedObject);
        }
    }
    
    private void UpdateHeldObject()
    {
        if (_heldObject == null || _holdPoint == null) return;
        
        Vector3 targetPosition = _holdPoint.position;
        Vector3 direction = targetPosition - _heldTransform.position;
        float distance = direction.magnitude;
        
        // Apply force to move object to hold position
        if (distance > 0.1f)
        {
            Vector3 force = direction.normalized * _pullForce * distance;
            _heldObject.AddForce(force, ForceMode.Force);
        }
        
        // Dampen rotation for stability
        if (_heldObject.angularVelocity.magnitude > 0.1f)
        {
            _heldObject.angularVelocity = Vector3.Lerp(_heldObject.angularVelocity, Vector3.zero, Time.deltaTime * _rotationDamping);
        }
        
        // Limit velocity to prevent objects from flying away
        if (_heldObject.velocity.magnitude > 20f)
        {
            _heldObject.velocity = _heldObject.velocity.normalized * 20f;
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_beamRenderer != null)
        {
            if (_isPulling && _heldObject != null)
            {
                _beamRenderer.enabled = true;
                _beamRenderer.SetPosition(0, transform.position);
                _beamRenderer.SetPosition(1, _heldTransform.position);
            }
            else
            {
                _beamRenderer.enabled = false;
            }
        }
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw max range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _maxRange);
        
        // Draw hold point
        if (_holdPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_holdPoint.position, 0.2f);
        }
        
        // Draw line to held object
        if (_heldObject != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _heldObject.transform.position);
        }
    }
    
    public void SetPullForce(float force)
    {
        _pullForce = Mathf.Max(0f, force);
    }
    
    public void SetMaxRange(float range)
    {
        _maxRange = Mathf.Max(0f, range);
    }
    
    public bool IsHoldingObject()
    {
        return _heldObject != null;
    }
    
    public GameObject GetHeldObject()
    {
        return _heldObject != null ? _heldObject.gameObject : null;
    }
}