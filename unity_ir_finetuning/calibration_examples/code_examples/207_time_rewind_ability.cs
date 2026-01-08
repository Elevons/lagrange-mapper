// Prompt: time rewind ability
// Type: general

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class TimeRewindAbility : MonoBehaviour
{
    [System.Serializable]
    public class TransformSnapshot
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float timestamp;
        
        public TransformSnapshot(Transform transform, float time)
        {
            position = transform.position;
            rotation = transform.rotation;
            scale = transform.localScale;
            timestamp = time;
        }
    }
    
    [System.Serializable]
    public class RigidbodySnapshot
    {
        public Vector3 velocity;
        public Vector3 angularVelocity;
        public float timestamp;
        
        public RigidbodySnapshot(Rigidbody rb, float time)
        {
            velocity = rb.velocity;
            angularVelocity = rb.angularVelocity;
            timestamp = time;
        }
    }
    
    [Header("Rewind Settings")]
    [SerializeField] private KeyCode _rewindKey = KeyCode.R;
    [SerializeField] private float _maxRewindTime = 10f;
    [SerializeField] private float _snapshotInterval = 0.1f;
    [SerializeField] private float _rewindSpeed = 2f;
    [SerializeField] private bool _pauseTimeWhileRewinding = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _rewindEffect;
    [SerializeField] private Material _rewindMaterial;
    [SerializeField] private Color _rewindTint = Color.cyan;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _rewindStartSound;
    [SerializeField] private AudioClip _rewindLoopSound;
    [SerializeField] private AudioClip _rewindEndSound;
    
    [Header("Events")]
    public UnityEvent OnRewindStart;
    public UnityEvent OnRewindEnd;
    public UnityEvent<float> OnRewindProgress;
    
    private List<TransformSnapshot> _transformSnapshots = new List<TransformSnapshot>();
    private List<RigidbodySnapshot> _rigidbodySnapshots = new List<RigidbodySnapshot>();
    private Dictionary<Renderer, Material[]> _originalMaterials = new Dictionary<Renderer, Material[]>();
    
    private bool _isRewinding = false;
    private float _lastSnapshotTime = 0f;
    private float _rewindStartTime = 0f;
    private float _originalTimeScale = 1f;
    private int _currentSnapshotIndex = 0;
    
    private Rigidbody _rigidbody;
    private Renderer[] _renderers;
    private Collider[] _colliders;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _renderers = GetComponentsInChildren<Renderer>();
        _colliders = GetComponentsInChildren<Collider>();
        
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        StoreOriginalMaterials();
        _originalTimeScale = Time.timeScale;
    }
    
    private void Update()
    {
        HandleInput();
        
        if (!_isRewinding)
        {
            RecordSnapshot();
            CleanupOldSnapshots();
        }
        else
        {
            ProcessRewind();
        }
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(_rewindKey) && !_isRewinding && _transformSnapshots.Count > 0)
        {
            StartRewind();
        }
        else if (Input.GetKeyUp(_rewindKey) && _isRewinding)
        {
            StopRewind();
        }
    }
    
    private void RecordSnapshot()
    {
        if (Time.time - _lastSnapshotTime >= _snapshotInterval)
        {
            _transformSnapshots.Add(new TransformSnapshot(transform, Time.time));
            
            if (_rigidbody != null)
            {
                _rigidbodySnapshots.Add(new RigidbodySnapshot(_rigidbody, Time.time));
            }
            
            _lastSnapshotTime = Time.time;
        }
    }
    
    private void CleanupOldSnapshots()
    {
        float cutoffTime = Time.time - _maxRewindTime;
        
        _transformSnapshots.RemoveAll(snapshot => snapshot.timestamp < cutoffTime);
        _rigidbodySnapshots.RemoveAll(snapshot => snapshot.timestamp < cutoffTime);
    }
    
    private void StartRewind()
    {
        if (_transformSnapshots.Count == 0) return;
        
        _isRewinding = true;
        _rewindStartTime = Time.time;
        _currentSnapshotIndex = _transformSnapshots.Count - 1;
        
        if (_pauseTimeWhileRewinding)
        {
            Time.timeScale = 0f;
        }
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = true;
        }
        
        SetCollidersEnabled(false);
        ApplyRewindVisuals();
        PlayRewindAudio(_rewindStartSound);
        
        if (_rewindEffect != null)
            _rewindEffect.Play();
        
        OnRewindStart?.Invoke();
    }
    
    private void ProcessRewind()
    {
        if (_currentSnapshotIndex < 0)
        {
            StopRewind();
            return;
        }
        
        float rewindProgress = 1f - (_currentSnapshotIndex / (float)_transformSnapshots.Count);
        OnRewindProgress?.Invoke(rewindProgress);
        
        TransformSnapshot targetSnapshot = _transformSnapshots[_currentSnapshotIndex];
        
        transform.position = targetSnapshot.position;
        transform.rotation = targetSnapshot.rotation;
        transform.localScale = targetSnapshot.scale;
        
        _currentSnapshotIndex -= Mathf.RoundToInt(_rewindSpeed);
        _currentSnapshotIndex = Mathf.Max(_currentSnapshotIndex, 0);
    }
    
    private void StopRewind()
    {
        if (!_isRewinding) return;
        
        _isRewinding = false;
        
        if (_pauseTimeWhileRewinding)
        {
            Time.timeScale = _originalTimeScale;
        }
        
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = false;
            RestoreRigidbodyState();
        }
        
        SetCollidersEnabled(true);
        RestoreOriginalVisuals();
        PlayRewindAudio(_rewindEndSound);
        
        if (_rewindEffect != null)
            _rewindEffect.Stop();
        
        ClearSnapshotsAfterRewind();
        OnRewindEnd?.Invoke();
    }
    
    private void RestoreRigidbodyState()
    {
        if (_rigidbody == null || _rigidbodySnapshots.Count == 0) return;
        
        int targetIndex = Mathf.Clamp(_currentSnapshotIndex, 0, _rigidbodySnapshots.Count - 1);
        RigidbodySnapshot targetState = _rigidbodySnapshots[targetIndex];
        
        _rigidbody.velocity = targetState.velocity;
        _rigidbody.angularVelocity = targetState.angularVelocity;
    }
    
    private void ClearSnapshotsAfterRewind()
    {
        if (_currentSnapshotIndex >= 0 && _currentSnapshotIndex < _transformSnapshots.Count)
        {
            _transformSnapshots.RemoveRange(_currentSnapshotIndex, _transformSnapshots.Count - _currentSnapshotIndex);
            
            if (_rigidbodySnapshots.Count > _currentSnapshotIndex)
            {
                _rigidbodySnapshots.RemoveRange(_currentSnapshotIndex, _rigidbodySnapshots.Count - _currentSnapshotIndex);
            }
        }
    }
    
    private void SetCollidersEnabled(bool enabled)
    {
        foreach (Collider col in _colliders)
        {
            if (col != null)
                col.enabled = enabled;
        }
    }
    
    private void StoreOriginalMaterials()
    {
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                _originalMaterials[renderer] = renderer.materials;
            }
        }
    }
    
    private void ApplyRewindVisuals()
    {
        if (_rewindMaterial == null) return;
        
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                Material[] rewindMaterials = new Material[renderer.materials.Length];
                for (int i = 0; i < rewindMaterials.Length; i++)
                {
                    rewindMaterials[i] = _rewindMaterial;
                }
                renderer.materials = rewindMaterials;
                renderer.material.color = _rewindTint;
            }
        }
    }
    
    private void RestoreOriginalVisuals()
    {
        foreach (var kvp in _originalMaterials)
        {
            if (kvp.Key != null)
            {
                kvp.Key.materials = kvp.Value;
            }
        }
    }
    
    private void PlayRewindAudio(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    public void TriggerRewind()
    {
        if (!_isRewinding && _transformSnapshots.Count > 0)
        {
            StartRewind();
        }
    }
    
    public void StopRewindManually()
    {
        if (_isRewinding)
        {
            StopRewind();
        }
    }
    
    public bool IsRewinding()
    {
        return _isRewinding;
    }
    
    public float GetRewindProgress()
    {
        if (!_isRewinding || _transformSnapshots.Count == 0) return 0f;
        return 1f - (_currentSnapshotIndex / (float)_transformSnapshots.Count);
    }
    
    public void ClearAllSnapshots()
    {
        _transformSnapshots.Clear();
        _rigidbodySnapshots.Clear();
    }
    
    private void OnDestroy()
    {
        if (_pauseTimeWhileRewinding && _isRewinding)
        {
            Time.timeScale = _originalTimeScale;
        }
    }
}