// Prompt: quicksand that slows movement
// Type: movement

using UnityEngine;
using System.Collections.Generic;

public class Quicksand : MonoBehaviour
{
    [Header("Quicksand Settings")]
    [SerializeField] private float _slowdownFactor = 0.3f;
    [SerializeField] private float _sinkDepth = 0.5f;
    [SerializeField] private float _sinkSpeed = 1f;
    [SerializeField] private float _exitSpeed = 2f;
    [SerializeField] private bool _affectsJumping = true;
    [SerializeField] private float _jumpReduction = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _enterSound;
    [SerializeField] private AudioClip _exitSound;
    [SerializeField] private AudioClip _strugglingSound;
    
    [Header("Detection")]
    [SerializeField] private LayerMask _affectedLayers = -1;
    [SerializeField] private string[] _affectedTags = { "Player", "Enemy", "NPC" };
    
    private Dictionary<GameObject, QuicksandData> _affectedObjects = new Dictionary<GameObject, QuicksandData>();
    private Collider _collider;
    
    private class QuicksandData
    {
        public Vector3 originalPosition;
        public float originalSpeed;
        public float originalJumpForce;
        public bool isCharacterController;
        public CharacterController characterController;
        public Rigidbody rigidBody;
        public float timeInQuicksand;
        public bool hasModifiedMovement;
    }
    
    private void Start()
    {
        _collider = GetComponent<Collider>();
        if (_collider == null)
        {
            Debug.LogError("Quicksand requires a Collider component!");
            return;
        }
        
        if (!_collider.isTrigger)
        {
            _collider.isTrigger = true;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_bubbleEffect != null && _bubbleEffect.isPlaying)
        {
            _bubbleEffect.Stop();
        }
    }
    
    private void Update()
    {
        UpdateAffectedObjects();
    }
    
    private void UpdateAffectedObjects()
    {
        var objectsToRemove = new List<GameObject>();
        
        foreach (var kvp in _affectedObjects)
        {
            var obj = kvp.Key;
            var data = kvp.Value;
            
            if (obj == null)
            {
                objectsToRemove.Add(obj);
                continue;
            }
            
            data.timeInQuicksand += Time.deltaTime;
            
            // Sink the object
            Vector3 targetPosition = data.originalPosition - Vector3.up * _sinkDepth;
            obj.transform.position = Vector3.MoveTowards(obj.transform.position, targetPosition, _sinkSpeed * Time.deltaTime);
            
            // Apply movement slowdown
            ApplyMovementSlowdown(obj, data);
        }
        
        // Remove null objects
        foreach (var obj in objectsToRemove)
        {
            _affectedObjects.Remove(obj);
        }
    }
    
    private void ApplyMovementSlowdown(GameObject obj, QuicksandData data)
    {
        if (data.hasModifiedMovement) return;
        
        // Try to modify CharacterController
        if (data.characterController != null)
        {
            // CharacterController speed modification would need to be handled by the movement script
            // We'll use a different approach - modify the object's scale or add a component
            var slowdownComponent = obj.GetComponent<QuicksandSlowdown>();
            if (slowdownComponent == null)
            {
                slowdownComponent = obj.AddComponent<QuicksandSlowdown>();
            }
            slowdownComponent.SetSlowdownFactor(_slowdownFactor);
            data.hasModifiedMovement = true;
        }
        
        // Modify Rigidbody drag
        if (data.rigidBody != null)
        {
            data.rigidBody.drag = Mathf.Max(data.rigidBody.drag, 10f * (1f - _slowdownFactor));
            data.rigidBody.angularDrag = Mathf.Max(data.rigidBody.angularDrag, 5f * (1f - _slowdownFactor));
            data.hasModifiedMovement = true;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!ShouldAffectObject(other.gameObject)) return;
        
        if (_affectedObjects.ContainsKey(other.gameObject)) return;
        
        var data = new QuicksandData
        {
            originalPosition = other.transform.position,
            timeInQuicksand = 0f,
            hasModifiedMovement = false
        };
        
        // Store original movement data
        data.characterController = other.GetComponent<CharacterController>();
        data.rigidBody = other.GetComponent<Rigidbody>();
        data.isCharacterController = data.characterController != null;
        
        _affectedObjects[other.gameObject] = data;
        
        // Play enter effects
        PlayEnterEffects();
        
        // Notify the object it entered quicksand
        other.SendMessage("OnEnterQuicksand", _slowdownFactor, SendMessageOptions.DontRequireReceiver);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!_affectedObjects.ContainsKey(other.gameObject)) return;
        
