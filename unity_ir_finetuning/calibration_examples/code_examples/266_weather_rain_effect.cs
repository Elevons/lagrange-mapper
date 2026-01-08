// Prompt: weather rain effect
// Type: combat

using UnityEngine;
using System.Collections.Generic;

public class WeatherRainEffect : MonoBehaviour
{
    [Header("Rain Settings")]
    [SerializeField] private GameObject _rainDropPrefab;
    [SerializeField] private int _maxRainDrops = 1000;
    [SerializeField] private float _rainIntensity = 0.5f;
    [SerializeField] private Vector3 _rainAreaSize = new Vector3(50f, 20f, 50f);
    [SerializeField] private float _rainDropSpeed = 10f;
    [SerializeField] private Vector3 _windDirection = new Vector3(0.1f, 0f, 0f);
    
    [Header("Rain Drop Properties")]
    [SerializeField] private float _dropLifetime = 5f;
    [SerializeField] private float _dropScale = 1f;
    [SerializeField] private LayerMask _groundLayer = 1;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _rainAudioSource;
    [SerializeField] private AudioClip _rainSoundClip;
    [SerializeField] private float _audioVolume = 0.3f;
    
    [Header("Particle System")]
    [SerializeField] private ParticleSystem _rainParticleSystem;
    [SerializeField] private bool _useParticleSystem = true;
    
    private List<RainDrop> _activeRainDrops = new List<RainDrop>();
    private Queue<RainDrop> _rainDropPool = new Queue<RainDrop>();
    private Transform _playerTransform;
    private Camera _mainCamera;
    private float _spawnTimer;
    private bool _isRaining = true;
    
    [System.Serializable]
    private class RainDrop
    {
        public GameObject gameObject;
        public Transform transform;
        public Rigidbody rigidbody;
        public float lifetime;
        public bool isActive;
        
        public RainDrop(GameObject obj)
        {
            gameObject = obj;
            transform = obj.transform;
            rigidbody = obj.GetComponent<Rigidbody>();
            lifetime = 0f;
            isActive = false;
        }
    }
    
    void Start()
    {
        InitializeRainSystem();
        SetupAudio();
        SetupParticleSystem();
        FindPlayerAndCamera();
    }
    
    void Update()
    {
        if (!_isRaining) return;
        
        UpdatePlayerPosition();
        SpawnRainDrops();
        UpdateRainDrops();
        UpdateAudio();
    }
    
    private void InitializeRainSystem()
    {
        if (_rainDropPrefab == null)
        {
            CreateDefaultRainDropPrefab();
        }
        
        PrewarmRainDropPool();
    }
    
    private void CreateDefaultRainDropPrefab()
    {
        _rainDropPrefab = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        _rainDropPrefab.transform.localScale = new Vector3(0.02f, 0.1f, 0.02f);
        
        Renderer renderer = _rainDropPrefab.GetComponent<Renderer>();
        Material rainMaterial = new Material(Shader.Find("Standard"));
        rainMaterial.color = new Color(0.7f, 0.8f, 1f, 0.6f);
        rainMaterial.SetFloat("_Mode", 3);
        rainMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        rainMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        rainMaterial.SetInt("_ZWrite", 0);
        rainMaterial.DisableKeyword("_ALPHATEST_ON");
        rainMaterial.EnableKeyword("_ALPHABLEND_ON");
        rainMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        rainMaterial.renderQueue = 3000;
        renderer.material = rainMaterial;
        
        Rigidbody rb = _rainDropPrefab.GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.drag = 0f;
        
        _rainDropPrefab.SetActive(false);
    }
    
    private void PrewarmRainDropPool()
    {
        for (int i = 0; i < _maxRainDrops; i++)
        {
            GameObject dropObj = Instantiate(_rainDropPrefab, transform);
            RainDrop rainDrop = new RainDrop(dropObj);
            _rainDropPool.Enqueue(rainDrop);
        }
    }
    
    private void SetupAudio()
    {
        if (_rainAudioSource == null)
        {
            _rainAudioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _rainAudioSource.clip = _rainSoundClip;
        _rainAudioSource.loop = true;
        _rainAudioSource.volume = _audioVolume;
        _rainAudioSource.spatialBlend = 0f;
        
        if (_rainSoundClip != null && _isRaining)
        {
            _rainAudioSource.Play();
        }
    }
    
    private void SetupParticleSystem()
    {
        if (_useParticleSystem && _rainParticleSystem != null)
        {
            var main = _rainParticleSystem.main;
            main.startLifetime = _dropLifetime;
            main.startSpeed = _rainDropSpeed;
            main.maxParticles = _maxRainDrops;
            
            var emission = _rainParticleSystem.emission;
            emission.rateOverTime = _maxRainDrops * _rainIntensity / _dropLifetime;
            
            var shape = _rainParticleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = _rainAreaSize;
            
            var velocityOverLifetime = _rainParticleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = _windDirection.x * _rainDropSpeed;
            velocityOverLifetime.y = -_rainDropSpeed;
            velocityOverLifetime.z = _windDirection.z * _rainDropSpeed;
        }
    }
    
    private void FindPlayerAndCamera()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
        
        _mainCamera = Camera.main;
        if (_mainCamera == null)
        {
            _mainCamera = FindObjectOfType<Camera>();
        }
    }
    
