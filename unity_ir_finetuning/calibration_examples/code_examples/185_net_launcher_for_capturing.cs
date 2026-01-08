// Prompt: net launcher for capturing
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class NetLauncher : MonoBehaviour
{
    [Header("Net Projectile")]
    [SerializeField] private GameObject _netPrefab;
    [SerializeField] private Transform _launchPoint;
    [SerializeField] private float _launchForce = 15f;
    [SerializeField] private float _launchAngle = 45f;
    
    [Header("Capture Settings")]
    [SerializeField] private LayerMask _capturableLayers = -1;
    [SerializeField] private float _captureRadius = 3f;
    [SerializeField] private float _captureDuration = 5f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _launchKey = KeyCode.Space;
    [SerializeField] private bool _useMouseAiming = true;
    
    [Header("Cooldown")]
    [SerializeField] private float _cooldownTime = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _launchSound;
    [SerializeField] private AudioClip _captureSound;
    
    private Camera _playerCamera;
    private AudioSource _audioSource;
    private float _lastLaunchTime;
    private bool _canLaunch = true;
    
    private void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        if (_launchPoint == null)
            _launchPoint = transform;
    }
    
    private void Update()
    {
        HandleInput();
        UpdateCooldown();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_launchKey) && _canLaunch)
        {
            LaunchNet();
        }
    }
    
    private void UpdateCooldown()
    {
        if (!_canLaunch && Time.time >= _lastLaunchTime + _cooldownTime)
        {
            _canLaunch = true;
        }
    }
    
    private void LaunchNet()
    {
        if (_netPrefab == null) return;
        
        Vector3 launchDirection = GetLaunchDirection();
        GameObject netInstance = Instantiate(_netPrefab, _launchPoint.position, Quaternion.LookRotation(launchDirection));
        
        NetProjectile netProjectile = netInstance.GetComponent<NetProjectile>();
        if (netProjectile == null)
            netProjectile = netInstance.AddComponent<NetProjectile>();
            
        netProjectile.Initialize(launchDirection, _launchForce, _capturableLayers, _captureRadius, _captureDuration, _captureSound);
        
        PlayLaunchSound();
        
        _canLaunch = false;
        _lastLaunchTime = Time.time;
    }
    
    private Vector3 GetLaunchDirection()
    {
        Vector3 direction;
        
        if (_useMouseAiming && _playerCamera != null)
        {
            Ray ray = _playerCamera.ScreenPointToRay(Input.mousePosition);
            direction = ray.direction;
        }
        else
        {
            direction = transform.forward;
        }
        
        float angleInRadians = _launchAngle * Mathf.Deg2Rad;
        Vector3 horizontalDirection = new Vector3(direction.x, 0, direction.z).normalized;
        direction = horizontalDirection + Vector3.up * Mathf.Tan(angleInRadians);
        
        return direction.normalized;
    }
    
    private void PlayLaunchSound()
    {
        if (_audioSource != null && _launchSound != null)
        {
            _audioSource.PlayOneShot(_launchSound);
        }
    }
}

public class NetProjectile : MonoBehaviour
{
    private Vector3 _velocity;
    private LayerMask _capturableLayers;
    private float _captureRadius;
    private float _captureDuration;
    private AudioClip _captureSound;
    private bool _hasLanded;
    private List<GameObject> _capturedObjects = new List<GameObject>();
    private AudioSource _audioSource;
    
    [Header("Physics")]
    [SerializeField] private float _gravity = 9.81f;
    [SerializeField] private float _drag = 0.1f;
    
    [Header("Visual")]
    [SerializeField] private GameObject _netMesh;
    [SerializeField] private ParticleSystem _captureEffect;
    
    public void Initialize(Vector3 direction, float force, LayerMask capturableLayers, float captureRadius, float captureDuration, AudioClip captureSound)
    {
        _velocity = direction * force;
        _capturableLayers = capturableLayers;
        _captureRadius = captureRadius;
        _captureDuration = captureDuration;
        _captureSound = captureSound;
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        Destroy(gameObject, 10f);
    }
    
    private void Update()
    {
        if (!_hasLanded)
        {
            UpdateProjectileMovement();
            CheckGroundHit();
        }
    }
    
