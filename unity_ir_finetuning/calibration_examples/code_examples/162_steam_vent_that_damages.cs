// Prompt: steam vent that damages
// Type: general

using UnityEngine;
using System.Collections;

public class SteamVent : MonoBehaviour
{
    [Header("Steam Settings")]
    [SerializeField] private float _steamDuration = 3f;
    [SerializeField] private float _cooldownDuration = 5f;
    [SerializeField] private bool _startActive = false;
    
    [Header("Damage Settings")]
    [SerializeField] private float _damageAmount = 10f;
    [SerializeField] private float _damageInterval = 0.5f;
    
    [Header("Visual Effects")]
    [SerializeField] private ParticleSystem _steamParticles;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _steamSound;
    
    [Header("Warning System")]
    [SerializeField] private float _warningDuration = 1f;
    [SerializeField] private GameObject _warningIndicator;
    [SerializeField] private Light _warningLight;
    [SerializeField] private Color _warningColor = Color.red;
    
    private bool _isActive = false;
    private bool _isWarning = false;
    private Collider _ventCollider;
    private Color _originalLightColor;
    private System.Collections.Generic.List<GameObject> _playersInRange = new System.Collections.Generic.List<GameObject>();
    
    private void Start()
    {
        _ventCollider = GetComponent<Collider>();
        if (_ventCollider == null)
        {
            _ventCollider = gameObject.AddComponent<BoxCollider>();
            _ventCollider.isTrigger = true;
        }
        
        if (_warningLight != null)
        {
            _originalLightColor = _warningLight.color;
            _warningLight.enabled = false;
        }
        
        if (_warningIndicator != null)
        {
            _warningIndicator.SetActive(false);
        }
        
        if (_steamParticles != null)
        {
            _steamParticles.Stop();
        }
        
        if (_startActive)
        {
            StartCoroutine(SteamCycle());
        }
        else
        {
            StartCoroutine(SteamCycleWithDelay());
        }
    }
    
    private IEnumerator SteamCycleWithDelay()
    {
        yield return new WaitForSeconds(_cooldownDuration);
        StartCoroutine(SteamCycle());
    }
    
    private IEnumerator SteamCycle()
    {
        while (true)
        {
            yield return StartCoroutine(ShowWarning());
            yield return StartCoroutine(ActivateSteam());
            yield return new WaitForSeconds(_cooldownDuration);
        }
    }
    
    private IEnumerator ShowWarning()
    {
        _isWarning = true;
        
        if (_warningIndicator != null)
        {
            _warningIndicator.SetActive(true);
        }
        
        if (_warningLight != null)
        {
            _warningLight.enabled = true;
            _warningLight.color = _warningColor;
        }
        
        float elapsed = 0f;
        while (elapsed < _warningDuration)
        {
            if (_warningLight != null)
            {
                _warningLight.intensity = Mathf.PingPong(Time.time * 3f, 1f) + 0.5f;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }
        
        _isWarning = false;
        
        if (_warningIndicator != null)
        {
            _warningIndicator.SetActive(false);
        }
        
        if (_warningLight != null)
        {
            _warningLight.enabled = false;
            _warningLight.color = _originalLightColor;
            _warningLight.intensity = 1f;
        }
    }
    
    private IEnumerator ActivateSteam()
    {
        _isActive = true;
        
        if (_steamParticles != null)
        {
            _steamParticles.Play();
        }
        
        if (_audioSource != null && _steamSound != null)
        {
            _audioSource.clip = _steamSound;
            _audioSource.loop = true;
            _audioSource.Play();
        }
        
        StartCoroutine(DamagePlayersInRange());
        
        yield return new WaitForSeconds(_steamDuration);
        
        _isActive = false;
        
        if (_steamParticles != null)
        {
            _steamParticles.Stop();
        }
        
        if (_audioSource != null)
        {
            _audioSource.Stop();
        }
    }
    
    private IEnumerator DamagePlayersInRange()
    {
        while (_isActive)
        {
            foreach (GameObject player in _playersInRange)
            {
                if (player != null)
                {
                    DamagePlayer(player);
                }
            }
            yield return new WaitForSeconds(_damageInterval);
        }
    }
    
    private void DamagePlayer(GameObject player)
    {
        var playerScript = player.GetComponent<MonoBehaviour>();
        if (playerScript != null)
        {
            player.SendMessage("TakeDamage", _damageAmount, SendMessageOptions.DontRequireReceiver);
        }
        
        var rigidbody = player.GetComponent<Rigidbody>();
        if (rigidbody != null)
        {
            Vector3 knockbackDirection = (player.transform.position - transform.position).normalized;
            knockbackDirection.y = 0.5f;
            rigidbody.AddForce(knockbackDirection * 5f, ForceMode.Impulse);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (!_playersInRange.Contains(other.gameObject))
            {
                _playersInRange.Add(other.gameObject);
            }
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _playersInRange.Remove(other.gameObject);
        }
    }
    
    public void TriggerSteam()
    {
        if (!_isActive && !_isWarning)
        {
            StopAllCoroutines();
            StartCoroutine(SteamCycle());
        }
    }
    
    public void SetActive(bool active)
    {
        if (active && !_isActive && !_isWarning)
        {
            StopAllCoroutines();
            StartCoroutine(SteamCycle());
        }
        else if (!active)
        {
            StopAllCoroutines();
            _isActive = false;
            _isWarning = false;
            
            if (_steamParticles != null)
            {
                _steamParticles.Stop();
            }
            
            if (_audioSource != null)
            {
                _audioSource.Stop();
            }
            
            if (_warningIndicator != null)
            {
                _warningIndicator.SetActive(false);
            }
            
            if (_warningLight != null)
            {
                _warningLight.enabled = false;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isActive ? Color.red : (_isWarning ? Color.yellow : Color.blue);
        Gizmos.DrawWireCube(transform.position, transform.localScale);
    }
}