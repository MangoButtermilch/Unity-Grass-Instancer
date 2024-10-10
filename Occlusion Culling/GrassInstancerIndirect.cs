using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using System;
using UnityEngine.Rendering;

struct Chunk
{
    public Vector3 position; //World position of chunk
    public int instanceStartIndex; //Start index inside trsBuffer
    public int instanceCount; //End index inside trsBuffer

    public Chunk(Vector3 p)
    {
        position = p;
        instanceStartIndex = -1;
        instanceCount = 0;
    }
};

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

    private Bounds _renderBounds;
    private Camera _cam;

    [Header("Chunking")]
    [SerializeField] private int _chunkSize = 16;
    private ComputeBuffer _chunkBuffer;
    private Chunk[] _chunks;
    private int _numChunks;
    [SerializeField] private uint _threadsChunkRender;
    private int _kernelChunkRender;

    [Header("Occlusion")]
    private Texture _cameraDepthTexture;
    [SerializeField][Range(0.0001f, 0.1f)] private float _depthBias = 0.001f;


    private void Start()
    {
        _cam = Camera.main;
        _cam.depthTextureMode = DepthTextureMode.Depth;

        InitializeGrassChunkPositions();
        InitializeGrassInstancesPerChunk();
        InitializeComputeShaderAndMaterial();
    }

    private void Update()
    {
        if (_cameraDepthTexture == null)
        {
            _cameraDepthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
            _visiblityComputeShader.SetTextureFromGlobal(_kernelChunkRender, "_DepthTexture", "_CameraDepthTexture");
            return;
        }

        RenderInstances();
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
        _chunkBuffer?.Release();
        _chunkBuffer?.Dispose();
    }

    private void OnDrawGizmos()
    {
        if (!_drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));


        Gizmos.color = Color.green;
        if (_chunks == null) return;
        foreach (Chunk c in _chunks)
        {
            Gizmos.DrawWireCube(c.position, _chunkSize * Vector3.one);
        }
    }

    /// <summary>
    /// Initializes all buffers and variables for compute shader + material.
    /// </summary>
    private void InitializeComputeShaderAndMaterial()
    {
        _kernelChunkRender = _visiblityComputeShader.FindKernel("ChunkRender");

        _visibleBuffer = new ComputeBuffer(_trueInstanceCount, sizeof(float) * 4 * 4, ComputeBufferType.Append);

        _chunkBuffer = new ComputeBuffer(_numChunks, 3 * sizeof(float) + 2 * sizeof(int));
        _chunkBuffer.SetData(_chunks);

        _visiblityComputeShader.SetFloat("depthBias", _depthBias); // to avoid precision errors and flickering chunks

        _visiblityComputeShader.SetBuffer(_kernelChunkRender, "trsBuffer", _trsBuffer);
        _visiblityComputeShader.SetBuffer(_kernelChunkRender, "visibleList", _visibleBuffer);
        _visiblityComputeShader.SetBuffer(_kernelChunkRender, "chunkBuffer", _chunkBuffer);

        _visiblityComputeShader.SetInt("chunkSize", _chunkSize);
        _visiblityComputeShader.SetInt("numChunks", _numChunks);
        _visiblityComputeShader.SetInt("chunkSize", _chunkSize);
        _visiblityComputeShader.SetInt("instanceCount", _trueInstanceCount);

        _visiblityComputeShader.SetVectorArray("viewFrustumPlanes", GetViewFrustumPlaneNormals());

        _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        _readBackArgsBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
        _material.SetBuffer("visibleList", _visibleBuffer);

        //Data is now all on GPU
        if (!_drawGizmos) _chunks = null;
    }


    private void RenderInstances()
    {
        UpdateCameraViewProjectionMatrix();

        _material.SetFloat("_RecieveShadow", _recieveShadows ? 1f : 0f);

        if (_mesh == null) return;

        _visiblityComputeShader.SetVector("camPos", _cam.transform.position);
        _visiblityComputeShader.SetFloat("maxViewDistance", _maxViewDistance);
        _visiblityComputeShader.SetVectorArray("viewFrustumPlanes", GetViewFrustumPlaneNormals());

        _visibleBuffer.SetCounterValue(0);
        _visiblityComputeShader.Dispatch(_kernelChunkRender, Mathf.CeilToInt(_numChunks / _threadsChunkRender), 1, 1);

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

    /// <summary>
    /// Updates vpMatrix property in compute shader. Forced to make our own VP matrix since UNITY_MATRIX_VP does not contain the right values for us.
    /// </summary>
    private void UpdateCameraViewProjectionMatrix()
    {
        Matrix4x4 mat = GL.GetGPUProjectionMatrix(_cam.projectionMatrix, false) * _cam.worldToCameraMatrix;
        _visiblityComputeShader.SetMatrix("vpMatrix", mat);
    }

    /// <summary>
    /// Fetches amount of elements inside _visibleBuffer with an additional buffer.
    /// Stores result in _visibleInstanceCount.
    /// </summary>
    private void SetVisibleInstanceCount()
    {
        ComputeBuffer.CopyCount(_visibleBuffer, _readBackArgsBuffer, 0);
        int[] appendBufferCount = new int[1];
        _readBackArgsBuffer.GetData(appendBufferCount);
        _visibleInstanceCount = appendBufferCount[0];
    }

    /// <summary>
    /// Initializes grass chunk array with their x and z positions in a grid.
    /// </summary>
    private void InitializeGrassChunkPositions()
    {

        //whole range from -range.x to range.x
        int wholeRangeX = (int)(_range.x * 2);
        int wholeRangeZ = (int)(_range.y * 2);

        _numChunks = Mathf.CeilToInt(wholeRangeX / _chunkSize) * Mathf.CeilToInt(wholeRangeZ / _chunkSize);
        _chunks = new Chunk[_numChunks];

        //Used for centering grid
        int chunkSizeHalf = Mathf.CeilToInt(_chunkSize / 2);

        int startOffsetX = Mathf.CeilToInt(_range.x - chunkSizeHalf);
        int startOffsetZ = Mathf.CeilToInt(_range.y - chunkSizeHalf);
        Vector3 gridStartPos = transform.position - new Vector3(startOffsetX, 0, startOffsetZ);

        int xOffset = 0;
        int zOffset = 0;

        int chunksPerRow = wholeRangeX / _chunkSize;

        for (int i = 0; i < _numChunks; i++)
        {
            Vector3 p = gridStartPos;
            p.x += _chunkSize * xOffset;
            p.z += _chunkSize * zOffset;

            bool isSameRow = (i % chunksPerRow) < chunksPerRow - 1;

            xOffset = isSameRow ? xOffset + 1 : 0; //if same row then continue forward, else start at 0 again
            zOffset = isSameRow ? zOffset : zOffset + 1; //if same row keep z else got to the right (next row)

            _chunks[i] = new Chunk(p);
        }
    }

    /// <summary>
    /// Initializes grass instances for each chunk by performing raycasts to determine valid placement positions.
    /// </summary>
    /// <remarks>
    /// For each chunk, this method:
    /// 1. Calculates the number of instances needed based on the total instances and number of chunks.
    /// 2. Performs raycasts within the chunk to find valid ground positions.
    /// 3. Filters out positions that are too steep based on the ground normal.
    /// 4. Generates transformation matrices (position, rotation, scale) for each valid instance and stores them in a list.
    /// 5. Updates the chunk's instance count and start index in the global list.
    /// 6. Calculates the average Y position of instances in each chunk for accurate chunk positioning.
    /// 7. Uploads the transformation data to a compute buffer for GPU processing.
    /// </remarks>
    private void InitializeGrassInstancesPerChunk()
    {
        _trueInstanceCount = 0;
        _renderBounds = new Bounds(transform.position, Vector3.one * _range.x);

        Vector3 dir = Vector3.down;
        QueryParameters parameters = new QueryParameters(_groundLayer, false);

        int instancesPerChunk = Mathf.FloorToInt(_instances / _numChunks);
        print("Num chunks : " + _numChunks);
        print("instances per chunk : " + instancesPerChunk);

        for (int c = 0; c < _numChunks; c++)
        {
            NativeArray<RaycastHit> chunkResults = new NativeArray<RaycastHit>(instancesPerChunk, Allocator.TempJob);
            NativeArray<RaycastCommand> chunkCommands = new NativeArray<RaycastCommand>(instancesPerChunk, Allocator.TempJob);

            for (int i = 0; i < instancesPerChunk; i++)
            {
                chunkCommands[i] = new RaycastCommand(GetRandomRayPositionForChunk(_chunks[c]), dir, parameters);
            }

            JobHandle handle = RaycastCommand.ScheduleBatch(chunkCommands, chunkResults, 16, default);
            handle.Complete();

            for (int i = 0; i < chunkResults.Length; i++)
            {
                RaycastHit hit = chunkResults[i];
                if (IsToSteep(hit.normal, dir)) continue;

                Quaternion rotation = GetRotationFromNormal(hit.normal);
                Vector3 scale = GetRandomScale();
                Vector3 targetPos = hit.point;

                targetPos.y += scale.z / 2f; //keep or remove, depends on your mesh scaling
                _trsList.Add(Matrix4x4.TRS(targetPos, rotation, scale));

                _chunks[c].instanceCount++;

                _trueInstanceCount++;
            }

            int lastIndex = _trsList.Count;
            int firstIndex = lastIndex - _chunks[c].instanceCount;
            _chunks[c].instanceStartIndex = firstIndex;

            //Adjusting chunk y position to the average of all instances inside it
            float sumY = 0f;

            int start = _chunks[c].instanceStartIndex;
            int end = start + _chunks[c].instanceCount;
            for (int i = start; i < end; i++)
            {
                sumY += _trsList[i].GetColumn(3).y; //y coord
            }
            int count = _chunks[c].instanceCount;
            float averageY = count > 0 ? sumY / count : _chunks[c].position.y;
            _chunks[c].position.y = averageY;

            chunkResults.Dispose();
            chunkCommands.Dispose();
        }


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

    private Vector3 GetRandomRayPositionForChunk(Chunk chunk)
    {
        float halfChunkSize = _chunkSize / 2f;
        return new Vector3(
        chunk.position.x + UnityEngine.Random.Range(-halfChunkSize, halfChunkSize),
        chunk.position.y + 1000,
        chunk.position.z + UnityEngine.Random.Range(-halfChunkSize, halfChunkSize));
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