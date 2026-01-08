// Prompt: ghost that phases through walls
// Type: general

using UnityEngine;

public class GhostController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _phaseSpeed = 5f;
    [SerializeField] private float _rotationSpeed = 2f;
    
    [Header("AI Behavior")]
    [SerializeField] private float _detectionRange = 8f;
    [SerializeField] private float _wanderRadius = 10f;
    [SerializeField] private float _wanderTimer = 3f;
    [SerializeField] private LayerMask _wallLayers = -1;
    
    [Header("Phasing")]
    [SerializeField] private float _phaseTransparency = 0.3f;
    [SerializeField] private float _normalTransparency = 0.7f;
    [SerializeField] private float _fadeSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _ghostSound;
    [SerializeField] private float _soundInterval = 2f;
    
    private Vector3 _targetPosition;
    private Vector3 _wanderTarget;
    private float _wanderTimer_current;
    private float _soundTimer;
    private bool _isPhasing;
    private bool _chasingPlayer;
    
    private Transform _playerTransform;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Material _ghostMaterial;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _collider = GetComponent<Collider>();
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _rigidbody.useGravity = false;
        _rigidbody.freezeRotation = true;
        
        if (_collider != null)
        {
            _collider.isTrigger = true;
        }
        
        if (_renderer != null)
        {
            _ghostMaterial = _renderer.material;
            SetTransparency(_normalTransparency);
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = _ghostSound;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 1f;
        
        _wanderTimer_current = _wanderTimer;
        SetNewWanderTarget();
    }
    
    private void Update()
    {
        DetectPlayer();
        HandleMovement();
        HandlePhasing();
        HandleAudio();
        UpdateTransparency();
    }
    
    private void DetectPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        
        if (player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
            
            if (distanceToPlayer <= _detectionRange)
            {
                _playerTransform = player.transform;
                _chasingPlayer = true;
                _targetPosition = _playerTransform.position;
            }
            else
            {
                _chasingPlayer = false;
                _playerTransform = null;
            }
        }
        else
        {
            _chasingPlayer = false;
            _playerTransform = null;
        }
    }
    
    private void HandleMovement()
    {
        if (_chasingPlayer && _playerTransform != null)
        {
            _targetPosition = _playerTransform.position;
            MoveTowardsTarget(_phaseSpeed);
        }
        else
        {
            HandleWandering();
        }
        
        RotateTowardsTarget();
    }
    
    private void HandleWandering()
    {
        _wanderTimer_current -= Time.deltaTime;
        
        if (_wanderTimer_current <= 0f || Vector3.Distance(transform.position, _wanderTarget) < 1f)
        {
            SetNewWanderTarget();
            _wanderTimer_current = _wanderTimer;
        }
        
        _targetPosition = _wanderTarget;
        MoveTowardsTarget(_moveSpeed);
    }
    
    private void SetNewWanderTarget()
    {
        Vector3 randomDirection = Random.insideUnitSphere * _wanderRadius;
        randomDirection += transform.position;
        randomDirection.y = transform.position.y;
        _wanderTarget = randomDirection;
    }
    
    private void MoveTowardsTarget(float speed)
    {
        Vector3 direction = (_targetPosition - transform.position).normalized;
        _rigidbody.velocity = direction * speed;
    }
    
    private void RotateTowardsTarget()
    {
        Vector3 direction = (_targetPosition - transform.position).normalized;
        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
        }
    }
    
    private void HandlePhasing()
    {
        Vector3 direction = (_targetPosition - transform.position).normalized;
        RaycastHit hit;
        
        bool wallInPath = Physics.Raycast(transform.position, direction, out hit, 2f, _wallLayers);
        
        if (wallInPath || _chasingPlayer)
        {
            if (!_isPhasing)
            {
                StartPhasing();
            }
        }
        else
        {
            if (_isPhasing)
            {
                StopPhasing();
            }
        }
    }
    
    private void StartPhasing()
    {
        _isPhasing = true;
        if (_collider != null)
        {
            _collider.enabled = false;
        }
    }
    
    private void StopPhasing()
    {
        _isPhasing = false;
        if (_collider != null)
        {
            _collider.enabled = true;
        }
    }
    
    private void UpdateTransparency()
    {
        if (_ghostMaterial == null) return;
        
        float targetAlpha = _isPhasing ? _phaseTransparency : _normalTransparency;
        Color currentColor = _ghostMaterial.color;
        currentColor.a = Mathf.Lerp(currentColor.a, targetAlpha, _fadeSpeed * Time.deltaTime);
        _ghostMaterial.color = currentColor;
    }
    
    private void SetTransparency(float alpha)
    {
        if (_ghostMaterial == null) return;
        
        Color color = _ghostMaterial.color;
        color.a = alpha;
        _ghostMaterial.color = color;
        
        if (_ghostMaterial.HasProperty("_Mode"))
        {
            _ghostMaterial.SetFloat("_Mode", 3);
            _ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _ghostMaterial.SetInt("_ZWrite", 0);
            _ghostMaterial.DisableKeyword("_ALPHATEST_ON");
            _ghostMaterial.EnableKeyword("_ALPHABLEND_ON");
            _ghostMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _ghostMaterial.renderQueue = 3000;
        }
    }
    
    private void HandleAudio()
    {
        _soundTimer -= Time.deltaTime;
        
        if (_soundTimer <= 0f && _ghostSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_ghostSound);
            _soundTimer = _soundInterval;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Ghost touched player!");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _wanderRadius);
        
        if (_chasingPlayer && _playerTransform != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _playerTransform.position);
        }
        else
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _wanderTarget);
        }
    }
}