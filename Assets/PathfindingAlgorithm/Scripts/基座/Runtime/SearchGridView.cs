using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PathfindingAlgorithm.Visualization
{
    public sealed class SearchGridView : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        /// <summary>
        /// 格子模板
        /// </summary>
        [SerializeField]
        private GridCellView cellPrefab;

        /// <summary>
        /// 网格内容区域
        /// </summary>
        [SerializeField]
        private RectTransform gridRect;

        /// <summary>
        /// 当前格子集合
        /// </summary>
        [SerializeField]
        private List<GridCellView> cellList = new List<GridCellView>();

        /// <summary>
        /// 已构建的列数
        /// </summary>
        [SerializeField]
        private int columns;

        /// <summary>
        /// 已构建的行数
        /// </summary>
        [SerializeField]
        private int rows;

        /// <summary>
        /// 格子边长
        /// </summary>
        [SerializeField]
        private float cellSize;

        /// <summary>
        /// 格线宽度
        /// </summary>
        [SerializeField]
        private float lineWidth = 1f;

        /// <summary>
        /// 箭头显示开关
        /// </summary>
        [SerializeField]
        private bool arrowsVisible;

        /// <summary>
        /// 当前绘制状态
        /// </summary>
        private eCellState eBrush = eCellState.Obstacle;

        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => cellList.Count;

        /// <summary>
        /// 重建网格 改变行列数时清空原有绘制内容
        /// </summary>
        public void Rebuild(int width, int height, float size)
        {
            if (columns != width || rows != height)
            {
                // 尺寸变化后旧索引不再代表相同坐标 因此重建格子
                foreach (var cell in cellList)
                {
                    cell.gameObject.SetActive(false);
                    if (Application.isPlaying)
                        Destroy(cell.gameObject);
                    else
                        DestroyImmediate(cell.gameObject);
                }
                cellList.Clear();
                columns = width;
                rows = height;
                for (int index = 0; index < columns * rows; index++)
                    cellList.Add(Instantiate(cellPrefab, gridRect));
            }

            cellSize = size;
            gridRect.sizeDelta = new Vector2(columns * cellSize, rows * cellSize);
            for (int index = 0; index < cellList.Count; index++)
            {
                var rect = (RectTransform)cellList[index].transform;
                rect.name = $"Cell_{index % columns}_{index / columns}";
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(index % columns * cellSize, -index / columns * cellSize);
                rect.sizeDelta = Vector2.one * (cellSize - lineWidth);
            }
        }

        /// <summary>
        /// 设置绘制工具
        /// </summary>
        public void SetBrush(eCellState eValue)
        {
            eBrush = eValue;
        }

        /// <summary>
        /// 设置状态 坐标从左上角开始 起点终点分别唯一
        /// </summary>
        public void SetCellState(Vector2Int position, eCellState eValue)
        {
            if (eValue == eCellState.Start || eValue == eCellState.End)
            {
                foreach (var cell in cellList)
                    if (cell.State == eValue)
                        cell.SetState(eCellState.Empty);
            }
            cellList[position.y * columns + position.x].SetState(eValue);
        }

        /// <summary>
        /// 读取指定格子的状态
        /// </summary>
        public eCellState GetCellState(Vector2Int position)
        {
            return cellList[position.y * columns + position.x].State;
        }

        /// <summary>
        /// 设置箭头方向 右为正横轴 下为正纵轴
        /// </summary>
        public void SetCellArrow(Vector2Int position, Vector2 direction)
        {
            cellList[position.y * columns + position.x].SetDirection(direction, arrowsVisible);
        }

        /// <summary>
        /// 设置箭头层可见性
        /// </summary>
        public void SetArrowsVisible(bool visible)
        {
            arrowsVisible = visible;
            foreach (var cell in cellList)
                cell.SetArrowVisible(visible);
        }

        /// <summary>
        /// 清空所有状态和箭头
        /// </summary>
        public void Clear()
        {
            foreach (var cell in cellList)
            {
                cell.SetState(eCellState.Empty);
                cell.SetDirection(Vector2.zero, arrowsVisible);
            }
        }

        /// <summary>
        /// 点击绘制
        /// </summary>
        public void OnPointerDown(PointerEventData eventData)
        {
            Paint(eventData);
        }

        /// <summary>
        /// 拖动连续绘制
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            Paint(eventData);
        }

        /// <summary>
        /// 将鼠标坐标转换为格子坐标
        /// </summary>
        private void Paint(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, eventData.position, eventData.pressEventCamera, out var point);
            int x = Mathf.FloorToInt(point.x / cellSize);
            int y = Mathf.FloorToInt(-point.y / cellSize);
            if (x < 0 || x >= columns || y < 0 || y >= rows)
                return;
            var position = new Vector2Int(x, y);
            SetCellState(position, eventData.button == PointerEventData.InputButton.Right ? eCellState.Empty : eBrush);
            SetCellArrow(position, Vector2.zero);
        }
    }
}
