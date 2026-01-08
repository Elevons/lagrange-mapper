// Prompt: hit flash effect on damage
// Type: general

using UnityEngine;
using System.Collections;

public class HitFlashEffect : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Color _flashColor = Color.red;
    [SerializeField] private float _flashDuration = 0.1f;
    [SerializeField] private int _flashCount = 1;
    [SerializeField] private float _flashInterval = 0.05f;
    
    [Header("Component References")]
    [SerializeField] private Renderer _targetRenderer;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    
    private Material _originalMaterial;
    private Color _originalColor;
    private bool _isFlashing = false;
    private Coroutine _flashCoroutine;
    
    private void Start()
    {
        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();
            
        if (_spriteRenderer == null)
            _spriteRenderer = GetComponent<SpriteRenderer>();
            
        if (_targetRenderer != null)
        {
            _originalMaterial = _targetRenderer.material;
        }
        
        if (_spriteRenderer != null)
        {
            _originalColor = _spriteRenderer.color;
        }
    }
    
    public void TriggerFlash()
    {
        if (_isFlashing)
        {
            StopFlash();
        }
        
        _flashCoroutine = StartCoroutine(FlashRoutine());
    }
    
    public void TriggerFlash(Color flashColor)
    {
        _flashColor = flashColor;
        TriggerFlash();
    }
    
    public void TriggerFlash(float duration)
    {
        _flashDuration = duration;
        TriggerFlash();
    }
    
    public void TriggerFlash(Color flashColor, float duration)
    {
        _flashColor = flashColor;
        _flashDuration = duration;
        TriggerFlash();
    }
    
    public void StopFlash()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        
        _isFlashing = false;
        RestoreOriginalAppearance();
    }
    
    private IEnumerator FlashRoutine()
    {
        _isFlashing = true;
        
        for (int i = 0; i < _flashCount; i++)
        {
            ApplyFlashEffect();
            yield return new WaitForSeconds(_flashDuration);
            
            RestoreOriginalAppearance();
            
            if (i < _flashCount - 1)
            {
                yield return new WaitForSeconds(_flashInterval);
            }
        }
        
        _isFlashing = false;
        _flashCoroutine = null;
    }
    
    private void ApplyFlashEffect()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _flashColor;
        }
        else if (_targetRenderer != null)
        {
            _targetRenderer.material.color = _flashColor;
        }
    }
    
    private void RestoreOriginalAppearance()
    {
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = _originalColor;
        }
        else if (_targetRenderer != null && _originalMaterial != null)
        {
            _targetRenderer.material.color = _originalMaterial.color;
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy"))
        {
            TriggerFlash();
        }
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") || other.CompareTag("Enemy"))
        {
            TriggerFlash();
        }
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            TriggerFlash();
        }
    }
    
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
        {
            TriggerFlash();
        }
    }
    
    private void OnDestroy()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
    }
    
    private void OnValidate()
    {
        if (_flashDuration < 0.01f)
            _flashDuration = 0.01f;
            
        if (_flashCount < 1)
            _flashCount = 1;
            
        if (_flashInterval < 0f)
            _flashInterval = 0f;
    }
}