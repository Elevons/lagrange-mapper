// Prompt: spectator camera
// Type: general

using UnityEngine;

public class SpectatorCamera : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private float _fastMoveSpeed = 20f;
    [SerializeField] private float _smoothTime = 0.1f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float _mouseSensitivity = 2f;
    [SerializeField] private float _maxLookAngle = 80f;
    
    [Header("Zoom Settings")]
    [SerializeField] private float _zoomSpeed = 2f;
    [SerializeField] private float _minFOV = 10f;
    [SerializeField] private float _maxFOV = 90f;
    
    [Header("Input Settings")]
    [SerializeField] private KeyCode _fastMoveKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode _upKey = KeyCode.E;
    [SerializeField] private KeyCode _downKey = KeyCode.Q;
    [SerializeField] private bool _invertY = false;
    
    private Camera _camera;
    private Vector3 _velocity;
    private float _rotationX = 0f;
    private float _rotationY = 0f;
    private bool _isMouseLocked = true;
    
    private void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = Camera.main;
        }
        
        Vector3 eulerAngles = transform.eulerAngles;
        _rotationX = eulerAngles.x;
        _rotationY = eulerAngles.y;
        
        LockCursor();
    }
    
    private void Update()
    {
        HandleInput();
        HandleMouseLook();
        HandleMovement();
        HandleZoom();
    }
    
    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorLock();
        }
    }
    
    private void HandleMouseLook()
    {
        if (!_isMouseLocked) return;
        
        float mouseX = Input.GetAxis("Mouse X") * _mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * _mouseSensitivity;
        
        if (_invertY)
            mouseY = -mouseY;
        
        _rotationY += mouseX;
        _rotationX -= mouseY;
        _rotationX = Mathf.Clamp(_rotationX, -_maxLookAngle, _maxLookAngle);
        
        transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
    }
    
    private void HandleMovement()
    {
        Vector3 inputDirection = Vector3.zero;
        
        if (Input.GetKey(KeyCode.W)) inputDirection += Vector3.forward;
        if (Input.GetKey(KeyCode.S)) inputDirection += Vector3.back;
        if (Input.GetKey(KeyCode.A)) inputDirection += Vector3.left;
        if (Input.GetKey(KeyCode.D)) inputDirection += Vector3.right;
        if (Input.GetKey(_upKey)) inputDirection += Vector3.up;
        if (Input.GetKey(_downKey)) inputDirection += Vector3.down;
        
        inputDirection = transform.TransformDirection(inputDirection);
        inputDirection.Normalize();
        
        float currentSpeed = Input.GetKey(_fastMoveKey) ? _fastMoveSpeed : _moveSpeed;
        Vector3 targetVelocity = inputDirection * currentSpeed;
        
        _velocity = Vector3.Lerp(_velocity, targetVelocity, Time.deltaTime / _smoothTime);
        transform.position += _velocity * Time.deltaTime;
    }
    
    private void HandleZoom()
    {
        if (_camera == null) return;
        
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float newFOV = _camera.fieldOfView - scroll * _zoomSpeed * 10f;
            _camera.fieldOfView = Mathf.Clamp(newFOV, _minFOV, _maxFOV);
        }
    }
    
    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        _isMouseLocked = true;
    }
    
    private void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _isMouseLocked = false;
    }
    
    private void ToggleCursorLock()
    {
        if (_isMouseLocked)
        {
            UnlockCursor();
        }
        else
        {
            LockCursor();
        }
    }
    
    public void SetPosition(Vector3 position)
    {
        transform.position = position;
        _velocity = Vector3.zero;
    }
    
    public void SetRotation(Vector3 eulerAngles)
    {
        transform.eulerAngles = eulerAngles;
        _rotationX = eulerAngles.x;
        _rotationY = eulerAngles.y;
    }
    
    public void SetMoveSpeed(float speed)
    {
        _moveSpeed = Mathf.Max(0f, speed);
    }
    
    public void SetMouseSensitivity(float sensitivity)
    {
        _mouseSensitivity = Mathf.Max(0.1f, sensitivity);
    }
    
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _isMouseLocked)
        {
            LockCursor();
        }
    }
}