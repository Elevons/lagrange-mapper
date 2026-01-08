// Prompt: team color indicator
// Type: general

using UnityEngine;
using UnityEngine.UI;

public class TeamColorIndicator : MonoBehaviour
{
    [System.Serializable]
    public class TeamData
    {
        public string teamName;
        public Color teamColor;
        public Sprite teamIcon;
    }

    [Header("Team Configuration")]
    [SerializeField] private TeamData[] _teams = new TeamData[]
    {
        new TeamData { teamName = "Red Team", teamColor = Color.red },
        new TeamData { teamName = "Blue Team", teamColor = Color.blue },
        new TeamData { teamName = "Green Team", teamColor = Color.green },
        new TeamData { teamName = "Yellow Team", teamColor = Color.yellow }
    };
    
    [SerializeField] private int _currentTeamIndex = 0;

    [Header("Visual Components")]
    [SerializeField] private Renderer _meshRenderer;
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Image _uiImage;
    [SerializeField] private Text _teamNameText;
    [SerializeField] private Light _indicatorLight;
    [SerializeField] private ParticleSystem _particleEffect;

    [Header("Material Properties")]
    [SerializeField] private string _colorPropertyName = "_Color";
    [SerializeField] private string _emissionPropertyName = "_EmissionColor";
    [SerializeField] private bool _useEmission = false;
    [SerializeField] private float _emissionIntensity = 1.0f;

    [Header("Animation")]
    [SerializeField] private bool _enablePulse = false;
    [SerializeField] private float _pulseSpeed = 2.0f;
    [SerializeField] private float _pulseMinIntensity = 0.5f;
    [SerializeField] private float _pulseMaxIntensity = 1.0f;

    [Header("Events")]
    public UnityEngine.Events.UnityEvent<int> OnTeamChanged;
    public UnityEngine.Events.UnityEvent<string> OnTeamNameChanged;
    public UnityEngine.Events.UnityEvent<Color> OnTeamColorChanged;

    private Material _originalMaterial;
    private Material _instanceMaterial;
    private Color _originalColor;
    private float _pulseTimer;

    public int CurrentTeamIndex 
    { 
        get => _currentTeamIndex; 
        set => SetTeam(value); 
    }

    public TeamData CurrentTeam 
    { 
        get => _teams[_currentTeamIndex]; 
    }

    public Color CurrentTeamColor 
    { 
        get => _teams[_currentTeamIndex].teamColor; 
    }

    public string CurrentTeamName 
    { 
        get => _teams[_currentTeamIndex].teamName; 
    }

    private void Start()
    {
        InitializeMaterials();
        ApplyTeamColor();
    }

    private void Update()
    {
        if (_enablePulse)
        {
            UpdatePulseEffect();
        }
    }

    private void InitializeMaterials()
    {
        if (_meshRenderer != null && _meshRenderer.material != null)
        {
            _originalMaterial = _meshRenderer.material;
            _instanceMaterial = new Material(_originalMaterial);
            _meshRenderer.material = _instanceMaterial;
            _originalColor = _instanceMaterial.color;
        }
    }

    public void SetTeam(int teamIndex)
    {
        if (teamIndex < 0 || teamIndex >= _teams.Length)
        {
            Debug.LogWarning($"Invalid team index: {teamIndex}. Must be between 0 and {_teams.Length - 1}");
            return;
        }

        _currentTeamIndex = teamIndex;
        ApplyTeamColor();
        
        OnTeamChanged?.Invoke(_currentTeamIndex);
        OnTeamNameChanged?.Invoke(CurrentTeamName);
        OnTeamColorChanged?.Invoke(CurrentTeamColor);
    }

    public void SetTeamByName(string teamName)
    {
        for (int i = 0; i < _teams.Length; i++)
        {
            if (_teams[i].teamName.Equals(teamName, System.StringComparison.OrdinalIgnoreCase))
            {
                SetTeam(i);
                return;
            }
        }
        Debug.LogWarning($"Team with name '{teamName}' not found");
    }

    public void NextTeam()
    {
        SetTeam((_currentTeamIndex + 1) % _teams.Length);
    }

    public void PreviousTeam()
    {
        SetTeam((_currentTeamIndex - 1 + _teams.Length) % _teams.Length);
    }

    private void ApplyTeamColor()
    {
        if (_currentTeamIndex >= _teams.Length) return;

        Color teamColor = _teams[_currentTeamIndex].teamColor;
        Sprite teamIcon = _teams[_currentTeamIndex].teamIcon;

        // Apply to mesh renderer
        if (_instanceMaterial != null)
        {
            _instanceMaterial.SetColor(_colorPropertyName, teamColor);
            
            if (_useEmission && _instanceMaterial.HasProperty(_emissionPropertyName))
            {
                _instanceMaterial.SetColor(_emissionPropertyName, teamColor * _emissionIntensity);
                _instanceMaterial.EnableKeyword("_EMISSION");
            }
        }

        // Apply to sprite renderer
        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = teamColor;
            if (teamIcon != null)
            {
                _spriteRenderer.sprite = teamIcon;
            }
        }

        // Apply to UI image
        if (_uiImage != null)
        {
            _uiImage.color = teamColor;
            if (teamIcon != null)
            {
                _uiImage.sprite = teamIcon;
            }
        }

        // Apply to text
        if (_teamNameText != null)
        {
            _teamNameText.text = _teams[_currentTeamIndex].teamName;
            _teamNameText.color = teamColor;
        }

        // Apply to light
        if (_indicatorLight != null)
        {
            _indicatorLight.color = teamColor;
        }

        // Apply to particle system
        if (_particleEffect != null)
        {
            var main = _particleEffect.main;
            main.startColor = teamColor;
        }
    }

    private void UpdatePulseEffect()
    {
        _pulseTimer += Time.deltaTime * _pulseSpeed;
        float pulseValue = Mathf.Lerp(_pulseMinIntensity, _pulseMaxIntensity, 
            (Mathf.Sin(_pulseTimer) + 1.0f) * 0.5f);

        Color baseColor = _teams[_currentTeamIndex].teamColor;
        Color pulsedColor = baseColor * pulseValue;

        if (_instanceMaterial != null)
        {
            _instanceMaterial.SetColor(_colorPropertyName, pulsedColor);
        }

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = pulsedColor;
        }

        if (_uiImage != null)
        {
            _uiImage.color = pulsedColor;
        }

        if (_indicatorLight != null)
        {
            _indicatorLight.intensity = pulseValue;
        }
    }

    public void TogglePulse()
    {
        _enablePulse = !_enablePulse;
        if (!_enablePulse)
        {
            ApplyTeamColor();
        }
    }

    public void SetPulseEnabled(bool enabled)
    {
        _enablePulse = enabled;
        if (!_enablePulse)
        {
            ApplyTeamColor();
        }
    }

    private void OnDestroy()
    {
        if (_instanceMaterial != null)
        {
            DestroyImmediate(_instanceMaterial);
        }
    }

    private void OnValidate()
    {
        if (_teams == null || _teams.Length == 0)
        {
            _teams = new TeamData[]
            {
                new TeamData { teamName = "Red Team", teamColor = Color.red },
                new TeamData { teamName = "Blue Team", teamColor = Color.blue }
            };
        }

        _currentTeamIndex = Mathf.Clamp(_currentTeamIndex, 0, _teams.Length - 1);
    }
}