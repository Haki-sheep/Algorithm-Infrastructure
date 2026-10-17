using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 演示用自由相机 按住右键旋转 WASD 平移 QE 升降 Shift 加速
    /// </summary>
    public class DemoFlyCamera : MonoBehaviour
    {
        /// <summary> 移动速度 米每秒 </summary>
        [SerializeField]
        private float moveSpeed = 5f;

        /// <summary> 旋转灵敏度 度每像素 </summary>
        [SerializeField]
        private float lookSensitivity = 0.15f;

        private float yaw;

        private float pitch;

        private void Start() => Init();

        private void Init()
        {
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x > 180f ? euler.x - 360f : euler.x;
        }

        private void Update()
        {
            if (Input.GetMouseButton(1))
            {
                yaw += Input.GetAxisRaw("Mouse X") * lookSensitivity * 20f;
                pitch = Mathf.Clamp(pitch - Input.GetAxisRaw("Mouse Y") * lookSensitivity * 20f, -85f, 85f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 move = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            if (Input.GetKey(KeyCode.E))
            {
                move.y += 1f;
            }
            if (Input.GetKey(KeyCode.Q))
            {
                move.y -= 1f;
            }

            float speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * 3f : moveSpeed;
            transform.position += transform.TransformDirection(move) * (speed * Time.unscaledDeltaTime);
        }
    }
}
