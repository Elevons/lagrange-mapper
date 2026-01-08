// Prompt: ambient sound zone
// Type: general

using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class AmbientSoundZone : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioClip[] _ambientClips;
    [SerializeField] private bool _playOnAwake = true;
    [SerializeField] private bool _loop = true;
    [SerializeField] private bool _randomizeClips = false;
    
    [Header("Volume Control")]
    [SerializeField] private float _baseVolume = 0.5f;
    [SerializeField] private float _fadeInDuration = 2f;
    [SerializeField] private float _fadeOutDuration = 2f;
    [SerializeField] private AnimationCurve _volumeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Zone Settings")]
    [SerializeField] private bool _useDistanceAttenuation = true;
    [SerializeField] private float _maxDistance = 20f;
    [SerializeField] private float _minDistance = 1f;
    [SerializeField] private bool _triggerBasedZone = false;
    
    [Header("Playback Options")]
    [SerializeField] private float _clipChangeInterval = 30f;
    [SerializeField] private bool _crossfade = false;
    [SerializeField] private float _crossfadeDuration = 3f;
    
    private AudioSource _primaryAudioSource;
    private AudioSource _secondaryAudioSource;
    private bool _isPlayerInZone = false;
    private bool _isPlaying = false;
    private float _currentVolume = 0f;
    private float _targetVolume = 0f;
    private Coroutine _fadeCoroutine;
    private Coroutine _clipChangeCoroutine;
    private int _currentClipIndex = 0;
    private Transform _playerTransform;
    
    private void Awake()
    {
        _primaryAudioSource = GetComponent<AudioSource>();
        _primaryAudioSource.playOnAwake = false;
        _primaryAudioSource.loop = _loop && !_randomizeClips;
        _primaryAudioSource.volume = 0f;
        
        if (_crossfade)
        {
            _secondaryAudioSource = gameObject.AddComponent<AudioSource>();
            _secondaryAudioSource.playOnAwake = false;
            _secondaryAudioSource.loop = _loop && !_randomizeClips;
            _secondaryAudioSource.volume = 0f;
        }
        
        SetupAudioSource(_primaryAudioSource);
        if (_secondaryAudioSource != null)
        {
            SetupAudioSource(_secondaryAudioSource);
        }
    }
    
    private void Start()
    {
        if (_playOnAwake && !_triggerBasedZone)
        {
            StartAmbientSound();
        }
        
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            _playerTransform = playerObject.transform;
        }
    }
    
    private void Update()
    {
        if (!_triggerBasedZone && _useDistanceAttenuation && _playerTransform != null)
        {
            UpdateDistanceBasedVolume();
        }
        
        UpdateVolumeSmoothing();
    }
    
    private void SetupAudioSource(AudioSource audioSource)
    {
        if (_useDistanceAttenuation)
        {
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = _minDistance;
            audioSource.maxDistance = _maxDistance;
        }
        else
        {
            audioSource.spatialBlend = 0f;
        }
    }
    
    private void UpdateDistanceBasedVolume()
    {
        float distance = Vector3.Distance(transform.position, _playerTransform.position);
        float normalizedDistance = Mathf.Clamp01((distance - _minDistance) / (_maxDistance - _minDistance));
        float distanceVolume = _volumeCurve.Evaluate(1f - normalizedDistance);
        
        _targetVolume = _isPlaying ? _baseVolume * distanceVolume : 0f;
    }
    
    private void UpdateVolumeSmoothing()
    {
        if (!Mathf.Approximately(_currentVolume, _targetVolume))
        {
            float fadeSpeed = _targetVolume > _currentVolume ? 1f / _fadeInDuration : 1f / _fadeOutDuration;
            _currentVolume = Mathf.MoveTowards(_currentVolume, _targetVolume, fadeSpeed * Time.deltaTime);
            
            _primaryAudioSource.volume = _currentVolume;
            if (_secondaryAudioSource != null)
            {
                _secondaryAudioSource.volume = _currentVolume;
            }
        }
    }
    
    public void StartAmbientSound()
    {
        if (_ambientClips == null || _ambientClips.Length == 0) return;
        
        _isPlaying = true;
        
        if (_triggerBasedZone)
        {
            _targetVolume = _baseVolume;
        }
        else if (_useDistanceAttenuation && _playerTransform != null)
        {
            UpdateDistanceBasedVolume();
        }
        else
        {
            _targetVolume = _baseVolume;
        }
        
        PlayCurrentClip();
        
        if (_randomizeClips && _clipChangeInterval > 0f)
        {
            _clipChangeCoroutine = StartCoroutine(ClipChangeRoutine());
        }
    }
    
    public void StopAmbientSound()
    {
        _isPlaying = false;
        _targetVolume = 0f;
        
        if (_clipChangeCoroutine != null)
        {
            StopCoroutine(_clipChangeCoroutine);
            _clipChangeCoroutine = null;
        }
    }
    
    private void PlayCurrentClip()
    {
        if (_ambientClips == null || _ambientClips.Length == 0) return;
        
        AudioClip clipToPlay = _ambientClips[_currentClipIndex];
        if (clipToPlay == null) return;
        
        if (_crossfade && _secondaryAudioSource != null && _primaryAudioSource.isPlaying)
        {
            StartCoroutine(CrossfadeToClip(clipToPlay));
        }
        else
        {
            _primaryAudioSource.clip = clipToPlay;
            _primaryAudioSource.Play();
        }
    }
    
    private System.Collections.IEnumerator CrossfadeToClip(AudioClip newClip)
    {
        _secondaryAudioSource.clip = newClip;
        _secondaryAudioSource.volume = 0f;
        _secondaryAudioSource.Play();
        
        float timer = 0f;
        float primaryStartVolume = _primaryAudioSource.volume;
        
        while (timer < _crossfadeDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / _crossfadeDuration;
            
            _primaryAudioSource.volume = Mathf.Lerp(primaryStartVolume, 0f, progress);
            _secondaryAudioSource.volume = Mathf.Lerp(0f, _currentVolume, progress);
            
            yield return null;
        }
        
        _primaryAudioSource.Stop();
        
        AudioSource temp = _primaryAudioSource;
        _primaryAudioSource = _secondaryAudioSource;
        _secondaryAudioSource = temp;
    }
    
    private System.Collections.IEnumerator ClipChangeRoutine()
    {
        while (_isPlaying)
        {
            yield return new WaitForSeconds(_clipChangeInterval);
            
            if (_isPlaying && _randomizeClips)
            {
                int previousIndex = _currentClipIndex;
                _currentClipIndex = Random.Range(0, _ambientClips.Length);
                
                if (_ambientClips.Length > 1 && _currentClipIndex == previousIndex)
                {
                    _currentClipIndex = (_currentClipIndex + 1) % _ambientClips.Length;
                }
                
                PlayCurrentClip();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_triggerBasedZone) return;
        
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = true;
            StartAmbientSound();
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!_triggerBasedZone) return;
        
        if (other.CompareTag("Player"))
        {
            _isPlayerInZone = false;
            StopAmbientSound();
        }
    }
    
    private void OnValidate()
    {
        _baseVolume = Mathf.Clamp01(_baseVolume);
        _fadeInDuration = Mathf.Max(0.1f, _fadeInDuration);
        _fadeOutDuration = Mathf.Max(0.1f, _fadeOutDuration);
        _maxDistance = Mathf.Max(_minDistance + 0.1f, _maxDistance);
        _clipChangeInterval = Mathf.Max(1f, _clipChangeInterval);
        _crossfadeDuration = Mathf.Max(0.1f, _crossfadeDuration);
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_useDistanceAttenuation)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, _minDistance);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _maxDistance);
        }
        
        if (_triggerBasedZone)
        {
            Collider col = GetComponent<Collider>();
            if (col != null && col.isTrigger)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireCube(transform.position, col.bounds.size);
            }
        }
    }
}