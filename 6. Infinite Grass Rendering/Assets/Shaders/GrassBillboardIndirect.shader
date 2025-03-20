Shader "Unlit/GrassBlillboardIndirect"
{
    Properties
    {

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

            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma shader_feature _ALPHATEST_ON

            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE
            #pragma require 2darray

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            
            #define GRASS_RENDER_PASS
            
            #include "GrassBillboardPass.hlsl"

            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma multi_compile_instancing 
			#pragma target 4.5

            #define SHADOW_CASTER_PASS

            #define GRASS_RENDER_PASS

            #include "GrassBillboardPass.hlsl"

            ENDHLSL
        }
    }

    Fallback "VertexLix"
}