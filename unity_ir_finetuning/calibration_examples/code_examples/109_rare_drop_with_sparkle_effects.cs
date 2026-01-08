// Prompt: rare drop with sparkle effects
// Type: general

using UnityEngine;
using System.Collections;

public class RareDrop : MonoBehaviour
{
    [Header("Drop Settings")]
    [SerializeField] private float _magnetRange = 5f;
    [SerializeField] private float _magnetSpeed = 8f;
    [SerializeField] private float _bobHeight = 0.5f;
    [SerializeField] private float _bobSpeed = 2f;
    [SerializeField] private float _rotationSpeed = 90f;
    [SerializeField] private float _despawnTime = 30f;
    
    [Header("Sparkle Effects")]
    [SerializeField] private GameObject _sparklePrefab;
    [SerializeField] private int _sparkleCount = 8;
    [SerializeField] private float _sparkleRadius = 1.5f;
    [SerializeField] private float _sparkleSpeed = 2f;
    [SerializeField] private float _sparkleLifetime = 1f;
    [SerializeField] private Color _sparkleColor = Color.yellow;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;
    [SerializeField] private AudioClip _spawnSound;
    
    private Vector3 _startPosition;
    private bool _isBeingMagneted = false;
    private Transform _playerTransform;
    private AudioSource _audioSource;
    private float _bobTimer;
    private Coroutine _sparkleCoroutine;
    
    private void Start()
    {
        _startPosition = transform.position;
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        if (_spawnSound != null)
        {
            _audioSource.PlayOneShot(_spawnSound);
        }
        
        _sparkleCoroutine = StartCoroutine(SparkleEffect());
        
        Destroy(gameObject, _despawnTime);
    }
    
    private void Update()
    {
        HandleBobbing();
        HandleRotation();
        CheckForPlayer();
        
        if (_isBeingMagneted && _playerTransform != null)
        {
            MoveTowardsPlayer();
        }
    }
    
    private void HandleBobbing()
    {
        if (!_isBeingMagneted)
        {
            _bobTimer += Time.deltaTime * _bobSpeed;
            Vector3 newPosition = _startPosition;
            newPosition.y += Mathf.Sin(_bobTimer) * _bobHeight;
            transform.position = newPosition;
        }
    }
    
    private void HandleRotation()
    {
        transform.Rotate(Vector3.up, _rotationSpeed * Time.deltaTime);
    }
    
    private void CheckForPlayer()
    {
        if (_isBeingMagneted) return;
        
        Collider[] colliders = Physics.OverlapSphere(transform.position, _magnetRange);
        
        foreach (Collider col in colliders)
        {
            if (col.CompareTag("Player"))
            {
                _playerTransform = col.transform;
                _isBeingMagneted = true;
                break;
            }
        }
    }
    
    private void MoveTowardsPlayer()
    {
        if (_playerTransform == null) return;
        
        Vector3 direction = (_playerTransform.position - transform.position).normalized;
        transform.position += direction * _magnetSpeed * Time.deltaTime;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            CollectDrop();
        }
    }
    
    private void CollectDrop()
    {
        if (_pickupSound != null)
        {
            _audioSource.PlayOneShot(_pickupSound);
        }
        
        CreatePickupEffect();
        
        if (_sparkleCoroutine != null)
        {
            StopCoroutine(_sparkleCoroutine);
        }
        
        GetComponent<Renderer>().enabled = false;
        GetComponent<Collider>().enabled = false;
        
        Destroy(gameObject, _pickupSound != null ? _pickupSound.length : 0.1f);
    }
    
    private void CreatePickupEffect()
    {
        for (int i = 0; i < _sparkleCount * 2; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere;
            Vector3 spawnPosition = transform.position + randomDirection * 0.5f;
            
            GameObject sparkle = CreateSparkle(spawnPosition);
            if (sparkle != null)
            {
                Rigidbody rb = sparkle.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.velocity = randomDirection * _sparkleSpeed * 2f;
                }
                
                Destroy(sparkle, _sparkleLifetime);
            }
        }
    }
    
    private IEnumerator SparkleEffect()
    {
        while (true)
        {
            for (int i = 0; i < _sparkleCount; i++)
            {
                float angle = (360f / _sparkleCount) * i;
                float radian = angle * Mathf.Deg2Rad;
                
                Vector3 offset = new Vector3(
                    Mathf.Cos(radian) * _sparkleRadius,
                    Random.Range(-0.5f, 0.5f),
                    Mathf.Sin(radian) * _sparkleRadius
                );
                
                Vector3 sparklePosition = transform.position + offset;
                GameObject sparkle = CreateSparkle(sparklePosition);
                
                if (sparkle != null)
                {
                    StartCoroutine(AnimateSparkle(sparkle));
                }
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }
    
    private GameObject CreateSparkle(Vector3 position)
    {
        GameObject sparkle;
        
        if (_sparklePrefab != null)
        {
            sparkle = Instantiate(_sparklePrefab, position, Quaternion.identity);
        }
        else
        {
            sparkle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sparkle.transform.position = position;
            sparkle.transform.localScale = Vector3.one * 0.1f;
            
            Renderer renderer = sparkle.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = new Material(Shader.Find("Standard"));
                material.color = _sparkleColor;
                material.SetFloat("_Metallic", 1f);
                material.SetFloat("_Smoothness", 1f);
                renderer.material = material;
            }
            
            Destroy(sparkle.GetComponent<Collider>());
            
            Rigidbody rb = sparkle.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.drag = 2f;
        }
        
        return sparkle;
    }
    
    private IEnumerator AnimateSparkle(GameObject sparkle)
    {
        if (sparkle == null) yield break;
        
        Vector3 startScale = sparkle.transform.localScale;
        float timer = 0f;
        
        while (timer < _sparkleLifetime && sparkle != null)
        {
            timer += Time.deltaTime;
            float progress = timer / _sparkleLifetime;
            
            float scale = Mathf.Lerp(1f, 0f, progress);
            sparkle.transform.localScale = startScale * scale;
            
            sparkle.transform.Rotate(Vector3.up, 360f * Time.deltaTime);
            
            Renderer renderer = sparkle.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.material.color;
                color.a = Mathf.Lerp(1f, 0f, progress);
                renderer.material.color = color;
            }
            
            yield return null;
        }
        
        if (sparkle != null)
        {
            Destroy(sparkle);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _magnetRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _sparkleRadius);
    }
}