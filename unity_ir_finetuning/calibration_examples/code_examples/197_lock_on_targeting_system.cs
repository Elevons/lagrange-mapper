// Prompt: lock-on targeting system
// Type: general

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using System.Linq;

public class LockOnTargetingSystem : MonoBehaviour
{
    [System.Serializable]
    public class TargetData
    {
        public Transform target;
        public float distance;
        public float angle;
        public bool isVisible;
        
        public TargetData(Transform t, float d, float a, bool v)
        {
            target = t;
            distance = d;
            angle = a;
            isVisible = v;
        }
    }

    [Header("Targeting Settings")]
    [SerializeField] private float _maxLockOnDistance = 50f;
    [SerializeField] private float _maxLockOnAngle = 60f;
    [SerializeField] private LayerMask _targetLayers = -1;
    [SerializeField] private LayerMask _obstacleLayer = -1;
    [SerializeField] private string[] _targetTags = { "Enemy" };
    
    [Header("Camera Settings")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _lockOnSmoothTime = 0.3f;
    [SerializeField] private Vector3 _targetOffset = Vector3.up;
    
    [Header("Input")]
    [SerializeField] private KeyCode _lockOnKey = KeyCode.Tab;
    [SerializeField] private KeyCode _switchTargetKey = KeyCode.R;
    [SerializeField] private bool _useMouseForSwitching = true;
    
    [Header("UI")]
    [SerializeField] private GameObject _lockOnIndicatorPrefab;
    [SerializeField] private Canvas _uiCanvas;
    
    [Header("Events")]
    public UnityEvent<Transform> OnTargetLocked;
    public UnityEvent OnTargetUnlocked;
    public UnityEvent<Transform> OnTargetSwitched;

    private Transform _currentTarget;
    private List<TargetData> _availableTargets = new List<TargetData>();
    private GameObject _lockOnIndicator;
    private Vector3 _cameraVelocity;
    private bool _isLockingOn = false;
    private int _currentTargetIndex = 0;
    private Camera _mainCamera;

    public Transform CurrentTarget => _currentTarget;
    public bool IsLockingOn => _isLockingOn;

    private void Start()
    {
        if (_cameraTransform == null)
            _cameraTransform = Camera.main?.transform;
            
        _mainCamera = _cameraTransform?.GetComponent<Camera>();
        
        if (_uiCanvas == null)
            _uiCanvas = FindObjectOfType<Canvas>();
    }

    private void Update()
    {
        HandleInput();
        UpdateTargetList();
        UpdateLockOn();
        UpdateUI();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(_lockOnKey))
        {
            if (_isLockingOn)
                UnlockTarget();
            else
                LockOnToNearestTarget();
        }

        if (Input.GetKeyDown(_switchTargetKey) && _isLockingOn)
        {
            SwitchToNextTarget();
        }

        if (_useMouseForSwitching && _isLockingOn)
        {
            float mouseX = Input.GetAxis("Mouse X");
            if (Mathf.Abs(mouseX) > 0.5f)
            {
                if (mouseX > 0)
                    SwitchToNextTarget();
                else
                    SwitchToPreviousTarget();
            }
        }
    }

    private void UpdateTargetList()
    {
        _availableTargets.Clear();

        if (_cameraTransform == null) return;

        Collider[] potentialTargets = Physics.OverlapSphere(transform.position, _maxLockOnDistance, _targetLayers);

        foreach (Collider col in potentialTargets)
        {
            if (col.transform == transform) continue;

            bool isValidTag = false;
            foreach (string tag in _targetTags)
            {
                if (col.CompareTag(tag))
                {
                    isValidTag = true;
                    break;
                }
            }

            if (!isValidTag) continue;

            Vector3 directionToTarget = (col.transform.position - _cameraTransform.position).normalized;
            float angle = Vector3.Angle(_cameraTransform.forward, directionToTarget);

            if (angle <= _maxLockOnAngle)
            {
                float distance = Vector3.Distance(transform.position, col.transform.position);
                bool isVisible = IsTargetVisible(col.transform);

                _availableTargets.Add(new TargetData(col.transform, distance, angle, isVisible));
            }
        }

        _availableTargets = _availableTargets.OrderBy(t => t.distance).ToList();

        if (_isLockingOn && _currentTarget != null)
        {
            bool currentTargetStillValid = _availableTargets.Any(t => t.target == _currentTarget);
            if (!currentTargetStillValid)
            {
                UnlockTarget();
            }
        }
    }

    private bool IsTargetVisible(Transform target)
    {
        if (_cameraTransform == null || target == null) return false;

        Vector3 directionToTarget = (target.position + _targetOffset - _cameraTransform.position);
        float distanceToTarget = directionToTarget.magnitude;

        if (Physics.Raycast(_cameraTransform.position, directionToTarget.normalized, out RaycastHit hit, distanceToTarget, _obstacleLayer))
        {
            return hit.transform == target;
        }

        return true;
    }

    public void LockOnToNearestTarget()
    {
        if (_availableTargets.Count == 0) return;

        var visibleTargets = _availableTargets.Where(t => t.isVisible).ToList();
        if (visibleTargets.Count == 0) return;

        _currentTarget = visibleTargets[0].target;
        _currentTargetIndex = 0;
        _isLockingOn = true;

        CreateLockOnIndicator();
        OnTargetLocked?.Invoke(_currentTarget);
    }

