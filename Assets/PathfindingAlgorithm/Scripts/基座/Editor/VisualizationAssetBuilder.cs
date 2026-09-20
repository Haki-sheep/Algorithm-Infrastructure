using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization.Editor
{
    public static class VisualizationAssetBuilder
    {
        /// <summary>
        /// 模块资源根目录
        /// </summary>
        internal const string Root = "Assets/PathfindingAlgorithm";

        /// <summary>
        /// 显式构建请求文件
        /// </summary>
        private const string RequestPath = "Temp/PathfindingVisualizationBuild.request";

        /// <summary>
        /// 状态素材名称 顺序与状态枚举对应
        /// </summary>
        private static readonly string[] spriteNameList = { "White", "Red", "Yellow", "Green", "Black", "Blue" };

        /// <summary>
        /// 学习范围内的分类名称
        /// </summary>
        private static readonly string[] categoryNameList = { "无信息搜索", "有信息搜索", "路径规划优化" };

        /// <summary>
        /// 分类对应的算法目录与界面标签
        /// </summary>
        private static readonly string[][] algorithmNameList =
        {
            new[] { "BFS", "DFS", "DLS", "IDDFS", "UCS · Dijkstra" },
            new[] { "GBFS", "A*", "Weighted A*" },
            new[] { "JPS · JPS+", "Theta*", "HPA*" }
        };

        #region 构建入口

        /// <summary>
        /// 仅处理外部明确提交的一次性构建请求
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ProcessBuildRequest()
        {
            if (File.Exists(RequestPath))
                EditorApplication.delayCall += BuildRequested;
        }

        /// <summary>
        /// 完成请求并输出验收记录
        /// </summary>
        private static void BuildRequested()
        {
            File.Delete(RequestPath);
            Build();
            VisualizationVerifier.Verify();
        }

        /// <summary>
        /// 生成完整预制体和独立演示场景
        /// </summary>
        [MenuItem("Tools/寻路可视化/生成或重建基座资源")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(Root + "/Scenes");
            AssetDatabase.ImportAsset(Root + "/Arts/Fonts/NotoSansSC-Regular.ttf", ImportAssetOptions.ForceSynchronousImport);
            var font = AssetDatabase.LoadAssetAtPath<Font>(Root + "/Arts/Fonts/NotoSansSC-Regular.ttf");
            var ui = new VisualizationUIFactory(font);
            var spriteList = new Sprite[spriteNameList.Length];
            for (int index = 0; index < spriteNameList.Length; index++)
                spriteList[index] = ImportSprite(Root + "/Arts/" + spriteNameList[index] + ".png");
            var arrow = ImportSprite(Root + "/Arts/Arrow.png");
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var cell = BuildCell(ui, spriteList, arrow);
                var canvas = BuildCanvas(ui, cell, spriteList);
                PrefabUtility.SaveAsPrefabAsset(canvas, Root + "/Prefab/SearchVisualization.prefab");
                Object.DestroyImmediate(canvas);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefab/SearchVisualization.prefab");
                PrefabUtility.InstantiatePrefab(prefab, scene);
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                EditorSceneManager.SaveScene(scene, Root + "/Scenes/VisualizationDemo.unity");
                AssetDatabase.SaveAssets();
                Debug.Log("寻路可视化基座生成完成");
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 配置可直接用于界面的精灵导入设置
        /// </summary>
        private static Sprite ImportSprite(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        #endregion

        #region 网格与主界面

        /// <summary>
        /// 创建带独立箭头的格子预制体
        /// </summary>
        private static GridCellView BuildCell(VisualizationUIFactory ui, Sprite[] spriteList, Sprite arrowSprite)
        {
            var rect = ui.Rect("GridCell", null);
            var background = ui.Fill(rect, Color.white);
            background.sprite = spriteList[0];
            var arrowRect = ui.Rect("Arrow", rect);
            arrowRect.anchorMin = Vector2.one * 0.18f;
            arrowRect.anchorMax = Vector2.one * 0.82f;
            arrowRect.offsetMin = arrowRect.offsetMax = Vector2.zero;
            var arrow = ui.Fill(arrowRect, new Color32(30, 43, 62, 255));
            arrow.sprite = arrowSprite;
            arrow.preserveAspect = true;
            arrow.enabled = false;
            var view = rect.gameObject.AddComponent<GridCellView>();
            ui.Bind(view, "background", background);
            ui.Bind(view, "arrow", arrow);
            ui.BindList(view, "stateSpriteList", spriteList);
            var prefab = PrefabUtility.SaveAsPrefabAsset(rect.gameObject, Root + "/Prefab/GridCell.prefab");
            Object.DestroyImmediate(rect.gameObject);
            return prefab.GetComponent<GridCellView>();
        }

        /// <summary>
        /// 创建根画布和左右分栏
        /// </summary>
        private static GameObject BuildCanvas(VisualizationUIFactory ui, GridCellView cell, Sprite[] spriteList)
        {
            var root = ui.Rect("SearchVisualization", null);
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.matchWidthOrHeight = 0.5f;
            root.gameObject.AddComponent<GraphicRaycaster>();
            ui.Fill(root, new Color32(30, 39, 51, 255));
            var heading = ui.Label("搜索算法可视化", root, 32);
            heading.color = new Color32(235, 241, 247, 255);
            ui.Place(heading.rectTransform, 32f, 24f, 700f, 50f);
            var subtitle = ui.Label("SEARCH LAB   /   网格实验台", root, 16);
            ui.Place(subtitle.rectTransform, 34f, 76f, 600f, 30f);
            subtitle.color = new Color32(170, 187, 204, 255);
            var badge = ui.Label("无信息搜索", root, 17);
            badge.color = subtitle.color;
            ui.TopStretch(badge.rectTransform, 32f, 30f, 32f, 40f);
            badge.alignment = TextAnchor.MiddleRight;

            var board = ui.Rect("GridPanel", root);
            ui.Stretch(board, 32f, 188f, 392f, 88f);
            var scroll = ui.Scroll(board, false);
            scroll.horizontal = false;
            scroll.vertical = false;
            scroll.verticalScrollbar = null;
            var verticalBar = scroll.transform.Find("VerticalScrollbar");
            verticalBar.gameObject.SetActive(false);
            var viewport = (RectTransform)scroll.transform.Find("Viewport");
            ui.Stretch(viewport);
            ui.Fill(scroll.content, new Color32(204, 218, 226, 255), true);
            var grid = scroll.content.gameObject.AddComponent<SearchGridView>();
            ui.Bind(grid, "cellPrefab", cell);
            ui.Bind(grid, "gridRect", scroll.content);

            var info = ui.Label("", root, 17);
            info.color = subtitle.color;
            ui.Place(info.rectTransform, 32f, 942f, 330f, 32f);
            info.rectTransform.anchorMin = info.rectTransform.anchorMax = Vector2.zero;
            info.rectTransform.anchoredPosition = new Vector2(32f, 52f);
            var hint = ui.Label("左键绘制  /  右键擦除", root, 16);
            hint.color = subtitle.color;
            ui.Place(hint.rectTransform, 330f, 0f, 610f, 32f);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = Vector2.zero;
            hint.rectTransform.anchoredPosition = new Vector2(330f, 52f);

            var controller = root.gameObject.AddComponent<GridSearchController>();
            ui.Bind(controller, "grid", grid);
            BuildToolbar(ui, root, grid, spriteList, controller);
            BuildSidebar(ui, root, grid, controller);
            var view = root.gameObject.AddComponent<SearchVisualization>();
            ui.Bind(view, "grid", grid);
            ui.Bind(view, "gridInfo", info);
            ui.Bind(view, "searchController", controller);
            view.RefreshInterface();
            return root.gameObject;
        }

        /// <summary>
        /// 创建状态画笔与清空操作
        /// </summary>
        private static void BuildToolbar(VisualizationUIFactory ui, RectTransform root, SearchGridView grid, Sprite[] spriteList, GridSearchController controller)
        {
            var toolbar = ui.Rect("BrushToolbar", root);
            ui.TopStretch(toolbar, 32f, 122f, 392f, 48f);
            var group = toolbar.gameObject.AddComponent<ToggleGroup>();
            var brushNameList = new[] { "空白", "障碍", "贵1.5", "贵2", "贵3", "起点", "终点" };
            var brushStateList = new[]
            {
                eCellState.Empty,
                eCellState.Obstacle,
                eCellState.Cost15,
                eCellState.Cost2,
                eCellState.Cost3,
                eCellState.Start,
                eCellState.End
            };
            var brushSpriteList = new[]
            {
                spriteList[0],
                spriteList[1],
                spriteList[0],
                spriteList[0],
                spriteList[0],
                spriteList[4],
                spriteList[5]
            };
            var brushTintList = new Color[]
            {
                Color.white,
                Color.white,
                new Color32(255, 186, 73, 255),
                new Color32(232, 120, 48, 255),
                new Color32(176, 64, 32, 255),
                Color.white,
                Color.white
            };
            float slot = 118f;
            for (int index = 0; index < brushNameList.Length; index++)
            {
                var toggle = ui.Toggle(brushNameList[index], toolbar, brushSpriteList[index]);
                ui.Place((RectTransform)toggle.transform, index * slot, 4f, slot - 6f, 38f);
                toggle.group = group;
                toggle.isOn = brushStateList[index] == eCellState.Obstacle;
                var swatch = toggle.transform.Find("Swatch");
                if (swatch != null)
                    swatch.GetComponent<Image>().color = brushTintList[index];
                var brush = toggle.gameObject.AddComponent<GridBrushOption>();
                ui.Bind(brush, "grid", grid);
                var serialized = new SerializedObject(brush);
                serialized.FindProperty("eState").enumValueIndex = (int)brushStateList[index];
                serialized.ApplyModifiedPropertiesWithoutUndo();
                UnityEventTools.AddPersistentListener(toggle.onValueChanged, brush.Select);
            }
            var clear = ui.Button("清空网格", toolbar);
            ui.Place((RectTransform)clear.transform, brushNameList.Length * slot + 8f, 4f, 120f, 38f);
            UnityEventTools.AddPersistentListener(clear.onClick, grid.Clear);
            var reset = ui.Button("重置", toolbar);
            ui.Place((RectTransform)reset.transform, brushNameList.Length * slot + 136f, 4f, 88f, 38f);
            UnityEventTools.AddPersistentListener(reset.onClick, controller.ResetRound);
        }

        #endregion

        #region 算法选择区

        /// <summary>
        /// 创建分类滚动区和显示选项
        /// </summary>
        private static void BuildSidebar(VisualizationUIFactory ui, RectTransform root, SearchGridView grid, GridSearchController controller)
        {
            var panel = ui.Rect("AlgorithmPanel", root);
            ui.Stretch(panel, 0f, 122f, 32f, 32f);
            panel.anchorMin = new Vector2(1f, 0f);
            panel.offsetMin = new Vector2(-360f, 32f);
            ui.Fill(panel, Color.white);
            var heading = ui.Label("算法分类", panel, 23);
            ui.Place(heading.rectTransform, 20f, 16f, 290f, 40f);
            var area = ui.Rect("Categories", panel);
            ui.Stretch(area, 12f, 70f, 12f, 236f);
            var scroll = ui.Scroll(area, false);
            scroll.content.anchorMax = Vector2.one;
            scroll.content.anchorMin = new Vector2(0f, 1f);
            scroll.content.sizeDelta = Vector2.zero;
            ui.Vertical(scroll.content, 10f);
            Toggle bfsToggle = null;
            Toggle dfsToggle = null;
            Toggle dlsToggle = null;
            Toggle iddfsToggle = null;
            Toggle ucsToggle = null;
            for (int index = 0; index < categoryNameList.Length; index++)
            {
                var category = BuildCategory(ui, scroll.content, index);
                if (index == 0)
                {
                    bfsToggle = category.Options[0];
                    dfsToggle = category.Options[1];
                    dlsToggle = category.Options[2];
                    iddfsToggle = category.Options[3];
                    ucsToggle = category.Options[4];
                }
            }
            var arrows = ui.Toggle("显示方向箭头", panel);
            ui.Place((RectTransform)arrows.transform, 12f, 0f, 292f, 38f);
            var arrowRect = (RectTransform)arrows.transform;
            arrowRect.pivot = Vector2.zero;
            arrowRect.anchorMin = arrowRect.anchorMax = Vector2.zero;
            arrowRect.anchoredPosition = new Vector2(12f, 190f);
            arrowRect.sizeDelta = new Vector2(292f, 38f);
            UnityEventTools.AddPersistentListener(arrows.onValueChanged, grid.SetArrowsVisible);
            var footnote = ui.Label("", panel, 14);
            footnote.alignment = TextAnchor.UpperLeft;
            footnote.verticalOverflow = VerticalWrapMode.Overflow;
            footnote.rectTransform.anchorMin = footnote.rectTransform.anchorMax = Vector2.zero;
            footnote.rectTransform.pivot = Vector2.zero;
            footnote.rectTransform.anchoredPosition = new Vector2(20f, 102f);
            footnote.rectTransform.sizeDelta = new Vector2(292f, 80f);
            var play = ui.Button("开始搜索", panel);
            var playRect = (RectTransform)play.transform;
            playRect.anchorMin = playRect.anchorMax = playRect.pivot = Vector2.zero;
            playRect.anchoredPosition = new Vector2(12f, 56f);
            playRect.sizeDelta = new Vector2(160f, 38f);
            UnityEventTools.AddPersistentListener(play.onClick, controller.PlaySearch);
            var pause = ui.Button("暂停", panel);
            var pauseRect = (RectTransform)pause.transform;
            pauseRect.anchorMin = pauseRect.anchorMax = pauseRect.pivot = Vector2.zero;
            pauseRect.anchoredPosition = new Vector2(180f, 56f);
            pauseRect.sizeDelta = new Vector2(136f, 38f);
            UnityEventTools.AddPersistentListener(pause.onClick, controller.PauseSearch);
            var step = ui.Button("单步", panel);
            var stepRect = (RectTransform)step.transform;
            stepRect.anchorMin = stepRect.anchorMax = stepRect.pivot = Vector2.zero;
            stepRect.anchoredPosition = new Vector2(12f, 12f);
            stepRect.sizeDelta = new Vector2(160f, 38f);
            UnityEventTools.AddPersistentListener(step.onClick, controller.StepSearch);
            var undo = ui.Button("上一步", panel);
            var undoRect = (RectTransform)undo.transform;
            undoRect.anchorMin = undoRect.anchorMax = undoRect.pivot = Vector2.zero;
            undoRect.anchoredPosition = new Vector2(180f, 12f);
            undoRect.sizeDelta = new Vector2(136f, 38f);
            UnityEventTools.AddPersistentListener(undo.onClick, controller.UndoStep);
            ui.Bind(controller, "bfsToggle", bfsToggle);
            ui.Bind(controller, "dfsToggle", dfsToggle);
            ui.Bind(controller, "dlsToggle", dlsToggle);
            ui.Bind(controller, "iddfsToggle", iddfsToggle);
            ui.Bind(controller, "ucsToggle", ucsToggle);
            ui.Bind(controller, "statusText", footnote);
        }

        /// <summary>
        /// 创建一个可折叠分类及具体算法复选框
        /// </summary>
        private static AlgorithmCategoryView BuildCategory(VisualizationUIFactory ui, RectTransform parent, int categoryIndex)
        {
            var category = ui.Rect(categoryNameList[categoryIndex], parent);
            ui.Vertical(category, 0f);
            Object.DestroyImmediate(category.GetComponent<ContentSizeFitter>());
            var view = category.gameObject.AddComponent<AlgorithmCategoryView>();
            var header = ui.Button("", category);
            header.name = "Header";
            ui.Height(header.gameObject, 54f);
            var title = ui.Label(categoryNameList[categoryIndex], header.transform, 17);
            ui.Stretch(title.rectTransform, 12f, 0f, 52f, 0f);
            var count = ui.Label("00", header.transform, 14);
            ui.Stretch(count.rectTransform, 0f, 0f, 34f, 0f);
            count.alignment = TextAnchor.MiddleRight;
            var indicator = ui.Label(categoryIndex == 0 ? "−" : "+", header.transform, 22);
            ui.Stretch(indicator.rectTransform, 0f, 0f, 12f, 0f);
            indicator.alignment = TextAnchor.MiddleRight;
            var options = ui.Rect("Options", category);
            ui.Vertical(options, 2f);
            Object.DestroyImmediate(options.GetComponent<ContentSizeFitter>());
            var group = options.gameObject.AddComponent<ToggleGroup>();
            group.allowSwitchOff = true;
            var toggleList = new Toggle[algorithmNameList[categoryIndex].Length];
            for (int index = 0; index < toggleList.Length; index++)
            {
                var toggle = ui.Toggle(algorithmNameList[categoryIndex][index], options);
                ui.Height(toggle.gameObject, 38f);
                toggle.group = group;
                toggleList[index] = toggle;
                UnityEventTools.AddPersistentListener(toggle.onValueChanged, view.RefreshSelection);
            }
            ui.Bind(view, "options", options.gameObject);
            ui.Bind(view, "indicator", indicator);
            ui.Bind(view, "selectionInfo", count);
            ui.BindList(view, "optionList", toggleList);
            options.gameObject.SetActive(categoryIndex == 0);
            UnityEventTools.AddPersistentListener(header.onClick, view.ToggleExpanded);
            return view;
        }

        #endregion
    }
}
