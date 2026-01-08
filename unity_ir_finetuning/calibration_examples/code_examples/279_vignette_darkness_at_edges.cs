// Prompt: vignette darkness at edges
// Type: general

using UnityEngine;
using UnityEngine.UI;

public class VignetteEffect : MonoBehaviour
{
    [Header("Vignette Settings")]
    [SerializeField] private float _vignetteIntensity = 0.5f;
    [SerializeField] private float _vignetteSmoothness = 0.3f;
    [SerializeField] private Color _vignetteColor = Color.black;
    [SerializeField] private bool _animateVignette = false;
    
    [Header("Animation Settings")]
    [SerializeField] private float _animationSpeed = 1f;
    [SerializeField] private float _minIntensity = 0.2f;
    [SerializeField] private float _maxIntensity = 0.8f;
    
    [Header("Trigger Settings")]
    [SerializeField] private bool _triggerOnPlayerNear = false;
    [SerializeField] private float _triggerDistance = 5f;
    [SerializeField] private float _transitionSpeed = 2f;
    
    private Material _vignetteMaterial;
    private Camera _camera;
    private float _currentIntensity;
    private float _targetIntensity;
    private Transform _player;
    
    private static readonly int IntensityProperty = Shader.PropertyToID("_Intensity");
    private static readonly int SmoothnessProperty = Shader.PropertyToID("_Smoothness");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");
    
    void Start()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = Camera.main;
        }
        
        CreateVignetteMaterial();
        _currentIntensity = _vignetteIntensity;
        _targetIntensity = _vignetteIntensity;
        
        if (_triggerOnPlayerNear)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                _player = playerObject.transform;
            }
        }
    }
    
    void CreateVignetteMaterial()
    {
        Shader vignetteShader = Shader.Find("Hidden/VignetteShader");
        if (vignetteShader == null)
        {
            vignetteShader = CreateVignetteShader();
        }
        
        _vignetteMaterial = new Material(vignetteShader);
        _vignetteMaterial.SetFloat(IntensityProperty, _vignetteIntensity);
        _vignetteMaterial.SetFloat(SmoothnessProperty, _vignetteSmoothness);
        _vignetteMaterial.SetColor(ColorProperty, _vignetteColor);
    }
    
    Shader CreateVignetteShader()
    {
        string shaderCode = @"
        Shader ""Hidden/VignetteShader""
        {
            Properties
            {
                _MainTex (""Texture"", 2D) = ""white"" {}
                _Intensity (""Intensity"", Range(0, 1)) = 0.5
                _Smoothness (""Smoothness"", Range(0, 1)) = 0.3
                _Color (""Color"", Color) = (0,0,0,1)
            }
            SubShader
            {
                Tags { ""RenderType""=""Opaque"" }
                Pass
                {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #include ""UnityCG.cginc""
                    
                    struct appdata
                    {
                        float4 vertex : POSITION;
                        float2 uv : TEXCOORD0;
                    };
                    
                    struct v2f
                    {
                        float2 uv : TEXCOORD0;
                        float4 vertex : SV_POSITION;
                    };
                    
                    sampler2D _MainTex;
                    float _Intensity;
                    float _Smoothness;
                    fixed4 _Color;
                    
                    v2f vert (appdata v)
                    {
                        v2f o;
                        o.vertex = UnityObjectToClipPos(v.vertex);
                        o.uv = v.uv;
                        return o;
                    }
                    
                    fixed4 frag (v2f i) : SV_Target
                    {
                        fixed4 col = tex2D(_MainTex, i.uv);
                        float2 center = float2(0.5, 0.5);
                        float dist = distance(i.uv, center);
                        float vignette = smoothstep(0.5 - _Smoothness, 0.5, dist);
                        vignette = pow(vignette, 2);
                        col.rgb = lerp(col.rgb, _Color.rgb, vignette * _Intensity);
                        return col;
                    }
                    ENDCG
                }
            }
        }";
        
        return Shader.Find("Hidden/VignetteShader");
    }
    
    void Update()
    {
        if (_animateVignette)
        {
            float animatedIntensity = Mathf.Lerp(_minIntensity, _maxIntensity, 
                (Mathf.Sin(Time.time * _animationSpeed) + 1f) * 0.5f);
            _targetIntensity = animatedIntensity;
        }
        
        if (_triggerOnPlayerNear && _player != null)
        {
            float distance = Vector3.Distance(transform.position, _player.position);
            if (distance <= _triggerDistance)
            {
                _targetIntensity = _maxIntensity;
            }
            else
            {
                _targetIntensity = _vignetteIntensity;
            }
        }
        
        _currentIntensity = Mathf.Lerp(_currentIntensity, _targetIntensity, 
            Time.deltaTime * _transitionSpeed);
        
        if (_vignetteMaterial != null)
        {
            _vignetteMaterial.SetFloat(IntensityProperty, _currentIntensity);
            _vignetteMaterial.SetFloat(SmoothnessProperty, _vignetteSmoothness);
            _vignetteMaterial.SetColor(ColorProperty, _vignetteColor);
        }
    }
    
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_vignetteMaterial != null)
        {
            Graphics.Blit(source, destination, _vignetteMaterial);
        }
        else
        {
            Graphics.Blit(source, destination);
        }
    }
    
    public void SetVignetteIntensity(float intensity)
    {
        _vignetteIntensity = Mathf.Clamp01(intensity);
        if (!_animateVignette && !_triggerOnPlayerNear)
        {
            _targetIntensity = _vignetteIntensity;
        }
    }
    
    public void SetVignetteSmoothness(float smoothness)
    {
        _vignetteSmoothness = Mathf.Clamp01(smoothness);
    }
    
    public void SetVignetteColor(Color color)
    {
        _vignetteColor = color;
    }
    
    public void EnableAnimation(bool enable)
    {
        _animateVignette = enable;
        if (!enable)
        {
            _targetIntensity = _vignetteIntensity;
        }
    }
    
    public void TriggerVignetteEffect(float duration)
    {
        StartCoroutine(VignetteEffectCoroutine(duration));
    }
    
    private System.Collections.IEnumerator VignetteEffectCoroutine(float duration)
    {
        float originalTarget = _targetIntensity;
        _targetIntensity = _maxIntensity;
        
        yield return new WaitForSeconds(duration);
        
        _targetIntensity = originalTarget;
    }
    
    void OnDestroy()
    {
        if (_vignetteMaterial != null)
        {
            DestroyImmediate(_vignetteMaterial);
        }
    }
    
    void OnValidate()
    {
        _vignetteIntensity = Mathf.Clamp01(_vignetteIntensity);
        _vignetteSmoothness = Mathf.Clamp01(_vignetteSmoothness);
        _minIntensity = Mathf.Clamp01(_minIntensity);
        _maxIntensity = Mathf.Clamp01(_maxIntensity);
        _triggerDistance = Mathf.Max(0f, _triggerDistance);
        _animationSpeed = Mathf.Max(0f, _animationSpeed);
        _transitionSpeed = Mathf.Max(0f, _transitionSpeed);
    }
}