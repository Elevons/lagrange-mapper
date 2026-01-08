// Prompt: slingshot with stones
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Slingshot : MonoBehaviour
{
    [Header("Slingshot Settings")]
    [SerializeField] private Transform _leftAnchor;
    [SerializeField] private Transform _rightAnchor;
    [SerializeField] private Transform _projectileSpawnPoint;
    [SerializeField] private LineRenderer _leftBand;
    [SerializeField] private LineRenderer _rightBand;
    [SerializeField] private float _maxPullDistance = 3f;
    [SerializeField] private float _forceMultiplier = 10f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Projectile Settings")]
    [SerializeField] private GameObject _stonePrefab;
    [SerializeField] private int _maxStones = 10;
    [SerializeField] private float _stoneLifetime = 5f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _pullSound;
    [SerializeField] private AudioClip _releaseSound;
    
    [Header("Events")]
    public UnityEvent<Vector3> OnStoneReleased;
    public UnityEvent OnOutOfStones;
    
    private Camera _camera;
    private bool _isPulling = false;
    private Vector3 _pullPosition;
    private GameObject _currentStone;
    private int _remainingStones;
    private Vector3 _initialSpawnPosition;
    
    private void Start()
    {
        _camera = Camera.main;
        if (_camera == null)
            _camera = FindObjectOfType<Camera>();
            
        _remainingStones = _maxStones;
        _initialSpawnPosition = _projectileSpawnPoint.position;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        SetupBands();
        ResetSlingshot();
    }
    
    private void Update()
    {
        HandleInput();
        UpdateVisuals();
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && _remainingStones > 0)
        {
            StartPull();
        }
        else if (Input.GetMouseButton(0) && _isPulling)
        {
            UpdatePull();
        }
        else if (Input.GetMouseButtonUp(0) && _isPulling)
        {
            ReleaseSlingshot();
        }
    }
    
    private void StartPull()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        float distanceToSlingshot = Vector3.Distance(mouseWorldPos, _projectileSpawnPoint.position);
        
        if (distanceToSlingshot <= _maxPullDistance)
        {
            _isPulling = true;
            _pullPosition = _initialSpawnPosition;
            CreateStone();
            
            if (_audioSource && _pullSound)
                _audioSource.PlayOneShot(_pullSound);
        }
    }
    
    private void UpdatePull()
    {
        Vector3 mouseWorldPos = GetMouseWorldPosition();
        Vector3 pullDirection = mouseWorldPos - _initialSpawnPosition;
        
        float pullDistance = Mathf.Clamp(pullDirection.magnitude, 0f, _maxPullDistance);
        _pullPosition = _initialSpawnPosition + pullDirection.normalized * pullDistance;
        
        if (_currentStone != null)
        {
            _currentStone.transform.position = _pullPosition;
        }
    }
    
    private void ReleaseSlingshot()
    {
        if (_currentStone != null)
        {
            Vector3 launchDirection = (_initialSpawnPosition - _pullPosition).normalized;
            float pullDistance = Vector3.Distance(_pullPosition, _initialSpawnPosition);
            float launchForce = pullDistance * _forceMultiplier;
            
            Rigidbody stoneRb = _currentStone.GetComponent<Rigidbody>();
            if (stoneRb != null)
            {
                stoneRb.isKinematic = false;
                stoneRb.AddForce(launchDirection * launchForce, ForceMode.Impulse);
            }
            
            Destroy(_currentStone, _stoneLifetime);
            _currentStone = null;
            _remainingStones--;
            
            if (_audioSource && _releaseSound)
                _audioSource.PlayOneShot(_releaseSound);
                
            OnStoneReleased?.Invoke(launchDirection * launchForce);
            
            if (_remainingStones <= 0)
                OnOutOfStones?.Invoke();
        }
        
        _isPulling = false;
        ResetSlingshot();
    }
    
    private void CreateStone()
    {
        if (_stonePrefab != null)
        {
            _currentStone = Instantiate(_stonePrefab, _pullPosition, Quaternion.identity);
            
            Rigidbody rb = _currentStone.GetComponent<Rigidbody>();
            if (rb == null)
                rb = _currentStone.AddComponent<Rigidbody>();
                
            rb.isKinematic = true;
            rb.mass = 0.1f;
            
            Collider col = _currentStone.GetComponent<Collider>();
            if (col == null)
            {
                SphereCollider sphereCol = _currentStone.AddComponent<SphereCollider>();
                sphereCol.radius = 0.05f;
            }
            
            StoneProjectile stoneScript = _currentStone.GetComponent<StoneProjectile>();
            if (stoneScript == null)
                _currentStone.AddComponent<StoneProjectile>();
        }
    }
    
    private void UpdateVisuals()
    {
        if (_leftBand != null && _rightBand != null)
        {
            Vector3 bandPosition = _isPulling ? _pullPosition : _initialSpawnPosition;
            
            _leftBand.SetPosition(0, _leftAnchor.position);
            _leftBand.SetPosition(1, bandPosition);
            
            _rightBand.SetPosition(0, _rightAnchor.position);
            _rightBand.SetPosition(1, bandPosition);
        }
    }
    
    private void SetupBands()
    {
        if (_leftBand != null)
        {
            _leftBand.positionCount = 2;
            _leftBand.startWidth = 0.02f;
            _leftBand.endWidth = 0.02f;
        }
        
        if (_rightBand != null)
        {
            _rightBand.positionCount = 2;
            _rightBand.startWidth = 0.02f;
            _rightBand.endWidth = 0.02f;
        }
    }
    
    private void ResetSlingshot()
    {
        _pullPosition = _initialSpawnPosition;
        UpdateVisuals();
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        if (_camera == null) return Vector3.zero;
        
        Vector3 mouseScreenPos = Input.mousePosition;
        mouseScreenPos.z = Vector3.Distance(_camera.transform.position, transform.position);
        return _camera.ScreenToWorldPoint(mouseScreenPos);
    }
    
    public void AddStones(int amount)
    {
        _remainingStones += amount;
        _remainingStones = Mathf.Clamp(_remainingStones, 0, _maxStones);
    }
    
    public int GetRemainingStones()
    {
        return _remainingStones;
    }
    
    public void ResetAmmo()
    {
        _remainingStones = _maxStones;
    }
}

public class StoneProjectile : MonoBehaviour
{
    [Header("Impact Settings")]
    [SerializeField] private float _impactForce = 5f;
    [SerializeField] private GameObject _impactEffect;
    [SerializeField] private AudioClip _impactSound;
    
    private AudioSource _audioSource;
    private bool _hasImpacted = false;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_hasImpacted) return;
        
        _hasImpacted = true;
        
        if (_impactEffect != null)
        {
            GameObject effect = Instantiate(_impactEffect, transform.position, Quaternion.identity);
            Destroy(effect, 2f);
        }
        
        if (_audioSource && _impactSound)
            _audioSource.PlayOneShot(_impactSound);
            
        Rigidbody hitRb = collision.rigidbody;
        if (hitRb != null)
        {
            Vector3 impactDirection = collision.contacts[0].normal * -1f;
            hitRb.AddForceAtPosition(impactDirection * _impactForce, collision.contacts[0].point, ForceMode.Impulse);
        }
        
        if (collision.gameObject.CompareTag("Enemy"))
        {
            collision.gameObject.SendMessage("TakeDamage", 1, SendMessageOptions.DontRequireReceiver);
        }
    }
}