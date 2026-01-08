// Prompt: ping indicator
// Type: general

using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PingIndicator : MonoBehaviour
{
    [Header("Ping Display")]
    [SerializeField] private Text _pingText;
    [SerializeField] private Image _pingIcon;
    [SerializeField] private string _pingPrefix = "Ping: ";
    [SerializeField] private string _pingSuffix = "ms";
    
    [Header("Update Settings")]
    [SerializeField] private float _updateInterval = 1f;
    [SerializeField] private int _sampleCount = 10;
    
    [Header("Visual Feedback")]
    [SerializeField] private Color _goodPingColor = Color.green;
    [SerializeField] private Color _averagePingColor = Color.yellow;
    [SerializeField] private Color _badPingColor = Color.red;
    [SerializeField] private int _goodPingThreshold = 50;
    [SerializeField] private int _badPingThreshold = 150;
    
    [Header("Animation")]
    [SerializeField] private bool _enablePulseAnimation = true;
    [SerializeField] private float _pulseSpeed = 2f;
    [SerializeField] private float _pulseIntensity = 0.3f;
    
    private float[] _pingSamples;
    private int _currentSampleIndex;
    private float _currentPing;
    private float _averagePing;
    private Coroutine _updateCoroutine;
    private Vector3 _originalScale;
    
    private void Start()
    {
        InitializePingIndicator();
        StartPingUpdates();
    }
    
    private void Update()
    {
        if (_enablePulseAnimation && _pingIcon != null)
        {
            UpdatePulseAnimation();
        }
    }
    
    private void InitializePingIndicator()
    {
        _pingSamples = new float[_sampleCount];
        _currentSampleIndex = 0;
        _currentPing = 0f;
        _averagePing = 0f;
        
        if (_pingIcon != null)
        {
            _originalScale = _pingIcon.transform.localScale;
        }
        
        if (_pingText != null)
        {
            _pingText.text = _pingPrefix + "..." + _pingSuffix;
        }
    }
    
    private void StartPingUpdates()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
        }
        
        _updateCoroutine = StartCoroutine(UpdatePingRoutine());
    }
    
    private IEnumerator UpdatePingRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(_updateInterval);
            UpdatePing();
        }
    }
    
    private void UpdatePing()
    {
        _currentPing = SimulatePing();
        
        _pingSamples[_currentSampleIndex] = _currentPing;
        _currentSampleIndex = (_currentSampleIndex + 1) % _sampleCount;
        
        CalculateAveragePing();
        UpdateDisplay();
        UpdateVisualFeedback();
    }
    
    private float SimulatePing()
    {
        float basePing = Random.Range(20f, 200f);
        float variation = Random.Range(-10f, 10f);
        return Mathf.Max(1f, basePing + variation);
    }
    
    private void CalculateAveragePing()
    {
        float sum = 0f;
        int validSamples = 0;
        
        for (int i = 0; i < _pingSamples.Length; i++)
        {
            if (_pingSamples[i] > 0f)
            {
                sum += _pingSamples[i];
                validSamples++;
            }
        }
        
        _averagePing = validSamples > 0 ? sum / validSamples : _currentPing;
    }
    
    private void UpdateDisplay()
    {
        if (_pingText != null)
        {
            int displayPing = Mathf.RoundToInt(_averagePing);
            _pingText.text = _pingPrefix + displayPing.ToString() + _pingSuffix;
        }
    }
    
    private void UpdateVisualFeedback()
    {
        Color targetColor = GetPingColor(_averagePing);
        
        if (_pingText != null)
        {
            _pingText.color = targetColor;
        }
        
        if (_pingIcon != null)
        {
            _pingIcon.color = targetColor;
        }
    }
    
    private Color GetPingColor(float ping)
    {
        if (ping <= _goodPingThreshold)
        {
            return _goodPingColor;
        }
        else if (ping <= _badPingThreshold)
        {
            float t = (ping - _goodPingThreshold) / (_badPingThreshold - _goodPingThreshold);
            return Color.Lerp(_goodPingColor, _averagePingColor, t);
        }
        else
        {
            float t = Mathf.Clamp01((ping - _badPingThreshold) / 100f);
            return Color.Lerp(_averagePingColor, _badPingColor, t);
        }
    }
    
    private void UpdatePulseAnimation()
    {
        if (_originalScale == Vector3.zero) return;
        
        float pulseValue = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseIntensity;
        _pingIcon.transform.localScale = _originalScale * pulseValue;
    }
    
    public void SetPingValue(float ping)
    {
        _currentPing = Mathf.Max(1f, ping);
        _pingSamples[_currentSampleIndex] = _currentPing;
        _currentSampleIndex = (_currentSampleIndex + 1) % _sampleCount;
        
        CalculateAveragePing();
        UpdateDisplay();
        UpdateVisualFeedback();
    }
    
    public float GetCurrentPing()
    {
        return _currentPing;
    }
    
    public float GetAveragePing()
    {
        return _averagePing;
    }
    
    public void ResetPingHistory()
    {
        for (int i = 0; i < _pingSamples.Length; i++)
        {
            _pingSamples[i] = 0f;
        }
        _currentSampleIndex = 0;
        _averagePing = 0f;
    }
    
    public void SetUpdateInterval(float interval)
    {
        _updateInterval = Mathf.Max(0.1f, interval);
        StartPingUpdates();
    }
    
    private void OnDestroy()
    {
        if (_updateCoroutine != null)
        {
            StopCoroutine(_updateCoroutine);
        }
    }
}