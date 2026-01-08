// Prompt: portal that when entered plays whoosh sound, rotates player 180 degrees instantly, reverses velocity, changes color to random hue, scales to 0.5x size, and teleports to random location within 50 units while creating visual distortion effect
// Type: movement

using UnityEngine;
using UnityEngine.Events;

public class Portal : MonoBehaviour
{
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _whooshSound;
    
    [Header("Portal Settings")]
    [SerializeField] private float _teleportRadius = 50f;
    [SerializeField] private float _scaleMultiplier = 0.5f;
    [SerializeField] private float _distortionDuration = 1f;
    [SerializeField] private AnimationCurve _distortionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _portalEffect;
    [SerializeField] private Material _distortionMaterial;
    
    [Header("Events")]
    public UnityEvent OnPlayerTeleported;
    
    private Camera _mainCamera;
    private Material _originalCameraMaterial;
    private bool _isDistorting = false;
    private float _distortionTimer = 0f;
    
    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
        }
        
        _mainCamera = Camera.main;
        if (_mainCamera == null)
            _mainCamera = FindObjectOfType<Camera>();
            
        if (_portalEffect == null)
            _portalEffect = GetComponentInChildren<ParticleSystem>();
    }
    
    private void Update()
    {
        if (_isDistorting)
        {
            _distortionTimer += Time.deltaTime;
            float normalizedTime = _distortionTimer / _distortionDuration;
            
            if (normalizedTime >= 1f)
            {
                _isDistorting = false;
                _distortionTimer = 0f;
                RemoveDistortionEffect();
            }
            else
            {
                UpdateDistortionEffect(normalizedTime);
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            TeleportPlayer(other.gameObject);
        }
    }
    
    private void TeleportPlayer(GameObject player)
    {
        PlayWhooshSound();
        RotatePlayer180(player);
        ReversePlayerVelocity(player);
        ChangePlayerColor(player);
        ScalePlayer(player);
        TeleportToRandomLocation(player);
        CreateDistortionEffect();
        
        if (_portalEffect != null)
            _portalEffect.Play();
            
        OnPlayerTeleported?.Invoke();
    }
    
    private void PlayWhooshSound()
    {
        if (_audioSource != null && _whooshSound != null)
        {
            _audioSource.PlayOneShot(_whooshSound);
        }
    }
    
    private void RotatePlayer180(GameObject player)
    {
        Transform playerTransform = player.transform;
        playerTransform.rotation = Quaternion.Euler(
            playerTransform.eulerAngles.x,
            playerTransform.eulerAngles.y + 180f,
            playerTransform.eulerAngles.z
        );
    }
    
    private void ReversePlayerVelocity(GameObject player)
    {
        Rigidbody playerRb = player.GetComponent<Rigidbody>();
        if (playerRb != null)
        {
            playerRb.velocity = -playerRb.velocity;
            playerRb.angularVelocity = -playerRb.angularVelocity;
        }
        
        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            Vector3 reversedVelocity = -characterController.velocity;
            characterController.Move(reversedVelocity * Time.fixedDeltaTime);
        }
    }
    
    private void ChangePlayerColor(GameObject player)
    {
        Renderer playerRenderer = player.GetComponent<Renderer>();
        if (playerRenderer == null)
            playerRenderer = player.GetComponentInChildren<Renderer>();
            
        if (playerRenderer != null)
        {
            Material playerMaterial = playerRenderer.material;
            Color randomColor = Color.HSVToRGB(Random.Range(0f, 1f), 1f, 1f);
            playerMaterial.color = randomColor;
        }
        
        Renderer[] childRenderers = player.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in childRenderers)
        {
            Color randomColor = Color.HSVToRGB(Random.Range(0f, 1f), 1f, 1f);
            renderer.material.color = randomColor;
        }
    }
    
    private void ScalePlayer(GameObject player)
    {
        player.transform.localScale *= _scaleMultiplier;
    }
    
    private void TeleportToRandomLocation(GameObject player)
    {
        Vector3 randomDirection = Random.insideUnitSphere * _teleportRadius;
        randomDirection.y = Mathf.Abs(randomDirection.y);
        
        Vector3 newPosition = transform.position + randomDirection;
        
        RaycastHit hit;
        if (Physics.Raycast(newPosition + Vector3.up * 100f, Vector3.down, out hit, 200f))
        {
            newPosition.y = hit.point.y + 1f;
        }
        
        CharacterController characterController = player.GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
            player.transform.position = newPosition;
            characterController.enabled = true;
        }
        else
        {
            player.transform.position = newPosition;
        }
    }
    
    private void CreateDistortionEffect()
    {
        if (_mainCamera == null) return;
        
        _isDistorting = true;
        _distortionTimer = 0f;
        
        if (_distortionMaterial != null)
        {
            ApplyDistortionMaterial();
        }
        else
        {
            CreateScreenShake();
        }
    }
    
    private void ApplyDistortionMaterial()
    {
        if (_mainCamera != null && _distortionMaterial != null)
        {
            _mainCamera.GetComponent<Renderer>().material = _distortionMaterial;
        }
    }
    
    private void UpdateDistortionEffect(float normalizedTime)
    {
        float distortionStrength = _distortionCurve.Evaluate(normalizedTime);
        
        if (_distortionMaterial != null)
        {
            _distortionMaterial.SetFloat("_DistortionStrength", distortionStrength);
        }
        
        CreateScreenShake(distortionStrength * 0.1f);
    }
    
    private void CreateScreenShake(float intensity = 0.1f)
    {
        if (_mainCamera != null)
        {
            Vector3 originalPosition = _mainCamera.transform.position;
            Vector3 shakeOffset = Random.insideUnitSphere * intensity;
            _mainCamera.transform.position = originalPosition + shakeOffset;
            
            StartCoroutine(ResetCameraPosition(originalPosition));
        }
    }
    
    private System.Collections.IEnumerator ResetCameraPosition(Vector3 originalPosition)
    {
        yield return new WaitForEndOfFrame();
        if (_mainCamera != null)
        {
            _mainCamera.transform.position = Vector3.Lerp(
                _mainCamera.transform.position, 
                originalPosition, 
                Time.deltaTime * 10f
            );
        }
    }
    
    private void RemoveDistortionEffect()
    {
        if (_mainCamera != null && _originalCameraMaterial != null)
        {
            _mainCamera.GetComponent<Renderer>().material = _originalCameraMaterial;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _teleportRadius);
    }
}