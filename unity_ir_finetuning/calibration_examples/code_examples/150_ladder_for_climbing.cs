// Prompt: ladder for climbing
// Type: general

using UnityEngine;

public class Ladder : MonoBehaviour
{
    [Header("Ladder Settings")]
    [SerializeField] private float _climbSpeed = 3f;
    [SerializeField] private LayerMask _playerLayer = -1;
    [SerializeField] private bool _requireInputToClimb = true;
    [SerializeField] private string _climbInputAxis = "Vertical";
    
    [Header("Audio")]
    [SerializeField] private AudioClip _climbSound;
    [SerializeField] private float _climbSoundVolume = 0.5f;
    
    private Collider _ladderCollider;
    private AudioSource _audioSource;
    private GameObject _currentPlayer;
    private Rigidbody _playerRigidbody;
    private bool _isPlayerOnLadder = false;
    private float _originalGravityScale;
    private bool _wasKinematic;

    private void Start()
    {
        _ladderCollider = GetComponent<Collider>();
        if (_ladderCollider == null)
        {
            Debug.LogError("Ladder requires a Collider component!");
            return;
        }
        
        if (!_ladderCollider.isTrigger)
        {
            _ladderCollider.isTrigger = true;
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _climbSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.clip = _climbSound;
            _audioSource.volume = _climbSoundVolume;
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
        }
    }

    private void Update()
    {
        if (_isPlayerOnLadder && _currentPlayer != null)
        {
            HandleClimbing();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && IsInLayerMask(other.gameObject.layer, _playerLayer))
        {
            _currentPlayer = other.gameObject;
            _playerRigidbody = other.GetComponent<Rigidbody>();
            
            if (_playerRigidbody != null)
            {
                _isPlayerOnLadder = true;
                _wasKinematic = _playerRigidbody.isKinematic;
                _playerRigidbody.useGravity = false;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject == _currentPlayer)
        {
            ExitLadder();
        }
    }

    private void HandleClimbing()
    {
        if (_playerRigidbody == null) return;

        float climbInput = _requireInputToClimb ? Input.GetAxis(_climbInputAxis) : 0f;
        
        if (!_requireInputToClimb)
        {
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
                climbInput = 1f;
            else if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
                climbInput = -1f;
        }

        Vector3 climbVelocity = new Vector3(
            _playerRigidbody.velocity.x,
            climbInput * _climbSpeed,
            _playerRigidbody.velocity.z
        );

        _playerRigidbody.velocity = climbVelocity;

        HandleClimbingAudio(climbInput);
    }

    private void HandleClimbingAudio(float climbInput)
    {
        if (_audioSource == null || _climbSound == null) return;

        bool shouldPlaySound = Mathf.Abs(climbInput) > 0.1f;
        
        if (shouldPlaySound && !_audioSource.isPlaying)
        {
            _audioSource.Play();
        }
        else if (!shouldPlaySound && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }

    private void ExitLadder()
    {
        if (_playerRigidbody != null)
        {
            _playerRigidbody.useGravity = true;
            _playerRigidbody.isKinematic = _wasKinematic;
        }

        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }

        _isPlayerOnLadder = false;
        _currentPlayer = null;
        _playerRigidbody = null;
    }

    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private void OnDisable()
    {
        if (_isPlayerOnLadder)
        {
            ExitLadder();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + col.bounds.center, col.bounds.size);
        }
    }
}