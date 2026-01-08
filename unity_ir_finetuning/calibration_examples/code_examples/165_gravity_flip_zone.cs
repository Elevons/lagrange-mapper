// Prompt: gravity flip zone
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class GravityFlipZone : MonoBehaviour
{
    [Header("Gravity Settings")]
    [SerializeField] private Vector3 _newGravityDirection = Vector3.up;
    [SerializeField] private float _gravityStrength = 9.81f;
    [SerializeField] private bool _useGlobalGravity = false;
    
    [Header("Zone Behavior")]
    [SerializeField] private bool _flipOnEnter = true;
    [SerializeField] private bool _revertOnExit = true;
    [SerializeField] private bool _affectRigidbodies = true;
    [SerializeField] private bool _affectCharacterControllers = true;
    
    [Header("Visual Feedback")]
    [SerializeField] private ParticleSystem _enterEffect;
    [SerializeField] private ParticleSystem _exitEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _enterSound;
    [SerializeField] private AudioClip _exitSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectEnterZone;
    public UnityEvent<GameObject> OnObjectExitZone;
    
    private Vector3 _originalGravity;
    private System.Collections.Generic.Dictionary<GameObject, GravityData> _affectedObjects;
    
    private class GravityData
    {
        public Vector3 originalGravity;
        public Rigidbody rigidbody;
        public CharacterController characterController;
        public bool wasUsingGravity;
        
        public GravityData(Rigidbody rb, CharacterController cc)
        {
            rigidbody = rb;
            characterController = cc;
            originalGravity = Physics.gravity;
            wasUsingGravity = rb != null ? rb.useGravity : false;
        }
    }
    
    private void Start()
    {
        _originalGravity = Physics.gravity;
        _affectedObjects = new System.Collections.Generic.Dictionary<GameObject, GravityData>();
        
        if (GetComponent<Collider>() == null)
        {
            Debug.LogWarning("GravityFlipZone requires a Collider component set as trigger!");
        }
        else
        {
            GetComponent<Collider>().isTrigger = true;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_flipOnEnter) return;
        
        GameObject obj = other.gameObject;
        
        if (_affectedObjects.ContainsKey(obj)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        CharacterController cc = other.GetComponent<CharacterController>();
        
        if ((!_affectRigidbodies || rb == null) && (!_affectCharacterControllers || cc == null))
            return;
        
        GravityData gravityData = new GravityData(rb, cc);
        _affectedObjects[obj] = gravityData;
        
        ApplyGravityFlip(obj, gravityData);
        
        PlayEffect(_enterEffect);
        PlaySound(_enterSound);
        
        OnObjectEnterZone?.Invoke(obj);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!_revertOnExit) return;
        
        GameObject obj = other.gameObject;
        
        if (!_affectedObjects.ContainsKey(obj)) return;
        
        GravityData gravityData = _affectedObjects[obj];
        RevertGravity(obj, gravityData);
        
        _affectedObjects.Remove(obj);
        
        PlayEffect(_exitEffect);
        PlaySound(_exitSound);
        
        OnObjectExitZone?.Invoke(obj);
    }
    
    private void ApplyGravityFlip(GameObject obj, GravityData gravityData)
    {
        if (_useGlobalGravity)
        {
            Physics.gravity = _newGravityDirection.normalized * _gravityStrength;
        }
        else
        {
            if (gravityData.rigidbody != null && _affectRigidbodies)
            {
                gravityData.rigidbody.useGravity = false;
                ApplyCustomGravityToRigidbody(gravityData.rigidbody);
            }
        }
        
        if (gravityData.characterController != null && _affectCharacterControllers)
        {
            CustomCharacterGravity customGravity = obj.GetComponent<CustomCharacterGravity>();
            if (customGravity == null)
            {
                customGravity = obj.AddComponent<CustomCharacterGravity>();
            }
            customGravity.SetGravity(_newGravityDirection.normalized * _gravityStrength);
        }
    }
    
    private void RevertGravity(GameObject obj, GravityData gravityData)
    {
        if (_useGlobalGravity)
        {
            Physics.gravity = gravityData.originalGravity;
        }
        else
        {
            if (gravityData.rigidbody != null && _affectRigidbodies)
            {
                gravityData.rigidbody.useGravity = gravityData.wasUsingGravity;
            }
        }
        
        if (gravityData.characterController != null && _affectCharacterControllers)
        {
            CustomCharacterGravity customGravity = obj.GetComponent<CustomCharacterGravity>();
            if (customGravity != null)
            {
                customGravity.RevertToOriginalGravity();
            }
        }
    }
    
    private void ApplyCustomGravityToRigidbody(Rigidbody rb)
    {
        if (rb != null)
        {
            rb.AddForce(_newGravityDirection.normalized * _gravityStrength * rb.mass, ForceMode.Acceleration);
        }
    }
    
    private void FixedUpdate()
    {
        if (!_useGlobalGravity)
        {
            foreach (var kvp in _affectedObjects)
            {
                GravityData gravityData = kvp.Value;
                if (gravityData.rigidbody != null && _affectRigidbodies)
                {
                    ApplyCustomGravityToRigidbody(gravityData.rigidbody);
                }
            }
        }
    }
    
    private void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
        {
            effect.Play();
        }
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
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
        
        Gizmos.color = Color.red;
        Vector3 arrowStart = transform.position;
        Vector3 arrowEnd = arrowStart + _newGravityDirection.normalized * 2f;
        Gizmos.DrawLine(arrowStart, arrowEnd);
        Gizmos.DrawSphere(arrowEnd, 0.1f);
    }
    
    private void OnDestroy()
    {
        if (_useGlobalGravity)
        {
            Physics.gravity = _originalGravity;
        }
        
        foreach (var kvp in _affectedObjects)
        {
            RevertGravity(kvp.Key, kvp.Value);
        }
    }
}

public class CustomCharacterGravity : MonoBehaviour
{
    private CharacterController _characterController;
    private Vector3 _customGravity;
    private Vector3 _originalGravity;
    private Vector3 _velocity;
    private bool _isUsingCustomGravity;
    
    private void Start()
    {
        _characterController = GetComponent<CharacterController>();
        _originalGravity = Vector3.down * 9.81f;
        _customGravity = _originalGravity;
    }
    
    public void SetGravity(Vector3 gravity)
    {
        _customGravity = gravity;
        _isUsingCustomGravity = true;
        _velocity = Vector3.zero;
    }
    
    public void RevertToOriginalGravity()
    {
        _customGravity = _originalGravity;
        _isUsingCustomGravity = false;
        _velocity = Vector3.zero;
    }
    
    private void Update()
    {
        if (_characterController != null && _isUsingCustomGravity)
        {
            if (_characterController.isGrounded)
            {
                _velocity.y = 0f;
            }
            
            _velocity += _customGravity * Time.deltaTime;
            _characterController.Move(_velocity * Time.deltaTime);
        }
    }
}