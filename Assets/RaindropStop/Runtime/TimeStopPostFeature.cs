using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RaindropStop
{
    /// <summary>
    /// 时停区域屏幕后处理 区域内去饱和冷色 球面边界折射色散与冲击波
    /// 场景中无生效时间场时不入队 零开销
    /// </summary>
    public class TimeStopPostFeature : ScriptableRendererFeature
    {
        /// <summary> 使用 Hidden/RaindropStop/TimeStopPost 的材质 </summary>
        [SerializeField]
        private Material postMaterial;

        /// <summary> 放在透明物体之后 让雨滴一起被调色 </summary>
        [SerializeField]
        private RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

        private TimeStopPostPass postPass;

        public override void Create()
        {
            postPass = new TimeStopPostPass(postMaterial) { renderPassEvent = passEvent };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (postMaterial == null || renderingData.cameraData.cameraType == CameraType.Preview || !postPass.PrepareFieldData())
            {
                return;
            }

            postPass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(postPass);
        }

        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            postPass.SetSource(renderer.cameraColorTargetHandle);
        }

        protected override void Dispose(bool disposing) => postPass?.Dispose();

        private sealed class TimeStopPostPass : ScriptableRenderPass
        {
            private static readonly int CenterRadiusId = Shader.PropertyToID("_TimeFieldCenterRadius");
            private static readonly int ParamId = Shader.PropertyToID("_TimeFieldParam");
            private static readonly int CountId = Shader.PropertyToID("_TimeFieldCount");

            private readonly Material material;
            private readonly ProfilingSampler sampler = new ProfilingSampler("TimeStopPost");
            private readonly Vector4[] centerRadiusList = new Vector4[LocalTimeField.MaxGpuFieldCount];
            private readonly Vector4[] paramList = new Vector4[LocalTimeField.MaxGpuFieldCount];
            private int fieldCount;
            private RTHandle source;

            /// <summary> 中转纹理 分辨率随相机变化按需重分配 Dispose 释放 </summary>
            private RTHandle tempHandle;

            public TimeStopPostPass(Material postMaterial)
            {
                material = postMaterial;
            }

            /// <summary>
            /// 采集本帧时间场 返回是否需要绘制
            /// </summary>
            public bool PrepareFieldData()
            {
                fieldCount = LocalTimeField.FillGpuData(centerRadiusList, paramList);
                return fieldCount > 0;
            }

            public void SetSource(RTHandle cameraColor) => source = cameraColor;

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;
                desc.msaaSamples = 1;
                RenderingUtils.ReAllocateIfNeeded(ref tempHandle, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TimeStopPostTemp");
                ConfigureTarget(source);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, sampler))
                {
                    material.SetVectorArray(CenterRadiusId, centerRadiusList);
                    material.SetVectorArray(ParamId, paramList);
                    material.SetInt(CountId, fieldCount);
                    Blitter.BlitCameraTexture(cmd, source, tempHandle, material, 0);
                    Blitter.BlitCameraTexture(cmd, tempHandle, source);
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public void Dispose() => tempHandle?.Release();
        }
    }
}
