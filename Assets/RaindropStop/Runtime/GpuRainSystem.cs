using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace RaindropStop
{
    /// <summary>
    /// compute shader 驱动的 GPU 降水 每个粒子按所在位置的局部时间流速积分
    /// 雨雪共用模拟 由材质决定外观 发射盒水平跟随主相机
    /// </summary>
    public class GpuRainSystem : MonoBehaviour
    {
        /// <summary> 雨滴数量 </summary>
        [SerializeField]
        [Range(1024, 262144)]
        private int dropCount = 65536;

        /// <summary> 发射盒尺寸 x z 为半宽 y 为地面以上的高度 </summary>
        [SerializeField]
        private Vector3 emitterExtents = new Vector3(30f, 20f, 30f);

        /// <summary> 地面世界高度 雨滴在此处溅射 </summary>
        [SerializeField]
        private float groundHeight = 0f;

        /// <summary> 雪的下落速度 米每秒 雨按雨滴直径取终端速度 不使用此值 </summary>
        [SerializeField]
        private float fallSpeed = 1.2f;

        /// <summary> 雪模式 下落速度固定 带摆动 </summary>
        [SerializeField]
        private bool isSnow;

        /// <summary> 每滴雨占用的四边形数 主体加溅射水珠 雪为 1 与 RainDrop.shader SPLASH_DROPLET_COUNT 加 1 一致 </summary>
        [SerializeField]
        private int quadsPerDrop = 7;

        /// <summary> 下落速度随机幅度 比例 </summary>
        [SerializeField]
        [Range(0f, 0.5f)]
        private float speedJitter = 0.2f;

        /// <summary> 风速倍率 乘 RainEnvironment 当前风速 雪受风影响更大 </summary>
        [SerializeField]
        private float windMultiplier = 1f;

        /// <summary> 水平摆动幅度 米每秒 雨为 0 雪花约 0.6 </summary>
        [SerializeField]
        private float flutter = 0f;

        /// <summary> 落地溅射持续时长 秒 </summary>
        [SerializeField]
        private float splashDuration = 0.28f;

        [SerializeField]
        private ComputeShader rainCompute;

        [SerializeField]
        private Material rainMaterial;

        /// <summary> 雨滴状态缓冲 生命周期与组件一致 OnDestroy 释放 </summary>
        private ComputeBuffer dropBuffer;

        private int initKernel;

        private int simulateKernel;

        private Transform followTarget;

        /// <summary>
        /// 与 RainSimulation.compute 和 RainDrop.shader 的 RainDrop 结构逐字节一致
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct RainDrop
        {
            public Vector3 Position;
            public float Splash;
            public Vector3 Velocity;
            public float TimeScale;
            public float Seed;
            public float LocalTime;
            public Vector2 Padding;
        }

        private void Start() => InitComponents();

        private void InitComponents()
        {
            followTarget = Camera.main.transform;

            dropBuffer = new ComputeBuffer(dropCount, Marshal.SizeOf<RainDrop>(), ComputeBufferType.Structured);
            initKernel = rainCompute.FindKernel("InitDrops");
            simulateKernel = rainCompute.FindKernel("SimulateDrops");
            rainCompute.SetBuffer(initKernel, "_DropBuffer", dropBuffer);
            rainMaterial.SetBuffer("_DropBuffer", dropBuffer);
            rainMaterial.SetFloat("_SplashDuration", splashDuration);

            PushSimulationParams();
            rainCompute.Dispatch(initKernel, ThreadGroupCount(), 1, 1);
        }

        private void OnDestroy() => dropBuffer?.Release();

        private void Update()
        {
            PushSimulationParams();
            rainCompute.SetBuffer(simulateKernel, "_DropBuffer", dropBuffer);
            rainCompute.SetFloat("_DeltaTime", Time.deltaTime);
            rainCompute.SetFloat("_FrameSeed", Time.time % 1000f);

            var Environment = RainEnvironment.Instance;
            rainCompute.SetInt("_TimeFieldCount", Environment.TimeFieldCount);
            rainCompute.SetVectorArray("_TimeFieldCenterRadius", Environment.TimeFieldCenterRadiusList);
            rainCompute.SetVectorArray("_TimeFieldParam", Environment.TimeFieldParamList);
            rainCompute.SetVector("_Wind", Environment.CurrentWind * windMultiplier);
            rainCompute.SetVector("_EnvironmentRect", Environment.EnvironmentRect);
            rainCompute.SetTexture(simulateKernel, "_OcclusionHeightTex", Environment.OcclusionTexture);
            rainCompute.SetFloat("_OcclusionEnabled", isSnow ? 0f : 1f);
            rainCompute.Dispatch(simulateKernel, ThreadGroupCount(), 1, 1);

            Vector3 center = EmitterCenter();
            var DrawBounds = new Bounds(center, new Vector3(emitterExtents.x * 2f, emitterExtents.y + 2f, emitterExtents.z * 2f));
            // 每个雨滴展开为若干四边形 每个 6 个顶点 顶点着色器按 SV_VertexID 读取缓冲
            Graphics.DrawProcedural(rainMaterial, DrawBounds, MeshTopology.Triangles, dropCount * quadsPerDrop * 6, 1, null, null, ShadowCastingMode.Off, false, gameObject.layer);
        }

        /// <summary>
        /// 发射盒中心 水平跟随相机 垂直居中于地面以上
        /// </summary>
        private Vector3 EmitterCenter()
        {
            Vector3 followPos = followTarget.position;
            return new Vector3(followPos.x, groundHeight + emitterExtents.y * 0.5f, followPos.z);
        }

        /// <summary>
        /// 雨雪两套系统共用同一个 compute shader 资产 参数与缓冲需在每次 Dispatch 前重新提交
        /// </summary>
        private void PushSimulationParams()
        {
            rainCompute.SetInt("_DropCount", dropCount);
            rainCompute.SetFloat("_SplashDuration", splashDuration);
            rainCompute.SetVector("_EmitterCenter", EmitterCenter());
            rainCompute.SetVector("_EmitterExtents", emitterExtents);
            rainCompute.SetFloat("_GroundHeight", groundHeight);
            rainCompute.SetFloat("_SnowFallSpeed", fallSpeed);
            rainCompute.SetFloat("_IsSnow", isSnow ? 1f : 0f);
            rainCompute.SetFloat("_SpeedJitter", speedJitter);
            rainCompute.SetFloat("_Flutter", flutter);
        }

        private int ThreadGroupCount() => (dropCount + 255) / 256;
    }
}
