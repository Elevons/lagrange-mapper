// Prompt: mimic chest that looks normal until player gets within 3 units, then opens its lid, reveals glowing eyes - if touched it snaps shut, applies strong force pushing player away, and spawns 3 smaller mimic objects that chase player
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class MimicChest : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private float _detectionRange = 3f;
    [SerializeField] private LayerMask _playerLayer = -1;
    
    [Header("Visual Components")]
    [SerializeField] private Transform _lidTransform;
    [SerializeField] private GameObject _eyesObject;
    [SerializeField] private Light _eyeGlow;
    [SerializeField] private float _lidOpenAngle = 45f;
    [SerializeField] private float _lidAnimationSpeed = 2f;
    
    [Header("Attack")]
    [SerializeField] private float _pushForce = 15f;
    [SerializeField] private float _pushRadius = 2f;
    [SerializeField] private float _snapSpeed = 5f;
    
    [Header("Minion Spawning")]
    [SerializeField] private GameObject _miniMimicPrefab;
    [SerializeField] private int _spawnCount = 3;
    [SerializeField] private float _spawnRadius = 2f;
    [SerializeField] private float _spawnHeight = 1f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _revealSound;
    [SerializeField] private AudioClip _snapSound;
    [SerializeField] private AudioClip _spawnSound;
    
    private bool _isRevealed = false;
    private bool _hasAttacked = false;
    private Transform _playerTransform;
    private Quaternion _lidClosedRotation;
    private Quaternion _lidOpenRotation;
    private Coroutine _lidAnimationCoroutine;
    private List<GameObject> _spawnedMinions = new List<GameObject>();
    
    private void Start()
    {
        if (_lidTransform != null)
        {
            _lidClosedRotation = _lidTransform.localRotation;
            _lidOpenRotation = _lidClosedRotation * Quaternion.Euler(-_lidOpenAngle, 0, 0);
        }
        
        if (_eyesObject != null)
            _eyesObject.SetActive(false);
            
        if (_eyeGlow != null)
            _eyeGlow.enabled = false;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
    }
    
    private void Update()
    {
        if (_hasAttacked) return;
        
        FindPlayer();
        
        if (_playerTransform != null)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
            
            if (!_isRevealed && distanceToPlayer <= _detectionRange)
            {
                RevealMimic();
            }
        }
    }
    
    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }
    }
    
    private void RevealMimic()
    {
        _isRevealed = true;
        
        if (_eyesObject != null)
            _eyesObject.SetActive(true);
            
        if (_eyeGlow != null)
            _eyeGlow.enabled = true;
            
        if (_lidAnimationCoroutine != null)
            StopCoroutine(_lidAnimationCoroutine);
            
        _lidAnimationCoroutine = StartCoroutine(AnimateLid(_lidOpenRotation));
        
        PlaySound(_revealSound);
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_hasAttacked) return;
        
        if (other.CompareTag("Player"))
        {
            Attack(other);
        }
    }
    
    private void OnMouseDown()
    {
        if (_hasAttacked) return;
        
        Collider playerCollider = FindObjectOfType<Collider>();
        if (playerCollider != null && playerCollider.CompareTag("Player"))
        {
            Attack(playerCollider);
        }
    }
    
    private void Attack(Collider playerCollider)
    {
        _hasAttacked = true;
        
        StartCoroutine(AttackSequence(playerCollider));
    }
    
    private IEnumerator AttackSequence(Collider playerCollider)
    {
        // Snap shut
        if (_lidAnimationCoroutine != null)
            StopCoroutine(_lidAnimationCoroutine);
            
        _lidAnimationCoroutine = StartCoroutine(AnimateLid(_lidClosedRotation, _snapSpeed));
        
        PlaySound(_snapSound);
        
        // Hide eyes
        if (_eyesObject != null)
            _eyesObject.SetActive(false);
            
        if (_eyeGlow != null)
            _eyeGlow.enabled = false;
        
        yield return new WaitForSeconds(0.2f);
        
        // Push player away
        PushPlayerAway(playerCollider);
        
        yield return new WaitForSeconds(0.3f);
        
        // Spawn minions
        SpawnMinions();
    }
    
    private void PushPlayerAway(Collider playerCollider)
    {
        Rigidbody playerRb = playerCollider.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            Vector3 pushDirection = (playerCollider.transform.position - transform.position).normalized;
            pushDirection.y = 0.3f; // Add slight upward force
            
            playerRb.AddForce(pushDirection * _pushForce, ForceMode.Impulse);
        }
        
        // Also push any other rigidbodies in range
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, _pushRadius);
        foreach (Collider col in nearbyObjects)
        {
            if (col != playerCollider && col.attachedRigidbody != null && !col.CompareTag("MiniMimic"))
            {
                Vector3 pushDirection = (col.transform.position - transform.position).normalized;
                col.attachedRigidbody.AddForce(pushDirection * (_pushForce * 0.5f), ForceMode.Impulse);
            }
        }
    }
    
    private void SpawnMinions()
    {
        if (_miniMimicPrefab == null) return;
        
        PlaySound(_spawnSound);
        
        for (int i = 0; i < _spawnCount; i++)
        {
            float angle = (360f / _spawnCount) * i;
            Vector3 spawnOffset = new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) * _spawnRadius,
                _spawnHeight,
                Mathf.Sin(angle * Mathf.Deg2Rad) * _spawnRadius
            );
            
            Vector3 spawnPosition = transform.position + spawnOffset;
            
            GameObject minion = Instantiate(_miniMimicPrefab, spawnPosition, Quaternion.identity);
            _spawnedMinions.Add(minion);
            
            // Add chase behavior to minion
            MiniMimicChaser chaser = minion.GetComponent<MiniMimicChaser>();
            if (chaser == null)
                chaser = minion.AddComponent<MiniMimicChaser>();
        }
    }
    
    private IEnumerator AnimateLid(Quaternion targetRotation, float speed = -1f)
    {
        if (_lidTransform == null) yield break;
        
        float animSpeed = speed > 0 ? speed : _lidAnimationSpeed;
        Quaternion startRotation = _lidTransform.localRotation;
        float elapsed = 0f;
        float duration = 1f / animSpeed;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            _lidTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
            yield return null;
        }
        
        _lidTransform.localRotation = targetRotation;
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
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _pushRadius);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _spawnRadius);
    }
}

