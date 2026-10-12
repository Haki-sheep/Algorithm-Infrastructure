using UnityEngine;

namespace HitImpact
{
    /// <summary>
    /// 内置管线相机后处理 参数全部为 0 时直接透传 不额外消耗
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ImpactPostEffect : MonoBehaviour
    {
        /// <summary> 使用 HitImpact/ImpactPost 着色器的材质 </summary>
        [SerializeField]
        private Material postMaterial;

        /// <summary> 径向模糊中心 屏幕 UV </summary>
        private Vector2 center = new Vector2(0.5f, 0.5f);

        /// <summary> 径向模糊强度 0 为关闭 </summary>
        private float blurStrength;

        /// <summary> 黑白化程度 0 到 1 </summary>
        private float mono;

        /// <summary> 反色程度 0 到 1 </summary>
        private float invert;

        /// <summary> 着色器属性 ID _Center </summary>
        private static readonly int CenterId = Shader.PropertyToID("_Center");

        /// <summary> 着色器属性 ID _BlurStrength </summary>
        private static readonly int BlurStrengthId = Shader.PropertyToID("_BlurStrength");

        /// <summary> 着色器属性 ID _Mono </summary>
        private static readonly int MonoId = Shader.PropertyToID("_Mono");

        /// <summary> 着色器属性 ID _Invert </summary>
        private static readonly int InvertId = Shader.PropertyToID("_Invert");

        /// <summary>
        /// 设置本帧后处理参数 由 HitImpactDirector 每帧驱动
        /// </summary>
        public void SetImpact(Vector2 screenUV, float blur, float monoAmount, float invertAmount)
        {
            center = screenUV;
            blurStrength = blur;
            mono = monoAmount;
            invert = invertAmount;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (blurStrength <= 0.0001f && mono <= 0.0001f && invert <= 0.0001f)
            {
                Graphics.Blit(source, destination);
                return;
            }

            postMaterial.SetVector(CenterId, center);
            postMaterial.SetFloat(BlurStrengthId, blurStrength);
            postMaterial.SetFloat(MonoId, mono);
            postMaterial.SetFloat(InvertId, invert);
            Graphics.Blit(source, destination, postMaterial);
        }
    }
}
