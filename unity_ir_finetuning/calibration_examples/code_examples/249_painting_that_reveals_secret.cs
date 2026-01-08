// Prompt: painting that reveals secret
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class SecretRevealingPainting : MonoBehaviour
{
    [Header("Painting Settings")]
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private KeyCode _interactionKey = KeyCode.E;
    [SerializeField] private LayerMask _playerLayer = 1;
    
    [Header("Secret Reveal")]
    [SerializeField] private GameObject _secretObject;
    [SerializeField] private Transform _secretRevealPosition;
    [SerializeField] private float _revealDuration = 2f;
    [SerializeField] private AnimationCurve _revealCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _revealParticles;
    [SerializeField] private Light _mysticalLight;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _revealSound;
    [SerializeField] private Material _glowMaterial;
    
    [Header("UI")]
    [SerializeField] private GameObject _interactionPrompt;
    [SerializeField] private string _promptText = "Press E to examine painting";
    
    [Header("Events")]
    public UnityEvent OnSecretRevealed;
    public UnityEvent OnPlayerNearby;
    public UnityEvent OnPlayerLeft;
    
    private bool _secretRevealed = false;
    private bool _isRevealing = false;
    private bool _playerNearby = false;
    private Transform _playerTransform;
    private Renderer _paintingRenderer;
    private Material _originalMaterial;
    private Vector3 _originalSecretPosition;
    private Vector3 _originalSecretScale;
    
    private void Start()
    {
        InitializeComponents();
        SetupSecret();
        SetupUI();
    }
    
    private void InitializeComponents()
    {
        _paintingRenderer = GetComponent<Renderer>();
        if (_paintingRenderer != null)
        {
            _originalMaterial = _paintingRenderer.material;
        }
        
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
        }
        
        if (_mysticalLight != null)
        {
            _mysticalLight.enabled = false;
        }
    }
    
    private void SetupSecret()
    {
        if (_secretObject != null)
        {
            _originalSecretPosition = _secretObject.transform.position;
            _originalSecretScale = _secretObject.transform.localScale;
            
            if (_secretRevealPosition == null)
            {
                _secretRevealPosition = transform;
            }
            
            _secretObject.transform.localScale = Vector3.zero;
            _secretObject.SetActive(false);
        }
    }
    
    private void SetupUI()
    {
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }
    }
    
    private void Update()
    {
        CheckForPlayer();
        HandleInput();
    }
    
    private void CheckForPlayer()
    {
        Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, _interactionDistance, _playerLayer);
        bool playerFound = false;
        
        foreach (Collider col in nearbyObjects)
        {
            if (col.CompareTag("Player"))
            {
                playerFound = true;
                _playerTransform = col.transform;
                
                if (!_playerNearby)
                {
                    OnPlayerEntered();
                }
                break;
            }
        }
        
        if (!playerFound && _playerNearby)
        {
            OnPlayerExited();
        }
    }
    
    private void OnPlayerEntered()
    {
        _playerNearby = true;
        
        if (_interactionPrompt != null && !_secretRevealed)
        {
            _interactionPrompt.SetActive(true);
        }
        
        OnPlayerNearby?.Invoke();
    }
    
    private void OnPlayerExited()
    {
        _playerNearby = false;
        _playerTransform = null;
        
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }
        
        OnPlayerLeft?.Invoke();
    }
    
    private void HandleInput()
    {
        if (_playerNearby && !_secretRevealed && !_isRevealing && Input.GetKeyDown(_interactionKey))
        {
            RevealSecret();
        }
    }
    
    private void RevealSecret()
    {
        if (_secretRevealed || _isRevealing) return;
        
        _isRevealing = true;
        
        if (_interactionPrompt != null)
        {
            _interactionPrompt.SetActive(false);
        }
        
        StartCoroutine(RevealSequence());
    }
    
    private System.Collections.IEnumerator RevealSequence()
    {
        // Start visual effects
        if (_revealParticles != null)
        {
            _revealParticles.Play();
        }
        
        if (_mysticalLight != null)
        {
            _mysticalLight.enabled = true;
        }
        
        // Play sound
        if (_audioSource != null && _revealSound != null)
        {
            _audioSource.PlayOneShot(_revealSound);
        }
        
        // Change painting material
        if (_paintingRenderer != null && _glowMaterial != null)
        {
            _paintingRenderer.material = _glowMaterial;
        }
        
        // Reveal secret object
        if (_secretObject != null)
        {
            _secretObject.SetActive(true);
            _secretObject.transform.position = _secretRevealPosition.position;
            
            float elapsedTime = 0f;
            
            while (elapsedTime < _revealDuration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / _revealDuration;
                float curveValue = _revealCurve.Evaluate(progress);
                
                _secretObject.transform.localScale = Vector3.Lerp(Vector3.zero, _originalSecretScale, curveValue);
                
                if (_mysticalLight != null)
                {
                    _mysticalLight.intensity = Mathf.Lerp(0f, 2f, curveValue);
                }
                
                yield return null;
            }
            
            _secretObject.transform.localScale = _originalSecretScale;
        }
        
        _secretRevealed = true;
        _isRevealing = false;
        
        OnSecretRevealed?.Invoke();
        
        // Fade out light after reveal
        if (_mysticalLight != null)
        {
            StartCoroutine(FadeLight());
        }
    }
    
    private System.Collections.IEnumerator FadeLight()
    {
        yield return new WaitForSeconds(1f);
        
        float startIntensity = _mysticalLight.intensity;
        float elapsedTime = 0f;
        float fadeDuration = 2f;
        
        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeDuration;
            
            _mysticalLight.intensity = Mathf.Lerp(startIntensity, 0.5f, progress);
            
            yield return null;
        }
    }
    
    public void ResetPainting()
    {
        _secretRevealed = false;
        _isRevealing = false;
        
        if (_secretObject != null)
        {
            _secretObject.SetActive(false);
            _secretObject.transform.position = _originalSecretPosition;
            _secretObject.transform.localScale = Vector3.zero;
        }
        
        if (_paintingRenderer != null && _originalMaterial != null)
        {
            _paintingRenderer.material = _originalMaterial;
        }
        
        if (_mysticalLight != null)
        {
            _mysticalLight.enabled = false;
        }
        
        if (_revealParticles != null)
        {
            _revealParticles.Stop();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _interactionDistance);
        
        if (_secretRevealPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(_secretRevealPosition.position, Vector3.one * 0.5f);
            Gizmos.DrawLine(transform.position, _secretRevealPosition.position);
        }
    }
}