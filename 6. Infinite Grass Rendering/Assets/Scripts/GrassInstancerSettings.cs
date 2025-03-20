using UnityEditor.EditorTools;
using UnityEngine;

namespace Acetix.Grass
{
    [System.Serializable]
    [CreateAssetMenu(fileName = "GrassInstancerSettings", menuName = "Scriptable Objects/GrassInstancerSettings")]
    public class GrassInstancerSettings : ScriptableObject
    {
        [Header("Instancing")]

        [Tooltip("Amount of grass that will be created for a chunk")]
        public int NumInstancesPerChunk = 524288;


        [Tooltip("Size of a chunk in meters")]
        public int GrassChunkSize = 512;

        [Tooltip("Size in meters. Each chunk is divided into sub chunks for frustum and occlusion culling.")]
        public int GrassSubChunkSize = 32;
        public LayerMask TerrainLayerMask;

        [Header("Grass settings")]

        [Tooltip("Minimum scale of grass in meters")]
        public Vector3 ScaleMin = Vector3.one;


        [Tooltip("Maximum scale of grass in meters")]
        public Vector3 ScaleMax = Vector3.one;


        [Range(0f, 1f)]
        [Tooltip("Grass below this threshhold won't be rendered")]
        public float MinGrassHeight = 0.15f;


        [Tooltip("Grass height is determined by a noise texture. This scale is defined here")]
        public float ScaleNoiseScale = 128f;

        [Range(0f, 1f)]
        [Tooltip("Threshhold for sampling the terrain splat map (color). Areas with values below this won't have any grass.")]
        public float GrassThreshhold = 0.5f;

        [Header("Rendering")]
        public LayerMask GrassRendererLayerMask;
        public ComputeShader GrassComputeShader;
        public Mesh MeshLOD0;
        public Mesh MeshLOD1;
        [Tooltip("Percentage of max view distance after LOD 1 mesh will be used.")]
        [Range(0f, 1f)] public float LodThreshhold1 = 0.25f;
        public Mesh MeshLOD2;
        [Tooltip("Percentage of max view distance after LOD 1 mesh will be used.")]
        [Range(0f, 1f)] public float LodThreshhold2 = 0.5f;


        [Tooltip("Max view distance in meters")]
        public float MaxViewDistance = 256f;

        [Range(0.0001f, 0.1f)]
        [Tooltip("Adjust this value if you encounter problems with occlusion culling")]
        public float DepthBias = 0.0001f;



        [Range(0f, 1f)]
        [Tooltip("Start of fading at x percent of max view distance")]
        public float FadeStart = 0.4f;
        [Range(0f, 1f)]
        [Tooltip("End of fading at x percent of max view distance")]
        public float FadeEnd = 1f;

        [Header("Material")]
        public Material Material;
        public Texture2DArray BillboardTexture;
        [Tooltip("Amount of tiles in billboard texture array")]
        public int BillboardTextureCount;
        public Vector4 BillboardTilingAndOffset = new Vector4(1f, 1f, 0f, 0f);
        public Vector2 TextureNoiseLayers = new Vector2(0.01f, 4.41f);
        [Tooltip("Every pixel with alpha value below this will be discarded. Aka. transparency threshhold.")]
        [Range(0f, 1f)] public float AlphaCutoff = 0.5f;
        [Tooltip("Color of the texture")]
        public Color Tint = Color.white;
        [Tooltip("Ambient occlusion color")]
        public Color AoColor = Color.white;
        [Tooltip("Controls brightness of grass. Usefull if flat shading disabled.")]
        public float MinBrightness = 3f;
        [Tooltip("Brightness of shadows casted onto the grass.")]
        public float ShadowBrighntess = 0.2f;
        public bool FlatShading = false;
        public bool CastShadows = false;
        [Header("Wind")]

        [Tooltip("Controls how much the mesh can be deformed by the wind at the bottom.")]
        public float MeshDeformationLimitLow = 0f;
        [Tooltip("Controls how much the mesh can be deformed by the wind at the top.")]
        public float MeshDeformationLimitTop = 3.37f;
        public float WindNoiseScale = 0.78f;
        public float WindStrength = 10f;
        public Vector2 WindSpeed = new Vector2(-9.84f, 6f);
    }
}