#ifndef RAINDROP_STOP_WET_GROUND_SURFACE_INCLUDED
#define RAINDROP_STOP_WET_GROUND_SURFACE_INCLUDED

// 依赖 WetGround.shader 的 UnityPerMaterial 与 Lighting.hlsl NoiseCommon.hlsl LocalTimeField.hlsl

Texture2D<float> _LocalTimeTex;
SamplerState point_clamp_sampler;
TEXTURE2D(_PlanarReflectionTex);
SAMPLER(sampler_PlanarReflectionTex);
float4 _EnvironmentRect;
float _RainIntensity;
float _PlanarReflectionValid;

struct GroundSurface
{
    float3 albedo;
    float3 normal;
    float smoothness;
    float puddle;
    // 时停星云强度 0 到 1
    float nebula;
};

// 输入世界 xz 输出该处地面累计局部时间 秒 区域外按正常流速累计
float SampleLocalTime(float2 worldXZ)
{
    float2 uv = (worldXZ - _EnvironmentRect.xy) / _EnvironmentRect.zw;
    return _LocalTimeTex.SampleLevel(point_clamp_sampler, saturate(uv), 0);
}

// 输入世界 xz 与局部时间 输出涟漪高度梯度 xz
// 每格每秒随机一次落点 圆环半径随寿命扩散 振幅衰减 周期 1 秒 与 LocalTimeWrap 64 整除
float2 RippleGradient(float2 worldXZ, float localTime)
{
    float2 cell = worldXZ * _RippleDensity;
    float2 cellId = floor(cell);
    float2 gradient = 0;
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            float2 id = cellId + float2(x, y);
            float2 rnd = Hash22(id);
            float phaseTime = localTime + rnd.x;
            float life = frac(phaseTime);
            // 每个周期落点重新随机 雨量低时部分周期不落雨
            float2 cycleRnd = Hash22(id + floor(phaseTime) * 7.31);
            if (cycleRnd.y > _RainIntensity)
            {
                continue;
            }
            float2 center = id + 0.15 + cycleRnd * 0.7;
            float2 offset = cell - center;
            float dist = length(offset);
            float radius = life * 0.85;
            float band = dist - radius;
            // 高斯包络乘余弦 得到 2 到 3 圈逐渐变弱的同心波纹
            float envelope = exp(-band * band * 60.0) * (1.0 - life) * (1.0 - life);
            float wave = sin(band * 40.0) * envelope;
            gradient += offset / max(dist, 1e-4) * wave;
        }
    }
    return gradient;
}

GroundSurface EvaluateGroundSurface(float3 positionWS)
{
    GroundSurface result;
    float2 xz = positionWS.xz;

    // 错缝地砖 奇数行偏移半块
    float2 tileUV = xz / _TileSize.xy;
    tileUV.x += fmod(floor(tileUV.y), 2.0) * 0.5;
    float2 tileId = floor(tileUV);
    float2 tileFrac = frac(tileUV);
    float2 groutDist = min(tileFrac, 1.0 - tileFrac) * _TileSize.xy;
    float groutMask = smoothstep(0.004, 0.012, min(groutDist.x, groutDist.y));
    float tileRnd = Hash21(tileId);
    float3 tileColor = tileRnd < _RedTileRatio ? _TileColorB.rgb : _TileColorA.rgb;
    tileColor *= lerp(0.75, 1.15, Hash21(tileId + 3.7)) * lerp(0.85, 1.1, Fbm(xz * 6.0, 3));
    float3 dryAlbedo = lerp(_GroutColor.rgb, tileColor, groutMask);

    // 积水 低频噪声决定水洼 砖缝先积水
    float puddleNoise = Fbm(xz * 0.35 + 5.1, 4);
    float puddle = smoothstep(1.0 - _PuddleCoverage, 1.0 - _PuddleCoverage + 0.08, puddleNoise);
    puddle = max(puddle, (1.0 - groutMask) * 0.6) * _Wetness;

    // 湿表面吸水变暗 积水处几乎全黑 反射主导
    result.albedo = dryAlbedo * lerp(1.0, 0.45, _Wetness) * lerp(1.0, 0.6, puddle);
    float microRough = Fbm(xz * 14.0, 2);
    result.smoothness = lerp(lerp(0.35, 0.78, _Wetness) - microRough * 0.15, 0.97, puddle);
    result.puddle = puddle;

    // 涟漪只出现在积水上 时停内局部时间不增长 涟漪保持定格
    float localTime = SampleLocalTime(xz);
    float2 ripple = RippleGradient(xz, localTime) * _RippleStrength * puddle;
    float3 microNormal = float3((Fbm(xz * 9.0, 2) - 0.5) * 0.25, 1, (Fbm(xz * 9.0 + 3.3, 2) - 0.5) * 0.25);
    result.normal = normalize(lerp(microNormal, float3(-ripple.x, 1, -ripple.y), puddle));

    result.nebula = 0;
    for (int i = 0; i < _TimeFieldCount; i++)
    {
        float dist = distance(xz, _TimeFieldCenterRadius[i].xz);
        float radius = _TimeFieldCenterRadius[i].w;
        float t = saturate((radius - dist) / max(_TimeFieldParam[i].x, 0.01));
        result.nebula = max(result.nebula, t * t * (3.0 - 2.0 * t));
    }
    return result;
}

