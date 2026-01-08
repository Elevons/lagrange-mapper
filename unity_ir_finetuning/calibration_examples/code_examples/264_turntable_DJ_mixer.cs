// Prompt: turntable DJ mixer
// Type: general

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Audio;
using System.Collections;

public class TurntableDJMixer : MonoBehaviour
{
    [System.Serializable]
    public class TurntableSettings
    {
        [Header("Turntable Properties")]
        public Transform turntablePlatter;
        public AudioSource audioSource;
        public AudioClip[] trackClips;
        [Range(0f, 2f)] public float pitchRange = 0.5f;
        [Range(0f, 1f)] public float volume = 1f;
        public bool isPlaying = false;
        public float currentRPM = 33.33f;
        public int currentTrackIndex = 0;
    }

    [System.Serializable]
    public class CrossfaderSettings
    {
        [Range(-1f, 1f)] public float position = 0f;
        public AnimationCurve crossfadeCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
    }

    [System.Serializable]
    public class EffectSettings
    {
        [Range(0f, 1f)] public float reverbAmount = 0f;
        [Range(0f, 1f)] public float distortionAmount = 0f;
        [Range(0f, 1f)] public float echoAmount = 0f;
        [Range(20f, 20000f)] public float lowPassFreq = 20000f;
        [Range(20f, 20000f)] public float highPassFreq = 20f;
    }

    [Header("Turntables")]
    [SerializeField] private TurntableSettings _leftTurntable;
    [SerializeField] private TurntableSettings _rightTurntable;

