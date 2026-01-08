// Prompt: skeleton that reassembles after death
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SkeletonReassembly : MonoBehaviour
{
    [Header("Skeleton Settings")]
    [SerializeField] private float _health = 100f;
    [SerializeField] private float _reassemblyTime = 5f;
    [SerializeField] private int _maxReassemblies = 3;
    [SerializeField] private float _reassemblyRadius = 2f;
    
    [Header("Bone Parts")]
    [SerializeField] private Transform[] _boneParts;
    [SerializeField] private Rigidbody[] _boneRigidbodies;
    [SerializeField] private Collider _mainCollider;
    [SerializeField] private Animator _animator;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _deathEffect;
    [SerializeField] private ParticleSystem _reassemblyEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _deathSound;
    [SerializeField] private AudioClip _reassemblySound;
    
    [Header("Movement")]
    [SerializeField] private float _moveSpeed = 3f;
    [SerializeField] private float _detectionRange = 10f;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    private float _currentHealth;
    private int _reassemblyCount = 0;
    private bool _isDead = false;
    private bool _isReassembling = false;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Transform _player;
    private Vector3[] _originalBonePositions;
    private Quaternion[] _originalBoneRotations;
    
    private enum SkeletonState
    {
        Alive,
        Dead,
        Reassembling
    }
    
    private SkeletonState _currentState = SkeletonState.Alive;
    
    void Start()
    {
        _currentHealth = _health;
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_animator == null)
            _animator = GetComponent<Animator>();
        
        if (_mainCollider == null)
            _mainCollider = GetComponent<Collider>();
        
        StoreBonePositions();
        FindPlayer();
    }
    
    void Update()
    {
        switch (_currentState)
        {
            case SkeletonState.Alive:
                HandleAliveState();
                break;
            case SkeletonState.Dead:
                HandleDeadState();
                break;
            case SkeletonState.Reassembling:
                break;
        }
    }
    
    void HandleAliveState()
    {
        if (_player != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _player.position);
            
            if (distanceToPlayer <= _detectionRange)
            {
                Vector3 direction = (_player.position - transform.position).normalized;
                transform.position += direction * _moveSpeed * Time.deltaTime;
                transform.LookAt(_player);
                
                if (_animator != null)
                    _animator.SetBool("IsWalking", true);
            }
            else
            {
                if (_animator != null)
                    _animator.SetBool("IsWalking", false);
            }
        }
    }
    
    void HandleDeadState()
    {
        if (!_isReassembling && _reassemblyCount < _maxReassemblies)
        {
            StartCoroutine(ReassembleCoroutine());
        }
    }
    
    void StoreBonePositions()
    {
        if (_boneParts != null && _boneParts.Length > 0)
        {
            _originalBonePositions = new Vector3[_boneParts.Length];
            _originalBoneRotations = new Quaternion[_boneParts.Length];
            
            for (int i = 0; i < _boneParts.Length; i++)
            {
                if (_boneParts[i] != null)
                {
                    _originalBonePositions[i] = _boneParts[i].localPosition;
                    _originalBoneRotations[i] = _boneParts[i].localRotation;
                }
            }
        }
    }
    
    void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
            _player = playerObject.transform;
    }
    
    public void TakeDamage(float damage)
    {
        if (_currentState != SkeletonState.Alive) return;
        
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }
    
    void Die()
    {
        _currentState = SkeletonState.Dead;
        _isDead = true;
        
        if (_animator != null)
        {
            _animator.SetBool("IsWalking", false);
            _animator.SetTrigger("Die");
        }
        
        if (_mainCollider != null)
            _mainCollider.enabled = false;
        
        ScatterBones();
        
        if (_deathEffect != null)
            _deathEffect.Play();
        
        if (_audioSource != null && _deathSound != null)
            _audioSource.PlayOneShot(_deathSound);
    }
    
    void ScatterBones()
    {
        if (_boneParts == null || _boneRigidbodies == null) return;
        
        for (int i = 0; i < _boneParts.Length && i < _boneRigidbodies.Length; i++)
        {
            if (_boneParts[i] != null && _boneRigidbodies[i] != null)
            {
                _boneRigidbodies[i].isKinematic = false;
                _boneRigidbodies[i].useGravity = true;
                
                Vector3 randomForce = new Vector3(
                    Random.Range(-5f, 5f),
                    Random.Range(2f, 8f),
                    Random.Range(-5f, 5f)
                );
                
                _boneRigidbodies[i].AddForce(randomForce, ForceMode.Impulse);
                _boneRigidbodies[i].AddTorque(Random.insideUnitSphere * 10f, ForceMode.Impulse);
            }
        }
    }
    
    IEnumerator ReassembleCoroutine()
    {
        _isReassembling = true;
        _currentState = SkeletonState.Reassembling;
        
        yield return new WaitForSeconds(_reassemblyTime);
        
        if (_reassemblyEffect != null)
            _reassemblyEffect.Play();
        
        if (_audioSource != null && _reassemblySound != null)
            _audioSource.PlayOneShot(_reassemblySound);
        
        yield return StartCoroutine(GatherBones());
        
        _currentHealth = _health;
        _isDead = false;
        _isReassembling = false;
        _reassemblyCount++;
        _currentState = SkeletonState.Alive;
        
        if (_mainCollider != null)
            _mainCollider.enabled = true;
        
        if (_animator != null)
            _animator.SetTrigger("Reassemble");
        
        transform.position = _originalPosition;
        transform.rotation = _originalRotation;
    }
    
    IEnumerator GatherBones()
    {
        float gatherTime = 2f;
        float elapsed = 0f;
        
        if (_boneParts == null || _boneRigidbodies == null) yield break;
        
        Vector3[] startPositions = new Vector3[_boneParts.Length];
        Quaternion[] startRotations = new Quaternion[_boneParts.Length];
        
        for (int i = 0; i < _boneParts.Length; i++)
        {
            if (_boneParts[i] != null && _boneRigidbodies[i] != null)
            {
                _boneRigidbodies[i].isKinematic = true;
                _boneRigidbodies[i].useGravity = false;
                startPositions[i] = _boneParts[i].position;
                startRotations[i] = _boneParts[i].rotation;
            }
        }
        
        while (elapsed < gatherTime)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / gatherTime;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            for (int i = 0; i < _boneParts.Length; i++)
            {
                if (_boneParts[i] != null)
                {
                    Vector3 targetWorldPos = transform.TransformPoint(_originalBonePositions[i]);
                    Quaternion targetWorldRot = transform.rotation * _originalBoneRotations[i];
                    
                    _boneParts[i].position = Vector3.Lerp(startPositions[i], targetWorldPos, t);
                    _boneParts[i].rotation = Quaternion.Lerp(startRotations[i], targetWorldRot, t);
                }
            }
            
            yield return null;
        }
        
        for (int i = 0; i < _boneParts.Length; i++)
        {
            if (_boneParts[i] != null)
            {
                _boneParts[i].localPosition = _originalBonePositions[i];
                _boneParts[i].localRotation = _originalBoneRotations[i];
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_currentState != SkeletonState.Alive) return;
        
        if (other.CompareTag("Player"))
        {
            TakeDamage(25f);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _detectionRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _reassemblyRadius);
    }
}