// Prompt: bounce pad that launches player
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class BouncePad : MonoBehaviour
{
    [Header("Bounce Settings")]
    [SerializeField] private float _bounceForce = 20f;
    [SerializeField] private Vector3 _bounceDirection = Vector3.up;
    [SerializeField] private bool _useLocalDirection = true;
    [SerializeField] private bool _overrideVelocity = true;
    
    [Header("Animation")]
    [SerializeField] private float _animationDuration = 0.3f;
    [SerializeField] private float _compressionAmount = 0.2f;
    [SerializeField] private AnimationCurve _animationCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Audio")]
    [SerializeField] private AudioClip _bounceSound;
    [SerializeField] private float _volume = 1f;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _bounceEffect;
    [SerializeField] private GameObject _bounceEffectPrefab;
    
    [Header("Events")]
    public UnityEvent OnPlayerBounce;
    
    private Vector3 _originalScale;
    private bool _isAnimating = false;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _originalScale = transform.localScale;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _bounceSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        _bounceDirection = _bounceDirection.normalized;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            LaunchPlayer(other);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            LaunchPlayer(collision.collider);
        }
    }
    
    private void LaunchPlayer(Collider player)
    {
        Rigidbody playerRigidbody = player.GetComponent<Rigidbody>();
        
        if (playerRigidbody == null)
        {
            playerRigidbody = player.GetComponentInParent<Rigidbody>();
        }
        
        if (playerRigidbody != null)
        {
            Vector3 launchDirection = _useLocalDirection ? transform.TransformDirection(_bounceDirection) : _bounceDirection;
            
            if (_overrideVelocity)
            {
                playerRigidbody.velocity = Vector3.zero;
            }
            
            playerRigidbody.AddForce(launchDirection * _bounceForce, ForceMode.VelocityChange);
            
            PlayBounceEffects();
            OnPlayerBounce?.Invoke();
        }
    }
    
    private void PlayBounceEffects()
    {
        if (!_isAnimating)
        {
            StartCoroutine(PlayBounceAnimation());
        }
        
        if (_audioSource != null && _bounceSound != null)
        {
            _audioSource.PlayOneShot(_bounceSound, _volume);
        }
        
        if (_bounceEffect != null)
        {
            _bounceEffect.Play();
        }
        
        if (_bounceEffectPrefab != null)
        {
            GameObject effect = Instantiate(_bounceEffectPrefab, transform.position, transform.rotation);
            Destroy(effect, 5f);
        }
    }
    
    private System.Collections.IEnumerator PlayBounceAnimation()
    {
        _isAnimating = true;
        float elapsedTime = 0f;
        
        while (elapsedTime < _animationDuration)
        {
            float normalizedTime = elapsedTime / _animationDuration;
            float curveValue = _animationCurve.Evaluate(normalizedTime);
            
            float compressionScale = 1f - (_compressionAmount * curveValue);
            Vector3 newScale = new Vector3(
                _originalScale.x * (1f + (1f - compressionScale) * 0.5f),
                _originalScale.y * compressionScale,
                _originalScale.z * (1f + (1f - compressionScale) * 0.5f)
            );
            
            transform.localScale = newScale;
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        transform.localScale = _originalScale;
        _isAnimating = false;
    }
    
    private void OnDrawGizmosSelected()
    {
        Vector3 direction = _useLocalDirection ? transform.TransformDirection(_bounceDirection) : _bounceDirection;
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + direction * (_bounceForce * 0.1f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(startPos, endPos);
        Gizmos.DrawSphere(endPos, 0.2f);
        
        Gizmos.color = Color.green;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
    }
}