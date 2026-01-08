// Prompt: mirror that reflects
// Type: general

using UnityEngine;
using UnityEngine.Rendering;

public class Mirror : MonoBehaviour
{
    [Header("Mirror Settings")]
    [SerializeField] private Camera _mirrorCamera;
    [SerializeField] private RenderTexture _mirrorTexture;
    [SerializeField] private Material _mirrorMaterial;
    [SerializeField] private int _textureSize = 512;
    [SerializeField] private LayerMask _reflectionLayers = -1;
    [SerializeField] private bool _disablePixelLights = true;
    [SerializeField] private float _clipPlaneOffset = 0.07f;
    
    [Header("Performance")]
    [SerializeField] private bool _useOcclusionCulling = true;
    [SerializeField] private float _maxRenderDistance = 100f;
    [SerializeField] private int _maxReflectionFPS = 30;
    
    private Camera _mainCamera;
    private float _lastRenderTime;
    private Vector4 _reflectionPlane;
    private Matrix4x4 _reflectionMatrix;
    private bool _isRendering;

    void Start()
    {
        SetupMirror();
    }

    void Update()
    {
        if (_mainCamera == null)
            _mainCamera = Camera.main;
            
        if (_mainCamera != null && ShouldRender())
        {
            RenderMirror();
        }
    }

    void SetupMirror()
    {
        if (_mirrorTexture == null)
        {
            _mirrorTexture = new RenderTexture(_textureSize, _textureSize, 16, RenderTextureFormat.ARGB32);
            _mirrorTexture.name = "MirrorTexture_" + GetInstanceID();
            _mirrorTexture.isPowerOfTwo = true;
            _mirrorTexture.hideFlags = HideFlags.DontSave;
        }

        if (_mirrorCamera == null)
        {
            GameObject cameraObject = new GameObject("MirrorCamera_" + GetInstanceID());
            cameraObject.transform.SetParent(transform);
            _mirrorCamera = cameraObject.AddComponent<Camera>();
            _mirrorCamera.enabled = false;
            _mirrorCamera.targetTexture = _mirrorTexture;
        }

        if (_mirrorMaterial == null)
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                _mirrorMaterial = renderer.material;
            }
        }

        if (_mirrorMaterial != null)
        {
            _mirrorMaterial.mainTexture = _mirrorTexture;
        }

        CalculateReflectionPlane();
    }

    bool ShouldRender()
    {
        if (_isRendering || _mainCamera == null || _mirrorCamera == null)
            return false;

        float distance = Vector3.Distance(_mainCamera.transform.position, transform.position);
        if (distance > _maxRenderDistance)
            return false;

        float timeSinceLastRender = Time.time - _lastRenderTime;
        float minInterval = 1f / _maxReflectionFPS;
        
        return timeSinceLastRender >= minInterval;
    }

    void RenderMirror()
    {
        _isRendering = true;
        _lastRenderTime = Time.time;

        Vector3 cameraPosition = _mainCamera.transform.position;
        Vector3 cameraDirection = _mainCamera.transform.forward;

        Vector3 reflectedPosition = ReflectPosition(cameraPosition);
        Vector3 reflectedDirection = ReflectDirection(cameraDirection);

        _mirrorCamera.transform.position = reflectedPosition;
        _mirrorCamera.transform.LookAt(reflectedPosition + reflectedDirection, Vector3.up);

        _mirrorCamera.fieldOfView = _mainCamera.fieldOfView;
        _mirrorCamera.aspect = _mainCamera.aspect;
        _mirrorCamera.nearClipPlane = _mainCamera.nearClipPlane;
        _mirrorCamera.farClipPlane = _mainCamera.farClipPlane;
        _mirrorCamera.cullingMask = _reflectionLayers;
        _mirrorCamera.useOcclusionCulling = _useOcclusionCulling;

        if (_disablePixelLights)
            _mirrorCamera.renderingPath = RenderingPath.VertexLit;

        SetupClipPlane();

        GL.invertCulling = true;
        _mirrorCamera.Render();
        GL.invertCulling = false;

        _isRendering = false;
    }

    void CalculateReflectionPlane()
    {
        Vector3 normal = transform.up;
        Vector3 position = transform.position;
        float distance = -Vector3.Dot(normal, position);
        _reflectionPlane = new Vector4(normal.x, normal.y, normal.z, distance);
    }

    Vector3 ReflectPosition(Vector3 position)
    {
        Vector3 normal = new Vector3(_reflectionPlane.x, _reflectionPlane.y, _reflectionPlane.z);
        float distance = Vector3.Dot(normal, position) + _reflectionPlane.w;
        return position - 2f * distance * normal;
    }

    Vector3 ReflectDirection(Vector3 direction)
    {
        Vector3 normal = new Vector3(_reflectionPlane.x, _reflectionPlane.y, _reflectionPlane.z);
        return Vector3.Reflect(direction, normal);
    }

    void SetupClipPlane()
    {
        Vector3 normal = transform.up;
        Vector3 position = transform.position + normal * _clipPlaneOffset;
        
        Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(position, normal));
        
        Matrix4x4 projection = _mirrorCamera.CalculateObliqueMatrix(clipPlane);
        _mirrorCamera.projectionMatrix = projection;
    }

    void OnValidate()
    {
        if (_textureSize < 64) _textureSize = 64;
        if (_textureSize > 2048) _textureSize = 2048;
        if (_maxReflectionFPS < 1) _maxReflectionFPS = 1;
        if (_maxReflectionFPS > 60) _maxReflectionFPS = 60;
    }

    void OnDestroy()
    {
        if (_mirrorTexture != null)
        {
            DestroyImmediate(_mirrorTexture);
        }
        
        if (_mirrorCamera != null)
        {
            DestroyImmediate(_mirrorCamera.gameObject);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);
        
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(Vector3.zero, Vector3.up * 2f);
    }
}