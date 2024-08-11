using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassInstancerIndirect : MonoBehaviour
{

    [Header("Debugging")]
    [SerializeField] private bool _drawGizmos;
    [Header("Instancing")]
    [SerializeField] private int _instances;
    [SerializeField] private int _trueInstanceCount;
    [Header("Grass settings")]
    [SerializeField] private Vector2 _range;
    [SerializeField] private Vector3 _scaleMin = Vector3.one;
    [SerializeField] private Vector3 _scaleMax = Vector3.one;
    [SerializeField][Range(0f, 1f)] private float _steepness;
    [SerializeField] private bool _rotateToGroundNormal = false;
    [SerializeField] private bool _randomYAxisRotation = false;
    [SerializeField] private float _maxYRotation = 90;
    [SerializeField] private LayerMask _groundLayer;
    [Header("Rendering")]
    [SerializeField] private Material _material;
    [SerializeField] private Mesh _mesh;
    [SerializeField] private bool _castShadows;
    [SerializeField] private bool _recieveShadows;

    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _trsBuffer;
    private List<Matrix4x4> _trsList = new List<Matrix4x4>();

    private void Start()
    {
        Initialize();
    }

    private void Update()
    {
        RenderInstances();
    }

    private void OnDestroy()
    {
        _argsBuffer?.Release();
        _argsBuffer?.Dispose();
        _trsBuffer?.Release();
        _trsBuffer?.Dispose();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));
    }

    private void RenderInstances()
    {
        if (_mesh == null) return;
        
        _material.SetFloat("_RecieveShadow", _recieveShadows ? 1f : 0f);

        Graphics.DrawMeshInstancedIndirect(
            _mesh,
            0,
            _material,
            new Bounds(transform.position, Vector3.one * _range.x),
            _argsBuffer,
            0,
            null,
            _castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off
        );
    }

    private void Initialize()
    {
        RaycastHit hit;
        Ray ray = new Ray(Vector3.zero, Vector3.down);

        for (int i = 0; i < _instances; i++)
        {
            Vector3 rayTestPosition = GetRandomRayPosition();
            ray.origin = rayTestPosition;

            if (!HitSomething(ray, out hit) || IsToSteep(hit.normal, ray.direction)) continue;

            Quaternion rotation = GetRotationFromNormal(hit.normal);
            Vector3 scale = GetRandomScale();
            Vector3 targetPos = hit.point;

            targetPos.y += scale.y / 2f; //keep or remove, depends on your mesh scaling
            _trsList.Add(Matrix4x4.TRS(targetPos, rotation, scale));
            _trueInstanceCount++;
        }

        uint[] args = new uint[5];
        args[0] = (uint)_mesh.GetIndexCount(0);
        args[1] = (uint)_trueInstanceCount;
        args[2] = (uint)_mesh.GetIndexStart(0);
        args[3] = (uint)_mesh.GetBaseVertex(0);
        args[4] = 0;

        _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        _trsBuffer = new ComputeBuffer(_trueInstanceCount, 4 * 4 * sizeof(float));
        _trsBuffer.SetData(_trsList.ToArray());

        _material.SetBuffer("trsBuffer", _trsBuffer);
        _trsList.Clear();

    }

    private bool HitSomething(Ray ray, out RaycastHit hit)
    {
        return Physics.Raycast(ray, out hit, Mathf.Infinity, _groundLayer);
    }

    private Vector3 GetRandomRayPosition()
    {
        return new Vector3(transform.position.x + Random.Range(-_range.x, _range.x), transform.position.y + 100, transform.position.z + Random.Range(-_range.y, _range.y));
    }

    private bool IsToSteep(Vector3 normal, Vector3 direction)
    {
        float dot = Mathf.Abs(Vector3.Dot(normal, direction));
        return dot < _steepness;
    }

    private Vector3 GetRandomScale()
    {
        return new Vector3(Random.Range(_scaleMin.x, _scaleMax.x), Random.Range(_scaleMin.y, _scaleMax.y), Random.Range(_scaleMin.z, _scaleMax.z));
    }

    private Quaternion GetRotationFromNormal(Vector3 normal)
    {
        Vector3 eulerIdentiy = Quaternion.ToEulerAngles(Quaternion.identity);
        eulerIdentiy.x += 90; //can be removed or changed, depends on your mesh orientation

        if (_randomYAxisRotation) eulerIdentiy.y += Random.Range(-_maxYRotation, _maxYRotation);

        if (_rotateToGroundNormal)
        {
            return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(eulerIdentiy);
        }
        return Quaternion.Euler(eulerIdentiy);
    }
}
