// Prompt: sync transform over network
// Type: general

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

public class NetworkTransformSync : NetworkBehaviour
{
    [Header("Sync Settings")]
    [SerializeField] private bool _syncPosition = true;
    [SerializeField] private bool _syncRotation = true;
    [SerializeField] private bool _syncScale = false;
    
    [Header("Interpolation")]
    [SerializeField] private float _lerpRate = 15f;
    [SerializeField] private float _snapThreshold = 5f;
    [SerializeField] private bool _useInterpolation = true;
    
    [Header("Network Settings")]
    [SerializeField] private float _sendRate = 20f;
    [SerializeField] private bool _compressRotation = true;
    
    private Vector3 _networkPosition;
    private Quaternion _networkRotation;
    private Vector3 _networkScale;
    
    private Vector3 _lastSentPosition;
    private Quaternion _lastSentRotation;
    private Vector3 _lastSentScale;
    
    private float _lastSendTime;
    private float _sendInterval;
    
    private bool _hasReceivedData = false;

    void Start()
    {
        _sendInterval = 1f / _sendRate;
        
        if (isLocalPlayer)
        {
            _networkPosition = transform.position;
            _networkRotation = transform.rotation;
            _networkScale = transform.localScale;
        }
    }

    void Update()
    {
        if (isLocalPlayer)
        {
            CheckForChangesAndSend();
        }
        else if (_hasReceivedData && _useInterpolation)
        {
            InterpolateTransform();
        }
    }

    private void CheckForChangesAndSend()
    {
        if (Time.time - _lastSendTime < _sendInterval)
            return;

        bool hasChanged = false;

        if (_syncPosition && Vector3.Distance(transform.position, _lastSentPosition) > 0.01f)
        {
            hasChanged = true;
        }

        if (_syncRotation && Quaternion.Angle(transform.rotation, _lastSentRotation) > 0.1f)
        {
            hasChanged = true;
        }

        if (_syncScale && Vector3.Distance(transform.localScale, _lastSentScale) > 0.01f)
        {
            hasChanged = true;
        }

        if (hasChanged)
        {
            CmdSendTransform(transform.position, transform.rotation, transform.localScale);
            _lastSendTime = Time.time;
            _lastSentPosition = transform.position;
            _lastSentRotation = transform.rotation;
            _lastSentScale = transform.localScale;
        }
    }

    [Command]
    private void CmdSendTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        RpcReceiveTransform(position, rotation, scale);
    }

    [ClientRpc]
    private void RpcReceiveTransform(Vector3 position, Quaternion rotation, Vector3 scale)
    {
        if (isLocalPlayer)
            return;

        _networkPosition = position;
        _networkRotation = rotation;
        _networkScale = scale;
        _hasReceivedData = true;

        if (!_useInterpolation)
        {
            ApplyTransformDirectly();
        }
        else
        {
            // Check if we need to snap due to large distance
            if (_syncPosition && Vector3.Distance(transform.position, _networkPosition) > _snapThreshold)
            {
                transform.position = _networkPosition;
            }
        }
    }

    private void InterpolateTransform()
    {
        if (_syncPosition)
        {
            transform.position = Vector3.Lerp(transform.position, _networkPosition, _lerpRate * Time.deltaTime);
        }

        if (_syncRotation)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, _networkRotation, _lerpRate * Time.deltaTime);
        }

        if (_syncScale)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _networkScale, _lerpRate * Time.deltaTime);
        }
    }

    private void ApplyTransformDirectly()
    {
        if (_syncPosition)
        {
            transform.position = _networkPosition;
        }

        if (_syncRotation)
        {
            transform.rotation = _networkRotation;
        }

        if (_syncScale)
        {
            transform.localScale = _networkScale;
        }
    }

    public override void OnStartLocalPlayer()
    {
        // Initialize network values for local player
        _networkPosition = transform.position;
        _networkRotation = transform.rotation;
        _networkScale = transform.localScale;
        
        _lastSentPosition = transform.position;
        _lastSentRotation = transform.rotation;
        _lastSentScale = transform.localScale;
    }

    void OnDrawGizmosSelected()
    {
        if (!isLocalPlayer && _hasReceivedData)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_networkPosition, 0.1f);
            
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(_networkPosition, _networkRotation * Vector3.forward);
        }
    }
}