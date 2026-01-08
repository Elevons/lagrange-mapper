// Prompt: compass pointing to target
// Type: general

using UnityEngine;
using UnityEngine.UI;

public class Compass : MonoBehaviour
{
    [Header("Compass Settings")]
    [SerializeField] private Transform _compassNeedle;
    [SerializeField] private Transform _target;
    [SerializeField] private bool _useClosestPlayerAsTarget = true;
    [SerializeField] private string _targetTag = "Player";
    
    [Header("Rotation Settings")]
    [SerializeField] private float _rotationSpeed = 5f;
    [SerializeField] private bool _smoothRotation = true;
    [SerializeField] private Vector3 _rotationAxis = Vector3.forward;
    [SerializeField] private float _rotationOffset = 0f;
    
    [Header("Distance Settings")]
    [SerializeField] private bool _hideWhenClose = false;
    [SerializeField] private float _hideDistance = 5f;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _compassObject;
    
    [Header("Debug")]
    [SerializeField] private bool _showDebugLine = false;
    [SerializeField] private Color _debugLineColor = Color.red;
    
    private Camera _playerCamera;
    private Vector3 _lastKnownTargetPosition;
    private bool _hasTarget;
    
    private void Start()
    {
        InitializeCompass();
        FindTarget();
    }
    
    private void Update()
    {
        if (_useClosestPlayerAsTarget && (_target == null || !_target.gameObject.activeInHierarchy))
        {
            FindTarget();
        }
        
        if (_target != null && _target.gameObject.activeInHierarchy)
        {
            _lastKnownTargetPosition = _target.position;
            _hasTarget = true;
        }
        
        if (_hasTarget)
        {
            UpdateCompassRotation();
            UpdateVisibility();
        }
        else
        {
            HideCompass();
        }
    }
    
    private void InitializeCompass()
    {
        if (_compassNeedle == null)
            _compassNeedle = transform;
            
        if (_playerCamera == null)
            _playerCamera = Camera.main;
            
        if (_canvasGroup == null)
            _canvasGroup = GetComponent<CanvasGroup>();
            
        if (_compassObject == null)
            _compassObject = gameObject;
    }
    
    private void FindTarget()
    {
        if (_useClosestPlayerAsTarget)
        {
            GameObject[] targets = GameObject.FindGameObjectsWithTag(_targetTag);
            float closestDistance = Mathf.Infinity;
            Transform closestTarget = null;
            
            foreach (GameObject targetObj in targets)
            {
                if (targetObj.activeInHierarchy)
                {
                    float distance = Vector3.Distance(transform.position, targetObj.transform.position);
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = targetObj.transform;
                    }
                }
            }
            
            _target = closestTarget;
        }
    }
    
    private void UpdateCompassRotation()
    {
        Vector3 directionToTarget = (_lastKnownTargetPosition - transform.position).normalized;
        
        if (_playerCamera != null)
        {
            Vector3 cameraForward = _playerCamera.transform.forward;
            cameraForward.y = 0;
            cameraForward.Normalize();
            
            directionToTarget.y = 0;
            directionToTarget.Normalize();
            
            float angle = Vector3.SignedAngle(cameraForward, directionToTarget, Vector3.up);
            angle += _rotationOffset;
            
            Quaternion targetRotation = Quaternion.AngleAxis(angle, _rotationAxis);
            
            if (_smoothRotation)
            {
                _compassNeedle.rotation = Quaternion.Lerp(_compassNeedle.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
            else
            {
                _compassNeedle.rotation = targetRotation;
            }
        }
        else
        {
            float angle = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
            angle += _rotationOffset;
            
            Quaternion targetRotation = Quaternion.AngleAxis(angle, _rotationAxis);
            
            if (_smoothRotation)
            {
                _compassNeedle.rotation = Quaternion.Lerp(_compassNeedle.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
            else
            {
                _compassNeedle.rotation = targetRotation;
            }
        }
    }
    
    private void UpdateVisibility()
    {
        if (_hideWhenClose)
        {
            float distance = Vector3.Distance(transform.position, _lastKnownTargetPosition);
            bool shouldShow = distance > _hideDistance;
            
            if (_canvasGroup != null)
            {
                float targetAlpha = shouldShow ? 1f : 0f;
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, targetAlpha, _rotationSpeed * Time.deltaTime);
            }
            else if (_compassObject != null)
            {
                _compassObject.SetActive(shouldShow);
            }
        }
        else
        {
            ShowCompass();
        }
    }
    
    private void ShowCompass()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 1f, _rotationSpeed * Time.deltaTime);
        }
        else if (_compassObject != null)
        {
            _compassObject.SetActive(true);
        }
    }
    
    private void HideCompass()
    {
        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 0f, _rotationSpeed * Time.deltaTime);
        }
        else if (_compassObject != null)
        {
            _compassObject.SetActive(false);
        }
    }
    
    public void SetTarget(Transform newTarget)
    {
        _target = newTarget;
        _useClosestPlayerAsTarget = false;
        _hasTarget = _target != null;
    }
    
    public void SetTargetPosition(Vector3 position)
    {
        _lastKnownTargetPosition = position;
        _target = null;
        _useClosestPlayerAsTarget = false;
        _hasTarget = true;
    }
    
    public void ClearTarget()
    {
        _target = null;
        _hasTarget = false;
        _useClosestPlayerAsTarget = false;
    }
    
    public float GetDistanceToTarget()
    {
        if (_hasTarget)
        {
            return Vector3.Distance(transform.position, _lastKnownTargetPosition);
        }
        return -1f;
    }
    
    public Vector3 GetDirectionToTarget()
    {
        if (_hasTarget)
        {
            return (_lastKnownTargetPosition - transform.position).normalized;
        }
        return Vector3.zero;
    }
    
    private void OnDrawGizmos()
    {
        if (_showDebugLine && _hasTarget)
        {
            Gizmos.color = _debugLineColor;
            Gizmos.DrawLine(transform.position, _lastKnownTargetPosition);
            Gizmos.DrawWireSphere(_lastKnownTargetPosition, 1f);
            
            if (_hideWhenClose)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, _hideDistance);
            }
        }
    }
}