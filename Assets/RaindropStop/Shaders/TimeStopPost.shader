Shader "Hidden/RaindropStop/TimeStopPost"
{
    Properties
    {
        _Desaturate ("Desaturate", Range(0, 1)) = 0.85
        _Tint ("Inside Tint", Color) = (0.72, 0.86, 1.0, 1)
        _EdgeColor ("Edge Color", Color) = (0.55, 0.85, 1.0, 1)
        _EdgeWidth ("Edge Width (m)", Float) = 0.35
        _Distortion ("Edge Distortion", Float) = 0.025
        _Chromatic ("Edge Chromatic", Float) = 0.006
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off

        Pass
        {
            Name "TimeStopPost"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "LocalTimeField.hlsl"

            float _Desaturate;
            float4 _Tint;
            float4 _EdgeColor;
            float _EdgeWidth;
            float _Distortion;
            float _Chromatic;

            // 输入像素世界坐标 输出 x 区域内程度 y 球面边界带强度 z 冲击波强度
            float3 EvaluateZone(float3 cameraPos, float3 rayDir, float sceneDistance, float3 worldPos)
            {
                float inside = 0.0;
                float edge = 0.0;
                float wave = 0.0;
                for (int i = 0; i < _TimeFieldCount; i++)
                {
                    float3 center = _TimeFieldCenterRadius[i].xyz;
                    float radius = _TimeFieldCenterRadius[i].w;
                    float intensity = _TimeFieldParam[i].z;

                    float surfaceDist = distance(worldPos, center);
                    float t = saturate((radius - surfaceDist) / _TimeFieldParam[i].x);
                    inside = max(inside, t * t * (3.0 - 2.0 * t));

                    // 视线与球面求交 被场景遮挡的球面部分不显示边界
                    float3 toCenter = center - cameraPos;
                    float along = dot(toCenter, rayDir);
                    float closest = length(toCenter - rayDir * along);
                    float rim = 1.0 - saturate(abs(closest - radius) / _EdgeWidth);
                    float halfChord = sqrt(max(radius * radius - closest * closest, 0.0));
                    float hitNear = along - halfChord;
                    float visible = step(hitNear, sceneDistance) * step(0.0, along + halfChord);
                    // 球体轮廓用 closest 求 面向相机那面用 surfaceDist 求 两者取大 地面与墙面也能看到切线环
                    float surfaceRim = 1.0 - saturate(abs(surfaceDist - radius) / _EdgeWidth);
                    float edgeStrength = max(rim * visible, surfaceRim) * intensity;
                    edge = max(edge, edgeStrength);
                    wave = max(wave, edgeStrength * _TimeFieldParam[i].w);
                }
                return float3(inside, edge, wave);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                float rawDepth = SampleSceneDepth(uv);
                float3 worldPos = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 rayDir = normalize(worldPos - cameraPos);
                float sceneDistance = distance(worldPos, cameraPos);
                float3 zone = EvaluateZone(cameraPos, rayDir, sceneDistance, worldPos);

                // 边界折射与色散 边界带越强偏移越大 冲击波期间加倍
                float2 fromCenter = uv - 0.5;
                float2 offsetDir = normalize(fromCenter + 1e-5);
                float distortAmount = zone.y * (1.0 + zone.z * 2.0);
                float2 offset = offsetDir * _Distortion * distortAmount * sin(zone.y * 3.14159);
                float2 chroma = offsetDir * _Chromatic * distortAmount;
                half r = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset + chroma).r;
                half g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset).g;
                half b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + offset - chroma).b;
                half3 color = half3(r, g, b);

                half luma = dot(color, half3(0.299, 0.587, 0.114));
                half3 graded = lerp(color, luma.xxx, _Desaturate) * _Tint.rgb;
                color = lerp(color, graded, zone.x);
                color += _EdgeColor.rgb * pow(zone.y, 3.0) * (0.6 + zone.z * 1.5);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
