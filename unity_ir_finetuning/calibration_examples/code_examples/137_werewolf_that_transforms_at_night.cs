// Prompt: werewolf that transforms at night
// Type: general

using UnityEngine;
using UnityEngine.Events;

public class Werewolf : MonoBehaviour
{
    [System.Serializable]
    public class WerewolfEvents
    {
        public UnityEvent OnTransformToWerewolf;
        public UnityEvent OnTransformToHuman;
    }

    [Header("Transformation Settings")]
    [SerializeField] private float _nightStartHour = 20f;
    [SerializeField] private float _nightEndHour = 6f;
    [SerializeField] private float _transformationDuration = 2f;
    [SerializeField] private bool _useRealTime = false;
    [SerializeField] private float _gameTimeSpeed = 1f;

    [Header("Werewolf Form")]
    [SerializeField] private GameObject _humanModel;
    [SerializeField] private GameObject _werewolfModel;
    [SerializeField] private float _werewolfSpeed = 8f;
    [SerializeField] private float _werewolfJumpForce = 15f;
    [SerializeField] private float _werewolfDamage = 50f;

    [Header("Human Form")]
    [SerializeField] private float _humanSpeed = 4f;
    [SerializeField] private float _humanJumpForce = 8f;
    [SerializeField] private float _humanDamage = 10f;

    [Header("Audio")]
    [SerializeField] private AudioClip _transformationSound;
    [SerializeField] private AudioClip _werewolfHowl;
    [SerializeField] private AudioClip _werewolfGrowl;

    [Header("Effects")]
    [SerializeField] private ParticleSystem _transformationEffect;
    [SerializeField] private Light _moonlight;

    [Header("Events")]
    [SerializeField] private WerewolfEvents _events;

    private bool _isWerewolf = false;
    private bool _isTransforming = false;
    private float _currentGameTime = 12f; // Start at noon
    private AudioSource _audioSource;
    private Rigidbody _rigidbody;
    private Collider _collider;
    private Animator _humanAnimator;
    private Animator _werewolfAnimator;
    private float _transformTimer = 0f;

    // Movement variables
    private float _currentSpeed;
    private float _currentJumpForce;
    private float _currentDamage;
    private bool _isGrounded = true;

    private void Start()
    {
        InitializeComponents();
        SetupInitialState();
    }

    private void InitializeComponents()
    {
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
            _rigidbody = gameObject.AddComponent<Rigidbody>();

        _collider = GetComponent<Collider>();

        if (_humanModel != null)
            _humanAnimator = _humanModel.GetComponent<Animator>();
        
        if (_werewolfModel != null)
            _werewolfAnimator = _werewolfModel.GetComponent<Animator>();
    }

    private void SetupInitialState()
    {
        SetHumanForm();
        
        if (_moonlight != null)
            _moonlight.enabled = false;
    }

    private void Update()
    {
        UpdateGameTime();
        CheckTransformationConditions();
        HandleTransformation();
        HandleMovement();
        UpdateAnimations();
    }

    private void UpdateGameTime()
    {
        if (_useRealTime)
        {
            System.DateTime currentTime = System.DateTime.Now;
            _currentGameTime = currentTime.Hour + (currentTime.Minute / 60f);
        }
        else
        {
            _currentGameTime += Time.deltaTime * _gameTimeSpeed;
            if (_currentGameTime >= 24f)
                _currentGameTime -= 24f;
        }
    }

    private void CheckTransformationConditions()
    {
        if (_isTransforming) return;

        bool shouldBeWerewolf = IsNightTime();

        if (shouldBeWerewolf && !_isWerewolf)
        {
            StartTransformation(true);
        }
        else if (!shouldBeWerewolf && _isWerewolf)
        {
            StartTransformation(false);
        }
    }

    private bool IsNightTime()
    {
        if (_nightStartHour > _nightEndHour) // Night crosses midnight
        {
            return _currentGameTime >= _nightStartHour || _currentGameTime <= _nightEndHour;
        }
        else
        {
            return _currentGameTime >= _nightStartHour && _currentGameTime <= _nightEndHour;
        }
    }

