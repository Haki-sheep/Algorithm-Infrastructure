using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 2D 朝镜头出拳 拳头 Sprite 位移同时快速放大模拟透视冲向画面 身体随之前压
    /// </summary>
    public class PunchAttack2D : PunchAttackBase
    {
        /// <summary> 身体 Sprite 节点 </summary>
        [Header("Rig")]
        [SerializeField]
        private Transform body;

        /// <summary> 前景拳头 Sprite 节点 </summary>
        [SerializeField]
        private Transform fist;

        /// <summary> 待机拳头位置 本地空间 </summary>
        [Header("Pose")]
        [SerializeField]
        private Vector3 idleFistLocal = new Vector3(0.9f, 0.4f, 0f);

        /// <summary> 待机拳头缩放 </summary>
        [SerializeField]
        private float idleFistScale = 1f;

        /// <summary> 蓄力拳头位置 本地空间 </summary>
        [SerializeField]
        private Vector3 windupFistLocal = new Vector3(1.2f, 0.7f, 0f);

        /// <summary> 蓄力拳头缩放 </summary>
        [SerializeField]
        private float windupFistScale = 0.85f;

        /// <summary> 出拳终点 本地空间 </summary>
        [SerializeField]
        private Vector3 strikeFistLocal = new Vector3(-0.5f, -1.6f, 0f);

        /// <summary> 出拳终点缩放 拳头占满画面下半部分 </summary>
        [SerializeField]
        private float strikeFistScale = 6f;

        /// <summary> 出拳时身体缩放 前压感 </summary>
        [SerializeField]
        private float strikeBodyScale = 1.08f;

        /// <summary> 出拳时身体倾斜角 度 </summary>
        [SerializeField]
        private float strikeBodyTilt = 8f;

        /// <summary> 身体初始本地缩放 </summary>
        private Vector3 bodyBaseScale;

        private void Start()
        {
            Init();
        }

        /// <summary>
        /// 记录身体初始缩放
        /// </summary>
        public void Init()
        {
            bodyBaseScale = body.localScale;
        }

        protected override void ApplyPose(PunchPhase ePhase, float progress)
        {
            Vector3 fistLocal;
            float fistScale;
            float bodyWeight;
            switch (ePhase)
            {
                case PunchPhase.Windup:
                    float windupEase = 1f - (1f - progress) * (1f - progress);
                    fistLocal = Vector3.Lerp(idleFistLocal, windupFistLocal, windupEase);
                    fistScale = Mathf.Lerp(idleFistScale, windupFistScale, windupEase);
                    bodyWeight = -0.3f * windupEase;
                    break;
                case PunchPhase.Strike:
                    // 缩放用三次方 末端暴涨 透视上越靠近镜头放大越快
                    float strikeEase = progress * progress;
                    fistLocal = Vector3.Lerp(windupFistLocal, strikeFistLocal, strikeEase);
                    fistScale = Mathf.Lerp(windupFistScale, strikeFistScale, progress * progress * progress);
                    bodyWeight = Mathf.Lerp(-0.3f, 1f, strikeEase);
                    break;
                case PunchPhase.Hold:
                    fistLocal = strikeFistLocal;
                    fistScale = strikeFistScale;
                    bodyWeight = 1f;
                    break;
                case PunchPhase.Recover:
                    float recoverEase = Mathf.SmoothStep(0f, 1f, progress);
                    fistLocal = Vector3.Lerp(strikeFistLocal, idleFistLocal, recoverEase);
                    fistScale = Mathf.Lerp(strikeFistScale, idleFistScale, recoverEase);
                    bodyWeight = 1f - recoverEase;
                    break;
                default:
                    float bob = Mathf.Sin(Time.time * 3f) * 0.04f;
                    fistLocal = idleFistLocal + new Vector3(0f, bob, 0f);
                    fistScale = idleFistScale;
                    bodyWeight = 0f;
                    break;
            }

            fist.localPosition = fistLocal;
            fist.localScale = Vector3.one * fistScale;
            body.localScale = bodyBaseScale * Mathf.LerpUnclamped(1f, strikeBodyScale, bodyWeight);
            body.localRotation = Quaternion.Euler(0f, 0f, strikeBodyTilt * bodyWeight);
        }

        protected override Vector3 GetHitPoint()
        {
            return fist.position;
        }

        protected override Vector3 GetHitDirection()
        {
            return transform.TransformDirection(strikeFistLocal - windupFistLocal).normalized;
        }
    }
}
