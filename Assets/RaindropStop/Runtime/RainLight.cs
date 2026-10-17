using System.Collections.Generic;
using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 标记照亮雨雪的光源 程序化绘制的降水拿不到 URP 光源列表 由此每帧打包为全局着色器数组
    /// 与 RainLighting.hlsl 的数组布局一致
    /// </summary>
    [RequireComponent(typeof(Light))]
    [ExecuteAlways]
    public class RainLight : MonoBehaviour
    {
        /// <summary> 与 MAX_RAIN_LIGHT_COUNT 保持一致 </summary>
        public const int MaxLightCount = 32;

        private static readonly List<RainLight> lightList = new List<RainLight>();

        private static readonly Vector4[] posRangeList = new Vector4[MaxLightCount];

        private static readonly Vector4[] colorList = new Vector4[MaxLightCount];

        private static readonly Vector4[] spotList = new Vector4[MaxLightCount];

        private static readonly int PosRangeId = Shader.PropertyToID("_RainLightPosRange");
        private static readonly int ColorId = Shader.PropertyToID("_RainLightColor");
        private static readonly int SpotId = Shader.PropertyToID("_RainLightSpot");
        private static readonly int CountId = Shader.PropertyToID("_RainLightCount");

        /// <summary> 照到雨上的额外倍率 让雨丝在光锥里更醒目 </summary>
        [SerializeField]
        private float rainIntensity = 1f;

        private Light lightComponent;

        private void OnEnable()
        {
            lightComponent = GetComponent<Light>();
            lightList.Add(this);
        }

        private void OnDisable() => lightList.Remove(this);

        /// <summary>
        /// 打包全部启用光源到全局数组 每帧由 RainEnvironment 调用一次
        /// </summary>
        public static void UploadGlobals()
        {
            int count = 0;
            foreach (var Item in lightList)
            {
                if (count >= MaxLightCount)
                {
                    break;
                }
                var Source = Item.lightComponent;
                if (!Source.isActiveAndEnabled || Source.intensity <= 0f)
                {
                    continue;
                }

                Color linear = Source.color.linear * (Source.intensity * Item.rainIntensity);
                colorList[count] = new Vector4(linear.r, linear.g, linear.b, 1f);
                Vector3 position = Item.transform.position;
                Vector3 forward = Item.transform.forward;
                if (Source.type == LightType.Directional)
                {
                    posRangeList[count] = new Vector4(position.x, position.y, position.z, -1f);
                    spotList[count] = new Vector4(forward.x, forward.y, forward.z, -2f);
                }
                else
                {
                    posRangeList[count] = new Vector4(position.x, position.y, position.z, Source.range);
                    float cosOuter = Source.type == LightType.Spot ? Mathf.Cos(Source.spotAngle * 0.5f * Mathf.Deg2Rad) : -2f;
                    spotList[count] = new Vector4(forward.x, forward.y, forward.z, cosOuter);
                }
                count++;
            }

            Shader.SetGlobalVectorArray(PosRangeId, posRangeList);
            Shader.SetGlobalVectorArray(ColorId, colorList);
            Shader.SetGlobalVectorArray(SpotId, spotList);
            Shader.SetGlobalInt(CountId, count);
        }
    }
}