    private void StartTransformation(bool toWerewolf)
    {
        _isTransforming = true;
        _transformTimer = 0f;

        if (_transformationEffect != null)
            _transformationEffect.Play();

        if (_transformationSound != null && _audioSource != null)
            _audioSource.PlayOneShot(_transformationSound);

        if (toWerewolf)
        {
            _events?.OnTransformToWerewolf?.Invoke();
            if (_werewolfHowl != null && _audioSource != null)
                _audioSource.PlayOneShot(_werewolfHowl);
        }
        else
        {
            _events?.OnTransformToHuman?.Invoke();
        }
    }

    private void HandleTransformation()
    {
        if (!_isTransforming) return;

        _transformTimer += Time.deltaTime;

        if (_transformTimer >= _transformationDuration)
        {
            CompleteTransformation();
        }
    }

    private void CompleteTransformation()
    {
        _isTransforming = false;
        
        if (IsNightTime())
        {
            SetWerewolfForm();
        }
        else
        {
            SetHumanForm();
        }
    }

    private void SetWerewolfForm()
    {
        _isWerewolf = true;
        
        if (_humanModel != null)
            _humanModel.SetActive(false);
        
        if (_werewolfModel != null)
            _werewolfModel.SetActive(true);

        _currentSpeed = _werewolfSpeed;
        _currentJumpForce = _werewolfJumpForce;
        _currentDamage = _werewolfDamage;

        if (_moonlight != null)
            _moonlight.enabled = true;
    }

    private void SetHumanForm()
    {
        _isWerewolf = false;
        
        if (_werewolfModel != null)
            _werewolfModel.SetActive(false);
        
        if (_humanModel != null)
            _humanModel.SetActive(true);

        _currentSpeed = _humanSpeed;
        _currentJumpForce = _humanJumpForce;
        _currentDamage = _humanDamage;

        if (_moonlight != null)
            _moonlight.enabled = false;
    }

    private void HandleMovement()
    {
        if (_isTransforming || _rigidbody == null) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 movement = new Vector3(horizontal, 0f, vertical).normalized;
        
        if (movement.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.LookRotation(movement);
            _rigidbody.MovePosition(transform.position + movement * _currentSpeed * Time.deltaTime);
        }

        if (Input.GetButtonDown("Jump") && _isGrounded)
        {
            _rigidbody.AddForce(Vector3.up * _currentJumpForce, ForceMode.Impulse);
            _isGrounded = false;
        }
    }

    private void UpdateAnimations()
    {
        float speed = _rigidbody != null ? _rigidbody.velocity.magnitude : 0f;
        
        if (_isWerewolf && _werewolfAnimator != null)
        {
            _werewolfAnimator.SetFloat("Speed", speed);
            _werewolfAnimator.SetBool("IsGrounded", _isGrounded);
        }
        else if (!_isWerewolf && _humanAnimator != null)
        {
            _humanAnimator.SetFloat("Speed", speed);
            _humanAnimator.SetBool("IsGrounded", _isGrounded);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Ground"))
        {
            _isGrounded = true;
        }

        if (_isWerewolf && collision.gameObject.CompareTag("Player"))
        {
            AttackTarget(collision.gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isWerewolf && other.CompareTag("Player"))
        {
            if (_werewolfGrowl != null && _audioSource != null)
                _audioSource.PlayOneShot(_werewolfGrowl);
        }
    }

    private void AttackTarget(GameObject target)
    {
        // Apply damage logic here
        Debug.Log($"Werewolf attacks {target.name} for {_currentDamage} damage!");
        
        // Push the target away
        Rigidbody targetRb = target.GetComponent<Rigidbody>();
        if (targetRb != null)
        {
            Vector3 pushDirection = (target.transform.position - transform.position).normalized;
            targetRb.AddForce(pushDirection * 10f, ForceMode.Impulse);
        }
    }

    public bool IsWerewolfForm()
    {
        return _isWerewolf;
    }

    public float GetCurrentGameTime()
    {
        return _currentGameTime;
    }

    public void ForceTransformation(bool toWerewolf)
    {
        if (_isTransforming) return;
        
        StartTransformation(toWerewolf);
    }

    public void SetGameTime(float hour)
    {
        _currentGameTime = Mathf.Clamp(hour, 0f, 24f);
    }
}