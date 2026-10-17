using UnityEngine;

namespace RaindropStop
{
    /// <summary>
    /// 演示用投射物发射器 循环复用固定数量的小球 穿过时停区域时减速悬停
    /// </summary>
    public class DemoBallLauncher : MonoBehaviour
    {
        /// <summary> 带 LocalTimeBody 的小球预制体 </summary>
        [SerializeField]
        private LocalTimeBody ballPrefab;

        /// <summary> 小球池大小 超出后复用最早发射的 </summary>
        [SerializeField]
        private int poolSize = 24;

        /// <summary> 发射间隔 秒 </summary>
        [SerializeField]
        private float interval = 0.35f;

        /// <summary> 出射速度 米每秒 沿本物体 forward </summary>
        [SerializeField]
        private float launchSpeed = 14f;

        /// <summary> 出射方向随机角度 度 </summary>
        [SerializeField]
        private float spreadAngle = 6f;

        private LocalTimeBody[] ballList;

        private int nextIndex;

        private float timer;

        private void Start() => Init();

        private void Init()
        {
            ballList = new LocalTimeBody[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                ballList[i] = Instantiate(ballPrefab, transform.position, Quaternion.identity);
                ballList[i].gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < interval)
            {
                return;
            }
            timer -= interval;

            var Ball = ballList[nextIndex];
            nextIndex = (nextIndex + 1) % poolSize;
            Ball.transform.SetPositionAndRotation(transform.position, Random.rotation);
            Ball.gameObject.SetActive(true);
            var Spread = Quaternion.Euler(Random.Range(-spreadAngle, spreadAngle), Random.Range(-spreadAngle, spreadAngle), 0f);
            Ball.Launch(transform.rotation * Spread * Vector3.forward * launchSpeed);
        }
    }
}
