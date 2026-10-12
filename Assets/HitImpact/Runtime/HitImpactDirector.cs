using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 打击感统一调度 一次命中依次驱动 顿帧 冲击帧 径向模糊 震屏 水墨粒子
    /// 所有表现使用非缩放时间推进 保证顿帧期间特效仍在播放
    /// </summary>
    public class HitImpactDirector : MonoBehaviour
    {
        /// <summary> 渲染画面的相机 用于把命中点换算为屏幕 UV </summary>
        [SerializeField]
        private Camera targetCamera;

        /// <summary> 相机上的后处理 </summary>
        [SerializeField]
        private ImpactPostEffect postEffect;

        /// <summary> 相机震动 </summary>
        [SerializeField]
        private CameraShaker cameraShaker;

        /// <summary> 水墨特效根节点 命中时移动到命中点 </summary>
        [SerializeField]
        private Transform impactFxRoot;

        /// <summary> 命中时重播的粒子 </summary>
        [SerializeField]
        private ParticleSystem[] impactParticleList;

        /// <summary> 顿帧时长 真实秒 </summary>
        [Header("Hit Stop")]
        [SerializeField]
        private float hitStopDuration = 0.09f;

        /// <summary> 顿帧期间的 timeScale </summary>
        [SerializeField]
        private float hitStopTimeScale = 0.02f;

        /// <summary> 冲击帧总时长 真实秒 下列曲线横轴按此归一化 </summary>
        [Header("Impact Frame")]
        [SerializeField]
        private float impactDuration = 0.45f;

        /// <summary> 径向模糊强度曲线 </summary>
        [SerializeField]
        private AnimationCurve blurCurve = new AnimationCurve(new Keyframe(0f, 0.38f), new Keyframe(0.25f, 0.3f), new Keyframe(1f, 0f));

        /// <summary> 径向模糊整体倍率 画面中主体占比大时调低 </summary>
        [SerializeField]
        private float blurMultiplier = 1f;

        /// <summary> 黑白化曲线 </summary>
        [SerializeField]
        private AnimationCurve monoCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.55f, 1f), new Keyframe(1f, 0f));

        /// <summary> 反色闪帧时长 真实秒 0 为关闭 </summary>
        [SerializeField]
        private float invertDuration = 0.035f;

        /// <summary> 震屏振幅 世界单位 </summary>
        [Header("Shake")]
        [SerializeField]
        private float shakeAmplitude = 0.12f;

        /// <summary> 震屏时长 真实秒 </summary>
        [SerializeField]
        private float shakeDuration = 0.28f;

        /// <summary> 距离上次命中的真实秒 </summary>
        private float impactElapsed = float.MaxValue;

        /// <summary> 顿帧剩余真实秒 </summary>
        private float hitStopRemaining;

        /// <summary> 命中点屏幕 UV </summary>
        private Vector2 impactScreenUV;

        /// <summary> 顿帧前的 timeScale 结束时恢复 </summary>
        private float cachedTimeScale = 1f;

        private void Update()
        {
            TickHitStop();
            TickImpactFrame();
        }

        private void OnDisable()
        {
            if (hitStopRemaining > 0f)
            {
                Time.timeScale = cachedTimeScale;
                hitStopRemaining = 0f;
            }
            postEffect.SetImpact(impactScreenUV, 0f, 0f, 0f);
        }

        /// <summary>
        /// 在世界坐标 hitPoint 触发一次命中 hitDirection 为攻击方向 用于震屏首帧推力
        /// </summary>
        public void PlayImpact(Vector3 hitPoint, Vector3 hitDirection)
        {
            Vector3 viewportPoint = targetCamera.WorldToViewportPoint(hitPoint);
            impactScreenUV = new Vector2(viewportPoint.x, viewportPoint.y);
            impactElapsed = 0f;

            if (hitStopRemaining <= 0f)
            {
                cachedTimeScale = Time.timeScale;
            }
            hitStopRemaining = hitStopDuration;
            Time.timeScale = hitStopTimeScale;

            Vector3 shakeDirection = targetCamera.transform.InverseTransformDirection(hitDirection);
            shakeDirection.z = 0f;
            cameraShaker.Shake(shakeAmplitude, shakeDuration, shakeDirection.normalized);

            impactFxRoot.position = hitPoint;
            foreach (ParticleSystem particle in impactParticleList)
            {
                particle.Clear(true);
                particle.Play(true);
            }
        }

        private void TickHitStop()
        {
            if (hitStopRemaining <= 0f)
            {
                return;
            }

            hitStopRemaining -= Time.unscaledDeltaTime;
            if (hitStopRemaining <= 0f)
            {
                Time.timeScale = cachedTimeScale;
            }
        }

        private void TickImpactFrame()
        {
            if (impactElapsed > impactDuration)
            {
                return;
            }

            impactElapsed += Time.unscaledDeltaTime;
            float normalizedTime = Mathf.Clamp01(impactElapsed / impactDuration);
            float invert = impactElapsed < invertDuration ? 1f : 0f;
            postEffect.SetImpact(impactScreenUV, blurCurve.Evaluate(normalizedTime) * blurMultiplier, monoCurve.Evaluate(normalizedTime), invert);
        }
    }
}
