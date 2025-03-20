
using System.Collections.Generic;

namespace Acetix.Grass
{
    using UnityEngine;
    
    public class GrassInstancerController : MonoBehaviour
    {
        [Header("Debugging")]
        [SerializeField]
        [Tooltip("Will show you the chunks.")]
        private bool _drawGizmos;
        [SerializeField]
        [Tooltip("Will show you the sub chunks.")]
        private bool _drawSubChunks;
        [Header("Settings")]
        [SerializeField] public GrassInstancerSettings _settings;

        private GrassInstancer[] _instancers;
        private List<GrassInstancer> _activeInstancers = new List<GrassInstancer>();
        private List<GrassInstancer> _visibleInstancers = new List<GrassInstancer>();
        private int _numInstancers;
        private int _chunkSize => _settings.GrassChunkSize;
        private Vector2 _range; //Range of the controller. Will contain terrain x and z size.

        private ComputeBuffer _lod0VerticesBuffer;
        private ComputeBuffer _lod1VerticesBuffer;
        private ComputeBuffer _lod2VerticesBuffer;
        [Header("Terrain")]
        [SerializeField] private Terrain _terrain;
        private TerrainData _terrainData;

        private Camera _cam;
        private Texture _cameraDepthTexture;

        void Start()
        {
            _cam = Camera.main;
            _cam.depthTextureMode = DepthTextureMode.Depth;
            FetchTerrainData();
            InitInstancerGrid();
            SetupGlobalLODBuffers();
            GrassInstancerEventBus.Subscribe(InstancerEventType.INSTANCER_ENTERED, OnInstancerEntered);
        }

        /// <summary>
        /// Used to fetch depth texture since it's not available in Update loop
        /// </summary>
        void OnRenderObject()
        {
            _cameraDepthTexture = Shader.GetGlobalTexture("_CameraDepthTexture");
            //Depth tex not correctly initialized yet. 
            if (_cameraDepthTexture.width == 4 && _cameraDepthTexture.height == 4)
            {
                return;
            }

            foreach (GrassInstancer instancer in _instancers)
            {
                instancer.SetCameraDepthTexture(_cameraDepthTexture);
            }
        }

        void Update()
        {
            if (_cameraDepthTexture == null) return;

            Vector3 cameraPosition = _cam.transform.position;
            Vector4[] viewFrustumPlaneNormals = GetViewFrustumPlaneNormals();
            Matrix4x4 viewProjectionMatrix = GetCameraViewProjectionMatrix();

            UpdateShaderGlobals();
            DetermineVisibleInstancers(viewFrustumPlaneNormals);

            foreach (GrassInstancer instancer in _visibleInstancers)
            {
                instancer.Render(viewProjectionMatrix, viewFrustumPlaneNormals, cameraPosition);
            }
        }

        private void DetermineVisibleInstancers(Vector4[] viewFrustumPlaneNormals)
        {
            if (_instancers == null) return;
            _visibleInstancers.Clear();

            foreach (var instancer in _activeInstancers)
            {
                bool isCurrent = instancer.IsCameraChunk;

                if ( //to far away or not in view frustum
                    !isCurrent &&
                    !GrassUtils.IsInViewFrustum(viewFrustumPlaneNormals, instancer.Corners))
                {
                    instancer.Visible = false;
                    continue;
                }

                instancer.Visible = true;
                _visibleInstancers.Add(instancer);
            }
        }

        private void SetupGlobalLODBuffers()
        {
            Mesh m0 = _settings.MeshLOD0;
            Mesh m1 = _settings.MeshLOD1;
            Mesh m2 = _settings.MeshLOD2;

            _lod0VerticesBuffer = new ComputeBuffer(m0.vertices.Length, 3 * sizeof(float));
            _lod1VerticesBuffer = new ComputeBuffer(m1.vertices.Length, 3 * sizeof(float));
            _lod2VerticesBuffer = new ComputeBuffer(m2.vertices.Length, 3 * sizeof(float));

            _lod0VerticesBuffer.SetData(m0.vertices);
            _lod1VerticesBuffer.SetData(m1.vertices);
            _lod2VerticesBuffer.SetData(m2.vertices);

            Shader.SetGlobalBuffer("agr_lod0Vertices", _lod0VerticesBuffer);
            Shader.SetGlobalBuffer("agr_lod1Vertices", _lod1VerticesBuffer);
            Shader.SetGlobalBuffer("agr_lod2Vertices", _lod2VerticesBuffer);
        }

