Shader "RaindropStop/RainDrop"
{
    Properties
    {
        _RainColor ("Rain Color", Color) = (0.78, 0.84, 0.95, 0.32)
        _StreakLength ("Streak Length (m at full speed)", Float) = 0.55
        _StreakWidth ("Streak Width", Float) = 0.008
        _DropRadius ("Frozen Drop Radius", Float) = 0.022
        _Refraction ("Frozen Drop Refraction", Float) = 0.035
        _SplashRadius ("Splash Radius", Float) = 0.16
        _SplashAlpha ("Splash Alpha", Float) = 0.5
        _SplashDuration ("Splash Duration", Float) = 0.28
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RainDrop"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct RainDrop
            {
                float3 position;
                float splash;
                float3 velocity;
                float timeScale;
                float seed;
                float localTime;
                float2 padding;
            };

            StructuredBuffer<RainDrop> _DropBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float _StreakLength;
                float _StreakWidth;
                float _DropRadius;
                float _Refraction;
                float _SplashRadius;
                float _SplashAlpha;
                float _SplashDuration;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // xy 四边形局部坐标 -1 到 1 z 长宽比 w 0 下落 1 溅射
                float4 quad : TEXCOORD0;
                // x 定格程度 y 溅射进度 z 透明度
                float3 state : TEXCOORD1;
                // xy 横向轴视空间方向 zw 纵向轴视空间方向 用于折射偏移
                float4 viewAxis : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            static const float2 CornerList[6] = { float2(-1, -1), float2(1, -1), float2(-1, 1), float2(-1, 1), float2(1, -1), float2(1, 1) };

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                RainDrop drop = _DropBuffer[vertexId / 6];
                float2 corner = CornerList[vertexId % 6];
                Varyings output = (Varyings)0;

                float3 worldPos;
                if (drop.splash >= 0.0)
                {
                    float progress = saturate(drop.splash / _SplashDuration);
                    float radius = lerp(0.02, _SplashRadius, sqrt(progress)) * lerp(0.7, 1.3, drop.seed);
                    worldPos = drop.position + float3(corner.x, 0, corner.y) * radius;
                    output.quad = float4(corner, 1, 1);
                    output.state = float3(1.0 - drop.timeScale, progress, _SplashAlpha * (1.0 - progress));
                }
                else
                {
                    float3 toCamera = normalize(_WorldSpaceCameraPos - drop.position);
                    float3 axis = normalize(drop.velocity);
                    float3 side = cross(axis, toCamera);
                    // 俯视雨滴时轴向与视线平行 退化为相机右方向
                    side = dot(side, side) < 1e-5 ? UNITY_MATRIX_V[0].xyz : normalize(side);

                    float frozen = 1.0 - smoothstep(0.0, 0.25, drop.timeScale);
                    float halfWidth = lerp(_StreakWidth, _DropRadius, frozen) * lerp(0.75, 1.25, drop.seed);
                    // 拖尾长度正比于局部速度 定格时退化成圆形水珠
                    float halfLength = max(_StreakLength * 0.5 * drop.timeScale * length(drop.velocity) / 9.0, halfWidth);

                    worldPos = drop.position + side * corner.x * halfWidth - axis * corner.y * halfLength;
                    output.quad = float4(corner, halfLength / halfWidth, 0);
                    output.state = float3(frozen, 0, _RainColor.a);
                    output.viewAxis = float4(mul((float3x3)UNITY_MATRIX_V, side).xy, mul((float3x3)UNITY_MATRIX_V, -axis).xy);
                }

                output.positionCS = TransformWorldToHClip(worldPos);
                output.screenPos = ComputeScreenPos(output.positionCS);
                float viewDepth = -TransformWorldToView(worldPos).z;
                output.state.z *= saturate((viewDepth - 0.3) * 2.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 quad = input.quad.xy;
                float alpha = input.state.z;

                if (input.quad.w > 0.5)
                {
                    float d = length(quad);
                    float ring = smoothstep(0.65, 0.85, d) * (1.0 - smoothstep(0.88, 1.0, d));
                    return half4(_RainColor.rgb, ring * alpha);
                }

                // 胶囊 SDF 统一拖尾与水珠形状 以横向半宽为单位
                float aspect = input.quad.z;
                float2 local = float2(quad.x, quad.y * aspect);
                // 保留 y 符号 否则上下半球法线镜像 高光会出现两份
                float2 capsule = float2(local.x, sign(local.y) * max(abs(local.y) - (aspect - 1.0), 0.0));
                float d = length(capsule);
                clip(1.0 - d);

                float frozen = input.state.x;
                float headFade = lerp(saturate(1.0 - quad.y) * 0.5 + 0.5, 1.0, frozen);
                float streakAlpha = (1.0 - d * d) * headFade * alpha;

                float3 normal = float3(capsule, sqrt(saturate(1.0 - d * d)));
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                // 真实水珠是凸透镜 背景倒像 偏移方向取法线反向
                float2 offset = -(normal.x * input.viewAxis.xy + normal.y * input.viewAxis.zw) * _Refraction;
                float3 refracted = SampleSceneColor(screenUV + offset);
                float rim = pow(1.0 - normal.z, 3.0);
                float highlight = smoothstep(0.82, 0.97, dot(normal, normalize(float3(-0.4, 0.5, 0.75))));
                float3 dropColor = refracted * 1.05 + rim * 0.55 + highlight * 1.6;
                float dropAlpha = saturate(0.85 + highlight) * smoothstep(1.0, 0.85, d);

                float3 color = lerp(_RainColor.rgb, dropColor, frozen);
                return half4(color, lerp(streakAlpha, dropAlpha, frozen));
            }
            ENDHLSL
        }
    }
}
