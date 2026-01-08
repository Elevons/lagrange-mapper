// Prompt: aurora borealis sky effect
// Type: general

using UnityEngine;
using System.Collections.Generic;

public class AuroraBorealis : MonoBehaviour
{
    [Header("Aurora Configuration")]
    [SerializeField] private Material auroraMaterial;
    [SerializeField] private int auroraLayers = 3;
    [SerializeField] private float auroraHeight = 50f;
    [SerializeField] private float auroraWidth = 100f;
    [SerializeField] private float auroraDepth = 20f;
    
    [Header("Animation Settings")]
    [SerializeField] private float waveSpeed = 0.5f;
    [SerializeField] private float waveAmplitude = 2f;
    [SerializeField] private float colorShiftSpeed = 0.3f;
    [SerializeField] private float opacityPulseSpeed = 1f;
    [SerializeField] private float verticalDrift = 0.2f;
    
    [Header("Visual Properties")]
    [SerializeField] private Gradient auroraColors;
    [SerializeField] private AnimationCurve intensityCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private float baseIntensity = 0.7f;
    [SerializeField] private float maxIntensity = 1.2f;
    [SerializeField] private bool useNoise = true;
    [SerializeField] private float noiseScale = 0.1f;
    
    private List<AuroraLayer> _auroraLayers;
    private float _timeOffset;
    
    [System.Serializable]
    private class AuroraLayer
    {
        public GameObject gameObject;
        public MeshRenderer renderer;
        public Material material;
        public float speedMultiplier;
        public float heightOffset;
        public float colorOffset;
        public Vector3[] originalVertices;
        public Mesh mesh;
    }
    
    private void Start()
    {
        _timeOffset = Random.Range(0f, 100f);
        InitializeAuroraColors();
        CreateAuroraLayers();
    }
    
    private void InitializeAuroraColors()
    {
        if (auroraColors.colorKeys.Length == 0)
        {
            GradientColorKey[] colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey(new Color(0.2f, 1f, 0.3f), 0f);
            colorKeys[1] = new GradientColorKey(new Color(0.3f, 0.8f, 1f), 0.33f);
            colorKeys[2] = new GradientColorKey(new Color(1f, 0.4f, 0.8f), 0.66f);
            colorKeys[3] = new GradientColorKey(new Color(0.8f, 1f, 0.2f), 1f);
            
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(0.3f, 0f);
            alphaKeys[1] = new GradientAlphaKey(0.8f, 1f);
            
            auroraColors.SetKeys(colorKeys, alphaKeys);
        }
    }
    
    private void CreateAuroraLayers()
    {
        _auroraLayers = new List<AuroraLayer>();
        
        for (int i = 0; i < auroraLayers; i++)
        {
            GameObject layerObj = new GameObject($"AuroraLayer_{i}");
            layerObj.transform.SetParent(transform);
            
            AuroraLayer layer = new AuroraLayer();
            layer.gameObject = layerObj;
            layer.speedMultiplier = Random.Range(0.5f, 1.5f);
            layer.heightOffset = i * (auroraHeight / auroraLayers) + Random.Range(-5f, 5f);
            layer.colorOffset = (float)i / auroraLayers;
            
            CreateAuroraMesh(layer, i);
            _auroraLayers.Add(layer);
        }
    }
    
    private void CreateAuroraMesh(AuroraLayer layer, int layerIndex)
    {
        MeshFilter meshFilter = layer.gameObject.AddComponent<MeshFilter>();
        layer.renderer = layer.gameObject.AddComponent<MeshRenderer>();
        
        if (auroraMaterial != null)
        {
            layer.material = new Material(auroraMaterial);
        }
        else
        {
            layer.material = CreateDefaultAuroraMaterial();
        }
        
        layer.renderer.material = layer.material;
        layer.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        layer.renderer.receiveShadows = false;
        
        Mesh mesh = new Mesh();
        mesh.name = $"AuroraMesh_{layerIndex}";
        
        int segments = 50;
        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];
        
        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float x = (t - 0.5f) * auroraWidth;
            float z = Random.Range(-auroraDepth * 0.5f, auroraDepth * 0.5f);
            
            vertices[i * 2] = new Vector3(x, layer.heightOffset, z);
            vertices[i * 2 + 1] = new Vector3(x, layer.heightOffset + auroraHeight * 0.3f, z);
            
