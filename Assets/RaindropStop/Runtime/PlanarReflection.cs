using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RaindropStop
{
    /// <summary>
    /// 地面平面反射 主相机渲染前用镜像相机渲染一次 结果写入全局 _PlanarReflectionTex
    /// 斜裁剪平面剔除镜面以下内容 湿地面按屏幕 uv 采样
    /// </summary>
    public class PlanarReflection : MonoBehaviour
    {
        private static readonly int ReflectionTexId = Shader.PropertyToID("_PlanarReflectionTex");
        private static readonly int ValidId = Shader.PropertyToID("_PlanarReflectionValid");

        /// <summary> 镜面世界高度 </summary>
        [SerializeField]
        private float planeHeight = 0f;

        /// <summary> 反射分辨率相对主相机比例 </summary>
        [SerializeField]
        [Range(0.25f, 1f)]
        private float resolutionScale = 0.5f;

        /// <summary> 参与反射的层 排除地面自身 </summary>
        [SerializeField]
        private LayerMask reflectionMask = ~0;

        /// <summary> 裁剪平面上移量 避免地面接缝处闪烁 米 </summary>
        [SerializeField]
        private float clipOffset = 0.03f;

        /// <summary> 镜像相机 随组件销毁 </summary>
        private Camera reflectionCamera;

        /// <summary> 反射纹理 分辨率变化时重建 随组件释放 </summary>
        private RenderTexture reflectionTexture;

        private void OnEnable()
        {
            InitComponents();
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            Shader.SetGlobalFloat(ValidId, 0f);
            if (reflectionCamera != null)
            {
                Destroy(reflectionCamera.gameObject);
            }
            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
                reflectionTexture = null;
            }
        }

        private void InitComponents()
        {
            var CameraObject = new GameObject("PlanarReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
            reflectionCamera = CameraObject.AddComponent<Camera>();
            reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;
            var CameraData = CameraObject.AddComponent<UniversalAdditionalCameraData>();
            CameraData.renderShadows = false;
            CameraData.requiresColorOption = CameraOverrideOption.Off;
            CameraData.requiresDepthOption = CameraOverrideOption.Off;
            CameraData.renderPostProcessing = false;
        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera sourceCamera)
        {
            if (sourceCamera == reflectionCamera || sourceCamera.cameraType == CameraType.Preview || sourceCamera.cameraType == CameraType.Reflection)
            {
                return;
            }

            RefreshTexture(sourceCamera);
            reflectionCamera.CopyFrom(sourceCamera);
            reflectionCamera.cullingMask = reflectionMask;
            reflectionCamera.targetTexture = reflectionTexture;
            reflectionCamera.useOcclusionCulling = false;

            var Normal = Vector3.up;
            var PlanePoint = new Vector3(0f, planeHeight, 0f);
            var Reflection = ReflectionMatrix(new Vector4(0f, 1f, 0f, -planeHeight));
            reflectionCamera.worldToCameraMatrix = sourceCamera.worldToCameraMatrix * Reflection;
            // 镜像后的相机位置仅用于排序与 LOD
            reflectionCamera.transform.position = Reflection.MultiplyPoint(sourceCamera.transform.position);

            Vector4 clipPlane = CameraSpacePlane(reflectionCamera, PlanePoint + Normal * clipOffset, Normal);
            reflectionCamera.projectionMatrix = sourceCamera.CalculateObliqueMatrix(clipPlane);

            // 镜像使三角形绕序反转
            GL.invertCulling = true;
#pragma warning disable CS0618
            UniversalRenderPipeline.RenderSingleCamera(context, reflectionCamera);
#pragma warning restore CS0618
            GL.invertCulling = false;

            Shader.SetGlobalTexture(ReflectionTexId, reflectionTexture);
            Shader.SetGlobalFloat(ValidId, 1f);
        }

        /// <summary>
        /// 主相机分辨率变化时重建反射纹理
        /// </summary>
        private void RefreshTexture(Camera sourceCamera)
        {
            int width = Mathf.Max(64, (int)(sourceCamera.pixelWidth * resolutionScale));
            int height = Mathf.Max(64, (int)(sourceCamera.pixelHeight * resolutionScale));
            if (reflectionTexture != null && reflectionTexture.width == width && reflectionTexture.height == height)
            {
                return;
            }
            if (reflectionTexture != null)
            {
                reflectionTexture.Release();
            }
            reflectionTexture = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR)
            {
                name = "PlanarReflection",
                useMipMap = false,
            };
        }

        /// <summary>
        /// 输入世界平面 xyz 法线 w 偏移 输出关于该平面的镜像矩阵
        /// </summary>
        private static Matrix4x4 ReflectionMatrix(Vector4 plane)
        {
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = 1f - 2f * plane.x * plane.x;
            m.m01 = -2f * plane.x * plane.y;
            m.m02 = -2f * plane.x * plane.z;
            m.m03 = -2f * plane.w * plane.x;
            m.m10 = -2f * plane.y * plane.x;
            m.m11 = 1f - 2f * plane.y * plane.y;
            m.m12 = -2f * plane.y * plane.z;
            m.m13 = -2f * plane.w * plane.y;
            m.m20 = -2f * plane.z * plane.x;
            m.m21 = -2f * plane.z * plane.y;
            m.m22 = 1f - 2f * plane.z * plane.z;
            m.m23 = -2f * plane.w * plane.z;
            return m;
        }

        /// <summary>
        /// 输入世界平面点与法线 输出相机空间平面 用于斜裁剪投影
        /// </summary>
        private static Vector4 CameraSpacePlane(Camera targetCamera, Vector3 point, Vector3 normal)
        {
            Matrix4x4 view = targetCamera.worldToCameraMatrix;
            Vector3 cameraPoint = view.MultiplyPoint(point);
            Vector3 cameraNormal = view.MultiplyVector(normal).normalized;
            return new Vector4(cameraNormal.x, cameraNormal.y, cameraNormal.z, -Vector3.Dot(cameraPoint, cameraNormal));
        }
    }
}
