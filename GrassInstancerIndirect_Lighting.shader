Shader "Unlit/GrassBladeIndirect"
{
    Properties
    {
        _MainTex ("Main Tex", 2D) = "white" {}
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
        _ShadowBrightness ("Shadow brightness", float) = 0.3
        _ShadowColor ("Shadow color",  Color) = (1, 1, 1)
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
            };

            StructuredBuffer<float4x4> trsBuffer;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _PrimaryCol, _SecondaryCol, _AOColor, _TipColor;
            float _Scale;
            float _MeshDeformationLimitLow;
            float _MeshDeformationLimitTop;
            float4 _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _MinBrightness;
            float _ShadowBrightness;
            float4 _ShadowColor;
            float _RecieveShadow;

            VertexOutput vert (VertexInput v, uint instanceID : SV_InstanceID)
            {
                VertexOutput o;

                //applying transformation matrix
                float3 positionWorldSpace = mul(trsBuffer[instanceID], float4(v.vertex.xyz, 1));

                //move world UVs by time
                float4 worldPos = float4(positionWorldSpace, 1);
                float2 worldUV = worldPos.xz + _WindSpeed * _Time.y; 

                //creating noise from world UVs
                float noise = 0;
                Unity_SimpleNoise_float(worldUV, _WindNoiseScale, noise);
                noise -= .5;

                //to keep bottom part of mesh at its position
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float smoothDeformation = smoothstep(_MeshDeformationLimitLow, _MeshDeformationLimitTop, o.uv.y);
                float distortion = smoothDeformation * noise;

                //apply distortion
                positionWorldSpace.xz += distortion * _WindStrength * normalize(_WindSpeed);
                o.vertex = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));

                //shadow coords for recieving shadows
                o.shadowCoord = TransformWorldToShadowCoord(positionWorldSpace);

                //getting normal for lighting calc
                VertexNormalInputs normal = GetVertexNormalInputs(v.vertex);
                o.normal = normal.normalWS; //normalize(mul(v.normal, (float3x3)UNITY_MATRIX_I_M));
                return o;
            }

            float4 frag (VertexOutput i) : SV_Target
            {
    
                //from https://github.com/GarrettGunnell/Grass/blob/main/Assets/Shaders/ModelGrass.shader
                float4 col = lerp(_PrimaryCol, _SecondaryCol, i.uv.y);
                float4 ao = lerp(_AOColor, 1.0f, i.uv.y);
                float4 tip = lerp(0.0f, _TipColor, i.uv.y * i.uv.y * (1.0f + _Scale));
                float4 grassColor = (col + tip)  * ao;

                Light mainLight = GetMainLight(i.shadowCoord); 
                float lightStrength = clamp(dot(mainLight.direction, i.normal), _MinBrightness, 1.0);
               
                if (_RecieveShadow > 0) {
                    float distanceAtten = mainLight.distanceAttenuation;
                    float shadowAtten = mainLight.shadowAttenuation + _ShadowBrightness;
                    float4 lightColor = float4(mainLight.color, 1) * (distanceAtten * shadowAtten) * _ShadowColor;

                    lightColor = saturate(lightColor);
                    return grassColor * lightColor * lightStrength;
                }

                return grassColor * lightStrength;               
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

            #include "UnityCG.cginc"
            #include "noise.hlsl"

            struct VertexInput
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct VertexOutput
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            StructuredBuffer<float4x4> trsBuffer;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _MeshDeformationLimit;
            float4 _WindSpeed;
            float _WindStrength;
            float _WindNoiseScale;
            float _MeshDeformationLimitLow;
            float _MeshDeformationLimitTop;

           
            VertexOutput vert (VertexInput v, uint instanceID : SV_InstanceID)
            {
                VertexOutput o;

                //applying transformation matrix
                float3 positionWorldSpace = mul(trsBuffer[instanceID], float4(v.vertex.xyz, 1));

                //move world UVs by time
                float4 worldPos = float4(positionWorldSpace, 1);
                float2 worldUV = worldPos.xz + _WindSpeed * _Time.y; 

                //creating noise from world UVs
                float noise = 0;
                Unity_SimpleNoise_float(worldUV, _WindNoiseScale, noise);
                noise -= .5;
                
                //to keep bottom part of mesh at its position
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                float smoothDeformation = smoothstep(_MeshDeformationLimitLow, _MeshDeformationLimitTop, o.uv.y);
                float distortion = smoothDeformation * noise;

                //apply distortion
                positionWorldSpace.xz += distortion * _WindStrength * normalize(_WindSpeed);
                o.vertex = mul(UNITY_MATRIX_VP, float4(positionWorldSpace, 1));
                return o;
            }

           fixed4 frag (VertexOutput i) : SV_Target
           {
                SHADOW_CASTER_FRAGMENT(i);
           }
           ENDHLSL
       }
    }
}