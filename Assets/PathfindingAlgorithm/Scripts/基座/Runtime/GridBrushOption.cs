using UnityEngine;

namespace PathfindingAlgorithm.Visualization
{
    public sealed class GridBrushOption : MonoBehaviour
    {
        /// <summary>
        /// 操作的网格
        /// </summary>
        [SerializeField]
        private SearchGridView grid;

        /// <summary>
        /// 对应的绘制状态
        /// </summary>
        [SerializeField]
        private eCellState eState;

        /// <summary>
        /// 勾选时切换绘制工具
        /// </summary>
        public void Select(bool selected)
        {
            if (selected)
                grid.SetBrush(eState);
        }
    }
}