        private void UpdateShaderGlobals()
        {
            Shader.SetGlobalVector("agr_terrainSize", _terrainData.size);

            Shader.SetGlobalTexture("agr_billboardTextures", _settings.BillboardTexture);
            Shader.SetGlobalInt("agr_billboardTextureCount", _settings.BillboardTextureCount);
            Shader.SetGlobalVector("agr_billboardTextures_ST", _settings.BillboardTilingAndOffset);
            Shader.SetGlobalVector("agr_textureNoiseLayers", _settings.TextureNoiseLayers);
            Shader.SetGlobalFloat("agr_alphaCutoff", _settings.AlphaCutoff);

            Shader.SetGlobalColor("agr_primaryCol", _settings.Tint);
            Shader.SetGlobalColor("agr_aoCol", _settings.AoColor);

            Shader.SetGlobalFloat("agr_minBrightness", _settings.MinBrightness);
            Shader.SetGlobalFloat("agr_shadowBrightness", _settings.ShadowBrighntess);
            Shader.SetGlobalFloat("agr_flatShading", (_settings.FlatShading ? 1f : -1f));

            Shader.SetGlobalFloat("agr_maxViewDistance", _settings.MaxViewDistance);
            Shader.SetGlobalFloat("agr_fadeStart", _settings.FadeStart);
            Shader.SetGlobalFloat("agr_fadeEnd", _settings.FadeEnd);

            Shader.SetGlobalFloat("agr_meshDeformationLimitLow", _settings.MeshDeformationLimitLow);
            Shader.SetGlobalFloat("agr_meshDeformationLimitTop", _settings.MeshDeformationLimitTop);

            Shader.SetGlobalFloat("agr_windNoiseScale", _settings.WindNoiseScale);
            Shader.SetGlobalFloat("agr_windStrength", _settings.WindStrength);
            Shader.SetGlobalVector("agr_windSpeed", _settings.WindSpeed);
        }


        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            if (!_terrainData)
            {
                FetchTerrainData();
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));

            if (_instancers == null) return;
            Gizmos.color = Color.cyan;
            foreach (GrassInstancer instancer in _instancers)
            {
                instancer.OnDrawGizmos();
            }
        }

        void OnDestroy()
        {
            _lod0VerticesBuffer?.Release();
            _lod1VerticesBuffer?.Release();
            _lod2VerticesBuffer?.Release();
            _lod0VerticesBuffer?.Dispose();
            _lod1VerticesBuffer?.Dispose();
            _lod2VerticesBuffer?.Dispose();

            GrassInstancerEventBus.Unsubscribe(InstancerEventType.INSTANCER_ENTERED, OnInstancerEntered);
            _visibleInstancers.Clear();

            for (int i = 0; i < _instancers.Length; i++)
            {
                if (_instancers[i] == null) continue;
                _instancers[i].OnDestroy();
            }
        }


        private void OnInstancerEntered(int currentChunkId)
        {
            _activeInstancers.Clear();

            float t0 = _chunkSize * 1.5f;
            float t1 = _chunkSize * 2.5f;

            GrassInstancer currentInstancer = _instancers[currentChunkId];
            Vector2 pos2D = new Vector2(currentInstancer._position.x, currentInstancer._position.z);

            foreach (var instancer in _instancers)
            {
                Vector2 otherPos2D = new Vector2(instancer._position.x, instancer._position.z);
                float chunkDistance = Vector2.Distance(pos2D, otherPos2D);
                instancer.IsCameraChunk = false;

                if (chunkDistance < t0)
                {
                    instancer.ChangeState(InstancerState.RENDER);
                    _activeInstancers.Add(instancer);
                }
                else if (chunkDistance < t1 && chunkDistance > t0)
                {
                    instancer.ChangeState(InstancerState.PREWARM);
                }
                else
                {
                    instancer.ChangeState(InstancerState.RELEASE);
                }

            }

            _instancers[currentChunkId].IsCameraChunk = true;
        }

        private void InitInstancerGrid()
        {
            //whole range from -range.x to range.x
            int wholeRangeX = (int)(_range.x * 2);
            int wholeRangeZ = (int)(_range.y * 2);

            _numInstancers = Mathf.CeilToInt(wholeRangeX / _chunkSize) * Mathf.CeilToInt(wholeRangeZ / _chunkSize);
            _instancers = new GrassInstancer[_numInstancers];

            //Used for centering grid
            int chunkSizeHalf = Mathf.CeilToInt(_chunkSize / 2);

            int startOffsetX = Mathf.CeilToInt(_range.x - chunkSizeHalf);
            int startOffsetZ = Mathf.CeilToInt(_range.y - chunkSizeHalf);
            Vector3 gridStartPos = transform.position - new Vector3(startOffsetX, 0, startOffsetZ);

            int xOffset = 0;
            int zOffset = 0;

            int instancersPerRow = wholeRangeX / _chunkSize;

            for (int i = 0; i < _numInstancers; i++)
            {
                Vector3 p = gridStartPos;
                p.x += _chunkSize * xOffset;
                p.z += _chunkSize * zOffset;

                bool isSameRow = (i % instancersPerRow) < instancersPerRow - 1;

                xOffset = isSameRow ? xOffset + 1 : 0; //if same row then continue forward, else start at 0 again
                zOffset = isSameRow ? zOffset : zOffset + 1; //if same row keep z else got to the right (next row)

                //Aligining chunk with terrain
                Ray ray = new Ray(p + Vector3.up * 1000, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, _settings.TerrainLayerMask))
                {
                    p.y = hit.point.y;
                }

                _instancers[i] = new GrassInstancer(i, p, transform, _terrain, _settings, _drawSubChunks);
            }
        }

        private void FetchTerrainData()
        {
            _terrainData = _terrain.terrainData;
            _range.x = _terrainData.size.x / 2f;
            _range.y = _terrainData.size.z / 2f;
        }

        /// <summary>
        /// Forced to make our own VP matrix since UNITY_MATRIX_VP does not contain the right values for us inside compute shader context.
        /// </summary>
        private Matrix4x4 GetCameraViewProjectionMatrix()
        {
            return GL.GetGPUProjectionMatrix(_cam.projectionMatrix, false) * _cam.worldToCameraMatrix;
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
    }
}