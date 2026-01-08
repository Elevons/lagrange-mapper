// Prompt: gem that floats and sparkles
// Type: general

using UnityEngine;
using System.Collections;

public class FloatingGem : MonoBehaviour
{
    [Header("Floating Animation")]
    [SerializeField] private float _floatAmplitude = 0.5f;
    [SerializeField] private float _floatSpeed = 2f;
    [SerializeField] private bool _randomizeStartPosition = true;
    
    [Header("Rotation")]
    [SerializeField] private Vector3 _rotationSpeed = new Vector3(0, 90, 0);
    
    [Header("Sparkle Effects")]
    [SerializeField] private ParticleSystem _sparkleParticles;
    [SerializeField] private Light _gemLight;
    [SerializeField] private float _lightIntensityMin = 0.5f;
    [SerializeField] private float _lightIntensityMax = 1.5f;
    [SerializeField] private float _lightPulseSpeed = 3f;
    
    [Header("Collection")]
    [SerializeField] private bool _canBeCollected = true;
    [SerializeField] private string _playerTag = "Player";
    [SerializeField] private AudioClip _collectSound;
    [SerializeField] private GameObject _collectEffect;
    
    private Vector3 _startPosition;
    private float _timeOffset;
    private AudioSource _audioSource;
    private Renderer _renderer;
    private bool _isCollected = false;
    
    private void Start()
    {
        _startPosition = transform.position;
        
        if (_randomizeStartPosition)
        {
            _timeOffset = Random.Range(0f, Mathf.PI * 2f);
        }
        
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null && _collectSound != null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        _renderer = GetComponent<Renderer>();
        
        if (_sparkleParticles == null)
        {
            _sparkleParticles = GetComponentInChildren<ParticleSystem>();
        }
        
        if (_gemLight == null)
        {
            _gemLight = GetComponentInChildren<Light>();
        }
        
        if (_sparkleParticles != null)
        {
            _sparkleParticles.Play();
        }
    }
    
    private void Update()
    {
        if (_isCollected) return;
        
        AnimateFloating();
        AnimateRotation();
        AnimateLight();
    }
    
    private void AnimateFloating()
    {
        float newY = _startPosition.y + Mathf.Sin((Time.time * _floatSpeed) + _timeOffset) * _floatAmplitude;
        transform.position = new Vector3(_startPosition.x, newY, _startPosition.z);
    }
    
    private void AnimateRotation()
    {
        transform.Rotate(_rotationSpeed * Time.deltaTime);
    }
    
    private void AnimateLight()
    {
        if (_gemLight != null)
        {
            float intensity = Mathf.Lerp(_lightIntensityMin, _lightIntensityMax, 
                (Mathf.Sin(Time.time * _lightPulseSpeed) + 1f) * 0.5f);
            _gemLight.intensity = intensity;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!_canBeCollected || _isCollected) return;
        
        if (other.CompareTag(_playerTag))
        {
            CollectGem();
        }
    }
    
    private void CollectGem()
    {
        _isCollected = true;
        
        if (_collectSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(_collectSound);
        }
        
        if (_collectEffect != null)
        {
            Instantiate(_collectEffect, transform.position, transform.rotation);
        }
        
        if (_sparkleParticles != null)
        {
            _sparkleParticles.Stop();
        }
        
        StartCoroutine(DestroyAfterSound());
    }
    
    private IEnumerator DestroyAfterSound()
    {
        if (_renderer != null)
        {
            _renderer.enabled = false;
        }
        
        GetComponent<Collider>().enabled = false;
        
        float waitTime = 0f;
        if (_collectSound != null)
        {
            waitTime = _collectSound.length;
        }
        
        yield return new WaitForSeconds(waitTime);
        
        Destroy(gameObject);
    }
    
    public void SetFloatParameters(float amplitude, float speed)
    {
        _floatAmplitude = amplitude;
        _floatSpeed = speed;
    }
    
    public void SetRotationSpeed(Vector3 rotationSpeed)
    {
        _rotationSpeed = rotationSpeed;
    }
    
    public void EnableCollection(bool enable)
    {
        _canBeCollected = enable;
    }
}