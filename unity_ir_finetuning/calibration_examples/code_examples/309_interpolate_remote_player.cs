// Prompt: interpolate remote player
// Type: movement

using UnityEngine;
using System.Collections.Generic;

public class RemotePlayerInterpolator : MonoBehaviour
{
    [Header("Interpolation Settings")]
    [SerializeField] private float _interpolationRate = 15f;
    [SerializeField] private float _extrapolationLimit = 0.5f;
    [SerializeField] private bool _useExtrapolation = true;
    [SerializeField] private float _maxInterpolationDistance = 10f;
    
    [Header("Network Settings")]
    [SerializeField] private float _sendRate = 20f;
    [SerializeField] private float _networkDelay = 0.1f;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugInfo = false;
    [SerializeField] private Color _debugLineColor = Color.red;

    private struct NetworkState
    {
        public float timestamp;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 velocity;
    }

    private Queue<NetworkState> _stateBuffer = new Queue<NetworkState>();
    private NetworkState _currentState;
    private NetworkState _previousState;
    private Vector3 _lastPosition;
    private float _lastUpdateTime;
    private bool _hasReceivedData = false;

    private void Start()
    {
        _lastPosition = transform.position;
        _lastUpdateTime = Time.time;
        
        _currentState = new NetworkState
        {
            timestamp = Time.time,
            position = transform.position,
            rotation = transform.rotation,
            velocity = Vector3.zero
        };
        
        _previousState = _currentState;
    }

    private void Update()
    {
        if (!_hasReceivedData) return;

        float renderTime = Time.time - _networkDelay;
        InterpolatePosition(renderTime);
        
        if (_showDebugInfo)
        {
            DrawDebugInfo();
        }
    }

    public void ReceiveNetworkUpdate(Vector3 position, Quaternion rotation, float timestamp)
    {
        Vector3 velocity = Vector3.zero;
        
        if (_hasReceivedData)
        {
            float deltaTime = timestamp - _currentState.timestamp;
            if (deltaTime > 0)
            {
                velocity = (position - _currentState.position) / deltaTime;
            }
        }

        NetworkState newState = new NetworkState
        {
            timestamp = timestamp,
            position = position,
            rotation = rotation,
            velocity = velocity
        };

        _stateBuffer.Enqueue(newState);
        
        // Keep buffer size reasonable
        while (_stateBuffer.Count > 10)
        {
            _stateBuffer.Dequeue();
        }
        
        _hasReceivedData = true;
    }

    private void InterpolatePosition(float renderTime)
    {
        // Find the two states to interpolate between
        NetworkState from = new NetworkState();
        NetworkState to = new NetworkState();
        bool foundStates = false;

        NetworkState[] states = _stateBuffer.ToArray();
        
        for (int i = 0; i < states.Length - 1; i++)
        {
            if (states[i].timestamp <= renderTime && renderTime <= states[i + 1].timestamp)
            {
                from = states[i];
                to = states[i + 1];
                foundStates = true;
                break;
            }
        }

        if (!foundStates)
        {
            // Use extrapolation if enabled
            if (_useExtrapolation && states.Length > 0)
            {
                NetworkState latest = states[states.Length - 1];
                float extrapolationTime = renderTime - latest.timestamp;
                
                if (extrapolationTime <= _extrapolationLimit)
                {
                    Vector3 extrapolatedPosition = latest.position + latest.velocity * extrapolationTime;
                    
                    // Check if extrapolation distance is reasonable
                    float distance = Vector3.Distance(transform.position, extrapolatedPosition);
                    if (distance <= _maxInterpolationDistance)
                    {
                        transform.position = Vector3.Lerp(transform.position, extrapolatedPosition, Time.deltaTime * _interpolationRate);
                        transform.rotation = Quaternion.Lerp(transform.rotation, latest.rotation, Time.deltaTime * _interpolationRate);
                    }
                }
            }
            return;
        }

        // Interpolate between the two states
        float lerpAmount = 0f;
        if (to.timestamp != from.timestamp)
        {
            lerpAmount = (renderTime - from.timestamp) / (to.timestamp - from.timestamp);
        }

        Vector3 targetPosition = Vector3.Lerp(from.position, to.position, lerpAmount);
        Quaternion targetRotation = Quaternion.Lerp(from.rotation, to.rotation, lerpAmount);

        // Check interpolation distance
        float interpolationDistance = Vector3.Distance(transform.position, targetPosition);
        if (interpolationDistance <= _maxInterpolationDistance)
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime * _interpolationRate);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * _interpolationRate);
        }
        else
        {
            // Teleport if too far
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        // Clean up old states
        CleanupOldStates(renderTime);
    }

    private void CleanupOldStates(float renderTime)
    {
        while (_stateBuffer.Count > 2)
        {
            NetworkState oldest = _stateBuffer.Peek();
            if (oldest.timestamp < renderTime - 1f) // Keep 1 second of history
            {
                _stateBuffer.Dequeue();
            }
            else
            {
                break;
            }
        }
    }

    private void DrawDebugInfo()
    {
        if (_stateBuffer.Count < 2) return;

        NetworkState[] states = _stateBuffer.ToArray();
        
        // Draw interpolation path
        for (int i = 0; i < states.Length - 1; i++)
        {
            Debug.DrawLine(states[i].position, states[i + 1].position, _debugLineColor, 0.1f);
        }

        // Draw current position
        Debug.DrawWireSphere(transform.position, 0.2f, Color.green, 0.1f);
        
        // Draw velocity vector
        if (states.Length > 0)
        {
            NetworkState latest = states[states.Length - 1];
            Debug.DrawRay(transform.position, latest.velocity, Color.blue, 0.1f);
        }
    }

    public void SetInterpolationRate(float rate)
    {
        _interpolationRate = Mathf.Clamp(rate, 1f, 50f);
    }

    public void SetNetworkDelay(float delay)
    {
        _networkDelay = Mathf.Clamp(delay, 0f, 1f);
    }

    public bool HasReceivedData()
    {
        return _hasReceivedData;
    }

    public int GetBufferSize()
    {
        return _stateBuffer.Count;
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showDebugInfo) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _maxInterpolationDistance);
        
        if (_stateBuffer != null && _stateBuffer.Count > 0)
        {
            NetworkState[] states = _stateBuffer.ToArray();
            Gizmos.color = Color.red;
            
            foreach (NetworkState state in states)
            {
                Gizmos.DrawWireCube(state.position, Vector3.one * 0.1f);
            }
        }
    }
}