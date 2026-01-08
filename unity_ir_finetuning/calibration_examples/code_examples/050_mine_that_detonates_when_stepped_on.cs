// Prompt: mine that detonates when stepped on
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class LandMine : MonoBehaviour
{
    [Header("Mine Settings")]
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private float _explosionForce = 1000f;
    [SerializeField] private float _damage = 100f;
    [SerializeField] private float _armingDelay = 1f;
    [SerializeField] private LayerMask _triggerLayers = -1;
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _explosionPrefab;
    [SerializeField] private GameObject _armedIndicator;
    [SerializeField] private AudioClip _armingSound;
    [SerializeField] private AudioClip _explosionSound;
    [SerializeField] private float _blinkRate = 2f;
    
    [Header("Events")]
    public UnityEvent OnMineArmed;
    public UnityEvent OnMineTriggered;
    public UnityEvent<float> OnDamageDealt;
    
    private bool _isArmed = false;
    private bool _hasExploded = false;
    private AudioSource _audioSource;
    private Collider _triggerCollider;
    private Renderer _indicatorRenderer;
    private float _blinkTimer = 0f;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)_triggerCollider).radius = 0.5f;
        }
        _triggerCollider.isTrigger = true;
        
        if (_armedIndicator != null)
        {
            _indicatorRenderer = _armedIndicator.GetComponent<Renderer>();
            _armedIndicator.SetActive(false);
        }
        
        StartCoroutine(ArmMineAfterDelay());
    }
    
    private void Update()
    {
        if (_isArmed && !_hasExploded && _indicatorRenderer != null)
        {
            _blinkTimer += Time.deltaTime;
            if (_blinkTimer >= 1f / _blinkRate)
            {
                _blinkTimer = 0f;
                _indicatorRenderer.enabled = !_indicatorRenderer.enabled;
            }
        }
    }
    
    private System.Collections.IEnumerator ArmMineAfterDelay()
    {
        yield return new WaitForSeconds(_armingDelay);
        ArmMine();
    }
    
    private void ArmMine()
    {
        if (_hasExploded) return;
        
        _isArmed = true;
        
        if (_armedIndicator != null)
        {
            _armedIndicator.SetActive(true);
        }
        
        if (_armingSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_armingSound);
        }
        
        OnMineArmed?.Invoke();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isArmed || _hasExploded) return;
        
        if (IsValidTrigger(other))
        {
            TriggerExplosion();
        }
    }
    
    private bool IsValidTrigger(Collider other)
    {
        return (_triggerLayers.value & (1 << other.gameObject.layer)) != 0;
    }
    
    private void TriggerExplosion()
    {
        if (_hasExploded) return;
        
        _hasExploded = true;
        OnMineTriggered?.Invoke();
        
        if (_explosionSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_explosionSound);
        }
        
        if (_explosionPrefab != null)
        {
            Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
        }
        
        ApplyExplosionEffects();
        
        if (_armedIndicator != null)
        {
            _armedIndicator.SetActive(false);
        }
        
        StartCoroutine(DestroyAfterExplosion());
    }
    
    private void ApplyExplosionEffects()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _explosionRadius);
        
        foreach (Collider hit in colliders)
        {
            if (hit == _triggerCollider) continue;
            
            Rigidbody rb = hit.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(_explosionForce, transform.position, _explosionRadius);
            }
            
            if (hit.CompareTag("Player"))
            {
                ApplyDamageToPlayer(hit.gameObject);
            }
            
            IDamageable damageable = hit.GetComponent<IDamageable>();
            if (damageable != null)
            {
                float distance = Vector3.Distance(transform.position, hit.transform.position);
                float damageMultiplier = 1f - (distance / _explosionRadius);
                float finalDamage = _damage * damageMultiplier;
                
                damageable.TakeDamage(finalDamage);
                OnDamageDealt?.Invoke(finalDamage);
            }
        }
    }
    
    private void ApplyDamageToPlayer(GameObject player)
    {
        float distance = Vector3.Distance(transform.position, player.transform.position);
        float damageMultiplier = 1f - (distance / _explosionRadius);
        float finalDamage = _damage * damageMultiplier;
        
        OnDamageDealt?.Invoke(finalDamage);
        
        player.SendMessage("TakeDamage", finalDamage, SendMessageOptions.DontRequireReceiver);
    }
    
    private System.Collections.IEnumerator DestroyAfterExplosion()
    {
        GetComponent<Renderer>().enabled = false;
        _triggerCollider.enabled = false;
        
        yield return new WaitForSeconds(2f);
        
        Destroy(gameObject);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isArmed ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _explosionRadius);
        
        if (!_isArmed)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        }
    }
    
    public interface IDamageable
    {
        void TakeDamage(float damage);
    }
}