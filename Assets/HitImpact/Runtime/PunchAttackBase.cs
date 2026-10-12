using UnityEngine;

namespace HitImpact
{
    public enum PunchPhase
    {
        Idle,
        Windup,
        Strike,
        Hold,
        Recover,
    }

    /// <summary>
    /// 出拳时间轴 蓄力 出拳 停顿 收招 出拳结束帧触发命中
    /// 使用缩放时间推进 顿帧时动作自然冻结
    /// </summary>
    public abstract class PunchAttackBase : MonoBehaviour
    {
        /// <summary> 命中调度器 </summary>
        [SerializeField]
        protected HitImpactDirector director;

        /// <summary> 是否自动循环出拳 </summary>
        [Header("Trigger")]
        [SerializeField]
        private bool autoLoop = true;

        /// <summary> 自动循环的待机间隔 秒 </summary>
        [SerializeField]
        private float loopInterval = 1.1f;

        /// <summary> 手动出拳按键 </summary>
        [SerializeField]
        private KeyCode punchKey = KeyCode.Space;

        /// <summary> 蓄力时长 秒 </summary>
        [Header("Timing")]
        [SerializeField]
        private float windupDuration = 0.22f;

        /// <summary> 出拳时长 秒 越短越有爆发感 </summary>
        [SerializeField]
        private float strikeDuration = 0.07f;

        /// <summary> 命中后定格时长 秒 </summary>
        [SerializeField]
        private float holdDuration = 0.18f;

        /// <summary> 收招时长 秒 </summary>
        [SerializeField]
        private float recoverDuration = 0.35f;

        /// <summary> 当前阶段 </summary>
        private PunchPhase phase = PunchPhase.Idle;

        /// <summary> 当前阶段已经过的缩放时间 秒 </summary>
        private float phaseElapsed;

        protected virtual void Update()
        {
            if (Input.GetKeyDown(punchKey) && phase == PunchPhase.Idle)
            {
                EnterPhase(PunchPhase.Windup);
            }

            phaseElapsed += Time.deltaTime;
            switch (phase)
            {
                case PunchPhase.Idle:
                    if (autoLoop && phaseElapsed >= loopInterval)
                    {
                        EnterPhase(PunchPhase.Windup);
                    }
                    break;
                case PunchPhase.Windup:
                    if (phaseElapsed >= windupDuration)
                    {
                        EnterPhase(PunchPhase.Strike);
                    }
                    break;
                case PunchPhase.Strike:
                    if (phaseElapsed >= strikeDuration)
                    {
                        EnterPhase(PunchPhase.Hold);
                        ApplyPose(PunchPhase.Hold, 0f);
                        director.PlayImpact(GetHitPoint(), GetHitDirection());
                        OnHit();
                    }
                    break;
                case PunchPhase.Hold:
                    if (phaseElapsed >= holdDuration)
                    {
                        EnterPhase(PunchPhase.Recover);
                    }
                    break;
                case PunchPhase.Recover:
                    if (phaseElapsed >= recoverDuration)
                    {
                        EnterPhase(PunchPhase.Idle);
                    }
                    break;
            }

            ApplyPose(phase, GetPhaseProgress());
        }

        /// <summary>
        /// 按阶段与阶段内归一化进度摆姿势
        /// </summary>
        protected abstract void ApplyPose(PunchPhase ePhase, float progress);

        /// <summary>
        /// 命中点 世界坐标
        /// </summary>
        protected abstract Vector3 GetHitPoint();

        /// <summary>
        /// 攻击方向 世界空间
        /// </summary>
        protected abstract Vector3 GetHitDirection();

        /// <summary>
        /// 命中瞬间的额外反馈 如受击方击退
        /// </summary>
        protected virtual void OnHit()
        {
        }

        private void EnterPhase(PunchPhase ePhase)
        {
            phase = ePhase;
            phaseElapsed = 0f;
        }

        private float GetPhaseProgress()
        {
            float duration = phase switch
            {
                PunchPhase.Windup => windupDuration,
                PunchPhase.Strike => strikeDuration,
                PunchPhase.Hold => holdDuration,
                PunchPhase.Recover => recoverDuration,
                _ => loopInterval,
            };
            return Mathf.Clamp01(phaseElapsed / duration);
        }
    }
}
