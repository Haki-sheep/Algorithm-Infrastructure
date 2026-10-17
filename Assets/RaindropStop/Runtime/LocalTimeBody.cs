using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 让刚体按所在位置的局部时间流速运动 时停区域内速度和重力同时缩放 离开后恢复原动量
    /// 接管重力 运行期间 Rigidbody.useGravity 保持关闭
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class LocalTimeBody : MonoBehaviour
    {
        /// <summary> 低于该流速视为定格 不再从物理结果反推真实速度 </summary>
        private const float FrozenThreshold = 0.02f;

        private Rigidbody body;

        /// <summary> 不受时间缩放影响的真实线速度 </summary>
        private Vector3 trueVelocity;

        /// <summary> 不受时间缩放影响的真实角速度 </summary>
        private Vector3 trueAngularVelocity;

        /// <summary> 上一物理帧应用的流速 用于从碰撞后的速度反推真实速度 </summary>
        private float appliedScale = 1f;

        public float CurrentScale => appliedScale;

        // 对象池激活后同帧即调用 Launch 早于 Start 必须在 Awake 获取刚体
        private void Awake() => InitComponents();

        private void InitComponents()
        {
            body = GetComponent<Rigidbody>();
            body.useGravity = false;
            trueVelocity = body.velocity;
            trueAngularVelocity = body.angularVelocity;
        }

        /// <summary>
        /// 以真实速度发射 不受当前所在时间场影响
        /// </summary>
        public void Launch(Vector3 velocity)
        {
            trueVelocity = velocity;
            body.velocity = velocity * appliedScale;
        }

        private void FixedUpdate()
        {
            // 碰撞求解会改写 body.velocity 按上一帧缩放还原 才能保留碰撞带来的动量变化
            if (appliedScale > FrozenThreshold)
            {
                trueVelocity = body.velocity / appliedScale;
                trueAngularVelocity = body.angularVelocity / appliedScale;
            }

            float scale = LocalTimeField.SampleScale(body.position);
            trueVelocity += Physics.gravity * (Time.fixedDeltaTime * scale);
            body.velocity = trueVelocity * scale;
            body.angularVelocity = trueAngularVelocity * scale;
            appliedScale = scale;
        }
    }
}
