using UnityEngine;
using UnityEngine.UI;

namespace PathfindingAlgorithm.Visualization
{
    /// <summary>
    /// 把网格画笔状态译成寻路输入 逐步调用当前算法再写回格子颜色与箭头
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

        /// <summary> 无信息搜索里的 DFS 勾选项 </summary>
        [SerializeField]
        private Toggle dfsToggle;

        /// <summary> 无信息搜索里的 DLS 勾选项 </summary>
        [SerializeField]
        private Toggle dlsToggle;

        /// <summary> 无信息搜索里的 IDDFS 勾选项 </summary>
        [SerializeField]
        private Toggle iddfsToggle;

        /// <summary> 无信息搜索里的 UCS 勾选项 </summary>
        [SerializeField]
        private Toggle ucsToggle;

        /// <summary> 有信息搜索里的 GBFS 勾选项 </summary>
        [SerializeField]
        private Toggle gbfsToggle;

        /// <summary> 有信息搜索里的 A* 勾选项 </summary>
        [SerializeField]
        private Toggle astarToggle;

        /// <summary> 有信息搜索里的 Weighted A* 勾选项 </summary>
        [SerializeField]
        private Toggle wastarToggle;

        /// <summary> WA* 权重输入 勾选时显示在右下角 </summary>
        [SerializeField]
        private GameObject weightRow;

        /// <summary> WA* 的 W 输入框 </summary>
        [SerializeField]
        private InputField weightInput;

        /// <summary> DLS 深度上限 从起点沿当前枝最多走几步 </summary>
        [SerializeField, Min(0)]
        private int dlsLimit = 24;

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

        /// <summary> DFS 地图与搜索状态 </summary>
        private DFSData dfsData;

        /// <summary> DFS 步进器 </summary>
        private DFSCore dfsCore;

        /// <summary> DLS 地图与搜索状态 </summary>
        private DLSData dlsData;

        /// <summary> DLS 步进器 </summary>
        private DLSCore dlsCore;

        /// <summary> IDDFS 地图与搜索状态 </summary>
        private IDDFSData iddfsData;

        /// <summary> IDDFS 步进器 </summary>
        private IDDFSCore iddfsCore;

        /// <summary> UCS 地图与搜索状态 </summary>
        private UCSData ucsData;

        /// <summary> UCS 步进器 </summary>
        private UCSCore ucsCore;

        /// <summary> GBFS 地图与搜索状态 </summary>
        private GBFSData gbfsData;

        /// <summary> GBFS 步进器 </summary>
        private GBFSCore gbfsCore;

        /// <summary> A* 地图与搜索状态 </summary>
        private AStarData astarData;

        /// <summary> A* 步进器 </summary>
        private AStarCore astarCore;

        /// <summary> WA* 地图与搜索状态 </summary>
        private WAStarData wastarData;

        /// <summary> WA* 步进器 </summary>
        private WAStarCore wastarCore;

        /// <summary> 本轮实际在跑的算法 </summary>
        private eSearchKind eKind;

        /// <summary> 已绘制的 IDDFS 深度上限 变了就清探索层 </summary>
        private int overlayLimit;

        /// <summary> 是否正在自动逐步搜索 </summary>
        private bool playing;

        /// <summary> 自动播放累计时间 </summary>
        private float elapsed;

        /// <summary> 当前网格是否已灌进搜索器 </summary>
        private bool prepared;

        /// <summary> 本轮地图可行走表 </summary>
        private bool[] walkableList;

        /// <summary> 本轮格子地形权重 普通1 贵地1.5/2/3 </summary>
        private float[] costList;

        /// <summary> 本轮起点 </summary>
        private Vector2Int mapStart;

        /// <summary> 本轮终点 </summary>
        private Vector2Int mapGoal;

        /// <summary> 本轮已弹出步数 </summary>
        private int stepCount;

        private enum eSearchKind
        {
            None,
            BFS,
            DFS,
            DLS,
            IDDFS,
            UCS,
            GBFS,
            AStar,
            WAStar,
        }

        /// <summary>
        /// 唯一生命周期入口
        /// </summary>
        private void Start()
        {
            InitComponents();
            if (wastarToggle != null)
                wastarToggle.onValueChanged.AddListener(RefreshWeightRow);
            RefreshWeightRow(wastarToggle != null && wastarToggle.isOn);
        }

