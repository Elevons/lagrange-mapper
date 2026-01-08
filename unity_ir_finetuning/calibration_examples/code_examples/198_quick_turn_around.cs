// Prompt: quick turn around
// Type: general

using UnityEngine;

public class QuickTurnAround : MonoBehaviour
{
    [Header("Turn Settings")]
    [SerializeField] private float _turnSpeed = 720f;
    [SerializeField] private KeyCode _turnKey = KeyCode.Q;
    [SerializeField] private bool _useMouseInput = false;
    [SerializeField] private float _mouseSensitivity = 2f;
    
    [Header("Turn Direction")]
    [SerializeField] private bool _turnLeft = true;
    [SerializeField] private float _turnAngle = 180f;
    
    [Header("Animation")]
    [SerializeField] private AnimationCurve _turnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private bool _useSmoothing = true;
    
    [Header("Cooldown")]
    [SerializeField] private float _cooldownTime = 1f;
    [SerializeField] private bool _allowContinuousTurn = false;
    
    private bool _isTurning = false;
    private float _currentTurnTime = 0f;
    private float _startRotationY;
    private float _targetRotationY;
    private float _lastTurnTime = 0f;
    private Vector3 _originalRotation;
    
    private void Start()
    {
        _originalRotation = transform.eulerAngles;
    }
    
    private void Update()
    {
        HandleInput();
        ProcessTurn();
    }
    
    private void HandleInput()
    {
        if (_isTurning && !_allowContinuousTurn) return;
        if (Time.time - _lastTurnTime < _cooldownTime) return;
        
        bool shouldTurn = false;
        
        if (_useMouseInput)
        {
            float mouseX = Input.GetAxis("Mouse X");
            if (Mathf.Abs(mouseX) > _mouseSensitivity)
            {
                _turnLeft = mouseX < 0;
                shouldTurn = true;
            }
        }
        
        if (Input.GetKeyDown(_turnKey))
        {
            shouldTurn = true;
        }
        
        if (shouldTurn)
        {
            StartTurn();
        }
    }
    
    private void StartTurn()
    {
        if (!_allowContinuousTurn && _isTurning) return;
        
        _isTurning = true;
        _currentTurnTime = 0f;
        _startRotationY = transform.eulerAngles.y;
        
        float turnDirection = _turnLeft ? -1f : 1f;
        _targetRotationY = _startRotationY + (_turnAngle * turnDirection);
        
        _lastTurnTime = Time.time;
    }
    
    private void ProcessTurn()
    {
        if (!_isTurning) return;
        
        _currentTurnTime += Time.deltaTime;
        float turnDuration = _turnAngle / _turnSpeed;
        float normalizedTime = _currentTurnTime / turnDuration;
        
        if (normalizedTime >= 1f)
        {
            CompleteTurn();
            return;
        }
        
        float rotationY;
        if (_useSmoothing)
        {
            float curveValue = _turnCurve.Evaluate(normalizedTime);
            rotationY = Mathf.LerpAngle(_startRotationY, _targetRotationY, curveValue);
        }
        else
        {
            rotationY = Mathf.LerpAngle(_startRotationY, _targetRotationY, normalizedTime);
        }
        
        Vector3 newRotation = transform.eulerAngles;
        newRotation.y = rotationY;
        transform.eulerAngles = newRotation;
    }
    
    private void CompleteTurn()
    {
        Vector3 finalRotation = transform.eulerAngles;
        finalRotation.y = _targetRotationY;
        transform.eulerAngles = finalRotation;
        
        _isTurning = false;
        _currentTurnTime = 0f;
    }
    
    public void TurnAround()
    {
        StartTurn();
    }
    
    public void TurnLeft()
    {
        _turnLeft = true;
        StartTurn();
    }
    
    public void TurnRight()
    {
        _turnLeft = false;
        StartTurn();
    }
    
    public void SetTurnSpeed(float speed)
    {
        _turnSpeed = Mathf.Max(0.1f, speed);
    }
    
    public void SetTurnAngle(float angle)
    {
        _turnAngle = Mathf.Clamp(angle, 1f, 360f);
    }
    
    public bool IsTurning()
    {
        return _isTurning;
    }
    
    private void OnValidate()
    {
        _turnSpeed = Mathf.Max(0.1f, _turnSpeed);
        _turnAngle = Mathf.Clamp(_turnAngle, 1f, 360f);
        _cooldownTime = Mathf.Max(0f, _cooldownTime);
        _mouseSensitivity = Mathf.Max(0.1f, _mouseSensitivity);
    }
}