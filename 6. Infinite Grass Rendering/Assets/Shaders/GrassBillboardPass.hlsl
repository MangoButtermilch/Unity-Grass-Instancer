
#include "noise.hlsl" 
#include "quaternion-matrix-utils.hlsl" 
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

#include "GrassDefines.hlsl"

struct VertexInput
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    float3 normal: NORMAL;
    uint id : SV_VertexID;
};

struct VertexOutput
{
    float4 vertex : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float3 dynamicWorldPos: TEXCOORD3;//world position moved by wind and other things
    float3 staticWorldPos: TEXCOORD4;//static world position for sampling a texture
    float3 scale: TEXCOORD5;
    float distToCam: TEXCOORD6;
    float textureNoise: TEXCOORD7;
    uint lodIndex : TEXCOORD8;
};

//individuall buffers per material
StructuredBuffer<InstanceData> visibleBuffer;
StructuredBuffer<float4x4> trsBuffer;

//global variables prefixed with agr (Acetix grass renderer)
TEXTURE2D_ARRAY(agr_billboardTextures);
SAMPLER(sampler_agr_billboardTextures);
float4 agr_billboardTextures_ST;
int agr_billboardTextureCount;
float2 agr_textureNoiseLayers;
float agr_alphaCutoff;

float4 agr_primaryCol;
float4 agr_aoCol;

float agr_minBrightness;
float agr_shadowBrightness;
float agr_flatShading;

float agr_fadeStart;
float agr_fadeEnd;

float agr_meshDeformationLimitLow;
float agr_meshDeformationLimitTop;
float4 agr_windSpeed;
float agr_windStrength;
float agr_windNoiseScale;

float agr_maxViewDistance;

float3 agr_terrainSize;

float getFade(float distToCam) {
    float start = agr_maxViewDistance * agr_fadeStart;
    float end = agr_maxViewDistance * agr_fadeEnd;
    return saturate((distToCam - start) / (end - start));
}

float GetGrassNoise(float3 worldPos) {
    float2 worldUV = worldPos.xz;
    float noiseLayer1 = 0;
    float noiseLayer2 = 0;
    Unity_GradientNoise_Deterministic_float(worldUV, agr_textureNoiseLayers.x, noiseLayer1);
    Unity_SimpleNoise_float(worldUV, agr_textureNoiseLayers.y, noiseLayer2);
    noiseLayer1 *= noiseLayer2 * 2;
    noiseLayer1 = floor(noiseLayer1 * 5.) / 5;
    return noiseLayer1;
}

//https://discussions.unity.com/t/how-to-compute-fog-in-hlsl-on-urp/943637/4
void Applyfog(inout float4 color, float3 positionWS)
{
    float4 inColor = color;
    
#if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
    float viewZ = -TransformWorldToView(positionWS).z;
    float nearZ0ToFarZ = max(viewZ - _ProjectionParams.y, 0);
    float density = 1.0f - ComputeFogIntensity(ComputeFogFactorZ0ToFar(nearZ0ToFarZ));

    color = lerp(color, unity_FogColor,  density);

#else
    color = color;
#endif
}

void ApplyTexColor(float3 worldPos, float2 uv, float textureNoise, out float4 texCol) {
    uv = uv * agr_billboardTextures_ST.xy + agr_billboardTextures_ST.zw;
    float index = floor(textureNoise * (agr_billboardTextureCount));
    texCol = SAMPLE_TEXTURE2D_ARRAY(agr_billboardTextures, sampler_agr_billboardTextures, uv, index);
}

