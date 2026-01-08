// Prompt: snake that slithers and bites
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class Snake : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _turnSpeed = 90f;
    [SerializeField] private float _slitherAmplitude = 0.5f;
    [SerializeField] private float _slitherFrequency = 2f;
    
    [Header("Body")]
    [SerializeField] private GameObject _bodySegmentPrefab;
    [SerializeField] private int _initialBodySegments = 5;
    [SerializeField] private float _segmentSpacing = 0.8f;
    
    [Header("Bite")]
    [SerializeField] private float _biteRange = 1.5f;
    [SerializeField] private float _biteCooldown = 2f;
    [SerializeField] private int _biteDamage = 10;
    [SerializeField] private LayerMask _biteTargetMask = -1;
    [SerializeField] private AudioClip _biteSound;
    
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 5f;
    [SerializeField] private float _chaseSpeed = 4f;
    
    private List<Transform> _bodySegments = new List<Transform>();
    private List<Vector3> _positionHistory = new List<Vector3>();
    private Transform _target;
    private float _lastBiteTime;
    private float _slitherTime;
    private Vector3 _wanderDirection;
    private float _wanderTimer;
    private AudioSource _audioSource;
    private bool _isChasing;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        CreateBodySegments();
        _wanderDirection = transform.forward;
        _wanderTimer = Random.Range(2f, 5f);
    }
    
    private void Update()
    {
        DetectTargets();
        HandleMovement();
        UpdateSlither();
        UpdateBodySegments();
        CheckForBite();
    }
    
    private void CreateBodySegments()
    {
        if (_bodySegmentPrefab == null) return;
        
        for (int i = 0; i < _initialBodySegments; i++)
        {
            Vector3 segmentPosition = transform.position - transform.forward * (i + 1) * _segmentSpacing;
            GameObject segment = Instantiate(_bodySegmentPrefab, segmentPosition, transform.rotation);
            _bodySegments.Add(segment.transform);
            _positionHistory.Add(segmentPosition);
        }
    }
    
    private void DetectTargets()
    {
        Collider[] targets = Physics.OverlapSphere(transform.position, _detectionRange, _biteTargetMask);
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;
        
        foreach (Collider target in targets)
        {
            if (target.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestTarget = target.transform;
            }
        }
        
        _target = closestTarget;
        _isChasing = _target != null;
    }
    
    private void HandleMovement()
    {
        Vector3 moveDirection;
        float currentSpeed;
        
        if (_isChasing && _target != null)
        {
            moveDirection = (_target.position - transform.position).normalized;
            currentSpeed = _chaseSpeed;
        }
        else
        {
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f)
            {
                _wanderDirection = Quaternion.Euler(0, Random.Range(-45f, 45f), 0) * transform.forward;
                _wanderTimer = Random.Range(2f, 5f);
            }
            moveDirection = _wanderDirection;
            currentSpeed = _moveSpeed;
        }
        
        if (moveDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, _turnSpeed * Time.deltaTime);
        }
        
        transform.Translate(Vector3.forward * currentSpeed * Time.deltaTime);
    }
    
    private void UpdateSlither()
    {
        _slitherTime += Time.deltaTime * _slitherFrequency;
        float slitherOffset = Mathf.Sin(_slitherTime) * _slitherAmplitude;
        
        Vector3 slitherPosition = transform.position + transform.right * slitherOffset;
        transform.position = slitherPosition;
        
        _positionHistory.Insert(0, transform.position);
        
        int maxHistoryLength = _bodySegments.Count * 5;
        if (_positionHistory.Count > maxHistoryLength)
        {
            _positionHistory.RemoveRange(maxHistoryLength, _positionHistory.Count - maxHistoryLength);
        }
    }
    
    private void UpdateBodySegments()
    {
        for (int i = 0; i < _bodySegments.Count; i++)
        {
            if (_bodySegments[i] == null) continue;
            
            int historyIndex = (i + 1) * 5;
            if (historyIndex < _positionHistory.Count)
            {
                Vector3 targetPosition = _positionHistory[historyIndex];
                _bodySegments[i].position = Vector3.Lerp(_bodySegments[i].position, targetPosition, Time.deltaTime * 10f);
                
                if (historyIndex + 5 < _positionHistory.Count)
                {
                    Vector3 direction = (_positionHistory[historyIndex] - _positionHistory[historyIndex + 5]).normalized;
                    if (direction != Vector3.zero)
                    {
                        _bodySegments[i].rotation = Quaternion.LookRotation(direction);
                    }
                }
            }
        }
    }
    
    private void CheckForBite()
    {
        if (Time.time - _lastBiteTime < _biteCooldown) return;
        if (_target == null) return;
        
        float distanceToTarget = Vector3.Distance(transform.position, _target.position);
        if (distanceToTarget <= _biteRange)
        {
            Vector3 directionToTarget = (_target.position - transform.position).normalized;
            float angle = Vector3.Angle(transform.forward, directionToTarget);
            
            if (angle < 45f)
            {
                PerformBite();
            }
        }
    }
    
    private void PerformBite()
    {
        _lastBiteTime = Time.time;
        
        if (_biteSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_biteSound);
        }
        
        if (_target != null)
        {
            if (_target.CompareTag("Player"))
            {
                _target.SendMessage("TakeDamage", _biteDamage, SendMessageOptions.DontRequireReceiver);
            }
            
            Rigidbody targetRb = _target.GetComponent<Rigidbody>();
            if (targetRb != null)
            {
                Vector3 knockbackDirection = (_target.position - transform.position).normalized;
                targetRb.AddForce(knockbackDirection * 5f, ForceMode.Impulse);
            }
        }
        
        transform.position += transform.forward * 0.3f;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _biteRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        if (_target != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _target.position);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _target = other.transform;
        }
    }
}