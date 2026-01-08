// Prompt: acid pool hazard
// Type: environment

using UnityEngine;
using UnityEngine.Events;

public class AcidPoolHazard : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 0.5f;
    [SerializeField] private bool _continuousDamage = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _bubbleEffect;
    [SerializeField] private ParticleSystem _splashEffect;
    [SerializeField] private Color _acidColor = Color.green;
    [SerializeField] private float _dissolveSpeed = 2f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _enterSound;
    [SerializeField] private AudioClip _damageSound;
    [SerializeField] private AudioClip _bubbleSound;
    [SerializeField] private float _audioVolume = 0.7f;
    
    [Header("Animation")]
    [SerializeField] private float _waveAmplitude = 0.1f;
    [SerializeField] private float _waveFrequency = 1f;
    [SerializeField] private Transform _surfaceTransform;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnObjectEnterAcid;
    public UnityEvent<GameObject> OnObjectExitAcid;
    public UnityEvent<GameObject, float> OnDamageDealt;
    
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Material _acidMaterial;
    private Vector3 _originalSurfacePosition;
    private float _waveTimer;
    
    private System.Collections.Generic.Dictionary<GameObject, float> _objectsInAcid = 
        new System.Collections.Generic.Dictionary<GameObject, float>();
    
    [System.Serializable]
    private class DissolvableObject
    {
        public GameObject gameObject;
        public Renderer renderer;
        public Material originalMaterial;
        public float dissolveProgress;
        public bool isDissolving;
        
        public DissolvableObject(GameObject obj)
        {
            gameObject = obj;
            renderer = obj.GetComponent<Renderer>();
            if (renderer != null)
            {
                originalMaterial = renderer.material;
            }
            dissolveProgress = 0f;
            isDissolving = false;
        }
    }
    
    private System.Collections.Generic.List<DissolvableObject> _dissolvingObjects = 
        new System.Collections.Generic.List<DissolvableObject>();
    
    private void Start()
    {
        InitializeComponents();
        SetupAcidMaterial();
        StartBubbleEffects();
        
        if (_surfaceTransform != null)
        {
            _originalSurfacePosition = _surfaceTransform.localPosition;
        }
    }
    
    private void InitializeComponents()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.volume = _audioVolume;
        _audioSource.spatialBlend = 1f;
        
        _renderer = GetComponent<Renderer>();
        
        if (_bubbleEffect == null)
        {
            _bubbleEffect = GetComponentInChildren<ParticleSystem>();
        }
    }
    
    private void SetupAcidMaterial()
    {
        if (_renderer != null)
        {
            _acidMaterial = _renderer.material;
            _acidMaterial.color = _acidColor;
            
            if (_acidMaterial.HasProperty("_EmissionColor"))
            {
                _acidMaterial.SetColor("_EmissionColor", _acidColor * 0.3f);
                _acidMaterial.EnableKeyword("_EMISSION");
            }
        }
    }
    
    private void StartBubbleEffects()
    {
        if (_bubbleEffect != null)
        {
            _bubbleEffect.Play();
        }
        
        if (_bubbleSound != null)
        {
            _audioSource.clip = _bubbleSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
    }
    
    private void Update()
    {
        UpdateWaveAnimation();
        ProcessContinuousDamage();
        UpdateDissolvingObjects();
    }
    
    private void UpdateWaveAnimation()
    {
        if (_surfaceTransform == null) return;
        
        _waveTimer += Time.deltaTime * _waveFrequency;
        float waveOffset = Mathf.Sin(_waveTimer) * _waveAmplitude;
        
        _surfaceTransform.localPosition = _originalSurfacePosition + Vector3.up * waveOffset;
    }
    
    private void ProcessContinuousDamage()
    {
        if (!_continuousDamage) return;
        
        var objectsToRemove = new System.Collections.Generic.List<GameObject>();
        var objectsToUpdate = new System.Collections.Generic.List<GameObject>(_objectsInAcid.Keys);
        
        foreach (var obj in objectsToUpdate)
        {
            if (obj == null)
            {
                objectsToRemove.Add(obj);
                continue;
            }
            
            _objectsInAcid[obj] += Time.deltaTime;
            
            if (_objectsInAcid[obj] >= _damageInterval)
            {
                DealDamage(obj);
                _objectsInAcid[obj] = 0f;
            }
        }
        
        foreach (var obj in objectsToRemove)
        {
            _objectsInAcid.Remove(obj);
        }
    }
    
    private void UpdateDissolvingObjects()
    {
        for (int i = _dissolvingObjects.Count - 1; i >= 0; i--)
        {
            var dissolvable = _dissolvingObjects[i];
            
            if (dissolvable.gameObject == null)
            {
                _dissolvingObjects.RemoveAt(i);
                continue;
            }
            
            if (dissolvable.isDissolving)
            {
                dissolvable.dissolveProgress += Time.deltaTime * _dissolveSpeed;
                
                if (dissolvable.renderer != null && dissolvable.renderer.material.HasProperty("_Cutoff"))
                {
                    dissolvable.renderer.material.SetFloat("_Cutoff", dissolvable.dissolveProgress);
                }
                else if (dissolvable.renderer != null)
                {
                    Color color = dissolvable.renderer.material.color;
                    color.a = Mathf.Lerp(1f, 0f, dissolvable.dissolveProgress);
                    dissolvable.renderer.material.color = color;
                }
                
                if (dissolvable.dissolveProgress >= 1f)
                {
                    if (dissolvable.gameObject != null)
                    {
                        Destroy(dissolvable.gameObject);
                    }
                    _dissolvingObjects.RemoveAt(i);
                }
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;
        
        PlaySplashEffect(other.transform.position);
        PlayEnterSound();
        
        if (!_objectsInAcid.ContainsKey(other.gameObject))
        {
            _objectsInAcid.Add(other.gameObject, 0f);
            OnObjectEnterAcid?.Invoke(other.gameObject);
            
            if (!_continuousDamage)
            {
                DealDamage(other.gameObject);
            }
            
            StartDissolving(other.gameObject);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other == null) return;
        
        if (_objectsInAcid.ContainsKey(other.gameObject))
        {
            _objectsInAcid.Remove(other.gameObject);
            OnObjectExitAcid?.Invoke(other.gameObject);
        }
    }
    
    private void PlaySplashEffect(Vector3 position)
    {
        if (_splashEffect != null)
        {
            _splashEffect.transform.position = position;
            _splashEffect.Play();
        }
    }
    
    private void PlayEnterSound()
    {
        if (_enterSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_enterSound, _audioVolume);
        }
    }
    
    private void DealDamage(GameObject target)
    {
        if (target == null) return;
        
        // Try to find and damage player
        if (target.CompareTag("Player"))
        {
            var characterController = target.GetComponent<CharacterController>();
            if (characterController != null)
            {
                // Apply knockback
                Vector3 knockback = (target.transform.position - transform.position).normalized * 2f;
                knockback.y = 1f;
                characterController.Move(knockback * Time.deltaTime);
            }
        }
        
        // Try to damage any object with health component
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Use reflection to try to call TakeDamage method if it exists
            var takeDamageMethod = healthComponent.GetType().GetMethod("TakeDamage");
            if (takeDamageMethod != null)
            {
                takeDamageMethod.Invoke(healthComponent, new object[] { _damageAmount });
            }
        }
        
        // Destroy non-player objects after some time
        if (!target.CompareTag("Player"))
        {
            var rigidbody = target.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                rigidbody.AddForce(Vector3.down * 5f, ForceMode.Impulse);
            }
        }
        
        PlayDamageSound();
        OnDamageDealt?.Invoke(target, _damageAmount);
    }
    
    private void PlayDamageSound()
    {
        if (_damageSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_damageSound, _audioVolume);
        }
    }
    
    private void StartDissolving(GameObject target)
    {
        if (target.CompareTag("Player")) return;
        
        var existingDissolvable = _dissolvingObjects.Find(d => d.gameObject == target);
        if (existingDissolvable == null)
        {
            var newDissolvable = new DissolvableObject(target);
            newDissolvable.isDissolving = true;
            _dissolvingObjects.Add(newDissolvable);
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
    
    public void EnableContinuousDamage(bool enable)
    {
        _continuousDamage = enable;
    }
    
    private void OnDestroy()
    {
        if (_acidMaterial != null)
        {
            Destroy(_acidMaterial);
        }
    }
}