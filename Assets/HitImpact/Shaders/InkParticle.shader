// 水墨粒子 顶点色 alpha 作为溶解阈值 噪声沿 U 方向滚动形成干笔飞白
Shader "HitImpact/InkParticle"
{
    Properties
    {
        _MainTex ("Shape (A)", 2D) = "white" {}
        _NoiseTex ("Erosion Noise", 2D) = "gray" {}
        _NoiseScale ("Noise Scale (XY) Scroll (ZW)", Vector) = (1, 1, 0, 0)
        _Softness ("Edge Softness", Range(0.01, 0.5)) = 0.08
        _ErosionBias ("Erosion Bias", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _NoiseTex;
            float4 _NoiseScale;
            float _Softness;
            float _ErosionBias;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                // xy 贴图 UV zw 每粒子随机数 来自 Custom Vertex Streams 的 StableRandom.xy
                float4 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 noiseUV : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = TRANSFORM_TEX(v.uv.xy, _MainTex);
                o.noiseUV = v.uv.xy * _NoiseScale.xy + v.uv.zw * 7.31 + _NoiseScale.zw * _Time.y;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float shape = tex2D(_MainTex, i.uv).a;
                float noise = tex2D(_NoiseTex, i.noiseUV).r;
                // alpha 从 1 降到 0 时阈值升高 墨迹由细碎处先断开 而不是整体变透明
                float threshold = (1 - i.color.a) * (1 + _ErosionBias);
                float erosion = shape * (1 - _ErosionBias + noise * _ErosionBias * 2) - threshold;
                float alpha = saturate(erosion / _Softness) * step(0.001, shape);
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
