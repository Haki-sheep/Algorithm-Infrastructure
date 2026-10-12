using System.IO;
using UnityEditor;
using UnityEngine;

namespace HitImpact.Editor
{
    /// <summary>
    /// 程序生成水墨特效贴图 笔触 墨点 侵蚀噪声 抖动噪声 输出 PNG 并设置导入参数
    /// </summary>
    public static class HitImpactTextureGenerator
    {
        /// <summary> 贴图输出目录 </summary>
        public const string TextureFolder = "Assets/HitImpact/Textures";

        /// <summary> 毛笔笔触 仅 alpha 有效 </summary>
        public const string StrokePath = TextureFolder + "/T_InkStroke.png";

        /// <summary> 墨点飞溅 仅 alpha 有效 </summary>
        public const string SplatPath = TextureFolder + "/T_InkSplat.png";

        /// <summary> 粒子溶解用可平铺噪声 </summary>
        public const string ErosionNoisePath = TextureFolder + "/T_InkNoise.png";

        /// <summary> 径向模糊采样抖动用白噪声 </summary>
        public const string JitterNoisePath = TextureFolder + "/T_JitterNoise.png";

        /// <summary>
        /// 生成全部贴图 已存在则覆盖
        /// </summary>
        [MenuItem("Tools/HitImpact/Generate Ink Textures")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(TextureFolder);
            WritePng(StrokePath, CreateStroke(512, 128, 1337));
            WritePng(SplatPath, CreateSplat(256, 7));
            WritePng(ErosionNoisePath, CreateTileableNoise(256, 5, 23));
            WritePng(JitterNoisePath, CreateWhiteNoise(64, 91));
            AssetDatabase.Refresh();

            ConfigureImporter(StrokePath, TextureWrapMode.Clamp, true, false);
            ConfigureImporter(SplatPath, TextureWrapMode.Clamp, true, true);
            ConfigureImporter(ErosionNoisePath, TextureWrapMode.Repeat, false, true);
            ConfigureImporter(JitterNoisePath, TextureWrapMode.Repeat, false, false);
        }

        /// <summary>
        /// 横向毛笔笔触 U=1 为落笔端 粗而实 U=0 为收笔端 细且飞白多
        /// </summary>
        private static Texture2D CreateStroke(int width, int height, int seed)
        {
            Random.InitState(seed);
            float bristleOffset = Random.value * 100f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixelList = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height * 2f - 1f;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    // 收笔端幂函数收尖 落笔端用圆头截断
                    float halfWidth = 0.9f * Mathf.Pow(u, 0.55f) * Mathf.Sqrt(Mathf.Clamp01((1f - u) / 0.08f));
                    float edgeWobble = (Mathf.PerlinNoise(u * 18f, 3.1f) - 0.5f) * 0.18f;
                    float edgeDistance = halfWidth + edgeWobble - Mathf.Abs(v);
                    float body = Mathf.Clamp01(edgeDistance * height * 0.5f / 1.5f);

                    // 笔毛 每行沿 U 方向拉长的噪声 越靠近收笔端阈值越高 飞白越多
                    float bristle = Mathf.PerlinNoise(v * 22f + bristleOffset, u * 2.5f);
                    float dryThreshold = Mathf.Lerp(0.62f, 0.18f, Mathf.SmoothStep(0f, 1f, u));
                    float dry = Mathf.Clamp01((bristle - dryThreshold) * 8f + 0.5f);

                    byte alpha = (byte)(body * dry * 255f);
                    pixelList[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }
            texture.SetPixels32(pixelList);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 中心墨团 + 周围随机飞溅墨滴
        /// </summary>
        private static Texture2D CreateSplat(int size, int seed)
        {
            Random.InitState(seed);
            const int DropletCount = 14;
            var dropletCenterList = new Vector2[DropletCount];
            var dropletRadiusList = new float[DropletCount];
            for (int i = 0; i < DropletCount; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float distance = Random.Range(0.28f, 0.44f);
                dropletCenterList[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                dropletRadiusList[i] = Random.Range(0.012f, 0.05f);
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixelList = new Color32[size * size];
            float pixel = 1f / size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var point = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f);
                    float angle = Mathf.Atan2(point.y, point.x);
                    float wobble = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.5f + 5f, Mathf.Sin(angle) * 2.5f + 5f);
                    float blobRadius = 0.16f + wobble * 0.11f;
                    float coverage = Mathf.Clamp01((blobRadius - point.magnitude) / pixel);

                    for (int i = 0; i < DropletCount; i++)
                    {
                        float dropletDistance = (point - dropletCenterList[i]).magnitude;
                        coverage = Mathf.Max(coverage, Mathf.Clamp01((dropletRadiusList[i] - dropletDistance) / pixel));
                    }
                    pixelList[y * size + x] = new Color32(255, 255, 255, (byte)(coverage * 255f));
                }
            }
            texture.SetPixels32(pixelList);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// 可平铺 FBM 值噪声 晶格按周期取模保证四边无缝
        /// </summary>
        private static Texture2D CreateTileableNoise(int size, int octaves, int seed)
        {
            Random.InitState(seed);
            const int LatticeSize = 256;
            var latticeList = new float[LatticeSize * LatticeSize];
            for (int i = 0; i < latticeList.Length; i++)
            {
                latticeList[i] = Random.value;
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            var pixelList = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sum = 0f;
                    float amplitude = 0.5f;
                    float totalAmplitude = 0f;
                    int period = 8;
                    for (int octave = 0; octave < octaves; octave++)
                    {
                        float sampleX = (float)x / size * period;
                        float sampleY = (float)y / size * period;
                        sum += SamplePeriodicValueNoise(latticeList, LatticeSize, sampleX, sampleY, period) * amplitude;
                        totalAmplitude += amplitude;
                        amplitude *= 0.5f;
                        period *= 2;
                    }
                    byte value = (byte)(Mathf.Clamp01(sum / totalAmplitude) * 255f);
                    pixelList[y * size + x] = new Color32(value, value, value, 255);
                }
            }
            texture.SetPixels32(pixelList);
            texture.Apply();
            return texture;
        }

        private static float SamplePeriodicValueNoise(float[] latticeList, int latticeSize, float x, float y, int period)
        {
            int x0 = Mathf.FloorToInt(x);
            int y0 = Mathf.FloorToInt(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);

            float a = latticeList[(y0 % period) * latticeSize + x0 % period];
            float b = latticeList[(y0 % period) * latticeSize + (x0 + 1) % period];
            float c = latticeList[((y0 + 1) % period) * latticeSize + x0 % period];
            float d = latticeList[((y0 + 1) % period) * latticeSize + (x0 + 1) % period];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static Texture2D CreateWhiteNoise(int size, int seed)
        {
            Random.InitState(seed);
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixelList = new Color32[size * size];
            for (int i = 0; i < pixelList.Length; i++)
            {
                byte value = (byte)Random.Range(0, 256);
                pixelList[i] = new Color32(value, value, value, 255);
            }
            texture.SetPixels32(pixelList);
            texture.Apply();
            return texture;
        }

        private static void WritePng(string assetPath, Texture2D texture)
        {
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static void ConfigureImporter(string assetPath, TextureWrapMode eWrapMode, bool alphaShape, bool mipmap)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = eWrapMode;
            importer.mipmapEnabled = mipmap;
            // 全部为数据贴图 颜色由顶点色或材质提供
            importer.sRGBTexture = false;
            importer.alphaSource = alphaShape ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alphaShape;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
