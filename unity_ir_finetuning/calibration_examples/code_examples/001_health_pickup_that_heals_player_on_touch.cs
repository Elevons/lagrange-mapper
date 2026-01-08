// Prompt: health pickup that heals player on touch
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class HealthPickup : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField] private int _healAmount = 25;
    [SerializeField] private int _maxHealth = 100;
    
    [Header("Pickup Settings")]
    [SerializeField] private bool _destroyOnPickup = true;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    
    [Header("Effects")]
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private GameObject _pickupEffect;
    [SerializeField] private float _effectDuration = 2f;
    
    [Header("Events")]
    [SerializeField] private UnityEvent<int> _onHealthPickedUp;
    
    private Vector3 _startPosition;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null && _pickupSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
    }
    
    private void Update()
    {
        AnimatePickup();
    }
    
    private void AnimatePickup()
    {
        if (_rotationSpeed > 0)
        {
            transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        }
        
        if (_bobHeight > 0 && _bobSpeed > 0)
        {
            float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            HealPlayer(other.gameObject);
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            HealPlayer(collision.gameObject);
        }
    }
    
    private void HealPlayer(GameObject player)
    {
        PlayerHealthComponent playerHealth = player.GetComponent<PlayerHealthComponent>();
        
        if (playerHealth == null)
        {
            playerHealth = player.GetComponentInChildren<PlayerHealthComponent>();
        }
        
        if (playerHealth == null)
        {
            playerHealth = player.AddComponent<PlayerHealthComponent>();
            playerHealth.Initialize(_maxHealth);
        }
        
        int actualHealAmount = playerHealth.Heal(_healAmount);
        
        if (actualHealAmount > 0)
        {
            PlayPickupEffects();
            _onHealthPickedUp?.Invoke(actualHealAmount);
            
            if (_destroyOnPickup)
            {
                if (_audioSource != null && _pickupSound != null)
                {
                    _audioSource.PlayOneShot(_pickupSound);
                    StartCoroutine(DestroyAfterSound());
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }
    
    private void PlayPickupEffects()
    {
        if (_pickupEffect != null)
        {
            GameObject effect = Instantiate(_pickupEffect, transform.position, transform.rotation);
            Destroy(effect, _effectDuration);
        }
        
        if (_audioSource != null && _pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
    }
    
    private System.Collections.IEnumerator DestroyAfterSound()
    {
        GetComponent<Collider>().enabled = false;
        GetComponent<Renderer>().enabled = false;
        
        yield return new WaitForSeconds(_pickupSound.length);
        Destroy(gameObject);
    }
    
    [System.Serializable]
    public class PlayerHealthComponent : MonoBehaviour
    {
        [SerializeField] private int _currentHealth;
        [SerializeField] private int _maxHealth = 100;
        
        public int CurrentHealth => _currentHealth;
        public int MaxHealth => _maxHealth;
        public bool IsFullHealth => _currentHealth >= _maxHealth;
        
        private void Start()
        {
            if (_currentHealth <= 0)
            {
                _currentHealth = _maxHealth;
            }
        }
        
        public void Initialize(int maxHealth)
        {
            _maxHealth = maxHealth;
            _currentHealth = maxHealth;
        }
        
        public int Heal(int amount)
        {
            if (amount <= 0 || IsFullHealth) return 0;
            
            int previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(_currentHealth + amount, _maxHealth);
            
            return _currentHealth - previousHealth;
        }
        
        public void TakeDamage(int amount)
        {
            if (amount <= 0) return;
            
            _currentHealth = Mathf.Max(_currentHealth - amount, 0);
        }
        
        public void SetHealth(int health)
        {
            _currentHealth = Mathf.Clamp(health, 0, _maxHealth);
        }
    }
}