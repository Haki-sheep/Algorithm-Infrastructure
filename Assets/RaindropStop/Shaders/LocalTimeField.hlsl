#ifndef RAINDROP_STOP_LOCAL_TIME_FIELD_INCLUDED
#define RAINDROP_STOP_LOCAL_TIME_FIELD_INCLUDED

// 与 LocalTimeField.MaxGpuFieldCount 保持一致
#define MAX_TIME_FIELD_COUNT 4

// xyz 中心 w 当前半径
float4 _TimeFieldCenterRadius[MAX_TIME_FIELD_COUNT];
// x 过渡带 y 中心流速 z 展开进度 w 是否在过渡
float4 _TimeFieldParam[MAX_TIME_FIELD_COUNT];
int _TimeFieldCount;

// 输入世界坐标 输出时间流速 与 C# LocalTimeField.SampleScale 同一公式
float SampleTimeScale(float3 worldPos)
{
    float result = 1.0;
    for (int i = 0; i < _TimeFieldCount; i++)
    {
        float dist = distance(worldPos, _TimeFieldCenterRadius[i].xyz);
        float t = saturate((_TimeFieldCenterRadius[i].w - dist) / _TimeFieldParam[i].x);
        result = min(result, lerp(1.0, _TimeFieldParam[i].y, t * t * (3.0 - 2.0 * t)));
    }
    return result;
}

#endif
