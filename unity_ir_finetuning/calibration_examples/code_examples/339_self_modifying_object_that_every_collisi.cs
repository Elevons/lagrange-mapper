// Prompt: self-modifying object that every collision increases its mass by 10%, plays growth sound, scales up by 5%, adds random color tint - after 10 collisions it splits into two objects with half the accumulated properties
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SelfModifyingObject : MonoBehaviour
{
    [Header("Growth Settings")]
    [SerializeField] private float _massIncreasePercent = 0.1f;
    [SerializeField] private float _scaleIncreasePercent = 0.05f;
    [SerializeField] private int _splitThreshold = 10;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _growthSound;
    [SerializeField] private float _audioVolume = 0.5f;
    
    [Header("Visual")]
    [SerializeField] private float _colorIntensity = 0.3f;
    [SerializeField] private GameObject _splitPrefab;
    
    [Header("Split Settings")]
    [SerializeField] private float _splitForce = 5f;
    [SerializeField] private float _splitDistance = 2f;
    
    private Rigidbody _rigidbody;
    private Renderer _renderer;
    private AudioSource _audioSource;
    private Material _originalMaterial;
    private Material _instanceMaterial;
    
    private int _collisionCount = 0;
    private float _originalMass;
    private Vector3 _originalScale;
    private Color _accumulatedTint = Color.white;
    private float _totalMassGained = 0f;
    private Vector3 _totalScaleGained = Vector3.zero;
    
    [System.Serializable]
    public class SplitEvent : UnityEvent<GameObject, GameObject> { }
    
    [Header("Events")]
    public SplitEvent OnSplit = new SplitEvent();
    public UnityEvent<int> OnCollisionCountChanged = new UnityEvent<int>();
    
    private void Start()
    {
        InitializeComponents();
        StoreOriginalValues();
        CreateInstanceMaterial();
    }
    
    private void InitializeComponents()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _renderer = GetComponent<Renderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.volume = _audioVolume;
        }
        
        if (_renderer == null)
        {
            Debug.LogWarning("No Renderer found on " + gameObject.name);
        }
    }
    
    private void StoreOriginalValues()
    {
        _originalMass = _rigidbody.mass;
        _originalScale = transform.localScale;
        
        if (_renderer != null && _renderer.material != null)
        {
            _originalMaterial = _renderer.material;
        }
    }
    
    private void CreateInstanceMaterial()
    {
        if (_renderer != null && _originalMaterial != null)
        {
            _instanceMaterial = new Material(_originalMaterial);
            _renderer.material = _instanceMaterial;
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        ProcessCollision();
    }
    
    private void ProcessCollision()
    {
        _collisionCount++;
        
        IncreaseMass();
        ScaleUp();
        AddRandomColorTint();
        PlayGrowthSound();
        
        OnCollisionCountChanged.Invoke(_collisionCount);
        
        if (_collisionCount >= _splitThreshold)
        {
            StartCoroutine(SplitObject());
        }
    }
    
    private void IncreaseMass()
    {
        float massIncrease = _rigidbody.mass * _massIncreasePercent;
        _rigidbody.mass += massIncrease;
        _totalMassGained += massIncrease;
    }
    
    private void ScaleUp()
    {
        Vector3 scaleIncrease = transform.localScale * _scaleIncreasePercent;
        transform.localScale += scaleIncrease;
        _totalScaleGained += scaleIncrease;
    }
    
    private void AddRandomColorTint()
    {
        if (_instanceMaterial == null) return;
        
        Color randomTint = new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            1f
        );
        
        _accumulatedTint = Color.Lerp(_accumulatedTint, randomTint, _colorIntensity);
        _instanceMaterial.color = _accumulatedTint;
    }
    
    private void PlayGrowthSound()
    {
        if (_audioSource != null && _growthSound != null)
        {
            _audioSource.pitch = Random.Range(0.8f, 1.2f);
            _audioSource.PlayOneShot(_growthSound);
        }
    }
    
    private IEnumerator SplitObject()
    {
        yield return new WaitForFixedUpdate();
        
        GameObject splitObject1 = CreateSplitObject();
        GameObject splitObject2 = CreateSplitObject();
        
        PositionSplitObjects(splitObject1, splitObject2);
        ApplySplitForces(splitObject1, splitObject2);
        
        OnSplit.Invoke(splitObject1, splitObject2);
        
        Destroy(gameObject);
    }
    
    private GameObject CreateSplitObject()
    {
        GameObject splitObj;
        
        if (_splitPrefab != null)
        {
            splitObj = Instantiate(_splitPrefab, transform.position, transform.rotation);
        }
        else
        {
            splitObj = Instantiate(gameObject);
        }
        
        SelfModifyingObject splitComponent = splitObj.GetComponent<SelfModifyingObject>();
        if (splitComponent != null)
        {
            splitComponent.InitializeAsSplit(this);
        }
        
        return splitObj;
    }
    
    private void PositionSplitObjects(GameObject obj1, GameObject obj2)
    {
        Vector3 randomDirection = Random.onUnitSphere;
        randomDirection.y = Mathf.Abs(randomDirection.y);
        
        obj1.transform.position = transform.position + randomDirection * _splitDistance;
        obj2.transform.position = transform.position - randomDirection * _splitDistance;
    }
    
    private void ApplySplitForces(GameObject obj1, GameObject obj2)
    {
        Rigidbody rb1 = obj1.GetComponent<Rigidbody>();
        Rigidbody rb2 = obj2.GetComponent<Rigidbody>();
        
        if (rb1 != null && rb2 != null)
        {
            Vector3 force1 = (obj1.transform.position - transform.position).normalized * _splitForce;
            Vector3 force2 = (obj2.transform.position - transform.position).normalized * _splitForce;
            
            rb1.AddForce(force1, ForceMode.Impulse);
            rb2.AddForce(force2, ForceMode.Impulse);
        }
    }
    
    public void InitializeAsSplit(SelfModifyingObject parent)
    {
        _collisionCount = 0;
        
        float halfMass = parent._originalMass + (parent._totalMassGained * 0.5f);
        Vector3 halfScale = parent._originalScale + (parent._totalScaleGained * 0.5f);
        Color halfTint = Color.Lerp(Color.white, parent._accumulatedTint, 0.5f);
        
        _rigidbody.mass = halfMass;
        transform.localScale = halfScale;
        
        if (_instanceMaterial != null)
        {
            _instanceMaterial.color = halfTint;
            _accumulatedTint = halfTint;
        }
        
        _totalMassGained = halfMass - _originalMass;
        _totalScaleGained = halfScale - _originalScale;
    }
    
    private void OnDestroy()
    {
        if (_instanceMaterial != null)
        {
            DestroyImmediate(_instanceMaterial);
        }
    }
    
    public int GetCollisionCount()
    {
        return _collisionCount;
    }
    
    public float GetCurrentMass()
    {
        return _rigidbody != null ? _rigidbody.mass : 0f;
    }
    
    public Vector3 GetCurrentScale()
    {
        return transform.localScale;
    }
    
    public Color GetCurrentTint()
    {
        return _accumulatedTint;
    }
}