    public void LockOnToTarget(Transform target)
    {
        if (target == null) return;

        var targetData = _availableTargets.FirstOrDefault(t => t.target == target);
        if (targetData == null) return;

        _currentTarget = target;
        _currentTargetIndex = _availableTargets.IndexOf(targetData);
        _isLockingOn = true;

        CreateLockOnIndicator();
        OnTargetLocked?.Invoke(_currentTarget);
    }

    public void UnlockTarget()
    {
        _currentTarget = null;
        _isLockingOn = false;
        _currentTargetIndex = 0;

        DestroyLockOnIndicator();
        OnTargetUnlocked?.Invoke();
    }

    public void SwitchToNextTarget()
    {
        if (_availableTargets.Count <= 1) return;

        var visibleTargets = _availableTargets.Where(t => t.isVisible).ToList();
        if (visibleTargets.Count <= 1) return;

        int currentVisibleIndex = visibleTargets.FindIndex(t => t.target == _currentTarget);
        currentVisibleIndex = (currentVisibleIndex + 1) % visibleTargets.Count;

        Transform previousTarget = _currentTarget;
        _currentTarget = visibleTargets[currentVisibleIndex].target;
        _currentTargetIndex = _availableTargets.FindIndex(t => t.target == _currentTarget);

        OnTargetSwitched?.Invoke(_currentTarget);
    }

    public void SwitchToPreviousTarget()
    {
        if (_availableTargets.Count <= 1) return;

        var visibleTargets = _availableTargets.Where(t => t.isVisible).ToList();
        if (visibleTargets.Count <= 1) return;

        int currentVisibleIndex = visibleTargets.FindIndex(t => t.target == _currentTarget);
        currentVisibleIndex = (currentVisibleIndex - 1 + visibleTargets.Count) % visibleTargets.Count;

        Transform previousTarget = _currentTarget;
        _currentTarget = visibleTargets[currentVisibleIndex].target;
        _currentTargetIndex = _availableTargets.FindIndex(t => t.target == _currentTarget);

        OnTargetSwitched?.Invoke(_currentTarget);
    }

    private void UpdateLockOn()
    {
        if (!_isLockingOn || _currentTarget == null || _cameraTransform == null) return;

        Vector3 targetPosition = _currentTarget.position + _targetOffset;
        Vector3 desiredPosition = Vector3.SmoothDamp(_cameraTransform.position, targetPosition, ref _cameraVelocity, _lockOnSmoothTime);
        
        Vector3 direction = (targetPosition - _cameraTransform.position).normalized;
        Quaternion targetRotation = Quaternion.LookRotation(direction);
        _cameraTransform.rotation = Quaternion.Slerp(_cameraTransform.rotation, targetRotation, Time.deltaTime / _lockOnSmoothTime);
    }

    private void CreateLockOnIndicator()
    {
        if (_lockOnIndicatorPrefab == null || _uiCanvas == null) return;

        DestroyLockOnIndicator();
        _lockOnIndicator = Instantiate(_lockOnIndicatorPrefab, _uiCanvas.transform);
    }

    private void DestroyLockOnIndicator()
    {
        if (_lockOnIndicator != null)
        {
            Destroy(_lockOnIndicator);
            _lockOnIndicator = null;
        }
    }

    private void UpdateUI()
    {
        if (_lockOnIndicator == null || _currentTarget == null || _mainCamera == null) return;

        Vector3 screenPosition = _mainCamera.WorldToScreenPoint(_currentTarget.position + _targetOffset);
        
        if (screenPosition.z > 0)
        {
            _lockOnIndicator.transform.position = screenPosition;
            _lockOnIndicator.SetActive(true);
        }
        else
        {
            _lockOnIndicator.SetActive(false);
        }
    }

    public List<Transform> GetAvailableTargets()
    {
        return _availableTargets.Select(t => t.target).ToList();
    }

    public float GetDistanceToCurrentTarget()
    {
        if (_currentTarget == null) return 0f;
        return Vector3.Distance(transform.position, _currentTarget.position);
    }

    public Vector3 GetDirectionToCurrentTarget()
    {
        if (_currentTarget == null) return Vector3.zero;
        return (_currentTarget.position - transform.position).normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _maxLockOnDistance);

        if (_cameraTransform != null)
        {
            Gizmos.color = Color.red;
            Vector3 leftBoundary = Quaternion.AngleAxis(-_maxLockOnAngle, _cameraTransform.up) * _cameraTransform.forward;
            Vector3 rightBoundary = Quaternion.AngleAxis(_maxLockOnAngle, _cameraTransform.up) * _cameraTransform.forward;
            
            Gizmos.DrawRay(_cameraTransform.position, leftBoundary * _maxLockOnDistance);
            Gizmos.DrawRay(_cameraTransform.position, rightBoundary * _maxLockOnDistance);
        }

        if (_currentTarget != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, _currentTarget.position);
            Gizmos.DrawWireSphere(_currentTarget.position, 1f);
        }
    }
}