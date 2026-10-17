#ifndef RAINDROP_STOP_NOISE_COMMON_INCLUDED
#define RAINDROP_STOP_NOISE_COMMON_INCLUDED

// 无纹理依赖的程序化噪声 地面 立面 星云共用

float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float2 Hash22(float2 p)
{
    float n = Hash21(p);
    return float2(n, Hash21(p + n * 17.17));
}

float ValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = Hash21(i);
    float b = Hash21(i + float2(1, 0));
    float c = Hash21(i + float2(0, 1));
    float d = Hash21(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// 输入坐标与倍频数 输出 0 到 1 分形噪声
float Fbm(float2 p, int octaves)
{
    float sum = 0.0;
    float amp = 0.5;
    float norm = 0.0;
    for (int i = 0; i < octaves; i++)
    {
        sum += ValueNoise(p) * amp;
        norm += amp;
        // 每层旋转约 37 度 消除值噪声的轴向条纹
        p = float2(p.x * 0.8 - p.y * 0.6, p.x * 0.6 + p.y * 0.8) * 2.03 + 11.7;
        amp *= 0.5;
    }
    return sum / norm;
}

#endif
