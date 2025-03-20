using UnityEngine;

namespace Acetix.Grass
{
    public class GrassInstancer
    {

        private InstancerState _state = InstancerState.IDLE;

        public int Id { get; private set; }
        private GrassInstancerSettings _settings;
        private Transform _parent;
        public Vector3 _position { get; private set; }
        private GameObject _colliderObj = null;

        private bool _debugChunks = false;

        private int _instances => _settings.NumInstancesPerChunk;
        private int _trueInstanceCount = 0;
        private Vector2 _range;

        private ComputeShader _grassComputeShader;
        private Material _material;
        private Mesh _mesh => _settings.MeshLOD0;

        private int _subMeshIndex = 0;
        private int _kernelChunkRender;
        private int _kernelChunkBeforeRender;
        private int _kernelInitChunkInstanceCount;
        private int _kernelInitInstanceTransforms;
        private ComputeBuffer _trsBuffer; //Contains all TRS matrices
        private ComputeBuffer _visibleBuffer; //Contains indices of Visible TRS matrices from _trsBuffer
        private ComputeBuffer _argsBuffer;
        private ComputeBuffer _chunkBuffer;
        private ComputeBuffer _instanceCounterBuffer; //Needed to atomically count the amount of instances
        private Bounds _renderBounds;

        private Chunk[] _chunks;
        private int _chunkSize;
        private int _numChunks;

        private readonly uint _threadsChunkRender = 32;
        private readonly uint _threadsChunkInit = 32;

        private Terrain _terrain;
        private TerrainData _terrainData;

        private bool _chunksInitialized = false;
        private bool _renderShadows => _settings.CastShadows && IsMainChunk && IsCameraChunk;

        public bool IsEmptyChunk => _state == InstancerState.EMPTY;
        public bool IsMainChunk => _state == InstancerState.RENDER;

        public Vector3[] Corners = new Vector3[8];

        public bool IsCameraChunk = false;
        public bool Visible = false;

        public GrassInstancer(
            int id,
            Vector3 position,
            Transform parent,
            Terrain terrain,
            GrassInstancerSettings settings,
            bool drawSubChunks
        )
        {
            Id = id;
            _position = position;
            _parent = parent;
            _terrain = terrain;
            _terrainData = _terrain.terrainData;
            _settings = settings;

            _debugChunks = drawSubChunks;

            _grassComputeShader = GameObject.Instantiate(_settings.GrassComputeShader);
            _material = GameObject.Instantiate(_settings.Material);

            _trueInstanceCount = 0;

            _range = new Vector2(_settings.GrassChunkSize, _settings.GrassChunkSize);
            _renderBounds = new Bounds(_position, _terrainData.size);
            _chunkSize = _settings.GrassSubChunkSize;

            Visible = true;

            SetupChunkCorners();
            CreateColliderObject();
        }

        ~GrassInstancer()
        {
            OnDestroy();
        }

        public void OnDestroy()
        {
            ChangeState(InstancerState.IDLE);

            ReleaseBuffers();

            if (_colliderObj != null)
            {
                GameObject.Destroy(_colliderObj.GetComponent<BoxCollider>());
                GameObject.Destroy(_colliderObj.GetComponent<GrassInstancerCollider>());
                GameObject.Destroy(_colliderObj);
            }

            GameObject.Destroy(_material);
            GameObject.Destroy(_grassComputeShader);
        }

        public void ChangeState(InstancerState state)
        {
            _state = state;
            HandleState();
        }

