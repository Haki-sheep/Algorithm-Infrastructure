#ifndef RAINDROP_STOP_RAIN_DROP_COMMON_INCLUDED
#define RAINDROP_STOP_RAIN_DROP_COMMON_INCLUDED

// 与 GpuRainSystem.RainDrop 内存布局一致 48 字节
struct RainDrop
{
    float3 position;
    // 小于 0 为空中 大于等于 0 为落地后的局部时间 秒
    float splash;
    float3 velocity;
    float timeScale;
    float seed;
    // 局部时间累计 驱动雪花摆动与翻滚相位
    float localTime;
    float2 padding;
};

static const float2 QuadCornerList[6] = { float2(-1, -1), float2(1, -1), float2(-1, 1), float2(-1, 1), float2(1, -1), float2(1, 1) };

// 输入种子 输出雨滴等效直径 毫米 小雨滴远多于大雨滴
float DropDiameterMm(float seed)
{
    return lerp(0.8, 4.5, pow(saturate(seed), 2.2));
}

// Atlas 1973 终端速度经验式 输入直径毫米 输出米每秒
float TerminalSpeed(float diameterMm)
{
    return 9.65 - 10.3 * exp(-0.6 * diameterMm);
}

struct StreakQuad
{
    float3 positionWS;
    // xy 四边形局部坐标 -1 到 1 z 长宽比
    float3 quad;
    // 被像素宽度钳制后的能量衰减 0 到 1
    float coverage;
    // 0 拖尾 1 静止水珠
    float frozen;
};

// 输入球心 已乘局部流速的速度 半径 快门时长 四边形角点 最小半宽像素数
// 输出运动模糊拖尾四边形 速度趋零时连续退化为圆点 半宽不低于 minHalfWidthPixels 以免闪烁
StreakQuad BuildStreakQuad(float3 center, float3 velocity, float radius, float shutter, float2 corner, float minHalfWidthPixels)
{
    StreakQuad result;
    float3 toCamera = normalize(_WorldSpaceCameraPos - center);
    float speed = length(velocity);
    float3 axis = speed > 1e-4 ? velocity / speed : float3(0, -1, 0);
    float3 side = cross(axis, toCamera);
    // 视线与运动方向平行时拖尾退化 改用相机右方向
    side = dot(side, side) < 1e-6 ? UNITY_MATRIX_V[0].xyz : normalize(side);

    float trueHalfLength = speed * shutter * 0.5 + radius;
    float viewDepth = max(dot(center - _WorldSpaceCameraPos, -UNITY_MATRIX_V[2].xyz), 0.05);
    float pixelWorld = 2.0 * viewDepth / (UNITY_MATRIX_P[1][1] * _ScreenParams.y);
    // 低于约 1.5 像素宽的雨丝经抗锯齿后几乎消失
    float halfWidth = max(radius, pixelWorld * minHalfWidthPixels);
    float halfLength = max(trueHalfLength, halfWidth);

    result.positionWS = center + side * (corner.x * halfWidth) - axis * (corner.y * halfLength);
    result.quad = float3(corner, halfLength / halfWidth);
    // 严格按面积守恒远处雨丝会完全消失 只按亚像素宽度衰减 开方保留中远景雨幕层次
    result.coverage = max(sqrt(radius / halfWidth), 0.35);
    result.frozen = saturate(1.0 - speed * shutter / (radius * 2.0));
    return result;
}

// 输入四边形局部坐标与长宽比 输出胶囊距离场 0 中心 1 边缘
float CapsuleDistance(float2 quad, float aspect, out float2 capsule)
{
    float2 local = float2(quad.x, quad.y * aspect);
    // 保留 y 符号 否则上下半球法线镜像
    capsule = float2(local.x, sign(local.y) * max(abs(local.y) - (aspect - 1.0), 0.0));
    return length(capsule);
}

#endif
