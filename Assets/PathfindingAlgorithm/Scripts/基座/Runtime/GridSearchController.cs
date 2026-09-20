using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization
{
    /// <summary>
    /// 把网格画笔状态译成寻路输入 逐步调用 BFS 再写回格子颜色与箭头
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class GridSearchController : MonoBehaviour
    {
        /// <summary> 要读写的网格 </summary>
        [SerializeField]
        private SearchGridView grid;

        /// <summary> 无信息搜索里的 BFS 勾选项 </summary>
        [SerializeField]
        private Toggle bfsToggle;

        /// <summary> 搜索状态说明 </summary>
        [SerializeField]
        private Text statusText;

        /// <summary> 自动播放时每步间隔秒 0 则一次跑完 </summary>
        [SerializeField, Min(0f)]
        private float stepInterval = 0.02f;

        /// <summary> BFS 地图与搜索状态 </summary>
        private BFSData bfsData;

        /// <summary> BFS 步进器 </summary>
        private BFSCore bfsCore;

        /// <summary> 是否正在自动逐步搜索 </summary>
        private bool playing;

        /// <summary> 自动播放累计时间 </summary>
        private float elapsed;

        /// <summary> 当前网格是否已灌进 BFS </summary>
        private bool prepared;

        /// <summary>
        /// 唯一生命周期入口
        /// </summary>
        private void Start()
        {
            InitComponents();
        }

        /// <summary>
        /// 创建无状态搜索器实例
        /// </summary>
        private void InitComponents()
        {
            bfsData = new BFSData();
            bfsCore = new BFSCore();
        }

        /// <summary>
        /// 按当前网格启动 BFS 间隔为 0 时一次跑完
        /// </summary>
        public void PlaySearch()
        {
            if (!PrepareSearch())
            {
                return;
            }

            if (stepInterval <= 0f)
            {
                while (Advance())
                {
                }

                return;
            }

            playing = true;
            elapsed = 0f;
        }

        /// <summary>
        /// 停止自动播放 下次开始会重新读网格
        /// </summary>
        public void StopSearch()
        {
            playing = false;
            prepared = false;
        }

        /// <summary>
        /// 清掉本轮探索和路径 保留墙与起终点
        /// </summary>
        public void ResetRound()
        {
            StopSearch();
            ClearSearchOverlay();
            statusText.text = "勾选 BFS 后点开始搜索";
        }

        /// <summary>
        /// 只推进一格 未准备或已结束时先从网格重建搜索
        /// </summary>
        public void StepSearch()
        {
            playing = false;
            if (!prepared || bfsData.Found || bfsData.Queue.Count == 0)
            {
                if (!PrepareSearch())
                {
                    return;
                }
            }

            Advance();
        }

        /// <summary>
        /// 自动播放时按间隔调用 Advance
        /// </summary>
        private void Update()
        {
            if (!playing)
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < stepInterval)
            {
                return;
            }

            elapsed = 0f;
            Advance();
        }

        /// <summary>
        /// 从网格重建 BFS 输入 失败时写入状态并停止
        /// </summary>
        private bool PrepareSearch()
        {
            playing = false;
            prepared = false;
            if (!bfsToggle.isOn)
            {
                statusText.text = "请勾选 BFS";
                return false;
            }

            ClearSearchOverlay();
            if (!TryBuildMap(out bool[] WalkableList, out Vector2Int start, out Vector2Int goal))
            {
                return false;
            }

            bfsData.Init(grid.Columns, grid.Rows, WalkableList, start, goal);
            bfsCore.Init(bfsData);
            prepared = true;
            RefreshStatus("搜索中");
            return true;
        }

        /// <summary>
        /// 弹出一格并着色 结束时画路径 输出是否还能继续
        /// </summary>
        private bool Advance()
        {
            bool more = bfsCore.Step();
            PaintExplored(bfsData.Current);
            if (more)
            {
                RefreshStatus("搜索中");
                return true;
            }

            playing = false;
            if (bfsData.Found)
            {
                PaintPath();
                RefreshStatus($"已找到路径  {bfsData.PathList.Count} 格");
            }
            else
            {
                RefreshStatus("不可达");
            }

            return false;
        }

        /// <summary>
        /// 写入本轮标题与 BFS 时空复杂度
        /// </summary>
        private void RefreshStatus(string headline)
        {
            statusText.text = $"{headline}\n公式  时间 O(V+E)  空间 O(V)\n本轮  已看 {bfsData.PopCount} 格  排队最多 {bfsData.PeakOpenCount} 格";
        }

        /// <summary>
        /// 读网格障碍与起终点 输出可行走表
        /// </summary>
        private bool TryBuildMap(out bool[] WalkableList, out Vector2Int start, out Vector2Int goal)
        {
            int width = grid.Columns;
            int height = grid.Rows;
            int cellCount = width * height;
            WalkableList = new bool[cellCount];
            start = Vector2Int.zero;
            goal = Vector2Int.zero;
            bool hasStart = false;
            bool hasGoal = false;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    eCellState eState = grid.GetCellState(position);
                    int index = y * width + x;
                    WalkableList[index] = eState != eCellState.Obstacle;
                    if (eState == eCellState.Start)
                    {
                        start = position;
                        hasStart = true;
                    }
                    else if (eState == eCellState.End)
                    {
                        goal = position;
                        hasGoal = true;
                    }
                }
            }

            if (!hasStart || !hasGoal)
            {
                statusText.text = "需要起点和终点";
                return false;
            }

            return true;
        }

        /// <summary>
        /// 去掉探索和路径色 保留墙与起终点
        /// </summary>
        private void ClearSearchOverlay()
        {
            int width = grid.Columns;
            int height = grid.Rows;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    eCellState eState = grid.GetCellState(position);
                    if (eState == eCellState.Explored || eState == eCellState.Path)
                    {
                        grid.SetCellState(position, eCellState.Empty);
                    }

                    grid.SetCellArrow(position, Vector2.zero);
                }
            }
        }

        /// <summary>
        /// 把已弹出格标成已探索并指向父格
        /// </summary>
        private void PaintExplored(Vector2Int cell)
        {
            eCellState eState = grid.GetCellState(cell);
            if (eState == eCellState.Empty)
            {
                grid.SetCellState(cell, eCellState.Explored);
            }

            PaintArrow(cell);
        }

        /// <summary>
        /// 把最短路标成路径色
        /// </summary>
        private void PaintPath()
        {
            int pathCount = bfsData.PathList.Count;
            for (int i = 0; i < pathCount; i++)
            {
                Vector2Int cell = bfsData.PathList[i];
                eCellState eState = grid.GetCellState(cell);
                if (eState == eCellState.Explored || eState == eCellState.Empty)
                {
                    grid.SetCellState(cell, eCellState.Path);
                }

                PaintArrow(cell);
            }
        }

        /// <summary>
        /// 箭头表示从父格走到当前格
        /// </summary>
        private void PaintArrow(Vector2Int cell)
        {
            int parentIndex = bfsData.ParentIndexList[bfsData.ToIndex(cell)];
            if (parentIndex < 0)
            {
                return;
            }

            Vector2Int parent = bfsData.ToCell(parentIndex);
            grid.SetCellArrow(cell, new Vector2(cell.x - parent.x, cell.y - parent.y));
        }
    }
}
