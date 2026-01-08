// Prompt: grab and throw enemy
// Type: combat

using UnityEngine;
using UnityEngine.Events;

public class GrabThrowEnemy : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float _grabRange = 2f;
    [SerializeField] private LayerMask _enemyLayer = -1;
    [SerializeField] private Transform _holdPosition;
    [SerializeField] private float _grabForce = 10f;
    
    [Header("Throw Settings")]
    [SerializeField] private float _throwForce = 15f;
    [SerializeField] private float _throwUpwardForce = 5f;
    [SerializeField] private float _maxThrowDistance = 20f;
    
    [Header("Input")]
    [SerializeField] private KeyCode _grabKey = KeyCode.E;
    [SerializeField] private KeyCode _throwKey = KeyCode.Mouse0;
    
    [Header("Events")]
    public UnityEvent<GameObject> OnEnemyGrabbed;
    public UnityEvent<GameObject> OnEnemyThrown;
    public UnityEvent<GameObject> OnEnemyReleased;
    
    private Camera _playerCamera;
    private GameObject _grabbedEnemy;
    private Rigidbody _grabbedRigidbody;
    private Collider _grabbedCollider;
    private bool _isGrabbing;
    private Vector3 _originalGravity;
    
    [System.Serializable]
    private class EnemyState
    {
        public bool wasKinematic;
        public float originalDrag;
        public float originalAngularDrag;
        public RigidbodyConstraints originalConstraints;
    }
    
    private EnemyState _enemyState = new EnemyState();
    
    void Start()
    {
        _playerCamera = Camera.main;
        if (_playerCamera == null)
            _playerCamera = FindObjectOfType<Camera>();
            
        if (_holdPosition == null)
        {
            GameObject holdPoint = new GameObject("HoldPosition");
            holdPoint.transform.SetParent(transform);
            holdPoint.transform.localPosition = Vector3.forward * 2f;
            _holdPosition = holdPoint.transform;
        }
        
        _originalGravity = Physics.gravity;
    }
    
    void Update()
    {
        HandleInput();
        
        if (_isGrabbing && _grabbedEnemy != null)
        {
            UpdateGrabbedEnemy();
        }
    }
    
    void HandleInput()
    {
        if (Input.GetKeyDown(_grabKey))
        {
            if (!_isGrabbing)
                TryGrabEnemy();
            else
                ReleaseEnemy();
        }
        
        if (Input.GetKeyDown(_throwKey) && _isGrabbing)
        {
            ThrowEnemy();
        }
    }
    
    void TryGrabEnemy()
    {
        Vector3 rayOrigin = _playerCamera.transform.position;
        Vector3 rayDirection = _playerCamera.transform.forward;
        
        if (Physics.Raycast(rayOrigin, rayDirection, out RaycastHit hit, _grabRange, _enemyLayer))
        {
            GameObject enemy = hit.collider.gameObject;
            
            if (enemy.CompareTag("Enemy"))
            {
                GrabEnemy(enemy);
            }
        }
    }
    
    void GrabEnemy(GameObject enemy)
    {
        _grabbedEnemy = enemy;
        _grabbedRigidbody = enemy.GetComponent<Rigidbody>();
        _grabbedCollider = enemy.GetComponent<Collider>();
        
        if (_grabbedRigidbody == null)
        {
            _grabbedRigidbody = enemy.AddComponent<Rigidbody>();
        }
        
        // Store original state
        _enemyState.wasKinematic = _grabbedRigidbody.isKinematic;
        _enemyState.originalDrag = _grabbedRigidbody.drag;
        _enemyState.originalAngularDrag = _grabbedRigidbody.angularDrag;
        _enemyState.originalConstraints = _grabbedRigidbody.constraints;
        
        // Configure for grabbing
        _grabbedRigidbody.isKinematic = false;
        _grabbedRigidbody.drag = 10f;
        _grabbedRigidbody.angularDrag = 5f;
        _grabbedRigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        
        // Disable enemy AI/movement components
        MonoBehaviour[] enemyScripts = enemy.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in enemyScripts)
        {
            if (script != this && script.enabled)
            {
                script.enabled = false;
            }
        }
        
        _isGrabbing = true;
        OnEnemyGrabbed?.Invoke(_grabbedEnemy);
    }
    
    void UpdateGrabbedEnemy()
    {
        if (_grabbedEnemy == null || _grabbedRigidbody == null)
        {
            ReleaseEnemy();
            return;
        }
        
        Vector3 targetPosition = _holdPosition.position;
        Vector3 direction = targetPosition - _grabbedEnemy.transform.position;
        
        _grabbedRigidbody.velocity = direction * _grabForce;
        
        // Keep enemy at reasonable distance
        float distance = Vector3.Distance(transform.position, _grabbedEnemy.transform.position);
        if (distance > _maxThrowDistance)
        {
            ReleaseEnemy();
        }
    }
    
    void ThrowEnemy()
    {
        if (_grabbedEnemy == null || _grabbedRigidbody == null)
            return;
            
        Vector3 throwDirection = _playerCamera.transform.forward;
        Vector3 throwVelocity = throwDirection * _throwForce + Vector3.up * _throwUpwardForce;
        
        // Restore original state
        _grabbedRigidbody.isKinematic = _enemyState.wasKinematic;
        _grabbedRigidbody.drag = _enemyState.originalDrag;
        _grabbedRigidbody.angularDrag = _enemyState.originalAngularDrag;
        _grabbedRigidbody.constraints = _enemyState.originalConstraints;
        
        // Apply throw force
        _grabbedRigidbody.velocity = throwVelocity;
        
        // Re-enable enemy scripts after a delay
        StartCoroutine(ReenableEnemyScripts(_grabbedEnemy, 1f));
        
        OnEnemyThrown?.Invoke(_grabbedEnemy);
        
        _grabbedEnemy = null;
        _grabbedRigidbody = null;
        _grabbedCollider = null;
        _isGrabbing = false;
    }
    
    void ReleaseEnemy()
    {
        if (_grabbedEnemy == null)
            return;
            
        if (_grabbedRigidbody != null)
        {
            // Restore original state
            _grabbedRigidbody.isKinematic = _enemyState.wasKinematic;
            _grabbedRigidbody.drag = _enemyState.originalDrag;
            _grabbedRigidbody.angularDrag = _enemyState.originalAngularDrag;
            _grabbedRigidbody.constraints = _enemyState.originalConstraints;
            _grabbedRigidbody.velocity = Vector3.zero;
        }
        
        // Re-enable enemy scripts
        MonoBehaviour[] enemyScripts = _grabbedEnemy.GetComponents<MonoBehaviour>();
        foreach (MonoBehaviour script in enemyScripts)
        {
            if (script != this)
            {
                script.enabled = true;
            }
        }
        
        OnEnemyReleased?.Invoke(_grabbedEnemy);
        
        _grabbedEnemy = null;
        _grabbedRigidbody = null;
        _grabbedCollider = null;
        _isGrabbing = false;
    }
    
    System.Collections.IEnumerator ReenableEnemyScripts(GameObject enemy, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (enemy != null)
        {
            MonoBehaviour[] enemyScripts = enemy.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in enemyScripts)
            {
                if (script != this)
                {
                    script.enabled = true;
                }
            }
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (_playerCamera != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(_playerCamera.transform.position, _playerCamera.transform.forward * _grabRange);
        }
        
        if (_holdPosition != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_holdPosition.position, 0.5f);
        }
    }
}