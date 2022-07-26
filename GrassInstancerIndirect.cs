using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GrassInstancerIndirect : MonoBehaviour {

    [Header("Debugging")]
    [SerializeField] private bool _drawGizmos;
    [Header("Batches")]
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
    [SerializeField] private bool _recieveShadows;
    [SerializeField] private Transform _mainLight;
    [SerializeField] private MeshLOD[] _meshes;
    private Mesh _mesh;
    private Transform _camera;
    private float distToCamera;
    private bool _castShadows;

    private ComputeBuffer _argsBuffer;
    private ComputeBuffer _trsBuffer;
    private List<Matrix4x4> _trsList = new List<Matrix4x4>();

    private void Start() {
        Initialize();
        Invoke("UpdateLight", 1f);
    }

    private void Update() {
        GetDistToCamera();
        GetMeshFromCameraDistance();
        UpdateLight();
        RenderInstances();
    }

    private void OnDestroy() {
        if (_argsBuffer != null) {
            _argsBuffer.Release();
        }
        if (_trsBuffer != null) {
            _trsBuffer.Release();
        }
    }

    private void OnDrawGizmos() {
        if (!_drawGizmos) return;
        if (_camera == null) _camera = Camera.main.transform;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));
    }

    private void GetDistToCamera() {
        distToCamera = Vector3.Distance(_camera.position, transform.position); ;
    }

    private void GetMeshFromCameraDistance() {
        float clampedDist = Mathf.Clamp(distToCamera, 0.1f, Mathf.Infinity);
        float meshDistRatio = distToCamera > 1f ? 1f / clampedDist : distToCamera;

        for (int i = _meshes.Length - 1; i >= 0; i--) {
            if (meshDistRatio <= _meshes[i].lod) {
                _mesh = _meshes[i].mesh;
                _castShadows = _meshes[i].shadows;
                break;
            }
        }
        if (_mesh == null) {
            _mesh = _meshes[_meshes.Length - 1].mesh;
        }
    }

    private void UpdateLight() {
        Vector3 lightDir = -_mainLight.forward;
        _material.SetVector("_LightDir", new Vector4(lightDir.x, lightDir.y, lightDir.z, 1));
    }

    private void RenderInstances() {
        if (_mesh == null) return;
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

    private void Initialize() {
        _camera = Camera.main.transform;
        RaycastHit hit;

        for (int i = 0; i < _instances; i++) {
            Vector3 rayTestPosition = GetRandomRayPosition();
            Ray ray = new Ray(rayTestPosition, Vector3.down);

            if (!HitSomething(ray, out hit)) continue;
            if (hit.transform.tag.Equals("IgnoreRaycast")) continue;  //can be replaced with whatever you want
            if (IsToSteep(hit.normal, ray.direction)) continue;

            Quaternion rotation = GetRotation(hit.normal);
            Vector3 scale = GetRandomScale();
            Vector3 targetPos = hit.point;

            targetPos.y += scale.y / 2f; //keep or remove, depends on your mesh
            _trsList.Add(Matrix4x4.TRS(targetPos, rotation, scale));
            _trueInstanceCount++;
        }

        Mesh mesh = _meshes[0].mesh;
        uint[] args = new uint[5];
        args[0] = (uint)mesh.GetIndexCount(0);
        args[1] = (uint)_trueInstanceCount;
        args[2] = (uint)mesh.GetIndexStart(0);
        args[3] = (uint)mesh.GetBaseVertex(0);
        args[4] = 0;

        _argsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
        _argsBuffer.SetData(args);

        _trsBuffer = new ComputeBuffer(_trueInstanceCount, 4 * 4 * sizeof(float));
        _trsBuffer.SetData(_trsList.ToArray());

        _material.SetBuffer("trsBuffer", _trsBuffer);
        _trsList.Clear();

    }

    private bool HitSomething(Ray ray, out RaycastHit hit) {
        return Physics.Raycast(ray, out hit, _groundLayer);
    }

    private Vector3 GetRandomRayPosition() {
        return new Vector3(transform.position.x + Random.Range(-_range.x, _range.x), transform.position.y + 100, transform.position.z + Random.Range(-_range.y, _range.y));
    }

    private bool IsToSteep(Vector3 normal, Vector3 direction) {
        float dot = Mathf.Abs(Vector3.Dot(normal, direction));
        return dot < _steepness;
    }

    private Vector3 GetRandomScale() {
        return new Vector3(Random.Range(_scaleMin.x, _scaleMax.x), Random.Range(_scaleMin.y, _scaleMax.y), Random.Range(_scaleMin.z, _scaleMax.z));
    }

    private Quaternion GetRotation(Vector3 normal) {
        Vector3 eulerIdentiy = Quaternion.ToEulerAngles(Quaternion.identity);
        eulerIdentiy.x += 90; //can be removed or changed, depends on your mesh

        if (_randomYAxisRotation) eulerIdentiy.y += Random.Range(-_maxYRotation, _maxYRotation);

        if (_rotateToGroundNormal) {
            return Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(eulerIdentiy);
        }
        return Quaternion.Euler(eulerIdentiy);

    }
}
