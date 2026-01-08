// Prompt: slippery slope
// Type: general

using UnityEngine;

public class SlipperySlope : MonoBehaviour
{
    [Header("Slippery Surface Settings")]
    [SerializeField] private float _slipperiness = 0.95f;
    [SerializeField] private float _slopeForce = 10f;
    [SerializeField] private float _maxSlideSpeed = 15f;
    [SerializeField] private bool _affectsPlayer = true;
    [SerializeField] private bool _affectsObjects = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _slideParticles;
    [SerializeField] private AudioSource _slideAudioSource;
    [SerializeField] private AudioClip _slideSound;
    
    [Header("Physics")]
    [SerializeField] private PhysicMaterial _slipperyMaterial;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    private Collider _surfaceCollider;
    private Vector3 _slopeDirection;
    private float _slopeAngle;
    
    private void Start()
    {
        _surfaceCollider = GetComponent<Collider>();
        if (_surfaceCollider == null)
        {
            Debug.LogError("SlipperySlope requires a Collider component!");
            return;
        }
        
        CalculateSlopeDirection();
        SetupSlipperyMaterial();
        
        if (_slideAudioSource == null)
        {
            _slideAudioSource = GetComponent<AudioSource>();
        }
    }
    
    private void CalculateSlopeDirection()
    {
        Vector3 forward = transform.forward;
        Vector3 up = transform.up;
        
        _slopeDirection = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
        _slopeAngle = Vector3.Angle(up, Vector3.up);
        
        if (_slopeAngle < 1f)
        {
            _slopeDirection = -transform.up;
        }
    }
    
    private void SetupSlipperyMaterial()
    {
        if (_slipperyMaterial == null)
        {
            _slipperyMaterial = new PhysicMaterial("SlipperyMaterial");
            _slipperyMaterial.dynamicFriction = 0.1f;
            _slipperyMaterial.staticFriction = 0.1f;
            _slipperyMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
        }
        
        if (_surfaceCollider != null)
        {
            _surfaceCollider.material = _slipperyMaterial;
        }
    }
    
    private void OnTriggerStay(Collider other)
    {
        if (!IsAffectedObject(other)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null) return;
        
        ApplySlipperyEffect(rb, other);
    }
    
    private void OnCollisionStay(Collision collision)
    {
        if (!IsAffectedObject(collision.collider)) return;
        
        Rigidbody rb = collision.rigidbody;
        if (rb == null) return;
        
        ApplySlipperyEffect(rb, collision.collider);
    }
    
    private bool IsAffectedObject(Collider other)
    {
        if ((_affectedLayers.value & (1 << other.gameObject.layer)) == 0)
            return false;
        
        if (other.CompareTag("Player") && !_affectsPlayer)
            return false;
        
        if (!other.CompareTag("Player") && !_affectsObjects)
            return false;
        
        return true;
    }
    
    private void ApplySlipperyEffect(Rigidbody rb, Collider other)
    {
        Vector3 slopeForceVector = _slopeDirection * _slopeForce;
        Vector3 currentVelocity = rb.velocity;
        
        Vector3 slideVelocity = Vector3.Lerp(currentVelocity, 
            currentVelocity + slopeForceVector, _slipperiness * Time.fixedDeltaTime);
        
        slideVelocity = Vector3.ClampMagnitude(slideVelocity, _maxSlideSpeed);
        
        rb.velocity = slideVelocity;
        
        if (other.CompareTag("Player"))
        {
            DisablePlayerControl(other);
        }
        
        PlaySlideEffects(other.transform.position);
    }
    
    private void DisablePlayerControl(Collider playerCollider)
    {
        CharacterController characterController = playerCollider.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            StartCoroutine(ReenableCharacterController(characterController));
        }
    }
    
    private System.Collections.IEnumerator ReenableCharacterController(CharacterController controller)
    {
        yield return new WaitForSeconds(0.1f);
        if (controller != null)
        {
            controller.enabled = true;
        }
    }
    
    private void PlaySlideEffects(Vector3 position)
    {
        if (_slideParticles != null && !_slideParticles.isPlaying)
        {
            _slideParticles.transform.position = position;
            _slideParticles.Play();
        }
        
        if (_slideAudioSource != null && _slideSound != null && !_slideAudioSource.isPlaying)
        {
            _slideAudioSource.clip = _slideSound;
            _slideAudioSource.Play();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            RestorePlayerControl(other);
        }
        
        StopSlideEffects();
    }
    
    private void OnCollisionExit(Collision collision)
    {
        if (collision.collider.CompareTag("Player"))
        {
            RestorePlayerControl(collision.collider);
        }
        
        StopSlideEffects();
    }
    
    private void RestorePlayerControl(Collider playerCollider)
    {
        CharacterController characterController = playerCollider.GetComponent<CharacterController>();
        if (characterController != null && !characterController.enabled)
        {
            characterController.enabled = true;
        }
    }
    
    private void StopSlideEffects()
    {
        if (_slideParticles != null && _slideParticles.isPlaying)
        {
            _slideParticles.Stop();
        }
        
        if (_slideAudioSource != null && _slideAudioSource.isPlaying)
        {
            _slideAudioSource.Stop();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(transform.position, _slopeDirection * 3f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
    
    public void SetSlipperiness(float value)
    {
        _slipperiness = Mathf.Clamp01(value);
    }
    
    public void SetSlopeForce(float value)
    {
        _slopeForce = Mathf.Max(0f, value);
    }
}