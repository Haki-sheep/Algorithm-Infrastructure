// 卡通二阶光照 + Smear Frame 背向运动方向的顶点沿反方向拉伸 噪声打碎拖尾
Shader "HitImpact/SmearToon"
{
    Properties
    {
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Shade Color", Color) = (0.55, 0.6, 0.8, 1)
        _ShadeStep ("Shade Step", Range(-1, 1)) = 0.1
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(1, 8)) = 4
        _SmearNoiseScale ("Smear Noise Scale", Float) = 9
        _SmearBreakup ("Smear Breakup", Range(0, 1)) = 0.65
        // 由 SmearMotion 通过 MaterialPropertyBlock 写入 世界空间 方向为运动方向 长度为拖尾长度
        [HideInInspector] _SmearVector ("Smear Vector", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Tags { "LightMode" = "ForwardBase" }
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"

            fixed4 _Color;
            fixed4 _ShadeColor;
            float _ShadeStep;
            fixed4 _RimColor;
            float _RimPower;
            float _SmearNoiseScale;
            float _SmearBreakup;
            float4 _SmearVector;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                SHADOW_COORDS(2)
            };

            float Hash13(float3 p)
            {
                p = frac(p * 0.1031);
                p += dot(p, p.zyx + 31.32);
                return frac((p.x + p.y) * p.z);
            }

            v2f vert(appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = UnityObjectToWorldNormal(v.normal);

                float smearLength = length(_SmearVector.xyz);
                if (smearLength > 1e-4)
                {
                    float3 moveDir = _SmearVector.xyz / smearLength;
                    // 只拉伸背向运动方向的半球 正面保持形体 背面拖出残影
                    float trailing = saturate(-dot(worldNormal, moveDir));
                    // 按顶点位置量化取随机 让拖尾长短不一 形成手绘 Smear 的毛刺感
                    float noise = Hash13(floor(v.vertex.xyz * _SmearNoiseScale));
                    float stretch = trailing * lerp(1, noise, _SmearBreakup);
                    worldPos -= moveDir * smearLength * stretch;
                }

                o.worldPos = worldPos;
                o.worldNormal = worldNormal;
                o.pos = UnityWorldToClipPos(worldPos);
                TRANSFER_SHADOW(o);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                float3 normal = normalize(i.worldNormal) * (facing > 0 ? 1 : -1);
                float3 lightDir = normalize(UnityWorldSpaceLightDir(i.worldPos));
                float3 viewDir = normalize(UnityWorldSpaceViewDir(i.worldPos));

                UNITY_LIGHT_ATTENUATION(atten, i, i.worldPos);
                float ndl = dot(normal, lightDir) * atten;
                float lit = smoothstep(_ShadeStep - 0.02, _ShadeStep + 0.02, ndl);
                fixed3 color = lerp(_ShadeColor.rgb * _Color.rgb, _Color.rgb, lit) * _LightColor0.rgb;

                float rim = pow(1 - saturate(dot(normal, viewDir)), _RimPower);
                color += _RimColor.rgb * step(0.5, rim) * 0.35;
                return fixed4(color, 1);
            }
            ENDCG
        }
    }
    Fallback "VertexLit"
}
