// Prompt: footstep sounds based on surface
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class FootstepSoundController : MonoBehaviour
{
    [System.Serializable]
    public class SurfaceAudioData
    {
        public string surfaceTag;
        public AudioClip[] footstepClips;
        [Range(0f, 1f)]
        public float volume = 1f;
        [Range(0.5f, 2f)]
        public float pitchVariation = 0.1f;
    }

    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private List<SurfaceAudioData> _surfaceAudioData = new List<SurfaceAudioData>();
    
    [Header("Footstep Detection")]
    [SerializeField] private LayerMask _groundLayerMask = 1;
    [SerializeField] private float _raycastDistance = 1.1f;
    [SerializeField] private Transform _raycastOrigin;
    
    [Header("Timing")]
    [SerializeField] private float _stepInterval = 0.5f;
    [SerializeField] private float _minimumVelocity = 0.1f;
    
    [Header("Default Audio")]
    [SerializeField] private AudioClip[] _defaultFootstepClips;
    [SerializeField] private float _defaultVolume = 0.7f;

    private Rigidbody _rigidbody;
    private CharacterController _characterController;
    private float _lastStepTime;
    private string _currentSurfaceTag = "";
    private Dictionary<string, SurfaceAudioData> _surfaceDataDict;

    private void Awake()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (_raycastOrigin == null)
            _raycastOrigin = transform;

        _rigidbody = GetComponent<Rigidbody>();
        _characterController = GetComponent<CharacterController>();

        InitializeSurfaceDataDictionary();
    }

    private void Start()
    {
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
    }

    private void Update()
    {
        if (ShouldPlayFootstep())
        {
            DetectSurfaceAndPlayFootstep();
        }
    }

    private void InitializeSurfaceDataDictionary()
    {
        _surfaceDataDict = new Dictionary<string, SurfaceAudioData>();
        
        foreach (var surfaceData in _surfaceAudioData)
        {
            if (!string.IsNullOrEmpty(surfaceData.surfaceTag))
            {
                _surfaceDataDict[surfaceData.surfaceTag] = surfaceData;
            }
        }
    }

    private bool ShouldPlayFootstep()
    {
        if (Time.time - _lastStepTime < _stepInterval)
            return false;

        bool isMoving = false;

        if (_characterController != null)
        {
            isMoving = _characterController.velocity.magnitude > _minimumVelocity && _characterController.isGrounded;
        }
        else if (_rigidbody != null)
        {
            Vector3 horizontalVelocity = new Vector3(_rigidbody.velocity.x, 0, _rigidbody.velocity.z);
            isMoving = horizontalVelocity.magnitude > _minimumVelocity && IsGrounded();
        }
        else
        {
            isMoving = transform.hasChanged && IsGrounded();
            transform.hasChanged = false;
        }

        return isMoving;
    }

    private bool IsGrounded()
    {
        return Physics.Raycast(_raycastOrigin.position, Vector3.down, _raycastDistance, _groundLayerMask);
    }

    private void DetectSurfaceAndPlayFootstep()
    {
        RaycastHit hit;
        if (Physics.Raycast(_raycastOrigin.position, Vector3.down, out hit, _raycastDistance, _groundLayerMask))
        {
            string surfaceTag = hit.collider.tag;
            PlayFootstepSound(surfaceTag);
            _currentSurfaceTag = surfaceTag;
        }
    }

    private void PlayFootstepSound(string surfaceTag)
    {
        AudioClip[] clipsToUse = _defaultFootstepClips;
        float volumeToUse = _defaultVolume;
        float pitchVariation = 0.1f;

        if (_surfaceDataDict.ContainsKey(surfaceTag))
        {
            var surfaceData = _surfaceDataDict[surfaceTag];
            if (surfaceData.footstepClips != null && surfaceData.footstepClips.Length > 0)
            {
                clipsToUse = surfaceData.footstepClips;
                volumeToUse = surfaceData.volume;
                pitchVariation = surfaceData.pitchVariation;
            }
        }

        if (clipsToUse != null && clipsToUse.Length > 0)
        {
            AudioClip clipToPlay = clipsToUse[Random.Range(0, clipsToUse.Length)];
            
            if (clipToPlay != null)
            {
                _audioSource.clip = clipToPlay;
                _audioSource.volume = volumeToUse;
                _audioSource.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
                _audioSource.Play();
                
                _lastStepTime = Time.time;
            }
        }
    }

    public void SetStepInterval(float interval)
    {
        _stepInterval = Mathf.Max(0.1f, interval);
    }

    public void SetMinimumVelocity(float velocity)
    {
        _minimumVelocity = Mathf.Max(0f, velocity);
    }

    public string GetCurrentSurfaceTag()
    {
        return _currentSurfaceTag;
    }

    private void OnDrawGizmosSelected()
    {
        if (_raycastOrigin != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(_raycastOrigin.position, _raycastOrigin.position + Vector3.down * _raycastDistance);
        }
    }
}