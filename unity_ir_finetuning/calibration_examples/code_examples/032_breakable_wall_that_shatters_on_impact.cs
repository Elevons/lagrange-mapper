// Prompt: breakable wall that shatters on impact
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class BreakableWall : MonoBehaviour
{
    [Header("Breaking Settings")]
    [SerializeField] private float _breakForce = 10f;
    [SerializeField] private float _health = 100f;
    [SerializeField] private bool _breakOnAnyImpact = false;
    [SerializeField] private string[] _breakableTags = { "Player", "Projectile" };
    
    [Header("Shatter Effects")]
    [SerializeField] private GameObject _shatterPrefab;
    [SerializeField] private int _fragmentCount = 20;
    [SerializeField] private float _explosionForce = 500f;
    [SerializeField] private float _explosionRadius = 5f;
    [SerializeField] private Material _fragmentMaterial;
    
    [Header("Audio")]
    [SerializeField] private AudioClip _breakSound;
    [SerializeField] private float _volume = 1f;
    
    [Header("Events")]
    public UnityEvent OnWallBroken;
    
    private bool _isBroken = false;
    private Collider _wallCollider;
    private MeshRenderer _meshRenderer;
    private AudioSource _audioSource;
    
    private void Start()
    {
        _wallCollider = GetComponent<Collider>();
        _meshRenderer = GetComponent<MeshRenderer>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _audioSource.playOnAwake = false;
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_isBroken) return;
        
        bool canBreak = _breakOnAnyImpact;
        
        if (!canBreak)
        {
            foreach (string tag in _breakableTags)
            {
                if (collision.gameObject.CompareTag(tag))
                {
                    canBreak = true;
                    break;
                }
            }
        }
        
        if (canBreak)
        {
            float impactForce = collision.relativeVelocity.magnitude;
            
            if (impactForce >= _breakForce)
            {
                TakeDamage(_health);
            }
            else
            {
                TakeDamage(impactForce * 10f);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (_isBroken) return;
        
        foreach (string tag in _breakableTags)
        {
            if (other.CompareTag(tag))
            {
                TakeDamage(_health);
                break;
            }
        }
    }
    
    public void TakeDamage(float damage)
    {
        if (_isBroken) return;
        
        _health -= damage;
        
        if (_health <= 0f)
        {
            BreakWall();
        }
    }
    
    public void BreakWall()
    {
        if (_isBroken) return;
        
        _isBroken = true;
        
        PlayBreakSound();
        CreateShatterEffect();
        OnWallBroken?.Invoke();
        
        if (_wallCollider != null)
            _wallCollider.enabled = false;
        
        if (_meshRenderer != null)
            _meshRenderer.enabled = false;
        
        StartCoroutine(DestroyAfterDelay(5f));
    }
    
    private void PlayBreakSound()
    {
        if (_breakSound != null && _audioSource != null)
        {
            _audioSource.clip = _breakSound;
            _audioSource.volume = _volume;
            _audioSource.Play();
        }
    }
    
    private void CreateShatterEffect()
    {
        Vector3 center = transform.position;
        Bounds bounds = GetComponent<Renderer>().bounds;
        
        if (_shatterPrefab != null)
        {
            GameObject shatterEffect = Instantiate(_shatterPrefab, center, transform.rotation);
            Destroy(shatterEffect, 10f);
        }
        else
        {
            CreateProceduralFragments(center, bounds);
        }
    }
    
    private void CreateProceduralFragments(Vector3 center, Bounds bounds)
    {
        for (int i = 0; i < _fragmentCount; i++)
        {
            GameObject fragment = CreateFragment(bounds);
            
            Vector3 randomPos = new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z)
            );
            
            fragment.transform.position = randomPos;
            
            Rigidbody rb = fragment.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 explosionDir = (randomPos - center).normalized;
                rb.AddForce(explosionDir * _explosionForce + Vector3.up * _explosionForce * 0.5f);
                rb.AddTorque(Random.insideUnitSphere * _explosionForce);
            }
            
            Destroy(fragment, Random.Range(3f, 8f));
        }
    }
    
    private GameObject CreateFragment(Bounds bounds)
    {
        GameObject fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        
        float scale = Random.Range(0.1f, 0.3f);
        fragment.transform.localScale = Vector3.one * scale;
        
        Rigidbody rb = fragment.AddComponent<Rigidbody>();
        rb.mass = scale;
        
        if (_fragmentMaterial != null)
        {
            fragment.GetComponent<Renderer>().material = _fragmentMaterial;
        }
        else if (_meshRenderer != null && _meshRenderer.material != null)
        {
            fragment.GetComponent<Renderer>().material = _meshRenderer.material;
        }
        
        fragment.AddComponent<FragmentBehavior>();
        
        return fragment;
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    
    public void ForceBreak()
    {
        BreakWall();
    }
    
    public bool IsBroken()
    {
        return _isBroken;
    }
    
    public float GetHealth()
    {
        return _health;
    }
    
    private class FragmentBehavior : MonoBehaviour
    {
        private void Start()
        {
            StartCoroutine(FadeOut());
        }
        
        private IEnumerator FadeOut()
        {
            yield return new WaitForSeconds(2f);
            
            Renderer renderer = GetComponent<Renderer>();
            Material material = renderer.material;
            Color originalColor = material.color;
            
            float fadeTime = 1f;
            float elapsed = 0f;
            
            while (elapsed < fadeTime)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / fadeTime);
                material.color = new Color(originalColor.r, originalColor.g, originalColor.b, alpha);
                yield return null;
            }
        }
    }
}