using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 演示输入 按键在施法者前方展开时停区域 再按收回 另一个键切换雨雪
    /// </summary>
    public class TimeStopCaster : MonoBehaviour
    {
        [SerializeField]
        private LocalTimeField timeField;

        /// <summary> 展开位置相对施法者的偏移 施法者局部坐标 </summary>
        [SerializeField]
        private Vector3 castOffset = new Vector3(0f, 1f, 4f);

        [SerializeField]
        private KeyCode toggleKey = KeyCode.T;

        [SerializeField]
        private KeyCode weatherKey = KeyCode.R;

        [SerializeField]
        private GameObject rainRoot;

        [SerializeField]
        private GameObject snowRoot;

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                // 收回过程中不移动 避免区域瞬移
                if (!timeField.IsActive && timeField.Intensity <= 0f)
                {
                    timeField.transform.position = transform.TransformPoint(castOffset);
                }
                timeField.Toggle();
            }

            if (Input.GetKeyDown(weatherKey))
            {
                bool useSnow = rainRoot.activeSelf;
                rainRoot.SetActive(!useSnow);
                snowRoot.SetActive(useSnow);
            }
        }
    }
}
