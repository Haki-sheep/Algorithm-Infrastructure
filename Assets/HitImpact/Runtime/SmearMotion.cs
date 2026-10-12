using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 根据自身世界速度生成拖影向量 写入 SmearToon 材质的 _SmearVector
    /// 平滑使用缩放时间 顿帧时拖影被冻结在命中瞬间 与冲击帧同步
    /// </summary>
    public class SmearMotion : MonoBehaviour
    {
        /// <summary> 需要拖影的渲染器 材质需使用 HitImpact/SmearToon </summary>
        [SerializeField]
        private Renderer[] smearRendererList;

        /// <summary> 速度到拖影长度的换算 秒 </summary>
        [SerializeField]
        private float velocityToLength = 0.045f;

        /// <summary> 拖影最大长度 世界单位 </summary>
        [SerializeField]
        private float maxLength = 1.6f;

        /// <summary> 低于该速度不产生拖影 世界单位每秒 </summary>
        [SerializeField]
        private float minSpeed = 3f;

        /// <summary> 拖影跟随速度的平滑系数 越大越跟手 </summary>
        [SerializeField]
        private float response = 30f;

        /// <summary> 写入渲染器的属性块 </summary>
        private MaterialPropertyBlock propertyBlock;

        /// <summary> 上一帧世界坐标 </summary>
        private Vector3 lastPosition;

        /// <summary> 当前拖影向量 世界空间 </summary>
        private Vector3 smearVector;

        /// <summary> 着色器属性 ID _SmearVector </summary>
        private static readonly int SmearVectorId = Shader.PropertyToID("_SmearVector");

        private void Start()
        {
            Init();
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 position = transform.position;
            Vector3 velocity = (position - lastPosition) / deltaTime;
            lastPosition = position;

            float speed = velocity.magnitude;
            Vector3 targetSmear = speed > minSpeed ? velocity * velocityToLength : Vector3.zero;
            targetSmear = Vector3.ClampMagnitude(targetSmear, maxLength);
            smearVector = Vector3.Lerp(smearVector, targetSmear, 1f - Mathf.Exp(-response * deltaTime));

            propertyBlock.SetVector(SmearVectorId, smearVector);
            foreach (Renderer smearRenderer in smearRendererList)
            {
                smearRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        /// <summary>
        /// 创建属性块并记录初始位置
        /// </summary>
        public void Init()
        {
            propertyBlock = new MaterialPropertyBlock();
            lastPosition = transform.position;
        }
    }
}