        /// <summary>
        /// 创建无状态搜索器实例
        /// </summary>
        private void InitComponents()
        {
            bfsData = new BFSData();
            bfsCore = new BFSCore();
            dfsData = new DFSData();
            dfsCore = new DFSCore();
            dlsData = new DLSData();
            dlsCore = new DLSCore();
            iddfsData = new IDDFSData();
            iddfsCore = new IDDFSCore();
            ucsData = new UCSData();
            ucsCore = new UCSCore();
            gbfsData = new GBFSData();
            gbfsCore = new GBFSCore();
            astarData = new AStarData();
            astarCore = new AStarCore();
            wastarData = new WAStarData();
            wastarCore = new WAStarCore();
        }

        /// <summary>
        /// WA* 勾选时露出右下角 W 输入 并给状态文字让出右侧
        /// </summary>
        private void RefreshWeightRow(bool show)
        {
            if (weightRow != null)
                weightRow.SetActive(show);
            if (statusText == null)
                return;
            Vector2 size = statusText.rectTransform.sizeDelta;
            size.x = show ? 168f : 292f;
            statusText.rectTransform.sizeDelta = size;
        }

        /// <summary>
        /// 读右下角 W 解析失败时按 1 跑
        /// </summary>
        private float ReadWeight()
        {
            float weight = 1f;
            if (weightInput != null
                && float.TryParse(weightInput.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float parsed))
                weight = parsed;
            return weight;
        }

        #region 播放控制

