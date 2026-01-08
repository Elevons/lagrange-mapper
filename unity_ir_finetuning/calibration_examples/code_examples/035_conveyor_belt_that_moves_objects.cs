// Prompt: conveyor belt that moves objects
// Type: movement

using UnityEngine;
using System.Collections.Generic;

public class ConveyorBelt : MonoBehaviour
{
    [Header("Conveyor Settings")]
    [SerializeField] private float _speed = 2f;
    [SerializeField] private Vector3 _direction = Vector3.forward;
    [SerializeField] private bool _isActive = true;
    
    [Header("Visual Settings")]
    [SerializeField] private Material _beltMaterial;
    [SerializeField] private float _textureScrollSpeed = 1f;
    [SerializeField] private string _texturePropertyName = "_MainTex";
    
    [Header("Physics Settings")]
    [SerializeField] private LayerMask _affectedLayers = -1;
    [SerializeField] private bool _useRigidbodyForce = true;
    [SerializeField] private ForceMode _forceMode = ForceMode.Force;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _conveyorSound;
    [SerializeField] private float _audioVolume = 0.5f;
    
    private List<Rigidbody> _objectsOnBelt = new List<Rigidbody>();
    private List<Transform> _kinematicObjects = new List<Transform>();
    private Renderer _beltRenderer;
    private Vector2 _textureOffset;
    private Vector3 _normalizedDirection;
    
    private void Start()
    {
        _normalizedDirection = _direction.normalized;
        _beltRenderer = GetComponent<Renderer>();
        
        SetupAudio();
        
        if (_beltRenderer != null && _beltMaterial != null)
        {
            _beltRenderer.material = _beltMaterial;
        }
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_audioSource != null && _conveyorSound != null)
        {
            _audioSource.clip = _conveyorSound;
            _audioSource.loop = true;
            _audioSource.volume = _audioVolume;
            
            if (_isActive)
            {
                _audioSource.Play();
            }
        }
    }
    
    private void Update()
    {
        if (!_isActive) return;
        
        UpdateTextureAnimation();
        UpdateAudio();
    }
    
    private void FixedUpdate()
    {
        if (!_isActive) return;
        
        MoveObjectsOnBelt();
    }
    
    private void UpdateTextureAnimation()
    {
        if (_beltRenderer != null && _beltRenderer.material != null)
        {
            _textureOffset.x += _speed * _textureScrollSpeed * Time.deltaTime;
            _beltRenderer.material.SetTextureOffset(_texturePropertyName, _textureOffset);
        }
    }
    
    private void UpdateAudio()
    {
        if (_audioSource != null)
        {
            if (_isActive && !_audioSource.isPlaying && _conveyorSound != null)
            {
                _audioSource.Play();
            }
            else if (!_isActive && _audioSource.isPlaying)
            {
                _audioSource.Stop();
            }
        }
    }
    
    private void MoveObjectsOnBelt()
    {
        Vector3 movement = _normalizedDirection * _speed;
        
        // Move rigidbody objects
        for (int i = _objectsOnBelt.Count - 1; i >= 0; i--)
        {
            if (_objectsOnBelt[i] == null)
            {
                _objectsOnBelt.RemoveAt(i);
                continue;
            }
            
            Rigidbody rb = _objectsOnBelt[i];
            
            if (_useRigidbodyForce)
            {
                rb.AddForce(movement, _forceMode);
            }
            else
            {
                rb.velocity = new Vector3(movement.x, rb.velocity.y, movement.z);
            }
        }
        
        // Move kinematic objects
        for (int i = _kinematicObjects.Count - 1; i >= 0; i--)
        {
            if (_kinematicObjects[i] == null)
            {
                _kinematicObjects.RemoveAt(i);
                continue;
            }
            
            Transform obj = _kinematicObjects[i];
            obj.Translate(movement * Time.fixedDeltaTime, Space.World);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!IsInAffectedLayer(other.gameObject)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            if (rb.isKinematic)
            {
                if (!_kinematicObjects.Contains(other.transform))
                {
                    _kinematicObjects.Add(other.transform);
                }
            }
            else
            {
                if (!_objectsOnBelt.Contains(rb))
                {
                    _objectsOnBelt.Add(rb);
                }
            }
        }
        else
        {
            // Objects without rigidbody are moved as transforms
            if (!_kinematicObjects.Contains(other.transform))
            {
                _kinematicObjects.Add(other.transform);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!IsInAffectedLayer(other.gameObject)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        
        if (rb != null)
        {
            _objectsOnBelt.Remove(rb);
        }
        
        _kinematicObjects.Remove(other.transform);
    }
    
    private bool IsInAffectedLayer(GameObject obj)
    {
        return (_affectedLayers.value & (1 << obj.layer)) != 0;
    }
    
    public void SetActive(bool active)
    {
        _isActive = active;
        
        if (_audioSource != null)
        {
            if (_isActive && _conveyorSound != null)
            {
                _audioSource.Play();
            }
            else
            {
                _audioSource.Stop();
            }
        }
    }
    
    public void SetSpeed(float newSpeed)
    {
        _speed = newSpeed;
    }
    
    public void SetDirection(Vector3 newDirection)
    {
        _direction = newDirection;
        _normalizedDirection = _direction.normalized;
    }
    
    public void ReverseDirection()
    {
        _direction = -_direction;
        _normalizedDirection = _direction.normalized;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = transform.position;
        Vector3 direction = _direction.normalized;
        
        // Draw direction arrow
        Gizmos.DrawRay(center, direction * 2f);
        Gizmos.DrawWireCube(center + direction * 1.8f, Vector3.one * 0.2f);
        
        // Draw conveyor bounds
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = Color.green;
            Gizmos.matrix = transform.localToWorldMatrix;
            
            if (col is BoxCollider boxCol)
            {
                Gizmos.DrawWireCube(boxCol.center, boxCol.size);
            }
            else if (col is CapsuleCollider capsuleCol)
            {
                Gizmos.DrawWireCube(capsuleCol.center, new Vector3(capsuleCol.radius * 2, capsuleCol.height, capsuleCol.radius * 2));
            }
        }
    }
}