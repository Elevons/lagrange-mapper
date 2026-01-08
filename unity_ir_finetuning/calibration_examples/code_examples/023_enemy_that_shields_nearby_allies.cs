// Prompt: enemy that shields nearby allies
// Type: combat

using UnityEngine;
using System.Collections.Generic;

public class ShieldingEnemy : MonoBehaviour
{
    [Header("Shield Settings")]
    [SerializeField] private float _shieldRadius = 5f;
    [SerializeField] private float _shieldStrength = 0.5f;
    [SerializeField] private LayerMask _allyLayerMask = -1;
    [SerializeField] private string _allyTag = "Enemy";
    
    [Header("Visual Effects")]
    [SerializeField] private GameObject _shieldEffectPrefab;
    [SerializeField] private LineRenderer _shieldBeamPrefab;
    [SerializeField] private Color _shieldColor = Color.cyan;
    [SerializeField] private bool _showShieldRadius = true;
    
    [Header("Performance")]
    [SerializeField] private float _updateInterval = 0.1f;
    
    private List<ShieldedAlly> _shieldedAllies = new List<ShieldedAlly>();
    private List<GameObject> _activeShieldEffects = new List<GameObject>();
    private List<LineRenderer> _activeBeams = new List<LineRenderer>();
    private float _lastUpdateTime;
    private SphereCollider _detectionCollider;
    
    [System.Serializable]
    private class ShieldedAlly
    {
        public GameObject ally;
        public float originalDamageMultiplier;
        public AllyDamageReceiver damageReceiver;
        
        public ShieldedAlly(GameObject allyObject, AllyDamageReceiver receiver)
        {
            ally = allyObject;
            damageReceiver = receiver;
            originalDamageMultiplier = receiver.damageMultiplier;
        }
    }
    
    private class AllyDamageReceiver : MonoBehaviour
    {
        public float damageMultiplier = 1f;
        public bool isShielded = false;
        
        public void TakeDamage(float damage)
        {
            float finalDamage = damage * damageMultiplier;
            
            if (TryGetComponent<Rigidbody>(out Rigidbody rb))
            {
                rb.AddForce(Vector3.up * finalDamage, ForceMode.Impulse);
            }
            
            if (finalDamage >= 100f)
            {
                gameObject.SetActive(false);
            }
        }
    }
    
    void Start()
    {
        SetupDetectionCollider();
        _lastUpdateTime = Time.time;
    }
    