        private void HandleState()
        {
            switch (_state)
            {
                case InstancerState.IDLE:
                    _chunksInitialized = false;
                    break;
                case InstancerState.RELEASE:
                    ReleaseBuffers();
                    break;
                case InstancerState.EMPTY:
                    ReleaseBuffers();
                    break;
                default:
                    TryInitChunks();
                    break;
            }
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            switch (_state)
            {
                case InstancerState.RENDER:
                    Gizmos.color = Color.green;
                    break;
                case InstancerState.PREWARM:
                    Gizmos.color = Color.cyan;
                    break;
                case InstancerState.EMPTY:
                    Gizmos.color = Color.red;
                    break;
                case InstancerState.RELEASE:
                    return;
            }

            if (!Visible && _state != InstancerState.PREWARM)
            {
                Gizmos.color = Color.black;
            }
            for (int i = 0; i < Corners.Length; i++)
            {
                Vector3 p = Corners[i];
                Gizmos.DrawCube(p, Vector3.one * 32);

            }

            Gizmos.DrawWireCube(_position, (_range.x) * Vector3.one);

            if (!_debugChunks || _chunks == null) return;

            if (!IsCameraChunk) return;

            Gizmos.color = Color.red;
            for (int i = 0; i < _chunks.Length; i++)
            {
                Gizmos.DrawWireCube(_chunks[i].position, _chunkSize * Vector3.one);
            }
        }


        public void Render(
            Matrix4x4 viewProjectionMatrix,
            Vector4[] viewFrustumPlaneNormals,
            Vector3 cameraPosition)
        {
            if (!_chunksInitialized || !Visible || _state != InstancerState.RENDER) return;

            if (_visibleBuffer == null)
            {
                ChangeState(InstancerState.EMPTY);
                return;
            }

            _grassComputeShader.SetFloat("depthBias", _settings.DepthBias);
            _grassComputeShader.SetFloat("maxViewDistance", _settings.MaxViewDistance);
            _grassComputeShader.SetMatrix("vpMatrix", viewProjectionMatrix);

            _grassComputeShader.SetFloat("lodThreshhold1", _settings.LodThreshhold1);
            _grassComputeShader.SetFloat("lodThreshhold2", _settings.LodThreshhold2);

            _grassComputeShader.SetFloat("fadeStart", _settings.FadeStart);
            _grassComputeShader.SetFloat("fadeEnd", _settings.FadeEnd);

            _grassComputeShader.SetVector("camPos", cameraPosition);
            _grassComputeShader.SetVectorArray("viewFrustumPlanes", viewFrustumPlaneNormals);

            //reset counter before dispatching every frame
            _visibleBuffer.SetCounterValue(0);

            _grassComputeShader.Dispatch(_kernelChunkBeforeRender, 1, 1, 1);
            _grassComputeShader.Dispatch(_kernelChunkRender, Mathf.FloorToInt(_numChunks / _threadsChunkRender), 1, 1);

            Graphics.DrawMeshInstancedIndirect(
              _mesh,
              0,
              _material,
              _renderBounds,
              _argsBuffer,
              0,
              null,
              _renderShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off
            );
        }

        /// <summary>
        /// Tries to init chunks with instances, if not already done.
        /// </summary>
        private void TryInitChunks()
        {
            if (_chunksInitialized || IsEmptyChunk) return;
            _chunksInitialized = true;

            InitializeGrassChunkPositions();
            InitializeGrassChunkInstances();

            _argsBuffer.SetData(new uint[5] {
            _mesh.GetIndexCount(_subMeshIndex), 0, _mesh.GetIndexStart(_subMeshIndex), _mesh.GetBaseVertex(_subMeshIndex), 0
        });
        }

