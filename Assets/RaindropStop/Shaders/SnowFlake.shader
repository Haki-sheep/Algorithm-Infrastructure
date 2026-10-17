Shader "RaindropStop/SnowFlake"
{
    Properties
    {
        _SnowColor ("Snow Color", Color) = (0.98, 0.98, 1.0, 0.92)
        _FlakeSize ("Flake Size (m)", Float) = 0.04
        _FrozenGlow ("Frozen Glow", Float) = 0.35
        _SplashDuration ("Melt Duration", Float) = 0.8
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "SnowFlake"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RainDropCommon.hlsl"
            #include "RainLighting.hlsl"
            #include "NoiseCommon.hlsl"

            StructuredBuffer<RainDrop> _DropBuffer;

            CBUFFER_START(UnityPerMaterial)
                float4 _SnowColor;
                float _FlakeSize;
                float _FrozenGlow;
                float _SplashDuration;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                // xy 四边形局部坐标 z 旋转角 w 透明度
                float4 quad : TEXCOORD1;
                // 定格程度
                float frozen : TEXCOORD2;
            };

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                RainDrop drop = _DropBuffer[vertexId / 6];
                float2 corner = QuadCornerList[vertexId % 6];
                Varyings output = (Varyings)0;

                float melt = drop.splash >= 0.0 ? saturate(drop.splash / _SplashDuration) : 0.0;
                float size = _FlakeSize * lerp(0.5, 1.4, drop.seed) * (1.0 - melt * 0.6);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 worldPos = drop.position + (right * corner.x + up * corner.y) * size;

                output.positionCS = TransformWorldToHClip(worldPos);
                output.positionWS = worldPos;
                // 旋转相位用局部时间 定格时雪花停止自转
                output.quad = float4(corner, drop.localTime * (drop.seed - 0.5) * 1.8, _SnowColor.a * (1.0 - melt));
                output.frozen = 1.0 - smoothstep(0.0, 0.25, drop.timeScale);
                float viewDepth = -TransformWorldToView(worldPos).z;
                output.quad.w *= saturate((viewDepth - 0.3) * 3.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.quad.xy;
                float s = sin(input.quad.z);
                float c = cos(input.quad.z);
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);

                float r = length(uv);
                // 六个小星芒 中心是软圆
                float angle = atan2(uv.y, uv.x);
                float arms = pow(abs(cos(angle * 3.0)), 14.0);
                float hex = cos(angle * 3.0 + 0.524) * 0.4 + 0.65;
                float core = smoothstep(0.35, 0.0, r);
                // 叠加程序化不规则噪声 模拟雪花结晶的凹凸
                float fluff = Fbm(uv * 5.0 + input.quad.z * 0.2, 2);
                float shapeBase = smoothstep(hex + fluff * 0.15, hex - 0.2, r);
                float shape = saturate(max(shapeBase, core) + arms * 0.5);
                clip(shape - 0.015);

                float3 toCamera = normalize(_WorldSpaceCameraPos - input.positionWS);
                // 雪花薄片朝向相机 大 wrap 近似冰晶半透明的背光透射
                float3 lighting = EvaluateDiffuseLighting(input.positionWS, toCamera, 1.5);
                float3 color = _SnowColor.rgb * (lighting + _FrozenGlow * input.frozen);
                return half4(color, shape * input.quad.w);
            }
            ENDHLSL
        }
    }
}
