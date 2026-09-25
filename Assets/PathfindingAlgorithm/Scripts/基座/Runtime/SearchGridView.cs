using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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

        /// <summary>
        /// 框边界线
        /// </summary>
        private readonly List<Image> clusterLineList = new List<Image>();

        /// <summary>
        /// 出入口标记
        /// </summary>
        private readonly List<Image> entranceMarkList = new List<Image>();

        /// <summary>
        /// 框线白图
        /// </summary>
        private static Sprite whiteSprite;

        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => cellList.Count;

        /// <summary>
        /// 画笔改过格子
        /// </summary>
        public event System.Action MapEdited;

        #region 网格

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

            ClearClusterLines();
            ClearEntranceMarks();
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

        #endregion

        #region 交互

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

        #endregion

        #region 框线

        /// <summary>
        /// 按框边长画闭合线 clusterSize小于1则清掉
        /// </summary>
        public void SetClusterLines(int clusterSize)
        {
            ClearClusterLines();
            if (clusterSize < 1 || columns < 1 || rows < 1)
                return;

            int countX = (columns + clusterSize - 1) / clusterSize;
            int countY = (rows + clusterSize - 1) / clusterSize;
            float thick = Mathf.Max(2f, cellSize * 0.08f);
            float width = columns * cellSize;
            float height = rows * cellSize;
            for (int i = 0; i <= countX; i++)
            {
                float x = i == 0 ? 0f : i == countX ? width - thick : i * clusterSize * cellSize - thick * 0.5f;
                AddClusterLine(new Vector2(x, 0f), new Vector2(thick, height));
            }

            for (int i = 0; i <= countY; i++)
            {
                float y = i == 0 ? 0f : i == countY ? -(height - thick) : -(i * clusterSize * cellSize - thick * 0.5f);
                AddClusterLine(new Vector2(0f, y), new Vector2(width, thick));
            }
        }

        /// <summary>
        /// 在网格上加一条不挡点击的框线
        /// </summary>
        private void AddClusterLine(Vector2 position, Vector2 size)
        {
            var line = new GameObject("ClusterLine", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(gridRect, false);
            var rect = (RectTransform)line.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = line.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = new Color32(18, 72, 168, 230);
            image.raycastTarget = false;
            rect.SetAsLastSibling();
            clusterLineList.Add(image);
        }

        /// <summary>
        /// 清掉框线
        /// </summary>
        private void ClearClusterLines()
        {
            for (int i = 0; i < clusterLineList.Count; i++)
            {
                if (clusterLineList[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(clusterLineList[i].gameObject);
                else
                    DestroyImmediate(clusterLineList[i].gameObject);
            }

            clusterLineList.Clear();
        }

        /// <summary>
        /// 1像素白图 给框线当底
        /// </summary>
        private static Sprite WhiteSprite()
        {
            if (whiteSprite != null)
                return whiteSprite;
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }

        #endregion

        #region 出入口

        /// <summary>
        /// 在能跨框的格子中心画点 空表则清掉
        /// </summary>
        public void SetEntrances(List<Vector2Int> cellList)
        {
            ClearEntranceMarks();
            if (cellList == null || columns < 1 || rows < 1)
                return;
            float mark = Mathf.Max(4f, cellSize * 0.28f);
            for (int i = 0; i < cellList.Count; i++)
            {
                Vector2Int cell = cellList[i];
                if (cell.x < 0 || cell.x >= columns || cell.y < 0 || cell.y >= rows)
                    continue;
                float x = cell.x * cellSize + (cellSize - mark) * 0.5f;
                float y = -(cell.y * cellSize + (cellSize - mark) * 0.5f);
                AddEntranceMark(new Vector2(x, y), mark);
            }
        }

        /// <summary>
        /// 加一个不挡点击的出入口点
        /// </summary>
        private void AddEntranceMark(Vector2 position, float mark)
        {
            var dot = new GameObject("EntranceMark", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dot.transform.SetParent(gridRect, false);
            var rect = (RectTransform)dot.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = Vector2.one * mark;
            var image = dot.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = new Color32(214, 39, 120, 255);
            image.raycastTarget = false;
            rect.SetAsLastSibling();
            entranceMarkList.Add(image);
        }

        /// <summary>
        /// 清掉出入口点
        /// </summary>
        private void ClearEntranceMarks()
        {
            for (int i = 0; i < entranceMarkList.Count; i++)
            {
                if (entranceMarkList[i] == null)
                    continue;
                if (Application.isPlaying)
                    Destroy(entranceMarkList[i].gameObject);
                else
                    DestroyImmediate(entranceMarkList[i].gameObject);
            }

            entranceMarkList.Clear();
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 将鼠标坐标转换为格子坐标
        /// </summary>
        private void Paint(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Middle)
                return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(gridRect, eventData.position, eventData.pressEventCamera, out var point);
            Rect rect = gridRect.rect;
            int x = Mathf.FloorToInt((point.x - rect.xMin) / cellSize);
            int y = Mathf.FloorToInt((rect.yMax - point.y) / cellSize);
            if (x < 0 || x >= columns || y < 0 || y >= rows)
                return;
            var position = new Vector2Int(x, y);
            SetCellState(position, eventData.button == PointerEventData.InputButton.Right ? eCellState.Empty : eBrush);
            SetCellArrow(position, Vector2.zero);
            MapEdited?.Invoke();
        }

        #endregion
    }
}
