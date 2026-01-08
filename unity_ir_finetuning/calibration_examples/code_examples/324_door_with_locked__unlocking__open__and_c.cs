// Prompt: door with locked, unlocking, open, and closing states - when locked it shakes slightly, when unlocking it plays key-turning sound for 3 seconds, when open it stays for 10 seconds then auto-closes, the door's material emission color changes with each state
// Type: environment

using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class Door : MonoBehaviour
{
    public enum DoorState
    {
        Locked,
        Unlocking,
        Open,
        Closing
    }

    [Header("Door Settings")]
    [SerializeField] private DoorState _currentState = DoorState.Locked;
    [SerializeField] private float _unlockDuration = 3f;
    [SerializeField] private float _openDuration = 10f;
    [SerializeField] private float _openAngle = 90f;
    [SerializeField] private float _openSpeed = 2f;

    [Header("Shake Settings")]
    [SerializeField] private float _shakeIntensity = 0.1f;
    [SerializeField] private float _shakeSpeed = 10f;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _unlockingSound;
    [SerializeField] private AudioClip _openSound;
    [SerializeField] private AudioClip _closeSound;

    [Header("Visual Settings")]
    [SerializeField] private Renderer _doorRenderer;
    [SerializeField] private string _emissionProperty = "_EmissionColor";
    [SerializeField] private Color _lockedColor = Color.red;
    [SerializeField] private Color _unlockingColor = Color.yellow;
    [SerializeField] private Color _openColor = Color.green;
    [SerializeField] private Color _closingColor = Color.blue;

    [Header("Events")]
    public UnityEvent OnDoorUnlocked;
    public UnityEvent OnDoorOpened;
    public UnityEvent OnDoorClosed;

    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private Quaternion _openRotation;
    private Material _doorMaterial;
    private Coroutine _currentStateCoroutine;
    private bool _isPlayerNear = false;

    private void Start()
    {
        _originalPosition = transform.position;
        _originalRotation = transform.rotation;
        _openRotation = _originalRotation * Quaternion.Euler(0, _openAngle, 0);

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_doorRenderer == null)
            _doorRenderer = GetComponent<Renderer>();

        if (_doorRenderer != null)
        {
            _doorMaterial = _doorRenderer.material;
            UpdateEmissionColor();
        }

        SetState(_currentState);
    }

    private void Update()
    {
        if (_currentState == DoorState.Locked)
        {
            ApplyShake();
        }

        if (_isPlayerNear && Input.GetKeyDown(KeyCode.E))
        {
            TryInteract();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerNear = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            _isPlayerNear = false;
        }
    }

    public void TryInteract()
    {
        if (_currentState == DoorState.Locked)
        {
            SetState(DoorState.Unlocking);
        }
    }

    public void SetState(DoorState newState)
    {
        if (_currentState == newState) return;

        if (_currentStateCoroutine != null)
        {
            StopCoroutine(_currentStateCoroutine);
        }

        _currentState = newState;
        UpdateEmissionColor();

        switch (_currentState)
        {
            case DoorState.Locked:
                break;
            case DoorState.Unlocking:
                _currentStateCoroutine = StartCoroutine(UnlockingSequence());
                break;
            case DoorState.Open:
                _currentStateCoroutine = StartCoroutine(OpenSequence());
                break;
            case DoorState.Closing:
                _currentStateCoroutine = StartCoroutine(ClosingSequence());
                break;
        }
    }

    private void ApplyShake()
    {
        if (_shakeIntensity <= 0) return;

        float shakeX = Mathf.Sin(Time.time * _shakeSpeed) * _shakeIntensity;
        float shakeZ = Mathf.Cos(Time.time * _shakeSpeed * 1.1f) * _shakeIntensity;
        
        transform.position = _originalPosition + new Vector3(shakeX, 0, shakeZ);
    }

    private IEnumerator UnlockingSequence()
    {
        if (_unlockingSound != null && _audioSource != null)
        {
            _audioSource.clip = _unlockingSound;
            _audioSource.Play();
        }

        yield return new WaitForSeconds(_unlockDuration);

        OnDoorUnlocked?.Invoke();
        SetState(DoorState.Open);
    }

    private IEnumerator OpenSequence()
    {
        if (_openSound != null && _audioSource != null)
        {
            _audioSource.clip = _openSound;
            _audioSource.Play();
        }

        // Reset position from shake
        transform.position = _originalPosition;

        // Rotate door to open position
        float elapsedTime = 0f;
        while (elapsedTime < 1f / _openSpeed)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime * _openSpeed;
            transform.rotation = Quaternion.Lerp(_originalRotation, _openRotation, t);
            yield return null;
        }

        transform.rotation = _openRotation;
        OnDoorOpened?.Invoke();

        // Wait for open duration
        yield return new WaitForSeconds(_openDuration);

        SetState(DoorState.Closing);
    }

    private IEnumerator ClosingSequence()
    {
        if (_closeSound != null && _audioSource != null)
        {
            _audioSource.clip = _closeSound;
            _audioSource.Play();
        }

        // Rotate door back to closed position
        float elapsedTime = 0f;
        while (elapsedTime < 1f / _openSpeed)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime * _openSpeed;
            transform.rotation = Quaternion.Lerp(_openRotation, _originalRotation, t);
            yield return null;
        }

        transform.rotation = _originalRotation;
        OnDoorClosed?.Invoke();
        SetState(DoorState.Locked);
    }

    private void UpdateEmissionColor()
    {
        if (_doorMaterial == null) return;

        Color targetColor = _currentState switch
        {
            DoorState.Locked => _lockedColor,
            DoorState.Unlocking => _unlockingColor,
            DoorState.Open => _openColor,
            DoorState.Closing => _closingColor,
            _ => _lockedColor
        };

        _doorMaterial.SetColor(_emissionProperty, targetColor);
    }

    public DoorState GetCurrentState()
    {
        return _currentState;
    }

    public bool IsLocked()
    {
        return _currentState == DoorState.Locked;
    }

    public bool IsOpen()
    {
        return _currentState == DoorState.Open;
    }

    private void OnDestroy()
    {
        if (_currentStateCoroutine != null)
        {
            StopCoroutine(_currentStateCoroutine);
        }
    }
}