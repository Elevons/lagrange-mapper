// Prompt: music that changes by area
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AreaMusicManager : MonoBehaviour
{
    [System.Serializable]
    public class MusicArea
    {
        [Header("Area Settings")]
        public string areaName;
        public AudioClip musicClip;
        public float volume = 1f;
        public bool loop = true;
        public float fadeInDuration = 2f;
        public float fadeOutDuration = 2f;
        
        [Header("Area Detection")]
        public Transform areaCenter;
        public float areaRadius = 10f;
        public LayerMask playerLayer = 1;
        
        [Header("Trigger Alternative")]
        public Collider areaTrigger;
    }

    [Header("Music Areas")]
    [SerializeField] private List<MusicArea> _musicAreas = new List<MusicArea>();
    
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _musicSource;
    [SerializeField] private float _globalVolume = 1f;
    [SerializeField] private bool _playOnStart = true;
    
    [Header("Default Music")]
    [SerializeField] private AudioClip _defaultMusicClip;
    [SerializeField] private float _defaultVolume = 0.5f;
    
    [Header("Detection Settings")]
    [SerializeField] private float _checkInterval = 0.5f;
    [SerializeField] private string _playerTag = "Player";
    
    private Transform _player;
    private MusicArea _currentArea;
    private Coroutine _fadeCoroutine;
    private bool _isTransitioning = false;
    private Dictionary<Collider, MusicArea> _triggerToAreaMap = new Dictionary<Collider, MusicArea>();

    private void Start()
    {
        InitializeAudioSource();
        FindPlayer();
        SetupTriggerMappings();
        
        if (_playOnStart)
        {
            PlayDefaultMusic();
        }
        
        StartCoroutine(CheckPlayerAreaRoutine());
    }

    private void InitializeAudioSource()
    {
        if (_musicSource == null)
        {
            _musicSource = GetComponent<AudioSource>();
            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
            }
        }
        
        _musicSource.playOnAwake = false;
        _musicSource.volume = 0f;
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag(_playerTag);
        if (playerObject != null)
        {
            _player = playerObject.transform;
        }
    }

    private void SetupTriggerMappings()
    {
        _triggerToAreaMap.Clear();
        
        foreach (var area in _musicAreas)
        {
            if (area.areaTrigger != null)
            {
                _triggerToAreaMap[area.areaTrigger] = area;
                
                if (area.areaTrigger.isTrigger == false)
                {
                    Debug.LogWarning($"Area trigger for {area.areaName} is not set as trigger!");
                }
            }
        }
    }

    private void PlayDefaultMusic()
    {
        if (_defaultMusicClip != null)
        {
            PlayMusic(_defaultMusicClip, _defaultVolume, true, 1f);
        }
    }

    private IEnumerator CheckPlayerAreaRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_checkInterval);
            
            if (_player != null && !_isTransitioning)
            {
                CheckPlayerArea();
            }
        }
    }

    private void CheckPlayerArea()
    {
        MusicArea newArea = GetPlayerCurrentArea();
        
        if (newArea != _currentArea)
        {
            ChangeToArea(newArea);
        }
    }

    private MusicArea GetPlayerCurrentArea()
    {
        Vector3 playerPosition = _player.position;
        
        foreach (var area in _musicAreas)
        {
            if (IsPlayerInArea(area, playerPosition))
            {
                return area;
            }
        }
        
        return null;
    }

    private bool IsPlayerInArea(MusicArea area, Vector3 playerPosition)
    {
        if (area.areaTrigger != null)
        {
            return area.areaTrigger.bounds.Contains(playerPosition);
        }
        
        if (area.areaCenter != null)
        {
            float distance = Vector3.Distance(playerPosition, area.areaCenter.position);
            return distance <= area.areaRadius;
        }
        
        return false;
    }

    private void ChangeToArea(MusicArea newArea)
    {
        _currentArea = newArea;
        
        if (newArea != null && newArea.musicClip != null)
        {
            PlayAreaMusic(newArea);
        }
        else
        {
            PlayDefaultMusic();
        }
    }

    private void PlayAreaMusic(MusicArea area)
    {
        float fadeIn = area.fadeInDuration;
        float fadeOut = _currentArea != null ? _currentArea.fadeOutDuration : 1f;
        
        PlayMusic(area.musicClip, area.volume, area.loop, fadeIn, fadeOut);
    }

    private void PlayMusic(AudioClip clip, float volume, bool loop, float fadeInDuration, float fadeOutDuration = 1f)
    {
        if (clip == null) return;
        
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        
        _fadeCoroutine = StartCoroutine(FadeToNewMusic(clip, volume, loop, fadeInDuration, fadeOutDuration));
    }

    private IEnumerator FadeToNewMusic(AudioClip newClip, float targetVolume, bool loop, float fadeInDuration, float fadeOutDuration)
    {
        _isTransitioning = true;
        
        // Fade out current music
        if (_musicSource.isPlaying)
        {
            float startVolume = _musicSource.volume;
            float fadeOutTime = 0f;
            
            while (fadeOutTime < fadeOutDuration)
            {
                fadeOutTime += Time.deltaTime;
                float t = fadeOutTime / fadeOutDuration;
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
                yield return null;
            }
        }
        
        // Change to new music
        _musicSource.clip = newClip;
        _musicSource.loop = loop;
        _musicSource.volume = 0f;
        _musicSource.Play();
        
        // Fade in new music
        float fadeInTime = 0f;
        float finalVolume = targetVolume * _globalVolume;
        
        while (fadeInTime < fadeInDuration)
        {
            fadeInTime += Time.deltaTime;
            float t = fadeInTime / fadeInDuration;
            _musicSource.volume = Mathf.Lerp(0f, finalVolume, t);
            yield return null;
        }
        
        _musicSource.volume = finalVolume;
        _isTransitioning = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag) && _triggerToAreaMap.ContainsKey(other))
        {
            MusicArea area = _triggerToAreaMap[other];
            if (area != _currentArea)
            {
                ChangeToArea(area);
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(_playerTag) && _triggerToAreaMap.ContainsKey(other))
        {
            MusicArea area = _triggerToAreaMap[other];
            if (area == _currentArea)
            {
                ChangeToArea(null);
            }
        }
    }

    public void SetGlobalVolume(float volume)
    {
        _globalVolume = Mathf.Clamp01(volume);
        if (_musicSource.isPlaying && _currentArea != null)
        {
            _musicSource.volume = _currentArea.volume * _globalVolume;
        }
    }

    public void StopMusic(float fadeOutDuration = 1f)
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
        }
        
        _fadeCoroutine = StartCoroutine(FadeOutMusic(fadeOutDuration));
    }

    private IEnumerator FadeOutMusic(float fadeOutDuration)
    {
        _isTransitioning = true;
        float startVolume = _musicSource.volume;
        float fadeTime = 0f;
        
        while (fadeTime < fadeOutDuration)
        {
            fadeTime += Time.deltaTime;
            float t = fadeTime / fadeOutDuration;
            _musicSource.volume = Mathf.Lerp(startVolume, 0f, t);
            yield return null;
        }
        
        _musicSource.Stop();
        _musicSource.volume = 0f;
        _currentArea = null;
        _isTransitioning = false;
    }

    public void ForceAreaChange(string areaName)
    {
        MusicArea area = _musicAreas.Find(a => a.areaName == areaName);
        if (area != null)
        {
            ChangeToArea(area);
        }
    }

    private void OnDrawGizmosSelected()
    {
        foreach (var area in _musicAreas)
        {
            if (area.areaCenter != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(area.areaCenter.position, area.areaRadius);
                
                Gizmos.color = Color.blue;
                Gizmos.DrawWireCube(area.areaCenter.position, Vector3.one * 0.5f);
            }
        }
    }
}