    [Header("Mixer Controls")]
    [SerializeField] private CrossfaderSettings _crossfader;
    [SerializeField] private Transform _crossfaderKnob;
    [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;
    [Range(0f, 1f)] [SerializeField] private float _leftChannelGain = 1f;
    [Range(0f, 1f)] [SerializeField] private float _rightChannelGain = 1f;

    [Header("Effects")]
    [SerializeField] private EffectSettings _leftEffects;
    [SerializeField] private EffectSettings _rightEffects;

    [Header("Visual Elements")]
    [SerializeField] private Transform _leftPitchSlider;
    [SerializeField] private Transform _rightPitchSlider;
    [SerializeField] private Light[] _vuMeterLights;
    [SerializeField] private Material _activeLightMaterial;
    [SerializeField] private Material _inactiveLightMaterial;

    [Header("Input Settings")]
    [SerializeField] private KeyCode _leftPlayKey = KeyCode.Q;
    [SerializeField] private KeyCode _rightPlayKey = KeyCode.P;
    [SerializeField] private KeyCode _leftNextTrackKey = KeyCode.W;
    [SerializeField] private KeyCode _rightNextTrackKey = KeyCode.O;

    [Header("Events")]
    public UnityEvent<int> OnLeftTrackChanged;
    public UnityEvent<int> OnRightTrackChanged;
    public UnityEvent<bool> OnLeftPlayStateChanged;
    public UnityEvent<bool> OnRightPlayStateChanged;
    public UnityEvent<float> OnCrossfaderChanged;

    private AudioLowPassFilter _leftLowPass;
    private AudioHighPassFilter _leftHighPass;
    private AudioReverbFilter _leftReverb;
    private AudioDistortionFilter _leftDistortion;
    private AudioEchoFilter _leftEcho;

    private AudioLowPassFilter _rightLowPass;
    private AudioHighPassFilter _rightHighPass;
    private AudioReverbFilter _rightReverb;
    private AudioDistortionFilter _rightDistortion;
    private AudioEchoFilter _rightEcho;

    private bool _isDraggingCrossfader = false;
    private bool _isDraggingLeftPitch = false;
    private bool _isDraggingRightPitch = false;

    private float _leftVuLevel = 0f;
    private float _rightVuLevel = 0f;

    private void Start()
    {
        InitializeTurntables();
        InitializeAudioEffects();
        InitializeVisuals();
    }

    private void InitializeTurntables()
    {
        if (_leftTurntable.audioSource == null)
            _leftTurntable.audioSource = gameObject.AddComponent<AudioSource>();
        if (_rightTurntable.audioSource == null)
            _rightTurntable.audioSource = gameObject.AddComponent<AudioSource>();

        _leftTurntable.audioSource.loop = true;
        _rightTurntable.audioSource.loop = true;
        _leftTurntable.audioSource.playOnAwake = false;
        _rightTurntable.audioSource.playOnAwake = false;

        if (_leftTurntable.trackClips != null && _leftTurntable.trackClips.Length > 0)
            _leftTurntable.audioSource.clip = _leftTurntable.trackClips[0];
        if (_rightTurntable.trackClips != null && _rightTurntable.trackClips.Length > 0)
            _rightTurntable.audioSource.clip = _rightTurntable.trackClips[0];
    }

    private void InitializeAudioEffects()
    {
        _leftLowPass = _leftTurntable.audioSource.gameObject.AddComponent<AudioLowPassFilter>();
        _leftHighPass = _leftTurntable.audioSource.gameObject.AddComponent<AudioHighPassFilter>();
        _leftReverb = _leftTurntable.audioSource.gameObject.AddComponent<AudioReverbFilter>();
        _leftDistortion = _leftTurntable.audioSource.gameObject.AddComponent<AudioDistortionFilter>();
        _leftEcho = _leftTurntable.audioSource.gameObject.AddComponent<AudioEchoFilter>();

        _rightLowPass = _rightTurntable.audioSource.gameObject.AddComponent<AudioLowPassFilter>();
        _rightHighPass = _rightTurntable.audioSource.gameObject.AddComponent<AudioHighPassFilter>();
        _rightReverb = _rightTurntable.audioSource.gameObject.AddComponent<AudioReverbFilter>();
        _rightDistortion = _rightTurntable.audioSource.gameObject.AddComponent<AudioDistortionFilter>();
        _rightEcho = _rightTurntable.audioSource.gameObject.AddComponent<AudioEchoFilter>();

        _leftReverb.enabled = false;
        _leftDistortion.enabled = false;
        _leftEcho.enabled = false;
        _rightReverb.enabled = false;
        _rightDistortion.enabled = false;
        _rightEcho.enabled = false;
    }

    private void InitializeVisuals()
    {
        if (_crossfaderKnob != null)
            _crossfaderKnob.localPosition = new Vector3(_crossfader.position, 0f, 0f);
    }

    private void Update()
    {
        HandleInput();
        UpdateTurntableRotation();
        UpdateAudioEffects();
        UpdateCrossfader();
        UpdateVUMeters();
        UpdateVisuals();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(_leftPlayKey))
            ToggleLeftTurntable();
        if (Input.GetKeyDown(_rightPlayKey))
            ToggleRightTurntable();
        if (Input.GetKeyDown(_leftNextTrackKey))
            NextLeftTrack();
        if (Input.GetKeyDown(_rightNextTrackKey))
            NextRightTrack();

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Crossfader"))
                    _isDraggingCrossfader = true;
                else if (hit.collider.CompareTag("LeftPitch"))
                    _isDraggingLeftPitch = true;
                else if (hit.collider.CompareTag("RightPitch"))
                    _isDraggingRightPitch = true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            _isDraggingCrossfader = false;
            _isDraggingLeftPitch = false;
            _isDraggingRightPitch = false;
        }