        /// <summary>
        /// 按当前网格启动或继续搜索 间隔为 0 时一次跑完
        /// </summary>
        public void PlaySearch()
        {
            if (playing)
            {
                return;
            }

            if (!CanResume() && !PrepareSearch())
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
        /// 暂停自动播放 保留当前搜索进度
        /// </summary>
        public void PauseSearch()
        {
            playing = false;
        }

        /// <summary>
        /// 退回上一步弹出 保留墙与起终点
        /// </summary>
        public void UndoStep()
        {
            if (!prepared || stepCount <= 0)
            {
                return;
            }

            playing = false;
            stepCount--;
            ReplayVisible();
        }

        /// <summary>
        /// 停止自动播放 下次开始会重新读网格
        /// </summary>
        public void StopSearch()
        {
            playing = false;
            prepared = false;
            stepCount = 0;
        }

        /// <summary>
        /// 清掉本轮探索和路径 保留墙与起终点
        /// </summary>
        public void ResetRound()
        {
            StopSearch();
            ClearSearchOverlay();
            statusText.text = "";
        }

        /// <summary>
        /// 只推进一格 未准备或已结束时先从网格重建搜索
        /// </summary>
        public void StepSearch()
        {
            playing = false;
            if (!prepared || IsFound() || IsOpenEmpty())
            {
                if (!PrepareSearch())
                {
                    return;
                }
            }

            Advance();
        }

        #endregion

        #region 搜索推进

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
        /// 从网格重建当前算法输入 失败时写入状态并停止
        /// </summary>
        private bool PrepareSearch()
        {
            playing = false;
            prepared = false;
            eKind = ResolveKind();
            if (eKind == eSearchKind.None)
            {
                return false;
            }

            ClearSearchOverlay();
            if (!TryBuildMap(out bool[] WalkableList, out float[] CostList, out Vector2Int start, out Vector2Int goal))
            {
                return false;
            }

            walkableList = WalkableList;
            costList = CostList;
            mapStart = start;
            mapGoal = goal;
            stepCount = 0;
            overlayLimit = -1;
            InitCurrentSearch();
            prepared = true;
            RefreshStatus("搜索中");
            return true;
        }

        /// <summary>
        /// 暂停后仍有未完成步骤则可继续
        /// </summary>
        private bool CanResume()
        {
            return prepared && !IsFound() && !IsOpenEmpty();
        }

        /// <summary>
        /// 用本轮地图重新初始化当前算法
        /// </summary>
        private void InitCurrentSearch()
        {
            if (eKind == eSearchKind.DFS)
            {
                dfsData.Init(grid.Columns, grid.Rows, walkableList, mapStart, mapGoal);
                dfsCore.Init(dfsData);
                return;
            }

            if (eKind == eSearchKind.DLS)
            {
                dlsData.Init(grid.Columns, grid.Rows, walkableList, mapStart, mapGoal, dlsLimit);
                dlsCore.Init(dlsData);
                return;
            }

            if (eKind == eSearchKind.IDDFS)
            {
                iddfsData.Init(grid.Columns, grid.Rows, walkableList, mapStart, mapGoal);
                iddfsCore.Init(iddfsData);
                return;
            }

            if (eKind == eSearchKind.UCS)
            {
                ucsData.Init(grid.Columns, grid.Rows, walkableList, costList, mapStart, mapGoal);
                ucsCore.Init(ucsData);
                return;
            }

            if (eKind == eSearchKind.GBFS)
            {
                gbfsData.Init(grid.Columns, grid.Rows, walkableList, mapStart, mapGoal);
                gbfsCore.Init(gbfsData);
                return;
            }

            if (eKind == eSearchKind.AStar)
            {
                astarData.Init(grid.Columns, grid.Rows, walkableList, costList, mapStart, mapGoal);
                astarCore.Init(astarData);
                return;
            }

            if (eKind == eSearchKind.WAStar)
            {
                wastarData.Init(grid.Columns, grid.Rows, walkableList, costList, mapStart, mapGoal, ReadWeight());
                wastarCore.Init(wastarData);
                return;
            }

            bfsData.Init(grid.Columns, grid.Rows, walkableList, mapStart, mapGoal);
            bfsCore.Init(bfsData);
        }

        /// <summary>
        /// 按已记录步数重跑并重绘探索层
        /// </summary>
        private void ReplayVisible()
        {
            overlayLimit = -1;
            ClearSearchOverlay();
            InitCurrentSearch();
            for (int i = 0; i < stepCount; i++)
            {
                bool more = StepCurrent();
                PaintCurrentProgress();
                if (!more)
                {
                    if (IsFound())
                    {
                        PaintPath();
                    }

                    break;
                }
            }

            if (stepCount == 0)
            {
                RefreshStatus("搜索中");
                return;
            }

            if (IsFound())
            {
                RefreshStatus($"已找到路径  {PathCount()} 格");
                return;
            }

            if (IsOpenEmpty())
            {
                RefreshStatus(eKind == eSearchKind.DLS ? "上限内不可达" : "不可达");
                return;
            }

            RefreshStatus("搜索中");
        }

        /// <summary>
        /// 按勾选决定本轮算法 多选或未选则失败
        /// </summary>
        private eSearchKind ResolveKind()
        {
            int onCount = 0;
            var eResolved = eSearchKind.None;
            if (bfsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.BFS;
            }

            if (dfsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.DFS;
            }

            if (dlsToggle != null && dlsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.DLS;
            }

            if (iddfsToggle != null && iddfsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.IDDFS;
            }

            if (ucsToggle != null && ucsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.UCS;
            }

            if (gbfsToggle != null && gbfsToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.GBFS;
            }

            if (astarToggle != null && astarToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.AStar;
            }

            if (wastarToggle != null && wastarToggle.isOn)
            {
                onCount++;
                eResolved = eSearchKind.WAStar;
            }

            if (onCount != 1)
                return eSearchKind.None;

            return eResolved;
        }

        /// <summary>
        /// 弹出一格并着色 结束时画路径 输出是否还能继续
        /// </summary>
        private bool Advance()
        {
            bool more = StepCurrent();
            stepCount++;
            PaintCurrentProgress();
            if (more)
            {
                RefreshStatus("搜索中");
                return true;
            }

            playing = false;
            if (IsFound())
            {
                PaintPath();
                RefreshStatus($"已找到路径  {PathCount()} 格");
            }
            else
            {
                RefreshStatus(eKind == eSearchKind.DLS ? "上限内不可达" : "不可达");
            }

            return false;
        }

        /// <summary>
        /// 写入本轮标题与时空复杂度
        /// </summary>
        private void RefreshStatus(string headline)
        {
            string openWord = "开集最多";
            if (eKind == eSearchKind.BFS)
                openWord = "排队最多";
            else if (eKind != eSearchKind.UCS && eKind != eSearchKind.GBFS && eKind != eSearchKind.AStar && eKind != eSearchKind.WAStar)
                openWord = "栈最多";
            int popCount = PopCount();
            int peakOpenCount = PeakOpenCount();
            string limitWord = "";
            if (eKind == eSearchKind.DLS)
                limitWord = $"  上限 {dlsLimit}";
            else if (eKind == eSearchKind.IDDFS)
                limitWord = $"  当前上限 {iddfsData.Limit}";
            else if (eKind == eSearchKind.WAStar)
                limitWord = $"  W {wastarData.Weight}";
            statusText.text = $"{headline}\n公式  {ComplexityFormula()}\n本轮  已看 {popCount} 格  {openWord} {peakOpenCount} 格{limitWord}";
        }

        /// <summary>
        /// 当前算法按格子C和邻居N算的复杂度
        /// </summary>
        private string ComplexityFormula()
        {
            if (eKind == eSearchKind.IDDFS)
                return "时间 O(dCN)  空间 O(C)";
            if (eKind == eSearchKind.UCS || eKind == eSearchKind.GBFS || eKind == eSearchKind.AStar || eKind == eSearchKind.WAStar)
                return "时间 O(C(C+N))  空间 O(C)";
            return "时间 O(CN)  空间 O(C)";
        }

        /// <summary>
        /// IDDFS 换上限时清探索层 再给当前格上色
        /// </summary>
        private void PaintCurrentProgress()
        {
            if (eKind == eSearchKind.IDDFS && iddfsData.Limit != overlayLimit)
            {
                overlayLimit = iddfsData.Limit;
                ClearSearchOverlay();
            }

            PaintExplored(CurrentCell());
        }

        #endregion

        #region 网格读写

        /// <summary>
        /// 读网格障碍贵地与起终点 输出可行走表和地形权重
        /// </summary>
        private bool TryBuildMap(out bool[] WalkableList, out float[] CostList, out Vector2Int start, out Vector2Int goal)
        {
            int width = grid.Columns;
            int height = grid.Rows;
            int cellCount = width * height;
            WalkableList = new bool[cellCount];
            CostList = new float[cellCount];
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
                    CostList[index] = TerrainCost(eState);
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
        /// 贵地权重 其余可走格为1
        /// </summary>
        private static float TerrainCost(eCellState eState)
        {
            if (eState == eCellState.Cost15)
                return 1.5f;
            if (eState == eCellState.Cost2)
                return 2f;
            if (eState == eCellState.Cost3)
                return 3f;
            return 1f;
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
        /// 把已找到的路径标成路径色
        /// </summary>
        private void PaintPath()
        {
            int pathCount = PathCount();
            for (int i = 0; i < pathCount; i++)
            {
                Vector2Int cell = PathCell(i);
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
            int parentIndex = ParentIndex(cell);
            if (parentIndex < 0)
            {
                return;
            }

            Vector2Int parent = ToCell(parentIndex);
            grid.SetCellArrow(cell, new Vector2(cell.x - parent.x, cell.y - parent.y));
        }

        #endregion

        /// <summary>
        /// 推进当前算法一步
        /// </summary>
        private bool StepCurrent()
        {
            if (eKind == eSearchKind.DFS)
                return dfsCore.Step();
            if (eKind == eSearchKind.DLS)
                return dlsCore.Step();
            if (eKind == eSearchKind.IDDFS)
                return iddfsCore.Step();
            if (eKind == eSearchKind.UCS)
                return ucsCore.Step();
            if (eKind == eSearchKind.GBFS)
                return gbfsCore.Step();
            if (eKind == eSearchKind.AStar)
                return astarCore.Step();
            if (eKind == eSearchKind.WAStar)
                return wastarCore.Step();
            return bfsCore.Step();
        }

        private Vector2Int CurrentCell()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.Current;
            if (eKind == eSearchKind.DLS)
                return dlsData.CurrentCell;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.CurrentCell;
            if (eKind == eSearchKind.UCS)
                return ucsData.Current;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.Current;
            if (eKind == eSearchKind.AStar)
                return astarData.Current;
            if (eKind == eSearchKind.WAStar)
                return wastarData.Current;
            return bfsData.Current;
        }

        private bool IsFound()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.Found;
            if (eKind == eSearchKind.DLS)
                return dlsData.Found;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.Found;
            if (eKind == eSearchKind.UCS)
                return ucsData.Found;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.Found;
            if (eKind == eSearchKind.AStar)
                return astarData.Found;
            if (eKind == eSearchKind.WAStar)
                return wastarData.Found;
            return bfsData.Found;
        }

        private bool IsOpenEmpty()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.Stack.Count == 0;
            if (eKind == eSearchKind.DLS)
                return dlsData.Stack.Count == 0;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.Stack.Count == 0 && !iddfsData.HitLimit;
            if (eKind == eSearchKind.UCS)
                return ucsData.OpenList.Count == 0;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.OpenList.Count == 0;
            if (eKind == eSearchKind.AStar)
                return astarData.OpenList.Count == 0;
            if (eKind == eSearchKind.WAStar)
                return wastarData.OpenList.Count == 0;
            return bfsData.Queue.Count == 0;
        }

        private int PathCount()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.PathList.Count;
            if (eKind == eSearchKind.DLS)
                return dlsData.PathList.Count;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.PathList.Count;
            if (eKind == eSearchKind.UCS)
                return ucsData.PathList.Count;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.PathList.Count;
            if (eKind == eSearchKind.AStar)
                return astarData.PathList.Count;
            if (eKind == eSearchKind.WAStar)
                return wastarData.PathList.Count;
            return bfsData.PathList.Count;
        }

