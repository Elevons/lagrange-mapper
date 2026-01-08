// Prompt: poison gas cloud that damages
// Type: general

using UnityEngine;
using System.Collections;

public class PoisonGasCloud : MonoBehaviour
{
    [Header("Gas Properties")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 1f;
    [SerializeField] private float _cloudDuration = 15f;
    [SerializeField] private bool _destroyAfterDuration = true;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _gasParticles;
    [SerializeField] private Color _gasColor = Color.green;
    [SerializeField] private AnimationCurve _opacityOverTime = AnimationCurve.Linear(0, 1, 1, 0);
    
    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _gasHissSound;
    [SerializeField] private AudioClip _damageSound;
    
    private float _currentLifetime;
    private Collider _gasCollider;
    private Renderer _gasRenderer;
    private Material _gasMaterial;
    private Color _originalColor;
    
    private void Start()
    {
        _gasCollider = GetComponent<Collider>();
        _gasRenderer = GetComponent<Renderer>();
        
        if (_gasCollider != null)
        {
            _gasCollider.isTrigger = true;
        }
        
        if (_gasRenderer != null && _gasRenderer.material != null)
        {
            _gasMaterial = _gasRenderer.material;
            _originalColor = _gasMaterial.color;
            _gasMaterial.color = _gasColor;
        }
        
        if (_gasParticles != null)
        {
            _gasParticles.Play();
        }
        
        if (_audioSource != null && _gasHissSound != null)
        {
            _audioSource.clip = _gasHissSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        _currentLifetime = 0f;
    }
    
    private void Update()
    {
        if (_destroyAfterDuration)
        {
            _currentLifetime += Time.deltaTime;
            
            float normalizedTime = _currentLifetime / _cloudDuration;
            
            if (_gasMaterial != null)
            {
                float opacity = _opacityOverTime.Evaluate(normalizedTime);
                Color currentColor = _gasMaterial.color;
                currentColor.a = opacity;
                _gasMaterial.color = currentColor;
            }
            
            if (_currentLifetime >= _cloudDuration)
            {
                DestroyGasCloud();
            }
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StartCoroutine(DamageOverTime(other.gameObject));
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            StopCoroutine(DamageOverTime(other.gameObject));
        }
    }
    
    private System.Collections.IEnumerator DamageOverTime(GameObject target)
    {
        while (target != null && _gasCollider.bounds.Contains(target.transform.position))
        {
            DealDamage(target);
            yield return new WaitForSeconds(_damageInterval);
        }
    }
    
    private void DealDamage(GameObject target)
    {
        if (target == null) return;
        
        // Try to find a component that can take damage
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Send damage message - target object should implement this
            target.SendMessage("TakeDamage", _damageAmount, SendMessageOptions.DontRequireReceiver);
        }
        
        // Play damage sound
        if (_audioSource != null && _damageSound != null)
        {
            _audioSource.PlayOneShot(_damageSound);
        }
        
        // Create damage effect at target position
        CreateDamageEffect(target.transform.position);
    }
    
    private void CreateDamageEffect(Vector3 position)
    {
        // Create a simple damage indicator
        GameObject damageIndicator = new GameObject("DamageIndicator");
        damageIndicator.transform.position = position + Vector3.up * 2f;
        
        TextMesh textMesh = damageIndicator.AddComponent<TextMesh>();
        textMesh.text = "-" + _damageAmount.ToString();
        textMesh.color = Color.red;
        textMesh.fontSize = 20;
        textMesh.anchor = TextAnchor.MiddleCenter;
        
        // Animate the damage text
        StartCoroutine(AnimateDamageText(damageIndicator));
    }
    
    private System.Collections.IEnumerator AnimateDamageText(GameObject textObject)
    {
        Vector3 startPos = textObject.transform.position;
        Vector3 endPos = startPos + Vector3.up * 1f;
        float duration = 1f;
        float elapsed = 0f;
        
        TextMesh textMesh = textObject.GetComponent<TextMesh>();
        Color startColor = textMesh.color;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;
            
            textObject.transform.position = Vector3.Lerp(startPos, endPos, progress);
            
            Color currentColor = startColor;
            currentColor.a = 1f - progress;
            textMesh.color = currentColor;
            
            yield return null;
        }
        
        Destroy(textObject);
    }
    
    private void DestroyGasCloud()
    {
        if (_gasParticles != null)
        {
            _gasParticles.Stop();
        }
        
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
        
        StartCoroutine(FadeOutAndDestroy());
    }
    
    private System.Collections.IEnumerator FadeOutAndDestroy()
    {
        float fadeTime = 2f;
        float elapsed = 0f;
        
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - (elapsed / fadeTime);
            
            if (_gasMaterial != null)
            {
                Color currentColor = _gasMaterial.color;
                currentColor.a = alpha;
                _gasMaterial.color = currentColor;
            }
            
            yield return null;
        }
        
        Destroy(gameObject);
    }
    
    public void SetDamage(float damage)
    {
        _damageAmount = damage;
    }
    
    public void SetDuration(float duration)
    {
        _cloudDuration = duration;
    }
    
    public void SetDamageInterval(float interval)
    {
        _damageInterval = interval;
    }
    
    private void OnDestroy()
    {
        if (_gasMaterial != null)
        {
            _gasMaterial.color = _originalColor;
        }
    }
}