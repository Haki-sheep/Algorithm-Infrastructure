using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 路人与车辆的往返运动 位移与肢体摆动都按所在位置局部时间推进 时停区域内保持当前姿态定格
    /// 不使用 Animator 避免动画状态与时间场脱节
    /// </summary>
    public class LocalTimeWalker : MonoBehaviour
    {
        /// <summary> 往返终点 相对起点的世界偏移 </summary>
        [SerializeField]
        private Vector3 travel = new Vector3(0f, 0f, 12f);

        /// <summary> 移动速度 米每秒 </summary>
        [SerializeField]
        private float speed = 1.3f;

        /// <summary> 走路摆臂摆腿的肢体 左右交替 车辆留空 </summary>
        [SerializeField]
        private Transform[] limbList;

        /// <summary> 肢体最大摆角 度 </summary>
        [SerializeField]
        private float swingAngle = 28f;

        /// <summary> 每米步频 周期每米 </summary>
        [SerializeField]
        private float strideFrequency = 1.4f;

        /// <summary> 车轮 绕本地 x 轴滚动 路人留空 </summary>
        [SerializeField]
        private Transform[] wheelList;

        /// <summary> 车轮半径 米 </summary>
        [SerializeField]
        private float wheelRadius = 0.32f;

        private Vector3 startPosition;

        /// <summary> 沿路径累计走过的距离 米 0 到 2 倍路径长循环 </summary>
        private float distance;

        private Quaternion[] limbRestList;

        private void Start() => Init();

        private void Init()
        {
            startPosition = transform.position;
            limbRestList = new Quaternion[limbList.Length];
            for (int i = 0; i < limbList.Length; i++)
            {
                limbRestList[i] = limbList[i].localRotation;
            }
        }

        private void Update()
        {
            float scale = LocalTimeField.SampleScale(transform.position + Vector3.up);
            float step = speed * Time.deltaTime * scale;
            float length = travel.magnitude;
            distance = Mathf.Repeat(distance + step, length * 2f);

            // 走到终点折返 朝向随之翻转
            bool isReturning = distance > length;
            float along = isReturning ? length * 2f - distance : distance;
            Vector3 direction = travel / length;
            transform.position = startPosition + direction * along;
            transform.rotation = Quaternion.LookRotation(isReturning ? -direction : direction, Vector3.up);

            float phase = distance * strideFrequency * Mathf.PI * 2f;
            for (int i = 0; i < limbList.Length; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                limbList[i].localRotation = limbRestList[i] * Quaternion.Euler(Mathf.Sin(phase) * swingAngle * side, 0f, 0f);
            }

            float wheelAngle = distance / wheelRadius * Mathf.Rad2Deg;
            foreach (var Wheel in wheelList)
            {
                Wheel.localRotation = Quaternion.Euler(wheelAngle, 0f, 90f);
            }
        }
    }
}
