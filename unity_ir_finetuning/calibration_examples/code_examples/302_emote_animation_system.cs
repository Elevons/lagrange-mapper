// Prompt: emote animation system
// Type: general

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EmoteAnimationSystem : MonoBehaviour
{
    [System.Serializable]
    public class EmoteData
    {
        [Header("Emote Settings")]
        public string emoteName;
        public KeyCode triggerKey;
        public AnimationClip animationClip;
        public float duration = 2f;
        public bool canInterrupt = true;
        public bool lockMovement = false;
        
        [Header("Audio")]
        public AudioClip soundEffect;
        [Range(0f, 1f)]
        public float volume = 1f;
        
        [Header("Effects")]
        public GameObject particleEffect;
        public Transform effectSpawnPoint;
    }

    [Header("Emote Configuration")]
    [SerializeField] private List<EmoteData> _availableEmotes = new List<EmoteData>();
    [SerializeField] private bool _enableKeyboardInput = true;
    [SerializeField] private float _cooldownTime = 0.5f;
    
    [Header("Animation Settings")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string _emoteParameterName = "EmoteTrigger";
    [SerializeField] private string _emoteIndexParameterName = "EmoteIndex";
    [SerializeField] private bool _useAnimatorParameters = true;
    
    [Header("Movement Control")]
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private bool _freezePositionDuringEmote = true;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    
    [Header("Events")]
    public UnityEvent<string> OnEmoteStarted;
    public UnityEvent<string> OnEmoteFinished;
    public UnityEvent OnEmoteInterrupted;

    private EmoteData _currentEmote;
    private bool _isEmoting = false;
    private float _lastEmoteTime = 0f;
    private Coroutine _emoteCoroutine;
    private Vector3 _originalPosition;
    private bool _wasKinematic = false;
    private Dictionary<string, int> _emoteIndexMap = new Dictionary<string, int>();

    private void Start()
    {
        InitializeComponents();
        BuildEmoteIndexMap();
    }

    private void InitializeComponents()
    {
        if (_animator == null)
            _animator = GetComponent<Animator>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();
            
        if (_characterController == null)
            _characterController = GetComponent<CharacterController>();
    }

    private void BuildEmoteIndexMap()
    {
        _emoteIndexMap.Clear();
        for (int i = 0; i < _availableEmotes.Count; i++)
        {
            if (!string.IsNullOrEmpty(_availableEmotes[i].emoteName))
            {
                _emoteIndexMap[_availableEmotes[i].emoteName] = i;
            }
        }
    }

    private void Update()
    {
        if (_enableKeyboardInput)
        {
            HandleKeyboardInput();
        }
    }

    private void HandleKeyboardInput()
    {
        if (Time.time - _lastEmoteTime < _cooldownTime)
            return;

        foreach (var emote in _availableEmotes)
        {
            if (Input.GetKeyDown(emote.triggerKey))
            {
                PlayEmote(emote.emoteName);
                break;
            }
        }
    }

    public void PlayEmote(string emoteName)
    {
        if (string.IsNullOrEmpty(emoteName))
            return;

        var emoteData = GetEmoteByName(emoteName);
        if (emoteData == null)
        {
            Debug.LogWarning($"Emote '{emoteName}' not found!");
            return;
        }

        PlayEmote(emoteData);
    }

    public void PlayEmoteByIndex(int index)
    {
        if (index < 0 || index >= _availableEmotes.Count)
        {
            Debug.LogWarning($"Emote index {index} is out of range!");
            return;
        }

        PlayEmote(_availableEmotes[index]);
    }

    private void PlayEmote(EmoteData emoteData)
    {
        if (emoteData == null)
            return;

        if (Time.time - _lastEmoteTime < _cooldownTime)
            return;

        if (_isEmoting && !_currentEmote.canInterrupt)
            return;

        if (_isEmoting)
        {
            StopCurrentEmote(true);
        }

        StartEmote(emoteData);
    }

    private void StartEmote(EmoteData emoteData)
    {
        _currentEmote = emoteData;
        _isEmoting = true;
        _lastEmoteTime = Time.time;

        if (_freezePositionDuringEmote && emoteData.lockMovement)
        {
            FreezeMovement();
        }

        PlayEmoteAnimation(emoteData);
        PlayEmoteAudio(emoteData);
        SpawnEmoteEffect(emoteData);

        OnEmoteStarted?.Invoke(emoteData.emoteName);

        _emoteCoroutine = StartCoroutine(EmoteCoroutine(emoteData));
    }

    private void PlayEmoteAnimation(EmoteData emoteData)
    {
        if (_animator == null)
            return;

        if (_useAnimatorParameters)
        {
            if (_animator.parameters != null)
            {
                foreach (var param in _animator.parameters)
                {
                    if (param.name == _emoteParameterName && param.type == AnimatorControllerParameterType.Trigger)
                    {
                        _animator.SetTrigger(_emoteParameterName);
                    }
                    else if (param.name == _emoteIndexParameterName && param.type == AnimatorControllerParameterType.Int)
                    {
                        if (_emoteIndexMap.ContainsKey(emoteData.emoteName))
                        {
                            _animator.SetInteger(_emoteIndexParameterName, _emoteIndexMap[emoteData.emoteName]);
                        }
                    }
                }
            }
        }
        else if (emoteData.animationClip != null)
        {
            _animator.Play(emoteData.animationClip.name);
        }
    }

    private void PlayEmoteAudio(EmoteData emoteData)
    {
        if (_audioSource != null && emoteData.soundEffect != null)
        {
            _audioSource.clip = emoteData.soundEffect;
            _audioSource.volume = emoteData.volume;
            _audioSource.Play();
        }
    }

    private void SpawnEmoteEffect(EmoteData emoteData)
    {
        if (emoteData.particleEffect != null)
        {
            Transform spawnPoint = emoteData.effectSpawnPoint != null ? emoteData.effectSpawnPoint : transform;
            GameObject effect = Instantiate(emoteData.particleEffect, spawnPoint.position, spawnPoint.rotation);
            
            if (emoteData.duration > 0)
            {
                Destroy(effect, emoteData.duration + 1f);
            }
        }
    }

    private void FreezeMovement()
    {
        _originalPosition = transform.position;

        if (_rigidbody != null)
        {
            _wasKinematic = _rigidbody.isKinematic;
            _rigidbody.isKinematic = true;
            _rigidbody.velocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }
    }

    private void UnfreezeMovement()
    {
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = _wasKinematic;
        }
    }

    private IEnumerator EmoteCoroutine(EmoteData emoteData)
    {
        yield return new WaitForSeconds(emoteData.duration);
        FinishEmote();
    }

    private void FinishEmote()
    {
        if (!_isEmoting)
            return;

        string emoteName = _currentEmote != null ? _currentEmote.emoteName : "";
        
        if (_currentEmote != null && _currentEmote.lockMovement)
        {
            UnfreezeMovement();
        }

        _isEmoting = false;
        _currentEmote = null;

        OnEmoteFinished?.Invoke(emoteName);
    }

    public void StopCurrentEmote(bool wasInterrupted = false)
    {
        if (!_isEmoting)
            return;

        if (_emoteCoroutine != null)
        {
            StopCoroutine(_emoteCoroutine);
            _emoteCoroutine = null;
        }

        if (_currentEmote != null && _currentEmote.lockMovement)
        {
            UnfreezeMovement();
        }

        if (wasInterrupted)
        {
            OnEmoteInterrupted?.Invoke();
        }

        _isEmoting = false;
        _currentEmote = null;
    }

    private EmoteData GetEmoteByName(string emoteName)
    {
        foreach (var emote in _availableEmotes)
        {
            if (emote.emoteName.Equals(emoteName, System.StringComparison.OrdinalIgnoreCase))
            {
                return emote;
            }
        }
        return null;
    }

    public bool IsEmoting()
    {
        return _isEmoting;
    }

    public string GetCurrentEmoteName()
    {
        return _currentEmote != null ? _currentEmote.emoteName : "";
    }

    public List<string> GetAvailableEmoteNames()
    {
        List<string> names = new List<string>();
        foreach (var emote in _availableEmotes)
        {
            if (!string.IsNullOrEmpty(emote.emoteName))
            {
                names.Add(emote.emoteName);
            }
        }
        return names;
    }

    public void AddEmote(EmoteData newEmote)
    {
        if (newEmote != null && !string.IsNullOrEmpty(newEmote.emoteName))
        {
            _availableEmotes.Add(newEmote);
            BuildEmoteIndexMap();
        }
    }

    public void RemoveEmote(string emoteName)
    {
        for (int i = _availableEmotes.Count - 1; i >= 0; i--)
        {
            if (_availableEmotes[i].emoteName.Equals(emoteName, System.StringComparison.OrdinalIgnoreCase))
            {
                _availableEmotes.RemoveAt(i);
                BuildEmoteIndexMap();
                break;
            }
        }
    }

    private void OnDisable()
    {
        StopCurrentEmote();
    }
}