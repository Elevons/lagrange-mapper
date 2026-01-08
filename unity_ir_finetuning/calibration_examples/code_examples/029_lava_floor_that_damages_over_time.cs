// Prompt: lava floor that damages over time
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class LavaFloor : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private bool _continuousDamage = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _lavaParticles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _sizzleSound;
    [SerializeField] private AudioClip _damageSound;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnPlayerEnterLava;
    public UnityEvent<GameObject> OnPlayerExitLava;
    public UnityEvent<GameObject, float> OnPlayerDamaged;
    
    private bool _playerInLava = false;
    private float _damageTimer = 0f;
    private GameObject _currentPlayer;
    
    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_lavaParticles == null)
            _lavaParticles = GetComponentInChildren<ParticleSystem>();
    }
    
    private void Update()
    {
        if (_playerInLava && _continuousDamage)
        {
            _damageTimer += Time.deltaTime;
            
            if (_damageTimer >= _damageInterval)
            {
                DealDamage();
                _damageTimer = 0f;
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInLava = true;
            _currentPlayer = other.gameObject;
            _damageTimer = 0f;
            
            OnPlayerEnterLava?.Invoke(_currentPlayer);
            
            if (_audioSource && _sizzleSound)
            {
                _audioSource.clip = _sizzleSound;
                _audioSource.loop = true;
                _audioSource.Play();
            }
            
            if (_lavaParticles)
                _lavaParticles.Play();
                
            if (!_continuousDamage)
                DealDamage();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && other.gameObject == _currentPlayer)
        {
            _playerInLava = false;
            _currentPlayer = null;
            _damageTimer = 0f;
            
            OnPlayerExitLava?.Invoke(other.gameObject);
            
            if (_audioSource)
                _audioSource.Stop();
                
            if (_lavaParticles)
                _lavaParticles.Stop();
        }
    }
    
    private void DealDamage()
    {
        if (_currentPlayer == null) return;
        
        var playerHealth = _currentPlayer.GetComponent<CharacterController>();
        if (playerHealth != null)
        {
            OnPlayerDamaged?.Invoke(_currentPlayer, _damageAmount);
            
            if (_audioSource && _damageSound)
            {
                _audioSource.PlayOneShot(_damageSound);
            }
        }
    }
    
    public void SetDamageAmount(float damage)
    {
        _damageAmount = Mathf.Max(0f, damage);
    }
    
    public void SetDamageInterval(float interval)
    {
        _damageInterval = Mathf.Max(0.1f, interval);
    }
    
    public void EnableLava(bool enable)
    {
        GetComponent<Collider>().enabled = enable;
        
        if (!enable && _playerInLava)
        {
            _playerInLava = false;
            _currentPlayer = null;
            
            if (_audioSource)
                _audioSource.Stop();
                
            if (_lavaParticles)
                _lavaParticles.Stop();
        }
    }
}