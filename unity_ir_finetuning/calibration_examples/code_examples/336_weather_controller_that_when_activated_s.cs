// Prompt: weather controller that when activated spawns rain particles falling downward, plays rain sound effects, makes all objects dampen (reduce bounce), changes ambient lighting to darker, and after 10 seconds spawns lightning bolts with thunder sounds
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

public class WeatherController : MonoBehaviour
{
    [Header("Rain Settings")]
    [SerializeField] private ParticleSystem _rainParticles;
    [SerializeField] private AudioSource _rainAudioSource;
    [SerializeField] private AudioClip _rainSoundClip;
    [SerializeField] private float _rainIntensity = 1000f;
    [SerializeField] private float _rainArea = 50f;
    
    [Header("Lightning Settings")]
    [SerializeField] private GameObject _lightningBoltPrefab;
    [SerializeField] private AudioSource _thunderAudioSource;
    [SerializeField] private AudioClip[] _thunderSoundClips;
    [SerializeField] private float _lightningDelay = 10f;
    [SerializeField] private float _lightningInterval = 3f;
    [SerializeField] private int _lightningCount = 5;
    [SerializeField] private float _lightningHeight = 20f;
    
    [Header("Lighting Settings")]
    [SerializeField] private Light _sunLight;
    [SerializeField] private Color _normalLightColor = Color.white;
    [SerializeField] private Color _stormLightColor = new Color(0.3f, 0.3f, 0.4f, 1f);
    [SerializeField] private float _normalLightIntensity = 1f;
    [SerializeField] private float _stormLightIntensity = 0.3f;
    [SerializeField] private float _lightTransitionSpeed = 2f;
    
    [Header("Physics Settings")]
    [SerializeField] private float _normalBounciness = 0.6f;
    [SerializeField] private float _wetBounciness = 0.2f;
    [SerializeField] private string[] _affectedTags = { "Ground", "Wall", "Platform" };
    
    [Header("Events")]
    public UnityEvent OnWeatherStart;
    public UnityEvent OnLightningStrike;
    public UnityEvent OnWeatherEnd;
    
    private bool _isWeatherActive = false;
    private List<PhysicMaterial> _originalMaterials = new List<PhysicMaterial>();
    private List<Collider> _affectedColliders = new List<Collider>();
    private Coroutine _weatherCoroutine;
    private Coroutine _lightningCoroutine;
    private Coroutine _lightTransitionCoroutine;
    
    private void Start()
    {
        SetupComponents();
        StoreOriginalMaterials();
    }
    
    private void SetupComponents()
    {
        if (_rainParticles == null)
        {
            GameObject rainObj = new GameObject("RainParticles");
            rainObj.transform.SetParent(transform);
            _rainParticles = rainObj.AddComponent<ParticleSystem>();
            SetupRainParticles();
        }
        
        if (_rainAudioSource == null)
        {
            _rainAudioSource = gameObject.AddComponent<AudioSource>();
            _rainAudioSource.loop = true;
            _rainAudioSource.volume = 0.5f;
        }
        
        if (_thunderAudioSource == null)
        {
            _thunderAudioSource = gameObject.AddComponent<AudioSource>();
            _thunderAudioSource.volume = 0.8f;
        }
        
        if (_sunLight == null)
        {
            _sunLight = FindObjectOfType<Light>();
        }
        
        if (_lightningBoltPrefab == null)
        {
            CreateLightningBoltPrefab();
        }
    }
    
    private void SetupRainParticles()
    {
        var main = _rainParticles.main;
        main.startLifetime = 2f;
        main.startSpeed = 10f;
        main.startSize = 0.1f;
        main.startColor = new Color(0.7f, 0.8f, 1f, 0.8f);
        main.maxParticles = (int)_rainIntensity;
        
        var emission = _rainParticles.emission;
        emission.rateOverTime = _rainIntensity / 2f;
        
        var shape = _rainParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(_rainArea, 1f, _rainArea);
        
        var velocityOverLifetime = _rainParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
        velocityOverLifetime.y = -15f;
        
        _rainParticles.transform.position = transform.position + Vector3.up * 15f;
    }
    
    private void CreateLightningBoltPrefab()
    {
        GameObject lightningObj = new GameObject("LightningBolt");
        LineRenderer lr = lightningObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.color = Color.white;
        lr.startWidth = 0.3f;
        lr.endWidth = 0.1f;
        lr.positionCount = 10;
        
        Light lightningLight = lightningObj.AddComponent<Light>();
        lightningLight.type = LightType.Point;
        lightningLight.color = Color.white;
        lightningLight.intensity = 8f;
        lightningLight.range = 30f;
        
        lightningObj.AddComponent<LightningBolt>();
        _lightningBoltPrefab = lightningObj;
    }
    
    private void StoreOriginalMaterials()
    {
        foreach (string tag in _affectedTags)
        {
            GameObject[] objects = GameObject.FindGameObjectsWithTag(tag);
            foreach (GameObject obj in objects)
            {
                Collider col = obj.GetComponent<Collider>();
                if (col != null)
                {
                    _affectedColliders.Add(col);
                    if (col.material != null)
                    {
                        _originalMaterials.Add(col.material);
                    }
                    else
                    {
                        _originalMaterials.Add(null);
                    }
                }
            }
        }
    }
    
    public void ActivateWeather()
    {
        if (_isWeatherActive) return;
        
        _isWeatherActive = true;
        _weatherCoroutine = StartCoroutine(WeatherSequence());
        OnWeatherStart?.Invoke();
    }
    
