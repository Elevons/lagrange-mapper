// Prompt: recursive reality object that contains miniature version of entire scene inside it as texture on material, when zooming in it reveals the miniature scene also contains same object creating infinite recursion, each level plays different musical note
// Type: combat

using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class RecursiveRealityObject : MonoBehaviour
{
    [System.Serializable]
    public class RecursionLevel
    {
        public Camera levelCamera;
        public RenderTexture renderTexture;
        public Transform miniatureScene;
        public AudioSource audioSource;
        public float pitch = 1f;
        public bool isActive = false;
    }

    [Header("Recursion Settings")]
    [SerializeField] private int _maxRecursionDepth = 5;
    [SerializeField] private float _scaleReduction = 0.1f;
    [SerializeField] private Vector3 _miniatureOffset = Vector3.zero;
    [SerializeField] private LayerMask _renderLayers = -1;

    [Header("Render Settings")]
    [SerializeField] private int _textureResolution = 512;
    [SerializeField] private Material _displayMaterial;
    [SerializeField] private Renderer _targetRenderer;

    [Header("Audio Settings")]
    [SerializeField] private AudioClip _baseNote;
    [SerializeField] private float _basePitch = 1f;
    [SerializeField] private float _pitchMultiplier = 1.5f;
    [SerializeField] private float _volumeReduction = 0.7f;
    [SerializeField] private float _maxAudioDistance = 50f;

    [Header("Zoom Detection")]
    [SerializeField] private float _zoomThreshold = 5f;
    [SerializeField] private Transform _viewerTransform;
    [SerializeField] private bool _autoDetectMainCamera = true;

    [Header("Performance")]
    [SerializeField] private float _updateInterval = 0.1f;
    [SerializeField] private bool _enableLOD = true;
    [SerializeField] private float _lodDistance = 100f;

    private List<RecursionLevel> _recursionLevels = new List<RecursionLevel>();
    private Transform _originalScene;
    private float _lastUpdateTime;
    private int _currentActiveDepth = 0;
    private bool _isInitialized = false;

    private void Start()
    {
        InitializeRecursion();
    }

    private void Update()
    {
        if (!_isInitialized) return;

        if (Time.time - _lastUpdateTime >= _updateInterval)
        {
            UpdateRecursionLevels();
            _lastUpdateTime = Time.time;
        }
    }

    private void InitializeRecursion()
    {
        if (_autoDetectMainCamera && _viewerTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
                _viewerTransform = mainCam.transform;
        }

        if (_targetRenderer == null)
            _targetRenderer = GetComponent<Renderer>();

        if (_displayMaterial == null && _targetRenderer != null)
            _displayMaterial = _targetRenderer.material;

        CreateOriginalSceneReference();
        SetupRecursionLevels();
        _isInitialized = true;
    }

    private void CreateOriginalSceneReference()
    {
        GameObject sceneRoot = new GameObject("OriginalSceneReference");
        _originalScene = sceneRoot.transform;

        Transform[] allObjects = FindObjectsOfType<Transform>();
        foreach (Transform obj in allObjects)
        {
            if (obj.parent == null && obj != transform && obj != sceneRoot.transform)
            {
                GameObject copy = Instantiate(obj.gameObject, _originalScene);
                copy.name = obj.name + "_Reference";
            }
        }
    }

    private void SetupRecursionLevels()
    {
        for (int i = 0; i < _maxRecursionDepth; i++)
        {
            RecursionLevel level = CreateRecursionLevel(i);
            _recursionLevels.Add(level);
        }
    }

    private RecursionLevel CreateRecursionLevel(int depth)
    {
        RecursionLevel level = new RecursionLevel();

        // Create render texture
        level.renderTexture = new RenderTexture(_textureResolution, _textureResolution, 16);
        level.renderTexture.name = $"RecursionTexture_Level_{depth}";

        // Create camera for this level
        GameObject cameraObj = new GameObject($"RecursionCamera_Level_{depth}");
        cameraObj.transform.SetParent(transform);
        level.levelCamera = cameraObj.AddComponent<Camera>();
        
        ConfigureCamera(level.levelCamera, depth);
        level.levelCamera.targetTexture = level.renderTexture;

        // Create miniature scene
        level.miniatureScene = CreateMiniatureScene(depth);

        // Setup audio
        level.audioSource = gameObject.AddComponent<AudioSource>();
        ConfigureAudio(level.audioSource, depth);
        level.pitch = _basePitch * Mathf.Pow(_pitchMultiplier, depth);

        return level;
    }

    private void ConfigureCamera(Camera cam, int depth)
    {
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            cam.fieldOfView = mainCam.fieldOfView;
            cam.nearClipPlane = mainCam.nearClipPlane;
            cam.farClipPlane = mainCam.farClipPlane;
        }

        cam.cullingMask = _renderLayers;
        cam.enabled = false;
    }

    private Transform CreateMiniatureScene(int depth)
    {
        if (_originalScene == null) return null;

        GameObject miniature = new GameObject($"MiniatureScene_Level_{depth}");
        miniature.transform.SetParent(transform);

        float scale = Mathf.Pow(_scaleReduction, depth + 1);
        miniature.transform.localScale = Vector3.one * scale;
        miniature.transform.localPosition = _miniatureOffset * (depth + 1);

        // Copy scene objects
        foreach (Transform child in _originalScene)
        {
            GameObject copy = Instantiate(child.gameObject, miniature.transform);
            
            // Ensure recursive objects in miniature also have this script
            RecursiveRealityObject recursiveScript = copy.GetComponent<RecursiveRealityObject>();
            if (recursiveScript != null && recursiveScript != this)
            {
                recursiveScript._maxRecursionDepth = Mathf.Max(0, _maxRecursionDepth - depth - 1);
            }
        }

        return miniature.transform;
    }

    private void ConfigureAudio(AudioSource audioSource, int depth)
    {
        audioSource.clip = _baseNote;
        audioSource.volume = Mathf.Pow(_volumeReduction, depth);
        audioSource.pitch = _basePitch * Mathf.Pow(_pitchMultiplier, depth);
        audioSource.spatialBlend = 1f;
        audioSource.maxDistance = _maxAudioDistance;
        audioSource.rolloffMode = AudioRolloffMode.Linear;
        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void UpdateRecursionLevels()
    {
        if (_viewerTransform == null) return;

        float distanceToViewer = Vector3.Distance(_viewerTransform.position, transform.position);
        int targetDepth = CalculateTargetDepth(distanceToViewer);

        UpdateActiveDepth(targetDepth);
        UpdateCameraPositions();
        UpdateRenderTextures();
        UpdateAudio();
    }

    private int CalculateTargetDepth(float distance)
    {
        if (_enableLOD && distance > _lodDistance)
            return 0;

        float zoomFactor = _zoomThreshold / Mathf.Max(distance, 0.1f);
        int depth = Mathf.FloorToInt(Mathf.Log(zoomFactor, 2f));
        return Mathf.Clamp(depth, 0, _maxRecursionDepth - 1);
    }

    private void UpdateActiveDepth(int targetDepth)
    {
        if (targetDepth == _currentActiveDepth) return;

        // Deactivate old levels
        for (int i = _currentActiveDepth; i < _recursionLevels.Count; i++)
        {
            if (i < _recursionLevels.Count)
            {
                _recursionLevels[i].isActive = false;
                _recursionLevels[i].levelCamera.enabled = false;
                if (_recursionLevels[i].audioSource.isPlaying)
                    _recursionLevels[i].audioSource.Stop();
            }
        }

        // Activate new levels
        for (int i = 0; i <= targetDepth && i < _recursionLevels.Count; i++)
        {
            _recursionLevels[i].isActive = true;
            _recursionLevels[i].levelCamera.enabled = true;
        }

        _currentActiveDepth = targetDepth;
    }

    private void UpdateCameraPositions()
    {
        if (_viewerTransform == null) return;

        for (int i = 0; i < _recursionLevels.Count; i++)
        {
            if (!_recursionLevels[i].isActive) continue;

            RecursionLevel level = _recursionLevels[i];
            
            // Position camera to look at miniature scene
            Vector3 cameraOffset = _viewerTransform.position - transform.position;
            float scale = Mathf.Pow(_scaleReduction, i + 1);
            
            level.levelCamera.transform.position = level.miniatureScene.position + cameraOffset * scale;
            level.levelCamera.transform.LookAt(level.miniatureScene.position);
        }
    }

    private void UpdateRenderTextures()
    {
        for (int i = 0; i < _recursionLevels.Count; i++)
        {
            if (!_recursionLevels[i].isActive) continue;

            RecursionLevel level = _recursionLevels[i];
            level.levelCamera.Render();

            // Apply texture to material
            if (_displayMaterial != null && i == 0)
            {
                _displayMaterial.mainTexture = level.renderTexture;
            }
        }
    }

    private void UpdateAudio()
    {
        for (int i = 0; i < _recursionLevels.Count; i++)
        {
            RecursionLevel level = _recursionLevels[i];
            
            if (level.isActive && !level.audioSource.isPlaying)
            {
                level.audioSource.Play();
            }
            else if (!level.isActive && level.audioSource.isPlaying)
            {
                level.audioSource.Stop();
            }
        }
    }

    private void OnDestroy()
    {
        foreach (RecursionLevel level in _recursionLevels)
        {
            if (level.renderTexture != null)
            {
                level.renderTexture.Release();
                DestroyImmediate(level.renderTexture);
            }
        }
    }

    private void OnValidate()
    {
        _maxRecursionDepth = Mathf.Clamp(_maxRecursionDepth, 1, 10);
        _scaleReduction = Mathf.Clamp01(_scaleReduction);
        _textureResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_textureResolution, 64, 2048));
        _updateInterval = Mathf.Max(0.01f, _updateInterval);
    }

    public void SetRecursionDepth(int depth)
    {
        _maxRecursionDepth = Mathf.Clamp(depth, 1, 10);
        if (_isInitialized)
        {
            ClearRecursionLevels();
            SetupRecursionLevels();
        }
    }

    private void ClearRecursionLevels()
    {
        foreach (RecursionLevel level in _recursionLevels)
        {
            if (level.levelCamera != null)
                DestroyImmediate(level.levelCamera.gameObject);
            if (level.renderTexture != null)
            {
                level.renderTexture.Release();
                DestroyImmediate(level.renderTexture);
            }
            if (level.miniatureScene != null)
                DestroyImmediate(level.miniatureScene.gameObject);
        }
        _recursionLevels.Clear();
    }
}