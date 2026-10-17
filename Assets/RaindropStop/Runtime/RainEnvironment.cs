using UnityEngine;
using UnityEngine.Rendering;

namespace RaindropStop
{
    /// <summary>
    /// 雨夜共享环境数据 每帧上传时间场 雨光源 风 到全局着色器变量
    /// 维护俯视遮挡高度图 雨滴落在伞面雨棚上 与地面局部时间图 地面涟漪按此推进 时停内涟漪定格
    /// 场景内唯一 执行顺序早于 GpuRainSystem
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class RainEnvironment : MonoBehaviour
    {
        public static RainEnvironment Instance { get; private set; }

        /// <summary> 局部时间回卷周期 秒 地面涟漪周期 1 秒 需为其整数倍 </summary>
        private const float LocalTimeWrap = 64f;

        private static readonly int TimeFieldCenterRadiusId = Shader.PropertyToID("_TimeFieldCenterRadius");
        private static readonly int TimeFieldParamId = Shader.PropertyToID("_TimeFieldParam");
        private static readonly int TimeFieldCountId = Shader.PropertyToID("_TimeFieldCount");
        private static readonly int EnvironmentRectId = Shader.PropertyToID("_EnvironmentRect");
        private static readonly int LocalTimeTexId = Shader.PropertyToID("_LocalTimeTex");
        private static readonly int OcclusionTexId = Shader.PropertyToID("_OcclusionHeightTex");
        private static readonly int RainAmbientId = Shader.PropertyToID("_RainAmbient");
        private static readonly int WindId = Shader.PropertyToID("_Wind");
        private static readonly int RainIntensityId = Shader.PropertyToID("_RainIntensity");

        /// <summary> 覆盖区域世界 xz 中心 地面时间图与遮挡图共用 </summary>
        [SerializeField]
        private Vector2 areaCenter = new Vector2(0f, 6f);

        /// <summary> 覆盖区域边长 米 </summary>
        [SerializeField]
        private float areaSize = 64f;

        /// <summary> 地面局部时间图分辨率 </summary>
        [SerializeField]
        private int localTimeResolution = 512;

        /// <summary> 遮挡高度图分辨率 </summary>
        [SerializeField]
        private int occlusionResolution = 1024;

        /// <summary> 遮挡图俯视投影顶部高度 世界坐标 </summary>
        [SerializeField]
        private float occlusionTop = 30f;

        /// <summary> 雨滴可见的环境底光 线性 </summary>
        [SerializeField]
        private Color rainAmbient = new Color(0.02f, 0.025f, 0.035f);

        /// <summary> 平均风速 米每秒 </summary>
        [SerializeField]
        private Vector3 baseWind = new Vector3(1.2f, 0f, 0.4f);

        /// <summary> 阵风幅度 米每秒 </summary>
        [SerializeField]
        private float gustStrength = 1.4f;

        /// <summary> 雨量 0 到 1 驱动地面涟漪密度 </summary>
        [SerializeField]
        [Range(0f, 1f)]
        private float rainIntensity = 1f;

        [SerializeField]
        private ComputeShader rainCompute;

        /// <summary> 使用 Hidden/RaindropStop/OcclusionHeight 输出世界高度 </summary>
        [SerializeField]
        private Material occlusionMaterial;

        /// <summary> 地面局部时间 R 通道 随组件释放 </summary>
        private RenderTexture localTimeTexture;

        /// <summary> 俯视遮挡最高表面高度 随组件释放 </summary>
        private RenderTexture occlusionTexture;

        /// <summary> 遮挡图绘制命令 每帧重录 </summary>
        private CommandBuffer occlusionCommand;

        private int accumulateKernel;

        private float gustTime;

        private readonly Vector4[] centerRadiusList = new Vector4[LocalTimeField.MaxGpuFieldCount];

        private readonly Vector4[] paramList = new Vector4[LocalTimeField.MaxGpuFieldCount];

        public Vector4 EnvironmentRect => new Vector4(areaCenter.x - areaSize * 0.5f, areaCenter.y - areaSize * 0.5f, areaSize, areaSize);

        public RenderTexture OcclusionTexture => occlusionTexture;

        public Vector3 CurrentWind { get; private set; }

        public int TimeFieldCount { get; private set; }

        public Vector4[] TimeFieldCenterRadiusList => centerRadiusList;

        public Vector4[] TimeFieldParamList => paramList;

        private void Awake()
        {
            Instance = this;
            InitComponents();
        }

        private void InitComponents()
        {
            localTimeTexture = new RenderTexture(localTimeResolution, localTimeResolution, 0, RenderTextureFormat.RFloat)
            {
                enableRandomWrite = true,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RainLocalTime",
            };
            localTimeTexture.Create();
            accumulateKernel = rainCompute.FindKernel("AccumulateLocalTime");

            occlusionTexture = new RenderTexture(occlusionResolution, occlusionResolution, 0, RenderTextureFormat.RFloat)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "RainOcclusionHeight",
            };
            occlusionTexture.Create();

            occlusionCommand = new CommandBuffer { name = "RainOcclusionHeight" };
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            localTimeTexture.Release();
            occlusionTexture.Release();
            occlusionCommand.Release();
        }

