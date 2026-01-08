// Prompt: trident with water affinity
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class WaterTrident : MonoBehaviour
{
    [Header("Trident Settings")]
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _range = 10f;
    [SerializeField] private float _attackCooldown = 1.5f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Water Affinity")]
    [SerializeField] private float _waterDamageMultiplier = 2f;
    [SerializeField] private float _underwaterSpeedBonus = 1.5f;
    [SerializeField] private float _waterHealingRate = 5f;
    [SerializeField] private ParticleSystem _waterEffectPrefab;
    [SerializeField] private AudioClip _waterAttackSound;
    [SerializeField] private AudioClip _waterSplashSound;
    
    [Header("Special Abilities")]
    [SerializeField] private float _waterBoltSpeed = 20f;
    [SerializeField] private GameObject _waterBoltPrefab;
    [SerializeField] private float _tidalWaveForce = 1000f;
    [SerializeField] private float _tidalWaveRadius = 15f;
    [SerializeField] private float _specialAbilityCooldown = 10f;
    
    [Header("Visual Effects")]
    [SerializeField] private Material _waterMaterial;
    [SerializeField] private Light _tridentGlow;
    [SerializeField] private Transform _projectileSpawnPoint;
    
    [Header("Events")]
    public UnityEvent<float> OnDamageDealt;
    public UnityEvent OnWaterAbilityUsed;
    public UnityEvent OnTridentEquipped;
    
    private bool _isEquipped = false;
    private float _lastAttackTime;
    private float _lastSpecialAbilityTime;
    private bool _isInWater = false;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Transform _wielder;
    private Rigidbody _wielderRigidbody;
    private ParticleSystem _currentWaterEffect;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();
            
        _renderer = GetComponent<Renderer>();
        
        if (_tridentGlow != null)
            _tridentGlow.enabled = false;
            
        SetupWaterDetection();
    }
    
    private void Update()
    {
        if (_isEquipped && _wielder != null)
        {
            HandleInput();
            UpdateWaterEffects();
            HandleWaterHealing();
        }
        
        UpdateVisualEffects();
    }
    
    private void SetupWaterDetection()
    {
        SphereCollider waterDetector = gameObject.AddComponent<SphereCollider>();
        waterDetector.isTrigger = true;
        waterDetector.radius = 2f;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Water") || other.name.ToLower().Contains("water"))
        {
            _isInWater = true;
            OnWaterAbilityUsed?.Invoke();
            PlayWaterEffect();
        }
        
        if (other.CompareTag("Player") && !_isEquipped)
        {
            EquipTrident(other.transform);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Water") || other.name.ToLower().Contains("water"))
        {
            _isInWater = false;
            StopWaterEffect();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0) && CanAttack())
        {
            PerformAttack();
        }
        
        if (Input.GetKeyDown(KeyCode.Q) && CanUseSpecialAbility())
        {
            UseWaterBolt();
        }
        
        if (Input.GetKeyDown(KeyCode.E) && CanUseSpecialAbility())
        {
            UseTidalWave();
        }
    }
    
    private void EquipTrident(Transform player)
    {
        _wielder = player;
        _wielderRigidbody = player.GetComponent<Rigidbody>();
        _isEquipped = true;
        
        transform.SetParent(player);
        transform.localPosition = new Vector3(0.5f, 0f, 1f);
        transform.localRotation = Quaternion.identity;
        
        if (_tridentGlow != null)
            _tridentGlow.enabled = true;
            
        OnTridentEquipped?.Invoke();
    }
    
    private bool CanAttack()
    {
        return Time.time >= _lastAttackTime + _attackCooldown;
    }
    
    private bool CanUseSpecialAbility()
    {
        return Time.time >= _lastSpecialAbilityTime + _specialAbilityCooldown;
    }
    
    private void PerformAttack()
    {
        _lastAttackTime = Time.time;
        
        RaycastHit hit;
        Vector3 attackDirection = _wielder.forward;
        
        if (Physics.Raycast(_wielder.position, attackDirection, out hit, _range, _targetLayers))
        {
            float finalDamage = _damage;
            
            if (_isInWater)
                finalDamage *= _waterDamageMultiplier;
                
            DealDamage(hit.collider.gameObject, finalDamage);
            CreateWaterImpactEffect(hit.point);
        }
        
        PlayAttackSound();
        OnWaterAbilityUsed?.Invoke();
    }
    
    private void UseWaterBolt()
    {
        _lastSpecialAbilityTime = Time.time;
        
        if (_waterBoltPrefab != null && _projectileSpawnPoint != null)
        {
            GameObject waterBolt = Instantiate(_waterBoltPrefab, _projectileSpawnPoint.position, _projectileSpawnPoint.rotation);
            
            WaterBolt boltScript = waterBolt.GetComponent<WaterBolt>();
            if (boltScript == null)
                boltScript = waterBolt.AddComponent<WaterBolt>();
                
            boltScript.Initialize(_damage * 1.5f, _waterBoltSpeed, _wielder);
        }
        
        OnWaterAbilityUsed?.Invoke();
    }
    
    private void UseTidalWave()
    {
        _lastSpecialAbilityTime = Time.time;
        
        Collider[] affectedObjects = Physics.OverlapSphere(_wielder.position, _tidalWaveRadius);
        
        foreach (Collider obj in affectedObjects)
        {
            if (obj.gameObject != _wielder.gameObject)
            {
                Rigidbody rb = obj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 forceDirection = (obj.transform.position - _wielder.position).normalized;
                    rb.AddForce(forceDirection * _tidalWaveForce, ForceMode.Impulse);
                }
                
                if (!obj.CompareTag("Player"))
                {
                    DealDamage(obj.gameObject, _damage * 0.8f);
                }
            }
        }
        
        CreateTidalWaveEffect();
        OnWaterAbilityUsed?.Invoke();
    }
    
    private void DealDamage(GameObject target, float damage)
    {
        if (target.CompareTag("Enemy") || target.name.ToLower().Contains("enemy"))
        {
            Destroy(target);
        }
        
        OnDamageDealt?.Invoke(damage);
    }
    
    private void HandleWaterHealing()
    {
        if (_isInWater && _wielder != null)
        {
            // Simulate healing by triggering event
            OnDamageDealt?.Invoke(-_waterHealingRate * Time.deltaTime);
        }
    }
    
    private void UpdateWaterEffects()
    {
        if (_isInWater && _wielderRigidbody != null)
        {
            // Apply underwater speed bonus
            Vector3 velocity = _wielderRigidbody.velocity;
            velocity *= _underwaterSpeedBonus;
            _wielderRigidbody.velocity = velocity;
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_renderer != null && _waterMaterial != null && _isInWater)
        {
            _renderer.material = _waterMaterial;
        }
        
        if (_tridentGlow != null)
        {
            _tridentGlow.intensity = _isInWater ? 2f : 1f;
            _tridentGlow.color = _isInWater ? Color.cyan : Color.blue;
        }
    }
    
    private void PlayWaterEffect()
    {
        if (_waterEffectPrefab != null)
        {
            _currentWaterEffect = Instantiate(_waterEffectPrefab, transform.position, Quaternion.identity);
            _currentWaterEffect.transform.SetParent(transform);
        }
        
        if (_audioSource != null && _waterSplashSound != null)
        {
            _audioSource.PlayOneShot(_waterSplashSound);
        }
    }
    
    private void StopWaterEffect()
    {
        if (_currentWaterEffect != null)
        {
            _currentWaterEffect.Stop();
            Destroy(_currentWaterEffect.gameObject, 2f);
        }
    }
    
    private void CreateWaterImpactEffect(Vector3 position)
    {
        if (_waterEffectPrefab != null)
        {
            ParticleSystem impact = Instantiate(_waterEffectPrefab, position, Quaternion.identity);
            Destroy(impact.gameObject, 3f);
        }
    }
    
    private void CreateTidalWaveEffect()
    {
        if (_waterEffectPrefab != null)
        {
            ParticleSystem tidalWave = Instantiate(_waterEffectPrefab, _wielder.position, Quaternion.identity);
            var main = tidalWave.main;
            main.startSize = _tidalWaveRadius;
            Destroy(tidalWave.gameObject, 5f);
        }
    }
    
    private void PlayAttackSound()
    {
        if (_audioSource != null && _waterAttackSound != null)
        {
            _audioSource.PlayOneShot(_waterAttackSound);
        }
    }
}

public class WaterBolt : MonoBehaviour
{
    private float _damage;
    private float _speed;
    private Transform _owner;
    private Rigidbody _rigidbody;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();
            
        _rigidbody.useGravity = false;
        
        Destroy(gameObject, 5f);
    }
    
    public void Initialize(float damage, float speed, Transform owner)
    {
        _damage = damage;
        _speed = speed;
        _owner = owner;
        
        if (_rigidbody != null)
            _rigidbody.velocity = transform.forward * _speed;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.transform != _owner && !other.CompareTag("Player"))
        {
            if (other.CompareTag("Enemy") || other.name.ToLower().Contains("enemy"))
            {
                Destroy(other.gameObject);
            }
            
            Destroy(gameObject);
        }
    }
}