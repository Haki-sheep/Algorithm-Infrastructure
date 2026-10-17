#ifndef RAINDROP_STOP_RAIN_LIGHTING_INCLUDED
#define RAINDROP_STOP_RAIN_LIGHTING_INCLUDED

// 与 RainLight.MaxLightCount 保持一致
#define MAX_RAIN_LIGHT_COUNT 32

// 程序化绘制拿不到 URP 逐物体光源列表 由 RainLight 每帧上传全局数组
// xyz 位置 w 范围 方向光 w 小于 0
float4 _RainLightPosRange[MAX_RAIN_LIGHT_COUNT];
// rgb 颜色乘强度 线性空间
float4 _RainLightColor[MAX_RAIN_LIGHT_COUNT];
// xyz 照射方向 w 聚光外角余弦 点光为 -2
float4 _RainLightSpot[MAX_RAIN_LIGHT_COUNT];
int _RainLightCount;
float4 _RainAmbient;

// 输入光源索引与受照点 输出到光源单位方向和含距离衰减的辐照度
float3 RainLightIrradiance(int index, float3 positionWS, out float3 toLight)
{
    float4 posRange = _RainLightPosRange[index];
    float4 spot = _RainLightSpot[index];
    toLight = float3(0, 1, 0);
    if (posRange.w < 0.0)
    {
        toLight = -spot.xyz;
        return _RainLightColor[index].rgb;
    }

    float3 offset = posRange.xyz - positionWS;
    float distSqr = max(dot(offset, offset), 0.04);
    toLight = offset * rsqrt(distSqr);
    // 与 URP 点光衰减同公式 范围边缘平滑归零
    float rangeRatio = distSqr / (posRange.w * posRange.w);
    float window = saturate(1.0 - rangeRatio * rangeRatio);
    float atten = window * window / distSqr;
    float cone = spot.w > -1.5 ? smoothstep(spot.w, lerp(spot.w, 1.0, 0.4), dot(spot.xyz, -toLight)) : 1.0;
    return _RainLightColor[index].rgb * (atten * cone);
}

// 输入受照点与指向相机方向 输出水体可见亮度
// forwardScatter 控制逆光前向散射 glint 控制顺光镜面反射
float3 EvaluateRainLighting(float3 positionWS, float3 toCamera, float forwardScatter, float glint)
{
    float3 result = _RainAmbient.rgb;
    for (int i = 0; i < _RainLightCount; i++)
    {
        float3 toLight;
        float3 irradiance = RainLightIrradiance(i, positionWS, toLight);
        float cosView = dot(toCamera, toLight);
        // 水滴是近似球透镜 逆光时把光源折射进相机 亮度远高于顺光
        float phase = 0.3 + forwardScatter * pow(saturate(-cosView), 5.0) + glint * pow(saturate(cosView), 6.0);
        result += irradiance * phase;
    }
    return result;
}

// 输入受照点与法线 输出漫反射辐照度 雪花等漫射体使用
float3 EvaluateDiffuseLighting(float3 positionWS, float3 normalWS, float wrap)
{
    float3 result = _RainAmbient.rgb;
    for (int i = 0; i < _RainLightCount; i++)
    {
        float3 toLight;
        float3 irradiance = RainLightIrradiance(i, positionWS, toLight);
        result += irradiance * saturate((dot(normalWS, toLight) + wrap) / (1.0 + wrap));
    }
    return result;
}

#endif
