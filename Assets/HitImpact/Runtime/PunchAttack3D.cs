using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 3D 程序化出拳 拳头在攻击者本地空间的三个关键点间插值 手臂段自动拉伸连接肩膀与拳头
    /// </summary>
    public class PunchAttack3D : PunchAttackBase
    {
        /// <summary> 拳头 </summary>
        [Header("Rig")]
        [SerializeField]
        private Transform fist;

        /// <summary> 肩膀 手臂段起点 </summary>
        [SerializeField]
        private Transform shoulder;

        /// <summary> 手臂段 使用内置 Cylinder 高度为 2 个单位 </summary>
        [SerializeField]
        private Transform armSegment;

        /// <summary> 上半身 出拳时扭转 </summary>
        [SerializeField]
        private Transform torso;

        /// <summary> 受击方 </summary>
        [SerializeField]
        private ImpactReceiver receiver;

        /// <summary> 拳头半径 命中点取拳面 </summary>
        [SerializeField]
        private float fistRadius = 0.22f;

        /// <summary> 待机拳头位置 攻击者本地空间 </summary>
        [Header("Pose")]
        [SerializeField]
        private Vector3 idleFistLocal = new Vector3(0.35f, 1.35f, 0.35f);

        /// <summary> 蓄力拳头位置 攻击者本地空间 </summary>
        [SerializeField]
        private Vector3 windupFistLocal = new Vector3(0.45f, 1.4f, -0.25f);

        /// <summary> 出拳终点 攻击者本地空间 </summary>
        [SerializeField]
        private Vector3 strikeFistLocal = new Vector3(0.12f, 1.45f, 1.25f);

        /// <summary> 蓄力时上半身扭转角 度 </summary>
        [SerializeField]
        private float windupTwist = -28f;

        /// <summary> 出拳时上半身扭转角 度 </summary>
        [SerializeField]
        private float strikeTwist = 22f;

        /// <summary> 命中时传给受击方的冲量 </summary>
        [SerializeField]
        private float hitImpulse = 6f;

        protected override void ApplyPose(PunchPhase ePhase, float progress)
        {
            Vector3 fistLocal;
            float twist;
            switch (ePhase)
            {
                case PunchPhase.Windup:
                    float windupEase = 1f - (1f - progress) * (1f - progress);
                    fistLocal = Vector3.Lerp(idleFistLocal, windupFistLocal, windupEase);
                    twist = Mathf.Lerp(0f, windupTwist, windupEase);
                    break;
                case PunchPhase.Strike:
                    // 加速进入 末端速度最大 拖影和冲击都集中在命中前一帧
                    float strikeEase = progress * progress;
                    fistLocal = Vector3.Lerp(windupFistLocal, strikeFistLocal, strikeEase);
                    twist = Mathf.Lerp(windupTwist, strikeTwist, strikeEase);
                    break;
                case PunchPhase.Hold:
                    fistLocal = strikeFistLocal;
                    twist = strikeTwist;
                    break;
                case PunchPhase.Recover:
                    float recoverEase = Mathf.SmoothStep(0f, 1f, progress);
                    fistLocal = Vector3.Lerp(strikeFistLocal, idleFistLocal, recoverEase);
                    twist = Mathf.Lerp(strikeTwist, 0f, recoverEase);
                    break;
                default:
                    float bob = Mathf.Sin(Time.time * 3f) * 0.025f;
                    fistLocal = idleFistLocal + new Vector3(0f, bob, 0f);
                    twist = 0f;
                    break;
            }

            torso.localRotation = Quaternion.Euler(0f, twist, 0f);
            fist.position = transform.TransformPoint(fistLocal);
            RefreshArmSegment();
        }

        protected override Vector3 GetHitPoint()
        {
            return fist.position + GetHitDirection() * fistRadius;
        }

        protected override Vector3 GetHitDirection()
        {
            return transform.TransformDirection(strikeFistLocal - windupFistLocal).normalized;
        }

        protected override void OnHit()
        {
            receiver.ReceiveHit(GetHitDirection() * hitImpulse);
        }

        private void RefreshArmSegment()
        {
            Vector3 start = shoulder.position;
            Vector3 end = fist.position;
            Vector3 offset = end - start;
            armSegment.position = (start + end) * 0.5f;
            armSegment.rotation = Quaternion.FromToRotation(Vector3.up, offset);
            Vector3 scale = armSegment.localScale;
            armSegment.localScale = new Vector3(scale.x, offset.magnitude * 0.5f, scale.z);
        }
    }
}