    private void UpdatePlayerPosition()
    {
        if (_playerTransform != null)
        {
            Vector3 playerPos = _playerTransform.position;
            transform.position = new Vector3(playerPos.x, playerPos.y + _rainAreaSize.y * 0.5f, playerPos.z);
        }
        else if (_mainCamera != null)
        {
            Vector3 cameraPos = _mainCamera.transform.position;
            transform.position = new Vector3(cameraPos.x, cameraPos.y + _rainAreaSize.y * 0.5f, cameraPos.z);
        }
    }
    
    private void SpawnRainDrops()
    {
        _spawnTimer += Time.deltaTime;
        float spawnRate = _maxRainDrops * _rainIntensity / _dropLifetime;
        float spawnInterval = 1f / spawnRate;
        
        if (_spawnTimer >= spawnInterval && _rainDropPool.Count > 0)
        {
            _spawnTimer = 0f;
            SpawnRainDrop();
        }
    }
    
    private void SpawnRainDrop()
    {
        RainDrop rainDrop = _rainDropPool.Dequeue();
        
        Vector3 spawnPosition = transform.position + new Vector3(
            Random.Range(-_rainAreaSize.x * 0.5f, _rainAreaSize.x * 0.5f),
            Random.Range(0f, _rainAreaSize.y * 0.5f),
            Random.Range(-_rainAreaSize.z * 0.5f, _rainAreaSize.z * 0.5f)
        );
        
        rainDrop.transform.position = spawnPosition;
        rainDrop.transform.localScale = Vector3.one * _dropScale;
        rainDrop.lifetime = _dropLifetime;
        rainDrop.isActive = true;
        rainDrop.gameObject.SetActive(true);
        
        Vector3 velocity = new Vector3(_windDirection.x, -1f, _windDirection.z) * _rainDropSpeed;
        rainDrop.rigidbody.velocity = velocity;
        
        _activeRainDrops.Add(rainDrop);
    }
    
    private void UpdateRainDrops()
    {
        for (int i = _activeRainDrops.Count - 1; i >= 0; i--)
        {
            RainDrop rainDrop = _activeRainDrops[i];
            
            rainDrop.lifetime -= Time.deltaTime;
            
            if (rainDrop.lifetime <= 0f || IsRainDropHittingGround(rainDrop))
            {
                DeactivateRainDrop(rainDrop, i);
            }
        }
    }
    
    private bool IsRainDropHittingGround(RainDrop rainDrop)
    {
        RaycastHit hit;
        Vector3 rayOrigin = rainDrop.transform.position;
        Vector3 rayDirection = rainDrop.rigidbody.velocity.normalized;
        float rayDistance = rainDrop.rigidbody.velocity.magnitude * Time.deltaTime;
        
        return Physics.Raycast(rayOrigin, rayDirection, out hit, rayDistance, _groundLayer);
    }
    
    private void DeactivateRainDrop(RainDrop rainDrop, int index)
    {
        rainDrop.isActive = false;
        rainDrop.gameObject.SetActive(false);
        rainDrop.rigidbody.velocity = Vector3.zero;
        
        _activeRainDrops.RemoveAt(index);
        _rainDropPool.Enqueue(rainDrop);
    }
    
    private void UpdateAudio()
    {
        if (_rainAudioSource != null)
        {
            float targetVolume = _isRaining ? _audioVolume * _rainIntensity : 0f;
            _rainAudioSource.volume = Mathf.Lerp(_rainAudioSource.volume, targetVolume, Time.deltaTime * 2f);
        }
    }
    
    public void SetRainIntensity(float intensity)
    {
        _rainIntensity = Mathf.Clamp01(intensity);
        
        if (_useParticleSystem && _rainParticleSystem != null)
        {
            var emission = _rainParticleSystem.emission;
            emission.rateOverTime = _maxRainDrops * _rainIntensity / _dropLifetime;
        }
    }
    
    public void StartRain()
    {
        _isRaining = true;
        
        if (_rainAudioSource != null && _rainSoundClip != null)
        {
            _rainAudioSource.Play();
        }
        
        if (_useParticleSystem && _rainParticleSystem != null)
        {
            _rainParticleSystem.Play();
        }
    }
    
    public void StopRain()
    {
        _isRaining = false;
        
        if (_rainAudioSource != null)
        {
            _rainAudioSource.Stop();
        }
        
        if (_useParticleSystem && _rainParticleSystem != null)
        {
            _rainParticleSystem.Stop();
        }
        
        for (int i = _activeRainDrops.Count - 1; i >= 0; i--)
        {
            DeactivateRainDrop(_activeRainDrops[i], i);
        }
    }
    
    public void SetWindDirection(Vector3 windDirection)
    {
        _windDirection = windDirection;
        
        if (_useParticleSystem && _rainParticleSystem != null)
        {
            var velocityOverLifetime = _rainParticleSystem.velocityOverLifetime;
            velocityOverLifetime.x = _windDirection.x * _rainDropSpeed;
            velocityOverLifetime.z = _windDirection.z * _rainDropSpeed;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, _rainAreaSize);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, _rainAreaSize);
    }
}