        if (_isDraggingCrossfader)
            UpdateCrossfaderFromMouse();
        if (_isDraggingLeftPitch)
            UpdateLeftPitchFromMouse();
        if (_isDraggingRightPitch)
            UpdateRightPitchFromMouse();
    }

    private void UpdateCrossfaderFromMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float newPosition = Mathf.Clamp((mousePos.x - transform.position.x) / 2f, -1f, 1f);
        SetCrossfaderPosition(newPosition);
    }

    private void UpdateLeftPitchFromMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float pitchValue = Mathf.Clamp((mousePos.y - transform.position.y) / 2f, -_leftTurntable.pitchRange, _leftTurntable.pitchRange);
        SetLeftTurntablePitch(1f + pitchValue);
    }

    private void UpdateRightPitchFromMouse()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        float pitchValue = Mathf.Clamp((mousePos.y - transform.position.y) / 2f, -_rightTurntable.pitchRange, _rightTurntable.pitchRange);
        SetRightTurntablePitch(1f + pitchValue);
    }

    private void UpdateTurntableRotation()
    {
        if (_leftTurntable.turntablePlatter != null && _leftTurntable.isPlaying)
        {
            float rotationSpeed = (_leftTurntable.currentRPM / 60f) * 360f * _leftTurntable.audioSource.pitch;
            _leftTurntable.turntablePlatter.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
        }

        if (_rightTurntable.turntablePlatter != null && _rightTurntable.isPlaying)
        {
            float rotationSpeed = (_rightTurntable.currentRPM / 60f) * 360f * _rightTurntable.audioSource.pitch;
            _rightTurntable.turntablePlatter.Rotate(0f, rotationSpeed * Time.deltaTime, 0f);
        }
    }

    private void UpdateAudioEffects()
    {
        UpdateEffectsForTurntable(_leftEffects, _leftLowPass, _leftHighPass, _leftReverb, _leftDistortion, _leftEcho);
        UpdateEffectsForTurntable(_rightEffects, _rightLowPass, _rightHighPass, _rightReverb, _rightDistortion, _rightEcho);
    }

    private void UpdateEffectsForTurntable(EffectSettings effects, AudioLowPassFilter lowPass, AudioHighPassFilter highPass,
        AudioReverbFilter reverb, AudioDistortionFilter distortion, AudioEchoFilter echo)
    {
        lowPass.cutoffFrequency = effects.lowPassFreq;
        highPass.cutoffFrequency = effects.highPassFreq;

        reverb.enabled = effects.reverbAmount > 0.01f;
        if (reverb.enabled)
            reverb.reverbLevel = Mathf.Lerp(-10000f, 0f, effects.reverbAmount);

        distortion.enabled = effects.distortionAmount > 0.01f;
        if (distortion.enabled)
            distortion.distortionLevel = effects.distortionAmount;

        echo.enabled = effects.echoAmount > 0.01f;
        if (echo.enabled)
        {
            echo.wetMix = effects.echoAmount;
            echo.delay = 500f;
            echo.decayRatio = 0.5f;
        }
    }

    private void UpdateCrossfader()
    {
        float leftVolume = _crossfader.crossfadeCurve.Evaluate(Mathf.Clamp01(1f + _crossfader.position));
        float rightVolume = _crossfader.crossfadeCurve.Evaluate(Mathf.Clamp01(1f - _crossfader.position));

        _leftTurntable.audioSource.volume = _leftTurntable.volume * _leftChannelGain * leftVolume * _masterVolume;
        _rightTurntable.audioSource.volume = _rightTurntable.volume * _rightChannelGain * rightVolume * _masterVolume;
    }

    private void UpdateVUMeters()
    {
        _leftVuLevel = GetAudioLevel(_leftTurntable.audioSource);
        _rightVuLevel = GetAudioLevel(_rightTurntable.audioSource);

        if (_vuMeterLights != null)
        {
            int leftLights = Mathf.RoundToInt(_leftVuLevel * (_vuMeterLights.Length / 2f));
            int rightLights = Mathf.RoundToInt(_rightVuLevel * (_vuMeterLights.Length / 2f));

            for (int i = 0; i < _vuMeterLights.Length / 2; i++)
            {
                if (_vuMeterLights[i] != null)
                {
                    _vuMeterLights[i].enabled = i < leftLights;
                    _vuMeterLights[i].color = i < leftLights ? Color.green : Color.black;
                }
            }

            for (int i = _vuMeterLights.Length / 2; i < _vuMeterLights.Length; i++)
            {
                if (_vuMeterLights[i] != null)
                {
                    int rightIndex = i - (_vuMeterLights.Length / 2);
                    _vuMeterLights[i].enabled = rightIndex < rightLights;
                    _vuMeterLights[i].color = rightIndex < rightLights ? Color.green : Color.black;
                }
            }
        }
    }

    private float GetAudioLevel(AudioSource audioSource)
    {
        if (audioSource == null || !audioSource.isPlaying)
            return 0f;

        float[] samples = new float[256];
        audioSource.GetOutputData(samples, 0);
        
        float sum = 0f;
        for (int i = 0; i < samples.Length; i++)
            sum += Mathf.Abs(samples[i]);
        
        return Mathf.Clamp01(sum / samples.Length * 10f);
    }

    private void UpdateVisuals()
    {
        if (_crossfaderKnob != null)
            _crossf