// Prompt: ice wand that freezes
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class IceWand : MonoBehaviour
{
    [Header("Wand Settings")]
    [SerializeField] private float _freezeRange = 10f;
    [SerializeField] private float _freezeDuration = 3f;
    [SerializeField] private float _cooldownTime = 1f;
    [SerializeField] private LayerMask _freezableLayer = -1;
    [SerializeField] private KeyCode _useKey = KeyCode.Mouse0;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _freezeEffectPrefab;
    [SerializeField] private ParticleSystem _castParticles;
    [SerializeField] private LineRenderer _iceBeam;
    [SerializeField] private float _beamDuration = 0.3f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _freezeSound;
    [SerializeField] private AudioClip _cooldownSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnTargetFrozen;
    public UnityEvent OnWandUsed;
    public UnityEvent OnCooldownComplete;
    
    private bool _isOnCooldown;
    private float _cooldownTimer;
    private Camera _playerCamera;
    
    [System.Serializable]
    public class FreezableObject
    {
        public Rigidbody rigidBody;
        public Collider objectCollider;
        public Renderer objectRenderer;
        public Material originalMaterial;
        public Material frozenMaterial;
        public bool isCurrentlyFrozen;
        public float freezeEndTime;
        public Vector3 originalVelocity;
        public Vector3 originalAngularVelocity;
        public bool wasKinematic;
        
        public FreezableObject(GameObject obj)
        {
            rigidBody = obj.GetComponent<Rigidbody>();
            objectCollider = obj.GetComponent<Collider>();
            objectRenderer = obj.GetComponent<Renderer>();
            
            if (objectRenderer != null)
            {
                originalMaterial = objectRenderer.material;
            }
            
            if (rigidBody != null)
            {
                wasKinematic = rigidBody.isKinematic;
            }
        }
    }
    
    private System.Collections.Generic.List<FreezableObject> _frozenObjects = new System.Collections.Generic.List<FreezableObject>();
    
    void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_iceBeam != null)
        {
            _iceBeam.enabled = false;
            _iceBeam.startWidth = 0.1f;
            _iceBeam.endWidth = 0.05f;
        }
    }
    
    void Update()
    {
        HandleInput();
        UpdateCooldown();
        UpdateFrozenObjects();
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(_useKey) && !_isOnCooldown)
        {
            UseWand();
        }
    }
    
    void UpdateCooldown()
    {
        if (_isOnCooldown)
        {
            _cooldownTimer -= Time.deltaTime;
            if (_cooldownTimer <= 0f)
            {
                _isOnCooldown = false;
                OnCooldownComplete?.Invoke();
                
                if (_audioSource != null && _cooldownSound != null)
                    _audioSource.PlayOneShot(_cooldownSound);
            }
        }
    }
    
    void UpdateFrozenObjects()
    {
        for (int i = _frozenObjects.Count - 1; i >= 0; i--)
        {
            var frozenObj = _frozenObjects[i];
            
            if (frozenObj.rigidBody == null)
            {
                _frozenObjects.RemoveAt(i);
                continue;
            }
            
            if (Time.time >= frozenObj.freezeEndTime)
            {
                UnfreezeObject(frozenObj);
                _frozenObjects.RemoveAt(i);
            }
        }
    }
    
    void UseWand()
    {
        if (_playerCamera == null) return;
        
        Ray ray = _playerCamera.ScreenPointToRay(new Vector3(Screen.width / 2, Screen.height / 2, 0));
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, _freezeRange, _freezableLayer))
        {
            FreezeTarget(hit.collider.gameObject, hit.point);
            
            if (_iceBeam != null)
                StartCoroutine(ShowIceBeam(transform.position, hit.point));
        }
        
        StartCooldown();
        OnWandUsed?.Invoke();
        
        if (_castParticles != null)
            _castParticles.Play();
            
        if (_audioSource != null && _freezeSound != null)
            _audioSource.PlayOneShot(_freezeSound);
    }
    
    void FreezeTarget(GameObject target, Vector3 hitPoint)
    {
        if (target == null) return;
        
        // Check if already frozen
        foreach (var frozen in _frozenObjects)
        {
            if (frozen.rigidBody != null && frozen.rigidBody.gameObject == target)
            {
                // Extend freeze duration
                frozen.freezeEndTime = Time.time + _freezeDuration;
                return;
            }
        }
        
        FreezableObject freezableObj = new FreezableObject(target);
        
        if (freezableObj.rigidBody != null)
        {
            // Store current physics state
            freezableObj.originalVelocity = freezableObj.rigidBody.velocity;
            freezableObj.originalAngularVelocity = freezableObj.rigidBody.angularVelocity;
            
            // Freeze physics
            freezableObj.rigidBody.velocity = Vector3.zero;
            freezableObj.rigidBody.angularVelocity = Vector3.zero;
            freezableObj.rigidBody.isKinematic = true;
        }
        
        // Visual freeze effect
        if (freezableObj.objectRenderer != null && freezableObj.frozenMaterial != null)
        {
            freezableObj.objectRenderer.material = freezableObj.frozenMaterial;
        }
        
        // Add ice effect
        if (_freezeEffectPrefab != null)
        {
            GameObject iceEffect = Instantiate(_freezeEffectPrefab, hitPoint, Quaternion.identity);
            iceEffect.transform.SetParent(target.transform);
            Destroy(iceEffect, _freezeDuration);
        }
        
        freezableObj.isCurrentlyFrozen = true;
        freezableObj.freezeEndTime = Time.time + _freezeDuration;
        
        _frozenObjects.Add(freezableObj);
        OnTargetFrozen?.Invoke(target);
    }
    
    void UnfreezeObject(FreezableObject frozenObj)
    {
        if (frozenObj.rigidBody == null) return;
        
        // Restore physics
        frozenObj.rigidBody.isKinematic = frozenObj.wasKinematic;
        
        if (!frozenObj.wasKinematic)
        {
            frozenObj.rigidBody.velocity = frozenObj.originalVelocity * 0.5f; // Reduced momentum
            frozenObj.rigidBody.angularVelocity = frozenObj.originalAngularVelocity * 0.5f;
        }
        
        // Restore visual
        if (frozenObj.objectRenderer != null && frozenObj.originalMaterial != null)
        {
            frozenObj.objectRenderer.material = frozenObj.originalMaterial;
        }
        
        frozenObj.isCurrentlyFrozen = false;
    }
    
    void StartCooldown()
    {
        _isOnCooldown = true;
        _cooldownTimer = _cooldownTime;
    }
    
    System.Collections.IEnumerator ShowIceBeam(Vector3 start, Vector3 end)
    {
        if (_iceBeam == null) yield break;
        
        _iceBeam.enabled = true;
        _iceBeam.positionCount = 2;
        _iceBeam.SetPosition(0, start);
        _iceBeam.SetPosition(1, end);
        
        yield return new WaitForSeconds(_beamDuration);
        
        _iceBeam.enabled = false;
    }
    
    public bool IsOnCooldown()
    {
        return _isOnCooldown;
    }
    
    public float GetCooldownProgress()
    {
        if (!_isOnCooldown) return 1f;
        return 1f - (_cooldownTimer / _cooldownTime);
    }
    
    public void SetFrozenMaterial(Material frozenMat)
    {
        foreach (var frozen in _frozenObjects)
        {
            frozen.frozenMaterial = frozenMat;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (_playerCamera != null)
        {
            Gizmos.color = Color.cyan;
            Vector3 forward = _playerCamera.transform.forward;
            Gizmos.DrawRay(_playerCamera.transform.position, forward * _freezeRange);
        }
    }
}