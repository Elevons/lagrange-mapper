// Prompt: lightning rod that chains
// Type: combat

using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class LightningRod : MonoBehaviour
{
    [Header("Lightning Settings")]
    [SerializeField] private float _detectionRadius = 15f;
    [SerializeField] private float _chainRadius = 8f;
    [SerializeField] private int _maxChainTargets = 5;
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _cooldownTime = 2f;
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private string[] _targetTags = { "Enemy", "Player" };

    [Header("Visual Effects")]
    [SerializeField] private LineRenderer _lightningPrefab;
    [SerializeField] private ParticleSystem _strikeEffect;
    [SerializeField] private ParticleSystem _chainEffect;
    [SerializeField] private float _lightningDuration = 0.3f;
    [SerializeField] private Color _lightningColor = Color.cyan;
    [SerializeField] private AnimationCurve _lightningWidth = AnimationCurve.Linear(0f, 0.2f, 1f, 0.05f);

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _lightningSound;
    [SerializeField] private AudioClip _chainSound;

    [Header("Trigger Settings")]
    [SerializeField] private bool _autoTrigger = true;
    [SerializeField] private float _autoTriggerInterval = 3f;
    [SerializeField] private bool _triggerOnPlayerEnter = true;

    private float _lastStrikeTime;
    private List<GameObject> _activeTargets = new List<GameObject>();
    private List<LineRenderer> _activeLightning = new List<LineRenderer>();
    private Coroutine _lightningCoroutine;

    private void Start()
    {
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
        
        if (_autoTrigger)
            InvokeRepeating(nameof(TriggerLightning), _autoTriggerInterval, _autoTriggerInterval);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_triggerOnPlayerEnter) return;
        
        if (other.CompareTag("Player") && CanStrike())
        {
            TriggerLightning();
        }
    }

    public void TriggerLightning()
    {
        if (!CanStrike()) return;

        GameObject primaryTarget = FindPrimaryTarget();
        if (primaryTarget == null) return;

        _lastStrikeTime = Time.time;
        
        if (_lightningCoroutine != null)
            StopCoroutine(_lightningCoroutine);
        
        _lightningCoroutine = StartCoroutine(ExecuteLightningChain(primaryTarget));
    }

    private bool CanStrike()
    {
        return Time.time >= _lastStrikeTime + _cooldownTime;
    }

    private GameObject FindPrimaryTarget()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, _detectionRadius, _targetLayers);
        GameObject closestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (IsValidTarget(col.gameObject))
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = col.gameObject;
                }
            }
        }

        return closestTarget;
    }

    private bool IsValidTarget(GameObject target)
    {
        if (target == gameObject) return false;

        foreach (string tag in _targetTags)
        {
            if (target.CompareTag(tag))
                return true;
        }
        return false;
    }

    private IEnumerator ExecuteLightningChain(GameObject primaryTarget)
    {
        ClearActiveLightning();
        _activeTargets.Clear();

        List<GameObject> chainTargets = new List<GameObject> { primaryTarget };
        GameObject currentTarget = primaryTarget;

        // Build chain
        for (int i = 0; i < _maxChainTargets - 1; i++)
        {
            GameObject nextTarget = FindNextChainTarget(currentTarget, chainTargets);
            if (nextTarget == null) break;
            
            chainTargets.Add(nextTarget);
            currentTarget = nextTarget;
        }

        // Create lightning visuals
        Vector3 startPos = transform.position;
        for (int i = 0; i < chainTargets.Count; i++)
        {
            Vector3 endPos = chainTargets[i].transform.position;
            CreateLightningBolt(startPos, endPos, i == 0);
            
            if (i < chainTargets.Count - 1)
            {
                startPos = endPos;
            }
        }

        // Apply damage and effects
        foreach (GameObject target in chainTargets)
        {
            ApplyLightningDamage(target);
            CreateStrikeEffect(target.transform.position);
        }

        // Play audio
        PlayLightningAudio();

        // Wait for lightning duration
        yield WaitForSeconds(_lightningDuration);

        // Clean up
        ClearActiveLightning();
    }

    private GameObject FindNextChainTarget(GameObject currentTarget, List<GameObject> excludeTargets)
    {
        Collider[] colliders = Physics.OverlapSphere(currentTarget.transform.position, _chainRadius, _targetLayers);
        GameObject bestTarget = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            GameObject target = col.gameObject;
            
            if (!IsValidTarget(target) || excludeTargets.Contains(target))
                continue;

            float distance = Vector3.Distance(currentTarget.transform.position, target.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                bestTarget = target;
            }
        }

        return bestTarget;
    }

    private void CreateLightningBolt(Vector3 startPos, Vector3 endPos, bool isPrimary)
    {
        if (_lightningPrefab == null) return;

        LineRenderer lightning = Instantiate(_lightningPrefab);
        lightning.positionCount = 2;
        lightning.SetPosition(0, startPos);
        lightning.SetPosition(1, endPos);
        lightning.color = _lightningColor;
        lightning.widthCurve = _lightningWidth;
        lightning.material.color = _lightningColor;

        _activeLightning.Add(lightning);
    }

    private void ApplyLightningDamage(GameObject target)
    {
        // Try different damage interfaces
        var healthComponent = target.GetComponent<MonoBehaviour>();
        if (healthComponent != null)
        {
            // Send damage message - common Unity pattern
            target.SendMessage("TakeDamage", _damage, SendMessageOptions.DontRequireReceiver);
            target.SendMessage("OnLightningStrike", _damage, SendMessageOptions.DontRequireReceiver);
        }

        // Alternative: Use Rigidbody for physics impact
        Rigidbody rb = target.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 forceDirection = (target.transform.position - transform.position).normalized;
            rb.AddForce(forceDirection * _damage * 10f, ForceMode.Impulse);
        }
    }

    private void CreateStrikeEffect(Vector3 position)
    {
        if (_strikeEffect != null)
        {
            ParticleSystem effect = Instantiate(_strikeEffect);
            effect.transform.position = position;
            effect.Play();
            Destroy(effect.gameObject, effect.main.duration + effect.main.startLifetime.constantMax);
        }

        if (_chainEffect != null)
        {
            ParticleSystem chain = Instantiate(_chainEffect);
            chain.transform.position = position;
            chain.Play();
            Destroy(chain.gameObject, chain.main.duration + chain.main.startLifetime.constantMax);
        }
    }

    private void PlayLightningAudio()
    {
        if (_audioSource != null && _lightningSound != null)
        {
            _audioSource.PlayOneShot(_lightningSound);
        }

        if (_audioSource != null && _chainSound != null && _activeTargets.Count > 1)
        {
            _audioSource.PlayOneShot(_chainSound);
        }
    }

    private void ClearActiveLightning()
    {
        foreach (LineRenderer lightning in _activeLightning)
        {
            if (lightning != null)
                Destroy(lightning.gameObject);
        }
        _activeLightning.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _detectionRadius);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, _chainRadius);
    }

    private void OnDestroy()
    {
        if (_lightningCoroutine != null)
            StopCoroutine(_lightningCoroutine);
        
        ClearActiveLightning();
    }
}