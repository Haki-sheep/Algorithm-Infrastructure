using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization
{
    public sealed class SearchVisualization : MonoBehaviour
    {
        /// <summary>
        /// 方形格子的边长
        /// </summary>
        [SerializeField, MinValue(4f), LabelText("基础格子尺寸"), Title("网格设置")]
        private float cellSize = 28f;

        /// <summary>
        /// 地图横向格子数量
        /// </summary>
        [SerializeField, MinValue(1), LabelText("寻路场长度 格数")]
        private int mapWidth = 40;

        /// <summary>
        /// 地图纵向格子数量
        /// </summary>
        [SerializeField, MinValue(1), LabelText("寻路场宽度 格数")]
        private int mapHeight = 26;

        /// <summary>
        /// 网格视图
        /// </summary>
        [SerializeField, LabelText("网格视图"), Title("组件引用")]
        private SearchGridView grid;

        /// <summary>
        /// 网格尺寸文本
        /// </summary>
        [SerializeField, LabelText("网格信息")]
        private Text gridInfo;

        /// <summary>
        /// 搜索播放控制
        /// </summary>
        [SerializeField, LabelText("搜索控制")]
        private GridSearchController searchController;

        public SearchGridView Grid => grid;

        /// <summary>
        /// 唯一生命周期入口
        /// </summary>
        private void Start()
        {
            InitComponents();
        }

        /// <summary>
        /// 首次应用网格配置
        /// </summary>
        private void InitComponents()
        {
            RefreshInterface();
        }

        /// <summary>
        /// 应用尺寸配置 行列变化时清空网格
        /// </summary>
        [Button("刷新界面", ButtonSizes.Large)]
        public void RefreshInterface()
        {
            searchController.StopSearch();
            grid.Rebuild(mapWidth, mapHeight, cellSize);
            gridInfo.text = $"{mapWidth} × {mapHeight}   /   {mapWidth * mapHeight} 格";
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                UnityEditor.EditorUtility.SetDirty(grid);
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
        }
    }
}
