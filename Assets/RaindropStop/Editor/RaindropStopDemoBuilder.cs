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
    /// 一键生成局部时停演示 材质 小球预制体 雨夜场景 并把后处理挂到当前 URP 渲染器
    /// 重复执行覆盖生成的资源
    /// </summary>
    public static class RaindropStopDemoBuilder
    {
        private const string RootFolder = "Assets/RaindropStop";

        private const string MaterialFolder = RootFolder + "/Materials";

        private const string PrefabFolder = RootFolder + "/Prefabs";

        private const string ScenePath = RootFolder + "/Scenes/RaindropStop.unity";

        private const string BallPrefabPath = PrefabFolder + "/TimeBall.prefab";

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

            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            Material rainMat = CreateMaterial("M_RainDrop", "RaindropStop/RainDrop");
            Material snowMat = CreateMaterial("M_SnowFlake", "RaindropStop/SnowFlake");
            Material postMat = CreateMaterial("M_TimeStopPost", "Hidden/RaindropStop/TimeStopPost");
            Material groundMat = CreateLitMaterial("M_WetGround", new Color(0.09f, 0.1f, 0.12f), 0.85f);
            Material propMat = CreateLitMaterial("M_Prop", new Color(0.35f, 0.38f, 0.45f), 0.4f);
            Material ballMat = CreateLitMaterial("M_Ball", new Color(1f, 0.45f, 0.15f), 0.7f);
            ballMat.EnableKeyword("_EMISSION");
            ballMat.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.1f) * 1.5f);

            ConfigurePipeline(Pipeline, postMat);
            GameObject ballPrefab = CreateBallPrefab(ballMat);
            BuildScene(rainMat, snowMat, groundMat, propMat, ballPrefab);
            AssetDatabase.SaveAssets();
        }

        private static Material CreateMaterial(string name, string shaderName)
        {
            string path = $"{MaterialFolder}/{name}.mat";
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

        private static Material CreateLitMaterial(string name, Color color, float smoothness)
        {
            Material mat = CreateMaterial(name, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        /// <summary>
        /// 开启深度与不透明纹理 雨滴折射与后处理依赖 并把后处理 Feature 加入默认渲染器
        /// </summary>
        private static void ConfigurePipeline(UniversalRenderPipelineAsset pipeline, Material postMat)
        {
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            EditorUtility.SetDirty(pipeline);

            var SerializedPipeline = new SerializedObject(pipeline);
            var RendererData = SerializedPipeline.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue as UniversalRendererData;
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

        private static GameObject CreateBallPrefab(Material ballMat)
        {
            var Ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Ball.name = "TimeBall";
            Ball.transform.localScale = Vector3.one * 0.3f;
            Ball.GetComponent<MeshRenderer>().sharedMaterial = ballMat;
            var Body = Ball.AddComponent<Rigidbody>();
            Body.mass = 0.2f;
            Body.interpolation = RigidbodyInterpolation.Interpolate;
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            Ball.AddComponent<LocalTimeBody>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(Ball, BallPrefabPath);
            Object.DestroyImmediate(Ball);
            return prefab;
        }

        #region Scene

        private static void BuildScene(Material rainMat, Material snowMat, Material groundMat, Material propMat, GameObject ballPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.24f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.07f, 0.08f, 0.11f);
            RenderSettings.fogDensity = 0.035f;

            var CameraObject = new GameObject("Main Camera");
            CameraObject.tag = "MainCamera";
            CameraObject.transform.SetPositionAndRotation(new Vector3(0f, 1.7f, -7f), Quaternion.Euler(8f, 0f, 0f));
            var Cam = CameraObject.AddComponent<Camera>();
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = RenderSettings.fogColor;
            Cam.nearClipPlane = 0.05f;
            Cam.fieldOfView = 55f;
            var CameraData = CameraObject.AddComponent<UniversalAdditionalCameraData>();
            CameraData.renderPostProcessing = true;
            CameraData.requiresDepthTexture = true;
            CameraData.requiresColorTexture = true;
            CameraObject.AddComponent<DemoFlyCamera>();

            var MoonLight = new GameObject("Moon Light").AddComponent<Light>();
            MoonLight.type = LightType.Directional;
            MoonLight.color = new Color(0.6f, 0.7f, 1f);
            MoonLight.intensity = 0.5f;
            MoonLight.shadows = LightShadows.Soft;
            MoonLight.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            BuildStreet(groundMat, propMat);
            CreatePostVolume();

            var FieldObject = new GameObject("LocalTimeField");
            FieldObject.transform.position = new Vector3(0f, 1f, 0f);
            var Field = FieldObject.AddComponent<LocalTimeField>();

            GameObject rainRoot = CreatePrecipitation("Rain", rainMat, 65536, 9f, new Vector3(0.8f, 0f, 0.3f), 0f, 0.28f);
            GameObject snowRoot = CreatePrecipitation("Snow", snowMat, 32768, 1.4f, new Vector3(0.4f, 0f, 0.15f), 0.6f, 0.8f);
            snowRoot.SetActive(false);

            var Launcher = new GameObject("BallLauncher");
            Launcher.transform.SetPositionAndRotation(new Vector3(-9f, 1.6f, 0.5f), Quaternion.Euler(-12f, 90f, 0f));
            var LauncherComp = Launcher.AddComponent<DemoBallLauncher>();
            SetField(LauncherComp, "ballPrefab", ballPrefab.GetComponent<LocalTimeBody>());

            var Caster = CameraObject.AddComponent<TimeStopCaster>();
            SetField(Caster, "timeField", Field);
            SetField(Caster, "rainRoot", rainRoot);
            SetField(Caster, "snowRoot", snowRoot);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        private static void BuildStreet(Material groundMat, Material propMat)
        {
            var Ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Ground.name = "Ground";
            Ground.transform.localScale = new Vector3(8f, 1f, 8f);
            Ground.GetComponent<MeshRenderer>().sharedMaterial = groundMat;

            // 两侧立柱与路灯 给时停区域提供深度参照 让边界环在物体表面上可见
            for (int i = 0; i < 6; i++)
            {
                float z = -6f + i * 5f;
                CreateProp(new Vector3(-4.5f, 2f, z), new Vector3(0.5f, 4f, 0.5f), propMat);
                CreateProp(new Vector3(4.5f, 2f, z + 2.5f), new Vector3(0.5f, 4f, 0.5f), propMat);
                var Lamp = new GameObject($"StreetLamp_{i}").AddComponent<Light>();
                Lamp.type = LightType.Point;
                Lamp.color = new Color(1f, 0.75f, 0.45f);
                Lamp.range = 9f;
                Lamp.intensity = 2.2f;
                Lamp.transform.position = new Vector3(i % 2 == 0 ? -3.8f : 3.8f, 4.2f, z + (i % 2 == 0 ? 0f : 2.5f));
            }

            CreateProp(new Vector3(1.5f, 0.6f, 1.5f), new Vector3(1.2f, 1.2f, 1.2f), propMat);
            CreateProp(new Vector3(-1.8f, 0.4f, -1f), new Vector3(2.5f, 0.8f, 0.8f), propMat);
        }

        private static void CreateProp(Vector3 position, Vector3 scale, Material mat)
        {
            var Prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Prop.name = "Prop";
            Prop.transform.position = position;
            Prop.transform.localScale = scale;
            Prop.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        private static void CreatePostVolume()
        {
            var VolumeObject = new GameObject("Global Volume");
            var GlobalVolume = VolumeObject.AddComponent<Volume>();
            GlobalVolume.isGlobal = true;
            var Profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(Profile, $"{RootFolder}/Scenes/RaindropStopVolume.asset");
            var BloomSetting = Profile.Add<Bloom>(true);
            BloomSetting.intensity.Override(0.8f);
            BloomSetting.threshold.Override(1f);
            var Tone = Profile.Add<Tonemapping>(true);
            Tone.mode.Override(TonemappingMode.ACES);
            var Vignette = Profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            Vignette.intensity.Override(0.3f);
            GlobalVolume.sharedProfile = Profile;
        }

        private static GameObject CreatePrecipitation(string name, Material mat, int count, float fallSpeed, Vector3 wind, float flutter, float splashDuration)
        {
            var Root = new GameObject(name);
            var Rain = Root.AddComponent<GpuRainSystem>();
            var Compute = AssetDatabase.LoadAssetAtPath<ComputeShader>($"{RootFolder}/Shaders/RainSimulation.compute");
            SetField(Rain, "rainCompute", Compute);
            SetField(Rain, "rainMaterial", mat);
            var SerializedRain = new SerializedObject(Rain);
            SerializedRain.FindProperty("dropCount").intValue = count;
            SerializedRain.FindProperty("fallSpeed").floatValue = fallSpeed;
            SerializedRain.FindProperty("wind").vector3Value = wind;
            SerializedRain.FindProperty("flutter").floatValue = flutter;
            SerializedRain.FindProperty("splashDuration").floatValue = splashDuration;
            SerializedRain.ApplyModifiedPropertiesWithoutUndo();
            return Root;
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
