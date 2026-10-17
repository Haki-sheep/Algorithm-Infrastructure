Shader "RaindropStop/SnowFlake"
{
    Properties
    {
        _SnowColor ("Snow Color", Color) = (1, 1, 1, 0.9)
        _FlakeSize ("Flake Size", Float) = 0.035
        _FrozenGlow ("Frozen Glow", Float) = 0.6
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

            // 与 RainSimulation.compute 的 RainDrop 结构一致
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
                float4 _SnowColor;
                float _FlakeSize;
                float _FrozenGlow;
                float _SplashDuration;
            CBUFFER_END

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                // xy 四边形局部坐标 z 旋转角 w 透明度
                float4 quad : TEXCOORD0;
                // 定格程度
                float frozen : TEXCOORD1;
            };

            static const float2 CornerList[6] = { float2(-1, -1), float2(1, -1), float2(-1, 1), float2(-1, 1), float2(1, -1), float2(1, 1) };

            Varyings Vert(uint vertexId : SV_VertexID)
            {
                RainDrop drop = _DropBuffer[vertexId / 6];
                float2 corner = CornerList[vertexId % 6];
                Varyings output = (Varyings)0;

                float melt = drop.splash >= 0.0 ? saturate(drop.splash / _SplashDuration) : 0.0;
                float size = _FlakeSize * lerp(0.5, 1.5, drop.seed) * (1.0 - melt * 0.5);
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 worldPos = drop.position + (right * corner.x + up * corner.y) * size;

                output.positionCS = TransformWorldToHClip(worldPos);
                // 旋转相位用局部时间 定格时雪花停止自转
                output.quad = float4(corner, drop.localTime * (drop.seed - 0.5) * 3.0, _SnowColor.a * (1.0 - melt));
                output.frozen = 1.0 - smoothstep(0.0, 0.25, drop.timeScale);
                float viewDepth = -TransformWorldToView(worldPos).z;
                output.quad.w *= saturate((viewDepth - 0.3) * 2.0);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.quad.xy;
                float s = sin(input.quad.z);
                float c = cos(input.quad.z);
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);

                float r = length(uv);
                // 六瓣星形 中心软圆叠加花瓣 远看是柔和光点 近看有结晶感
                float angle = atan2(uv.y, uv.x);
                float petal = pow(abs(cos(angle * 3.0)), 6.0);
                float star = smoothstep(0.25 + petal * 0.7, 0.15 + petal * 0.6, r);
                float core = smoothstep(0.45, 0.0, r);
                float shape = saturate(max(star * 0.8, core));
                clip(shape - 0.01);

                float3 color = _SnowColor.rgb * (1.0 + input.frozen * _FrozenGlow);
                return half4(color, shape * input.quad.w);
            }
            ENDHLSL
        }
    }
}
