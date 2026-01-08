// Prompt: magnetic field zone
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class MagneticFieldZone : MonoBehaviour
{
    [Header("Magnetic Field Settings")]
    [SerializeField] private float _magneticStrength = 10f;
    [SerializeField] private float _maxRange = 15f;
    [SerializeField] private AnimationCurve _strengthFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    [SerializeField] private LayerMask _affectedLayers = -1;
    
    [Header("Magnetic Behavior")]
    [SerializeField] private bool _attractMetalObjects = true;
    [SerializeField] private bool _repelMetalObjects = false;
    [SerializeField] private bool _affectRigidbodies = true;
    [SerializeField] private bool _affectCharacterControllers = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _magneticParticles;
    [SerializeField] private LineRenderer _fieldLines;
    [SerializeField] private Material _fieldLineMaterial;
    [SerializeField] private int _fieldLineCount = 8;
    [SerializeField] private float _fieldLineLength = 10f;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _magneticHumSound;
    [SerializeField] private float _baseVolume = 0.5f;
    
    [Header("Performance")]
    [SerializeField] private float _updateInterval = 0.02f;
    [SerializeField] private int _maxAffectedObjects = 50;
    
    private List<MagneticObject> _affectedObjects = new List<MagneticObject>();
    private SphereCollider _detectionCollider;
    private float _lastUpdateTime;
    private bool _isActive = true;
    
    [System.Serializable]
    private class MagneticObject
    {
        public Transform transform;
        public Rigidbody rigidbody;
        public CharacterController characterController;
        public float metallic = 1f;
        public Vector3 originalVelocity;
        
        public MagneticObject(Transform t, Rigidbody rb, CharacterController cc, float metalValue)
        {
            transform = t;
            rigidbody = rb;
            characterController = cc;
            metallic = metalValue;
        }
    }
    
    private void Start()
    {
        SetupDetectionCollider();
        SetupVisualEffects();
        SetupAudio();
    }
    
    private void SetupDetectionCollider()
    {
        _detectionCollider = GetComponent<SphereCollider>();
        if (_detectionCollider == null)
        {
            _detectionCollider = gameObject.AddComponent<SphereCollider>();
        }
        
        _detectionCollider.isTrigger = true;
        _detectionCollider.radius = _maxRange;
    }
    
    private void SetupVisualEffects()
    {
        if (_fieldLines == null)
        {
            GameObject fieldLinesObj = new GameObject("FieldLines");
            fieldLinesObj.transform.SetParent(transform);
            fieldLinesObj.transform.localPosition = Vector3.zero;
            _fieldLines = fieldLinesObj.AddComponent<LineRenderer>();
        }
        
        if (_fieldLines != null && _fieldLineMaterial != null)
        {
            _fieldLines.material = _fieldLineMaterial;
            _fieldLines.startWidth = 0.1f;
            _fieldLines.endWidth = 0.05f;
            _fieldLines.positionCount = _fieldLineCount * 2;
            _fieldLines.useWorldSpace = true;
        }
        
        UpdateFieldLines();
    }
    
    private void SetupAudio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        if (_audioSource != null && _magneticHumSound != null)
        {
            _audioSource.clip = _magneticHumSound;
            _audioSource.loop = true;
            _audioSource.volume = _baseVolume;
            _audioSource.Play();
        }
    }
    
    private void Update()
    {
        if (!_isActive) return;
        
        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            ApplyMagneticForces();
            UpdateVisualEffects();
            UpdateAudio();
            _lastUpdateTime = Time.time;
        }
    }
    
    private void ApplyMagneticForces()
    {
        for (int i = _affectedObjects.Count - 1; i >= 0; i--)
        {
            if (_affectedObjects[i].transform == null)
            {
                _affectedObjects.RemoveAt(i);
                continue;
            }
            
            ApplyForceToObject(_affectedObjects[i]);
        }
    }
    
    private void ApplyForceToObject(MagneticObject magneticObj)
    {
        Vector3 direction = transform.position - magneticObj.transform.position;
        float distance = direction.magnitude;
        
        if (distance > _maxRange)
        {
            _affectedObjects.Remove(magneticObj);
            return;
        }
        
        if (distance < 0.1f) return;
        
        direction.Normalize();
        
        float normalizedDistance = distance / _maxRange;
        float strengthMultiplier = _strengthFalloff.Evaluate(1f - normalizedDistance);
        float force = _magneticStrength * strengthMultiplier * magneticObj.metallic;
        
        if (_repelMetalObjects)
        {
            direction = -direction;
        }
        
        Vector3 magneticForce = direction * force;
        
        if (_affectRigidbodies && magneticObj.rigidbody != null)
        {
            magneticObj.rigidbody.AddForce(magneticForce, ForceMode.Force);
        }
        else if (_affectCharacterControllers && magneticObj.characterController != null)
        {
            Vector3 movement = magneticForce * Time.deltaTime / magneticObj.characterController.mass;
            magneticObj.characterController.Move(movement);
        }
        else
        {
            magneticObj.transform.position += magneticForce * Time.deltaTime * 0.1f;
        }
    }
    
    private void UpdateVisualEffects()
    {
        if (_magneticParticles != null)
        {
            var emission = _magneticParticles.emission;
            emission.rateOverTime = _affectedObjects.Count * 10f;
        }
        
        UpdateFieldLines();
    }
    
    private void UpdateFieldLines()
    {
        if (_fieldLines == null) return;
        
        Vector3[] positions = new Vector3[_fieldLineCount * 2];
        
        for (int i = 0; i < _fieldLineCount; i++)
        {
            float angle = (360f / _fieldLineCount) * i;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            
            positions[i * 2] = transform.position;
            positions[i * 2 + 1] = transform.position + direction * _fieldLineLength;
        }
        
        _fieldLines.positionCount = positions.Length;
        _fieldLines.SetPositions(positions);
    }
    
    private void UpdateAudio()
    {
        if (_audioSource != null && _audioSource.isPlaying)
        {
            float volumeMultiplier = Mathf.Clamp01(_affectedObjects.Count / 10f);
            _audioSource.volume = _baseVolume * volumeMultiplier;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_isActive) return;
        if (_affectedObjects.Count >= _maxAffectedObjects) return;
        if (((1 << other.gameObject.layer) & _affectedLayers) == 0) return;
        
        float metallicValue = GetMetallicValue(other);
        if (metallicValue <= 0f) return;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        CharacterController cc = other.GetComponent<CharacterController>();
        
        if (rb != null || cc != null || other.CompareTag("Player"))
        {
            MagneticObject magneticObj = new MagneticObject(other.transform, rb, cc, metallicValue);
            _affectedObjects.Add(magneticObj);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        for (int i = _affectedObjects.Count - 1; i >= 0; i--)
        {
            if (_affectedObjects[i].transform == other.transform)
            {
                _affectedObjects.RemoveAt(i);
                break;
            }
        }
    }
    
    private float GetMetallicValue(Collider other)
    {
        if (other.CompareTag("Metal")) return 1f;
        if (other.CompareTag("Player")) return 0.3f;
        if (other.name.ToLower().Contains("metal")) return 0.8f;
        if (other.name.ToLower().Contains("iron")) return 1f;
        if (other.name.ToLower().Contains("steel")) return 0.9f;
        
        Rigidbody rb = other.GetComponent<Rigidbody>();
        if (rb != null && rb.mass > 1f) return 0.5f;
        
        return 0.2f;
    }
    
    public void SetMagneticStrength(float strength)
    {
        _magneticStrength = Mathf.Max(0f, strength);
    }
    
    public void SetMaxRange(float range)
    {
        _maxRange = Mathf.Max(0.1f, range);
        if (_detectionCollider != null)
        {
            _detectionCollider.radius = _maxRange;
        }
    }
    
    public void ToggleAttraction()
    {
        _attractMetalObjects = !_attractMetalObjects;
        _repelMetalObjects = !_attractMetalObjects;
    }
    
    public void SetActive(bool active)
    {
        _isActive = active;
        
        if (!active)
        {
            _affectedObjects.Clear();
        }
        
        if (_magneticParticles != null)
        {
            if (active)
                _magneticParticles.Play();
            else
                _magneticParticles.Stop();
        }
        
        if (_audioSource != null)
        {
            if (active && !_audioSource.isPlaying)
                _audioSource.Play();
            else if (!active && _audioSource.isPlaying)
                _audioSource.Stop();
        }
    }
    
    public int GetAffectedObjectCount()
    {
        return _affectedObjects.Count;
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _maxRange);
        
        Gizmos.color = Color.cyan;
        for (int i = 0; i < _affectedObjects.Count; i++)
        {
            if (_affectedObjects[i].transform != null)
            {
                Gizmos.DrawLine(transform.position, _affectedObjects[i].transform.position);
            }
        }
    }
}