// 输入地面坐标与强度 输出叠加在地面上的星云光雾 区域内缓慢流动 不受时停影响
float3 NebulaOverlay(float2 xz, float intensity)
{
    if (intensity <= 0.001)
    {
        return 0;
    }
    float time = _Time.y * 0.05;
    float2 warp = float2(Fbm(xz * 0.18 + time, 4), Fbm(xz * 0.18 - time + 9.2, 4));
    float cloud = Fbm(xz * 0.22 + warp * 2.5, 5);
    float detail = Fbm(xz * 0.9 + warp * 4.0, 3);
    float hue = Fbm(xz * 0.08 + warp, 3);

    // 紫 粉 蓝三色 按低频噪声混合 与截图配色一致
    float3 violet = float3(0.38, 0.16, 0.62);
    float3 pink = float3(0.85, 0.32, 0.55);
    float3 blue = float3(0.22, 0.42, 0.85);
    float3 tint = lerp(lerp(violet, pink, smoothstep(0.35, 0.65, hue)), blue, smoothstep(0.55, 0.8, cloud));
    float density = smoothstep(0.3, 0.75, cloud) * (0.55 + detail * 0.6);
    // 稀疏星点
    float star = step(0.995, Hash21(floor(xz * 40.0))) * smoothstep(0.4, 0.7, cloud);
    return (tint * density + star * 1.5) * intensity * _NebulaIntensity;
}

float3 ShadeGround(GroundSurface surface, float3 positionWS, float4 screenPos)
{
    float2 screenUV = screenPos.xy / screenPos.w;
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = surface.normal;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.bakedGI = SampleSH(surface.normal);
    inputData.normalizedScreenSpaceUV = screenUV;
    inputData.shadowMask = 1;

    SurfaceData surfaceData = (SurfaceData)0;
    surfaceData.albedo = surface.albedo;
    surfaceData.smoothness = surface.smoothness;
    surfaceData.occlusion = 1;
    surfaceData.alpha = 1;
    float3 color = UniversalFragmentPBR(inputData, surfaceData).rgb;

    // 平面反射 反射相机与主相机同屏幕空间 法线扰动 uv 产生涟漪倒影抖动
    float2 reflectUV = screenUV + surface.normal.xz * 0.06;
    float3 reflected = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, reflectUV).rgb * _PlanarReflectionValid;
    float nDotV = saturate(dot(surface.normal, inputData.viewDirectionWS));
    // 水的菲涅尔 F0 0.02 掠射角倒影接近全反射
    float fresnel = 0.02 + 0.98 * pow(1.0 - nDotV, 5.0);
    float reflectWeight = fresnel * lerp(0.35, 1.0, surface.puddle) * surface.smoothness;
    color = lerp(color, reflected, saturate(reflectWeight));

    // 星云是半透明光雾 透过它仍能看到地砖与倒影
    color += NebulaOverlay(positionWS.xz, surface.nebula);
    return color;
}

#endif
