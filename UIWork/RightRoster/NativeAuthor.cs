using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.U2D;
using UnityEngine.UI;

/// <summary>
/// 使用已审查切片和布局进行分区原生制作
/// </summary>
public static class RightRosterAuthor
{
    /// <summary> 当前任务根目录 </summary>
    private const string Work = "UIWork/RightRoster/";
    /// <summary> 原生资产根目录 </summary>
    private const string AssetsRoot = "Assets/TestImgae/RightRoster/";
    /// <summary> 交付预制体路径 </summary>
    private const string PrefabPath = AssetsRoot + "RightRoster.prefab";
    /// <summary> 小样预制体路径 </summary>
    private const string ProbePath = AssetsRoot + "Validation/ComponentProbe.prefab";
    /// <summary> 原图宽度 </summary>
    private const int ReferenceWidth = 1540;
    /// <summary> 原图高度 </summary>
    private const int ReferenceHeight = 928;
    /// <summary> 右侧区域左坐标 </summary>
    private const int PanelLeft = 780;

    #region 资源准备
    /// <summary>
    /// 读取切片清单并导入当前任务资源和位图字体
    /// </summary>
    public static object Import()
    {
        var Manifest = Read("resources/slices-manifest-v2.json");
        Directory.CreateDirectory(AssetsRoot + "Sprites");
        Directory.CreateDirectory(AssetsRoot + "Fonts");
        Directory.CreateDirectory(AssetsRoot + "Validation");
        foreach (var Piece in Manifest["slices"])
        {
            string Destination = AssetsRoot + "Sprites/" + Piece["name"] + ".png";
            File.Copy(Work + (string)Piece["artifact"]["path"], Destination, false);
        }
        File.Copy(Work + "resources/glyph-atlas.png", AssetsRoot + "Fonts/ReferenceGlyphs.png", false);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var Piece in Manifest["slices"])
        {
            var Importer = (TextureImporter)AssetImporter.GetAtPath(AssetsRoot + "Sprites/" + Piece["name"] + ".png");
            Importer.textureType = TextureImporterType.Sprite;
            Importer.spriteImportMode = SpriteImportMode.Single;
            Importer.spritePixelsPerUnit = 100;
            Importer.mipmapEnabled = false;
            Importer.alphaIsTransparency = true;
            Importer.textureCompression = TextureImporterCompression.Uncompressed;
            Importer.npotScale = TextureImporterNPOTScale.None;
            Importer.filterMode = FilterMode.Bilinear;
            var Settings = new TextureImporterSettings();
            Importer.ReadTextureSettings(Settings);
            Settings.spriteMeshType = SpriteMeshType.FullRect;
            Importer.SetTextureSettings(Settings);
            Importer.SaveAndReimport();
        }
        var AtlasImporter = (TextureImporter)AssetImporter.GetAtPath(AssetsRoot + "Fonts/ReferenceGlyphs.png");
        AtlasImporter.mipmapEnabled = false;
        AtlasImporter.alphaIsTransparency = true;
        AtlasImporter.textureCompression = TextureImporterCompression.Uncompressed;
        AtlasImporter.npotScale = TextureImporterNPOTScale.None;
        AtlasImporter.filterMode = FilterMode.Bilinear;
        AtlasImporter.SaveAndReimport();
        CreateFont();
        var Atlas = new SpriteAtlas();
        Atlas.SetPackingSettings(new SpriteAtlasPackingSettings { enableRotation = false, enableTightPacking = false, padding = 4 });
        Atlas.SetTextureSettings(new SpriteAtlasTextureSettings { generateMipMaps = false, filterMode = FilterMode.Bilinear, sRGB = true });
        Atlas.Add(Manifest["slices"].Select(Piece => AssetDatabase.LoadAssetAtPath<Sprite>(AssetsRoot + "Sprites/" + Piece["name"] + ".png")).Cast<UnityEngine.Object>().ToArray());
        AssetDatabase.CreateAsset(Atlas, AssetsRoot + "RightRoster.spriteatlas");
        AssetDatabase.SaveAssets();
        return new { sprites = Manifest["slices"].Count(), glyphs = 11, font = AssetsRoot + "Fonts/ReferenceBitmap.asset" };
    }

    /// <summary>
    /// 将原图字形映射为可编辑的 TMP Unicode 字体
    /// </summary>
    private static void CreateFont()
    {
        var Source = AssetDatabase.LoadAssetAtPath<Font>("Assets/PathfindingAlgorithm/Arts/Fonts/NotoSansSC-Regular.ttf");
        var Font = TMP_FontAsset.CreateFontAsset(Source, 24, 0, GlyphRenderMode.SMOOTH_HINTED, 512, 64, AtlasPopulationMode.Static, false);
        var OldTexture = Font.atlasTextures[0];
        Font.name = "ReferenceBitmap";
        Font.faceInfo = new FaceInfo { familyName = "Screenshot Glyphs", styleName = "Reference", pointSize = 24, scale = 1, lineHeight = 26, ascentLine = 24, capLine = 24, meanLine = 18, baseline = 0, descentLine = -2 };
        Font.atlasTextures = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(AssetsRoot + "Fonts/ReferenceGlyphs.png") };
        UnityEngine.Object.DestroyImmediate(OldTexture);
        Font.material.name = "ReferenceBitmap Material";
        Font.material.mainTexture = Font.atlasTextures[0];
        Font.glyphTable.Clear();
        Font.characterTable.Clear();
        var GlyphData = Read("resources/glyphs.json");
        foreach (var Item in GlyphData["glyphs"])
        {
            uint Id = (uint)Item["unicode"];
            int Width = (int)Item["width"];
            int Height = (int)Item["height"];
            var Glyph = new Glyph(Id, new GlyphMetrics(Width, Height, 0, 24, (float)Item["advance"]), new GlyphRect((int)Item["x"], (int)Item["y"], Width, Height), 1, 0);
            Font.glyphTable.Add(Glyph);
            Font.characterTable.Add(new TMP_Character(Id, Font, Glyph));
        }
        Font.ReadFontAssetDefinition();
        AssetDatabase.CreateAsset(Font, AssetsRoot + "Fonts/ReferenceBitmap.asset");
        AssetDatabase.AddObjectToAsset(Font.material, Font);
        EditorUtility.SetDirty(Font);
    }
    #endregion

    #region 分区制作
    /// <summary>
    /// 创建仅包含代表字形与斜边的小样并保存重开
    /// </summary>
    public static object Probe()
    {
        if (File.Exists(ProbePath)) throw new InvalidOperationException("Probe already exists");
        var Scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var Root = CreateRoot("ComponentProbe", Scene);
            var Panel = Root.transform.Find("RightPanel");
            AddTabs(Panel);
            AddCards(Panel, "Right02");
            AddDecor(Panel);
            PrefabUtility.SaveAsPrefabAsset(Root, ProbePath);
        }
        finally { EditorSceneManager.ClosePreviewScene(Scene); }
        return Capture(ProbePath, ReferenceWidth, ReferenceHeight, "native/probe-primary");
    }

    /// <summary>
    /// 检查小样在两侧画幅下的固定右锚点布局
    /// </summary>
    public static object ProbeViewports()
    {
        Capture(ProbePath, 1280, 928, "native/probe-narrow");
        return Capture(ProbePath, 1920, 928, "native/probe-wide");
    }

    /// <summary>
    /// 创建正式空壳后保存当前哈希
    /// </summary>
    public static object Shell()
    {
        if (File.Exists(PrefabPath)) throw new InvalidOperationException("Prefab already exists");
        var Scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var Root = CreateRoot("RightRoster", Scene);
            PrefabUtility.SaveAsPrefabAsset(Root, PrefabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(Scene); }
        File.WriteAllText(Work + "native/current-hash.txt", Hash(PrefabPath));
        return Capture(PrefabPath, ReferenceWidth, ReferenceHeight, "native/01-shell");
    }

    /// <summary>
    /// 校验当前预制体版本后仅追加指定区域
    /// </summary>
    public static object Edit(string Region)
    {
        string BeforeHash = Hash(PrefabPath);
        if (File.ReadAllText(Work + "native/current-hash.txt") != BeforeHash) throw new InvalidOperationException("Prefab changed since last batch");
        var Before = ExportHierarchy(PrefabPath);
        var Root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var Panel = Root.transform.Find("RightPanel");
            if (Region == "tabs") AddTabs(Panel);
            else if (Region == "decor") AddDecor(Panel);
            else AddCards(Panel, Region);
            PrefabUtility.SaveAsPrefabAsset(Root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(Root); }
        var After = ExportHierarchy(PrefabPath);
        var AfterDict = After["nodes"].ToDictionary(Node => (string)Node["path"], Node => (long)Node["localId"]);
        bool Preserved = Before["nodes"].All(Node => AfterDict[(string)Node["path"]] == (long)Node["localId"]);
        if (!Preserved) throw new InvalidOperationException("Existing node identity changed");
        File.WriteAllText(Work + "native/current-hash.txt", Hash(PrefabPath));
        Write("native/batch-" + Region + "-edit.json", new JObject { ["beforeHash"] = BeforeHash, ["afterHash"] = Hash(PrefabPath), ["existingIdentitiesPreserved"] = Preserved, ["region"] = Region });
        return Capture(PrefabPath, ReferenceWidth, ReferenceHeight, "native/batch-" + Region);
    }

    /// <summary>
    /// 创建画布与由右侧锚点拥有尺寸的容器
    /// </summary>
    private static GameObject CreateRoot(string Name, Scene Scene)
    {
        var Root = new GameObject(Name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        SceneManager.MoveGameObjectToScene(Root, Scene);
        var Canvas = Root.GetComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var Scaler = Root.GetComponent<CanvasScaler>();
        Scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        Scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
        Scaler.matchWidthOrHeight = 1;
        var Background = Rect("PreviewBackground", Root.transform, 0, 0, ReferenceWidth, ReferenceHeight);
        Background.anchorMin = Vector2.zero;
        Background.anchorMax = Vector2.one;
        Background.offsetMin = Vector2.zero;
        Background.offsetMax = Vector2.zero;
        var Image = Background.gameObject.AddComponent<Image>();
        Image.color = new Color32(12, 13, 16, 255);
        Image.raycastTarget = false;
        var Panel = Rect("RightPanel", Root.transform, 0, 0, ReferenceWidth - PanelLeft, ReferenceHeight);
        Panel.anchorMin = Panel.anchorMax = new Vector2(1, 1);
        Panel.pivot = new Vector2(1, 1);
        Panel.anchoredPosition = Vector2.zero;
        var Black = Panel.gameObject.AddComponent<Image>();
        Black.color = Color.black;
        Black.raycastTarget = false;
        return Root;
    }

    /// <summary>
    /// 按清单增加一列卡片并分离头像信息条与文字
    /// </summary>
    private static void AddCards(Transform Panel, string Prefix)
    {
        var Manifest = Read("resources/slices-manifest-v2.json");
        var Group = Rect(Prefix + "Cards", Panel, 0, 0, 760, ReferenceHeight);
        foreach (var Card in Manifest["cards"].Where(Item => ((string)Item["name"]).StartsWith(Prefix, StringComparison.Ordinal)))
        {
            var Owner = Rect((string)Card["name"], Group, 0, 0, 760, ReferenceHeight);
            AddPiece((string)Card["portrait"], Owner, Manifest);
            foreach (var Child in Card["children"]) AddPiece((string)Child, Owner, Manifest);
            if ((int)Card["level"] == 0) continue;
            var Position = Card["labelRect"];
            AddText("Level", "等级" + (int)Card["level"], Owner, (float)Position[0] - PanelLeft, (float)Position[1] + 2, 76, 28, Color.white);
        }
    }

    /// <summary>
    /// 增加三项静态页签并绑定原生文字
    /// </summary>
    private static void AddTabs(Transform Panel)
    {
        var Manifest = Read("resources/slices-manifest-v2.json");
        var Group = Rect("Tabs", Panel, 0, 0, 760, ReferenceHeight);
        foreach (string Name in new[] { "BasicTab", "SkillTab", "EquipmentTab" })
        {
            var Piece = Manifest["slices"].First(Item => (string)Item["name"] == Name);
            var Item = AddPiece(Name, Group, Manifest);
            AddText("Label", (string)Piece["label"], Item, 60, 13, 52, 26, Name == "BasicTab" ? Color.black : Color.white);
        }
    }

    /// <summary>
    /// 增加筛选收藏和独立选中边框装饰
    /// </summary>
    private static void AddDecor(Transform Panel)
    {
        var Manifest = Read("resources/slices-manifest-v2.json");
        var Group = Rect("Decorations", Panel, 0, 0, 760, ReferenceHeight);
        foreach (string Name in new[] { "SelectionFrame", "FilterButton", "FavoriteButton", "SelectRibbon" }) AddPiece(Name, Group, Manifest);
    }

    /// <summary>
    /// 从清单读取源坐标并绑定透明 Sprite
    /// </summary>
    private static RectTransform AddPiece(string Name, Transform Parent, JObject Manifest)
    {
        var Piece = Manifest["slices"].First(Item => (string)Item["name"] == Name);
        var Position = Piece["rect"];
        var Node = Rect(Name, Parent, (float)Position[0] - PanelLeft, (float)Position[1], (float)Position[2], (float)Position[3]);
        var Image = Node.gameObject.AddComponent<Image>();
        Image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetsRoot + "Sprites/" + Name + ".png");
        Image.type = UnityEngine.UI.Image.Type.Simple;
        Image.raycastTarget = false;
        return Node;
    }

    /// <summary>
    /// 将原始 Unicode 文案写入独立 TMP 组件
    /// </summary>
    private static void AddText(string Name, string Value, Transform Parent, float X, float Y, float Width, float Height, Color Color)
    {
        var Node = Rect(Name, Parent, X, Y, Width, Height);
        var Text = Node.gameObject.AddComponent<TextMeshProUGUI>();
        Text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetsRoot + "Fonts/ReferenceBitmap.asset");
        Text.fontSize = 24;
        Text.text = Value;
        Text.color = Color;
        Text.alignment = TextAlignmentOptions.TopLeft;
        Text.enableWordWrapping = false;
        Text.overflowMode = TextOverflowModes.Overflow;
        Text.margin = Vector4.zero;
        Text.raycastTarget = false;
        Text.richText = false;
        Text.extraPadding = false;
    }

    /// <summary>
    /// 使用左上原点创建固定尺寸布局节点
    /// </summary>
    private static RectTransform Rect(string Name, Transform Parent, float X, float Y, float Width, float Height)
    {
        var Node = new GameObject(Name, typeof(RectTransform)).GetComponent<RectTransform>();
        Node.SetParent(Parent, false);
        Node.anchorMin = Node.anchorMax = new Vector2(0, 1);
        Node.pivot = new Vector2(0, 1);
        Node.anchoredPosition = new Vector2(X, -Y);
        Node.sizeDelta = new Vector2(Width, Height);
        return Node;
    }
    #endregion

    #region 原生验证
    /// <summary>
    /// 导出实际依赖快照并重开预览场景核验资源和组件
    /// </summary>
    public static object AuditDelivery()
    {
        var Hierarchy = ExportHierarchy(PrefabPath);
        var MappingList = new JArray();
        foreach (var Resource in Hierarchy["resources"])
        {
            string Source = (string)Resource["path"];
            string Destination = "native/source-snapshots/" + Source + ".snapshot";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Work + Destination));
            File.Copy(Source, Work + Destination, false);
            MappingList.Add(new JObject { ["source"] = Source, ["path"] = Destination, ["sha256"] = Hash(Source) });
        }
        Write("native/source-snapshots.json", new JObject { ["resources"] = MappingList, ["exportedAtUtc"] = DateTime.UtcNow.ToString("o") });
        var Scene = EditorSceneManager.OpenScene(AssetsRoot + "RightRosterPreview.unity", OpenSceneMode.Additive);
        JObject Report;
        try
        {
            var Root = Scene.GetRootGameObjects().Single(Item => Item.name == "RightRoster");
            int Missing = Root.GetComponentsInChildren<Transform>(true).Sum(Node => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(Node.gameObject));
            int BrokenImages = Root.GetComponentsInChildren<Image>(true).Count(Item => Item.name != "PreviewBackground" && Item.name != "RightPanel" && Item.sprite == null);
            int BrokenFonts = Root.GetComponentsInChildren<TMP_Text>(true).Count(Item => Item.font == null);
            var TextList = new JArray(Root.GetComponentsInChildren<TMP_Text>(true).Select(Item => new JObject { ["path"] = NodePath(Item.transform, Root.transform), ["text"] = Item.text }));
            Report = new JObject { ["scenePath"] = Scene.path, ["sceneReopened"] = true, ["prefabInstancePath"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Root), ["missingScripts"] = Missing, ["brokenImages"] = BrokenImages, ["brokenFonts"] = BrokenFonts, ["texts"] = TextList, ["selectables"] = Root.GetComponentsInChildren<Selectable>(true).Length, ["prefabHash"] = Hash(PrefabPath), ["observedAtUtc"] = DateTime.UtcNow.ToString("o") };
        }
        finally { EditorSceneManager.CloseScene(Scene, true); }
        Report["openScenesAfter"] = new JArray(Enumerable.Range(0, SceneManager.sceneCount).Select(Index => new JObject { ["path"] = SceneManager.GetSceneAt(Index).path, ["dirty"] = SceneManager.GetSceneAt(Index).isDirty }));
        Write("native/engineering-audit.json", Report);
        return Report;
    }

    /// <summary>
    /// 补齐 TMP 内部查询字形并验证画面与已有字形未变
    /// </summary>
    public static object PatchFontAndVerify()
    {
        string Path = AssetsRoot + "Fonts/ReferenceGlyphsV2.png";
        File.Copy(Work + "resources/glyph-atlas-v2.png", Path, false);
        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceSynchronousImport);
        var Importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        Importer.mipmapEnabled = false;
        Importer.alphaIsTransparency = true;
        Importer.textureCompression = TextureImporterCompression.Uncompressed;
        Importer.npotScale = TextureImporterNPOTScale.None;
        Importer.filterMode = FilterMode.Bilinear;
        Importer.SaveAndReimport();
        var Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetsRoot + "Fonts/ReferenceBitmap.asset");
        Font.atlasTextures = new[] { AssetDatabase.LoadAssetAtPath<Texture2D>(Path) };
        Font.material.mainTexture = Font.atlasTextures[0];
        var Glyph = new Glyph(95, new GlyphMetrics(12, 2, 0, -1, 12), new GlyphRect(486, 52, 12, 2), 1, 0);
        Font.glyphTable.Add(Glyph);
        Font.characterTable.Add(new TMP_Character(95, Font, Glyph));
        Font.ReadFontAssetDefinition();
        EditorUtility.SetDirty(Font);
        EditorUtility.SetDirty(Font.material);
        AssetDatabase.SaveAssets();
        Capture(PrefabPath, 1280, 928, "native/final-narrow-v3");
        Capture(PrefabPath, 1540, 928, "native/final-primary-v3");
        Capture(PrefabPath, 1920, 928, "native/final-wide-v3");
        Write("native/hierarchy-v3.json", ExportHierarchy(PrefabPath));
        return new { glyphs = Font.characterTable.Count, captures = 3 };
    }

    /// <summary>
    /// 根据最终对照仅补充右下信息并验证已有节点身份
    /// </summary>
    public static object AddFooter()
    {
        string BeforeHash = Hash(PrefabPath);
        if (File.ReadAllText(Work + "native/current-hash.txt") != BeforeHash) throw new InvalidOperationException("Prefab changed since last batch");
        var Before = ExportHierarchy(PrefabPath);
        Write("native/before-footer-hierarchy.json", Before);
        foreach (string Name in new[] { "Signal", "BottomEdge" })
        {
            string Destination = AssetsRoot + "Sprites/" + Name + ".png";
            File.Copy(Work + "resources/footer-v1/" + Name + ".png", Destination, false);
            AssetDatabase.ImportAsset(Destination, ImportAssetOptions.ForceSynchronousImport);
            var Importer = (TextureImporter)AssetImporter.GetAtPath(Destination);
            Importer.textureType = TextureImporterType.Sprite;
            Importer.spriteImportMode = SpriteImportMode.Single;
            Importer.mipmapEnabled = false;
            Importer.alphaIsTransparency = true;
            Importer.textureCompression = TextureImporterCompression.Uncompressed;
            Importer.npotScale = TextureImporterNPOTScale.None;
            var Settings = new TextureImporterSettings();
            Importer.ReadTextureSettings(Settings);
            Settings.spriteMeshType = SpriteMeshType.FullRect;
            Importer.SetTextureSettings(Settings);
            Importer.SaveAndReimport();
        }
        var Root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var Panel = Root.transform.Find("RightPanel");
            var Footer = Rect("Footer", Panel, 0, 0, 760, ReferenceHeight);
            var Uid = Rect("UID", Footer, 1430 - PanelLeft, 874, 84, 14).gameObject.AddComponent<TextMeshProUGUI>();
            Uid.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Uid.text = "UID: 21781313";
            Uid.fontSize = 11;
            Uid.fontStyle = FontStyles.Bold;
            Uid.color = new Color32(123, 125, 128, 255);
            Uid.enableWordWrapping = false;
            Uid.alignment = TextAlignmentOptions.TopLeft;
            Uid.raycastTarget = false;
            var Signal = Rect("Signal", Footer, 1515 - PanelLeft, 872, 18, 15).gameObject.AddComponent<Image>();
            Signal.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetsRoot + "Sprites/Signal.png");
            Signal.raycastTarget = false;
            var Edge = Rect("BottomEdge", Footer, 1060 - PanelLeft, 886, 475, 4).gameObject.AddComponent<Image>();
            Edge.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetsRoot + "Sprites/BottomEdge.png");
            Edge.raycastTarget = false;
            var Atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AssetsRoot + "RightRoster.spriteatlas");
            Atlas.Add(new UnityEngine.Object[] { Signal.sprite, Edge.sprite });
            EditorUtility.SetDirty(Atlas);
            PrefabUtility.SaveAsPrefabAsset(Root, PrefabPath);
            AssetDatabase.SaveAssets();
        }
        finally { PrefabUtility.UnloadPrefabContents(Root); }
        var After = ExportHierarchy(PrefabPath);
        var AfterDict = After["nodes"].ToDictionary(Node => (string)Node["path"], Node => Node);
        bool Preserved = Before["nodes"].All(Node => JToken.DeepEquals(Node, AfterDict[(string)Node["path"]]));
        if (!Preserved) throw new InvalidOperationException("Existing node properties changed");
        File.WriteAllText(Work + "native/current-hash.txt", Hash(PrefabPath));
        Write("native/footer-edit-result.json", new JObject { ["beforeHash"] = BeforeHash, ["afterHash"] = Hash(PrefabPath), ["existingNodesUnchanged"] = Preserved, ["addedNodes"] = After["nodes"].Count() - Before["nodes"].Count() });
        return new { preserved = Preserved, addedNodes = 4 };
    }

    /// <summary>
    /// 记录补漏后三视口画面与完整层级
    /// </summary>
    public static object VerifyV2()
    {
        Capture(PrefabPath, 1280, 928, "native/final-narrow-v2");
        Capture(PrefabPath, 1540, 928, "native/final-primary-v2");
        Capture(PrefabPath, 1920, 928, "native/final-wide-v2");
        var Hierarchy = ExportHierarchy(PrefabPath);
        Write("native/hierarchy-v2.json", Hierarchy);
        return new { prefab = PrefabPath, nodes = Hierarchy["nodes"].Count(), captures = 3 };
    }

    /// <summary>
    /// 在三个实际渲染宽度下核对已保存资产
    /// </summary>
    public static object Verify()
    {
        Capture(PrefabPath, 1280, 928, "native/final-narrow");
        Capture(PrefabPath, 1540, 928, "native/final-primary");
        Capture(PrefabPath, 1920, 928, "native/final-wide");
        var Hierarchy = ExportHierarchy(PrefabPath);
        Write("native/hierarchy.json", Hierarchy);
        return new { prefab = PrefabPath, nodes = Hierarchy["nodes"].Count(), captures = 3, fontGlyphs = 11 };
    }

    /// <summary>
    /// 使用隔离相机捕获保存重开的预制体与布局
    /// </summary>
    private static object Capture(string Path, int Width, int Height, string Stem)
    {
        Directory.CreateDirectory(Work + "native");
        if (File.Exists(Work + Stem + ".png")) throw new InvalidOperationException("Capture already exists");
        var Scene = EditorSceneManager.NewPreviewScene();
        var Previous = RenderTexture.active;
        RenderTexture Target = null;
        Texture2D Pixels = null;
        try
        {
            var Root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Path), Scene);
            var CameraObject = new GameObject("UI Capture Camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(CameraObject, Scene);
            var Camera = CameraObject.GetComponent<Camera>();
            Camera.enabled = false;
            Camera.scene = Scene;
            Camera.cameraType = CameraType.Game;
            Camera.orthographic = true;
            Camera.orthographicSize = Height * 0.5f;
            Camera.aspect = Width / (float)Height;
            Camera.nearClipPlane = 0.1f;
            Camera.farClipPlane = 100;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = Color.black;
            var Canvas = Root.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = Camera;
            Canvas.planeDistance = 10;
            Root.GetComponent<CanvasScaler>().enabled = false;
            Canvas.scaleFactor = Height / (float)ReferenceHeight;
            Target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            Target.Create();
            Camera.targetTexture = Target;
            UnityEngine.Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)Root.transform);
            foreach (var Text in Root.GetComponentsInChildren<TMP_Text>(true)) Text.ForceMeshUpdate();
            UnityEngine.Canvas.ForceUpdateCanvases();
            Camera.Render();
            RenderTexture.active = Target;
            Pixels = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            Pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            Pixels.Apply();
            File.WriteAllBytes(Work + Stem + ".png", Pixels.EncodeToPNG());
            var NodeList = new JArray();
            foreach (var Node in Root.GetComponentsInChildren<RectTransform>(true))
            {
                var CornerList = new Vector3[4];
                Node.GetWorldCorners(CornerList);
                var ScreenList = CornerList.Select(Corner => RectTransformUtility.WorldToScreenPoint(Camera, Corner)).ToArray();
                var Text = Node.GetComponent<TMP_Text>();
                NodeList.Add(new JObject { ["path"] = NodePath(Node, Root.transform), ["active"] = Node.gameObject.activeInHierarchy,
                    ["x"] = ScreenList.Min(Point => Point.x), ["y"] = ScreenList.Min(Point => Point.y), ["width"] = ScreenList.Max(Point => Point.x)-ScreenList.Min(Point => Point.x), ["height"] = ScreenList.Max(Point => Point.y)-ScreenList.Min(Point => Point.y),
                    ["screenRect"] = new JArray(ScreenList.Min(Point => Point.x), ScreenList.Min(Point => Point.y), ScreenList.Max(Point => Point.x)-ScreenList.Min(Point => Point.x), ScreenList.Max(Point => Point.y)-ScreenList.Min(Point => Point.y)),
                    ["text"] = Text == null ? null : Text.text, ["textOverflow"] = Text != null && Text.isTextOverflowing });
            }
            var Report = new JObject { ["prefabPath"] = Path, ["prefabHash"] = Hash(Path), ["pngSha256"] = Hash(Work + Stem + ".png"), ["width"] = Width, ["height"] = Height,
                ["observationScope"] = "isolated-explicit-settings", ["settings"] = new JObject { ["safeArea"] = new JArray(0,0,Width,Height), ["referenceWidth"] = ReferenceWidth, ["referenceHeight"] = ReferenceHeight, ["matchWidthOrHeight"] = 1 },
                ["nodes"] = NodeList, ["observedAtUtc"] = DateTime.UtcNow.ToString("o"), ["sourceDesignHash"] = Hash(Work + "06-micro-detail/evidence/design-6.png"), ["inputAcceptance"] = "visual-only no Selectable or business input" };
            Write(Stem + ".json", Report);
            return new { image = Work + Stem + ".png", report = Work + Stem + ".json", nodes = NodeList.Count };
        }
        finally
        {
            RenderTexture.active = Previous;
            if (Pixels != null) UnityEngine.Object.DestroyImmediate(Pixels);
            if (Target != null) { Target.Release(); UnityEngine.Object.DestroyImmediate(Target); }
            EditorSceneManager.ClosePreviewScene(Scene);
        }
    }

    /// <summary>
    /// 从真实保存资产导出节点组件及引用资源哈希
    /// </summary>
    private static JObject ExportHierarchy(string Path)
    {
        var Root = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var NodeList = new JArray();
        foreach (var Node in Root.GetComponentsInChildren<Transform>(true))
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(Node.gameObject, out string Guid, out long LocalId);
            var Text = Node.GetComponent<TMP_Text>();
            var Transform = (RectTransform)Node;
            NodeList.Add(new JObject { ["path"] = NodePath(Node, Root.transform), ["localId"] = LocalId, ["active"] = Node.gameObject.activeSelf,
                ["components"] = new JArray(Node.GetComponents<Component>().Select(Component => new JObject { ["type"] = Component.GetType().FullName })),
                ["text"] = Text == null ? null : Text.text, ["anchoredPosition"] = new JArray(Transform.anchoredPosition.x, Transform.anchoredPosition.y),
                ["sizeDelta"] = new JArray(Transform.sizeDelta.x, Transform.sizeDelta.y), ["nestedPrefab"] = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(Node.gameObject) });
        }
        var ResourceList = new JArray(AssetDatabase.GetDependencies(Path).Where(File.Exists).Select(Item => new JObject { ["path"] = Item, ["sha256"] = Hash(Item) }));
        return new JObject { ["sourcePrefabSha256"] = Hash(Path), ["nodes"] = NodeList, ["resources"] = ResourceList };
    }

    /// <summary>
    /// 保存独立预览场景并保留项目已有场景状态
    /// </summary>
    public static object SaveScene()
    {
        string ScenePath = AssetsRoot + "RightRosterPreview.unity";
        if (File.Exists(ScenePath)) throw new InvalidOperationException("Scene already exists");
        var Scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), Scene);
        var CameraObject = new GameObject("Preview Camera", typeof(Camera));
        SceneManager.MoveGameObjectToScene(CameraObject, Scene);
        CameraObject.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
        CameraObject.GetComponent<Camera>().backgroundColor = new Color32(12,13,16,255);
        EditorSceneManager.SaveScene(Scene, ScenePath);
        EditorSceneManager.CloseScene(Scene, true);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        EditorGUIUtility.PingObject(Selection.activeObject);
        return new { scene = ScenePath, prefab = PrefabPath };
    }
    #endregion

    #region 文件与节点查询
    /// <summary>
    /// 读取任务内 JSON 数据
    /// </summary>
    private static JObject Read(string Path) { return JObject.Parse(File.ReadAllText(Work + Path, Encoding.UTF8)); }

    /// <summary>
    /// 保存本次原生证据
    /// </summary>
    private static void Write(string Path, JObject Value) { File.WriteAllText(Work + Path, Value.ToString(), new UTF8Encoding(false)); }

    /// <summary>
    /// 计算文件字节哈希
    /// </summary>
    private static string Hash(string Path)
    {
        using (var Hasher = SHA256.Create()) return BitConverter.ToString(Hasher.ComputeHash(File.ReadAllBytes(Path))).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// 返回包含根节点名称的真实层级路径
    /// </summary>
    private static string NodePath(Transform Node, Transform Root)
    {
        return Node == Root ? Root.name : NodePath(Node.parent, Root) + "/" + Node.name;
    }
    #endregion
}
