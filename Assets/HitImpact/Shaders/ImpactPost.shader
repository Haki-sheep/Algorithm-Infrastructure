// 冲击帧全屏后处理 径向缩放模糊 + 保留蓝色的高对比黑白 + 反色闪帧
Shader "HitImpact/ImpactPost"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _NoiseTex ("Jitter Noise", 2D) = "gray" {}
        _Center ("Blur Center UV", Vector) = (0.5, 0.5, 0, 0)
        _BlurStrength ("Blur Strength", Range(0, 0.6)) = 0
        _BlurInnerRadius ("Blur Inner Radius", Range(0, 0.5)) = 0.05
        _Mono ("Mono Amount", Range(0, 1)) = 0
        _Contrast ("Mono Contrast", Range(1, 6)) = 2.6
        _KeepColor ("Keep Color", Color) = (0.15, 0.3, 1, 1)
        _KeepThreshold ("Keep Threshold", Range(0, 1)) = 0.18
        _Invert ("Invert", Range(0, 1)) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define SAMPLE_COUNT 16

            sampler2D _MainTex;
            sampler2D _NoiseTex;
            float4 _NoiseTex_TexelSize;
            float4 _MainTex_TexelSize;
            float4 _Center;
            float _BlurStrength;
            float _BlurInnerRadius;
            float _Mono;
            float _Contrast;
            float4 _KeepColor;
            float _KeepThreshold;
            float _Invert;

            float3 RadialBlur(float2 uv)
            {
                float aspect = _MainTex_TexelSize.w / _MainTex_TexelSize.z;
                float2 offset = uv - _Center.xy;
                float2 aspectOffset = float2(offset.x, offset.y * aspect);
                // 中心一小圈保持清晰 越往外拖影越长 符合朝镜头冲刺的透视感
                float falloff = smoothstep(_BlurInnerRadius, _BlurInnerRadius + 0.35, length(aspectOffset));
                float strength = _BlurStrength * falloff;

                // 每像素随机起始偏移 用少量采样换掉条带感
                float2 noiseUV = uv * _MainTex_TexelSize.zw * _NoiseTex_TexelSize.xy;
                float jitter = tex2D(_NoiseTex, noiseUV).r;

                float3 sum = 0;
                [unroll]
                for (int i = 0; i < SAMPLE_COUNT; i++)
                {
                    float t = (i + jitter) / SAMPLE_COUNT;
                    sum += tex2D(_MainTex, _Center.xy + offset * (1 - strength * t)).rgb;
                }
                return sum / SAMPLE_COUNT;
            }

            float3 MonoKeepColor(float3 color)
            {
                float luma = dot(color, float3(0.299, 0.587, 0.114));
                float inked = saturate((luma - 0.5) * _Contrast + 0.5);

                // 按色度与保留色的方向相似度判定是否保留原色 用于黑白画面里只留角色主色
                float3 chroma = color - luma;
                float3 keepChroma = normalize(_KeepColor.rgb - dot(_KeepColor.rgb, float3(0.299, 0.587, 0.114)) + 1e-4);
                float similarity = dot(normalize(chroma + 1e-4), keepChroma) * saturate(length(chroma) * 4);
                float keep = smoothstep(_KeepThreshold, _KeepThreshold + 0.25, similarity);

                float3 mono = lerp(inked.xxx, color, keep);
                return lerp(color, mono, _Mono);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float3 color = _BlurStrength > 0.0001 ? RadialBlur(i.uv) : tex2D(_MainTex, i.uv).rgb;
                color = MonoKeepColor(color);
                color = lerp(color, 1 - color, _Invert);
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
