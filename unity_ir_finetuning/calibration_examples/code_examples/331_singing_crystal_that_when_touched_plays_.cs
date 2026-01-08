// Prompt: singing crystal that when touched plays a sequence of 5 musical notes in a chord progression, each note lights up a different part of the crystal, the entire crystal pulses with the rhythm, after the sequence completes it creates a burst of light particles
// Type: general

using UnityEngine;
using System.Collections;

public class SingingCrystal : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip[] _musicalNotes = new AudioClip[5];
    [SerializeField] private float _noteDuration = 0.8f;
    [SerializeField] private float _noteInterval = 0.1f;

    [Header("Visual Components")]
    [SerializeField] private Renderer[] _crystalParts = new Renderer[5];
    [SerializeField] private Renderer _mainCrystalRenderer;
    [SerializeField] private ParticleSystem _burstParticles;

    [Header("Lighting Effects")]
    [SerializeField] private Color[] _noteColors = new Color[5];
    [SerializeField] private Color _pulseColor = Color.white;
    [SerializeField] private float _pulseIntensity = 2f;
    [SerializeField] private AnimationCurve _pulseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Interaction Settings")]
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private bool _requirePlayerTag = true;

    private Material[] _originalMaterials;
    private Color[] _originalColors;
    private Material _mainMaterial;
    private Color _originalMainColor;
    private bool _isPlaying = false;
    private bool _onCooldown = false;
    private Coroutine _sequenceCoroutine;

    private void Start()
    {
        InitializeComponents();
        CacheOriginalMaterials();
        SetupDefaultColors();
    }

    private void InitializeComponents()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        if (_mainCrystalRenderer == null)
            _mainCrystalRenderer = GetComponent<Renderer>();

        if (_burstParticles == null)
            _burstParticles = GetComponentInChildren<ParticleSystem>();
    }

    private void CacheOriginalMaterials()
    {
        _originalMaterials = new Material[_crystalParts.Length];
        _originalColors = new Color[_crystalParts.Length];

        for (int i = 0; i < _crystalParts.Length; i++)
        {
            if (_crystalParts[i] != null)
            {
                _originalMaterials[i] = _crystalParts[i].material;
                _originalColors[i] = _originalMaterials[i].color;
            }
        }

        if (_mainCrystalRenderer != null)
        {
            _mainMaterial = _mainCrystalRenderer.material;
            _originalMainColor = _mainMaterial.color;
        }
    }

    private void SetupDefaultColors()
    {
        if (_noteColors.Length != 5)
        {
            _noteColors = new Color[5]
            {
                Color.red,
                Color.yellow,
                Color.green,
                Color.cyan,
                Color.magenta
            };
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isPlaying || _onCooldown)
            return;

        if (_requirePlayerTag && !other.CompareTag("Player"))
            return;

        StartMusicalSequence();
    }

    private void StartMusicalSequence()
    {
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);

        _sequenceCoroutine = StartCoroutine(PlayMusicalSequence());
    }

    private IEnumerator PlayMusicalSequence()
    {
        _isPlaying = true;

        for (int i = 0; i < 5; i++)
        {
            yield return StartCoroutine(PlayNote(i));
            yield return new WaitForSeconds(_noteInterval);
        }

        yield return StartCoroutine(CreateLightBurst());
        
        _isPlaying = false;
        yield return StartCoroutine(CooldownTimer());
    }

    private IEnumerator PlayNote(int noteIndex)
    {
        if (noteIndex >= _musicalNotes.Length || _musicalNotes[noteIndex] == null)
            yield break;

        _audioSource.PlayOneShot(_musicalNotes[noteIndex]);

        StartCoroutine(LightUpCrystalPart(noteIndex));
        StartCoroutine(PulseCrystal());

        yield return new WaitForSeconds(_noteDuration);
    }

    private IEnumerator LightUpCrystalPart(int partIndex)
    {
        if (partIndex >= _crystalParts.Length || _crystalParts[partIndex] == null)
            yield break;

        Material partMaterial = _crystalParts[partIndex].material;
        Color targetColor = _noteColors[partIndex];
        Color originalColor = _originalColors[partIndex];

        float elapsed = 0f;
        float duration = _noteDuration * 0.8f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float intensity = _pulseCurve.Evaluate(t);
            
            partMaterial.color = Color.Lerp(originalColor, targetColor * _pulseIntensity, intensity);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        partMaterial.color = originalColor;
    }

    private IEnumerator PulseCrystal()
    {
        if (_mainMaterial == null)
            yield break;

        float elapsed = 0f;
        float duration = _noteDuration;

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float intensity = _pulseCurve.Evaluate(t);
            
            _mainMaterial.color = Color.Lerp(_originalMainColor, _pulseColor * _pulseIntensity, intensity);
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        _mainMaterial.color = _originalMainColor;
    }

    private IEnumerator CreateLightBurst()
    {
        if (_burstParticles != null)
        {
            _burstParticles.Play();
        }

        StartCoroutine(FinalCrystalFlash());
        
        yield return new WaitForSeconds(1f);
    }

    private IEnumerator FinalCrystalFlash()
    {
        float flashDuration = 0.5f;
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            float t = elapsed / flashDuration;
            float intensity = Mathf.Sin(t * Mathf.PI);
            
            if (_mainMaterial != null)
            {
                _mainMaterial.color = Color.Lerp(_originalMainColor, Color.white * _pulseIntensity, intensity);
            }

            for (int i = 0; i < _crystalParts.Length; i++)
            {
                if (_crystalParts[i] != null)
                {
                    _crystalParts[i].material.color = Color.Lerp(_originalColors[i], Color.white * _pulseIntensity, intensity);
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ResetCrystalColors();
    }

    private void ResetCrystalColors()
    {
        if (_mainMaterial != null)
            _mainMaterial.color = _originalMainColor;

        for (int i = 0; i < _crystalParts.Length; i++)
        {
            if (_crystalParts[i] != null)
                _crystalParts[i].material.color = _originalColors[i];
        }
    }

    private IEnumerator CooldownTimer()
    {
        _onCooldown = true;
        yield return new WaitForSeconds(_cooldownTime);
        _onCooldown = false;
    }

    private void OnDestroy()
    {
        if (_sequenceCoroutine != null)
            StopCoroutine(_sequenceCoroutine);
    }
}