// Prompt: spawn point assignment
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class SpawnPointManager : MonoBehaviour
{
    [System.Serializable]
    public class SpawnPoint
    {
        public Transform transform;
        public bool isOccupied;
        public float occupiedRadius = 2f;
        public string teamTag = "";
        public int priority = 0;
        public bool isActive = true;
        
        public SpawnPoint(Transform t)
        {
            transform = t;
            isOccupied = false;
        }
    }

    [Header("Spawn Configuration")]
    [SerializeField] private Transform[] _spawnTransforms;
    [SerializeField] private bool _autoFindSpawnPoints = true;
    [SerializeField] private string _spawnPointTag = "SpawnPoint";
    
    [Header("Assignment Rules")]
    [SerializeField] private bool _useTeamBasedSpawning = false;
    [SerializeField] private bool _avoidOccupiedSpawns = true;
    [SerializeField] private float _occupiedCheckRadius = 2f;
    [SerializeField] private LayerMask _obstacleCheckLayers = -1;
    
    [Header("Spawn Modes")]
    [SerializeField] private SpawnMode _spawnMode = SpawnMode.Random;
    [SerializeField] private bool _usePrioritySystem = false;
    [SerializeField] private float _respawnCooldown = 3f;
    
    [Header("Events")]
    public UnityEvent<Transform> OnSpawnPointAssigned;
    public UnityEvent<Transform> OnSpawnPointOccupied;
    public UnityEvent<Transform> OnSpawnPointFreed;

    public enum SpawnMode
    {
        Random,
        Sequential,
        Nearest,
        Furthest,
        Priority
    }

    private List<SpawnPoint> _spawnPoints = new List<SpawnPoint>();
    private Dictionary<string, List<SpawnPoint>> _teamSpawnPoints = new Dictionary<string, List<SpawnPoint>>();
    private int _sequentialIndex = 0;
    private Dictionary<Transform, float> _spawnCooldowns = new Dictionary<Transform, float>();

    private void Start()
    {
        InitializeSpawnPoints();
        InvokeRepeating(nameof(UpdateOccupiedStates), 0f, 0.5f);
        InvokeRepeating(nameof(UpdateCooldowns), 0f, 0.1f);
    }

    private void InitializeSpawnPoints()
    {
        _spawnPoints.Clear();
        _teamSpawnPoints.Clear();

        if (_autoFindSpawnPoints)
        {
            GameObject[] spawnObjects = GameObject.FindGameObjectsWithTag(_spawnPointTag);
            _spawnTransforms = spawnObjects.Select(go => go.transform).ToArray();
        }

        foreach (Transform spawnTransform in _spawnTransforms)
        {
            if (spawnTransform == null) continue;

            SpawnPoint spawnPoint = new SpawnPoint(spawnTransform);
            
            SpawnPointData spawnData = spawnTransform.GetComponent<SpawnPointData>();
            if (spawnData != null)
            {
                spawnPoint.teamTag = spawnData.teamTag;
                spawnPoint.priority = spawnData.priority;
                spawnPoint.occupiedRadius = spawnData.occupiedRadius;
                spawnPoint.isActive = spawnData.isActive;
            }

            _spawnPoints.Add(spawnPoint);

            if (_useTeamBasedSpawning && !string.IsNullOrEmpty(spawnPoint.teamTag))
            {
                if (!_teamSpawnPoints.ContainsKey(spawnPoint.teamTag))
                {
                    _teamSpawnPoints[spawnPoint.teamTag] = new List<SpawnPoint>();
                }
                _teamSpawnPoints[spawnPoint.teamTag].Add(spawnPoint);
            }
        }

        if (_usePrioritySystem)
        {
            _spawnPoints = _spawnPoints.OrderByDescending(sp => sp.priority).ToList();
        }
    }

    public Transform AssignSpawnPoint(string teamTag = "", Vector3 referencePosition = default)
    {
        List<SpawnPoint> availableSpawns = GetAvailableSpawnPoints(teamTag);
        
        if (availableSpawns.Count == 0)
        {
            Debug.LogWarning("No available spawn points found!");
            return null;
        }

        SpawnPoint selectedSpawn = SelectSpawnPoint(availableSpawns, referencePosition);
        
        if (selectedSpawn != null)
        {
            selectedSpawn.isOccupied = true;
            _spawnCooldowns[selectedSpawn.transform] = Time.time + _respawnCooldown;
            OnSpawnPointAssigned?.Invoke(selectedSpawn.transform);
            OnSpawnPointOccupied?.Invoke(selectedSpawn.transform);
            return selectedSpawn.transform;
        }

        return null;
    }

    public Transform GetNearestSpawnPoint(Vector3 position, string teamTag = "")
    {
        List<SpawnPoint> availableSpawns = GetAvailableSpawnPoints(teamTag);
        
        if (availableSpawns.Count == 0) return null;

        SpawnPoint nearest = availableSpawns
            .OrderBy(sp => Vector3.Distance(sp.transform.position, position))
            .FirstOrDefault();

        return nearest?.transform;
    }

    public Transform GetRandomSpawnPoint(string teamTag = "")
    {
        List<SpawnPoint> availableSpawns = GetAvailableSpawnPoints(teamTag);
        
        if (availableSpawns.Count == 0) return null;

        int randomIndex = Random.Range(0, availableSpawns.Count);
        return availableSpawns[randomIndex].transform;
    }

    public void FreeSpawnPoint(Transform spawnTransform)
    {
        SpawnPoint spawnPoint = _spawnPoints.FirstOrDefault(sp => sp.transform == spawnTransform);
        if (spawnPoint != null)
        {
            spawnPoint.isOccupied = false;
            OnSpawnPointFreed?.Invoke(spawnTransform);
        }
    }

    public void SetSpawnPointActive(Transform spawnTransform, bool active)
    {
        SpawnPoint spawnPoint = _spawnPoints.FirstOrDefault(sp => sp.transform == spawnTransform);
        if (spawnPoint != null)
        {
            spawnPoint.isActive = active;
        }
    }

    public bool IsSpawnPointAvailable(Transform spawnTransform)
    {
        SpawnPoint spawnPoint = _spawnPoints.FirstOrDefault(sp => sp.transform == spawnTransform);
        if (spawnPoint == null) return false;

        return spawnPoint.isActive && 
               !spawnPoint.isOccupied && 
               !IsOnCooldown(spawnTransform) &&
               !IsObstructed(spawnTransform);
    }

    private List<SpawnPoint> GetAvailableSpawnPoints(string teamTag)
    {
        List<SpawnPoint> spawnsToCheck = _spawnPoints;

        if (_useTeamBasedSpawning && !string.IsNullOrEmpty(teamTag) && _teamSpawnPoints.ContainsKey(teamTag))
        {
            spawnsToCheck = _teamSpawnPoints[teamTag];
        }

        return spawnsToCheck.Where(sp => 
            sp.isActive && 
            (!_avoidOccupiedSpawns || !sp.isOccupied) &&
            !IsOnCooldown(sp.transform) &&
            !IsObstructed(sp.transform)
        ).ToList();
    }

    private SpawnPoint SelectSpawnPoint(List<SpawnPoint> availableSpawns, Vector3 referencePosition)
    {
        switch (_spawnMode)
        {
            case SpawnMode.Random:
                return availableSpawns[Random.Range(0, availableSpawns.Count)];

            case SpawnMode.Sequential:
                SpawnPoint sequential = availableSpawns[_sequentialIndex % availableSpawns.Count];
                _sequentialIndex++;
                return sequential;

            case SpawnMode.Nearest:
                if (referencePosition == default) referencePosition = Vector3.zero;
                return availableSpawns.OrderBy(sp => Vector3.Distance(sp.transform.position, referencePosition)).FirstOrDefault();

            case SpawnMode.Furthest:
                if (referencePosition == default) referencePosition = Vector3.zero;
                return availableSpawns.OrderByDescending(sp => Vector3.Distance(sp.transform.position, referencePosition)).FirstOrDefault();

            case SpawnMode.Priority:
                return availableSpawns.OrderByDescending(sp => sp.priority).FirstOrDefault();

            default:
                return availableSpawns[0];
        }
    }

    private bool IsOnCooldown(Transform spawnTransform)
    {
        return _spawnCooldowns.ContainsKey(spawnTransform) && 
               Time.time < _spawnCooldowns[spawnTransform];
    }

    private bool IsObstructed(Transform spawnTransform)
    {
        Collider[] colliders = Physics.OverlapSphere(spawnTransform.position, _occupiedCheckRadius, _obstacleCheckLayers);
        return colliders.Length > 0;
    }

    private void UpdateOccupiedStates()
    {
        foreach (SpawnPoint spawnPoint in _spawnPoints)
        {
            bool wasOccupied = spawnPoint.isOccupied;
            
            Collider[] colliders = Physics.OverlapSphere(
                spawnPoint.transform.position, 
                spawnPoint.occupiedRadius
            );

            spawnPoint.isOccupied = colliders.Any(col => col.CompareTag("Player"));

            if (wasOccupied && !spawnPoint.isOccupied)
            {
                OnSpawnPointFreed?.Invoke(spawnPoint.transform);
            }
            else if (!wasOccupied && spawnPoint.isOccupied)
            {
                OnSpawnPointOccupied?.Invoke(spawnPoint.transform);
            }
        }
    }

    private void UpdateCooldowns()
    {
        List<Transform> expiredCooldowns = new List<Transform>();
        
        foreach (var kvp in _spawnCooldowns)
        {
            if (Time.time >= kvp.Value)
            {
                expiredCooldowns.Add(kvp.Key);
            }
        }

        foreach (Transform expired in expiredCooldowns)
        {
            _spawnCooldowns.Remove(expired);
        }
    }

    private void OnDrawGizmos()
    {
        if (_spawnPoints == null) return;

        foreach (SpawnPoint spawnPoint in _spawnPoints)
        {
            if (spawnPoint.transform == null) continue;

            Gizmos.color = spawnPoint.isActive ? 
                (spawnPoint.isOccupied ? Color.red : Color.green) : 
                Color.gray;

            Gizmos.DrawWireSphere(spawnPoint.transform.position, spawnPoint.occupiedRadius);
            Gizmos.DrawWireCube(spawnPoint.transform.position, Vector3.one * 0.5f);
        }
    }
}

[System.Serializable]
public class SpawnPointData : MonoBehaviour
{
    [Header("Spawn Point Settings")]
    public string teamTag = "";
    public int priority = 0;
    public float occupiedRadius = 2f;
    public bool isActive = true;
}