        var data = _affectedObjects[other.gameObject];
        
        // Restore original position gradually
        StartCoroutine(RestoreObjectPosition(other.gameObject, data));
        
        // Remove slowdown effects
        RemoveSlowdownEffects(other.gameObject, data);
        
        _affectedObjects.Remove(other.gameObject);
        
        // Play exit effects
        PlayExitEffects();
        
        // Notify the object it exited quicksand
        other.SendMessage("OnExitQuicksand", SendMessageOptions.DontRequireReceiver);
    }
    
    private System.Collections.IEnumerator RestoreObjectPosition(GameObject obj, QuicksandData data)
    {
        if (obj == null) yield break;
        
        Vector3 startPos = obj.transform.position;
        Vector3 targetPos = data.originalPosition;
        float journey = 0f;
        
        while (journey <= 1f && obj != null)
        {
            journey += Time.deltaTime * _exitSpeed;
            obj.transform.position = Vector3.Lerp(startPos, targetPos, journey);
            yield return null;
        }
    }
    
    private void RemoveSlowdownEffects(GameObject obj, QuicksandData data)
    {
        if (obj == null) return;
        
        // Remove slowdown component
        var slowdownComponent = obj.GetComponent<QuicksandSlowdown>();
        if (slowdownComponent != null)
        {
            Destroy(slowdownComponent);
        }
        
        // Restore Rigidbody properties
        if (data.rigidBody != null)
        {
            data.rigidBody.drag = 0f;
            data.rigidBody.angularDrag = 0.05f;
        }
    }
    
    private bool ShouldAffectObject(GameObject obj)
    {
        // Check layer
        if ((_affectedLayers.value & (1 << obj.layer)) == 0) return false;
        
        // Check tags
        foreach (string tag in _affectedTags)
        {
            if (obj.CompareTag(tag)) return true;
        }
        
        return false;
    }
    
    private void PlayEnterEffects()
    {
        if (_bubbleEffect != null && !_bubbleEffect.isPlaying)
        {
            _bubbleEffect.Play();
        }
        
        if (_audioSource != null && _enterSound != null)
        {
            _audioSource.PlayOneShot(_enterSound);
        }
    }
    
    private void PlayExitEffects()
    {
        if (_audioSource != null && _exitSound != null)
        {
            _audioSource.PlayOneShot(_exitSound);
        }
        
        if (_affectedObjects.Count == 0 && _bubbleEffect != null && _bubbleEffect.isPlaying)
        {
            _bubbleEffect.Stop();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.3f);
        if (_collider != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            if (_collider is BoxCollider box)
            {
                Gizmos.DrawCube(box.center, box.size);
            }
            else if (_collider is SphereCollider sphere)
            {
                Gizmos.DrawSphere(sphere.center, sphere.radius);
            }
        }
        
        // Draw sink depth
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position - Vector3.up * _sinkDepth, transform.localScale);
    }
}

public class QuicksandSlowdown : MonoBehaviour
{
    private float _slowdownFactor = 1f;
    
    public void SetSlowdownFactor(float factor)
    {
        _slowdownFactor = factor;
    }
    
    public float GetSlowdownFactor()
    {
        return _slowdownFactor;
    }
}