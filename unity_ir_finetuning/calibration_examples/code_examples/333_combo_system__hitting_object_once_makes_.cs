// Prompt: combo system: hitting object once makes it glow yellow and play low note, second hit within 2 seconds makes it glow orange and play higher note, third hit makes it glow red, fourth hit creates explosion effect and spawns 5 bonus objects - missing the 2-second window resets combo
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class ComboSystem : MonoBehaviour
{
    [Header("Combo Settings")]
    [SerializeField] private float _comboWindow = 2f;
    [SerializeField] private int _maxComboLevel = 4;
    
    [Header("Visual Effects")]
    [SerializeField] private Renderer _targetRenderer;
    [SerializeField] private Color _normalColor = Color.white;
    [SerializeField] private Color _combo1Color = Color.yellow;
    [SerializeField] private Color _combo2Color = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color _combo3Color = Color.red;
    [SerializeField] private ParticleSystem _explosionEffect;
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _lowNote;
    [SerializeField] private AudioClip _midNote;
    [SerializeField] private AudioClip _highNote;
    [SerializeField] private AudioClip _explosionSound;
    
    [Header("Bonus Objects")]
    [SerializeField] private GameObject _bonusObjectPrefab;
    [SerializeField] private int _bonusObjectCount = 5;
    [SerializeField] private float _spawnRadius = 3f;
    [SerializeField] private float _spawnForce = 5f;
    
    [Header("Events")]
    public UnityEvent OnComboReset;
    public UnityEvent OnComboComplete;
    
    private int _currentComboLevel = 0;
    private Coroutine _comboResetCoroutine;
    private Material _originalMaterial;
    
    private void Start()
    {
        InitializeComponents();
        ResetCombo();
    }
    
    private void InitializeComponents()
    {
        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_targetRenderer != null)
        {
            _originalMaterial = _targetRenderer.material;
        }
    }
    
    private void OnMouseDown()
    {
        ProcessHit();
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            ProcessHit();
        }
    }
    
    private void ProcessHit()
    {
        _currentComboLevel++;
        
        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
        }
        
        switch (_currentComboLevel)
        {
            case 1:
                HandleCombo1();
                break;
            case 2:
                HandleCombo2();
                break;
            case 3:
                HandleCombo3();
                break;
            case 4:
                HandleCombo4();
                return;
        }
        
        _comboResetCoroutine = StartCoroutine(ComboResetTimer());
    }
    
    private void HandleCombo1()
    {
        SetObjectColor(_combo1Color);
        PlaySound(_lowNote, 0.8f);
    }
    
    private void HandleCombo2()
    {
        SetObjectColor(_combo2Color);
        PlaySound(_midNote, 1.0f);
    }
    
    private void HandleCombo3()
    {
        SetObjectColor(_combo3Color);
        PlaySound(_highNote, 1.2f);
    }
    
    private void HandleCombo4()
    {
        CreateExplosionEffect();
        PlaySound(_explosionSound, 1.0f);
        SpawnBonusObjects();
        OnComboComplete?.Invoke();
        ResetCombo();
    }
    
    private void SetObjectColor(Color color)
    {
        if (_targetRenderer != null && _originalMaterial != null)
        {
            _targetRenderer.material.color = color;
        }
    }
    
    private void PlaySound(AudioClip clip, float pitch)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip);
        }
    }
    
    private void CreateExplosionEffect()
    {
        if (_explosionEffect != null)
        {
            _explosionEffect.Play();
        }
    }
    
    private void SpawnBonusObjects()
    {
        if (_bonusObjectPrefab == null) return;
        
        for (int i = 0; i < _bonusObjectCount; i++)
        {
            Vector3 randomDirection = Random.insideUnitSphere.normalized;
            Vector3 spawnPosition = transform.position + randomDirection * _spawnRadius;
            
            GameObject bonusObject = Instantiate(_bonusObjectPrefab, spawnPosition, Random.rotation);
            
            Rigidbody rb = bonusObject.GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = bonusObject.AddComponent<Rigidbody>();
            }
            
            Vector3 forceDirection = (spawnPosition - transform.position).normalized;
            rb.AddForce(forceDirection * _spawnForce, ForceMode.Impulse);
            
            StartCoroutine(DestroyBonusObjectAfterTime(bonusObject, 10f));
        }
    }
    
    private IEnumerator DestroyBonusObjectAfterTime(GameObject obj, float time)
    {
        yield return new WaitForSeconds(time);
        if (obj != null)
        {
            Destroy(obj);
        }
    }
    
    private IEnumerator ComboResetTimer()
    {
        yield return new WaitForSeconds(_comboWindow);
        ResetCombo();
    }
    
    private void ResetCombo()
    {
        _currentComboLevel = 0;
        SetObjectColor(_normalColor);
        
        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
            _comboResetCoroutine = null;
        }
        
        OnComboReset?.Invoke();
    }
    
    private void OnDestroy()
    {
        if (_comboResetCoroutine != null)
        {
            StopCoroutine(_comboResetCoroutine);
        }
    }
}