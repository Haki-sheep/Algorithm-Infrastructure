Shader "RaindropStop/WetGround"
{
    Properties
    {
        _TileColorA ("Tile Color A", Color) = (0.42, 0.40, 0.38, 1)
        _TileColorB ("Tile Color B", Color) = (0.48, 0.24, 0.20, 1)
        _GroutColor ("Grout Color", Color) = (0.12, 0.12, 0.12, 1)
        _TileSize ("Tile Size (m)", Vector) = (0.6, 0.3, 0, 0)
        _RedTileRatio ("Red Tile Ratio", Range(0, 1)) = 0.18
        _PuddleCoverage ("Puddle Coverage", Range(0, 1)) = 0.45
        _Wetness ("Wetness", Range(0, 1)) = 1
        _RippleDensity ("Ripple Cells per m", Float) = 6
        _RippleStrength ("Ripple Normal Strength", Float) = 0.35
        _NebulaIntensity ("Time Stop Nebula Intensity", Float) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _TileColorA;
            float4 _TileColorB;
            float4 _GroutColor;
            float4 _TileSize;
            float _RedTileRatio;
            float _PuddleCoverage;
            float _Wetness;
            float _RippleDensity;
            float _RippleStrength;
            float _NebulaIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "NoiseCommon.hlsl"
            #include "LocalTimeField.hlsl"
            #include "WetGroundSurface.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                GroundSurface surface = EvaluateGroundSurface(input.positionWS);
                float3 color = ShadeGround(surface, input.positionWS, input.screenPos);
                color = MixFog(color, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            float4 DepthVert(float4 positionOS : POSITION) : SV_POSITION
            {
                return TransformObjectToHClip(positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
