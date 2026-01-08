// Prompt: door that opens on approach
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class ProximityDoor : MonoBehaviour
{
    [Header("Door Settings")]
    [SerializeField] private float _detectionRange = 3f;
    [SerializeField] private float _openSpeed = 2f;
    [SerializeField] private float _closeSpeed = 1f;
    [SerializeField] private Vector3 _openOffset = new Vector3(0, 3f, 0);
    [SerializeField] private LayerMask _playerLayer = -1;
    
    [Header("Door Components")]
    [SerializeField] private Transform _doorTransform;
    [SerializeField] private Collider _doorCollider;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;
    
    [Header("Events")]
    public UnityEvent OnDoorOpened;
    public UnityEvent OnDoorClosed;
    
    private Vector3 _closedPosition;
    private Vector3 _openPosition;
    private bool _isOpen = false;
    private bool _isMoving = false;
    private bool _playerInRange = false;
    
    private void Start()
    {
        InitializeDoor();
    }
    
    private void InitializeDoor()
    {
        if (_doorTransform == null)
            _doorTransform = transform;
            
        _closedPosition = _doorTransform.localPosition;
        _openPosition = _closedPosition + _openOffset;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        CheckForPlayer();
        HandleDoorMovement();
    }
    
    private void CheckForPlayer()
    {
        bool playerWasInRange = _playerInRange;
        _playerInRange = false;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRange, _playerLayer);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                _playerInRange = true;
                break;
            }
        }
        
        if (_playerInRange && !playerWasInRange)
        {
            OpenDoor();
        }
        else if (!_playerInRange && playerWasInRange)
        {
            CloseDoor();
        }
    }
    
    private void HandleDoorMovement()
    {
        if (!_isMoving) return;
        
        Vector3 targetPosition = _isOpen ? _openPosition : _closedPosition;
        float speed = _isOpen ? _openSpeed : _closeSpeed;
        
        _doorTransform.localPosition = Vector3.MoveTowards(
            _doorTransform.localPosition, 
            targetPosition, 
            speed * Time.deltaTime
        );
        
        if (Vector3.Distance(_doorTransform.localPosition, targetPosition) < 0.01f)
        {
            _doorTransform.localPosition = targetPosition;
            _isMoving = false;
            
            if (_doorCollider != null)
                _doorCollider.enabled = !_isOpen;
        }
    }
    
    private void OpenDoor()
    {
        if (_isOpen || _isMoving) return;
        
        _isOpen = true;
        _isMoving = true;
        
        PlaySound(_openSound);
        OnDoorOpened?.Invoke();
    }
    
    private void CloseDoor()
    {
        if (!_isOpen || _isMoving) return;
        
        _isOpen = false;
        _isMoving = true;
        
        PlaySound(_closeSound);
        OnDoorClosed?.Invoke();
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
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        if (_doorTransform != null)
        {
            Gizmos.color = Color.green;
            Vector3 openPos = transform.TransformPoint(_closedPosition + _openOffset);
            Gizmos.DrawWireCube(openPos, _doorTransform.localScale);
        }
    }
}