        private Vector2Int PathCell(int index)
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.PathList[index];
            if (eKind == eSearchKind.DLS)
                return dlsData.PathList[index];
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.PathList[index];
            if (eKind == eSearchKind.UCS)
                return ucsData.PathList[index];
            if (eKind == eSearchKind.GBFS)
                return gbfsData.PathList[index];
            if (eKind == eSearchKind.AStar)
                return astarData.PathList[index];
            if (eKind == eSearchKind.WAStar)
                return wastarData.PathList[index];
            return bfsData.PathList[index];
        }

        private int ParentIndex(Vector2Int cell)
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.ParentIndexList[dfsData.ToIndex(cell)];
            if (eKind == eSearchKind.DLS)
                return dlsData.ParentIndexList[dlsData.ToIndex(cell)];
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.ParentIndexList[iddfsData.ToIndex(cell)];
            if (eKind == eSearchKind.UCS)
                return ucsData.ParentIndexList[ucsData.ToIndex(cell)];
            if (eKind == eSearchKind.GBFS)
                return gbfsData.ParentIndexList[gbfsData.ToIndex(cell)];
            if (eKind == eSearchKind.AStar)
                return astarData.ParentIndexList[astarData.ToIndex(cell)];
            if (eKind == eSearchKind.WAStar)
                return wastarData.ParentIndexList[wastarData.ToIndex(cell)];
            return bfsData.ParentIndexList[bfsData.ToIndex(cell)];
        }

        private Vector2Int ToCell(int index)
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.ToCell(index);
            if (eKind == eSearchKind.DLS)
                return dlsData.ToCell(index);
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.ToCell(index);
            if (eKind == eSearchKind.UCS)
                return ucsData.ToCell(index);
            if (eKind == eSearchKind.GBFS)
                return gbfsData.ToCell(index);
            if (eKind == eSearchKind.AStar)
                return astarData.ToCell(index);
            if (eKind == eSearchKind.WAStar)
                return wastarData.ToCell(index);
            return bfsData.ToCell(index);
        }

        private int PopCount()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.PopCount;
            if (eKind == eSearchKind.DLS)
                return dlsData.PopCount;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.PopCount;
            if (eKind == eSearchKind.UCS)
                return ucsData.PopCount;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.PopCount;
            if (eKind == eSearchKind.AStar)
                return astarData.PopCount;
            if (eKind == eSearchKind.WAStar)
                return wastarData.PopCount;
            return bfsData.PopCount;
        }

        private int PeakOpenCount()
        {
            if (eKind == eSearchKind.DFS)
                return dfsData.PeakOpenCount;
            if (eKind == eSearchKind.DLS)
                return dlsData.PeakOpenCount;
            if (eKind == eSearchKind.IDDFS)
                return iddfsData.PeakOpenCount;
            if (eKind == eSearchKind.UCS)
                return ucsData.PeakOpenCount;
            if (eKind == eSearchKind.GBFS)
                return gbfsData.PeakOpenCount;
            if (eKind == eSearchKind.AStar)
                return astarData.PeakOpenCount;
            if (eKind == eSearchKind.WAStar)
                return wastarData.PeakOpenCount;
            return bfsData.PeakOpenCount;
        }
    }
}
