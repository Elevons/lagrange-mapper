// Prompt: rope swing
// Type: general

using UnityEngine;

public class RopeSwing : MonoBehaviour
{
    [Header("Rope Settings")]
    [SerializeField] private Transform _anchorPoint;
    [SerializeField] private LineRenderer _ropeRenderer;
    [SerializeField] private int _ropeSegments = 20;
    [SerializeField] private float _ropeLength = 5f;
    [SerializeField] private float _ropeWidth = 0.1f;
    
    [Header("Physics")]
    [SerializeField] private float _swingForce = 10f;
    [SerializeField] private float _dampening = 0.98f;
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Player Interaction")]
    [SerializeField] private KeyCode _grabKey = KeyCode.E;
    [SerializeField] private KeyCode _releaseKey = KeyCode.Space;
    [SerializeField] private float _grabDistance = 2f;
    [SerializeField] private Transform _grabPoint;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _grabSound;
    [SerializeField] private AudioClip _releaseSound;
    [SerializeField] private AudioClip _swingSound;
    
    private bool _isPlayerGrabbing = false;
    private GameObject _currentPlayer;
    private Rigidbody _playerRigidbody;
    private Vector3 _ropeDirection;
    private float _currentAngle = 0f;
    private float _angularVelocity = 0f;
    private Vector3[] _ropePositions;
    private bool _isSwinging = false;
    private Vector3 _lastPlayerPosition;
    private bool _canGrab = true;
    
    private void Start()
    {
        InitializeRope();
        SetupAudio();
    }
    
    private void InitializeRope()
    {
        if (_anchorPoint == null)
            _anchorPoint = transform;
            
        if (_grabPoint == null)
        {
            GameObject grabPointObj = new GameObject("GrabPoint");
            grabPointObj.transform.SetParent(transform);
            _grabPoint = grabPointObj.transform;
        }
        
        _ropePositions = new Vector3[_ropeSegments + 1];
        
        if (_ropeRenderer == null)
        {
            _ropeRenderer = GetComponent<LineRenderer>();
            if (_ropeRenderer == null)
                _ropeRenderer = gameObject.AddComponent<LineRenderer>();
        }
        
        _ropeRenderer.positionCount = _ropeSegments + 1;
        _ropeRenderer.startWidth = _ropeWidth;
        _ropeRenderer.endWidth = _ropeWidth;
        _ropeRenderer.useWorldSpace = true;
        
        UpdateRopeVisual();
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
                _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }
    
    private void Update()
    {
        HandlePlayerInput();
        UpdateRopePhysics();
        UpdateRopeVisual();
        CheckGroundCollision();
    }
    
    private void HandlePlayerInput()
    {
        if (!_isPlayerGrabbing && _canGrab)
        {
            CheckForPlayerGrab();
        }
        
        if (_isPlayerGrabbing && _currentPlayer != null)
        {
            HandleSwingInput();
            
            if (Input.GetKeyDown(_releaseKey))
            {
                ReleasePlayer();
            }
        }
    }
    
    private void CheckForPlayerGrab()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(_grabPoint.position, _grabDistance);
        
