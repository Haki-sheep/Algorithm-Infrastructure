using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace RaindropStop.Editor
{
    /// <summary>
    /// 一键生成局部时停雨夜街道演示 材质 贴图 预制体 场景 并把后处理挂到当前 URP 渲染器
    /// 重复执行覆盖生成的资源
    /// </summary>
    public static class RaindropStopDemoBuilder
    {
        private const string RootFolder = "Assets/RaindropStop";

        private const string MaterialFolder = RootFolder + "/Materials";

        private const string FacadeFolder = MaterialFolder + "/Facade";

        private const string TextureFolder = RootFolder + "/Textures";

        private const string PrefabFolder = RootFolder + "/Prefabs";

        private const string ScenePath = RootFolder + "/Scenes/RaindropStop.unity";

        /// <summary> 地面所在层 平面反射排除此层 内置 Water 层 </summary>
        private const int GroundLayer = 4;

        /// <summary> 街道沿 z 轴起止 米 </summary>
        private const float StreetStart = -22f;

        private const float StreetEnd = 34f;

        /// <summary> 道路中心到建筑立面距离 米 </summary>
        private const float FacadeOffset = 7f;

        /// <summary>
        /// 生成全部资源和演示场景 完成后打开场景
        /// </summary>
        [MenuItem("Tools/RaindropStop/Build Demo")]
        public static void BuildAll()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene openScene = SceneManager.GetSceneAt(i);
                if (openScene.isDirty)
                {
                    throw new System.InvalidOperationException($"场景 {openScene.path} 有未保存修改 请先保存或丢弃后再搭建演示");
                }
            }

            var Pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (Pipeline == null)
            {
                throw new System.InvalidOperationException("当前未使用 URP 管线资产 请先在 Graphics Settings 中指定");
            }

            Directory.CreateDirectory(FacadeFolder);
            Directory.CreateDirectory(TextureFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var Palette = new DemoPalette
            {
                Rain = CreateMaterial("M_RainDrop", "RaindropStop/RainDrop"),
                Snow = CreateMaterial("M_SnowFlake", "RaindropStop/SnowFlake"),
                Post = CreateMaterial("M_TimeStopPost", "Hidden/RaindropStop/TimeStopPost"),
                Occlusion = CreateMaterial("M_OcclusionHeight", "Hidden/RaindropStop/OcclusionHeight"),
                Ground = CreateMaterial("M_WetGround", "RaindropStop/WetGround"),
                Concrete = CreateLitMaterial("M_WetConcrete", new Color(0.22f, 0.22f, 0.23f), 0.7f, 0f),
                Metal = CreateLitMaterial("M_DarkMetal", new Color(0.08f, 0.09f, 0.1f), 0.8f, 0.9f),
                Awning = CreateLitMaterial("M_Awning", new Color(0.35f, 0.06f, 0.07f), 0.75f, 0f),
                AwningAlt = CreateLitMaterial("M_AwningAlt", new Color(0.08f, 0.2f, 0.18f), 0.75f, 0f),
                Umbrella = CreateLitMaterial("M_Umbrella", new Color(0.03f, 0.03f, 0.035f), 0.85f, 0f),
                UmbrellaAlt = CreateLitMaterial("M_UmbrellaClear", new Color(0.6f, 0.65f, 0.7f), 0.95f, 0f),
                Coat = CreateLitMaterial("M_Coat", new Color(0.12f, 0.12f, 0.14f), 0.45f, 0f),
                CoatAlt = CreateLitMaterial("M_CoatBeige", new Color(0.45f, 0.38f, 0.28f), 0.4f, 0f),
                Skin = CreateLitMaterial("M_Skin", new Color(0.75f, 0.58f, 0.48f), 0.4f, 0f),
                CarBody = CreateLitMaterial("M_CarPaint", new Color(0.55f, 0.56f, 0.6f), 0.92f, 0.75f),
                Glass = CreateLitMaterial("M_CarGlass", new Color(0.02f, 0.025f, 0.03f), 0.98f, 0.2f),
                Tire = CreateLitMaterial("M_Tire", new Color(0.03f, 0.03f, 0.03f), 0.35f, 0f),
                LampHead = CreateEmissiveMaterial("M_LampHead", new Color(1f, 0.82f, 0.6f), 6f),
                HeadLight = CreateEmissiveMaterial("M_HeadLight", new Color(0.95f, 0.97f, 1f), 10f),
                TailLight = CreateEmissiveMaterial("M_TailLight", new Color(1f, 0.05f, 0.03f), 5f),
                ShopWindow = CreateEmissiveMaterial("M_ShopWindow", new Color(1f, 0.78f, 0.52f), 2.2f),
                Neon = CreateEmissiveMaterial("M_NeonSign", new Color(0.3f, 0.75f, 1f), 7f),
                NeonPink = CreateEmissiveMaterial("M_NeonSignPink", new Color(1f, 0.25f, 0.6f), 7f),
            };
            CreateWindowTextures(Palette);

            ConfigurePipeline(Pipeline, Palette.Post);
            BuildScene(Palette);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// 场景搭建共用的材质与贴图
        /// </summary>
        private sealed class DemoPalette
        {
            public Material Rain;
            public Material Snow;
            public Material Post;
            public Material Occlusion;
            public Material Ground;
            public Material Concrete;
            public Material Metal;
            public Material Awning;
            public Material AwningAlt;
            public Material Umbrella;
            public Material UmbrellaAlt;
            public Material Coat;
            public Material CoatAlt;
            public Material Skin;
            public Material CarBody;
            public Material Glass;
            public Material Tire;
            public Material LampHead;
            public Material HeadLight;
            public Material TailLight;
            public Material ShopWindow;
            public Material Neon;
            public Material NeonPink;
            public Texture2D WindowTexture;
            public Texture2D WindowEmission;
        }

        #region Assets

        private static Material CreateMaterial(string name, string shaderName, string folder = MaterialFolder)
        {
            string path = $"{folder}/{name}.mat";
            var Shader = UnityEngine.Shader.Find(shaderName);
            if (Shader == null)
            {
                throw new System.InvalidOperationException($"找不到着色器 {shaderName}");
            }

            var Mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (Mat == null)
            {
                Mat = new Material(Shader);
                AssetDatabase.CreateAsset(Mat, path);
            }
            Mat.shader = Shader;
            EditorUtility.SetDirty(Mat);
            return Mat;
        }

        private static Material CreateLitMaterial(string name, Color color, float smoothness, float metallic, string folder = MaterialFolder)
        {
            Material mat = CreateMaterial(name, "Universal Render Pipeline/Lit", folder);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            return mat;
        }

        private static Material CreateEmissiveMaterial(string name, Color color, float intensity)
        {
            Material mat = CreateLitMaterial(name, color * 0.2f, 0.9f, 0f);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            mat.SetColor("_EmissionColor", color * intensity);
            return mat;
        }

        /// <summary>
        /// 生成楼体立面反照率与自发光贴图 每块 4 米见方 含 2 层 每层 3 扇窗 随机亮灯
        /// </summary>
        private static void CreateWindowTextures(DemoPalette palette)
        {
            const int size = 512;
            var Random = new System.Random(7);
            var Pixels = new Color32[size * size];
            var EmissionPixels = new Color32[size * size];
            var Wall = new Color32(58, 54, 52, 0);
            var Frame = new Color32(22, 22, 24, 0);
            for (int i = 0; i < Pixels.Length; i++)
            {
                Pixels[i] = Wall;
            }

            // 窗格 2 行 3 列 每窗内框留 8% 窗框
            for (int row = 0; row < 2; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    bool isLit = Random.NextDouble() < 0.45;
                    float warmth = (float)Random.NextDouble();
                    var Glass = isLit
                        ? new Color32((byte)(230 + warmth * 25), (byte)(170 + warmth * 40), (byte)(100 + warmth * 50), 255)
                        : new Color32(14, 17, 22, 0);
                    int x0 = col * size / 3 + size / 18;
                    int x1 = (col + 1) * size / 3 - size / 18;
                    int y0 = row * size / 2 + size / 10;
                    int y1 = (row + 1) * size / 2 - size / 14;
                    for (int y = y0; y < y1; y++)
                    {
                        for (int x = x0; x < x1; x++)
                        {
                            bool isFrame = x < x0 + 8 || x > x1 - 8 || y < y0 + 8 || y > y1 - 8 || Mathf.Abs(x - (x0 + x1) / 2) < 3;
                            // 亮窗下半部加窗帘渐变 避免整片纯色
                            float curtain = isLit ? Mathf.Lerp(0.55f, 1f, (float)(y - y0) / (y1 - y0)) : 1f;
                            var Pixel = isFrame ? Frame : Glass;
                            Pixels[y * size + x] = isFrame ? Frame : (isLit ? new Color32(40, 36, 30, 255) : Glass);
                            if (!isFrame && isLit)
                            {
                                EmissionPixels[y * size + x] = new Color32((byte)(Pixel.r * curtain), (byte)(Pixel.g * curtain), (byte)(Pixel.b * curtain), 255);
                            }
                        }
                    }
                }
            }
            palette.WindowTexture = SaveTexture($"{TextureFolder}/T_FacadeWindows.png", Pixels, size);
            palette.WindowEmission = SaveTexture($"{TextureFolder}/T_FacadeWindowsEmission.png", EmissionPixels, size);
        }

        private static Texture2D SaveTexture(string path, Color32[] pixels, int size)
        {
            var Texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Texture.SetPixels32(pixels);
            Texture.Apply();
            File.WriteAllBytes(path, Texture.EncodeToPNG());
            Object.DestroyImmediate(Texture);
            AssetDatabase.ImportAsset(path);

            var Importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Importer.wrapMode = TextureWrapMode.Repeat;
            Importer.mipmapEnabled = true;
            Importer.anisoLevel = 8;
            Importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>
        /// 每栋楼一份立面材质 平铺按楼体尺寸换算 保证窗户为真实尺寸
        /// </summary>
        private static Material CreateFacadeMaterial(DemoPalette palette, int index, Vector3 size, float hueShift)
        {
            var Tint = Color.HSVToRGB(Mathf.Repeat(0.08f + hueShift, 1f), 0.15f, 0.9f);
            Material mat = CreateLitMaterial($"M_Facade_{index:00}", Tint, 0.55f, 0f, FacadeFolder);
            var Tiling = new Vector2(Mathf.Max(size.x, size.z) / 4f, size.y / 4f);
            mat.SetTexture("_BaseMap", palette.WindowTexture);
            mat.SetTextureScale("_BaseMap", Tiling);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", palette.WindowEmission);
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.65f) * Mathf.Lerp(0.6f, 1.6f, hueShift * 3f));
            return mat;
        }

        /// <summary>
        /// 开启深度与不透明纹理 雨滴折射与后处理依赖 并把后处理 Feature 加入默认渲染器
        /// </summary>
        private static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline, Material postMat)
        {
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.supportsHDR = true;
            pipeline.maxAdditionalLightsCount = 8;
            EditorUtility.SetDirty(pipeline);

            var SerializedPipeline = new SerializedObject(pipeline);
            SerializedPipeline.FindProperty("m_AdditionalLightsRenderingMode").intValue = (int)LightRenderingMode.PerPixel;
            SerializedPipeline.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            SerializedPipeline.ApplyModifiedPropertiesWithoutUndo();

            var RendererData = SerializedPipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
            // 整条街一块地面 Forward 逐物体 8 盏灯上限会丢路灯 Forward+ 按屏幕分簇无此限制
            RendererData.renderingMode = RenderingMode.ForwardPlus;
            EditorUtility.SetDirty(RendererData);
            foreach (var Feature in RendererData.rendererFeatures)
            {
                if (Feature is TimeStopPostFeature)
                {
                    BindPostMaterial(Feature, postMat);
                    return;
                }
            }

            var PostFeature = ScriptableObject.CreateInstance<TimeStopPostFeature>();
            PostFeature.name = "TimeStopPost";
            BindPostMaterial(PostFeature, postMat);
            AssetDatabase.AddObjectToAsset(PostFeature, RendererData);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(PostFeature, out string _, out long localId);

            var SerializedRenderer = new SerializedObject(RendererData);
            var FeatureList = SerializedRenderer.FindProperty("m_RendererFeatures");
            var FeatureMapList = SerializedRenderer.FindProperty("m_RendererFeatureMap");
            FeatureList.arraySize++;
            FeatureList.GetArrayElementAtIndex(FeatureList.arraySize - 1).objectReferenceValue = PostFeature;
            FeatureMapList.arraySize++;
            FeatureMapList.GetArrayElementAtIndex(FeatureMapList.arraySize - 1).longValue = localId;
            SerializedRenderer.ApplyModifiedProperties();
            RendererData.SetDirty();
            EditorUtility.SetDirty(RendererData);
        }

        private static void BindPostMaterial(ScriptableRendererFeature feature, Material postMat)
        {
            var SerializedFeature = new SerializedObject(feature);
            SerializedFeature.FindProperty("postMaterial").objectReferenceValue = postMat;
            SerializedFeature.ApplyModifiedProperties();
        }

        #endregion

        #region Scene

        private static void BuildScene(DemoPalette palette)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 夜雨 雾浓 环境光极暗 画面亮度主要来自路灯 店铺 车灯
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.05f, 0.06f, 0.09f);
            RenderSettings.ambientEquatorColor = new Color(0.07f, 0.06f, 0.06f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.02f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.045f, 0.05f, 0.065f);
            RenderSettings.fogDensity = 0.028f;
            RenderSettings.skybox = null;

            var CameraObject = new GameObject("Main Camera");
            CameraObject.tag = "MainCamera";
            CameraObject.transform.SetPositionAndRotation(new Vector3(0.6f, 1.65f, -8f), Quaternion.Euler(6f, 0f, 0f));
            var Cam = CameraObject.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = RenderSettings.fogColor;
            Cam.nearClipPlane = 0.05f;
            Cam.farClipPlane = 200f;
            Cam.fieldOfView = 50f;
            Cam.allowHDR = true;
            var CameraData = CameraObject.AddComponent<UniversalAdditionalCameraData>();
            CameraData.renderPostProcessing = true;
            CameraData.requiresDepthTexture = true;
            CameraData.requiresColorTexture = true;
            CameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            CameraData.antialiasingQuality = AntialiasingQuality.High;
            CameraObject.AddComponent<DemoFlyCamera>();

            var MoonLight = new GameObject("Moon Light").AddComponent<Light>();
            MoonLight.type = LightType.Directional;
            MoonLight.color = new Color(0.55f, 0.65f, 0.9f);
            MoonLight.intensity = 0.06f;
            MoonLight.shadows = LightShadows.Soft;
            MoonLight.transform.rotation = Quaternion.Euler(55f, -35f, 0f);

            var EnvironmentObject = new GameObject("RainEnvironment");
            var Environment = EnvironmentObject.AddComponent<RainEnvironment>();
            SetField(Environment, "rainCompute", LoadCompute());
            SetField(Environment, "occlusionMaterial", palette.Occlusion);

            var Reflection = EnvironmentObject.AddComponent<PlanarReflection>();
            var SerializedReflection = new SerializedObject(Reflection);
            SerializedReflection.FindProperty("reflectionMask").intValue = ~(1 << GroundLayer);
            SerializedReflection.ApplyModifiedPropertiesWithoutUndo();

            var Street = new GameObject("Street").transform;
            BuildGround(palette, Street);
            BuildBuildings(palette, Street);
            BuildStreetLamps(palette, Street);
            BuildShopFronts(palette, Street);
            var Pedestrians = new GameObject("Pedestrians").transform;
            BuildPedestrians(palette, Pedestrians);
            BuildCars(palette, Pedestrians);
            CreatePostVolume();

            var FieldObject = new GameObject("LocalTimeField");
            FieldObject.transform.position = new Vector3(0f, 1f, 0f);
            var Field = FieldObject.AddComponent<LocalTimeField>();
            var SerializedField = new SerializedObject(Field);
            SerializedField.FindProperty("radius").floatValue = 9f;
            SerializedField.FindProperty("falloff").floatValue = 1.5f;
            SerializedField.ApplyModifiedPropertiesWithoutUndo();

            GameObject rainRoot = CreatePrecipitation("Rain", palette.Rain, 131072, false, 7, 1f, 0f, 0.6f, new Vector3(22f, 18f, 22f));
            GameObject snowRoot = CreatePrecipitation("Snow", palette.Snow, 49152, true, 1, 1.8f, 0.5f, 1.2f, new Vector3(22f, 18f, 22f));
            snowRoot.SetActive(false);

            var Caster = CameraObject.AddComponent<TimeStopCaster>();
            SetField(Caster, "timeField", Field);
            SetField(Caster, "rainRoot", rainRoot);
            SetField(Caster, "snowRoot", snowRoot);
            var SerializedCaster = new SerializedObject(Caster);
            SerializedCaster.FindProperty("castOffset").vector3Value = new Vector3(0f, -0.65f, 6f);
            SerializedCaster.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static ComputeShader LoadCompute() => AssetDatabase.LoadAssetAtPath<ComputeShader>($"{RootFolder}/Shaders/RainSimulation.compute");

        private static void BuildGround(DemoPalette palette, Transform parent)
        {
            float length = StreetEnd - StreetStart;
            float centerZ = (StreetStart + StreetEnd) * 0.5f;
            var Ground = CreateBox("Ground", parent, new Vector3(0f, -0.05f, centerZ), new Vector3(FacadeOffset * 2f + 2f, 0.1f, length), palette.Ground);
            Ground.layer = GroundLayer;

            // 两侧路缘石 道路中间是行人步行街 不设机动车道高差
            CreateBox("Curb_L", parent, new Vector3(-FacadeOffset + 1.2f, 0.06f, centerZ), new Vector3(0.25f, 0.12f, length), palette.Concrete);
            CreateBox("Curb_R", parent, new Vector3(FacadeOffset - 1.2f, 0.06f, centerZ), new Vector3(0.25f, 0.12f, length), palette.Concrete);
        }

        private static void BuildBuildings(DemoPalette palette, Transform parent)
        {
            var Random = new System.Random(11);
            int index = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                float z = StreetStart;
                while (z < StreetEnd)
                {
                    float width = 6f + (float)Random.NextDouble() * 6f;
                    float height = 10f + (float)Random.NextDouble() * 18f;
                    float depth = 10f;
                    var Size = new Vector3(depth, height, width);
                    var Position = new Vector3(side * (FacadeOffset + depth * 0.5f), height * 0.5f, z + width * 0.5f);
                    Material facade = CreateFacadeMaterial(palette, index, new Vector3(width, height, width), (float)Random.NextDouble() * 0.3f);
                    CreateBox($"Building_{index:00}", parent, Position, Size, facade);

                    // 楼顶女儿墙 打破盒子轮廓
                    CreateBox($"Parapet_{index:00}", parent, Position + new Vector3(0f, height * 0.5f + 0.4f, 0f), new Vector3(depth + 0.3f, 0.8f, width + 0.3f), palette.Concrete);
                    index++;
                    z += width + 0.4f;
                }
            }

            // 街道尽头一栋楼封口 远景不露空
            Material endFacade = CreateFacadeMaterial(palette, index, new Vector3(FacadeOffset * 2f, 30f, 1f), 0.12f);
            CreateBox("Building_End", parent, new Vector3(0f, 15f, StreetEnd + 6f), new Vector3(FacadeOffset * 2f + 20f, 30f, 10f), endFacade);
        }

        private static void BuildStreetLamps(DemoPalette palette, Transform parent)
        {
            int index = 0;
            for (float z = StreetStart + 4f; z < StreetEnd; z += 9f)
            {
                float side = index % 2 == 0 ? -1f : 1f;
                var Root = new GameObject($"StreetLamp_{index:00}").transform;
                Root.SetParent(parent);
                Root.position = new Vector3(side * (FacadeOffset - 1.7f), 0f, z);
                CreateCylinder("Pole", Root, new Vector3(0f, 2.4f, 0f), new Vector3(0.12f, 2.4f, 0.12f), palette.Metal);
                CreateBox("Arm", Root, new Vector3(-side * 0.6f, 4.75f, 0f), new Vector3(1.3f, 0.08f, 0.08f), palette.Metal);
                CreateBox("Head", Root, new Vector3(-side * 1.15f, 4.65f, 0f), new Vector3(0.5f, 0.12f, 0.28f), palette.Metal);
                CreateBox("Bulb", Root, new Vector3(-side * 1.15f, 4.58f, 0f), new Vector3(0.42f, 0.03f, 0.2f), palette.LampHead);

                // 聚光灯照地 给雨丝一个可见光锥 另加弱点光模拟灯罩散射
                var Spot = CreateLight("Spot", Root, new Vector3(-side * 1.15f, 4.5f, 0f), LightType.Spot, new Color(1f, 0.78f, 0.55f), 9f, 14f);
                Spot.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                Spot.spotAngle = 105f;
                Spot.innerSpotAngle = 60f;
                Spot.shadows = LightShadows.Soft;
                Spot.gameObject.AddComponent<RainLight>();
                var Glow = CreateLight("Glow", Root, new Vector3(-side * 1.15f, 4.3f, 0f), LightType.Point, new Color(1f, 0.8f, 0.6f), 0.8f, 5f);
                Glow.gameObject.AddComponent<RainLight>();
                index++;
            }
        }

        private static void BuildShopFronts(DemoPalette palette, Transform parent)
        {
            var Random = new System.Random(23);
            int index = 0;
            foreach (float side in new[] { -1f, 1f })
            {
                for (float z = StreetStart + 2f; z < StreetEnd - 3f; z += 7.5f)
                {
                    if (Random.NextDouble() < 0.25)
                    {
                        continue;
                    }
                    var Root = new GameObject($"Shop_{index:00}").transform;
                    Root.SetParent(parent);
                    Root.position = new Vector3(side * FacadeOffset, 0f, z + 2.5f);

                    CreateBox("Window", Root, new Vector3(-side * 0.02f, 1.6f, 0f), new Vector3(0.05f, 2.6f, 4.2f), palette.ShopWindow);
                    // 雨棚 前低后高 登记为挡雨物 雨滴在棚面上溅开
                    var Awning = CreateBox("Awning", Root, new Vector3(-side * 0.9f, 3.25f, 0f), new Vector3(1.8f, 0.06f, 4.6f), Random.NextDouble() < 0.5 ? palette.Awning : palette.AwningAlt);
                    Awning.transform.localRotation = Quaternion.Euler(0f, 0f, side * 12f);
                    Awning.AddComponent<RainOccluder>();

                    if (Random.NextDouble() < 0.6)
                    {
                        CreateBox("Neon", Root, new Vector3(-side * 0.08f, 4.1f, 0f), new Vector3(0.06f, 0.35f, 2.4f), Random.NextDouble() < 0.5 ? palette.Neon : palette.NeonPink);
                    }

                    // 店内暖光透出 打在门前湿地面上
                    var ShopLight = CreateLight("ShopLight", Root, new Vector3(-side * 1.2f, 2.4f, 0f), LightType.Point, new Color(1f, 0.75f, 0.5f), 2.2f, 6f);
                    ShopLight.gameObject.AddComponent<RainLight>();
                    index++;
                }
            }
        }

        /// <summary>
        /// 打伞路人 胶囊身体加摆动四肢 伞面登记挡雨 部分人穿过时停区域以展示定格姿态
        /// </summary>
        private static void BuildPedestrians(DemoPalette palette, Transform parent)
        {
            var Random = new System.Random(37);
            for (int i = 0; i < 14; i++)
            {
                float x = Mathf.Lerp(-4.2f, 4.2f, (float)Random.NextDouble());
                float z = Mathf.Lerp(StreetStart + 4f, StreetEnd - 8f, (float)Random.NextDouble());
                bool isForward = Random.NextDouble() < 0.5;
                float travelLength = 10f + (float)Random.NextDouble() * 14f;
                var Root = new GameObject($"Pedestrian_{i:00}").transform;
                Root.SetParent(parent);
                Root.position = new Vector3(x, 0f, z);

                Material coat = Random.NextDouble() < 0.6 ? palette.Coat : palette.CoatAlt;
                float height = Mathf.Lerp(1.6f, 1.85f, (float)Random.NextDouble());
                float scale = height / 1.75f;
                CreateCapsule("Body", Root, new Vector3(0f, 1.15f * scale, 0f), new Vector3(0.42f, 0.42f, 0.3f) * scale, coat);
                CreateSphere("Head", Root, new Vector3(0f, 1.62f * scale, 0f), Vector3.one * 0.21f * scale, palette.Skin);

                var LegL = CreateLimb("Leg_L", Root, new Vector3(-0.1f, 0.82f, 0f) * scale, 0.8f * scale, 0.13f * scale, palette.Coat);
                var LegR = CreateLimb("Leg_R", Root, new Vector3(0.1f, 0.82f, 0f) * scale, 0.8f * scale, 0.13f * scale, palette.Coat);
                var ArmL = CreateLimb("Arm_L", Root, new Vector3(-0.26f, 1.42f, 0f) * scale, 0.6f * scale, 0.09f * scale, coat);

                // 右手举伞 不参与摆臂
                var ArmR = CreateLimb("Arm_R", Root, new Vector3(0.26f, 1.42f, 0f) * scale, 0.5f * scale, 0.09f * scale, coat);
                ArmR.localRotation = Quaternion.Euler(-150f, 0f, 10f);
                var Umbrella = new GameObject("Umbrella").transform;
                Umbrella.SetParent(Root, false);
                Umbrella.localPosition = new Vector3(0.12f, 1.95f * scale, 0.05f);
                Umbrella.localRotation = Quaternion.Euler(-8f, 0f, -6f);
                CreateCylinder("Shaft", Umbrella, new Vector3(0f, -0.25f, 0f), new Vector3(0.02f, 0.4f, 0.02f), palette.Metal);
                var Canopy = CreateSphere("Canopy", Umbrella, new Vector3(0f, 0.15f, 0f), new Vector3(1.05f, 0.32f, 1.05f), Random.NextDouble() < 0.7 ? palette.Umbrella : palette.UmbrellaAlt);
                Canopy.AddComponent<RainOccluder>();

                var Walker = Root.gameObject.AddComponent<LocalTimeWalker>();
                var SerializedWalker = new SerializedObject(Walker);
                SerializedWalker.FindProperty("travel").vector3Value = new Vector3(0f, 0f, isForward ? travelLength : -travelLength);
                SerializedWalker.FindProperty("speed").floatValue = Mathf.Lerp(1.0f, 1.5f, (float)Random.NextDouble());
                var LimbList = SerializedWalker.FindProperty("limbList");
                LimbList.arraySize = 3;
                LimbList.GetArrayElementAtIndex(0).objectReferenceValue = LegL;
                LimbList.GetArrayElementAtIndex(1).objectReferenceValue = LegR;
                LimbList.GetArrayElementAtIndex(2).objectReferenceValue = ArmL;
                SerializedWalker.FindProperty("wheelList").arraySize = 0;
                SerializedWalker.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>
        /// 低速行驶车辆 车灯是雨丝最强的照明 穿过时停区域时连车灯光柱里的雨一起定住
        /// </summary>
        private static void BuildCars(DemoPalette palette, Transform parent)
        {
            var CarSpecList = new[]
            {
                (x: -2.2f, z: StreetStart + 2f, forward: true),
                (x: 2.2f, z: StreetEnd - 10f, forward: false),
            };
            for (int i = 0; i < CarSpecList.Length; i++)
            {
                var Spec = CarSpecList[i];
                var Root = new GameObject($"Car_{i}").transform;
                Root.SetParent(parent);
                Root.position = new Vector3(Spec.x, 0f, Spec.z);

                var Body = CreateBox("Body", Root, new Vector3(0f, 0.62f, 0f), new Vector3(1.8f, 0.65f, 4.4f), palette.CarBody);
                Body.AddComponent<RainOccluder>();
                var Cabin = CreateBox("Cabin", Root, new Vector3(0f, 1.18f, -0.25f), new Vector3(1.6f, 0.55f, 2.3f), palette.Glass);
                Cabin.AddComponent<RainOccluder>();
                CreateBox("Roof", Root, new Vector3(0f, 1.47f, -0.25f), new Vector3(1.62f, 0.04f, 2.0f), palette.CarBody);
                CreateBox("HeadLight_L", Root, new Vector3(-0.65f, 0.7f, 2.21f), new Vector3(0.35f, 0.12f, 0.02f), palette.HeadLight);
                CreateBox("HeadLight_R", Root, new Vector3(0.65f, 0.7f, 2.21f), new Vector3(0.35f, 0.12f, 0.02f), palette.HeadLight);
                CreateBox("TailLight_L", Root, new Vector3(-0.7f, 0.75f, -2.21f), new Vector3(0.3f, 0.1f, 0.02f), palette.TailLight);
                CreateBox("TailLight_R", Root, new Vector3(0.7f, 0.75f, -2.21f), new Vector3(0.3f, 0.1f, 0.02f), palette.TailLight);

                var WheelList = new Transform[4];
                for (int w = 0; w < 4; w++)
                {
                    float wx = w % 2 == 0 ? -0.85f : 0.85f;
                    float wz = w < 2 ? 1.4f : -1.4f;
                    WheelList[w] = CreateCylinder($"Wheel_{w}", Root, new Vector3(wx, 0.32f, wz), new Vector3(0.64f, 0.12f, 0.64f), palette.Tire).transform;
                    WheelList[w].localRotation = Quaternion.Euler(0f, 0f, 90f);
                }

                foreach (float lx in new[] { -0.65f, 0.65f })
                {
                    var Beam = CreateLight("HeadBeam", Root, new Vector3(lx, 0.7f, 2.3f), LightType.Spot, new Color(0.92f, 0.95f, 1f), 18f, 28f);
                    Beam.transform.localRotation = Quaternion.Euler(6f, 0f, 0f);
                    Beam.spotAngle = 48f;
                    Beam.innerSpotAngle = 20f;
                    Beam.shadows = LightShadows.Soft;
                    Beam.gameObject.AddComponent<RainLight>();
                }
                var Tail = CreateLight("TailGlow", Root, new Vector3(0f, 0.8f, -2.5f), LightType.Point, new Color(1f, 0.08f, 0.05f), 1.2f, 4f);
                Tail.gameObject.AddComponent<RainLight>();

                var Walker = Root.gameObject.AddComponent<LocalTimeWalker>();
                var SerializedWalker = new SerializedObject(Walker);
                float travelLength = StreetEnd - StreetStart - 14f;
                SerializedWalker.FindProperty("travel").vector3Value = new Vector3(0f, 0f, Spec.forward ? travelLength : -travelLength);
                SerializedWalker.FindProperty("speed").floatValue = 3.2f;
                SerializedWalker.FindProperty("limbList").arraySize = 0;
                var WheelProperty = SerializedWalker.FindProperty("wheelList");
                WheelProperty.arraySize = WheelList.Length;
                for (int w = 0; w < WheelList.Length; w++)
                {
                    WheelProperty.GetArrayElementAtIndex(w).objectReferenceValue = WheelList[w];
                }
                SerializedWalker.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void CreatePostVolume()
        {
            string profilePath = $"{RootFolder}/Scenes/RaindropStopVolume.asset";
            AssetDatabase.DeleteAsset(profilePath);
            var VolumeObject = new GameObject("Global Volume");
            var GlobalVolume = VolumeObject.AddComponent<Volume>();
            GlobalVolume.isGlobal = true;
            var Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(Profile, profilePath);

            // 雨夜灯光在潮湿空气里有明显光晕 bloom 阈值压低 散射拉大
            var BloomSetting = Profile.Add<Bloom>(true);
            BloomSetting.intensity.Override(1.1f);
            BloomSetting.threshold.Override(0.85f);
            BloomSetting.scatter.Override(0.82f);
            var Tone = Profile.Add<Tonemapping>(true);
            Tone.mode.Override(TonemappingMode.ACES);
            var Adjust = Profile.Add<ColorAdjustments>(true);
            Adjust.postExposure.Override(0.6f);
            Adjust.contrast.Override(12f);
            Adjust.saturation.Override(-8f);
            var Balance = Profile.Add<WhiteBalance>(true);
            Balance.temperature.Override(-12f);
            var Vignette = Profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            Vignette.intensity.Override(0.28f);
            var Grain = Profile.Add<FilmGrain>(true);
            Grain.intensity.Override(0.18f);
            GlobalVolume.sharedProfile = Profile;
            AssetDatabase.SaveAssets();
        }

        private static GameObject CreatePrecipitation(string name, Material mat, int count, bool isSnow, int quadsPerDrop, float windMultiplier, float flutter, float splashDuration, Vector3 extents)
        {
            var Root = new GameObject(name);
            var Rain = Root.AddComponent<GpuRainSystem>();
            SetField(Rain, "rainCompute", LoadCompute());
            SetField(Rain, "rainMaterial", mat);
            var SerializedRain = new SerializedObject(Rain);
            SerializedRain.FindProperty("dropCount").intValue = count;
            SerializedRain.FindProperty("isSnow").boolValue = isSnow;
            SerializedRain.FindProperty("quadsPerDrop").intValue = quadsPerDrop;
            SerializedRain.FindProperty("windMultiplier").floatValue = windMultiplier;
            SerializedRain.FindProperty("flutter").floatValue = flutter;
            SerializedRain.FindProperty("splashDuration").floatValue = splashDuration;
            SerializedRain.FindProperty("emitterExtents").vector3Value = extents;
            SerializedRain.ApplyModifiedPropertiesWithoutUndo();
            mat.SetFloat("_SplashDuration", splashDuration);
            return Root;
        }

        #endregion

        #region Primitive

        private static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat)
        {
            var Item = GameObject.CreatePrimitive(type);
            Item.name = name;
            Item.transform.SetParent(parent, false);
            Item.transform.localPosition = localPosition;
            Item.transform.localScale = localScale;
            Item.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return Item;
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat) =>
            CreatePrimitive(PrimitiveType.Cube, name, parent, localPosition, localScale, mat);

        private static GameObject CreateSphere(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat) =>
            CreatePrimitive(PrimitiveType.Sphere, name, parent, localPosition, localScale, mat);

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat) =>
            CreatePrimitive(PrimitiveType.Cylinder, name, parent, localPosition, localScale, mat);

        private static GameObject CreateCapsule(string name, Transform parent, Vector3 localPosition, Vector3 localScale, Material mat) =>
            CreatePrimitive(PrimitiveType.Capsule, name, parent, localPosition, localScale, mat);

        /// <summary>
        /// 肢体枢轴在关节处 子网格向下延伸 旋转枢轴即可摆动
        /// </summary>
        private static Transform CreateLimb(string name, Transform parent, Vector3 jointPosition, float length, float thickness, Material mat)
        {
            var Pivot = new GameObject(name).transform;
            Pivot.SetParent(parent, false);
            Pivot.localPosition = jointPosition;
            var Mesh = CreateCapsule("Mesh", Pivot, new Vector3(0f, -length * 0.5f, 0f), new Vector3(thickness, length * 0.5f, thickness), mat);
            Object.DestroyImmediate(Mesh.GetComponent<Collider>());
            return Pivot;
        }

        private static Light CreateLight(string name, Transform parent, Vector3 localPosition, LightType type, Color color, float intensity, float range)
        {
            var Item = new GameObject(name).AddComponent<Light>();
            Item.transform.SetParent(parent, false);
            Item.transform.localPosition = localPosition;
            Item.type = type;
            Item.color = color;
            Item.intensity = intensity;
            Item.range = range;
            return Item;
        }

        private static void SetField(Object target, string fieldName, Object value)
        {
            var SerializedTarget = new SerializedObject(target);
            SerializedTarget.FindProperty(fieldName).objectReferenceValue = value;
            SerializedTarget.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