        /// <summary>
        /// Initializes grass chunk array with their x and z positions in a grid.
        /// </summary>
        private void InitializeGrassChunkPositions()
        {
            if (_chunks != null) return;

            //whole range from -range.x to range.x
            int wholeRangeX = (int)_range.x;
            int wholeRangeZ = (int)_range.y;

            _numChunks = Mathf.CeilToInt(wholeRangeX / _chunkSize) * Mathf.CeilToInt(wholeRangeZ / _chunkSize);
            _chunks = new Chunk[_numChunks];

            //Used for centering grid
            int chunkSizeHalf = Mathf.CeilToInt(_chunkSize / 2);

            int startOffsetX = Mathf.CeilToInt(_range.x / 2f - chunkSizeHalf);
            int startOffsetZ = Mathf.CeilToInt(_range.y / 2f - chunkSizeHalf);
            Vector3 gridStartPos = _position - new Vector3(startOffsetX, 0, startOffsetZ);

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
        /// Initializes grass instances for each chunk via compute shader.
        /// </summary>
        private void InitializeGrassChunkInstances()
        {
            int instancesPerChunk = Mathf.CeilToInt(_instances / _numChunks);

            _kernelChunkBeforeRender = _grassComputeShader.FindKernel("ChunkBeforeRender");
            _kernelChunkRender = _grassComputeShader.FindKernel("ChunkRender");
            _kernelInitChunkInstanceCount = _grassComputeShader.FindKernel("InitChunkInstanceCount");
            _kernelInitInstanceTransforms = _grassComputeShader.FindKernel("InitInstanceTransforms");

            _grassComputeShader.SetFloat("depthBias", _settings.DepthBias);
            _grassComputeShader.SetFloat("maxViewDistance", _settings.MaxViewDistance);

            _grassComputeShader.SetFloat("grassThreshhold", _settings.GrassThreshhold);
            _grassComputeShader.SetFloat("minGrassHeight", _settings.MinGrassHeight);
            _grassComputeShader.SetFloat("halfChunkSize", _settings.GrassSubChunkSize / 2f);

            _grassComputeShader.SetFloat("lodThreshhold1", _settings.LodThreshhold1);
            _grassComputeShader.SetFloat("lodThreshhold2", _settings.LodThreshhold2);
            _grassComputeShader.SetInt("numChunks", _numChunks);
            _grassComputeShader.SetInt("chunkSize", _chunkSize);
            _grassComputeShader.SetInt("instancesPerChunk", instancesPerChunk);

            _grassComputeShader.SetVector("scaleMin", _settings.ScaleMin);
            _grassComputeShader.SetVector("scaleMax", _settings.ScaleMax);
            _grassComputeShader.SetFloat("scaleNoiseScale", _settings.ScaleNoiseScale);

            _grassComputeShader.SetVector("terrainPos", _terrain.transform.position);
            _grassComputeShader.SetVector("terrainSize", _terrainData.size);
            _grassComputeShader.SetInt("terrainHeightmapResolution", _terrainData.heightmapResolution);

            _grassComputeShader.SetTexture(_kernelInitInstanceTransforms, "Heightmap", _terrainData.heightmapTexture);
            _grassComputeShader.SetTexture(_kernelInitInstanceTransforms, "Splatmap", _terrainData.alphamapTextures[0]);

            _grassComputeShader.SetTexture(_kernelInitChunkInstanceCount, "Heightmap", _terrainData.heightmapTexture);
            _grassComputeShader.SetTexture(_kernelInitChunkInstanceCount, "Splatmap", _terrainData.alphamapTextures[0]);


            _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);

            _instanceCounterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            _instanceCounterBuffer.SetData(new int[] { 0 });

            _chunkBuffer = new ComputeBuffer(_numChunks, 3 * sizeof(float) + 2 * sizeof(int));
            _chunkBuffer.SetData(_chunks);

            _grassComputeShader.SetBuffer(_kernelChunkRender, "argsBuffer", _argsBuffer);
            _grassComputeShader.SetBuffer(_kernelChunkBeforeRender, "argsBuffer", _argsBuffer);

            _grassComputeShader.SetBuffer(_kernelInitChunkInstanceCount, "chunkBuffer", _chunkBuffer);
            _grassComputeShader.SetBuffer(_kernelInitChunkInstanceCount, "instanceCounter", _instanceCounterBuffer);

            _grassComputeShader.SetBuffer(_kernelInitInstanceTransforms, "chunkBuffer", _chunkBuffer);
            _grassComputeShader.SetBuffer(_kernelInitInstanceTransforms, "instanceCounter", _instanceCounterBuffer);

            // First calculating instances per chunk
            _grassComputeShader.Dispatch(_kernelInitChunkInstanceCount, Mathf.CeilToInt(_numChunks / _threadsChunkInit), 1, 1);

            // _instanceCounterBuffer now contains sum of all instances of all chunks
            int[] cb = new int[1];
            _instanceCounterBuffer.GetData(cb);
            _trueInstanceCount = cb[0];

            //special case if no instances could be created (for example in only rocky terrain)
            if (_trueInstanceCount == 0)
            {
                _chunks = null;
                ChangeState(InstancerState.EMPTY);
                return;
            }

            // Now generate the TRS buffer for rendering
            _trsBuffer = new ComputeBuffer(_trueInstanceCount, sizeof(float) * 4 * 4);
            _grassComputeShader.SetBuffer(_kernelInitInstanceTransforms, "trsBuffer", _trsBuffer);
            // Create buffer for indexing the TRS buffer in the material
            _visibleBuffer = new ComputeBuffer(_trueInstanceCount, 2 * sizeof(uint), ComputeBufferType.Append);
            _grassComputeShader.SetBuffer(_kernelChunkRender, "visibleBuffer", _visibleBuffer);
            _grassComputeShader.SetBuffer(_kernelChunkRender, "trsBuffer", _trsBuffer);
            _grassComputeShader.SetBuffer(_kernelChunkRender, "chunkBuffer", _chunkBuffer);


            // Fill buffer for rendering with chunk data
            _grassComputeShader.Dispatch(_kernelInitInstanceTransforms, Mathf.CeilToInt(_numChunks / _threadsChunkInit), 1, 1);

            _material.SetBuffer("visibleBuffer", _visibleBuffer);
            _material.SetBuffer("trsBuffer", _trsBuffer);

            _instanceCounterBuffer?.Release();

            if (!_debugChunks)
            {
                _chunks = null;
                return;
            }
            _chunkBuffer.GetData(_chunks);
        }