    private void UpdateProjectileMovement()
    {
        _velocity.y -= _gravity * Time.deltaTime;
        _velocity *= (1f - _drag * Time.deltaTime);
        transform.position += _velocity * Time.deltaTime;
        
        if (_velocity.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity);
        }
    }
    
    private void CheckGroundHit()
    {
        if (transform.position.y <= 0.1f || _velocity.y < 0 && Physics.Raycast(transform.position, Vector3.down, 0.5f))
        {
            LandNet();
        }
    }
    
    private void LandNet()
    {
        _hasLanded = true;
        _velocity = Vector3.zero;
        
        if (_netMesh != null)
        {
            _netMesh.SetActive(true);
            _netMesh.transform.localScale = Vector3.one * _captureRadius;
        }
        
        CaptureNearbyObjects();
        StartCoroutine(ReleaseAfterDuration());
    }
    
    private void CaptureNearbyObjects()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _captureRadius, _capturableLayers);
        
        foreach (Collider col in colliders)
        {
            if (col.gameObject != gameObject)
            {
                CaptureObject(col.gameObject);
            }
        }
        
        if (_capturedObjects.Count > 0)
        {
            PlayCaptureSound();
            PlayCaptureEffect();
        }
    }
    
    private void CaptureObject(GameObject target)
    {
        if (_capturedObjects.Contains(target)) return;
        
        _capturedObjects.Add(target);
        
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
        }
        
        CapturedObject capturedComponent = target.GetComponent<CapturedObject>();
        if (capturedComponent == null)
            capturedComponent = target.AddComponent<CapturedObject>();
            
        capturedComponent.SetCaptured(true, transform.position);
    }
    
    private void PlayCaptureSound()
    {
        if (_audioSource != null && _captureSound != null)
        {
            _audioSource.PlayOneShot(_captureSound);
        }
    }
    
    private void PlayCaptureEffect()
    {
        if (_captureEffect != null)
        {
            _captureEffect.Play();
        }
    }
    
    private IEnumerator ReleaseAfterDuration()
    {
        yield return new WaitForSeconds(_captureDuration);
        ReleaseAllObjects();
        Destroy(gameObject);
    }
    
    private void ReleaseAllObjects()
    {
        foreach (GameObject capturedObj in _capturedObjects)
        {
            if (capturedObj != null)
            {
                Rigidbody rb = capturedObj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.isKinematic = false;
                }
                
                CapturedObject capturedComponent = capturedObj.GetComponent<CapturedObject>();
                if (capturedComponent != null)
                {
                    capturedComponent.SetCaptured(false, Vector3.zero);
                }
            }
        }
        _capturedObjects.Clear();
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_hasLanded)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _captureRadius);
        }
    }
}

public class CapturedObject : MonoBehaviour
{
    private bool _isCaptured;
    private Vector3 _captureCenter;
    private Vector3 _originalPosition;
    private Rigidbody _rigidbody;
    
    [Header("Capture Behavior")]
    [SerializeField] private float _pullStrength = 5f;
    [SerializeField] private float _maxDistanceFromCenter = 2f;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _originalPosition = transform.position;
    }
    
    private void Update()
    {
        if (_isCaptured)
        {
            ApplyCaptureForces();
        }
    }
    
    public void SetCaptured(bool captured, Vector3 captureCenter)
    {
        _isCaptured = captured;
        _captureCenter = captureCenter;
        
        if (!captured && _rigidbody != null)
        {
            _rigidbody.AddForce(Vector3.up * 2f, ForceMode.Impulse);
        }
    }
    
    private void ApplyCaptureForces()
    {
        if (_rigidbody == null) return;
        
        Vector3 directionToCenter = (_captureCenter - transform.position);
        float distanceToCenter = directionToCenter.magnitude;
        
        if (distanceToCenter > _maxDistanceFromCenter)
        {
            Vector3 pullForce = directionToCenter.normalized * _pullStrength;
            _rigidbody.AddForce(pullForce, ForceMode.Force);
        }
        
        _rigidbody.drag = 5f;
    }
    
    private void OnDestroy()
    {
        if (_rigidbody != null)
        {
            _rigidbody.drag = 0f;
        }
    }
}