    public void DeactivateWeather()
    {
        if (!_isWeatherActive) return;
        
        _isWeatherActive = false;
        
        if (_weatherCoroutine != null)
            StopCoroutine(_weatherCoroutine);
        
        if (_lightningCoroutine != null)
            StopCoroutine(_lightningCoroutine);
        
        StopRain();
        RestorePhysics();
        RestoreLighting();
        
        OnWeatherEnd?.Invoke();
    }
    
    private IEnumerator WeatherSequence()
    {
        StartRain();
        DampenPhysics();
        DarkenLighting();
        
        yield return new WaitForSeconds(_lightningDelay);
        
        _lightningCoroutine = StartCoroutine(LightningSequence());
    }
    
    private void StartRain()
    {
        if (_rainParticles != null)
        {
            _rainParticles.Play();
        }
        
        if (_rainAudioSource != null && _rainSoundClip != null)
        {
            _rainAudioSource.clip = _rainSoundClip;
            _rainAudioSource.Play();
        }
    }
    
    private void StopRain()
    {
        if (_rainParticles != null)
        {
            _rainParticles.Stop();
        }
        
        if (_rainAudioSource != null)
        {
            _rainAudioSource.Stop();
        }
    }
    
    private void DampenPhysics()
    {
        PhysicMaterial wetMaterial = new PhysicMaterial("WetMaterial");
        wetMaterial.bounciness = _wetBounciness;
        wetMaterial.dynamicFriction = 0.8f;
        wetMaterial.staticFriction = 0.9f;
        
        foreach (Collider col in _affectedColliders)
        {
            if (col != null)
            {
                col.material = wetMaterial;
            }
        }
    }
    
    private void RestorePhysics()
    {
        for (int i = 0; i < _affectedColliders.Count && i < _originalMaterials.Count; i++)
        {
            if (_affectedColliders[i] != null)
            {
                _affectedColliders[i].material = _originalMaterials[i];
            }
        }
    }
    
    private void DarkenLighting()
    {
        if (_lightTransitionCoroutine != null)
            StopCoroutine(_lightTransitionCoroutine);
        
        _lightTransitionCoroutine = StartCoroutine(TransitionLighting(_stormLightColor, _stormLightIntensity));
    }
    
    private void RestoreLighting()
    {
        if (_lightTransitionCoroutine != null)
            StopCoroutine(_lightTransitionCoroutine);
        
        _lightTransitionCoroutine = StartCoroutine(TransitionLighting(_normalLightColor, _normalLightIntensity));
    }
    
    private IEnumerator TransitionLighting(Color targetColor, float targetIntensity)
    {
        if (_sunLight == null) yield break;
        
        Color startColor = _sunLight.color;
        float startIntensity = _sunLight.intensity;
        float elapsed = 0f;
        
        while (elapsed < 1f)
        {
            elapsed += Time.deltaTime * _lightTransitionSpeed;
            float t = Mathf.SmoothStep(0f, 1f, elapsed);
            
            _sunLight.color = Color.Lerp(startColor, targetColor, t);
            _sunLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);
            
            yield return null;
        }
        
        _sunLight.color = targetColor;
        _sunLight.intensity = targetIntensity;
    }
    
    private IEnumerator LightningSequence()
    {
        for (int i = 0; i < _lightningCount; i++)
        {
            if (!_isWeatherActive) yield break;
            
            SpawnLightning();
            yield return new WaitForSeconds(_lightningInterval + Random.Range(-1f, 1f));
        }
    }
    
    private void SpawnLightning()
    {
        if (_lightningBoltPrefab == null) return;
        
        Vector3 spawnPos = transform.position + new Vector3(
            Random.Range(-_rainArea / 2f, _rainArea / 2f),
            _lightningHeight,
            Random.Range(-_rainArea / 2f, _rainArea / 2f)
        );
        
        GameObject lightning = Instantiate(_lightningBoltPrefab, spawnPos, Quaternion.identity);
        
        if (_thunderAudioSource != null && _thunderSoundClips.Length > 0)
        {
            AudioClip thunderClip = _thunderSoundClips[Random.Range(0, _thunderSoundClips.Length)];
            _thunderAudioSource.PlayOneShot(thunderClip);
        }
        
        OnLightningStrike?.Invoke();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ActivateWeather();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireCube(transform.position, new Vector3(_rainArea, 1f, _rainArea));
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * _lightningHeight, 2f);
    }
}

public class LightningBolt : MonoBehaviour
{
    private LineRenderer _lineRenderer;
    private Light _lightningLight;
    private float _duration = 0.2f;
    private float _flickerTime = 0.05f;
    
    private void Start()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lightningLight = GetComponent<Light>();
        
        GenerateLightningPath();
        StartCoroutine(LightningEffect());
    }
    
    private void GenerateLightningPath()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = startPos + Vector3.down * 20f;
        
        Vector3[] positions = new Vector3[_lineRenderer.positionCount];
        
        for (int i = 0; i < positions.Length; i++)
        {
            float t = (float)i / (positions.Length - 1);
            Vector3 basePos = Vector3.Lerp(startPos, endPos, t);
            
            if (i > 0 && i < positions.Length - 1)
            {
                basePos += new Vector3(
                    Random.Range(-2f, 2f),
                    0f,
                    Random.Range(-2f, 2f)
                );
            }
            
            positions[i] = basePos;
        }
        
        _lineRenderer.SetPositions(positions);
    }
    
    private IEnumerator LightningEffect()
    {
        float elapsed = 0f;
        
        while (elapsed < _duration)
        {
            bool isVisible = (elapsed % _flickerTime) < (_flickerTime * 0.7f);
            _lineRenderer.enabled = isVisible;
            _lightningLight.enabled = isVisible;
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        Destroy(gameObject);
    }
}