struct InstanceData {
    uint trsIndex; //index inside TRS buffer which contains all transformation matrices
    uint lodIndex; //index for LOD level buffers
};

struct Chunk {
    float3 position;
    uint instanceStartIndex; //start index inside TRS buffer
    uint instanceCount; //start + count = end index inside TRS buffer
};

//global buffers for material shader
#ifdef GRASS_RENDER_PASS

    StructuredBuffer<float3> agr_lod0Vertices;
    StructuredBuffer<float3> agr_lod1Vertices;
    StructuredBuffer<float3> agr_lod2Vertices;

#endif

//data for compute shader
#ifdef GRASS_COMPUTE

    #define THREADS_CHUNK_RENDER 32
    #define THREADS_CHUNK_INIT 32

    RWStructuredBuffer<float4x4> trsBuffer;
    RWStructuredBuffer<Chunk> chunkBuffer;
    AppendStructuredBuffer<InstanceData> visibleBuffer; 
    //see https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Graphics.DrawMeshInstancedIndirect.html
    //index 1 holds the amount of instances to render
    RWStructuredBuffer<uint> argsBuffer;
    RWStructuredBuffer<int> instanceCounter;

    Texture2D<float4> _DepthTexture;
    SamplerState sampler_DepthTexture;

    Texture2D<float4> Heightmap;
    SamplerState sampler_Heightmap;

    Texture2D<float4> Splatmap;
    SamplerState sampler_Splatmap;
#endif
