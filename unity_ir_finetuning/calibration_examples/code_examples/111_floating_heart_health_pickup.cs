// Prompt: floating heart health pickup
// Type: pickup

using UnityEngine;

public class FloatingHeartPickup : MonoBehaviour
{
    [Header("Pickup Settings")]
    [SerializeField] private int _healthAmount = 20;
    [SerializeField] private float _pickupRange = 2f;
    [SerializeField] private bool _destroyOnPickup = true;
    
    [Header("Floating Animation")]
    [SerializeField] private float _floatAmplitude = 0.5f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 90f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private float _scaleOnPickup = 1.2f;
    [SerializeField] private float _pickupAnimationDuration = 0.3f;
    
    [Header("Magnet Effect")]
    [SerializeField] private bool _enableMagnetEffect = true;
    [SerializeField] private float _magnetRange = 5f;
    [SerializeField] private float _magnetSpeed = 8f;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    private bool _isPickedUp = false;
    private Transform _playerTransform;
    private bool _isMagneting = false;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }
    
    private void Update()
    {
        if (_isPickedUp) return;
        
        HandleFloatingAnimation();
        HandleMagnetEffect();
        CheckForPlayerPickup();
    }
    
    private void HandleFloatingAnimation()
    {
        if (!_isMagneting)
        {
            float newY = _startPosition.y + Mathf.Sin(Time.time * _floatSpeed) * _floatAmplitude;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
        
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
    }
    
    private void HandleMagnetEffect()
    {
        if (!_enableMagnetEffect || _playerTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        
        if (distanceToPlayer <= _magnetRange && distanceToPlayer > _pickupRange)
        {
            _isMagneting = true;
            Vector3 direction = (_playerTransform.position - transform.position).normalized;
            transform.position = Vector3.MoveTowards(transform.position, _playerTransform.position, _magnetSpeed * Time.deltaTime);
        }
        else if (distanceToPlayer > _magnetRange)
        {
            _isMagneting = false;
        }
    }
    
    private void CheckForPlayerPickup()
    {
        if (_playerTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        
        if (distanceToPlayer <= _pickupRange)
        {
            PickupHeart();
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        
        if (other.CompareTag("Player"))
        {
            PickupHeart();
        }
    }
    
    private void PickupHeart()
    {
        if (_isPickedUp) return;
        
        _isPickedUp = true;
        
        // Try to heal the player
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Try different common health component patterns
            var healthComponent = player.GetComponent<MonoBehaviour>();
            if (healthComponent != null)
            {
                // Send message to any health-related methods that might exist
                player.SendMessage("Heal", _healthAmount, SendMessageOptions.DontRequireReceiver);
                player.SendMessage("AddHealth", _healthAmount, SendMessageOptions.DontRequireReceiver);
                player.SendMessage("RestoreHealth", _healthAmount, SendMessageOptions.DontRequireReceiver);
            }
        }
        
        // Play pickup effects
        PlayPickupEffects();
        
        // Handle pickup animation or immediate destruction
        if (_destroyOnPickup)
        {
            StartCoroutine(PickupAnimation());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
    
    private void PlayPickupEffects()
    {
        if (_pickupEffect != null)
        {
            _pickupEffect.Play();
        }
        
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
    }
    
    private System.Collections.IEnumerator PickupAnimation()
    {
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * _scaleOnPickup;
        
        float elapsedTime = 0f;
        
        while (elapsedTime < _pickupAnimationDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / _pickupAnimationDuration;
            
            // Scale up then down
            float scaleMultiplier = progress < 0.5f ? 
                Mathf.Lerp(1f, _scaleOnPickup, progress * 2f) : 
                Mathf.Lerp(_scaleOnPickup, 0f, (progress - 0.5f) * 2f);
            
            transform.localScale = originalScale * scaleMultiplier;
            
            // Fade out if renderer exists
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null && renderer.material.HasProperty("_Color"))
            {
                Color color = renderer.material.color;
                color.a = 1f - progress;
                renderer.material.color = color;
            }
            
            yield return null;
        }
        
        // Wait for sound to finish if playing
        if (_audioSource != null && _audioSource.isPlaying)
        {
            yield return new WaitWhile(() => _audioSource.isPlaying);
        }
        
        Destroy(gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw pickup range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _pickupRange);
        
        // Draw magnet range
        if (_enableMagnetEffect)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _magnetRange);
        }
    }
}