// Prompt: spawner that creates enemies periodically
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class EnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject[] _enemyPrefabs;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private float _spawnInterval = 2f;
    [SerializeField] private int _maxEnemies = 10;
    [SerializeField] private bool _spawnOnStart = true;
    
    [Header("Spawn Area (if no spawn points)")]
    [SerializeField] private float _spawnRadius = 5f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Wave Settings")]
    [SerializeField] private bool _useWaves = false;
    [SerializeField] private int _enemiesPerWave = 5;
    [SerializeField] private float _timeBetweenWaves = 10f;
    
    private List<GameObject> _spawnedEnemies = new List<GameObject>();
    private Coroutine _spawnCoroutine;
    private int _currentWaveEnemies = 0;
    private bool _isSpawning = false;
    
    private void Start()
    {
        if (_spawnOnStart)
        {
            StartSpawning();
        }
        
        ValidateSpawnSettings();
    }
    
    private void ValidateSpawnSettings()
    {
        if (_enemyPrefabs == null || _enemyPrefabs.Length == 0)
        {
            Debug.LogWarning("EnemySpawner: No enemy prefabs assigned!");
            return;
        }
        
        if (_spawnPoints == null || _spawnPoints.Length == 0)
        {
            Debug.LogWarning("EnemySpawner: No spawn points assigned, using random positions around spawner.");
        }
    }
    
    public void StartSpawning()
    {
        if (_isSpawning) return;
        
        _isSpawning = true;
        
        if (_useWaves)
        {
            _spawnCoroutine = StartCoroutine(SpawnWaves());
        }
        else
        {
            _spawnCoroutine = StartCoroutine(SpawnContinuous());
        }
    }
    
    public void StopSpawning()
    {
        _isSpawning = false;
        
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }
    
    public void ClearAllEnemies()
    {
        for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (_spawnedEnemies[i] != null)
            {
                Destroy(_spawnedEnemies[i]);
            }
        }
        _spawnedEnemies.Clear();
    }
    
    private IEnumerator SpawnContinuous()
    {
        while (_isSpawning)
        {
            if (CanSpawn())
            {
                SpawnEnemy();
            }
            
            yield return new WaitForSeconds(_spawnInterval);
        }
    }
    
    private IEnumerator SpawnWaves()
    {
        while (_isSpawning)
        {
            _currentWaveEnemies = 0;
            
            // Spawn wave
            for (int i = 0; i < _enemiesPerWave; i++)
            {
                if (CanSpawn())
                {
                    SpawnEnemy();
                    _currentWaveEnemies++;
                    yield return new WaitForSeconds(_spawnInterval);
                }
            }
            
            // Wait for wave to be cleared or timeout
            float waveTimer = 0f;
            while (_currentWaveEnemies > 0 && waveTimer < _timeBetweenWaves)
            {
                CleanupDestroyedEnemies();
                waveTimer += Time.deltaTime;
                yield return null;
            }
            
            yield return new WaitForSeconds(_timeBetweenWaves);
        }
    }
    
    private bool CanSpawn()
    {
        CleanupDestroyedEnemies();
        return _spawnedEnemies.Count < _maxEnemies && _enemyPrefabs.Length > 0;
    }
    
    private void SpawnEnemy()
    {
        GameObject enemyPrefab = _enemyPrefabs[Random.Range(0, _enemyPrefabs.Length)];
        Vector3 spawnPosition = GetSpawnPosition();
        
        if (spawnPosition != Vector3.zero)
        {
            GameObject enemy = Instantiate(enemyPrefab, spawnPosition, GetSpawnRotation());
            _spawnedEnemies.Add(enemy);
            
            // Add enemy tracker component
            EnemyTracker tracker = enemy.GetComponent<EnemyTracker>();
            if (tracker == null)
            {
                tracker = enemy.AddComponent<EnemyTracker>();
            }
            tracker.Initialize(this);
        }
    }
    
    private Vector3 GetSpawnPosition()
    {
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            Transform spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
            return spawnPoint != null ? spawnPoint.position : transform.position;
        }
        
        // Random position around spawner
        Vector2 randomCircle = Random.insideUnitCircle * _spawnRadius;
        Vector3 randomPosition = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
        
        // Try to place on ground
        if (Physics.Raycast(randomPosition + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 20f, _groundLayer))
        {
            return hit.point;
        }
        
        return randomPosition;
    }
    
    private Quaternion GetSpawnRotation()
    {
        return Quaternion.Euler(0, Random.Range(0, 360), 0);
    }
    
    private void CleanupDestroyedEnemies()
    {
        for (int i = _spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (_spawnedEnemies[i] == null)
            {
                _spawnedEnemies.RemoveAt(i);
                if (_useWaves)
                {
                    _currentWaveEnemies--;
                }
            }
        }
    }
    
    public void OnEnemyDestroyed(GameObject enemy)
    {
        if (_spawnedEnemies.Contains(enemy))
        {
            _spawnedEnemies.Remove(enemy);
            if (_useWaves)
            {
                _currentWaveEnemies--;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw spawn radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCircle(transform.position, _spawnRadius);
        
        // Draw spawn points
        if (_spawnPoints != null)
        {
            Gizmos.color = Color.red;
            foreach (Transform spawnPoint in _spawnPoints)
            {
                if (spawnPoint != null)
                {
                    Gizmos.DrawWireCube(spawnPoint.position, Vector3.one);
                }
            }
        }
    }
}

public class EnemyTracker : MonoBehaviour
{
    private EnemySpawner _spawner;
    
    public void Initialize(EnemySpawner spawner)
    {
        _spawner = spawner;
    }
    
    private void OnDestroy()
    {
        if (_spawner != null)
        {
            _spawner.OnEnemyDestroyed(gameObject);
        }
    }
}