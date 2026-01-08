// Prompt: ice floor that reduces friction
// Type: general

using UnityEngine;

public class IceFloor : MonoBehaviour
{
    [Header("Ice Properties")]
    [SerializeField] private float _frictionReduction = 0.1f;
    [SerializeField] private float _slidingForce = 5f;
    [SerializeField] private bool _maintainMomentum = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _iceParticles;
    [SerializeField] private AudioClip _slideSound;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Physics Materials")]
    [SerializeField] private PhysicMaterial _icyMaterial;
    [SerializeField] private PhysicMaterial _originalMaterial;
    
    private Collider _floorCollider;
    
    private void Start()
    {
        _floorCollider = GetComponent<Collider>();
        
        if (_floorCollider == null)
        {
            Debug.LogError("IceFloor requires a Collider component!");
            return;
        }
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        SetupIcyPhysicsMaterial();
    }
    
    private void SetupIcyPhysicsMaterial()
    {
        if (_icyMaterial == null)
        {
            _icyMaterial = new PhysicMaterial("IcyMaterial");
            _icyMaterial.dynamicFriction = _frictionReduction;
            _icyMaterial.staticFriction = _frictionReduction;
            _icyMaterial.bounciness = 0f;
            _icyMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
            _icyMaterial.bounceCombine = PhysicMaterialCombine.Minimum;
        }
        
        _floorCollider.material = _icyMaterial;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ApplyIceEffect(other);
            PlayIceEffects();
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (other.CompareTag("Player") && _maintainMomentum)
        {
            Rigidbody rb = other.GetComponent<Rigidbody>();
            if (rb != null && rb.velocity.magnitude > 0.1f)
            {
                Vector3 slideDirection = rb.velocity.normalized;
                rb.AddForce(slideDirection * _slidingForce, ForceMode.Acceleration);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            RestoreNormalFriction(other);
            StopIceEffects();
        }
    }
    
    private void ApplyIceEffect(Collider player)
    {
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.drag = _frictionReduction;
        }
        
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null)
        {
            if (_originalMaterial == null)
                _originalMaterial = playerCollider.material;
            
            playerCollider.material = _icyMaterial;
        }
    }
    
    private void RestoreNormalFriction(Collider player)
    {
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.drag = 0f;
        }
        
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null && _originalMaterial != null)
        {
            playerCollider.material = _originalMaterial;
        }
    }
    
    private void PlayIceEffects()
    {
        if (_iceParticles != null && !_iceParticles.isPlaying)
        {
            _iceParticles.Play();
        }
        
        if (_audioSource != null && _slideSound != null)
        {
            _audioSource.clip = _slideSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void StopIceEffects()
    {
        if (_iceParticles != null && _iceParticles.isPlaying)
        {
            _iceParticles.Stop();
        }
        
        if (_audioSource != null && _audioSource.isPlaying)
        {
            _audioSource.Stop();
        }
    }
    
    private void OnValidate()
    {
        _frictionReduction = Mathf.Clamp01(_frictionReduction);
        _slidingForce = Mathf.Max(0f, _slidingForce);
    }
}