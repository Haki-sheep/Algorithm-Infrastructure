using UnityEngine;
using UnityEngine.UI;

namespace HitImpact
{
    /// <summary>
    /// 屏幕左上角效果开关面板 逐项控制打击感表现 便于对比开关前后的差异
    /// </summary>
    public class HitImpactTogglePanel : MonoBehaviour
    {
        /// <summary> 打击调度器 </summary>
        [SerializeField]
        private HitImpactDirector director;

        /// <summary> 拳头拖影 </summary>
        [SerializeField]
        private SmearMotion smear;

        /// <summary> 顿帧开关 </summary>
        [SerializeField]
        private Toggle hitStopToggle;

        /// <summary> 径向模糊开关 </summary>
        [SerializeField]
        private Toggle blurToggle;

        /// <summary> 黑白化开关 </summary>
        [SerializeField]
        private Toggle monoToggle;

        /// <summary> 反色闪帧开关 </summary>
        [SerializeField]
        private Toggle invertToggle;

        /// <summary> 震屏开关 </summary>
        [SerializeField]
        private Toggle shakeToggle;

        /// <summary> 水墨粒子开关 </summary>
        [SerializeField]
        private Toggle inkFxToggle;

        /// <summary> 拖影开关 </summary>
        [SerializeField]
        private Toggle smearToggle;

        private void Start()
        {
            InitComponents();
        }

        /// <summary>
        /// 绑定开关回调 并把面板显示同步到各效果的当前状态
        /// </summary>
        public void InitComponents()
        {
            hitStopToggle.onValueChanged.AddListener(value => director.HitStopEnabled = value);
            blurToggle.onValueChanged.AddListener(value => director.BlurEnabled = value);
            monoToggle.onValueChanged.AddListener(value => director.MonoEnabled = value);
            invertToggle.onValueChanged.AddListener(value => director.InvertEnabled = value);
            shakeToggle.onValueChanged.AddListener(value => director.ShakeEnabled = value);
            inkFxToggle.onValueChanged.AddListener(value => director.InkFxEnabled = value);
            smearToggle.onValueChanged.AddListener(value => smear.SmearEnabled = value);

            hitStopToggle.SetIsOnWithoutNotify(director.HitStopEnabled);
            blurToggle.SetIsOnWithoutNotify(director.BlurEnabled);
            monoToggle.SetIsOnWithoutNotify(director.MonoEnabled);
            invertToggle.SetIsOnWithoutNotify(director.InvertEnabled);
            shakeToggle.SetIsOnWithoutNotify(director.ShakeEnabled);
            inkFxToggle.SetIsOnWithoutNotify(director.InkFxEnabled);
            smearToggle.SetIsOnWithoutNotify(smear.SmearEnabled);
        }
    }
}
