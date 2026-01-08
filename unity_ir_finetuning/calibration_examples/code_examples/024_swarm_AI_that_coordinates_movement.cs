// Prompt: swarm AI that coordinates movement
// Type: combat

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class SwarmAI : MonoBehaviour
{
    [Header("Swarm Settings")]
    [SerializeField] private float _detectionRadius = 10f;
    [SerializeField] private float _separationRadius = 2f;
    [SerializeField] private float _alignmentRadius = 5f;
    [SerializeField] private float _cohesionRadius = 8f;
    
    [Header("Movement Forces")]
    [SerializeField] private float _separationForce = 2f;
    [SerializeField] private float _alignmentForce = 1f;
    [SerializeField] private float _cohesionForce = 1f;
    [SerializeField] private float _seekForce = 1.5f;
    [SerializeField] private float _maxSpeed = 5f;
    [SerializeField] private float _maxForce = 3f;
    
    [Header("Target Settings")]
    [SerializeField] private Transform _target;
    [SerializeField] private bool _followPlayer = true;
    [SerializeField] private float _wanderRadius = 15f;
    [SerializeField] private float _wanderChangeInterval = 3f;
    
    [Header("Avoidance")]
    [SerializeField] private LayerMask _obstacleLayer = 1;
    [SerializeField] private float _avoidanceRadius = 3f;
    [SerializeField] private float _avoidanceForce = 4f;
    
    private Vector3 _velocity;
    private Vector3 _wanderTarget;
    private float _wanderTimer;
    private List<SwarmAI> _neighbors;
    private static List<SwarmAI> _allAgents = new List<SwarmAI>();
    
    private void Start()
    {
        _allAgents.Add(this);
        _neighbors = new List<SwarmAI>();
        _velocity = Random.insideUnitSphere * 2f;
        _velocity.y = 0f;
        
        if (_followPlayer && _target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
                _target = player.transform;
        }
        
        GenerateWanderTarget();
    }
    
    private void OnDestroy()
    {
        _allAgents.Remove(this);
    }
    
    private void Update()
    {
        UpdateNeighbors();
        Vector3 steeringForce = CalculateSteeringForce();
        ApplyForce(steeringForce);
        Move();
        
        _wanderTimer += Time.deltaTime;
        if (_wanderTimer >= _wanderChangeInterval)
        {
            GenerateWanderTarget();
            _wanderTimer = 0f;
        }
    }
    
    private void UpdateNeighbors()
    {
        _neighbors.Clear();
        
        foreach (SwarmAI agent in _allAgents)
        {
            if (agent == this) continue;
            
            float distance = Vector3.Distance(transform.position, agent.transform.position);
            if (distance <= _detectionRadius)
            {
                _neighbors.Add(agent);
            }
        }
    }
    
    private Vector3 CalculateSteeringForce()
    {
        Vector3 separation = CalculateSeparation();
        Vector3 alignment = CalculateAlignment();
        Vector3 cohesion = CalculateCohesion();
        Vector3 seek = CalculateSeek();
        Vector3 avoidance = CalculateObstacleAvoidance();
        
        Vector3 totalForce = separation * _separationForce +
                           alignment * _alignmentForce +
                           cohesion * _cohesionForce +
                           seek * _seekForce +
                           avoidance * _avoidanceForce;
        
        return Vector3.ClampMagnitude(totalForce, _maxForce);
    }
    
    private Vector3 CalculateSeparation()
    {
        Vector3 steer = Vector3.zero;
        int count = 0;
        
        foreach (SwarmAI neighbor in _neighbors)
        {
            float distance = Vector3.Distance(transform.position, neighbor.transform.position);
            if (distance > 0 && distance < _separationRadius)
            {
                Vector3 diff = transform.position - neighbor.transform.position;
                diff.Normalize();
                diff /= distance; // Weight by distance
                steer += diff;
                count++;
            }
        }
        
        if (count > 0)
        {
            steer /= count;
            steer.Normalize();
            steer *= _maxSpeed;
            steer -= _velocity;
        }
        
        return steer;
    }
    
    private Vector3 CalculateAlignment()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        foreach (SwarmAI neighbor in _neighbors)
        {
            float distance = Vector3.Distance(transform.position, neighbor.transform.position);
            if (distance > 0 && distance < _alignmentRadius)
            {
                sum += neighbor._velocity;
                count++;
            }
        }
        
        if (count > 0)
        {
            sum /= count;
            sum.Normalize();
            sum *= _maxSpeed;
            Vector3 steer = sum - _velocity;
            return steer;
        }
        
        return Vector3.zero;
    }
    
    private Vector3 CalculateCohesion()
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        
        foreach (SwarmAI neighbor in _neighbors)
        {
            float distance = Vector3.Distance(transform.position, neighbor.transform.position);
            if (distance > 0 && distance < _cohesionRadius)
            {
                sum += neighbor.transform.position;
                count++;
            }
        }
        
        if (count > 0)
        {
            sum /= count;
            return Seek(sum);
        }
        
        return Vector3.zero;
    }
    
    private Vector3 CalculateSeek()
    {
        Vector3 targetPos;
        
        if (_target != null)
        {
            targetPos = _target.position;
        }
        else
        {
            targetPos = _wanderTarget;
        }
        
        return Seek(targetPos);
    }
    
    private Vector3 Seek(Vector3 target)
    {
        Vector3 desired = target - transform.position;
        desired.Normalize();
        desired *= _maxSpeed;
        
        Vector3 steer = desired - _velocity;
        return steer;
    }
    
    private Vector3 CalculateObstacleAvoidance()
    {
        Vector3 avoidanceForce = Vector3.zero;
        
        Collider[] obstacles = Physics.OverlapSphere(transform.position, _avoidanceRadius, _obstacleLayer);
        
        foreach (Collider obstacle in obstacles)
        {
            Vector3 directionAway = transform.position - obstacle.ClosestPoint(transform.position);
            if (directionAway.magnitude > 0)
            {
                directionAway.Normalize();
                float distance = Vector3.Distance(transform.position, obstacle.ClosestPoint(transform.position));
                directionAway /= distance; // Weight by distance
                avoidanceForce += directionAway;
            }
        }
        
        return avoidanceForce;
    }
    
    private void ApplyForce(Vector3 force)
    {
        _velocity += force * Time.deltaTime;
        _velocity = Vector3.ClampMagnitude(_velocity, _maxSpeed);
    }
    
    private void Move()
    {
        transform.position += _velocity * Time.deltaTime;
        
        if (_velocity.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(_velocity);
        }
    }
    
    private void GenerateWanderTarget()
    {
        Vector2 randomCircle = Random.insideUnitCircle * _wanderRadius;
        _wanderTarget = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _separationRadius);
        
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _alignmentRadius);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _cohesionRadius);
        
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _avoidanceRadius);
        
        if (_target != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, _target.position);
        }
        
        Gizmos.color = Color.white;
        Gizmos.DrawSphere(_wanderTarget, 0.5f);
        
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position, _velocity);
    }
}