        /// <summary>
        /// Creates a new object for this instancer with a collider to trigger rendering.
        /// </summary>
        private void CreateColliderObject()
        {
            _colliderObj = new GameObject("instancer " + Id.ToString());
            _colliderObj.transform.parent = _parent;
            _colliderObj.isStatic = true;
            _colliderObj.layer = LayerMask.NameToLayer("GrassRenderer");
            _colliderObj.AddComponent<GrassInstancerCollider>().Id = Id;
            _colliderObj.AddComponent<BoxCollider>();
            _colliderObj.GetComponent<BoxCollider>().isTrigger = true;
            _colliderObj.GetComponent<BoxCollider>().center = _position;
            _colliderObj.GetComponent<BoxCollider>().size = Vector3.one * _range.x + Vector3.up * _range.x * 20;
        }

        /// <summary>
        /// Creates 8 corners for the chunk + 1 for center + 1 for the lower center. Also sets bottom corners to terrain height.
        /// </summary>
        private void SetupChunkCorners()
        {
            float halfSize = _settings.GrassChunkSize * .5f;

            Corners = new Vector3[10] {
            _position,
            _position + new Vector3(0, -halfSize, 0),

            _position + new Vector3(halfSize, halfSize, halfSize),
            _position + new Vector3(-halfSize, halfSize, halfSize),
            _position + new Vector3(halfSize, halfSize, -halfSize),
            _position + new Vector3(-halfSize, halfSize, -halfSize),

            _position + new Vector3(halfSize, -halfSize, halfSize),
            _position + new Vector3(-halfSize, -halfSize, halfSize),
            _position + new Vector3(halfSize, -halfSize, -halfSize),
            _position + new Vector3(-halfSize, -halfSize, -halfSize)
        };

            //make lower corners align with terrain
            for (int i = 6; i < Corners.Length; i++)
            {
                Ray ray = new Ray(Corners[i] + Vector3.up * 1000, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _settings.TerrainLayerMask))
                {
                    Corners[i].y = hit.point.y;
                }
            }
        }

        private void ReleaseBuffers()
        {
            _chunksInitialized = false;
            _argsBuffer?.Release();
            _trsBuffer?.Release();
            _visibleBuffer?.Release();
            _chunkBuffer?.Release();
            _instanceCounterBuffer?.Release();
        }

        public void SetCameraDepthTexture(Texture tex)
        {
            _grassComputeShader.SetTexture(_kernelChunkRender, "_DepthTexture", tex);
        }
    }
}