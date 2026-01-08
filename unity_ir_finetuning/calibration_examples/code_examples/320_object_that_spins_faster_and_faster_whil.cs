// Prompt: object that spins faster and faster while playing a sound that gets louder, and when it reaches maximum speed it explodes into 10 smaller pieces that each bounce around randomly while playing their own unique sound effects, and the original object's material color shifts from blue to red during the spin-up phase
// Type: general

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpinningExploder : MonoBehaviour
{
    [Header("Spin Settings")]
    [SerializeField] private float _maxSpinSpeed = 1000f;
    [SerializeField] private float _spinAcceleration = 50f;
    [SerializeField] private float _timeToMaxSpeed = 5f;
    
    [Header("Audio Settings")]
    [SerializeField] private AudioClip _spinSound;
    [SerializeField] private float _maxVolume = 1f;
    [SerializeField] private AudioClip[] _explosionSounds;
    
    [Header("Explosion Settings")]
    [SerializeField] private int _fragmentCount = 10;
    [SerializeField] private float _explosionForce = 500f;
    [SerializeField] private float _fragmentLifetime = 5f;
    [SerializeField] private GameObject _fragmentPrefab;
    
    [Header("Visual Settings")]
    [SerializeField] private Color _startColor = Color.blue;
    [SerializeField] private Color _endColor = Color.red;
    
    private float _currentSpinSpeed = 0f;
    private float _spinTimer = 0f;
    private bool _hasExploded = false;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private Material _material;
    private Color _originalColor;
    
    private void Start()
    {
        SetupComponents();
        StartSpinning();
    }
    
    private void SetupComponents()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        _renderer = GetComponent<Renderer>();
        if (_renderer != null)
        {
            _material = _renderer.material;
            _originalColor = _material.color;
        }
        
        if (_fragmentPrefab == null)
        {
            _fragmentPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _fragmentPrefab.transform.localScale = Vector3.one * 0.3f;
        }
    }
    
    private void StartSpinning()
    {
        if (_spinSound != null && _audioSource != null)
        {
            _audioSource.clip = _spinSound;
            _audioSource.loop = true;
            _audioSource.volume = 0f;
            _audioSource.Play();
        }
    }
    
    private void Update()
    {
        if (_hasExploded) return;
        
        UpdateSpin();
        UpdateAudio();
        UpdateColor();
        
        if (_spinTimer >= _timeToMaxSpeed)
        {
            Explode();
        }
    }
    
    private void UpdateSpin()
    {
        _spinTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(_spinTimer / _timeToMaxSpeed);
        _currentSpinSpeed = Mathf.Lerp(0f, _maxSpinSpeed, progress * progress);
        
        transform.Rotate(0f, _currentSpinSpeed * Time.deltaTime, 0f);
    }
    
    private void UpdateAudio()
    {
        if (_audioSource != null && _spinSound != null)
        {
            float progress = Mathf.Clamp01(_spinTimer / _timeToMaxSpeed);
            _audioSource.volume = Mathf.Lerp(0f, _maxVolume, progress);
            _audioSource.pitch = Mathf.Lerp(0.5f, 2f, progress);
        }
    }
    
    private void UpdateColor()
    {
        if (_material != null)
        {
            float progress = Mathf.Clamp01(_spinTimer / _timeToMaxSpeed);
            Color currentColor = Color.Lerp(_startColor, _endColor, progress);
            _material.color = currentColor;
        }
    }
    
    private void Explode()
    {
        _hasExploded = true;
        
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
        
        CreateFragments();
        
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        Collider collider = GetComponent<Collider>();
        if (collider != null)
        {
            collider.enabled = false;
        }
        
        StartCoroutine(DestroyAfterDelay(1f));
    }
    
    private void CreateFragments()
    {
        for (int i = 0; i < _fragmentCount; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere.normalized;
            Vector3 spawnPosition = transform.position + randomDirection * 0.5f;
            
            GameObject fragment = Instantiate(_fragmentPrefab, spawnPosition, Random.rotation);
            
            SetupFragment(fragment, randomDirection, i);
        }
    }
    
    private void SetupFragment(GameObject fragment, Vector3 direction, int index)
    {
        Rigidbody rb = fragment.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = fragment.AddComponent<Rigidbody>();
        }
        
        rb.AddForce(direction * _explosionForce);
        rb.AddTorque(Random.insideUnitSphere * _explosionForce * 0.1f);
        
        AudioSource fragmentAudio = fragment.GetComponent<AudioSource>();
        if (fragmentAudio == null)
        {
            fragmentAudio = fragment.AddComponent<AudioSource>();
        }
        
        if (_explosionSounds != null && _explosionSounds.Length > 0)
        {
            int soundIndex = index % _explosionSounds.Length;
            if (_explosionSounds[soundIndex] != null)
            {
                fragmentAudio.clip = _explosionSounds[soundIndex];
                fragmentAudio.volume = Random.Range(0.3f, 0.8f);
                fragmentAudio.pitch = Random.Range(0.8f, 1.5f);
                fragmentAudio.Play();
            }
        }
        
        FragmentBehavior fragmentBehavior = fragment.AddComponent<FragmentBehavior>();
        fragmentBehavior.Initialize(_fragmentLifetime);
        
        Renderer fragmentRenderer = fragment.GetComponent<Renderer>();
        if (fragmentRenderer != null)
        {
            fragmentRenderer.material.color = Random.ColorHSV();
        }
    }
    
    private IEnumerator DestroyAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }
    
    private void OnDestroy()
    {
        if (_material != null)
        {
            Destroy(_material);
        }
    }
}

public class FragmentBehavior : MonoBehaviour
{
    private float _lifetime;
    private float _timer;
    private Rigidbody _rigidbody;
    private AudioSource _audioSource;
    
    public void Initialize(float lifetime)
    {
        _lifetime = lifetime;
        _rigidbody = GetComponent<Rigidbody>();
        _audioSource = GetComponent<AudioSource>();
        
        if (_rigidbody != null)
        {
            _rigidbody.drag = 0.5f;
            _rigidbody.angularDrag = 0.5f;
        }
    }
    
    private void Update()
    {
        _timer += Time.deltaTime;
        
        if (_timer >= _lifetime)
        {
            Destroy(gameObject);
            return;
        }
        
        float fadeProgress = _timer / _lifetime;
        if (fadeProgress > 0.7f)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = renderer.material.color;
                color.a = Mathf.Lerp(1f, 0f, (fadeProgress - 0.7f) / 0.3f);
                renderer.material.color = color;
            }
            
            if (_audioSource != null)
            {
                _audioSource.volume = Mathf.Lerp(_audioSource.volume, 0f, Time.deltaTime * 2f);
            }
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (_rigidbody != null)
        {
            Vector3 bounceDirection = Vector3.Reflect(_rigidbody.velocity.normalized, collision.contacts[0].normal);
            _rigidbody.velocity = bounceDirection * _rigidbody.velocity.magnitude * 0.8f;
        }
        
        if (_audioSource != null && _audioSource.clip != null)
        {
            _audioSource.pitch = Random.Range(0.8f, 1.5f);
            _audioSource.PlayOneShot(_audioSource.clip, 0.5f);
        }
    }
}