public class MiniMimicChaser : MonoBehaviour
{
    [Header("Chase Behavior")]
    [SerializeField] private float _chaseSpeed = 3f;
    [SerializeField] private float _jumpForce = 5f;
    [SerializeField] private float _jumpCooldown = 2f;
    [SerializeField] private float _attackRange = 1f;
    [SerializeField] private float _lifetime = 30f;
    
    private Transform _playerTransform;
    private Rigidbody _rigidbody;
    private float _lastJumpTime;
    private bool _isGrounded = true;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        gameObject.tag = "MiniMimic";
        
        FindPlayer();
        
        Destroy(gameObject, _lifetime);
    }
    
    private void Update()
    {
        FindPlayer();
        
        if (_playerTransform != null)
        {
            ChasePlayer();
        }
    }
    
    private void FindPlayer()
    {
        if (_playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _playerTransform = player.transform;
        }
    }
    
    private void ChasePlayer()
    {
        Vector3 directionToPlayer = (_playerTransform.position - transform.position).normalized;
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        
        if (distanceToPlayer > _attackRange)
        {
            // Move towards player
            Vector3 moveDirection = new Vector3(directionToPlayer.x, 0, directionToPlayer.z);
            transform.position += moveDirection * _chaseSpeed * Time.deltaTime;
            
            // Look at player
            transform.LookAt(new Vector3(_playerTransform.position.x, transform.position.y, _playerTransform.position.z));
            
            // Jump if grounded and cooldown elapsed
            if (_isGrounded && Time.time - _lastJumpTime > _jumpCooldown)
            {
                Jump();
            }
        }
    }
    
    private void Jump()
    {
        if (_rigidbody != null)
        {
            _rigidbody.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _lastJumpTime = Time.time;
            _isGrounded = false;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground") || collision.contacts[0].normal.y > 0.7f)
        {
            _isGrounded = true;
        }
        
        if (collision.gameObject.CompareTag("Player"))
        {
            // Mini attack - small push
            Rigidbody playerRb = collision.rigidbody;
            if (playerRb != null)
            {
                Vector3 pushDirection = (collision.transform.position - transform.position).normalized;
                playerRb.AddForce(pushDirection * 3f, ForceMode.Impulse);
            }
        }
    }
}