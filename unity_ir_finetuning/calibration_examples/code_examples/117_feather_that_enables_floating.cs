// Prompt: feather that enables floating
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class FloatingFeather : MonoBehaviour
{
    [Header("Feather Settings")]
    [SerializeField] private float _floatDuration = 5f;
    [SerializeField] private float _floatForce = 10f;
    [SerializeField] private float _pickupRange = 2f;
    [SerializeField] private bool _consumeOnUse = true;
    
    [Header("Visual Effects")]
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    [SerializeField] private float _rotationSpeed = 45f;
    [SerializeField] private ParticleSystem _pickupEffect;
    [SerializeField] private ParticleSystem _floatEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private AudioClip _activateSound;
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Events")]
    public UnityEvent OnFeatherPickedUp;
    public UnityEvent OnFloatActivated;
    public UnityEvent OnFloatDeactivated;
    
    private Vector3 _startPosition;
    private bool _isPickedUp = false;
    private GameObject _currentPlayer;
    private FloatingController _floatingController;
    
    [System.Serializable]
    private class FloatingController
    {
        public GameObject player;
        public Rigidbody playerRigidbody;
        public float originalDrag;
        public float floatTimeRemaining;
        public bool isFloating;
        
        public FloatingController(GameObject playerObj, float duration)
        {
            player = playerObj;
            playerRigidbody = playerObj.GetComponent<Rigidbody>();
            floatTimeRemaining = duration;
            isFloating = false;
            
            if (playerRigidbody != null)
            {
                originalDrag = playerRigidbody.drag;
            }
        }
    }
    
    private void Start()
    {
        _startPosition = transform.position;
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.isTrigger = true;
        }
    }
    
    private void Update()
    {
        if (!_isPickedUp)
        {
            HandleFeatherAnimation();
            CheckForPlayerInRange();
        }
        else if (_floatingController != null && _floatingController.isFloating)
        {
            UpdateFloating();
        }
    }
    
    private void HandleFeatherAnimation()
    {
        float bobOffset = Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = _startPosition + Vector3.up * bobOffset;
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }
    
    private void CheckForPlayerInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _pickupRange);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                PickupFeather(col.gameObject);
                break;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isPickedUp && other.CompareTag("Player"))
        {
            PickupFeather(other.gameObject);
        }
    }
    
    private void PickupFeather(GameObject player)
    {
        _isPickedUp = true;
        _currentPlayer = player;
        
        PlaySound(_pickupSound);
        PlayEffect(_pickupEffect);
        
        GetComponent<Renderer>().enabled = false;
        GetComponent<Collider>().enabled = false;
        
        OnFeatherPickedUp?.Invoke();
        
        ActivateFloating();
    }
    
    private void ActivateFloating()
    {
        if (_currentPlayer == null) return;
        
        _floatingController = new FloatingController(_currentPlayer, _floatDuration);
        
        if (_floatingController.playerRigidbody != null)
        {
            _floatingController.playerRigidbody.drag = 5f;
            _floatingController.isFloating = true;
            
            PlaySound(_activateSound);
            PlayEffect(_floatEffect);
            
            OnFloatActivated?.Invoke();
        }
    }
    
    private void UpdateFloating()
    {
        _floatingController.floatTimeRemaining -= Time.deltaTime;
        
        if (_floatingController.playerRigidbody != null)
        {
            Vector3 floatForceVector = Vector3.up * _floatForce;
            _floatingController.playerRigidbody.AddForce(floatForceVector, ForceMode.Force);
            
            if (Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.LeftShift))
            {
                _floatingController.playerRigidbody.AddForce(floatForceVector * 0.5f, ForceMode.Force);
            }
        }
        
        if (_floatingController.floatTimeRemaining <= 0f)
        {
            DeactivateFloating();
        }
    }
    
    private void DeactivateFloating()
    {
        if (_floatingController?.playerRigidbody != null)
        {
            _floatingController.playerRigidbody.drag = _floatingController.originalDrag;
            _floatingController.isFloating = false;
        }
        
        if (_floatEffect != null && _floatEffect.isPlaying)
        {
            _floatEffect.Stop();
        }
        
        OnFloatDeactivated?.Invoke();
        
        if (_consumeOnUse)
        {
            Destroy(gameObject);
        }
        else
        {
            ResetFeather();
        }
    }
    
    private void ResetFeather()
    {
        _isPickedUp = false;
        _currentPlayer = null;
        _floatingController = null;
        
        GetComponent<Renderer>().enabled = true;
        GetComponent<Collider>().enabled = true;
        
        transform.position = _startPosition;
    }
    
    private void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void PlayEffect(ParticleSystem effect)
    {
        if (effect != null)
        {
            effect.Play();
        }
    }
    
    public float GetRemainingFloatTime()
    {
        return _floatingController?.floatTimeRemaining ?? 0f;
    }
    
    public bool IsFloating()
    {
        return _floatingController?.isFloating ?? false;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _pickupRange);
    }
}