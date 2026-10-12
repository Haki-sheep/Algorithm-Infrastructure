using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HitImpact.Editor
{
    /// <summary>
    /// 一键搭建水墨打击感演示 材质 粒子 Prefab 2D 场景 3D 场景
    /// 重复执行会覆盖生成的资源 场景与 Prefab 的参数可在生成后于 Inspector 中继续调整
    /// </summary>
    public static class HitImpactDemoBuilder
    {
        /// <summary> 演示根目录 </summary>
        private const string RootFolder = "Assets/HitImpact";

        /// <summary> 材质目录 </summary>
        private const string MaterialFolder = RootFolder + "/Materials";

        /// <summary> Prefab 目录 </summary>
        private const string PrefabFolder = RootFolder + "/Prefabs";

        /// <summary> 场景目录 </summary>
        private const string SceneFolder = RootFolder + "/Scenes";

        /// <summary> Kenney 素材目录 </summary>
        private const string ArtFolder = RootFolder + "/Art/Kenney";

        /// <summary> 水墨冲击特效 Prefab 路径 </summary>
        private const string ImpactFxPrefabPath = PrefabFolder + "/FX_InkImpact.prefab";

        /// <summary> 2D 演示场景路径 </summary>
        public const string Scene2DPath = SceneFolder + "/HitImpact2D.unity";

        /// <summary> 3D 演示场景路径 </summary>
        public const string Scene3DPath = SceneFolder + "/HitImpact3D.unity";

        /// <summary> 角色主色 后处理黑白化时保留 </summary>
        private static readonly Color KeepBlue = new Color(0.16f, 0.32f, 0.95f);

        /// <summary>
        /// 生成全部资源与两个演示场景 完成后打开 3D 场景
        /// </summary>
        [MenuItem("Tools/HitImpact/Build Demo")]
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

            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(SceneFolder);

            HitImpactTextureGenerator.GenerateAll();
            ConfigureSprites();
            var materials = CreateMaterials();
            GameObject impactFxPrefab = CreateImpactFxPrefab(materials);
            Build2DScene(materials, impactFxPrefab);
            Build3DScene(materials, impactFxPrefab);
            AssetDatabase.SaveAssets();
        }

        #region Assets

        private sealed class DemoMaterials
        {
            /// <summary> 相机后处理 </summary>
            public Material Post;

            /// <summary> 笔触粒子 </summary>
            public Material InkStroke;

            /// <summary> 墨点粒子 </summary>
            public Material InkSplat;

            /// <summary> 角色主色 </summary>
            public Material ToonBlue;

            /// <summary> 头发与裤子 </summary>
            public Material ToonNavy;

            /// <summary> 皮肤与拳头 </summary>
            public Material ToonSkin;

            /// <summary> 地面 </summary>
            public Material ToonWhite;

            /// <summary> 沙袋 </summary>
            public Material ToonDummy;
        }

        private static void ConfigureSprites()
        {
            foreach (string spriteName in new[] { "Adventurer_Attack", "Adventurer_Idle", "Adventurer_Fist" })
            {
                string path = $"{ArtFolder}/{spriteName}.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 100f;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        private static DemoMaterials CreateMaterials()
        {
            var jitterNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(HitImpactTextureGenerator.JitterNoisePath);
            var erosionNoise = AssetDatabase.LoadAssetAtPath<Texture2D>(HitImpactTextureGenerator.ErosionNoisePath);
            var strokeTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(HitImpactTextureGenerator.StrokePath);
            var splatTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(HitImpactTextureGenerator.SplatPath);

            var materials = new DemoMaterials();
            materials.Post = CreateMaterial("M_ImpactPost", "HitImpact/ImpactPost");
            materials.Post.SetTexture("_NoiseTex", jitterNoise);
            materials.Post.SetColor("_KeepColor", KeepBlue);
            materials.Post.SetFloat("_Contrast", 1.9f);

            materials.InkStroke = CreateMaterial("M_InkStroke", "HitImpact/InkParticle");
            materials.InkStroke.SetTexture("_MainTex", strokeTexture);
            materials.InkStroke.SetTexture("_NoiseTex", erosionNoise);
            materials.InkStroke.SetVector("_NoiseScale", new Vector4(1.5f, 0.35f, 0f, 0f));
            materials.InkStroke.SetFloat("_ErosionBias", 0.45f);

            materials.InkSplat = CreateMaterial("M_InkSplat", "HitImpact/InkParticle");
            materials.InkSplat.SetTexture("_MainTex", splatTexture);
            materials.InkSplat.SetTexture("_NoiseTex", erosionNoise);
            materials.InkSplat.SetVector("_NoiseScale", new Vector4(0.8f, 0.8f, 0f, 0f));
            materials.InkSplat.SetFloat("_ErosionBias", 0.3f);

            materials.ToonBlue = CreateToon("M_Toon_Blue", new Color(0.2f, 0.36f, 0.95f), new Color(0.35f, 0.4f, 0.75f));
            materials.ToonNavy = CreateToon("M_Toon_Navy", new Color(0.12f, 0.16f, 0.45f), new Color(0.5f, 0.5f, 0.75f));
            materials.ToonSkin = CreateToon("M_Toon_Skin", new Color(1f, 0.86f, 0.8f), new Color(0.85f, 0.65f, 0.7f));
            materials.ToonWhite = CreateToon("M_Toon_White", new Color(0.96f, 0.96f, 0.97f), new Color(0.78f, 0.8f, 0.88f));
            materials.ToonDummy = CreateToon("M_Toon_Dummy", new Color(0.85f, 0.75f, 0.6f), new Color(0.65f, 0.55f, 0.6f));
            return materials;
        }

        private static Material CreateToon(string materialName, Color baseColor, Color shadeColor)
        {
            Material material = CreateMaterial(materialName, "HitImpact/SmearToon");
            material.SetColor("_Color", baseColor);
            material.SetColor("_ShadeColor", shadeColor);
            return material;
        }

        private static Material CreateMaterial(string materialName, string shaderName)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new System.InvalidOperationException($"找不到着色器 {shaderName} 请确认 Shader 已编译");
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        #endregion

        #region Impact Fx

        /// <summary>
        /// 特效以 2D 正交相机 10 单位画面高度为基准制作 本地 XY 平面朝向相机
        /// 场景中通过根节点缩放适配其他画面尺寸
        /// </summary>
        private static GameObject CreateImpactFxPrefab(DemoMaterials materials)
        {
            var root = new GameObject("FX_InkImpact");

            ParticleSystem radialStrokes = CreateParticle(root.transform, "RadialStrokes", materials.InkStroke, 20);
            ConfigureBurst(radialStrokes, 26, new Vector2(0.22f, 0.42f), new Vector2(22f, 40f), new Vector2(0.35f, 0.9f));
            ConfigureCircleShape(radialStrokes, 0.4f, 1f);
            ConfigureStretched(radialStrokes, 2.5f, 0.09f);
            ConfigureDamping(radialStrokes, 0.12f);
            ConfigureErosion(radialStrokes, 0.15f);

            ParticleSystem focusLines = CreateParticle(root.transform, "FocusLines", materials.InkStroke, 19);
            ConfigureBurst(focusLines, 40, new Vector2(0.12f, 0.22f), new Vector2(-14f, -26f), new Vector2(0.12f, 0.32f));
            ConfigureCircleShape(focusLines, 8.5f, 0.25f);
            ConfigureStretched(focusLines, 6f, 0.12f);
            ConfigureErosion(focusLines, 0.1f);

            ParticleSystem splatter = CreateParticle(root.transform, "Splatter", materials.InkSplat, 40);
            ConfigureBurst(splatter, 20, new Vector2(0.45f, 0.85f), new Vector2(6f, 20f), new Vector2(0.2f, 0.9f));
            ConfigureCircleShape(splatter, 0.6f, 1f);
            ConfigureDamping(splatter, 0.25f);
            ConfigureErosion(splatter, 0.55f);
            var splatterMain = splatter.main;
            splatterMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            splatterMain.gravityModifier = 0.5f;

            ParticleSystem coreBlot = CreateParticle(root.transform, "CoreBlot", materials.InkSplat, 18);
            ConfigureBurst(coreBlot, 2, new Vector2(0.12f, 0.18f), new Vector2(0f, 0.5f), new Vector2(1.4f, 2.2f));
            ConfigureCircleShape(coreBlot, 0.3f, 1f);
            ConfigureErosion(coreBlot, 0f);
            var coreMain = coreBlot.main;
            coreMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, ImpactFxPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static ParticleSystem CreateParticle(Transform parent, string particleName, Material material, int sortingOrder)
        {
            var particleObject = new GameObject(particleName);
            particleObject.transform.SetParent(parent, false);
            var particle = particleObject.AddComponent<ParticleSystem>();
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particle.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.5f;
            // 顿帧期间 timeScale 接近 0 特效仍需按真实时间播放
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.startColor = new Color(0.04f, 0.04f, 0.05f, 1f);

            var emission = particle.emission;
            emission.rateOverTime = 0f;

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.sortingOrder = sortingOrder;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position,
                ParticleSystemVertexStream.Color,
                ParticleSystemVertexStream.UV,
                ParticleSystemVertexStream.StableRandomXY,
            });
            return particle;
        }

        private static void ConfigureBurst(ParticleSystem particle, int count, Vector2 lifetime, Vector2 speed, Vector2 size)
        {
            var main = particle.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
            var emission = particle.emission;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
        }

        private static void ConfigureCircleShape(ParticleSystem particle, float radius, float radiusThickness)
        {
            var shape = particle.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.radiusThickness = radiusThickness;
            shape.arc = 360f;
            shape.randomDirectionAmount = 0.08f;
        }

        private static void ConfigureStretched(ParticleSystem particle, float lengthScale, float velocityScale)
        {
            var particleRenderer = particle.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            particleRenderer.lengthScale = lengthScale;
            particleRenderer.velocityScale = velocityScale;
            particleRenderer.cameraVelocityScale = 0f;
        }

        private static void ConfigureDamping(ParticleSystem particle, float dampen)
        {
            var limitVelocity = particle.limitVelocityOverLifetime;
            limitVelocity.enabled = true;
            limitVelocity.limit = 0f;
            limitVelocity.dampen = dampen;
        }

        /// <summary>
        /// alpha 驱动 InkParticle 溶解阈值 holdUntil 之前保持完整墨迹
        /// </summary>
        private static void ConfigureErosion(ParticleSystem particle, float holdUntil)
        {
            var colorOverLifetime = particle.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, holdUntil), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;
        }

        #endregion

        #region Scenes

        private static void Build2DScene(DemoMaterials materials, GameObject impactFxPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Camera camera = CreateCamera(materials, new Vector3(0f, 0f, -10f), Quaternion.identity);
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.backgroundColor = new Color(0.97f, 0.97f, 0.96f);

            var attacker = new GameObject("Attacker");
            SpriteRenderer body = CreateSprite(attacker.transform, "Body", "Adventurer_Attack", 0);
            body.transform.localScale = Vector3.one * 1.45f;
            SpriteRenderer fist = CreateSprite(attacker.transform, "Fist", "Adventurer_Fist", 30);

            GameObject impactFx = InstantiateFx(impactFxPrefab, Quaternion.identity, 1f);
            HitImpactDirector director = CreateDirector(camera, impactFx, 0.28f);
            SetFloats(director, ("blurMultiplier", 0.55f));

            var punch = attacker.AddComponent<PunchAttack2D>();
            SetFields(punch, ("director", director), ("body", body.transform), ("fist", fist.transform));
            SetFloats(punch, ("idleFistScale", 0.24f), ("windupFistScale", 0.2f), ("strikeFistScale", 1.05f));
            SetVectors(punch,
                ("idleFistLocal", new Vector3(-1.25f, -1.1f, 0f)),
                ("windupFistLocal", new Vector3(-1.7f, -0.4f, 0f)),
                ("strikeFistLocal", new Vector3(0.35f, -2.5f, 0f)));

            EditorSceneManager.SaveScene(scene, Scene2DPath);
        }

        private static void Build3DScene(DemoMaterials materials, GameObject impactFxPrefab)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Vector3 cameraPosition = new Vector3(4.3f, 2.2f, -1.3f);
            Vector3 lookTarget = new Vector3(0f, 1.15f, 0.9f);
            Quaternion cameraRotation = Quaternion.LookRotation(lookTarget - cameraPosition);
            Camera camera = CreateCamera(materials, cameraPosition, cameraRotation);
            camera.fieldOfView = 45f;
            camera.backgroundColor = new Color(0.93f, 0.93f, 0.95f);

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.65f);

            GameObject ground = CreatePrimitive(PrimitiveType.Plane, null, "Ground", materials.ToonWhite, Vector3.zero, Vector3.one * 2f);
            ground.transform.position = new Vector3(0f, 0f, 1f);

            var attacker = new GameObject("Attacker");
            var torso = new GameObject("Torso").transform;
            torso.SetParent(attacker.transform, false);
            torso.localPosition = new Vector3(0f, 1.05f, 0f);
            CreatePrimitive(PrimitiveType.Capsule, torso, "Body", materials.ToonBlue, Vector3.zero, new Vector3(0.55f, 0.42f, 0.4f));
            CreatePrimitive(PrimitiveType.Sphere, torso, "Head", materials.ToonSkin, new Vector3(0f, 0.62f, 0f), Vector3.one * 0.42f);
            CreatePrimitive(PrimitiveType.Sphere, torso, "Hair", materials.ToonNavy, new Vector3(0f, 0.7f, -0.04f), new Vector3(0.46f, 0.36f, 0.46f));
            CreatePrimitive(PrimitiveType.Capsule, torso, "ArmIdle", materials.ToonBlue, new Vector3(-0.36f, 0.05f, 0.05f), new Vector3(0.14f, 0.28f, 0.14f));
            var shoulder = new GameObject("Shoulder").transform;
            shoulder.SetParent(torso, false);
            shoulder.localPosition = new Vector3(0.3f, 0.3f, 0.02f);
            CreatePrimitive(PrimitiveType.Capsule, attacker.transform, "LegL", materials.ToonNavy, new Vector3(-0.15f, 0.42f, 0f), new Vector3(0.2f, 0.42f, 0.2f));
            CreatePrimitive(PrimitiveType.Capsule, attacker.transform, "LegR", materials.ToonNavy, new Vector3(0.15f, 0.42f, 0f), new Vector3(0.2f, 0.42f, 0.2f));
            GameObject armSegment = CreatePrimitive(PrimitiveType.Cylinder, attacker.transform, "ArmSegment", materials.ToonBlue, Vector3.zero, new Vector3(0.14f, 0.3f, 0.14f));
            GameObject fist = CreatePrimitive(PrimitiveType.Sphere, attacker.transform, "Fist", materials.ToonSkin, new Vector3(0.35f, 1.35f, 0.35f), Vector3.one * 0.36f);

            var smear = fist.AddComponent<SmearMotion>();
            SetFields(smear, ("smearRendererList", new Object[] { fist.GetComponent<Renderer>(), armSegment.GetComponent<Renderer>() }));

            var dummy = new GameObject("Dummy");
            dummy.transform.position = new Vector3(0f, 0f, 1.75f);
            CreatePrimitive(PrimitiveType.Cylinder, dummy.transform, "Base", materials.ToonNavy, new Vector3(0f, 0.04f, 0f), new Vector3(0.6f, 0.04f, 0.6f));
            var dummyVisual = new GameObject("Visual").transform;
            dummyVisual.SetParent(dummy.transform, false);
            dummyVisual.localPosition = new Vector3(0f, 0.08f, 0f);
            CreatePrimitive(PrimitiveType.Capsule, dummyVisual, "Bag", materials.ToonDummy, new Vector3(0f, 0.95f, 0f), new Vector3(0.7f, 0.85f, 0.7f));
            var receiver = dummy.AddComponent<ImpactReceiver>();
            SetFields(receiver, ("visual", dummyVisual));

            GameObject impactFx = InstantiateFx(impactFxPrefab, cameraRotation, 0.3f);
            HitImpactDirector director = CreateDirector(camera, impactFx, 0.06f);

            var punch = attacker.AddComponent<PunchAttack3D>();
            SetFields(punch,
                ("director", director),
                ("fist", fist.transform),
                ("shoulder", shoulder),
                ("armSegment", armSegment.transform),
                ("torso", torso),
                ("receiver", receiver));

            EditorSceneManager.SaveScene(scene, Scene3DPath);
        }

        private static Camera CreateCamera(DemoMaterials materials, Vector3 position, Quaternion rotation)
        {
            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetPositionAndRotation(position, rotation);
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<CameraShaker>();
            var postEffect = cameraObject.AddComponent<ImpactPostEffect>();
            SetFields(postEffect, ("postMaterial", materials.Post));
            return camera;
        }

        private static HitImpactDirector CreateDirector(Camera camera, GameObject impactFx, float shakeAmplitude)
        {
            var directorObject = new GameObject("HitImpactDirector");
            var director = directorObject.AddComponent<HitImpactDirector>();
            ParticleSystem[] particleList = impactFx.GetComponentsInChildren<ParticleSystem>();
            SetFields(director,
                ("targetCamera", camera),
                ("postEffect", camera.GetComponent<ImpactPostEffect>()),
                ("cameraShaker", camera.GetComponent<CameraShaker>()),
                ("impactFxRoot", impactFx.transform),
                ("impactParticleList", particleList));
            SetFloats(director, ("shakeAmplitude", shakeAmplitude));
            return director;
        }

        private static GameObject InstantiateFx(GameObject impactFxPrefab, Quaternion rotation, float scale)
        {
            var impactFx = (GameObject)PrefabUtility.InstantiatePrefab(impactFxPrefab);
            impactFx.transform.rotation = rotation;
            impactFx.transform.localScale = Vector3.one * scale;
            return impactFx;
        }

        private static SpriteRenderer CreateSprite(Transform parent, string objectName, string spriteName, int sortingOrder)
        {
            var spriteObject = new GameObject(objectName);
            spriteObject.transform.SetParent(parent, false);
            var spriteRenderer = spriteObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtFolder}/{spriteName}.png");
            spriteRenderer.sortingOrder = sortingOrder;
            return spriteRenderer;
        }

        private static GameObject CreatePrimitive(PrimitiveType ePrimitive, Transform parent, string objectName, Material material, Vector3 localPosition, Vector3 localScale)
        {
            GameObject primitive = GameObject.CreatePrimitive(ePrimitive);
            primitive.name = objectName;
            Object.DestroyImmediate(primitive.GetComponent<Collider>());
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        #endregion

        #region Serialized Field Helpers

        private static void SetFields(Object target, params (string name, object value)[] fieldList)
        {
            var serializedObject = new SerializedObject(target);
            foreach ((string name, object value) in fieldList)
            {
                SerializedProperty property = FindPropertyOrThrow(serializedObject, name);
                if (value is Object[] objectList)
                {
                    property.arraySize = objectList.Length;
                    for (int i = 0; i < objectList.Length; i++)
                    {
                        property.GetArrayElementAtIndex(i).objectReferenceValue = objectList[i];
                    }
                }
                else
                {
                    property.objectReferenceValue = (Object)value;
                }
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloats(Object target, params (string name, float value)[] fieldList)
        {
            var serializedObject = new SerializedObject(target);
            foreach ((string name, float value) in fieldList)
            {
                FindPropertyOrThrow(serializedObject, name).floatValue = value;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVectors(Object target, params (string name, Vector3 value)[] fieldList)
        {
            var serializedObject = new SerializedObject(target);
            foreach ((string name, Vector3 value) in fieldList)
            {
                FindPropertyOrThrow(serializedObject, name).vector3Value = value;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static SerializedProperty FindPropertyOrThrow(SerializedObject serializedObject, string name)
        {
            SerializedProperty property = serializedObject.FindProperty(name);
            if (property == null)
            {
                throw new System.InvalidOperationException($"{serializedObject.targetObject.GetType().Name} 上找不到序列化字段 {name}");
            }
            return property;
        }

        #endregion
    }
}
