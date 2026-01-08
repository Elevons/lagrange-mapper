// Prompt: clone ability
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class CloneAbility : MonoBehaviour
{
    [System.Serializable]
    public class CloneData
    {
        public GameObject clonePrefab;
        public float lifetime = 5f;
        public float health = 100f;
        public bool canAttack = true;
        public bool followsPlayer = false;
        public float followDistance = 3f;
    }

    [Header("Clone Settings")]
    [SerializeField] private CloneData _cloneData;
    [SerializeField] private int _maxClones = 3;
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private float _spawnRadius = 2f;
    [SerializeField] private LayerMask _spawnLayerMask = -1;

    [Header("Input")]
    [SerializeField] private KeyCode _cloneKey = KeyCode.C;

    [Header("Effects")]
    [SerializeField] private GameObject _spawnEffect;
    [SerializeField] private GameObject _despawnEffect;
    [SerializeField] private AudioClip _spawnSound;
    [SerializeField] private AudioClip _despawnSound;

    [Header("Events")]
    public UnityEvent<GameObject> OnCloneSpawned;
    public UnityEvent<GameObject> OnCloneDespawned;
    public UnityEvent OnCooldownStarted;
    public UnityEvent OnCooldownEnded;

    private List<GameObject> _activeClones = new List<GameObject>();
    private float _lastCloneTime;
    private AudioSource _audioSource;
    private Transform _playerTransform;

    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _playerTransform = transform;
        
        if (_cloneData.clonePrefab == null)
        {
            Debug.LogWarning("Clone prefab not assigned to CloneAbility on " + gameObject.name);
        }
    }

    private void Update()
    {
        HandleInput();
        CleanupDestroyedClones();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(_cloneKey))
        {
            TrySpawnClone();
        }
    }

    public void TrySpawnClone()
    {
        if (!CanSpawnClone())
            return;

        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition != Vector3.zero)
        {
            SpawnClone(spawnPosition);
        }
    }

    private bool CanSpawnClone()
    {
        if (_cloneData.clonePrefab == null)
            return false;

        if (_activeClones.Count >= _maxClones)
            return false;

        if (Time.time - _lastCloneTime < _cooldownTime)
            return false;

        return true;
    }

    private Vector3 GetValidSpawnPosition()
    {
        for (int attempts = 0; attempts < 10; attempts++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
            Vector3 testPosition = _playerTransform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
            
            if (Physics.CheckSphere(testPosition, 0.5f, _spawnLayerMask))
                continue;

            return testPosition;
        }

        return _playerTransform.position + _playerTransform.forward * 2f;
    }

    private void SpawnClone(Vector3 position)
    {
        GameObject clone = Instantiate(_cloneData.clonePrefab, position, _playerTransform.rotation);
        
        CloneController cloneController = clone.GetComponent<CloneController>();
        if (cloneController == null)
            cloneController = clone.AddComponent<CloneController>();

        cloneController.Initialize(_cloneData, _playerTransform);
        
        _activeClones.Add(clone);
        _lastCloneTime = Time.time;

        StartCoroutine(DestroyCloneAfterTime(clone, _cloneData.lifetime));

        if (_spawnEffect != null)
            Instantiate(_spawnEffect, position, Quaternion.identity);

        if (_spawnSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_spawnSound);

        OnCloneSpawned?.Invoke(clone);
        OnCooldownStarted?.Invoke();
        
        StartCoroutine(CooldownTimer());
    }

    private IEnumerator DestroyCloneAfterTime(GameObject clone, float time)
    {
        yield return new WaitForSeconds(time);
        
        if (clone != null)
        {
            DestroyClone(clone);
        }
    }

    private IEnumerator CooldownTimer()
    {
        yield return new WaitForSeconds(_cooldownTime);
        OnCooldownEnded?.Invoke();
    }

    public void DestroyClone(GameObject clone)
    {
        if (clone == null)
            return;

        _activeClones.Remove(clone);

        if (_despawnEffect != null)
            Instantiate(_despawnEffect, clone.transform.position, Quaternion.identity);

        if (_despawnSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_despawnSound);

        OnCloneDespawned?.Invoke(clone);
        Destroy(clone);
    }

    public void DestroyAllClones()
    {
        for (int i = _activeClones.Count - 1; i >= 0; i--)
        {
            if (_activeClones[i] != null)
                DestroyClone(_activeClones[i]);
        }
        _activeClones.Clear();
    }

    private void CleanupDestroyedClones()
    {
        for (int i = _activeClones.Count - 1; i >= 0; i--)
        {
            if (_activeClones[i] == null)
            {
                _activeClones.RemoveAt(i);
            }
        }
    }

    public bool IsOnCooldown()
    {
        return Time.time - _lastCloneTime < _cooldownTime;
    }

    public float GetCooldownProgress()
    {
        return Mathf.Clamp01((Time.time - _lastCloneTime) / _cooldownTime);
    }

    public int GetActiveCloneCount()
    {
        CleanupDestroyedClones();
        return _activeClones.Count;
    }

    public List<GameObject> GetActiveClones()
    {
        CleanupDestroyedClones();
        return new List<GameObject>(_activeClones);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _spawnRadius);
    }
}

public class CloneController : MonoBehaviour
{
    private CloneAbility.CloneData _data;
    private Transform _playerTransform;
    private float _currentHealth;
    private bool _isInitialized;

    public void Initialize(CloneAbility.CloneData data, Transform playerTransform)
    {
        _data = data;
        _playerTransform = playerTransform;
        _currentHealth = data.health;
        _isInitialized = true;
    }

    private void Update()
    {
        if (!_isInitialized || _playerTransform == null)
            return;

        if (_data.followsPlayer)
        {
            FollowPlayer();
        }
    }

    private void FollowPlayer()
    {
        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        
        if (distance > _data.followDistance)
        {
            Vector3 direction = (_playerTransform.position - transform.position).normalized;
            transform.position += direction * Time.deltaTime * 5f;
            transform.LookAt(_playerTransform);
        }
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        
        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        CloneAbility cloneAbility = _playerTransform.GetComponent<CloneAbility>();
        if (cloneAbility != null)
        {
            cloneAbility.DestroyClone(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") && _data.canAttack)
        {
            // Clone can interact with enemies
            Debug.Log("Clone interacting with enemy: " + other.name);
        }
    }

    public float GetHealthPercentage()
    {
        return _currentHealth / _data.health;
    }
}