// Prompt: pressure plate that opens nearby door
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class PressurePlate : MonoBehaviour
{
    [Header("Pressure Plate Settings")]
    [SerializeField] private float _activationDelay = 0.1f;
    [SerializeField] private float _deactivationDelay = 0.5f;
    [SerializeField] private bool _stayActivated = false;
    [SerializeField] private string[] _activatorTags = { "Player", "Box" };
    
    [Header("Visual Feedback")]
    [SerializeField] private Transform _plateTransform;
    [SerializeField] private float _pressedHeight = -0.1f;
    [SerializeField] private float _normalHeight = 0f;
    [SerializeField] private float _animationSpeed = 5f;
    [SerializeField] private AudioClip _activationSound;
    [SerializeField] private AudioClip _deactivationSound;
    
    [Header("Door Connection")]
    [SerializeField] private DoorController _connectedDoor;
    [SerializeField] private float _doorSearchRadius = 10f;
    
    [Header("Events")]
    public UnityEvent OnActivated;
    public UnityEvent OnDeactivated;
    
    private bool _isActivated = false;
    private bool _hasObjectsOnPlate = false;
    private int _objectsOnPlate = 0;
    private AudioSource _audioSource;
    private Vector3 _targetPosition;
    private float _activationTimer = 0f;
    private float _deactivationTimer = 0f;
    private bool _pendingActivation = false;
    private bool _pendingDeactivation = false;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
        
        if (_plateTransform == null)
            _plateTransform = transform;
            
        _targetPosition = _plateTransform.localPosition;
        _targetPosition.y = _normalHeight;
        _plateTransform.localPosition = _targetPosition;
        
        if (_connectedDoor == null)
            FindNearbyDoor();
    }
    
    private void Update()
    {
        HandleTimers();
        AnimatePlate();
    }
    
    private void HandleTimers()
    {
        if (_pendingActivation)
        {
            _activationTimer += Time.deltaTime;
            if (_activationTimer >= _activationDelay)
            {
                ActivatePlate();
                _pendingActivation = false;
                _activationTimer = 0f;
            }
        }
        
        if (_pendingDeactivation && !_stayActivated)
        {
            _deactivationTimer += Time.deltaTime;
            if (_deactivationTimer >= _deactivationDelay)
            {
                DeactivatePlate();
                _pendingDeactivation = false;
                _deactivationTimer = 0f;
            }
        }
    }
    
    private void AnimatePlate()
    {
        Vector3 currentPos = _plateTransform.localPosition;
        currentPos = Vector3.Lerp(currentPos, _targetPosition, Time.deltaTime * _animationSpeed);
        _plateTransform.localPosition = currentPos;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (IsValidActivator(other))
        {
            _objectsOnPlate++;
            
            if (!_hasObjectsOnPlate)
            {
                _hasObjectsOnPlate = true;
                _pendingActivation = true;
                _pendingDeactivation = false;
                _activationTimer = 0f;
                _deactivationTimer = 0f;
                
                _targetPosition.y = _pressedHeight;
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (IsValidActivator(other))
        {
            _objectsOnPlate--;
            
            if (_objectsOnPlate <= 0)
            {
                _objectsOnPlate = 0;
                _hasObjectsOnPlate = false;
                
                if (!_stayActivated)
                {
                    _pendingDeactivation = true;
                    _pendingActivation = false;
                    _activationTimer = 0f;
                    _deactivationTimer = 0f;
                }
                
                _targetPosition.y = _normalHeight;
            }
        }
    }
    
    private bool IsValidActivator(Collider other)
    {
        foreach (string tag in _activatorTags)
        {
            if (other.CompareTag(tag))
                return true;
        }
        return false;
    }
    
    private void ActivatePlate()
    {
        if (_isActivated) return;
        
        _isActivated = true;
        
        if (_connectedDoor != null)
            _connectedDoor.OpenDoor();
            
        PlaySound(_activationSound);
        OnActivated.Invoke();
    }
    
    private void DeactivatePlate()
    {
        if (!_isActivated) return;
        
        _isActivated = false;
        
        if (_connectedDoor != null)
            _connectedDoor.CloseDoor();
            
        PlaySound(_deactivationSound);
        OnDeactivated.Invoke();
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void FindNearbyDoor()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, _doorSearchRadius);
        
        foreach (Collider col in nearbyObjects)
        {
            DoorController door = col.GetComponent<DoorController>();
            if (door != null)
            {
                _connectedDoor = door;
                break;
            }
        }
    }
    
    public void ResetPlate()
    {
        _isActivated = false;
        _hasObjectsOnPlate = false;
        _objectsOnPlate = 0;
        _pendingActivation = false;
        _pendingDeactivation = false;
        _activationTimer = 0f;
        _deactivationTimer = 0f;
        _targetPosition.y = _normalHeight;
        
        if (_connectedDoor != null)
            _connectedDoor.CloseDoor();
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _doorSearchRadius);
        
        if (_connectedDoor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _connectedDoor.transform.position);
        }
    }
}

public class DoorController : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private Transform _doorTransform;
    [SerializeField] private Vector3 _openPosition;
    [SerializeField] private Vector3 _closedPosition;
    [SerializeField] private float _openSpeed = 2f;
    [SerializeField] private AnimationCurve _openCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("Audio")]
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;
    
    private bool _isOpen = false;
    private bool _isMoving = false;
    private Vector3 _targetPosition;
    private AudioSource _audioSource;
    private Coroutine _moveCoroutine;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_doorTransform == null)
            _doorTransform = transform;
            
        _closedPosition = _doorTransform.localPosition;
        _targetPosition = _closedPosition;
    }
    
    public void OpenDoor()
    {
        if (_isOpen || _isMoving) return;
        
        _isOpen = true;
        _targetPosition = _openPosition;
        
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);
            
        _moveCoroutine = StartCoroutine(MoveDoor(_openPosition, _openSound));
    }
    
    public void CloseDoor()
    {
        if (!_isOpen || _isMoving) return;
        
        _isOpen = false;
        _targetPosition = _closedPosition;
        
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);
            
        _moveCoroutine = StartCoroutine(MoveDoor(_closedPosition, _closeSound));
    }
    
    private System.Collections.IEnumerator MoveDoor(Vector3 targetPos, AudioClip sound)
    {
        _isMoving = true;
        Vector3 startPos = _doorTransform.localPosition;
        float journey = 0f;
        float distance = Vector3.Distance(startPos, targetPos);
        
        if (_audioSource != null && sound != null)
            _audioSource.PlayOneShot(sound);
        
        while (journey <= 1f)
        {
            journey += Time.deltaTime * _openSpeed;
            float curveValue = _openCurve.Evaluate(journey);
            _doorTransform.localPosition = Vector3.Lerp(startPos, targetPos, curveValue);
            yield return null;
        }
        
        _doorTransform.localPosition = targetPos;
        _isMoving = false;
    }
}