    void Update()
    {
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            UpdateShieldedAllies();
            _lastUpdateTime = Time.time;
        }
    }
    
    void SetupDetectionCollider()
    {
        _detectionCollider = gameObject.GetComponent<SphereCollider>();
        if (_detectionCollider == null)
        {
            _detectionCollider = gameObject.AddComponent<SphereCollider>();
        }
        
        _detectionCollider.isTrigger = true;
        _detectionCollider.radius = _shieldRadius;
    }
    
    void UpdateShieldedAllies()
    {
        RemoveInvalidAllies();
        FindNewAllies();
        UpdateVisualEffects();
    }
    
    void RemoveInvalidAllies()
    {
        for (int i = _shieldedAllies.Count - 1; i >= 0; i--)
        {
            var shieldedAlly = _shieldedAllies[i];
            
            if (shieldedAlly.ally == null || !shieldedAlly.ally.activeInHierarchy)
            {
                RemoveShieldFromAlly(shieldedAlly);
                _shieldedAllies.RemoveAt(i);
                continue;
            }
            
            float distance = Vector3.Distance(transform.position, shieldedAlly.ally.transform.position);
            if (distance > _shieldRadius)
            {
                RemoveShieldFromAlly(shieldedAlly);
                _shieldedAllies.RemoveAt(i);
            }
        }
    }
    
    void FindNewAllies()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, _shieldRadius, _allyLayerMask);
        
        foreach (var collider in nearbyColliders)
        {
            if (collider.gameObject == gameObject) continue;
            
            if (!string.IsNullOrEmpty(_allyTag) && !collider.CompareTag(_allyTag)) continue;
            
            if (!IsAllyAlreadyShielded(collider.gameObject))
            {
                AddShieldToAlly(collider.gameObject);
            }
        }
    }
    
    bool IsAllyAlreadyShielded(GameObject ally)
    {
        foreach (var shieldedAlly in _shieldedAllies)
        {
            if (shieldedAlly.ally == ally)
                return true;
        }
        return false;
    }
    
    void AddShieldToAlly(GameObject ally)
    {
        AllyDamageReceiver damageReceiver = ally.GetComponent<AllyDamageReceiver>();
        if (damageReceiver == null)
        {
            damageReceiver = ally.AddComponent<AllyDamageReceiver>();
        }
        
        ShieldedAlly shieldedAlly = new ShieldedAlly(ally, damageReceiver);
        damageReceiver.damageMultiplier = _shieldStrength;
        damageReceiver.isShielded = true;
        
        _shieldedAllies.Add(shieldedAlly);
    }
    
    void RemoveShieldFromAlly(ShieldedAlly shieldedAlly)
    {
        if (shieldedAlly.damageReceiver != null)
        {
            shieldedAlly.damageReceiver.damageMultiplier = shieldedAlly.originalDamageMultiplier;
            shieldedAlly.damageReceiver.isShielded = false;
        }
    }
    
    void UpdateVisualEffects()
    {
        ClearVisualEffects();
        
        foreach (var shieldedAlly in _shieldedAllies)
        {
            if (shieldedAlly.ally != null)
            {
                CreateShieldEffect(shieldedAlly.ally);
                CreateShieldBeam(shieldedAlly.ally);
            }
        }
    }
    
    void CreateShieldEffect(GameObject ally)
    {
        if (_shieldEffectPrefab != null)
        {
            GameObject effect = Instantiate(_shieldEffectPrefab, ally.transform.position, Quaternion.identity);
            effect.transform.SetParent(ally.transform);
            _activeShieldEffects.Add(effect);
            
            if (effect.TryGetComponent<Renderer>(out Renderer renderer))
            {
                renderer.material.color = _shieldColor;
            }
        }
    }
    
    void CreateShieldBeam(GameObject ally)
    {
        LineRenderer beam;
        
        if (_shieldBeamPrefab != null)
        {
            beam = Instantiate(_shieldBeamPrefab, transform.position, Quaternion.identity);
        }
        else
        {
            GameObject beamObject = new GameObject("ShieldBeam");
            beam = beamObject.AddComponent<LineRenderer>();
            beam.material = new Material(Shader.Find("Sprites/Default"));
            beam.color = _shieldColor;
            beam.startWidth = 0.1f;
            beam.endWidth = 0.05f;
        }
        
        beam.positionCount = 2;
        beam.SetPosition(0, transform.position);
        beam.SetPosition(1, ally.transform.position);
        
        _activeBeams.Add(beam);
    }
    
    void ClearVisualEffects()
    {
        foreach (var effect in _activeShieldEffects)
        {
            if (effect != null)
                DestroyImmediate(effect);
        }
        _activeShieldEffects.Clear();
        
        foreach (var beam in _activeBeams)
        {
            if (beam != null)
                DestroyImmediate(beam.gameObject);
        }
        _activeBeams.Clear();
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == gameObject) return;
        
        if (!string.IsNullOrEmpty(_allyTag) && !other.CompareTag(_allyTag)) return;
        
        if (!IsAllyAlreadyShielded(other.gameObject))
        {
            AddShieldToAlly(other.gameObject);
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        for (int i = _shieldedAllies.Count - 1; i >= 0; i--)
        {
            if (_shieldedAllies[i].ally == other.gameObject)
            {
                RemoveShieldFromAlly(_shieldedAllies[i]);
                _shieldedAllies.RemoveAt(i);
                break;
            }
        }
    }
    
    void OnDestroy()
    {
        foreach (var shieldedAlly in _shieldedAllies)
        {
            RemoveShieldFromAlly(shieldedAlly);
        }
        
        ClearVisualEffects();
    }
    
    void OnDrawGizmosSelected()
    {
        if (_showShieldRadius)
        {
            Gizmos.color = _shieldColor;
            Gizmos.DrawWireSphere(transform.position, _shieldRadius);
        }
    }
}