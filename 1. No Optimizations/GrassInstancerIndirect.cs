using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;

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

    private Vector3 _instancerPos;
    private Bounds _renderBounds;

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
            _renderBounds,
            _argsBuffer,
            0,
            null,
            _castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off
        );
    }

    private void Initialize()
    {
        _instancerPos = transform.position;
        _renderBounds = new Bounds(_instancerPos, Vector3.one * _range.x);

        NativeArray<RaycastHit> results = new NativeArray<RaycastHit>(_instances, Allocator.TempJob);
        NativeArray<RaycastCommand> commands = new NativeArray<RaycastCommand>(_instances, Allocator.TempJob);

        Vector3 dir = Vector3.down;
        QueryParameters parameters = new QueryParameters(_groundLayer, false);

        for (int i = 0; i < _instances; i++)
        {
            commands[i] = new RaycastCommand(GetRandomRayPosition(), dir, parameters);
        }

        JobHandle handle = RaycastCommand.ScheduleBatch(commands, results, 16, default);
        handle.Complete();


        int amount = results.Length;
        for (int i = 0; i < amount; i++)
        {
            RaycastHit hit = results[i];

            if (IsToSteep(hit.normal, dir)) continue;

            Quaternion rotation = GetRotationFromNormal(hit.normal);
            Vector3 scale = GetRandomScale();
            Vector3 targetPos = hit.point;

            targetPos.y += scale.y / 2f; //keep or remove, depends on your mesh scaling
            _trsList.Add(Matrix4x4.TRS(targetPos, rotation, scale));
            _trueInstanceCount++;

        }

        results.Dispose();
        commands.Dispose();

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

    private Vector3 GetRandomRayPosition()
    {
        return new Vector3(
            _instancerPos.x + UnityEngine.Random.Range(-_range.x, _range.x),
            _instancerPos.y + 100,
            _instancerPos.z + UnityEngine.Random.Range(-_range.y, _range.y));
    }

    private bool IsToSteep(Vector3 normal, Vector3 direction)
    {
        float dot = Mathf.Abs(Vector3.Dot(normal, direction));
        return dot < _steepness;
    }

    private Vector3 GetRandomScale()
    {
        return new Vector3(
            UnityEngine.Random.Range(_scaleMin.x, _scaleMax.x),
            UnityEngine.Random.Range(_scaleMin.y, _scaleMax.y),
            UnityEngine.Random.Range(_scaleMin.z, _scaleMax.z));
    }

    private Quaternion GetRotationFromNormal(Vector3 normal)
    {
        Vector3 eulerIdentiy = Quaternion.ToEulerAngles(Quaternion.identity);
        eulerIdentiy.x += 90; //can be removed or changed, depends on your mesh orientation

        if (_randomYAxisRotation) eulerIdentiy.y += UnityEngine.Random.Range(-_maxYRotation, _maxYRotation);

        if (_rotateToGroundNormal)
        {
            return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(eulerIdentiy);
        }
        return Quaternion.Euler(eulerIdentiy);
    }
}
