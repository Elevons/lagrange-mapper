// Prompt: voice chat proximity
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class ProximityVoiceChat : MonoBehaviour
{
    [System.Serializable]
    public class VoiceSettings
    {
        [Range(0f, 100f)]
        public float maxHearingDistance = 20f;
        [Range(0f, 100f)]
        public float whisperDistance = 5f;
        [Range(0f, 100f)]
        public float shoutDistance = 50f;
        [Range(0f, 1f)]
        public float minVolume = 0.1f;
        [Range(0f, 1f)]
        public float maxVolume = 1f;
        public AnimationCurve volumeFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    }

    [System.Serializable]
    public class PlayerVoiceData
    {
        public Transform playerTransform;
        public AudioSource audioSource;
        public AudioClip voiceClip;
        public bool isTalking;
        public float talkingVolume;
        public VoiceMode currentMode;
        
        public PlayerVoiceData(Transform transform, AudioSource source)
        {
            playerTransform = transform;
            audioSource = source;
            isTalking = false;
            talkingVolume = 1f;
            currentMode = VoiceMode.Normal;
        }
    }

    public enum VoiceMode
    {
        Whisper,
        Normal,
        Shout
    }

    [Header("Voice Settings")]
    [SerializeField] private VoiceSettings _voiceSettings = new VoiceSettings();
    
    [Header("Audio Configuration")]
    [SerializeField] private AudioMixerGroup _voiceMixerGroup;
    [SerializeField] private bool _use3DAudio = true;
    [SerializeField] private AudioRolloffMode _rolloffMode = AudioRolloffMode.Linear;
    
    [Header("Detection")]
    [SerializeField] private LayerMask _playerLayer = -1;
    [SerializeField] private LayerMask _obstacleLayer = -1;
    [SerializeField] private bool _checkLineOfSight = true;
    [SerializeField] private float _updateRate = 10f;
    
    [Header("Events")]
    public UnityEvent<Transform, float> OnPlayerVoiceHeard;
    public UnityEvent<Transform> OnPlayerStartedTalking;
    public UnityEvent<Transform> OnPlayerStoppedTalking;

    private Dictionary<Transform, PlayerVoiceData> _nearbyPlayers = new Dictionary<Transform, PlayerVoiceData>();
    private Transform _localPlayer;
    private float _updateTimer;
    private bool _isLocalPlayerTalking;
    private VoiceMode _currentVoiceMode = VoiceMode.Normal;

    private void Start()
    {
        _localPlayer = transform;
        _updateTimer = 0f;
        
        if (_voiceSettings.volumeFalloffCurve.keys.Length == 0)
        {
            _voiceSettings.volumeFalloffCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        }
    }

    private void Update()
    {
        _updateTimer += Time.deltaTime;
        
        if (_updateTimer >= 1f / _updateRate)
        {
            UpdateProximityVoice();
            _updateTimer = 0f;
        }

        HandleVoiceInput();
        UpdateVoiceVisualization();
    }

    private void HandleVoiceInput()
    {
        bool wasTalking = _isLocalPlayerTalking;
        
        // Simulate voice input detection (replace with actual microphone input)
        _isLocalPlayerTalking = Input.GetKey(KeyCode.V) || Input.GetKey(KeyCode.T);
        
        // Voice mode switching
        if (Input.GetKeyDown(KeyCode.Alpha1))
            _currentVoiceMode = VoiceMode.Whisper;
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            _currentVoiceMode = VoiceMode.Normal;
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            _currentVoiceMode = VoiceMode.Shout;

        if (_isLocalPlayerTalking && !wasTalking)
        {
            OnPlayerStartedTalking?.Invoke(_localPlayer);
            BroadcastVoiceToNearbyPlayers(true);
        }
        else if (!_isLocalPlayerTalking && wasTalking)
        {
            OnPlayerStoppedTalking?.Invoke(_localPlayer);
            BroadcastVoiceToNearbyPlayers(false);
        }
    }

    private void UpdateProximityVoice()
    {
        DetectNearbyPlayers();
        UpdatePlayerVoiceVolumes();
        CleanupDistantPlayers();
    }

    private void DetectNearbyPlayers()
    {
        Collider[] nearbyColliders = Physics.OverlapSphere(
            _localPlayer.position, 
            _voiceSettings.maxHearingDistance, 
            _playerLayer
        );

        foreach (Collider col in nearbyColliders)
        {
            if (col.transform == _localPlayer) continue;
            
            if (!col.CompareTag("Player")) continue;

            if (!_nearbyPlayers.ContainsKey(col.transform))
            {
                AddNewPlayer(col.transform);
            }
        }
    }

    private void AddNewPlayer(Transform playerTransform)
    {
        AudioSource audioSource = playerTransform.GetComponent<AudioSource>();
        
        if (audioSource == null)
        {
            audioSource = playerTransform.gameObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(audioSource);
        
        PlayerVoiceData voiceData = new PlayerVoiceData(playerTransform, audioSource);
        _nearbyPlayers.Add(playerTransform, voiceData);
    }

    private void ConfigureAudioSource(AudioSource audioSource)
    {
        audioSource.spatialBlend = _use3DAudio ? 1f : 0f;
        audioSource.rolloffMode = _rolloffMode;
        audioSource.maxDistance = _voiceSettings.maxHearingDistance;
        audioSource.minDistance = 1f;
        audioSource.loop = false;
        audioSource.playOnAwake = false;
        
        if (_voiceMixerGroup != null)
        {
            audioSource.outputAudioMixerGroup = _voiceMixerGroup;
        }
    }

    private void UpdatePlayerVoiceVolumes()
    {
        List<Transform> playersToRemove = new List<Transform>();

        foreach (var kvp in _nearbyPlayers)
        {
            Transform playerTransform = kvp.Key;
            PlayerVoiceData voiceData = kvp.Value;

            if (playerTransform == null)
            {
                playersToRemove.Add(playerTransform);
                continue;
            }

            float distance = Vector3.Distance(_localPlayer.position, playerTransform.position);
            float maxDistance = GetMaxDistanceForMode(voiceData.currentMode);

            if (distance > maxDistance)
            {
                playersToRemove.Add(playerTransform);
                continue;
            }

            float volume = CalculateVoiceVolume(distance, voiceData.currentMode);
            
            if (_checkLineOfSight && !HasLineOfSight(playerTransform.position))
            {
                volume *= 0.5f; // Muffle voice through obstacles
            }

            voiceData.audioSource.volume = volume * voiceData.talkingVolume;
            
            if (voiceData.isTalking && volume > 0f)
            {
                OnPlayerVoiceHeard?.Invoke(playerTransform, volume);
            }
        }

        foreach (Transform player in playersToRemove)
        {
            RemovePlayer(player);
        }
    }

    private float GetMaxDistanceForMode(VoiceMode mode)
    {
        switch (mode)
        {
            case VoiceMode.Whisper:
                return _voiceSettings.whisperDistance;
            case VoiceMode.Shout:
                return _voiceSettings.shoutDistance;
            default:
                return _voiceSettings.maxHearingDistance;
        }
    }

    private float CalculateVoiceVolume(float distance, VoiceMode mode)
    {
        float maxDistance = GetMaxDistanceForMode(mode);
        
        if (distance >= maxDistance)
            return 0f;

        float normalizedDistance = distance / maxDistance;
        float volumeMultiplier = _voiceSettings.volumeFalloffCurve.Evaluate(1f - normalizedDistance);
        
        return Mathf.Lerp(_voiceSettings.minVolume, _voiceSettings.maxVolume, volumeMultiplier);
    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - _localPlayer.position;
        float distance = direction.magnitude;
        
        if (Physics.Raycast(_localPlayer.position + Vector3.up * 0.5f, direction.normalized, distance, _obstacleLayer))
        {
            return false;
        }
        
        return true;
    }

    private void CleanupDistantPlayers()
    {
        List<Transform> playersToRemove = new List<Transform>();

        foreach (var kvp in _nearbyPlayers)
        {
            Transform playerTransform = kvp.Key;
            
            if (playerTransform == null)
            {
                playersToRemove.Add(playerTransform);
                continue;
            }

            float distance = Vector3.Distance(_localPlayer.position, playerTransform.position);
            
            if (distance > _voiceSettings.maxHearingDistance * 1.2f) // Add buffer to prevent flickering
            {
                playersToRemove.Add(playerTransform);
            }
        }

        foreach (Transform player in playersToRemove)
        {
            RemovePlayer(player);
        }
    }

    private void RemovePlayer(Transform playerTransform)
    {
        if (_nearbyPlayers.ContainsKey(playerTransform))
        {
            PlayerVoiceData voiceData = _nearbyPlayers[playerTransform];
            
            if (voiceData.audioSource != null && voiceData.audioSource.isPlaying)
            {
                voiceData.audioSource.Stop();
            }
            
            _nearbyPlayers.Remove(playerTransform);
        }
    }

    private void BroadcastVoiceToNearbyPlayers(bool isTalking)
    {
        // This would typically send network messages to other players
        // For now, we'll simulate by updating local data
        foreach (var kvp in _nearbyPlayers)
        {
            PlayerVoiceData voiceData = kvp.Value;
            // In a real implementation, this would be received from network
        }
    }

    private void UpdateVoiceVisualization()
    {
        // Visual feedback for current voice mode and talking state
        // This could update UI elements or particle effects
    }

    public void SetPlayerTalking(Transform player, bool isTalking, VoiceMode mode = VoiceMode.Normal)
    {
        if (_nearbyPlayers.ContainsKey(player))
        {
            PlayerVoiceData voiceData = _nearbyPlayers[player];
            voiceData.isTalking = isTalking;
            voiceData.currentMode = mode;

            if (isTalking)
            {
                OnPlayerStartedTalking?.Invoke(player);
            }
            else
            {
                OnPlayerStoppedTalking?.Invoke(player);
                if (voiceData.audioSource.isPlaying)
                {
                    voiceData.audioSource.Stop();
                }
            }
        }
    }

    public void PlayVoiceClip(Transform player, AudioClip clip, float volume = 1f)
    {
        if (_nearbyPlayers.ContainsKey(player))
        {
            PlayerVoiceData voiceData = _nearbyPlayers[player];
            voiceData.audioSource.clip = clip;
            voiceData.talkingVolume = volume;
            voiceData.audioSource.Play();
        }
    }

    public void SetVoiceMode(VoiceMode mode)
    {
        _currentVoiceMode = mode;
    }

    public VoiceMode GetCurrentVoiceMode()
    {
        return _currentVoiceMode;
    }

    public bool IsPlayerInRange(Transform player)
    {
        return _nearbyPlayers.ContainsKey(player);
    }

    public float GetPlayerDistance(Transform player)
    {
        if (player == null) return float.MaxValue;
        return Vector3.Distance(_localPlayer.position, player.position);
    }

    private void OnDrawGizmosSelected()
    {
        if (_localPlayer == null) return;

        // Draw hearing ranges
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(_localPlayer.position, _voiceSettings.whisperDistance);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_localPlayer.position, _voiceSettings.maxHearingDistance);
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(_localPlayer.position, _voiceSettings.shoutDistance);

        // Draw connections to nearby players
        Gizmos.color = Color.cyan;
        foreach (var kvp in _nearbyPlayers)
        {
            if (kvp.Key != null)
            {
                Gizmos.DrawLine(_localPlayer.position, kvp.Key.position);
            }
        }
    }
}