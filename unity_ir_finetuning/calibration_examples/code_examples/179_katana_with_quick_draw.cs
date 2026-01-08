// Prompt: katana with quick draw
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Katana : MonoBehaviour
{
    [Header("Katana Settings")]
    [SerializeField] private float _damage = 50f;
    [SerializeField] private float _attackRange = 2f;
    [SerializeField] private LayerMask _targetLayers = -1;
    
    [Header("Quick Draw")]
    [SerializeField] private float _quickDrawWindow = 0.3f;
    [SerializeField] private float _quickDrawDamageMultiplier = 2f;
    [SerializeField] private float _quickDrawRange = 3f;
    [SerializeField] private KeyCode _quickDrawKey = KeyCode.Q;
    
    [Header("Animation")]
    [SerializeField] private float _drawSpeed = 2f;
    [SerializeField] private float _sheatheSpeed = 1.5f;
    [SerializeField] private Transform _katanaModel;
    [SerializeField] private Transform _sheathedPosition;
    [SerializeField] private Transform _drawnPosition;
    
    [Header("Effects")]
    [SerializeField] private ParticleSystem _slashEffect;
    [SerializeField] private ParticleSystem _quickDrawEffect;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _drawSound;
    [SerializeField] private AudioClip _sheatheSound;
    [SerializeField] private AudioClip _slashSound;
    [SerializeField] private AudioClip _quickDrawSound;
    
    [Header("Events")]
    public UnityEvent<float> OnAttack;
    public UnityEvent OnQuickDraw;
    public UnityEvent OnDraw;
    public UnityEvent OnSheathe;
    
    private bool _isDrawn = false;
    private bool _isQuickDrawReady = true;
    private bool _isAnimating = false;
    private float _lastDrawTime;
    private Camera _playerCamera;
    
    [System.Serializable]
    public class HitInfo
    {
        public GameObject target;
        public float damage;
        public Vector3 hitPoint;
        public Vector3 hitDirection;
    }
    
    void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_katanaModel == null)
            _katanaModel = transform;
            
        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();
            
        if (_sheathedPosition == null)
        {
            GameObject sheathed = new GameObject("SheathedPosition");
            sheathed.transform.SetParent(transform.parent);
            sheathed.transform.localPosition = new Vector3(0.5f, -0.5f, 0.5f);
            sheathed.transform.localRotation = Quaternion.Euler(0, 0, 45);
            _sheathedPosition = sheathed.transform;
        }
        
        if (_drawnPosition == null)
        {
            GameObject drawn = new GameObject("DrawnPosition");
            drawn.transform.SetParent(transform.parent);
            drawn.transform.localPosition = new Vector3(0.3f, 0, 0.8f);
            drawn.transform.localRotation = Quaternion.identity;
            _drawnPosition = drawn.transform;
        }
        
        SetKatanaPosition(_sheathedPosition);
    }
    
    void Update()
    {
        HandleInput();
        UpdateQuickDrawWindow();
    }
    
    void HandleInput()
    {
        if (_isAnimating) return;
        
        if (Input.GetKeyDown(_quickDrawKey))
        {
            if (!_isDrawn && _isQuickDrawReady)
            {
                PerformQuickDraw();
            }
            else if (!_isDrawn)
            {
                DrawKatana();
            }
            else
            {
                SheatheKatana();
            }
        }
        
        if (_isDrawn && Input.GetMouseButtonDown(0))
        {
            PerformAttack();
        }
    }
    
    void UpdateQuickDrawWindow()
    {
        if (!_isQuickDrawReady && Time.time - _lastDrawTime > _quickDrawWindow)
        {
            _isQuickDrawReady = true;
        }
    }
    
    void PerformQuickDraw()
    {
        if (_isAnimating) return;
        
        _isQuickDrawReady = false;
        _lastDrawTime = Time.time;
        
        StartCoroutine(QuickDrawSequence());
    }
    
    System.Collections.IEnumerator QuickDrawSequence()
    {
        _isAnimating = true;
        
        PlaySound(_quickDrawSound);
        OnQuickDraw?.Invoke();
        
        if (_quickDrawEffect != null)
            _quickDrawEffect.Play();
        
        float drawTime = 1f / (_drawSpeed * 2f);
        yield return StartCoroutine(AnimateToPosition(_drawnPosition, drawTime));
        
        PerformQuickDrawAttack();
        
        yield return new WaitForSeconds(0.1f);
        
        float sheatheTime = 1f / (_sheatheSpeed * 2f);
        yield return StartCoroutine(AnimateToPosition(_sheathedPosition, sheatheTime));
        
        _isDrawn = false;
        _isAnimating = false;
        
        PlaySound(_sheatheSound);
        OnSheathe?.Invoke();
    }
    
    void PerformQuickDrawAttack()
    {
        float damage = _damage * _quickDrawDamageMultiplier;
        Vector3 attackOrigin = _katanaModel.position;
        Vector3 attackDirection = _playerCamera != null ? _playerCamera.transform.forward : transform.forward;
        
        RaycastHit[] hits = Physics.SphereCastAll(attackOrigin, 0.5f, attackDirection, _quickDrawRange, _targetLayers);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            
            ProcessHit(hit.collider.gameObject, damage, hit.point, attackDirection);
        }
        
        if (_slashEffect != null)
        {
            _slashEffect.transform.position = attackOrigin + attackDirection * (_quickDrawRange * 0.5f);
            _slashEffect.Play();
        }
        
        OnAttack?.Invoke(damage);
    }
    
    void DrawKatana()
    {
        if (_isDrawn || _isAnimating) return;
        
        StartCoroutine(DrawSequence());
    }
    
    System.Collections.IEnumerator DrawSequence()
    {
        _isAnimating = true;
        
        PlaySound(_drawSound);
        OnDraw?.Invoke();
        
        float drawTime = 1f / _drawSpeed;
        yield return StartCoroutine(AnimateToPosition(_drawnPosition, drawTime));
        
        _isDrawn = true;
        _isAnimating = false;
    }
    
    void SheatheKatana()
    {
        if (!_isDrawn || _isAnimating) return;
        
        StartCoroutine(SheatheSequence());
    }
    
    System.Collections.IEnumerator SheatheSequence()
    {
        _isAnimating = true;
        
        PlaySound(_sheatheSound);
        
        float sheatheTime = 1f / _sheatheSpeed;
        yield return StartCoroutine(AnimateToPosition(_sheathedPosition, sheatheTime));
        
        _isDrawn = false;
        _isAnimating = false;
        
        OnSheathe?.Invoke();
    }
    
    System.Collections.IEnumerator AnimateToPosition(Transform targetTransform, float duration)
    {
        Vector3 startPos = _katanaModel.position;
        Quaternion startRot = _katanaModel.rotation;
        Vector3 targetPos = targetTransform.position;
        Quaternion targetRot = targetTransform.rotation;
        
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            t = Mathf.SmoothStep(0f, 1f, t);
            
            _katanaModel.position = Vector3.Lerp(startPos, targetPos, t);
            _katanaModel.rotation = Quaternion.Lerp(startRot, targetRot, t);
            
            yield return null;
        }
        
        _katanaModel.position = targetPos;
        _katanaModel.rotation = targetRot;
    }
    
    void SetKatanaPosition(Transform targetTransform)
    {
        _katanaModel.position = targetTransform.position;
        _katanaModel.rotation = targetTransform.rotation;
    }
    
    void PerformAttack()
    {
        if (!_isDrawn || _isAnimating) return;
        
        Vector3 attackOrigin = _katanaModel.position;
        Vector3 attackDirection = _playerCamera != null ? _playerCamera.transform.forward : transform.forward;
        
        RaycastHit[] hits = Physics.SphereCastAll(attackOrigin, 0.3f, attackDirection, _attackRange, _targetLayers);
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.gameObject == gameObject) continue;
            
            ProcessHit(hit.collider.gameObject, _damage, hit.point, attackDirection);
        }
        
        PlaySound(_slashSound);
        
        if (_slashEffect != null)
        {
            _slashEffect.transform.position = attackOrigin + attackDirection * (_attackRange * 0.5f);
            _slashEffect.Play();
        }
        
        OnAttack?.Invoke(_damage);
    }
    
    void ProcessHit(GameObject target, float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        if (target.CompareTag("Player")) return;
        
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            targetRb.AddForce(hitDirection * 10f, ForceMode.Impulse);
        }
        
        HitInfo hitInfo = new HitInfo
        {
            target = target,
            damage = damage,
            hitPoint = hitPoint,
            hitDirection = hitDirection
        };
        
        target.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
        target.SendMessage("OnKatanaHit", hitInfo, SendMessageOptions.DontRequireReceiver);
    }
    
    void PlaySound(AudioClip clip)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.PlayOneShot(clip);
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (_katanaModel == null) return;
        
        Vector3 attackOrigin = _katanaModel.position;
        Vector3 attackDirection = _playerCamera != null ? _playerCamera.transform.forward : transform.forward;
        
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackOrigin + attackDirection * _attackRange, 0.3f);
        Gizmos.DrawLine(attackOrigin, attackOrigin + attackDirection * _attackRange);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(attackOrigin + attackDirection * _quickDrawRange, 0.5f);
        Gizmos.DrawLine(attackOrigin, attackOrigin + attackDirection * _quickDrawRange);
    }
}