VertexOutput vert (VertexInput input, uint instanceID : SV_InstanceID)
{
    VertexOutput output;
    InstanceData instanceData = visibleBuffer[instanceID];

    uint index = instanceData.trsIndex; 
    uint lodIndex = instanceData.lodIndex;
    output.lodIndex = lodIndex;

#ifdef SHADOW_CASTER_PASS
    if (lodIndex > 0)
    {
        //Move the vertex out of view to skip fragment pass
        //avoids rendering shadows on other LODs
        output.vertex = float4(0.0, 0.0, -1.0, 1.0);
        return output;
    }
#endif

    float4x4 mat = trsBuffer[index];
    float3 vertex = input.vertex;

    if (instanceData.lodIndex == 0) vertex = agr_lod0Vertices[input.id];
    else if (instanceData.lodIndex == 1) vertex = agr_lod1Vertices[input.id];
    else if (instanceData.lodIndex == 2) vertex = agr_lod2Vertices[input.id];

    //applying transformation matrix
    float3 staticWorldPos = mul(mat, float4(vertex.xyz, 1));
    output.staticWorldPos = staticWorldPos;

    float3 camPos = _WorldSpaceCameraPos;
    output.distToCam = distance(staticWorldPos, camPos);


    float3 dynamicWorldPos = staticWorldPos;
    float3 scale = 0;
    float noise = 0;
  
    if (lodIndex == 0) {
        float4 worldPos = float4(dynamicWorldPos, 1);
        float2 worldUV = worldPos.xz + agr_windSpeed * _Time.y; 
        Unity_SimpleNoise_float(worldUV, agr_windNoiseScale, noise);
        noise = (noise - .5) * 2.; 
        scale = GetScaleFromMatrix(mat);
    }
    output.scale = scale;


    //to keep bottom part of mesh at its position
    output.uv = input.uv;
    //the taller the grass the more it should sway 
    float smoothDeformation = smoothstep(agr_meshDeformationLimitLow, agr_meshDeformationLimitTop, output.uv.y * scale.y + .2);
    float distortion = smoothDeformation * noise;

    //apply distortion
    dynamicWorldPos.xz += distortion * agr_windStrength * normalize(agr_windSpeed);

    //distortion based on view
    float3 viewDisplacement = mul(UNITY_MATRIX_VP, input.normal) * dynamicWorldPos.z;
    dynamicWorldPos.xz += ((1. - viewDisplacement) * 0.0001) * output.uv.y;
    output.dynamicWorldPos = dynamicWorldPos;

    output.vertex = mul(UNITY_MATRIX_VP, float4(dynamicWorldPos, 1));

    //generating texture noise in vertex stage to improve performance
    output.textureNoise = GetGrassNoise(output.staticWorldPos);

    output.normal = (agr_flatShading < 0) ? input.normal : 0;
    return output;
}


float4 frag (VertexOutput input) : SV_Target
{

    float fade = getFade(input.distToCam);

    float4 texCol = 0;
    float noise = input.textureNoise;

    ApplyTexColor(input.staticWorldPos, input.uv, noise, texCol);

    texCol.a *= (1. - fade);
    if (texCol.a < agr_alphaCutoff) {
        discard;
    }
    
    #if defined(_MAIN_LIGHT_SHADOWS_SCREEN) 
        float4 shadowCoord = ComputeScreenPos(TransformWorldToHClip(input.dynamicWorldPos));
    #else
        float4 shadowCoord = TransformWorldToShadowCoord(input.dynamicWorldPos);
    #endif


    // combining noise with color to get differently colored grass patches.
    // also inverting noise so it doesn't match the tex color
    texCol.rgb *= (1. - noise) + agr_minBrightness;
   
    float4 grassColor = texCol * agr_primaryCol;
    float4 ao = lerp(agr_aoCol, 1.0, input.uv.y);
    grassColor *= ao;

    Light mainLight = GetMainLight(shadowCoord);
    float NdotL = dot(mainLight.direction, input.normal) * .5 + .5;
    float diffuseLight = NdotL;

    float shadowAtten = (mainLight.shadowAttenuation + agr_shadowBrightness);
    shadowAtten = ApplyShadowFade(shadowAtten, input.dynamicWorldPos);

    float4 lightColor = float4(mainLight.color, 1) * (diffuseLight * shadowAtten);
    grassColor *= lightColor;
    
    Applyfog(grassColor, input.dynamicWorldPos);

    return grassColor;
}