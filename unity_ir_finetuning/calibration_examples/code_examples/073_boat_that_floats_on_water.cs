// Prompt: boat that floats on water
// Type: general

using UnityEngine;

public class FloatingBoat : MonoBehaviour
{
    [Header("Buoyancy Settings")]
    [SerializeField] private float _buoyancyForce = 15f;
    [SerializeField] private float _waterLevel = 0f;
    [SerializeField] private float _waterDrag = 0.99f;
    [SerializeField] private float _waterAngularDrag = 0.5f;
    
    [Header("Float Points")]
    [SerializeField] private Transform[] _floatPoints;
    [SerializeField] private float _underwaterDrag = 3f;
    [SerializeField] private float _underwaterAngularDrag = 1f;
    
    [Header("Wave Settings")]
    [SerializeField] private bool _useWaves = true;
    [SerializeField] private float _waveHeight = 0.5f;
    [SerializeField] private float _waveSpeed = 1f;
    [SerializeField] private float _waveLength = 10f;
    
    [Header("Stability")]
    [SerializeField] private float _stabilityForce = 50f;
    [SerializeField] private float _stabilityTorque = 50f;
    
    private Rigidbody _rigidbody;
    private bool _isUnderwater;
    private int _underwaterCount;
    private float _originalDrag;
    private float _originalAngularDrag;
    
    private void Start()
    {
        _rigidbody = GetComponent<Rigidbody>();
        if (_rigidbody == null)
        {
            _rigidbody = gameObject.AddComponent<Rigidbody>();
        }
        
        _originalDrag = _rigidbody.drag;
        _originalAngularDrag = _rigidbody.angularDrag;
        
        if (_floatPoints == null || _floatPoints.Length == 0)
        {
            CreateDefaultFloatPoints();
        }
    }
    
    private void CreateDefaultFloatPoints()
    {
        _floatPoints = new Transform[4];
        Bounds bounds = GetComponent<Collider>()?.bounds ?? new Bounds(transform.position, Vector3.one);
        
        GameObject frontLeft = new GameObject("FloatPoint_FrontLeft");
        GameObject frontRight = new GameObject("FloatPoint_FrontRight");
        GameObject backLeft = new GameObject("FloatPoint_BackLeft");
        GameObject backRight = new GameObject("FloatPoint_BackRight");
        
        frontLeft.transform.SetParent(transform);
        frontRight.transform.SetParent(transform);
        backLeft.transform.SetParent(transform);
        backRight.transform.SetParent(transform);
        
        Vector3 size = bounds.size;
        frontLeft.transform.localPosition = new Vector3(-size.x * 0.4f, -size.y * 0.5f, size.z * 0.4f);
        frontRight.transform.localPosition = new Vector3(size.x * 0.4f, -size.y * 0.5f, size.z * 0.4f);
        backLeft.transform.localPosition = new Vector3(-size.x * 0.4f, -size.y * 0.5f, -size.z * 0.4f);
        backRight.transform.localPosition = new Vector3(size.x * 0.4f, -size.y * 0.5f, -size.z * 0.4f);
        
        _floatPoints[0] = frontLeft.transform;
        _floatPoints[1] = frontRight.transform;
        _floatPoints[2] = backLeft.transform;
        _floatPoints[3] = backRight.transform;
    }
    
    private void FixedUpdate()
    {
        ApplyBuoyancy();
        ApplyStability();
        UpdateDrag();
    }
    
    private void ApplyBuoyancy()
    {
        _underwaterCount = 0;
        
        foreach (Transform floatPoint in _floatPoints)
        {
            if (floatPoint == null) continue;
            
            float waterHeight = GetWaterHeightAtPosition(floatPoint.position);
            float difference = floatPoint.position.y - waterHeight;
            
            if (difference < 0)
            {
                _underwaterCount++;
                Vector3 buoyancyForceVector = Vector3.up * _buoyancyForce * Mathf.Abs(difference);
                _rigidbody.AddForceAtPosition(buoyancyForceVector, floatPoint.position, ForceMode.Force);
            }
        }
        
        _isUnderwater = _underwaterCount > 0;
    }
    
    private float GetWaterHeightAtPosition(Vector3 position)
    {
        if (!_useWaves)
        {
            return _waterLevel;
        }
        
        float wave1 = Mathf.Sin((position.x / _waveLength + Time.time * _waveSpeed) * 2 * Mathf.PI) * _waveHeight;
        float wave2 = Mathf.Sin((position.z / _waveLength + Time.time * _waveSpeed * 0.8f) * 2 * Mathf.PI) * _waveHeight * 0.5f;
        
        return _waterLevel + wave1 + wave2;
    }
    
    private void ApplyStability()
    {
        Vector3 stabilityTorqueVector = Vector3.Cross(transform.up, Vector3.up);
        _rigidbody.AddTorque(stabilityTorqueVector * _stabilityTorque, ForceMode.Force);
        
        Vector3 stabilityForceVector = Vector3.up * _stabilityForce;
        _rigidbody.AddForce(stabilityForceVector * (Vector3.Dot(Vector3.down, transform.up) + 1) * 0.5f, ForceMode.Force);
    }
    
    private void UpdateDrag()
    {
        if (_isUnderwater)
        {
            float dragMultiplier = (float)_underwaterCount / _floatPoints.Length;
            _rigidbody.drag = Mathf.Lerp(_originalDrag, _underwaterDrag, dragMultiplier);
            _rigidbody.angularDrag = Mathf.Lerp(_originalAngularDrag, _underwaterAngularDrag, dragMultiplier);
        }
        else
        {
            _rigidbody.drag = _originalDrag;
            _rigidbody.angularDrag = _originalAngularDrag;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (_floatPoints == null) return;
        
        Gizmos.color = Color.blue;
        foreach (Transform floatPoint in _floatPoints)
        {
            if (floatPoint != null)
            {
                Gizmos.DrawWireSphere(floatPoint.position, 0.2f);
            }
        }
        
        Gizmos.color = Color.cyan;
        Vector3 waterPlaneSize = new Vector3(20f, 0.1f, 20f);
        Gizmos.DrawCube(new Vector3(transform.position.x, _waterLevel, transform.position.z), waterPlaneSize);
    }
}