// Prompt: telekinesis grab
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class TelekinesisGrab : MonoBehaviour
{
    [Header("Telekinesis Settings")]
    [SerializeField] private float _maxGrabDistance = 10f;
    [SerializeField] private float _grabForce = 500f;
    [SerializeField] private float _holdDistance = 3f;
    [SerializeField] private float _rotationSpeed = 100f;
    [SerializeField] private LayerMask _grabbableLayerMask = -1;
    
    [Header("Input")]
    [SerializeField] private KeyCode _grabKey = KeyCode.E;
    [SerializeField] private KeyCode _releaseKey = KeyCode.Q;
    [SerializeField] private KeyCode _rotateLeftKey = KeyCode.Z;
    [SerializeField] private KeyCode _rotateRightKey = KeyCode.X;
    
    [Header("Visual Effects")]
    [SerializeField] private LineRenderer _telekinesisBeam;
    [SerializeField] private ParticleSystem _grabEffect;
    [SerializeField] private Color _beamColor = Color.cyan;
    [SerializeField] private float _beamWidth = 0.1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _grabSound;
    [SerializeField] private AudioClip _releaseSound;
    [SerializeField] private AudioClip _holdLoopSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectGrabbed;
    public UnityEvent<GameObject> OnObjectReleased;
    
    private Camera _playerCamera;
    private GameObject _grabbedObject;
    private Rigidbody _grabbedRigidbody;
    private Vector3 _originalGravityScale;
    private bool _wasKinematic;
    private Transform _holdPoint;
    private List<Collider> _grabbedColliders = new List<Collider>();
    
    [System.Serializable]
    public class GrabbableObject
    {
        public Rigidbody rigidbody;
        public Vector3 originalPosition;
        public Quaternion originalRotation;
        public bool wasKinematic;
        public float originalMass;
    }
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        CreateHoldPoint();
        SetupLineRenderer();
    }
    
    private void CreateHoldPoint()
    {
        GameObject holdPointObj = new GameObject("TelekinesisHoldPoint");
        _holdPoint = holdPointObj.transform;
        _holdPoint.SetParent(transform);
        _holdPoint.localPosition = Vector3.forward * _holdDistance;
    }
    
    private void SetupLineRenderer()
    {
        if (_telekinesisBeam == null)
        {
            _telekinesisBeam = gameObject.AddComponent<LineRenderer>();
        }
        
        _telekinesisBeam.material = new Material(Shader.Find("Sprites/Default"));
        _telekinesisBeam.color = _beamColor;
        _telekinesisBeam.startWidth = _beamWidth;
        _telekinesisBeam.endWidth = _beamWidth;
        _telekinesisBeam.positionCount = 2;
        _telekinesisBeam.enabled = false;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateGrabbedObject();
        UpdateVisualEffects();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_grabKey))
        {
            if (_grabbedObject == null)
                TryGrabObject();
            else
                ReleaseObject();
        }
        
        if (Input.GetKeyDown(_releaseKey) && _grabbedObject != null)
        {
            ReleaseObject();
        }
        
        if (_grabbedObject != null)
        {
            HandleRotationInput();
            HandleDistanceAdjustment();
        }
    }
    
    private void HandleRotationInput()
    {
        if (Input.GetKey(_rotateLeftKey))
        {
            _grabbedObject.transform.Rotate(Vector3.up, -_rotationSpeed * Time.deltaTime, Space.World);
        }
        
        if (Input.GetKey(_rotateRightKey))
        {
            _grabbedObject.transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime, Space.World);
        }
    }
    
    private void HandleDistanceAdjustment()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            _holdDistance = Mathf.Clamp(_holdDistance + scroll * 2f, 1f, _maxGrabDistance);
            _holdPoint.localPosition = Vector3.forward * _holdDistance;
        }
    }
    
    private void TryGrabObject()
    {
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, _maxGrabDistance, _grabbableLayerMask))
        {
            GameObject target = hit.collider.gameObject;
            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            
            if (targetRb != null && CanGrabObject(target))
            {
                GrabObject(target, targetRb);
            }
        }
    }
    
    private bool CanGrabObject(GameObject obj)
    {
        if (obj.CompareTag("Player"))
            return false;
            
        TelekinesisGrab otherGrabber = obj.GetComponent<TelekinesisGrab>();
        if (otherGrabber != null)
            return false;
            
        return true;
    }
    
    private void GrabObject(GameObject obj, Rigidbody rb)
    {
        _grabbedObject = obj;
        _grabbedRigidbody = rb;
        
        _wasKinematic = rb.isKinematic;
        rb.isKinematic = false;
        rb.useGravity = false;
        
        _grabbedColliders.Clear();
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        foreach (Collider col in colliders)
        {
            _grabbedColliders.Add(col);
        }
        
        PlaySound(_grabSound);
        
        if (_grabEffect != null)
        {
            _grabEffect.transform.position = obj.transform.position;
            _grabEffect.Play();
        }
        
        OnObjectGrabbed?.Invoke(obj);
    }
    
    private void UpdateGrabbedObject()
    {
        if (_grabbedObject == null || _grabbedRigidbody == null)
            return;
            
        Vector3 targetPosition = _holdPoint.position;
        Vector3 direction = targetPosition - _grabbedObject.transform.position;
        
        float distance = direction.magnitude;
        if (distance > 0.1f)
        {
            Vector3 force = direction.normalized * _grabForce * distance;
            _grabbedRigidbody.AddForce(force);
        }
        
        _grabbedRigidbody.velocity *= 0.95f;
        _grabbedRigidbody.angularVelocity *= 0.9f;
        
        if (Vector3.Distance(_grabbedObject.transform.position, transform.position) > _maxGrabDistance * 1.5f)
        {
            ReleaseObject();
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_grabbedObject != null && _telekinesisBeam != null)
        {
            _telekinesisBeam.enabled = true;
            _telekinesisBeam.SetPosition(0, transform.position);
            _telekinesisBeam.SetPosition(1, _grabbedObject.transform.position);
        }
        else if (_telekinesisBeam != null)
        {
            _telekinesisBeam.enabled = false;
        }
    }
    
    private void ReleaseObject()
    {
        if (_grabbedObject == null)
            return;
            
        if (_grabbedRigidbody != null)
        {
            _grabbedRigidbody.isKinematic = _wasKinematic;
            _grabbedRigidbody.useGravity = true;
        }
        
        PlaySound(_releaseSound);
        
        OnObjectReleased?.Invoke(_grabbedObject);
        
        _grabbedObject = null;
        _grabbedRigidbody = null;
        _grabbedColliders.Clear();
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _maxGrabDistance);
        
        if (_holdPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_holdPoint.position, 0.2f);
            Gizmos.DrawLine(transform.position, _holdPoint.position);
        }
    }
    
    private void OnDisable()
    {
        if (_grabbedObject != null)
        {
            ReleaseObject();
        }
    }
}