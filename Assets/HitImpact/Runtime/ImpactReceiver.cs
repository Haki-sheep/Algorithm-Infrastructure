using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 受击方反馈 阻尼弹簧击退 + 沿受击方向压扁
    /// 使用缩放时间 顿帧期间保持受击瞬间的形变
    /// </summary>
    public class ImpactReceiver : MonoBehaviour
    {
        /// <summary> 被位移和形变的可视节点 不要直接用带碰撞或逻辑的根节点 </summary>
        [SerializeField]
        private Transform visual;

        /// <summary> 弹簧刚度 </summary>
        [SerializeField]
        private float stiffness = 140f;

        /// <summary> 弹簧阻尼 </summary>
        [SerializeField]
        private float damping = 11f;

        /// <summary> 每单位位移对应的压扁比例 </summary>
        [SerializeField]
        private float squashPerOffset = 0.9f;

        /// <summary> 最大压扁比例 </summary>
        [SerializeField]
        private float maxSquash = 0.35f;

        /// <summary> 可视节点初始本地坐标 </summary>
        private Vector3 baseLocalPosition;

        /// <summary> 可视节点初始本地缩放 </summary>
        private Vector3 baseLocalScale;

        /// <summary> 当前偏移 世界空间 </summary>
        private Vector3 offset;

        /// <summary> 当前偏移速度 世界空间 </summary>
        private Vector3 velocity;

        private void Start()
        {
            Init();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            Vector3 acceleration = -stiffness * offset - damping * velocity;
            velocity += acceleration * deltaTime;
            offset += velocity * deltaTime;

            visual.localPosition = baseLocalPosition + visual.parent.InverseTransformVector(offset);

            float squash = Mathf.Min(offset.magnitude * squashPerOffset, maxSquash);
            visual.localScale = new Vector3(baseLocalScale.x * (1f + squash * 0.5f), baseLocalScale.y * (1f - squash * 0.3f), baseLocalScale.z * (1f - squash));
        }

        /// <summary>
        /// 记录可视节点初始变换
        /// </summary>
        public void Init()
        {
            baseLocalPosition = visual.localPosition;
            baseLocalScale = visual.localScale;
        }

        /// <summary>
        /// 施加击退冲量 世界空间 单位为速度
        /// </summary>
        public void ReceiveHit(Vector3 impulse)
        {
            velocity += impulse;
        }
    }
}
