// Prompt: gravity field that inverts gravity for all objects within its radius, plays space-warp sound, creates visual distortion effect - objects entering should smoothly transition gravity direction over 1 second
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GravityField : MonoBehaviour
{
    [Header("Gravity Settings")]
    [SerializeField] private float _fieldRadius = 5f;
    [SerializeField] private float _transitionDuration = 1f;
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _spaceWarpSound;
    [SerializeField] private float _audioVolume = 0.7f;
    
    [Header("Visual Effects")]
    [SerializeField] private Material _distortionMaterial;
    [SerializeField] private float _distortionStrength = 0.1f;
    [SerializeField] private float _distortionSpeed = 2f;
    [SerializeField] private Color _fieldColor = Color.cyan;
    [SerializeField] private float _fieldAlpha = 0.3f;
    
    private AudioSource _audioSource;
    private SphereCollider _triggerCollider;
    private Dictionary<Rigidbody, GravityTransition> _affectedObjects = new Dictionary<Rigidbody, GravityTransition>();
    private MeshRenderer _fieldRenderer;
    private Material _fieldMaterial;
    private float _distortionTime;
    
    [System.Serializable]
    private class GravityTransition
    {
        public Rigidbody rigidbody;
        public bool isInField;
        public float transitionProgress;
        public Vector3 originalGravity;
        public Vector3 targetGravity;
        public Coroutine transitionCoroutine;
        
        public GravityTransition(Rigidbody rb)
        {
            rigidbody = rb;
            isInField = false;
            transitionProgress = 0f;
            originalGravity = Physics.gravity;
            targetGravity = -Physics.gravity;
        }
    }
    
    void Start()
    {
        SetupAudioSource();
        SetupTriggerCollider();
        SetupVisualField();
    }
    
    void Update()
    {
        UpdateDistortionEffect();
        UpdateGravityTransitions();
    }
    
    void SetupAudioSource()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.clip = _spaceWarpSound;
        _audioSource.volume = _audioVolume;
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.maxDistance = _fieldRadius * 2f;
    }
    
    void SetupTriggerCollider()
    {
        _triggerCollider = GetComponent<SphereCollider>();
        if (_triggerCollider == null)
        {
            _triggerCollider = gameObject.AddComponent<SphereCollider>();
        }
        
        _triggerCollider.isTrigger = true;
        _triggerCollider.radius = _fieldRadius;
    }
    
    void SetupVisualField()
    {
        GameObject fieldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyImmediate(fieldSphere.GetComponent<SphereCollider>());
        
        fieldSphere.transform.SetParent(transform);
        fieldSphere.transform.localPosition = Vector3.zero;
        fieldSphere.transform.localScale = Vector3.one * (_fieldRadius * 2f);
        
        _fieldRenderer = fieldSphere.GetComponent<MeshRenderer>();
        
        if (_distortionMaterial != null)
        {
            _fieldMaterial = new Material(_distortionMaterial);
        }
        else
        {
            _fieldMaterial = new Material(Shader.Find("Standard"));
            _fieldMaterial.SetFloat("_Mode", 3);
            _fieldMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _fieldMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _fieldMaterial.SetInt("_ZWrite", 0);
            _fieldMaterial.DisableKeyword("_ALPHATEST_ON");
            _fieldMaterial.EnableKeyword("_ALPHABLEND_ON");
            _fieldMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _fieldMaterial.renderQueue = 3000;
        }
        
        Color fieldColor = _fieldColor;
        fieldColor.a = _fieldAlpha;
        _fieldMaterial.color = fieldColor;
        _fieldRenderer.material = _fieldMaterial;
    }
    
    void UpdateDistortionEffect()
    {
        if (_fieldMaterial == null) return;
        
        _distortionTime += Time.deltaTime * _distortionSpeed;
        
        float distortion = Mathf.Sin(_distortionTime) * _distortionStrength;
        Vector2 offset = new Vector2(distortion, distortion * 0.5f);
        
        if (_fieldMaterial.HasProperty("_MainTex"))
        {
            _fieldMaterial.SetTextureOffset("_MainTex", offset);
        }
        
        float pulse = (Mathf.Sin(_distortionTime * 2f) + 1f) * 0.5f;
        Color currentColor = _fieldColor;
        currentColor.a = _fieldAlpha * (0.7f + pulse * 0.3f);
        _fieldMaterial.color = currentColor;
    }
    
    void UpdateGravityTransitions()
    {
        List<Rigidbody> toRemove = new List<Rigidbody>();
        
        foreach (var kvp in _affectedObjects)
        {
            var transition = kvp.Value;
            if (transition.rigidbody == null)
            {
                toRemove.Add(kvp.Key);
                continue;
            }
            
            if (transition.transitionCoroutine == null && transition.transitionProgress < 1f)
            {
                Vector3 currentGravity = Vector3.Lerp(transition.originalGravity, transition.targetGravity, transition.transitionProgress);
                ApplyGravityToRigidbody(transition.rigidbody, currentGravity);
            }
        }
        
        foreach (var rb in toRemove)
        {
            _affectedObjects.Remove(rb);
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (!IsValidTarget(other)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null) return;
        
        if (!_affectedObjects.ContainsKey(rb))
        {
            _affectedObjects[rb] = new GravityTransition(rb);
        }
        
        var transition = _affectedObjects[rb];
        if (!transition.isInField)
        {
            transition.isInField = true;
            transition.targetGravity = -Physics.gravity;
            
            if (transition.transitionCoroutine != null)
            {
                StopCoroutine(transition.transitionCoroutine);
            }
            
            transition.transitionCoroutine = StartCoroutine(TransitionGravity(transition, true));
            PlaySpaceWarpSound();
        }
    }
    
    void OnTriggerExit(Collider other)
    {
        if (!IsValidTarget(other)) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb == null || !_affectedObjects.ContainsKey(rb)) return;
        
        var transition = _affectedObjects[rb];
        if (transition.isInField)
        {
            transition.isInField = false;
            transition.targetGravity = Physics.gravity;
            
            if (transition.transitionCoroutine != null)
            {
                StopCoroutine(transition.transitionCoroutine);
            }
            
            transition.transitionCoroutine = StartCoroutine(TransitionGravity(transition, false));
        }
    }
    
    bool IsValidTarget(Collider other)
    {
        return (_affectedLayers.value & (1 << other.gameObject.layer)) != 0;
    }
    
    IEnumerator TransitionGravity(GravityTransition transition, bool enteringField)
    {
        float startProgress = transition.transitionProgress;
        float targetProgress = enteringField ? 1f : 0f;
        float elapsedTime = 0f;
        
        Vector3 startGravity = Vector3.Lerp(transition.originalGravity, -transition.originalGravity, startProgress);
        
        while (elapsedTime < _transitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = elapsedTime / _transitionDuration;
            float smoothProgress = Mathf.SmoothStep(startProgress, targetProgress, normalizedTime);
            
            transition.transitionProgress = smoothProgress;
            
            Vector3 currentGravity = Vector3.Lerp(transition.originalGravity, transition.targetGravity, smoothProgress);
            ApplyGravityToRigidbody(transition.rigidbody, currentGravity);
            
            yield return null;
        }
        
        transition.transitionProgress = targetProgress;
        Vector3 finalGravity = Vector3.Lerp(transition.originalGravity, transition.targetGravity, targetProgress);
        ApplyGravityToRigidbody(transition.rigidbody, finalGravity);
        
        transition.transitionCoroutine = null;
        
        if (!enteringField && targetProgress <= 0f)
        {
            _affectedObjects.Remove(transition.rigidbody);
        }
    }
    
    void ApplyGravityToRigidbody(Rigidbody rb, Vector3 gravity)
    {
        if (rb == null) return;
        
        rb.useGravity = false;
        rb.AddForce(gravity * rb.mass, ForceMode.Force);
    }
    
    void PlaySpaceWarpSound()
    {
        if (_audioSource != null && _spaceWarpSound != null)
        {
            _audioSource.pitch = Random.Range(0.8f, 1.2f);
            _audioSource.PlayOneShot(_spaceWarpSound, _audioVolume);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = _fieldColor;
        Gizmos.DrawWireSphere(transform.position, _fieldRadius);
        
        Gizmos.color = new Color(_fieldColor.r, _fieldColor.g, _fieldColor.b, 0.1f);
        Gizmos.DrawSphere(transform.position, _fieldRadius);
    }
    
    void OnDestroy()
    {
        foreach (var kvp in _affectedObjects)
        {
            var transition = kvp.Value;
            if (transition.rigidbody != null)
            {
                transition.rigidbody.useGravity = true;
            }
            
            if (transition.transitionCoroutine != null)
            {
                StopCoroutine(transition.transitionCoroutine);
            }
        }
        
        _affectedObjects.Clear();
        
        if (_fieldMaterial != null)
        {
            DestroyImmediate(_fieldMaterial);
        }
    }
}