            uvs[i * 2] = new Vector2(t, 0f);
            uvs[i * 2 + 1] = new Vector2(t, 1f);
        }
        
        for (int i = 0; i < segments; i++)
        {
            int baseIndex = i * 6;
            int vertIndex = i * 2;
            
            triangles[baseIndex] = vertIndex;
            triangles[baseIndex + 1] = vertIndex + 2;
            triangles[baseIndex + 2] = vertIndex + 1;
            
            triangles[baseIndex + 3] = vertIndex + 1;
            triangles[baseIndex + 4] = vertIndex + 2;
            triangles[baseIndex + 5] = vertIndex + 3;
        }
        
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        
        layer.originalVertices = (Vector3[])vertices.Clone();
        layer.mesh = mesh;
        meshFilter.mesh = mesh;
    }
    
    private Material CreateDefaultAuroraMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
        return mat;
    }
    
    private void Update()
    {
        float time = Time.time + _timeOffset;
        
        for (int i = 0; i < _auroraLayers.Count; i++)
        {
            UpdateAuroraLayer(_auroraLayers[i], time, i);
        }
    }
    
    private void UpdateAuroraLayer(AuroraLayer layer, float time, int layerIndex)
    {
        if (layer.mesh == null || layer.originalVertices == null) return;
        
        Vector3[] vertices = new Vector3[layer.originalVertices.Length];
        float layerTime = time * layer.speedMultiplier;
        
        for (int i = 0; i < layer.originalVertices.Length; i++)
        {
            Vector3 originalPos = layer.originalVertices[i];
            Vector3 newPos = originalPos;
            
            float waveX = Mathf.Sin(layerTime * waveSpeed + originalPos.x * 0.1f) * waveAmplitude;
            float waveY = Mathf.Sin(layerTime * waveSpeed * 0.7f + originalPos.x * 0.05f) * waveAmplitude * 0.5f;
            
            if (useNoise)
            {
                float noiseX = Mathf.PerlinNoise(originalPos.x * noiseScale, layerTime * 0.1f) - 0.5f;
                float noiseY = Mathf.PerlinNoise(originalPos.x * noiseScale + 100f, layerTime * 0.1f) - 0.5f;
                waveX += noiseX * waveAmplitude * 0.5f;
                waveY += noiseY * waveAmplitude * 0.3f;
            }
            
            newPos.x += waveX;
            newPos.y += waveY + Mathf.Sin(layerTime * verticalDrift) * 2f;
            
            vertices[i] = newPos;
        }
        
        layer.mesh.vertices = vertices;
        layer.mesh.RecalculateNormals();
        
        UpdateLayerMaterial(layer, time, layerIndex);
    }
    
    private void UpdateLayerMaterial(AuroraLayer layer, float time, int layerIndex)
    {
        if (layer.material == null) return;
        
        float colorTime = (time * colorShiftSpeed + layer.colorOffset) % 1f;
        Color auroraColor = auroraColors.Evaluate(colorTime);
        
        float intensityTime = time * opacityPulseSpeed + layerIndex * 0.5f;
        float intensity = baseIntensity + (maxIntensity - baseIntensity) * intensityCurve.Evaluate((Mathf.Sin(intensityTime) + 1f) * 0.5f);
        
        auroraColor.a *= intensity;
        layer.material.color = auroraColor;
        
        if (layer.material.HasProperty("_MainTex"))
        {
            Vector2 offset = layer.material.mainTextureOffset;
            offset.x = time * 0.1f * layer.speedMultiplier;
            layer.material.mainTextureOffset = offset;
        }
    }
    
    private void OnDestroy()
    {
        if (_auroraLayers != null)
        {
            foreach (var layer in _auroraLayers)
            {
                if (layer.material != null)
                {
                    DestroyImmediate(layer.material);
                }
            }
        }
    }
    
    private void OnValidate()
    {
        auroraLayers = Mathf.Clamp(auroraLayers, 1, 10);
        auroraHeight = Mathf.Max(0f, auroraHeight);
        auroraWidth = Mathf.Max(0f, auroraWidth);
        baseIntensity = Mathf.Clamp01(baseIntensity);
        maxIntensity = Mathf.Max(baseIntensity, maxIntensity);
    }
}