        private void Update()
        {
            TimeFieldCount = LocalTimeField.FillGpuData(centerRadiusList, paramList);
            Shader.SetGlobalVectorArray(TimeFieldCenterRadiusId, centerRadiusList);
            Shader.SetGlobalVectorArray(TimeFieldParamId, paramList);
            Shader.SetGlobalInt(TimeFieldCountId, TimeFieldCount);
            Shader.SetGlobalVector(EnvironmentRectId, EnvironmentRect);
            Shader.SetGlobalColor(RainAmbientId, rainAmbient.linear);
            Shader.SetGlobalFloat(RainIntensityId, rainIntensity);
            RainLight.UploadGlobals();

            // 两个不同周期正弦叠加 风向和风速缓慢摆动 避免机械感
            gustTime += Time.deltaTime;
            float gust = Mathf.Sin(gustTime * 0.37f) * 0.6f + Mathf.Sin(gustTime * 1.13f + 1.7f) * 0.4f;
            CurrentWind = baseWind + new Vector3(Mathf.Cos(gustTime * 0.21f), 0f, Mathf.Sin(gustTime * 0.21f)) * (gust * gustStrength);
            Shader.SetGlobalVector(WindId, CurrentWind);

            RenderOcclusion();

            rainCompute.SetTexture(accumulateKernel, LocalTimeTexId, localTimeTexture);
            rainCompute.SetInt("_LocalTimeSize", localTimeResolution);
            rainCompute.SetFloat("_LocalTimeWrap", LocalTimeWrap);
            rainCompute.SetFloat("_DeltaTime", Time.deltaTime);
            rainCompute.SetVector(EnvironmentRectId, EnvironmentRect);
            rainCompute.SetFloat("_GroundHeight", 0f);
            rainCompute.SetVectorArray(TimeFieldCenterRadiusId, centerRadiusList);
            rainCompute.SetVectorArray(TimeFieldParamId, paramList);
            rainCompute.SetInt(TimeFieldCountId, TimeFieldCount);
            int groups = (localTimeResolution + 7) / 8;
            rainCompute.Dispatch(accumulateKernel, groups, groups, 1);
            Shader.SetGlobalTexture(LocalTimeTexId, localTimeTexture);
        }

        /// <summary>
        /// 俯视正交投影绘制所有遮挡物 每像素取最高表面世界高度 雨滴模拟据此在伞面上溅开
        /// </summary>
        private void RenderOcclusion()
        {
            float half = areaSize * 0.5f;
            // 视图矩阵 从顶部向下看 观察空间 -z 指向世界 -y
            Matrix4x4 view = Matrix4x4.TRS(new Vector3(areaCenter.x, occlusionTop, areaCenter.y), Quaternion.Euler(90f, 0f, 0f), new Vector3(1f, 1f, -1f)).inverse;
            Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-half, half, -half, half, 0.1f, occlusionTop + 5f), true);

            occlusionCommand.Clear();
            occlusionCommand.SetRenderTarget(occlusionTexture);
            occlusionCommand.ClearRenderTarget(false, true, new Color(-1000f, 0f, 0f, 0f));
            occlusionCommand.SetViewProjectionMatrices(view, projection);
            foreach (var Item in RainOccluder.RendererList)
            {
                for (int i = 0; i < Item.sharedMaterials.Length; i++)
                {
                    occlusionCommand.DrawRenderer(Item, occlusionMaterial, i, 0);
                }
            }
            Graphics.ExecuteCommandBuffer(occlusionCommand);
            Shader.SetGlobalTexture(OcclusionTexId, occlusionTexture);
        }
    }
}
