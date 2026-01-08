// Prompt: flying enemy that circles overhead
// Type: combat

using UnityEngine;

public class FlyingEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _circleRadius = 10f;
    [SerializeField] private float _circleSpeed = 2f;
    [SerializeField] private float _hoverHeight = 8f;
    [SerializeField] private bool _clockwise = true;
    
    [Header("Target Settings")]
    [SerializeField] private Transform _target;
    [SerializeField] private bool _followPlayer = true;
    [SerializeField] private string _playerTag = "Player";
    
    [Header("Vertical Movement")]
    [SerializeField] private float _bobAmount = 0.5f;
    [SerializeField] private float _bobSpeed = 3f;
    
    [Header("Rotation")]
    [SerializeField] private bool _faceMovementDirection = true;
    [SerializeField] private float _rotationSpeed = 5f;
    
    private Vector3 _centerPoint;
    private float _currentAngle;
    private float _bobTimer;
    private Vector3 _lastPosition;
    private GameObject _player;
    
    private void Start()
    {
        if (_followPlayer)
        {
            _player = GameObject.FindGameObjectWithTag(_playerTag);
            if (_player != null)
            {
                _target = _player.transform;
            }
        }
        
        if (_target == null)
        {
            _centerPoint = transform.position;
        }
        else
        {
            _centerPoint = new Vector3(_target.position.x, _target.position.y + _hoverHeight, _target.position.z);
        }
        
        _lastPosition = transform.position;
        _currentAngle = Random.Range(0f, 360f);
    }
    
    private void Update()
    {
        UpdateCenterPoint();
        UpdateCircularMovement();
        UpdateVerticalBobbing();
        UpdateRotation();
    }
    
    private void UpdateCenterPoint()
    {
        if (_target != null)
        {
            Vector3 targetCenter = new Vector3(_target.position.x, _target.position.y + _hoverHeight, _target.position.z);
            _centerPoint = Vector3.Lerp(_centerPoint, targetCenter, Time.deltaTime * 2f);
        }
    }
    
    private void UpdateCircularMovement()
    {
        float angleIncrement = _circleSpeed * Time.deltaTime;
        if (!_clockwise)
        {
            angleIncrement = -angleIncrement;
        }
        
        _currentAngle += angleIncrement;
        if (_currentAngle >= 360f)
        {
            _currentAngle -= 360f;
        }
        else if (_currentAngle < 0f)
        {
            _currentAngle += 360f;
        }
        
        float radians = _currentAngle * Mathf.Deg2Rad;
        Vector3 circlePosition = new Vector3(
            _centerPoint.x + Mathf.Cos(radians) * _circleRadius,
            _centerPoint.y,
            _centerPoint.z + Mathf.Sin(radians) * _circleRadius
        );
        
        transform.position = circlePosition;
    }
    
    private void UpdateVerticalBobbing()
    {
        _bobTimer += Time.deltaTime * _bobSpeed;
        float bobOffset = Mathf.Sin(_bobTimer) * _bobAmount;
        
        Vector3 currentPos = transform.position;
        currentPos.y += bobOffset;
        transform.position = currentPos;
    }
    
    private void UpdateRotation()
    {
        if (_faceMovementDirection)
        {
            Vector3 movementDirection = transform.position - _lastPosition;
            if (movementDirection.magnitude > 0.01f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }
        }
        
        _lastPosition = transform.position;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(_playerTag))
        {
            // Handle player collision
            Debug.Log("Flying enemy detected player");
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying ? _centerPoint : transform.position;
        
        // Draw circle path
        for (int i = 0; i < 36; i++)
        {
            float angle1 = i * 10f * Mathf.Deg2Rad;
            float angle2 = (i + 1) * 10f * Mathf.Deg2Rad;
            
            Vector3 point1 = new Vector3(
                center.x + Mathf.Cos(angle1) * _circleRadius,
                center.y,
                center.z + Mathf.Sin(angle1) * _circleRadius
            );
            
            Vector3 point2 = new Vector3(
                center.x + Mathf.Cos(angle2) * _circleRadius,
                center.y,
                center.z + Mathf.Sin(angle2) * _circleRadius
            );
            
            Gizmos.DrawLine(point1, point2);
        }
        
        // Draw center point
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(center, 0.5f);
        
        // Draw hover height indicator
        if (_target != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(_target.position, center);
        }
    }
}