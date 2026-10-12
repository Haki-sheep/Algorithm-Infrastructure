using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 相机震动 叠加在初始本地坐标上 使用非缩放时间 顿帧期间照常震动
    /// </summary>
    public class CameraShaker : MonoBehaviour
    {
        /// <summary> 震动频率 每秒 Perlin 采样步进 </summary>
        [SerializeField]
        private float frequency = 38f;

        /// <summary> 衰减曲线 横轴为归一化时间 </summary>
        [SerializeField]
        private AnimationCurve decayCurve = new AnimationCurve(new Keyframe(0f, 1f, 0f, -3f), new Keyframe(1f, 0f));

        /// <summary> 初始本地坐标 </summary>
        private Vector3 baseLocalPosition;

        /// <summary> 当前震动振幅 世界单位 </summary>
        private float amplitude;

        /// <summary> 当前震动总时长 秒 </summary>
        private float duration;

        /// <summary> 已震动时长 秒 </summary>
        private float elapsed;

        /// <summary> 主方向 让第一下沿受击方向弹出 </summary>
        private Vector3 kickDirection;

        private void Start()
        {
            Init();
        }

        private void LateUpdate()
        {
            if (elapsed >= duration)
            {
                transform.localPosition = baseLocalPosition;
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float strength = amplitude * decayCurve.Evaluate(normalizedTime);
            float sampleTime = elapsed * frequency;
            Vector3 noise = new Vector3(Mathf.PerlinNoise(sampleTime, 0.17f) - 0.5f, Mathf.PerlinNoise(0.53f, sampleTime) - 0.5f, 0f) * 2f;
            // 前 15% 时长叠加一次方向性回弹 只用噪声会缺少被打中的那一下推力
            float kick = Mathf.Clamp01(1f - normalizedTime / 0.15f);
            transform.localPosition = baseLocalPosition + (noise + kickDirection * kick) * strength;
        }

        /// <summary>
        /// 记录初始本地坐标
        /// </summary>
        public void Init()
        {
            baseLocalPosition = transform.localPosition;
        }

        /// <summary>
        /// 开始一次震动 direction 为相机本地空间方向 可为零向量
        /// </summary>
        public void Shake(float shakeAmplitude, float shakeDuration, Vector3 direction)
        {
            amplitude = shakeAmplitude;
            duration = shakeDuration;
            elapsed = 0f;
            kickDirection = direction;
        }
    }
}
