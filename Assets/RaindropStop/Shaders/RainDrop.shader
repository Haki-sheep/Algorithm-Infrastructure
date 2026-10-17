Shader "RaindropStop/RainDrop"
{
    Properties
    {
        _RainColor ("Rain Tint", Color) = (0.85, 0.9, 1.0, 1.0)
        _Opacity ("Streak Opacity", Range(0, 1)) = 0.8
        _StreakMinLight ("Streak Min Light", Float) = 0.08
        _StreakBackgroundBoost ("Streak Background Boost", Float) = 1.25
        _FrozenScale ("Frozen Drop Visual Scale", Float) = 3
        _BeadMinPixels ("Frozen Drop Min Radius (px)", Float) = 2.5
        _BeadThreshold ("Bead Time Scale Threshold", Range(0.01, 1)) = 0.9
        _BeadRim ("Frozen Drop Rim", Float) = 0.6
        _Refraction ("Frozen Refraction (uv)", Float) = 0.012
        _SpecularIntensity ("Frozen Specular", Float) = 6
        _MotionBlurShutter ("Shutter (s)", Float) = 0.04
        _SplashDuration ("Splash Duration", Float) = 0.35
        _SplashSpeed ("Splash Speed (m/s)", Float) = 1.6
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "RainDrop"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "RainDropCommon.hlsl"
            #include "RainLighting.hlsl"

            // 与 GpuRainSystem.SplashDropletCount 保持一致 每滴雨 1 个主体加若干溅射小水珠
            #define SPLASH_DROPLET_COUNT 6

            StructuredBuffer<RainDrop> _DropBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _RainColor;
                float _Opacity;
                float _StreakMinLight;
                float _StreakBackgroundBoost;
                float _FrozenScale;
                float _BeadMinPixels;
                float _BeadThreshold;
                float _BeadRim;
                float _Refraction;
                float _SpecularIntensity;
                float _MotionBlurShutter;
                float _SplashDuration;
                float _SplashSpeed;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // xy 四边形局部坐标 z 长宽比
                float3 quad : TEXCOORD1;
                // x 定格程度 y 能量占比 z 整体透明度
                float3 state : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            // 输入溅射局部时间 种子 序号 撞击点 输出该溅射水珠位置与速度 解析抛物线 定格时随局部时间一起停住
            void EvaluateSplashDroplet(float t, float seed, uint index, float3 origin, float strength, out float3 position, out float3 velocity)
            {
                float h = frac(seed * 91.7 + index * 0.618);
                float yaw = (index + h * 0.8) * (6.2832 / SPLASH_DROPLET_COUNT) + seed * 6.2832;
                float elevation = lerp(0.6, 1.25, frac(h * 7.13));
                float speed = _SplashSpeed * strength * lerp(0.55, 1.2, frac(h * 3.71));
                float3 v0 = float3(cos(yaw) * cos(elevation), sin(elevation), sin(yaw) * cos(elevation)) * speed;
                position = origin + v0 * t + float3(0, -4.905 * t * t, 0);
                velocity = v0 + float3(0, -9.81 * t, 0);
            }

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                const uint quadsPerDrop = SPLASH_DROPLET_COUNT + 1;
                uint quadId = vertexId / 6;
                RainDrop drop = _DropBuffer[quadId / quadsPerDrop];
                uint part = quadId % quadsPerDrop;
                float2 corner = QuadCornerList[vertexId % 6];
                Varyings output = (Varyings)0;

                float diameter = DropDiameterMm(drop.seed);
                float radius = diameter * 0.0005;
                float3 center = drop.position;
                float3 velocity = drop.velocity;
                float visible = 1.0;

                if (drop.splash >= 0.0)
                {
                    // 落地后主体消失 小雨滴溅射少且弱
                    float strength = saturate(diameter / 3.0);
                    uint activeCount = (uint)ceil(strength * SPLASH_DROPLET_COUNT);
                    visible = (part > 0 && part <= activeCount) ? 1.0 : 0.0;
                    EvaluateSplashDroplet(drop.splash, drop.seed, part, drop.position, strength, center, velocity);
                    radius *= lerp(0.25, 0.45, frac(drop.seed * 13.1 + part * 0.37));
                    visible *= step(drop.position.y - 0.002, center.y) * (1.0 - smoothstep(0.6, 1.0, drop.splash / _SplashDuration));
                }
                else
                {
                    visible = part == 0 ? 1.0 : 0.0;
                }

                if (visible <= 0.0)
                {
                    output.positionCS = float4(0, 0, -2, 1);
                    return output;
                }

                velocity *= drop.timeScale;
                // 按所在区域流速切换形态而非残余速度 否则区域边缘慢速段仍是短雨线 阈值以下一律为水珠
                float frozen = smoothstep(_BeadThreshold, _BeadThreshold * 0.6, drop.timeScale);
                // 真实雨滴 1 到 4 毫米 定格后在屏幕上不足一像素 放大以保证可读
                radius *= lerp(1.0, _FrozenScale, frozen);
                float shutter = _MotionBlurShutter * (1.0 - frozen);

                StreakQuad quad = BuildStreakQuad(center, velocity, radius, shutter, corner, lerp(0.75, _BeadMinPixels, frozen));
                output.positionCS = TransformWorldToHClip(quad.positionWS);
                output.positionWS = quad.positionWS;
                output.quad = quad.quad;
                output.state = float3(frozen, quad.coverage, visible);
                output.screenPos = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 capsule;
                float dist = CapsuleDistance(input.quad.xy, input.quad.z, capsule);
                clip(1.0 - dist);
                float frozen = input.state.x;

                // 视空间球面法线 z 朝向相机
                float3 viewNormal = float3(capsule, sqrt(saturate(1.0 - dot(capsule, capsule))));
                float3 worldNormal = normalize(UNITY_MATRIX_V[0].xyz * viewNormal.x + UNITY_MATRIX_V[1].xyz * viewNormal.y + UNITY_MATRIX_V[2].xyz * viewNormal.z);
                float3 toCamera = normalize(_WorldSpaceCameraPos - input.positionWS);
                float fresnel = 0.02 + 0.98 * pow(saturate(1.0 - viewNormal.z), 5.0);

                float3 lighting = EvaluateRainLighting(input.positionWS, toCamera, lerp(2.5, 1.2, frozen), lerp(0.4, 0.2, frozen));
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                float3 refracted = SampleSceneColor(screenUV - viewNormal.xy * _Refraction);

                // 纯光源驱动在夜景暗处整片不可见 叠加最低亮度与背景提亮 雨丝在暗背景显灰白 在亮橱窗前显亮丝
                float3 streakColor = _RainColor.rgb * (lighting + _StreakMinLight) + refracted * _StreakBackgroundBoost;
                float3 specular = 0;
                for (int i = 0; i < _RainLightCount; i++)
                {
                    float3 toLight;
                    float3 irradiance = RainLightIrradiance(i, input.positionWS, toLight);
                    float3 halfDir = normalize(toLight + toCamera);
                    specular += irradiance * pow(saturate(dot(worldNormal, halfDir)), 220.0);
                }
                // 暗背景下折射色与背景相同 水珠只剩高光 加环境亮边勾出轮廓
                float3 rim = _RainColor.rgb * (lighting * 0.35 + _StreakMinLight + _RainAmbient.rgb * 2.0) * (1.0 + _BeadRim * 4.0);
                float3 beadColor = refracted * (1.0 - fresnel) * 1.15 + rim * fresnel + specular * _SpecularIntensity;

                float edge = smoothstep(1.0, 0.75, dist);
                float streakAlpha = _Opacity * input.state.y * edge;
                float beadAlpha = edge;
                float alpha = lerp(streakAlpha, beadAlpha, frozen) * input.state.z;
                // 预乘 拖尾能量按覆盖率缩放 水珠用折射色完全替换背景
                float3 color = lerp(streakColor * streakAlpha, beadColor * beadAlpha, frozen) * input.state.z;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