        foreach (Collider col in nearbyObjects)
        {
            if (col.CompareTag("Player") && Input.GetKeyDown(_grabKey))
            {
                GrabPlayer(col.gameObject);
                break;
            }
        }
    }
    
    private void GrabPlayer(GameObject player)
    {
        _currentPlayer = player;
        _playerRigidbody = player.GetComponent<Rigidbody>();
        
        if (_playerRigidbody == null)
            return;
            
        _isPlayerGrabbing = true;
        _isSwinging = true;
        
        // Calculate initial angle based on player position relative to anchor
        Vector3 toPlayer = player.transform.position - _anchorPoint.position;
        _currentAngle = Mathf.Atan2(toPlayer.x, -toPlayer.y);
        _angularVelocity = 0f;
        
        // Disable player's gravity temporarily
        _playerRigidbody.useGravity = false;
        _playerRigidbody.velocity = Vector3.zero;
        
        _lastPlayerPosition = player.transform.position;
        
        PlaySound(_grabSound);
    }
    
    private void HandleSwingInput()
    {
        float horizontal = Input.GetAxis("Horizontal");
        
        if (Mathf.Abs(horizontal) > 0.1f)
        {
            _angularVelocity += horizontal * _swingForce * Time.deltaTime;
            
            if (!_audioSource.isPlaying && _swingSound != null)
            {
                PlaySound(_swingSound);
            }
        }
    }
    
    private void UpdateRopePhysics()
    {
        if (_isPlayerGrabbing && _currentPlayer != null && _playerRigidbody != null)
        {
            // Apply gravity to angular velocity
            float gravityForce = _gravity * Mathf.Sin(_currentAngle) / _ropeLength;
            _angularVelocity += gravityForce * Time.deltaTime;
            
            // Apply dampening
            _angularVelocity *= _dampening;
            
            // Update angle
            _currentAngle += _angularVelocity * Time.deltaTime;
            
            // Calculate new player position
            Vector3 newPosition = _anchorPoint.position + new Vector3(
                Mathf.Sin(_currentAngle) * _ropeLength,
                -Mathf.Cos(_currentAngle) * _ropeLength,
                0f
            );
            
            // Move player
            _currentPlayer.transform.position = newPosition;
            
            // Calculate velocity for when player releases
            Vector3 velocity = (newPosition - _lastPlayerPosition) / Time.deltaTime;
            _playerRigidbody.velocity = velocity;
            
            _lastPlayerPosition = newPosition;
        }
        
        // Update grab point position
        if (_grabPoint != null)
        {
            _grabPoint.position = _anchorPoint.position + new Vector3(
                Mathf.Sin(_currentAngle) * _ropeLength,
                -Mathf.Cos(_currentAngle) * _ropeLength,
                0f
            );
        }
    }
    
    private void UpdateRopeVisual()
    {
        if (_ropeRenderer == null || _anchorPoint == null)
            return;
            
        Vector3 ropeEnd = _grabPoint != null ? _grabPoint.position : 
            _anchorPoint.position + Vector3.down * _ropeLength;
        
        for (int i = 0; i <= _ropeSegments; i++)
        {
            float t = (float)i / _ropeSegments;
            Vector3 position = Vector3.Lerp(_anchorPoint.position, ropeEnd, t);
            
            // Add slight curve to rope
            float curve = Mathf.Sin(t * Mathf.PI) * 0.5f;
            position.x += curve * Mathf.Sign(ropeEnd.x - _anchorPoint.position.x);
            
            _ropePositions[i] = position;
        }
        
        _ropeRenderer.SetPositions(_ropePositions);
    }
    
    private void CheckGroundCollision()
    {
        if (_isPlayerGrabbing && _currentPlayer != null)
        {
            Bounds playerBounds = _currentPlayer.GetComponent<Collider>().bounds;
            
            if (Physics.CheckBox(playerBounds.center, playerBounds.extents, 
                _currentPlayer.transform.rotation, _groundLayer))
            {
                ReleasePlayer();
            }
        }
    }
    
    private void ReleasePlayer()
    {
        if (_currentPlayer != null && _playerRigidbody != null)
        {
            _playerRigidbody.useGravity = true;
            
            // Add release velocity
            Vector3 releaseVelocity = new Vector3(
                Mathf.Cos(_currentAngle) * _angularVelocity * _ropeLength,
                Mathf.Sin(_currentAngle) * _angularVelocity * _ropeLength,
                0f
            );
            
            _playerRigidbody.velocity += releaseVelocity;
        }
        
        _isPlayerGrabbing = false;
        _isSwinging = false;
        _currentPlayer = null;
        _playerRigidbody = null;
        _angularVelocity = 0f;
        
        PlaySound(_releaseSound);
        
        // Prevent immediate re-grab
        _canGrab = false;
        Invoke(nameof(EnableGrab), 0.5f);
    }
    
    private void EnableGrab()
    {
        _canGrab = true;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_anchorPoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_anchorPoint.position, 0.2f);
            
            Vector3 ropeEnd = _anchorPoint.position + Vector3.down * _ropeLength;
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_anchorPoint.position, ropeEnd);
            
            if (_grabPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(_grabPoint.position, _grabDistance);
            }
        }
    }
}