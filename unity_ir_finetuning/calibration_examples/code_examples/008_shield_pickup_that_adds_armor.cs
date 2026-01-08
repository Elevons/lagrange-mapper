// Prompt: shield pickup that adds armor
// Type: pickup

using UnityEngine;
using UnityEngine.Events;

public class ShieldPickup : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private int _armorAmount = 25;
    [SerializeField] private int _maxArmor = 100;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _pickupEffect;
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _bobHeight = 0.5f;
    
    [Header("Events")]
    [SerializeField] private UnityEvent<int> _onArmorAdded;
    
    private AudioSource _audioSource;
    private Vector3 _startPosition;
    private bool _isPickedUp = false;
    
    [System.Serializable]
    public class PlayerArmor
    {
        public int currentArmor;
        public int maxArmor;
        
        public PlayerArmor(int maxArmor)
        {
            this.maxArmor = maxArmor;
            this.currentArmor = 0;
        }
        
        public int AddArmor(int amount)
        {
            int oldArmor = currentArmor;
            currentArmor = Mathf.Clamp(currentArmor + amount, 0, maxArmor);
            return currentArmor - oldArmor;
        }
    }
    
    void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }
    
    void Update()
    {
        if (_isPickedUp) return;
        
        AnimatePickup();
    }
    
    void AnimatePickup()
    {
        transform.Rotate(0, _rotationSpeed * Time.deltaTime, 0);
        
        float newY = _startPosition.y + Mathf.Sin(Time.time * _bobSpeed) * _bobHeight;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (_isPickedUp) return;
        
        if (other.CompareTag("Player"))
        {
            PlayerArmor playerArmor = other.GetComponent<PlayerArmor>();
            if (playerArmor == null)
            {
                playerArmor = other.gameObject.AddComponent<PlayerArmorComponent>().armor;
            }
            
            int armorAdded = playerArmor.AddArmor(_armorAmount);
            
            if (armorAdded > 0)
            {
                PickupShield(armorAdded);
            }
        }
    }
    
    void PickupShield(int armorAdded)
    {
        _isPickedUp = true;
        
        if (_pickupEffect != null)
        {
            _pickupEffect.Play();
        }
        
        if (_pickupSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        _onArmorAdded?.Invoke(armorAdded);
        
        GetComponent<Collider>().enabled = false;
        GetComponent<Renderer>().enabled = false;
        
        Destroy(gameObject, _pickupSound != null ? _pickupSound.length : 0.1f);
    }
    
    public class PlayerArmorComponent : MonoBehaviour
    {
        public PlayerArmor armor;
        
        void Awake()
        {
            armor = new PlayerArmor(100);
        }
        
        public int GetCurrentArmor()
        {
            return armor.currentArmor;
        }
        
        public int GetMaxArmor()
        {
            return armor.maxArmor;
        }
        
        public void TakeDamage(int damage)
        {
            armor.currentArmor = Mathf.Max(0, armor.currentArmor - damage);
        }
    }
}