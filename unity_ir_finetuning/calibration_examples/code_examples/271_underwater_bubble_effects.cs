// Prompt: underwater bubble effects
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class UnderwaterBubbleEffects : MonoBehaviour
{
    [Header("Bubble Generation")]
    [SerializeField] private GameObject _bubblePrefab;
    [SerializeField] private int _maxBubbles = 50;
    [SerializeField] private float _bubbleSpawnRate = 2f;
    [SerializeField] private Vector3 _spawnAreaSize = new Vector3(10f, 1f, 10f);
    [SerializeField] private float _bubbleLifetime = 8f;
    
    [Header("Bubble Movement")]
    [SerializeField] private float _minRiseSpeed = 1f;
    [SerializeField] private float _maxRiseSpeed = 3f;
    [SerializeField] private float _horizontalDriftStrength = 0.5f;
    [SerializeField] private float _wavyMotionFrequency = 1f;
    [SerializeField] private float _wavyMotionAmplitude = 0.3f;
    
    [Header("Bubble Appearance")]
    [SerializeField] private float _minBubbleSize = 0.1f;
    [SerializeField] private float _maxBubbleSize = 0.5f;
    [SerializeField] private AnimationCurve _sizeOverLifetime = AnimationCurve.Linear(0f, 1f, 1f, 0.8f);
    [SerializeField] private AnimationCurve _alphaOverLifetime = AnimationCurve.EaseInOut(0f, 0f, 0.2f, 1f);
    
    [Header("Water Surface")]
    [SerializeField] private float _waterSurfaceY = 5f;
    [SerializeField] private bool _popAtSurface = true;
    [SerializeField] private GameObject _popEffectPrefab;
    
    [Header("Player Interaction")]
    [SerializeField] private bool _reactToPlayer = true;
    [SerializeField] private float _playerDetectionRadius = 3f;
    [SerializeField] private float _playerAvoidanceStrength = 2f;
    
    private List<BubbleData> _activeBubbles = new List<BubbleData>();
    private float _nextSpawnTime;
    private Transform _playerTransform;
    
    [System.Serializable]
    private class BubbleData
    {
        public GameObject gameObject;
        public Transform transform;
        public Renderer renderer;
        public float spawnTime;
        public float riseSpeed;
        public float initialSize;
        public Vector3 driftDirection;
        public float wavyOffset;
        public Material material;
        public Color originalColor;
    }
    
    private void Start()
    {
        if (_bubblePrefab == null)
        {
            CreateDefaultBubblePrefab();
        }
        
        FindPlayer();
        _nextSpawnTime = Time.time + (1f / _bubbleSpawnRate);
    }
    
    private void Update()
    {
        if (Time.time >= _nextSpawnTime && _activeBubbles.Count < _maxBubbles)
        {
            SpawnBubble();
            _nextSpawnTime = Time.time + (1f / _bubbleSpawnRate);
        }
        
        UpdateBubbles();
        CleanupBubbles();
    }
    
    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }
    }
    
    private void SpawnBubble()
    {
        Vector3 spawnPosition = transform.position + new Vector3(
            Random.Range(-_spawnAreaSize.x * 0.5f, _spawnAreaSize.x * 0.5f),
            Random.Range(-_spawnAreaSize.y * 0.5f, _spawnAreaSize.y * 0.5f),
            Random.Range(-_spawnAreaSize.z * 0.5f, _spawnAreaSize.z * 0.5f)
        );
        
        GameObject bubbleObj = Instantiate(_bubblePrefab, spawnPosition, Quaternion.identity);
        
        BubbleData bubble = new BubbleData
        {
            gameObject = bubbleObj,
            transform = bubbleObj.transform,
            renderer = bubbleObj.GetComponent<Renderer>(),
            spawnTime = Time.time,
            riseSpeed = Random.Range(_minRiseSpeed, _maxRiseSpeed),
            initialSize = Random.Range(_minBubbleSize, _maxBubbleSize),
            driftDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized,
            wavyOffset = Random.Range(0f, Mathf.PI * 2f)
        };
        
        if (bubble.renderer != null)
        {
            bubble.material = bubble.renderer.material;
            bubble.originalColor = bubble.material.color;
        }
        
        bubble.transform.localScale = Vector3.one * bubble.initialSize;
        _activeBubbles.Add(bubble);
    }
    
    private void UpdateBubbles()
    {
        for (int i = 0; i < _activeBubbles.Count; i++)
        {
            BubbleData bubble = _activeBubbles[i];
            if (bubble.gameObject == null) continue;
            
            float age = Time.time - bubble.spawnTime;
            float normalizedAge = age / _bubbleLifetime;
            
            // Movement
            Vector3 movement = Vector3.up * bubble.riseSpeed * Time.deltaTime;
            
            // Add wavy motion
            float wavyX = Mathf.Sin((Time.time * _wavyMotionFrequency) + bubble.wavyOffset) * _wavyMotionAmplitude;
            float wavyZ = Mathf.Cos((Time.time * _wavyMotionFrequency * 0.7f) + bubble.wavyOffset) * _wavyMotionAmplitude;
            movement += new Vector3(wavyX, 0f, wavyZ) * Time.deltaTime;
            
            // Add drift
            movement += bubble.driftDirection * _horizontalDriftStrength * Time.deltaTime;
            
            // Player avoidance
            if (_reactToPlayer && _playerTransform != null)
            {
                Vector3 toPlayer = _playerTransform.position - bubble.transform.position;
                float distanceToPlayer = toPlayer.magnitude;
                
                if (distanceToPlayer < _playerDetectionRadius)
                {
                    Vector3 avoidanceForce = -toPlayer.normalized * _playerAvoidanceStrength * Time.deltaTime;
                    avoidanceForce.y *= 0.3f; // Reduce vertical avoidance
                    movement += avoidanceForce;
                }
            }
            
            bubble.transform.position += movement;
            
            // Update appearance
            float sizeMultiplier = _sizeOverLifetime.Evaluate(normalizedAge);
            bubble.transform.localScale = Vector3.one * (bubble.initialSize * sizeMultiplier);
            
            if (bubble.material != null)
            {
                float alpha = _alphaOverLifetime.Evaluate(normalizedAge);
                Color color = bubble.originalColor;
                color.a = alpha;
                bubble.material.color = color;
            }
            
            // Check if bubble reached surface
            if (_popAtSurface && bubble.transform.position.y >= _waterSurfaceY)
            {
                if (_popEffectPrefab != null)
                {
                    Vector3 popPosition = new Vector3(bubble.transform.position.x, _waterSurfaceY, bubble.transform.position.z);
                    Instantiate(_popEffectPrefab, popPosition, Quaternion.identity);
                }
                
                DestroyBubble(i);
                i--;
            }
        }
    }
    
    private void CleanupBubbles()
    {
        for (int i = _activeBubbles.Count - 1; i >= 0; i--)
        {
            BubbleData bubble = _activeBubbles[i];
            
            if (bubble.gameObject == null || Time.time - bubble.spawnTime >= _bubbleLifetime)
            {
                DestroyBubble(i);
            }
        }
    }
    
    private void DestroyBubble(int index)
    {
        if (index >= 0 && index < _activeBubbles.Count)
        {
            BubbleData bubble = _activeBubbles[index];
            if (bubble.gameObject != null)
            {
                DestroyImmediate(bubble.gameObject);
            }
            _activeBubbles.RemoveAt(index);
        }
    }
    
    private void CreateDefaultBubblePrefab()
    {
        GameObject bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = "DefaultBubble";
        
        // Remove collider
        Collider collider = bubble.GetComponent<Collider>();
        if (collider != null)
        {
            DestroyImmediate(collider);
        }
        
        // Setup material
        Renderer renderer = bubble.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material bubbleMaterial = new Material(Shader.Find("Standard"));
            bubbleMaterial.SetFloat("_Mode", 3); // Transparent mode
            bubbleMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            bubbleMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            bubbleMaterial.SetInt("_ZWrite", 0);
            bubbleMaterial.DisableKeyword("_ALPHATEST_ON");
            bubbleMaterial.EnableKeyword("_ALPHABLEND_ON");
            bubbleMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            bubbleMaterial.renderQueue = 3000;
            
            bubbleMaterial.color = new Color(0.8f, 0.9f, 1f, 0.3f);
            bubbleMaterial.SetFloat("_Metallic", 0f);
            bubbleMaterial.SetFloat("_Glossiness", 0.9f);
            
            renderer.material = bubbleMaterial;
        }
        
        _bubblePrefab = bubble;
        bubble.SetActive(false);
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position, _spawnAreaSize);
        
        if (_reactToPlayer && _playerTransform != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(_playerTransform.position, _playerDetectionRadius);
        }
        
        if (_popAtSurface)
        {
            Gizmos.color = Color.blue;
            Vector3 surfaceStart = transform.position - Vector3.right * _spawnAreaSize.x * 0.5f;
            Vector3 surfaceEnd = transform.position + Vector3.right * _spawnAreaSize.x * 0.5f;
            surfaceStart.y = _waterSurfaceY;
            surfaceEnd.y = _waterSurfaceY;
            Gizmos.DrawLine(surfaceStart, surfaceEnd);
        }
    }
    
    public void SetBubbleSpawnRate(float rate)
    {
        _bubbleSpawnRate = Mathf.Max(0.1f, rate);
    }
    
    public void SetWaterSurfaceLevel(float level)
    {
        _waterSurfaceY = level;
    }
    
    public void ClearAllBubbles()
    {
        for (int i = _activeBubbles.Count - 1; i >= 0; i--)
        {
            DestroyBubble(i);
        }
    }
}