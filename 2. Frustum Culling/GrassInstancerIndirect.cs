using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using Unity.VisualScripting;
using UnityEngine.UI;

public class GrassInstancerIndirect : MonoBehaviour
{

    [Header("Debugging")]
    [SerializeField] private bool _drawGizmos;
    [Header("Instancing")]
    [SerializeField] private int _instances;
    [SerializeField] private int _trueInstanceCount;
    [SerializeField] private int _visibleInstanceCount;
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
    [SerializeField] private float _maxViewDistance = 30f;

    [SerializeField] private ComputeShader _visiblityComputeShader;
    private ComputeBuffer _visibleBuffer;
    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _trsBuffer;
    private ComputeBuffer _readBackArgsBuffer;
    private List<Matrix4x4> _trsList = new List<Matrix4x4>();

    private Vector3 _instancerPos;
    private Bounds _renderBounds;
    private Camera _cam;

    private void Start()
    {
        _cam = Camera.main;

        Initialize();
        InitializeComputeShader();
    }

    private void Update()
    {
        RenderInstances();
    }

    private void InitializeComputeShader()
    {
        _visibleBuffer = new ComputeBuffer(_trueInstanceCount, sizeof(float) * 4 * 4, ComputeBufferType.Append);
        _visiblityComputeShader.SetBuffer(0, "visibleList", _visibleBuffer);
        _visiblityComputeShader.SetBuffer(0, "trsBuffer", _trsBuffer);
        _visiblityComputeShader.SetVectorArray("viewFrustumPlanes", GetViewFrustumPlaneNormals());

        _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        _readBackArgsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _material.SetBuffer("visibleList", _visibleBuffer);
    }


    private void RenderInstances()
    {
        _material.SetFloat("_RecieveShadow", _recieveShadows ? 1f : 0f);

        if (_mesh == null) return;

        _visiblityComputeShader.SetVectorArray("viewFrustumPlanes", GetViewFrustumPlaneNormals());
        _visiblityComputeShader.SetVector("camPos", _cam.transform.position);
        _visiblityComputeShader.SetFloat("maxViewDistance", _maxViewDistance);

        _visibleBuffer.SetCounterValue(0);
        _visiblityComputeShader.Dispatch(0, Mathf.CeilToInt(_trueInstanceCount / 512), 1, 1);

        SetVisibleInstanceCount();

        _argsBuffer.SetData(new uint[5] {
            _mesh.GetIndexCount(0), (uint)_visibleInstanceCount, 0, 0, 0
        });

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


    private void SetVisibleInstanceCount()
    {
        ComputeBuffer.CopyCount(_visibleBuffer, _readBackArgsBuffer, 0);
        int[] appendBufferCount = new int[1];
        _readBackArgsBuffer.GetData(appendBufferCount);
        _visibleInstanceCount = appendBufferCount[0];
    }


    private void OnDestroy()
    {
        _argsBuffer?.Release();
        _argsBuffer?.Dispose();
        _trsBuffer?.Release();
        _trsBuffer?.Dispose();
        _visibleBuffer?.Release();
        _visibleBuffer?.Dispose();
        _readBackArgsBuffer?.Release();
        _readBackArgsBuffer?.Dispose();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));


        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(Camera.main);

        foreach (Plane p in planes)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(Vector3.zero + p.normal * 5f, p.normal * 20f);
        }
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

            targetPos.y += scale.z / 2f; //keep or remove, depends on your mesh scaling
            _trsList.Add(Matrix4x4.TRS(targetPos, rotation, scale));
            _trueInstanceCount++;

        }

        results.Dispose();
        commands.Dispose();

        _trsBuffer = new ComputeBuffer(_trueInstanceCount, 4 * 4 * sizeof(float));
        _trsBuffer.SetData(_trsList.ToArray());

        _trsList.Clear();
    }

    private Vector4[] GetViewFrustumPlaneNormals()
    {
        Vector4[] planeNormals = new Vector4[6];
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(_cam);

        for (int i = 0; i < 6; i++)
        {
            planeNormals[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
        }
        return planeNormals;
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
