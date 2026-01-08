// Prompt: teleporter between two locations
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Teleporter : MonoBehaviour
{
    [Header("Teleporter Settings")]
    [SerializeField] private Transform _destinationPoint;
    [SerializeField] private bool _isTwoWay = true;
    [SerializeField] private float _teleportCooldown = 2f;
    [SerializeField] private LayerMask _teleportLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _teleportEffect;
    [SerializeField] private AudioClip _teleportSound;
    [SerializeField] private float _effectDuration = 1f;
    
    [Header("Animation")]
    [SerializeField] private bool _useScaleAnimation = true;
    [SerializeField] private AnimationCurve _scaleCurve = AnimationCurve.EaseInOut(0, 1, 1, 0);
    [SerializeField] private float _animationDuration = 0.5f;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectTeleported;
    public UnityEvent OnTeleportActivated;
    
    private AudioSource _audioSource;
    private bool _canTeleport = true;
    private Teleporter _linkedTeleporter;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _teleportSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        FindLinkedTeleporter();
        
        if (_destinationPoint == null)
        {
            Debug.LogWarning($"Teleporter {gameObject.name} has no destination point assigned!");
        }
    }
    
    private void FindLinkedTeleporter()
    {
        if (_destinationPoint != null)
        {
            _linkedTeleporter = _destinationPoint.GetComponent<Teleporter>();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_canTeleport || _destinationPoint == null)
            return;
            
        if (!IsValidTeleportTarget(other))
            return;
            
        StartCoroutine(TeleportSequence(other.gameObject));
    }
    
    private bool IsValidTeleportTarget(Collider other)
    {
        int objectLayer = 1 << other.gameObject.layer;
        return (_teleportLayers.value & objectLayer) != 0;
    }
    
    private System.Collections.IEnumerator TeleportSequence(GameObject target)
    {
        _canTeleport = false;
        
        if (_linkedTeleporter != null)
        {
            _linkedTeleporter._canTeleport = false;
        }
        
        OnTeleportActivated.Invoke();
        
        if (_useScaleAnimation)
        {
            yield return StartCoroutine(AnimateScale(target, true));
        }
        
        if (_teleportEffect != null)
        {
            _teleportEffect.Play();
        }
        
        if (_audioSource != null && _teleportSound != null)
        {
            _audioSource.PlayOneShot(_teleportSound);
        }
        
        yield return new WaitForSeconds(_effectDuration * 0.5f);
        
        PerformTeleport(target);
        
        OnObjectTeleported.Invoke(target);
        
        yield return new WaitForSeconds(_effectDuration * 0.5f);
        
        if (_useScaleAnimation)
        {
            yield return StartCoroutine(AnimateScale(target, false));
        }
        
        yield return new WaitForSeconds(_teleportCooldown);
        
        _canTeleport = true;
        
        if (_linkedTeleporter != null)
        {
            _linkedTeleporter._canTeleport = true;
        }
    }
    
    private void PerformTeleport(GameObject target)
    {
        CharacterController characterController = target.GetComponent<CharacterController>();
        Rigidbody targetRigidbody = target.GetComponent<Rigidbody>();
        
        if (characterController != null)
        {
            characterController.enabled = false;
            target.transform.position = _destinationPoint.position;
            target.transform.rotation = _destinationPoint.rotation;
            characterController.enabled = true;
        }
        else
        {
            target.transform.position = _destinationPoint.position;
            target.transform.rotation = _destinationPoint.rotation;
        }
        
        if (targetRigidbody != null)
        {
            targetRigidbody.velocity = Vector3.zero;
            targetRigidbody.angularVelocity = Vector3.zero;
        }
    }
    
    private System.Collections.IEnumerator AnimateScale(GameObject target, bool scaleDown)
    {
        Vector3 originalScale = target.transform.localScale;
        Vector3 targetScale = scaleDown ? Vector3.zero : originalScale;
        Vector3 startScale = scaleDown ? originalScale : Vector3.zero;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / _animationDuration;
            float curveValue = _scaleCurve.Evaluate(normalizedTime);
            
            target.transform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);
            
            yield return null;
        }
        
        target.transform.localScale = targetScale;
    }
    
    public void SetDestination(Transform newDestination)
    {
        _destinationPoint = newDestination;
        FindLinkedTeleporter();
    }
    
    public void ActivateTeleporter()
    {
        _canTeleport = true;
    }
    
    public void DeactivateTeleporter()
    {
        _canTeleport = false;
    }
    
    public void TeleportObject(GameObject target)
    {
        if (_canTeleport && _destinationPoint != null && IsValidTeleportTarget(target.GetComponent<Collider>()))
        {
            StartCoroutine(TeleportSequence(target));
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_destinationPoint != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, _destinationPoint.position);
            
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_destinationPoint.position, 1f);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}