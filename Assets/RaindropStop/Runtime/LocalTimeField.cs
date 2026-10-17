using System.Collections.Generic;
using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 球形局部时间场 区域内时间流速从边缘 1 平滑过渡到中心 coreScale
    /// 半径以真实时间展开和收回 不受自身时间场影响
    /// </summary>
    public class LocalTimeField : MonoBehaviour
    {
        /// <summary> 着色器与计算着色器可同时读取的时间场上限 </summary>
        public const int MaxGpuFieldCount = 4;

        /// <summary> 场景内所有启用的时间场 </summary>
        private static readonly List<LocalTimeField> fieldList = new List<LocalTimeField>();

        /// <summary> 完全展开时的半径 世界单位 </summary>
        [SerializeField]
        private float radius = 8f;

        /// <summary> 边缘过渡带宽度 世界单位 </summary>
        [SerializeField]
        private float falloff = 2.5f;

        /// <summary> 区域中心的时间流速 0 为完全静止 </summary>
        [SerializeField]
        private float coreScale = 0f;

        /// <summary> 半径展开速度 世界单位每秒 </summary>
        [SerializeField]
        private float expandSpeed = 16f;

        /// <summary> 半径收回速度 世界单位每秒 </summary>
        [SerializeField]
        private float collapseSpeed = 10f;

        /// <summary> 当前半径 展开动画进行中小于 radius </summary>
        private float currentRadius;

        /// <summary> 目标状态 true 为展开 </summary>
        private bool isActive;

        public bool IsActive => isActive;

        /// <summary> 展开进度 0 到 1 驱动后处理强度 </summary>
        public float Intensity => radius > 0f ? currentRadius / radius : 0f;

        /// <summary> 当前是否处于半径变化中 </summary>
        public bool IsTransitioning => !Mathf.Approximately(currentRadius, isActive ? radius : 0f);

        private void OnEnable() => fieldList.Add(this);

        private void OnDisable() => fieldList.Remove(this);

        private void Update()
        {
            float target = isActive ? radius : 0f;
            float speed = isActive ? expandSpeed : collapseSpeed;
            currentRadius = Mathf.MoveTowards(currentRadius, target, speed * Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 切换展开与收回
        /// </summary>
        public void Toggle() => isActive = !isActive;

        /// <summary>
        /// 输入单个时间场与世界坐标 输出该点流速 未生效时返回 1
        /// </summary>
        private float Evaluate(Vector3 worldPos)
        {
            if (currentRadius <= 0f)
            {
                return 1f;
            }

            float dist = Vector3.Distance(worldPos, transform.position);
            float edgeWidth = Mathf.Min(falloff, currentRadius);
            float t = Mathf.Clamp01((currentRadius - dist) / edgeWidth);
            // smoothstep 与 GPU 端 SampleTimeScale 保持一致 保证粒子与刚体在边缘减速节奏相同
            return Mathf.Lerp(1f, coreScale, t * t * (3f - 2f * t));
        }

        /// <summary>
        /// 输入世界坐标 输出该点时间流速 多个时间场重叠时取最慢值
        /// </summary>
        public static float SampleScale(Vector3 worldPos)
        {
            float result = 1f;
            foreach (var Field in fieldList)
            {
                result = Mathf.Min(result, Field.Evaluate(worldPos));
            }
            return result;
        }

        /// <summary>
        /// 把生效中的时间场打包为 GPU 数据 返回写入数量
        /// centerRadiusList 为 xyz 中心 w 当前半径 paramList 为 x 过渡带 y 中心流速 z 展开进度 w 是否在过渡
        /// </summary>
        public static int FillGpuData(Vector4[] centerRadiusList, Vector4[] paramList)
        {
            int count = 0;
            foreach (var Field in fieldList)
            {
                if (count >= MaxGpuFieldCount)
                {
                    break;
                }
                if (Field.currentRadius <= 0f)
                {
                    continue;
                }

                Vector3 center = Field.transform.position;
                centerRadiusList[count] = new Vector4(center.x, center.y, center.z, Field.currentRadius);
                paramList[count] = new Vector4(Mathf.Min(Field.falloff, Field.currentRadius), Field.coreScale, Field.Intensity, Field.IsTransitioning ? 1f : 0f);
                count++;
            }
            return count;
        }
    }
}
