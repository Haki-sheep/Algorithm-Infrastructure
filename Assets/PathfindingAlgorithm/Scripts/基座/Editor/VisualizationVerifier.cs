using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace PathfindingAlgorithm.Visualization.Editor
{
    public static class VisualizationVerifier
    {
        /// <summary>
        /// 验证记录和预览图目录
        /// </summary>
        private const string Output = "Artifacts/PathfindingVisualization";

        /// <summary>
        /// 从已保存的预制体验证序列化引用和界面行为
        /// </summary>
        [MenuItem("Tools/寻路可视化/验证基座")]
        public static void Verify()
        {
            Directory.CreateDirectory(Output);
            var previousScene = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualizationAssetBuilder.Root + "/Prefab/SearchVisualization.prefab");
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                var view = instance.GetComponent<SearchVisualization>();
                var grid = view.Grid;
                view.RefreshInterface();
                Require(grid.Columns == 40 && grid.Rows == 26 && grid.CellCount == 1040, "默认尺寸与格子数量");
                foreach (var text in instance.GetComponentsInChildren<Text>(true))
                    Require(text.font != null, "中文字体引用");
                foreach (var cell in instance.GetComponentsInChildren<GridCellView>(true))
                    Require(cell.GetComponent<Image>().sprite != null, "格子素材引用");
                Capture(instance, scene, "Preview.png");

                var first = new Vector2Int(1, 1);
                var second = new Vector2Int(2, 2);
                grid.SetCellState(first, eCellState.Start);
                grid.SetCellState(second, eCellState.Start);
                Require(grid.GetCellState(first) == eCellState.Empty && grid.GetCellState(second) == eCellState.Start, "唯一起点");
                grid.SetCellState(first, eCellState.End);
                grid.SetCellState(second, eCellState.End);
                Require(grid.GetCellState(first) == eCellState.Empty && grid.GetCellState(second) == eCellState.End, "唯一终点");
                grid.SetCellState(first, eCellState.Obstacle);
                grid.Rebuild(40, 26, 32f);
                Require(grid.GetCellState(first) == eCellState.Obstacle, "调整边长保留状态");
                grid.SetCellArrow(first, Vector2.right);
                var arrow = grid.transform.GetChild(41).GetChild(0).GetComponent<Image>();
                Require(!arrow.enabled, "默认隐藏箭头");
                grid.SetArrowsVisible(true);
                Require(arrow.enabled, "箭头开关");
                grid.Clear();
                Require(grid.GetCellState(first) == eCellState.Empty && !arrow.enabled, "清空状态与方向");
                grid.Rebuild(12, 9, 30f);
                Require(grid.CellCount == 108 && grid.transform.childCount == 108, "重建移除旧格子");
                grid.Rebuild(12, 9, 30f);
                Require(grid.transform.childCount == 108, "重复刷新不叠加格子");
                view.RefreshInterface();

                var categoryList = instance.GetComponentsInChildren<AlgorithmCategoryView>(true);
                Require(categoryList.Length == 5, "五大分类");
                int optionCount = 0;
                foreach (var category in categoryList)
                {
                    optionCount += category.Options.Length;
                    category.ToggleExpanded();
                    category.Options[0].isOn = true;
                    Require(category.Options[0].isOn, "算法复选框");
                    category.ToggleExpanded();
                    Require(category.Options[0].isOn, "折叠保留选择");
                    category.Options[0].isOn = false;
                }
                Require(optionCount == 29, "算法选项总数");
                var eventSystem = new GameObject("VerificationEventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
                var gridRect = (RectTransform)grid.transform;
                Canvas.ForceUpdateCanvases();
                var screenPoint = RectTransformUtility.WorldToScreenPoint(instance.GetComponent<Canvas>().worldCamera, gridRect.TransformPoint(new Vector3(42f, -42f, 0f)));
                var pointer = new PointerEventData(eventSystem) { position = screenPoint, button = PointerEventData.InputButton.Left };
                // 验证鼠标路径前恢复覆盖画布 使屏幕坐标转换不依赖事件相机
                instance.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.ForceUpdateCanvases();
                pointer.position = RectTransformUtility.WorldToScreenPoint(null, gridRect.TransformPoint(new Vector3(42f, -42f, 0f)));
                grid.OnPointerDown(pointer);
                Require(grid.GetCellState(first) == eCellState.Obstacle, "左键绘制");
                pointer.button = PointerEventData.InputButton.Right;
                grid.OnDrag(pointer);
                Require(grid.GetCellState(first) == eCellState.Empty, "右键擦除");
                File.WriteAllText(Output + "/Verification.txt", "PASS\nUnity " + Application.unityVersion + "\n预制体引用 中文字体 默认网格 状态唯一性 尺寸刷新 箭头开关 清空 分类勾选 鼠标绘制均通过\n");
                Debug.Log("寻路可视化基座验证通过");
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/Verification.txt", "FAIL\n" + exception);
                throw;
            }
            finally
            {
                SceneManager.SetActiveScene(previousScene);
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 验证失败时保留具体条件
        /// </summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        /// <summary>
        /// 使用实际画布渲染预览图
        /// </summary>
        private static void Capture(GameObject instance, Scene scene, string filename)
        {
            var camera = new GameObject("PreviewCamera", typeof(Camera)).GetComponent<Camera>();
            camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;
            var target = new RenderTexture(1600, 1000, 24);
            camera.targetTexture = target;
            var canvas = instance.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            foreach (var scroll in instance.GetComponentsInChildren<ScrollRect>())
                scroll.normalizedPosition = new Vector2(0f, 1f);
            foreach (var mask in instance.GetComponentsInChildren<RectMask2D>())
                mask.PerformClipping();
            foreach (var text in instance.GetComponentsInChildren<Text>())
                text.font.RequestCharactersInTexture(text.text, text.fontSize, text.fontStyle);
            Canvas.ForceUpdateCanvases();
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0f, 0f, target.width, target.height), 0, 0);
            texture.Apply();
            File.WriteAllBytes(Output + "/" + filename, texture.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(texture);
        }
    }
}
