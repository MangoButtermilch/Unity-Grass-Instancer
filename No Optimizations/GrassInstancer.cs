using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MeshLOD {
    public Mesh mesh;
    public float lod;
    public bool shadows;
}

public class GrassInstancer : MonoBehaviour {

    private bool _visible;
    private bool _castShadows;
    private Mesh _mesh;
    private Transform _camera;
    private List<List<Matrix4x4>> _batches = new List<List<Matrix4x4>>();

    [SerializeField] private bool _drawGizmos;
    [SerializeField][Range(1, 1023)] private int _batchSize = 1000;
    [SerializeField] private int _instances;
    [SerializeField] private Vector2 _range;
    [SerializeField] private Vector3 _scaleMin = Vector3.one;
    [SerializeField] private Vector3 _scaleMax = Vector3.one;
    [SerializeField][Range(0f, 1f)] private float _steepness;
    [SerializeField] private bool _rotateToGroundNormal = false;
    [SerializeField] private bool _randomYAxisRotation = false;
    [SerializeField] private float _maxYRotation = 90;
    [SerializeField] private bool _recieveShadows;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private Material _material;
    [SerializeField] private MeshLOD[] _meshes;

    private void Start() {
        _camera = Camera.main.transform;
        Initialize();
        transform.position = new Vector3(transform.position.x, _camera.position.y, transform.position.z);
    }

    private void Update() {
        GetMeshFromCameraDistance();
        RenderBatches();
    }

    private void OnBecameVisible() {
        _visible = true;
    }

    private void OnBecameInvisible() {
        _visible = false;
    }

    private void GetMeshFromCameraDistance() {
        if (!_visible) {
            _mesh = null;
            return;
        }
        float dist = Vector3.Distance(_camera.position, transform.position);
        float ratio = dist > 1f ? 1f / Mathf.Clamp(dist, 0.1f, Mathf.Infinity) : dist;
        for (int i = _meshes.Length - 1; i >= 0; i--) {
            if (ratio <= _meshes[i].lod) {
                _mesh = _meshes[i].mesh;
                _castShadows = _meshes[i].shadows;
                break;
            }
        }
    }

    private void RenderBatches() {
        if (_mesh == null) return;
        for (int i = 0; i < _batches.Count; i++) {
            Graphics.DrawMeshInstanced(
                _mesh,
                 0,
                _material,
                _batches[i],
                null,
                _castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off,
                _recieveShadows
            );
        }
    }

    private void OnDrawGizmos() {
        if (!_drawGizmos) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(transform.position, new Vector3(_range.x * 2, 5, _range.y * 2));

    }

    private void Initialize() {
        int addedMatricies = 0;
        _batches.Clear();
        _batches.Add(new List<Matrix4x4>());

        RaycastHit hit;

        for (int i = 0; i < _instances; i++) {
            if (addedMatricies < _batchSize && _batches.Count != 0) {
                Vector3 rayTestPosition = GetRandomRayPosition();
                Ray ray = new Ray(rayTestPosition, Vector3.down);

                if (!Physics.Raycast(ray, out hit, _groundLayer)) continue;

                if (hit.transform.tag.Equals("IgnoreRaycast")) continue;  //can be replaced with whatever you want

                if (IsToSteep(hit.normal, ray.direction)) continue;

                Quaternion rotation = GetRotation(hit.normal);
                Vector3 scale = GetRandomScale();

                Vector3 targetPos = hit.point;
                targetPos.y += scale.y / 2f; //keep or remove, depends on your mesh

                _batches[_batches.Count - 1].Add(Matrix4x4.TRS(targetPos, rotation, scale));
                addedMatricies++;
                continue;
            }
            _batches.Add(new List<Matrix4x4>());
            addedMatricies = 0;

        }
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
