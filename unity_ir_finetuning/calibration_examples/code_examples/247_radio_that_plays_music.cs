// Prompt: radio that plays music
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Radio : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _musicTracks;
    [SerializeField] private float _volume = 0.7f;
    [SerializeField] private bool _playOnStart = false;
    [SerializeField] private bool _shuffleMode = false;
    [SerializeField] private bool _loopPlaylist = true;
    
    [Header("Interaction")]
    [SerializeField] private bool _canPlayerInteract = true;
    [SerializeField] private float _interactionRange = 3f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    
    [Header("Visual Feedback")]
    [SerializeField] private GameObject _powerIndicator;
    [SerializeField] private Light _radioLight;
    [SerializeField] private Color _onColor = Color.green;
    [SerializeField] private Color _offColor = Color.red;
    
    [Header("Events")]
    public UnityEvent OnRadioTurnedOn;
    public UnityEvent OnRadioTurnedOff;
    public UnityEvent OnTrackChanged;
    
    private bool _isPlaying = false;
    private int _currentTrackIndex = 0;
    private Transform _playerTransform;
    private bool _playerInRange = false;
    
    private void Start()
    {
        InitializeRadio();
        
        if (_playOnStart && _musicTracks.Length > 0)
        {
            TurnOn();
        }
        
        FindPlayer();
    }
    
    private void Update()
    {
        HandlePlayerInteraction();
        CheckTrackCompletion();
    }
    
    private void InitializeRadio()
    {
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _audioSource.volume = _volume;
        _audioSource.loop = false;
        _audioSource.playOnAwake = false;
        
        UpdateVisualFeedback();
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }
    
    private void HandlePlayerInteraction()
    {
        if (!_canPlayerInteract || _playerTransform == null) return;
        
        float distanceToPlayer = Vector3.Distance(transform.position, _playerTransform.position);
        _playerInRange = distanceToPlayer <= _interactionRange;
        
        if (_playerInRange && Input.GetKeyDown(_interactionKey))
        {
            ToggleRadio();
        }
    }
    
    private void CheckTrackCompletion()
    {
        if (_isPlaying && !_audioSource.isPlaying && _musicTracks.Length > 0)
        {
            PlayNextTrack();
        }
    }
    
    public void ToggleRadio()
    {
        if (_isPlaying)
        {
            TurnOff();
        }
        else
        {
            TurnOn();
        }
    }
    
    public void TurnOn()
    {
        if (_musicTracks.Length == 0) return;
        
        _isPlaying = true;
        PlayCurrentTrack();
        UpdateVisualFeedback();
        OnRadioTurnedOn?.Invoke();
    }
    
    public void TurnOff()
    {
        _isPlaying = false;
        _audioSource.Stop();
        UpdateVisualFeedback();
        OnRadioTurnedOff?.Invoke();
    }
    
    private void PlayCurrentTrack()
    {
        if (_musicTracks.Length == 0 || _currentTrackIndex >= _musicTracks.Length) return;
        
        _audioSource.clip = _musicTracks[_currentTrackIndex];
        _audioSource.Play();
        OnTrackChanged?.Invoke();
    }
    
    private void PlayNextTrack()
    {
        if (_shuffleMode)
        {
            _currentTrackIndex = Random.Range(0, _musicTracks.Length);
        }
        else
        {
            _currentTrackIndex++;
            if (_currentTrackIndex >= _musicTracks.Length)
            {
                if (_loopPlaylist)
                {
                    _currentTrackIndex = 0;
                }
                else
                {
                    TurnOff();
                    return;
                }
            }
        }
        
        PlayCurrentTrack();
    }
    
    public void NextTrack()
    {
        if (!_isPlaying || _musicTracks.Length <= 1) return;
        
        _audioSource.Stop();
        PlayNextTrack();
    }
    
    public void PreviousTrack()
    {
        if (!_isPlaying || _musicTracks.Length <= 1) return;
        
        _currentTrackIndex--;
        if (_currentTrackIndex < 0)
        {
            _currentTrackIndex = _musicTracks.Length - 1;
        }
        
        _audioSource.Stop();
        PlayCurrentTrack();
    }
    
    public void SetVolume(float volume)
    {
        _volume = Mathf.Clamp01(volume);
        _audioSource.volume = _volume;
    }
    
    public void SetShuffleMode(bool shuffle)
    {
        _shuffleMode = shuffle;
    }
    
    public void SetLoopMode(bool loop)
    {
        _loopPlaylist = loop;
    }
    
    private void UpdateVisualFeedback()
    {
        if (_powerIndicator != null)
        {
            _powerIndicator.SetActive(_isPlaying);
        }
        
        if (_radioLight != null)
        {
            _radioLight.color = _isPlaying ? _onColor : _offColor;
            _radioLight.enabled = _isPlaying;
        }
    }
    
    public bool IsPlaying => _isPlaying;
    public string CurrentTrackName => _musicTracks.Length > 0 && _currentTrackIndex < _musicTracks.Length ? _musicTracks[_currentTrackIndex].name : "None";
    public int CurrentTrackIndex => _currentTrackIndex;
    public int TotalTracks => _musicTracks.Length;
    
    private void OnDrawGizmosSelected()
    {
        if (_canPlayerInteract)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _interactionRange);
        }
    }
}