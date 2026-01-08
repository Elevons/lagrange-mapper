// Prompt: zipline for fast travel
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Zipline : MonoBehaviour
{
    [System.Serializable]
    public class ZiplineEvents
    {
        public UnityEvent OnPlayerAttach;
        public UnityEvent OnPlayerDetach;
        public UnityEvent OnZiplineComplete;
    }

    [Header("Zipline Configuration")]
    [SerializeField] private Transform _startPoint;
    [SerializeField] private Transform _endPoint;
    [SerializeField] private float _travelSpeed = 10f;
    [SerializeField] private float _attachDistance = 2f;
    [SerializeField] private bool _autoDetach = true;
    [SerializeField] private float _detachDistance = 1f;

    [Header("Player Control")]
    [SerializeField] private KeyCode _attachKey = KeyCode.E;
    [SerializeField] private KeyCode _detachKey = KeyCode.Space;
    [SerializeField] private bool _requireKeyToAttach = true;
    [SerializeField] private bool _allowEarlyDetach = true;

    [Header("Physics")]
    [SerializeField] private bool _disablePlayerGravity = true;
    [SerializeField] private bool _disablePlayerMovement = true;
    [SerializeField] private Vector3 _playerOffset = Vector3.zero;

    [Header("Visual")]
    [SerializeField] private LineRenderer _ziplineRenderer;
    [SerializeField] private GameObject _handlePrefab;
    [SerializeField] private bool _createVisualHandle = true;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _attachSound;
    [SerializeField] private AudioClip _travelSound;
    [SerializeField] private AudioClip _detachSound;

    [Header("Events")]
    [SerializeField] private ZiplineEvents _events;

    private Transform _attachedPlayer;
    private Rigidbody _playerRigidbody;
    private CharacterController _playerController;
    private bool _isPlayerAttached;
    private float _travelProgress;
    private Vector3 _originalGravity;
    private bool _wasKinematic;
    private GameObject _visualHandle;
    private Transform _nearbyPlayer;

    private void Start()
    {
        InitializeZipline();
    }

    private void InitializeZipline()
    {
        if (_startPoint == null)
        {
            GameObject startObj = new GameObject("ZiplineStart");
            startObj.transform.SetParent(transform);
            startObj.transform.localPosition = Vector3.zero;
            _startPoint = startObj.transform;
        }

        if (_endPoint == null)
        {
            GameObject endObj = new GameObject("ZiplineEnd");
            endObj.transform.SetParent(transform);
            endObj.transform.localPosition = new Vector3(10f, -5f, 0f);
            _endPoint = endObj.transform;
        }

        SetupVisuals();
        SetupAudio();
    }

    private void SetupVisuals()
    {
        if (_ziplineRenderer == null)
        {
            _ziplineRenderer = GetComponent<LineRenderer>();
            if (_ziplineRenderer == null)
            {
                _ziplineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }

        if (_ziplineRenderer != null)
        {
            _ziplineRenderer.positionCount = 2;
            _ziplineRenderer.startWidth = 0.05f;
            _ziplineRenderer.endWidth = 0.05f;
            _ziplineRenderer.material = Resources.Load<Material>("Default-Material");
            UpdateZiplineVisual();
        }

        if (_createVisualHandle && _handlePrefab != null && _visualHandle == null)
        {
            _visualHandle = Instantiate(_handlePrefab, _startPoint.position, Quaternion.identity);
            _visualHandle.transform.SetParent(transform);
        }
    }

    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        if (_audioSource != null)
        {
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 1f;
        }
    }

    private void Update()
    {
        UpdateZiplineVisual();
        CheckForNearbyPlayer();
        HandlePlayerInput();
        
        if (_isPlayerAttached)
        {
            UpdatePlayerMovement();
        }
    }

    private void UpdateZiplineVisual()
    {
        if (_ziplineRenderer != null && _startPoint != null && _endPoint != null)
        {
            _ziplineRenderer.SetPosition(0, _startPoint.position);
            _ziplineRenderer.SetPosition(1, _endPoint.position);
        }
    }

    private void CheckForNearbyPlayer()
    {
        if (_isPlayerAttached) return;

        Collider[] nearbyColliders = Physics.OverlapSphere(_startPoint.position, _attachDistance);
        _nearbyPlayer = null;

        foreach (Collider col in nearbyColliders)
        {
            if (col.CompareTag("Player"))
            {
                _nearbyPlayer = col.transform;
                break;
            }
        }
    }

    private void HandlePlayerInput()
    {
        if (_nearbyPlayer != null && !_isPlayerAttached)
        {
            if (!_requireKeyToAttach || Input.GetKeyDown(_attachKey))
            {
                AttachPlayer(_nearbyPlayer);
            }
        }
        else if (_isPlayerAttached && _allowEarlyDetach && Input.GetKeyDown(_detachKey))
        {
            DetachPlayer();
        }
    }

    private void AttachPlayer(Transform player)
    {
        _attachedPlayer = player;
        _playerRigidbody = player.GetComponent<Rigidbody>();
        _playerController = player.GetComponent<CharacterController>();
        _isPlayerAttached = true;
        _travelProgress = 0f;

        if (_playerRigidbody != null)
        {
            _wasKinematic = _playerRigidbody.isKinematic;
            if (_disablePlayerGravity)
            {
                _playerRigidbody.useGravity = false;
            }
            if (_disablePlayerMovement)
            {
                _playerRigidbody.isKinematic = true;
            }
        }

        if (_playerController != null && _disablePlayerMovement)
        {
            _playerController.enabled = false;
        }

        PlaySound(_attachSound);
        _events?.OnPlayerAttach?.Invoke();

        if (_travelSound != null && _audioSource != null)
        {
            _audioSource.clip = _travelSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }

    private void UpdatePlayerMovement()
    {
        if (_attachedPlayer == null) return;

        _travelProgress += _travelSpeed * Time.deltaTime / Vector3.Distance(_startPoint.position, _endPoint.position);
        _travelProgress = Mathf.Clamp01(_travelProgress);

        Vector3 targetPosition = Vector3.Lerp(_startPoint.position, _endPoint.position, _travelProgress) + _playerOffset;
        
        if (_playerController != null && !_playerController.enabled)
        {
            _attachedPlayer.position = targetPosition;
        }
        else if (_playerRigidbody != null)
        {
            _playerRigidbody.MovePosition(targetPosition);
        }
        else
        {
            _attachedPlayer.position = targetPosition;
        }

        Vector3 direction = (_endPoint.position - _startPoint.position).normalized;
        if (direction != Vector3.zero)
        {
            _attachedPlayer.rotation = Quaternion.LookRotation(direction);
        }

        if (_visualHandle != null)
        {
            _visualHandle.transform.position = targetPosition;
        }

        if (_autoDetach && Vector3.Distance(_attachedPlayer.position, _endPoint.position) <= _detachDistance)
        {
            DetachPlayer();
            _events?.OnZiplineComplete?.Invoke();
        }
    }

    private void DetachPlayer()
    {
        if (!_isPlayerAttached || _attachedPlayer == null) return;

        if (_playerRigidbody != null)
        {
            _playerRigidbody.isKinematic = _wasKinematic;
            if (_disablePlayerGravity)
            {
                _playerRigidbody.useGravity = true;
            }
        }

        if (_playerController != null && !_playerController.enabled)
        {
            _playerController.enabled = true;
        }

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        PlaySound(_detachSound);
        _events?.OnPlayerDetach?.Invoke();

        _attachedPlayer = null;
        _playerRigidbody = null;
        _playerController = null;
        _isPlayerAttached = false;
        _travelProgress = 0f;

        if (_visualHandle != null)
        {
            _visualHandle.transform.position = _startPoint.position;
        }
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_requireKeyToAttach && other.CompareTag("Player") && !_isPlayerAttached)
        {
            AttachPlayer(other.transform);
        }
    }

    private void OnDrawGizmos()
    {
        if (_startPoint != null && _endPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(_startPoint.position, _endPoint.position);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_startPoint.position, _attachDistance);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_endPoint.position, _detachDistance);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_startPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(_startPoint.position, Vector3.one * 0.5f);
        }
        
        if (_endPoint != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(_endPoint.position, Vector3.one * 0.5f);
        }
    }
}