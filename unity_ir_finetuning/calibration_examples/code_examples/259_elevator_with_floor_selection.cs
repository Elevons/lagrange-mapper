// Prompt: elevator with floor selection
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Elevator : MonoBehaviour
{
    [System.Serializable]
    public class Floor
    {
        public string floorName;
        public Transform floorPosition;
        public Button floorButton;
        public bool isAccessible = true;
    }

    [Header("Elevator Settings")]
    [SerializeField] private Transform _elevatorCabin;
    [SerializeField] private float _moveSpeed = 2f;
    [SerializeField] private float _doorOpenTime = 3f;
    [SerializeField] private AnimationCurve _movementCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Floors")]
    [SerializeField] private List<Floor> _floors = new List<Floor>();
    [SerializeField] private int _currentFloorIndex = 0;

    [Header("Doors")]
    [SerializeField] private Transform _leftDoor;
    [SerializeField] private Transform _rightDoor;
    [SerializeField] private Vector3 _doorOpenOffset = new Vector3(1.5f, 0, 0);
    [SerializeField] private float _doorSpeed = 1f;

    [Header("UI")]
    [SerializeField] private Text _floorDisplay;
    [SerializeField] private Text _statusDisplay;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _movingSound;
    [SerializeField] private AudioClip _arrivalSound;
    [SerializeField] private AudioClip _doorSound;

    [Header("Lights")]
    [SerializeField] private Light _elevatorLight;
    [SerializeField] private Color _normalLightColor = Color.white;
    [SerializeField] private Color _movingLightColor = Color.yellow;

    private bool _isMoving = false;
    private bool _doorsOpen = false;
    private Vector3 _leftDoorClosedPos;
    private Vector3 _rightDoorClosedPos;
    private Queue<int> _floorQueue = new Queue<int>();
    private Coroutine _currentMovement;

    private void Start()
    {
        InitializeElevator();
        SetupFloorButtons();
        UpdateDisplay();
        StartCoroutine(OpenDoorsRoutine());
    }

    private void InitializeElevator()
    {
        if (_elevatorCabin == null)
            _elevatorCabin = transform;

        if (_audioSource == null)
            _audioSource = GetComponent<AudioSource>();

        if (_leftDoor != null)
            _leftDoorClosedPos = _leftDoor.localPosition;

        if (_rightDoor != null)
            _rightDoorClosedPos = _rightDoor.localPosition;

        if (_floors.Count > 0 && _currentFloorIndex < _floors.Count)
        {
            _elevatorCabin.position = _floors[_currentFloorIndex].floorPosition.position;
        }

        if (_elevatorLight != null)
            _elevatorLight.color = _normalLightColor;
    }

    private void SetupFloorButtons()
    {
        for (int i = 0; i < _floors.Count; i++)
        {
            int floorIndex = i;
            if (_floors[i].floorButton != null)
            {
                _floors[i].floorButton.onClick.AddListener(() => RequestFloor(floorIndex));
                UpdateButtonState(i);
            }
        }
    }

    public void RequestFloor(int floorIndex)
    {
        if (floorIndex < 0 || floorIndex >= _floors.Count)
            return;

        if (!_floors[floorIndex].isAccessible)
        {
            UpdateStatus("Floor not accessible");
            return;
        }

        if (floorIndex == _currentFloorIndex)
        {
            if (!_doorsOpen)
                StartCoroutine(OpenDoorsRoutine());
            return;
        }

        if (!_floorQueue.Contains(floorIndex))
        {
            _floorQueue.Enqueue(floorIndex);
            UpdateButtonState(floorIndex);
        }

        if (!_isMoving)
            StartCoroutine(ProcessFloorQueue());
    }

    private IEnumerator ProcessFloorQueue()
    {
        while (_floorQueue.Count > 0)
        {
            int targetFloor = _floorQueue.Dequeue();
            yield return StartCoroutine(MoveToFloor(targetFloor));
            UpdateButtonState(targetFloor);
        }
    }

    private IEnumerator MoveToFloor(int targetFloorIndex)
    {
        if (targetFloorIndex == _currentFloorIndex)
            yield break;

        _isMoving = true;
        UpdateStatus("Moving...");

        if (_elevatorLight != null)
            _elevatorLight.color = _movingLightColor;

        yield return StartCoroutine(CloseDoorsRoutine());

        Vector3 startPos = _elevatorCabin.position;
        Vector3 targetPos = _floors[targetFloorIndex].floorPosition.position;
        float distance = Vector3.Distance(startPos, targetPos);
        float duration = distance / _moveSpeed;

        PlaySound(_movingSound, true);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = _movementCurve.Evaluate(elapsed / duration);
            _elevatorCabin.position = Vector3.Lerp(startPos, targetPos, progress);
            yield return null;
        }

        _elevatorCabin.position = targetPos;
        _currentFloorIndex = targetFloorIndex;

        if (_audioSource != null && _audioSource.isPlaying)
            _audioSource.Stop();

        PlaySound(_arrivalSound);

        if (_elevatorLight != null)
            _elevatorLight.color = _normalLightColor;

        _isMoving = false;
        UpdateDisplay();
        UpdateStatus("Arrived");

        yield return StartCoroutine(OpenDoorsRoutine());
    }

    private IEnumerator OpenDoorsRoutine()
    {
        if (_doorsOpen)
            yield break;

        PlaySound(_doorSound);
        UpdateStatus("Opening doors...");

        Vector3 leftTarget = _leftDoorClosedPos - _doorOpenOffset;
        Vector3 rightTarget = _rightDoorClosedPos + _doorOpenOffset;

        float elapsed = 0f;
        float duration = 1f / _doorSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            if (_leftDoor != null)
                _leftDoor.localPosition = Vector3.Lerp(_leftDoorClosedPos, leftTarget, progress);

            if (_rightDoor != null)
                _rightDoor.localPosition = Vector3.Lerp(_rightDoorClosedPos, rightTarget, progress);

            yield return null;
        }

        _doorsOpen = true;
        UpdateStatus("Doors open");

        yield return new WaitForSeconds(_doorOpenTime);

        if (!_isMoving && _floorQueue.Count == 0)
            yield return StartCoroutine(CloseDoorsRoutine());
    }

    private IEnumerator CloseDoorsRoutine()
    {
        if (!_doorsOpen)
            yield break;

        PlaySound(_doorSound);
        UpdateStatus("Closing doors...");

        Vector3 leftCurrent = _leftDoor != null ? _leftDoor.localPosition : _leftDoorClosedPos;
        Vector3 rightCurrent = _rightDoor != null ? _rightDoor.localPosition : _rightDoorClosedPos;

        float elapsed = 0f;
        float duration = 1f / _doorSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / duration;

            if (_leftDoor != null)
                _leftDoor.localPosition = Vector3.Lerp(leftCurrent, _leftDoorClosedPos, progress);

            if (_rightDoor != null)
                _rightDoor.localPosition = Vector3.Lerp(rightCurrent, _rightDoorClosedPos, progress);

            yield return null;
        }

        _doorsOpen = false;
        UpdateStatus("Doors closed");
    }

    private void UpdateDisplay()
    {
        if (_floorDisplay != null && _currentFloorIndex < _floors.Count)
        {
            _floorDisplay.text = _floors[_currentFloorIndex].floorName;
        }
    }

    private void UpdateStatus(string status)
    {
        if (_statusDisplay != null)
            _statusDisplay.text = status;
    }

    private void UpdateButtonState(int floorIndex)
    {
        if (floorIndex < 0 || floorIndex >= _floors.Count)
            return;

        Button button = _floors[floorIndex].floorButton;
        if (button != null)
        {
            bool isQueued = _floorQueue.Contains(floorIndex);
            bool isCurrent = floorIndex == _currentFloorIndex;
            
            ColorBlock colors = button.colors;
            colors.normalColor = isCurrent ? Color.green : (isQueued ? Color.yellow : Color.white);
            button.colors = colors;
            
            button.interactable = _floors[floorIndex].isAccessible && !isCurrent;
        }
    }

    private void PlaySound(AudioClip clip, bool loop = false)
    {
        if (_audioSource != null && clip != null)
        {
            _audioSource.clip = clip;
            _audioSource.loop = loop;
            _audioSource.Play();
        }
    }

    public void SetFloorAccessibility(int floorIndex, bool accessible)
    {
        if (floorIndex >= 0 && floorIndex < _floors.Count)
        {
            _floors[floorIndex].isAccessible = accessible;
            UpdateButtonState(floorIndex);
        }
    }

    public int GetCurrentFloor()
    {
        return _currentFloorIndex;
    }

    public bool IsMoving()
    {
        return _isMoving;
    }

    public bool AreDoorsOpen()
    {
        return _doorsOpen;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && _doorsOpen)
        {
            UpdateStatus("Welcome aboard!");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player") && _doorsOpen)
        {
            UpdateStatus("Doors open");
        }
    }

    private void OnValidate()
    {
        if (_floors != null)
        {
            for (int i = 0; i < _floors.Count; i++)
            {
                if (string.IsNullOrEmpty(_floors[i].floorName))
                    _floors[i].floorName = "Floor " + (i + 1);
            }
        }
    }
}