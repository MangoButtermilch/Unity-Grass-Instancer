Shader "Unlit/GrassBlillboardIndirect"
{
    Properties
    {
        _GrassTexture ("Texture", 2D) = "white" {}
        _FlowerTex1 ("Texture", 2D) = "white" {}
        _FlowerTex2 ("Texture", 2D) = "white" {}
        _FlowerTex3 ("Texture", 2D) = "white" {}
        _NoiseScale ("Noise Scale", float) = 1.
        _NoiseScale2 ("Noise Scale 2", float) = 1.
        _Cutoff("Alpha cut off",  Range(0.0, 1.0)) = 0
        _PrimaryCol ("Primary Color", Color) = (1, 1, 1)
        _SecondaryCol ("Secondary Color", Color) = (1, 0, 1)
        _AOColor ("AO Color", Color) = (1, 0, 1)
        _TipColor ("Tip Color", Color) = (0, 0, 1)
        _Scale ("Scale", Range(0.0, 2.0)) = 1.28
        _MeshDeformationLimitLow ("Mesh Deformation low limit", Range(0.0, 5.0)) = 0.08
        _MeshDeformationLimitTop ("Mesh Deformation top limit", Range(0.0, 5.0)) = 2.0
        _WindNoiseScale ("Wind Noise Scale", float) = 2.25
        _WindStrength ("Wind Strength", float) = 4.8
        _WindSpeed ("Wind Speed", Vector) = (-4.92, 3, 0, 0)
        _MinBrightness ("Min brightness", float) = 0.5
        _ShadowBrightness ("Shadow brightness", float) = 0.25
        _MaxViewDistance ("Max view distnace", float) = 1024
        _DistanceTilingOffset ("Distance Tiling Offset", float) = .2

    }
    SubShader
    {
        LOD 100
        Cull Off
        ZWrite On
        Name "Grass"
        Tags { 
            "RenderType"="Opaque"
            "RenderPipeline"="UniversalPipeline" 
            "LightMode"="UniversalForwardOnly" 
            "Queue"="Geometry"
        }

        Pass
        {

            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForwardOnly" }
    

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma multi_compile_fog
            #pragma multi_compile_instancing 
			#pragma target 4.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE

            #include "noise.hlsl" 
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 
            #pragma _MAIN_LIGHT_SHADOWS
            #pragma _MAIN_LIGHT_SHADOWS_CASCADE


            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal: NORMAL;
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 shadowCoord: TEXCOORD1;
                float3 positionWorldSpace: TEXCOORD3;
                float3 staticWorldPos: TEXCOORD4;
            };

            StructuredBuffer<uint> visibleList;
            StructuredBuffer<float4x4> trsBuffer;
            float4 _PrimaryCol, _SecondaryCol, _AOColor, _TipColor;
            float _Scale;
            float _MeshDeformationLimitLow;
            float _MeshDeformationLimitTop;
            float4 _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _MinBrightness;
            float _ShadowBrightness;
            float _Cutoff;

            sampler2D _GrassTexture;
            float4 _GrassTexture_ST;

            sampler2D _FlowerTex1;
            float4 _FlowerTex1_ST;
            
            sampler2D _FlowerTex2;
            float4 _FlowerTex2_ST;

            sampler2D _FlowerTex3;
            float4 _FlowerTex3_ST;

            float _NoiseScale;
            float _NoiseScale2;

            float _DistanceTilingOffset;
            float _MaxViewDistance;
            
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

            float GetGrassNoise(float3 worldPos) {
                float2 worldUV = worldPos.xz; 
                float noiseLayer1 = 0;
                float noiseLayer2 = 0;
                Unity_GradientNoise_Deterministic_float(worldUV, _NoiseScale, noiseLayer1);
                Unity_SimpleNoise_float(worldUV, _NoiseScale2, noiseLayer2);
                noiseLayer1 *= noiseLayer2 * 2;
                noiseLayer1 = floor(noiseLayer1 * 5.) / 5;
                return noiseLayer1;
            }

            float3 GetScaleFromMatrix(float4x4 mat) {
                return float3(length(mat._m00_m10_m20), length(mat._m01_m11_m21), length(mat._m02_m12_m22));
            }
            
            VertexOutput vert (VertexInput v, uint instanceID : SV_InstanceID)
            {
                VertexOutput o;

                uint index = visibleList[instanceID];
                float4x4 mat = trsBuffer[index];

                float3 scale = 1;
                //applying transformation matrix
                float3 positionWorldSpace = mul(mat, float4(v.vertex.xyz, 1));
                o.staticWorldPos = positionWorldSpace;

                //move world UVs by time
                float4 worldPos = float4(positionWorldSpace, 1);
                float2 worldUV = worldPos.xz + _WindSpeed * _Time.y; 

                //creating noise from world UVs
                float noise = 0;
                Unity_SimpleNoise_float(worldUV, _WindNoiseScale, noise);
                noise -= .5; 

                //to keep bottom part of mesh at its position
                o.uv = v.uv;
                //the taller the grass the more it should sway 
                float smoothDeformation = smoothstep(_MeshDeformationLimitLow, _MeshDeformationLimitTop, o.uv.y * scale.y + .2);
                float distortion = smoothDeformation * noise;

                //apply distortion
                positionWorldSpace.xz += distortion * _WindStrength * normalize(_WindSpeed);
                o.vertex = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));

                //shadow coords for recieving shadows
                o.shadowCoord = TransformWorldToShadowCoord(positionWorldSpace);

                o.positionWorldSpace = positionWorldSpace;
                return o;
            }

            float4 frag (VertexOutput i) : SV_Target
            {
                float4 texCol = 0;

                //Tiling textures based on distance - can be completley removed if you want
                float distToCam = distance(i.staticWorldPos, _WorldSpaceCameraPos);
                float distRatio = saturate(distToCam / _MaxViewDistance);

                float distRatioTiling = 1. - pow(distRatio, _DistanceTilingOffset);
                float tilingX = clamp(_GrassTexture_ST.x * (distRatioTiling), .3, 2.);
                
                float noise = GetGrassNoise(i.staticWorldPos);

                _GrassTexture_ST.x = tilingX;
                _FlowerTex1_ST.x = tilingX;
                _FlowerTex2_ST.x = tilingX;
                _FlowerTex3_ST.x = tilingX;

                if(noise <= .5) {
                    texCol = tex2D(_GrassTexture, i.uv * _GrassTexture_ST.xy + _GrassTexture_ST.zw);
                } else if (noise > .5 && noise <= .75) {
                    texCol = tex2D(_FlowerTex1, i.uv * _FlowerTex1_ST.xy + _FlowerTex1_ST.zw);
                } else if (noise > .75 && noise <= .9) {
                    texCol = tex2D(_FlowerTex2, i.uv * _FlowerTex2_ST.xy + _FlowerTex2_ST.zw);
                } else {
                    texCol = tex2D(_FlowerTex3, i.uv * _FlowerTex3_ST.xy + _FlowerTex3_ST.zw);
                }

                if (texCol.a < _Cutoff) {
                    discard;
                    return 0;
                }

                // combining noise with color to get differently colored grass patches.
                // also inverting noise so it doesn't match the tex color
                texCol.rgb *= (1. - noise) + _MinBrightness;
               
                float4 grassColor = texCol * _PrimaryCol;
                float4 ao = lerp(_AOColor, 1.0f, i.uv.y);
                grassColor *= ao;


                Light mainLight = GetMainLight(i.shadowCoord); 
                float diffuseLight = clamp(dot(mainLight.direction, i.normal), _MinBrightness, 1.0);
                
                float distanceAtten = mainLight.distanceAttenuation;
                float shadowAtten = saturate(mainLight.shadowAttenuation + _ShadowBrightness);
                float4 lightColor = float4(mainLight.color, 1) * (distanceAtten * shadowAtten);

                lightColor = saturate(lightColor);
                grassColor *= lightColor * diffuseLight;
                Applyfog(grassColor, i.positionWorldSpace);
                return grassColor;

            }
            ENDHLSL
        }

   
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0
            LOD 100
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
			#pragma target 4.5

            #pragma _MAIN_LIGHT_SHADOWS
            #pragma _MAIN_LIGHT_SHADOWS_CASCADE

            #include "UnityCG.cginc"
            #include "noise.hlsl"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 staticWorldPos: TEXCOORD4;
            };

           
            StructuredBuffer<uint> visibleList;
            StructuredBuffer<float4x4> trsBuffer;
            float4 _PrimaryCol, _SecondaryCol, _AOColor, _TipColor;
            float _Scale;
            float _MeshDeformationLimitLow;
            float _MeshDeformationLimitTop;
            float4 _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _MinBrightness;
            float _ShadowBrightness;
            float _Cutoff;

            sampler2D _GrassTexture;
            float4 _GrassTexture_ST;

            sampler2D _FlowerTex1;
            float4 _FlowerTex1_ST;
            
            sampler2D _FlowerTex2;
            float4 _FlowerTex2_ST;

            sampler2D _FlowerTex3;
            float4 _FlowerTex3_ST;

            float _NoiseScale;
            float _NoiseScale2;

            float _DistanceTilingOffset;
            float _MaxViewDistance;
            
            float GetGrassNoise(float3 worldPos) {
                float2 worldUV = worldPos.xz; 
                float noiseLayer1 = 0;
                float noiseLayer2 = 0;
                Unity_GradientNoise_Deterministic_float(worldUV, _NoiseScale, noiseLayer1);
                Unity_SimpleNoise_float(worldUV, _NoiseScale2, noiseLayer2);
                noiseLayer1 *= noiseLayer2 * 2;
                noiseLayer1 = floor(noiseLayer1 * 5.) / 5;
                return noiseLayer1;
            }
           
            VertexOutput vert (VertexInput v, uint instanceID : SV_InstanceID)
            {
                VertexOutput o;

                uint index = visibleList[instanceID];
                float4x4 mat = trsBuffer[index];
                //applying transformation matrix
                float3 positionWorldSpace = mul(mat, float4(v.vertex.xyz, 1));
                o.staticWorldPos = positionWorldSpace;

                //move world UVs by time
                float4 worldPos = float4(positionWorldSpace, 1);
                float2 worldUV = worldPos.xz + _WindSpeed * _Time.y; 

                //creating noise from world UVs
                float noise = 0;
                Unity_SimpleNoise_float(worldUV, _WindNoiseScale, noise);
                noise -= .5;
                
                //to keep bottom part of mesh at its position
                float smoothDeformation = smoothstep(_MeshDeformationLimitLow, _MeshDeformationLimitTop, v.uv.y);
                float distortion = smoothDeformation * noise;

                //apply distortion
                positionWorldSpace.xz += distortion * _WindStrength * normalize(_WindSpeed);
                o.vertex = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));
                o.uv = v.uv;
                return o;
            }

           fixed4 frag (VertexOutput i) : SV_Target
           {    
                float4 texCol = 0;

                float distToCam = distance(i.staticWorldPos, _WorldSpaceCameraPos);
                float distRatio = saturate(distToCam / _MaxViewDistance);

                float distRatioTiling = 1. - pow(distRatio, _DistanceTilingOffset);
                float tilingX = clamp(_GrassTexture_ST.x * (distRatioTiling), .3, 2.);
                
                float noise = GetGrassNoise(i.staticWorldPos);

                _GrassTexture_ST.x = tilingX;
                _FlowerTex1_ST.x = tilingX;
                _FlowerTex2_ST.x = tilingX;
                _FlowerTex3_ST.x = tilingX;

                if(noise <= .5) {
                    texCol = tex2D(_GrassTexture, i.uv * _GrassTexture_ST.xy + _GrassTexture_ST.zw);
                } else if (noise > .5 && noise <= .75) {
                    texCol = tex2D(_FlowerTex1, i.uv * _FlowerTex1_ST.xy + _FlowerTex1_ST.zw);
                } else if (noise > .75 && noise <= .9) {
                    texCol = tex2D(_FlowerTex2, i.uv * _FlowerTex2_ST.xy + _FlowerTex2_ST.zw);
                } else {
                    texCol = tex2D(_FlowerTex3, i.uv * _FlowerTex3_ST.xy + _FlowerTex3_ST.zw);
                }

                if (texCol.a < _Cutoff) {
                    discard;
                    return 0;
                }

                SHADOW_CASTER_FRAGMENT(i);
           }
           ENDHLSL
        }
    }

    Fallback "VertexLix"
}