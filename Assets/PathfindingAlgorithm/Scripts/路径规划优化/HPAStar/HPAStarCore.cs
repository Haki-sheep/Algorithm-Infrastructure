using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HPA*先建入口图 再在入口图上A* 最后把边还原成格子路
/// </summary>
public class HPAStarCore
{
    private HPAStarData data;

    public void Init(HPAStarData data)
    {
        this.data = data;
        // 核心 先建入口图 再把起终点接上 最后才在抽象图上开搜
        BuildAbstractGraph();
        InsertStartEnd();
        InitAbsSearch();
    }

    /// <summary>
    /// 预处理完后一步步弹抽象点 终点弹出或开集空则停
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.OpenList.Count == 0)
            return false;

        int best = 0;
        // 核心 抽象图上的A* 比的是入口的F 不是格子
        for (int i = 1; i < data.OpenList.Count; i++)
        {
            if (data.OpenList[i].F < data.OpenList[best].F)
                best = i;
        }

        HPAAbsNode node = data.OpenList[best];
        data.OpenList.RemoveAt(best);

        if (data.AbsComputed[node.NodeIndex]
        || data.AbsGList[node.NodeIndex] < node.G)
            return data.OpenList.Count > 0;

        data.AbsComputed[node.NodeIndex] = true;
        data.Current = data.NodeList[node.NodeIndex].Cell;
        data.CurrentG = node.G;
        data.PopCount++;
        FillStepCells(node.NodeIndex);

        if (node.NodeIndex == data.EndNodeIndex)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }

        Expand(node.NodeIndex);
        return data.OpenList.Count > 0;
    }

    public bool Search()
    {
        while (Step()) ;
        return data.PathList.Count > 0;
    }

    #region 预处理

    /// <summary>
    /// 扫边界建入口 再连跨框边和框内边
    /// </summary>
    private void BuildAbstractGraph()
    {
        // 核心 登记出入口 跨框一步连边 同框入口之间预跑A*
        AddEntrances();
        AddInterEdges();
        AddIntraEdges();
    }

    /// <summary>
    /// 输入格子 已是入口则返回旧下标 否则追加Node
    /// </summary>
    private int AddNode(Vector2Int cell)
    {
        int cellIndex = data.ToIndex(cell);
        // 如果已经是入口则返回旧下标
        if (data.CellToNodeIndex[cellIndex] >= 0)
            return data.CellToNodeIndex[cellIndex];

        // 最后一个抽象点的下标
        int nodeIndex = data.NodeList.Count;

        // 添加抽象点
        data.NodeList.Add(new HPANode
        {
            Cell = cell,
            ClusterIndex = ToClusterIndex(cell)
        });

        // 记录该格对应抽象点下标
        data.CellToNodeIndex[cellIndex] = nodeIndex;

        // 添加抽象点连出去的边下标
        data.NodeEdgeList.Add(new List<int>());
        return nodeIndex;
    }

    /// <summary>
    /// 邻框能一步跨过去的格子登记成入口
    /// </summary>
    private void AddEntrances()
    {
        List<Vector2Int> cellList = CollectEntrances(data.Width, data.Height, data.Walkable, data.ClusterSize);
        for (int i = 0; i < cellList.Count; i++)
            AddNode(cellList[i]);
    }

    /// <summary>
    /// 输入可行走表和框边长 输出能跨到邻框的格子
    /// </summary>
    public static List<Vector2Int> CollectEntrances(int width, int height, bool[] walkable, int clusterSize)
    {
        var cellList = new List<Vector2Int>();
        if (clusterSize < 1)
            clusterSize = 5;
        int countX = (width + clusterSize - 1) / clusterSize;
        var marked = new bool[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int cellIndex = x + y * width;
                if (!walkable[cellIndex])
                    continue;
                int clusterIndex = (x / clusterSize) + (y / clusterSize) * countX;
                Vector2Int cell = new Vector2Int(x, y);
                for (int i = 0; i < NeighborList.Length; i++)
                {
                    Vector2Int next = cell + NeighborList[i];
                    if (next.x < 0 || next.x >= width || next.y < 0 || next.y >= height)
                        continue;
                    int nextIndex = next.x + next.y * width;
                    if (!walkable[nextIndex])
                        continue;
                    int nextCluster = (next.x / clusterSize) + (next.y / clusterSize) * countX;
                    // 核心 邻居在另一个框 两边都是出入口
                    if (nextCluster == clusterIndex)
                        continue;
                    if (!marked[cellIndex])
                    {
                        marked[cellIndex] = true;
                        cellList.Add(cell);
                    }
                    if (!marked[nextIndex])
                    {
                        marked[nextIndex] = true;
                        cellList.Add(next);
                    }
                }
            }
        }
        return cellList;
    }

    /// <summary>
    /// 邻框入口之间连跨一步的边
    /// </summary>
    private void AddInterEdges()
    {
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                int cellIndex = data.ToIndex(cell);
                if (!data.Walkable[cellIndex])
                    continue;
                int fromIndex = data.CellToNodeIndex[cellIndex];
                if (fromIndex < 0)
                    continue;

                int clusterIndex = ToClusterIndex(cell);
                for (int i = 0; i < NeighborList.Length; i++)
                {
                    Vector2Int next = cell + NeighborList[i];
                    if (!InBounds(next))
                        continue;
                    int nextIndex = data.ToIndex(next);
                    if (!data.Walkable[nextIndex])
                        continue;
                    if (ToClusterIndex(next) == clusterIndex)
                        continue;
                    if (cellIndex >= nextIndex)
                        continue;

                    int toIndex = data.CellToNodeIndex[nextIndex];
                    if (toIndex < 0)
                        continue;

                    int dirX = next.x - cell.x;
                    int dirY = next.y - cell.y;
                    float dirCost = (dirX != 0 && dirY != 0) ? Mathf.Sqrt(2f) : 1f;
                    // 核心 跨框这一步就是一条抽象边 格子路只有这两格
                    AddEdge(fromIndex, toIndex, dirCost * data.CostList[nextIndex], new List<Vector2Int> { cell, next });
                    AddEdge(toIndex, fromIndex, dirCost * data.CostList[cellIndex], new List<Vector2Int> { next, cell });
                }
            }
        }
    }

    /// <summary>
    /// 同一框入口两两在框内A* 存成边
    /// </summary>
    private void AddIntraEdges()
    {
        int clusterCount = data.ClusterCountX * data.ClusterCountY;
        var clusterNodeList = new List<int>[clusterCount];
        for (int i = 0; i < clusterCount; i++)
            clusterNodeList[i] = new List<int>();

        for (int i = 0; i < data.NodeList.Count; i++)
            clusterNodeList[data.NodeList[i].ClusterIndex].Add(i);

        for (int c = 0; c < clusterCount; c++)
        {
            List<int> nodeIndexList = clusterNodeList[c];
            for (int a = 0; a < nodeIndexList.Count; a++)
            {
                for (int b = a + 1; b < nodeIndexList.Count; b++)
                {
                    int fromIndex = nodeIndexList[a];
                    int toIndex = nodeIndexList[b];
                    Vector2Int fromCell = data.NodeList[fromIndex].Cell;
                    Vector2Int toCell = data.NodeList[toIndex].Cell;
                    // 核心 同框两个入口做一次小A* 整段格子路存进一条边
                    if (TrySearchInCluster(fromCell, toCell, c, out float cost, out List<Vector2Int> cellList))
                        AddEdge(fromIndex, toIndex, cost, cellList);
                    if (TrySearchInCluster(toCell, fromCell, c, out float backCost, out List<Vector2Int> backList))
                        AddEdge(toIndex, fromIndex, backCost, backList);
                }
            }
        }
    }

    /// <summary>
    /// 把起点终点接进所在框的入口
    /// </summary>
    private void InsertStartEnd()
    {
        int startCellIndex = data.ToIndex(data.Start);
        int endCellIndex = data.ToIndex(data.End);
        bool startWasNode = data.CellToNodeIndex[startCellIndex] >= 0;
        bool endWasNode = data.CellToNodeIndex[endCellIndex] >= 0;
        data.StartNodeIndex = AddNode(data.Start);
        data.EndNodeIndex = AddNode(data.End);
        // 核心 起点终点本来不是入口才临时接上 只连本框已有入口
        if (!startWasNode)
            ConnectNodeInCluster(data.StartNodeIndex);
        if (!endWasNode && data.EndNodeIndex != data.StartNodeIndex)
            ConnectNodeInCluster(data.EndNodeIndex);
    }

    /// <summary>
    /// 输入新抽象点 与同框已有入口做框内A*连边
    /// </summary>
    private void ConnectNodeInCluster(int nodeIndex)
    {
        int clusterIndex = data.NodeList[nodeIndex].ClusterIndex;
        Vector2Int cell = data.NodeList[nodeIndex].Cell;
        for (int i = 0; i < data.NodeList.Count; i++)
        {
            if (i == nodeIndex)
                continue;
            if (data.NodeList[i].ClusterIndex != clusterIndex)
                continue;
            Vector2Int other = data.NodeList[i].Cell;
            if (TrySearchInCluster(cell, other, clusterIndex, out float cost, out List<Vector2Int> cellList))
                AddEdge(nodeIndex, i, cost, cellList);
            if (TrySearchInCluster(other, cell, clusterIndex, out float backCost, out List<Vector2Int> backList))
                AddEdge(i, nodeIndex, backCost, backList);
        }
    }

    /// <summary>
    /// 输入两端抽象点 追加一条有向边
    /// </summary>
    private void AddEdge(int fromIndex, int toIndex, float cost, List<Vector2Int> cellList)
    {
        int edgeIndex = data.EdgeList.Count;
        data.EdgeList.Add(new HPAEdge
        {
            FromIndex = fromIndex,
            ToIndex = toIndex,
            Cost = cost,
            CellList = cellList
        });
        data.NodeEdgeList[fromIndex].Add(edgeIndex);
    }

    #endregion

    #region 查询

    /// <summary>
    /// 入口图建好后把起点塞进抽象开集
    /// </summary>
    private void InitAbsSearch()
    {
        int nodeCount = data.NodeList.Count;
        data.OpenList = new List<HPAAbsNode>();
        data.AbsGList = new float[nodeCount];
        data.AbsComputed = new bool[nodeCount];
        data.AbsParentEdgeIndex = new int[nodeCount];
        data.StepCellList.Clear();
        data.PathList.Clear();
        data.Found = false;

        for (int i = 0; i < nodeCount; i++)
        {
            data.AbsGList[i] = -1;
            data.AbsComputed[i] = false;
            data.AbsParentEdgeIndex[i] = -1;
        }

        int cellCount = data.Width * data.Height;
        for (int i = 0; i < cellCount; i++)
            data.ParentIndexList[i] = -1;

        if (data.StartNodeIndex == data.EndNodeIndex)
        {
            data.Found = true;
            data.PathList.Add(data.Start);
            data.Current = data.Start;
            data.StepCellList.Add(data.Start);
            return;
        }

        data.OpenList.Add(new HPAAbsNode
        {
            NodeIndex = data.StartNodeIndex,
            G = 0,
            F = QiFangFunction(data.Start)
        });
        data.AbsGList[data.StartNodeIndex] = 0;
        if (data.OpenList.Count > data.PeakOpenCount)
            data.PeakOpenCount = data.OpenList.Count;
    }

    /// <summary>
    /// 输入当前抽象点 把更便宜的邻接边对端塞进开集
    /// </summary>
    private void Expand(int nodeIndex)
    {
        List<int> edgeIndexList = data.NodeEdgeList[nodeIndex];
        for (int i = 0; i < edgeIndexList.Count; i++)
        {
            int edgeIndex = edgeIndexList[i];
            HPAEdge edge = data.EdgeList[edgeIndex];
            int nextIndex = edge.ToIndex;
            if (data.AbsComputed[nextIndex])
                continue;

            // 核心 沿抽象边扩 花费用边上存好的Cost 不再一格一格走
            float newG = data.CurrentG + edge.Cost;
            if (data.AbsGList[nextIndex] >= 0 && newG >= data.AbsGList[nextIndex])
                continue;

            data.AbsGList[nextIndex] = newG;
            data.AbsParentEdgeIndex[nextIndex] = edgeIndex;
            Vector2Int nextCell = data.NodeList[nextIndex].Cell;
            data.OpenList.Add(new HPAAbsNode
            {
                NodeIndex = nextIndex,
                G = newG,
                F = newG + QiFangFunction(nextCell)
            });
            if (data.OpenList.Count > data.PeakOpenCount)
                data.PeakOpenCount = data.OpenList.Count;
        }
    }

    /// <summary>
    /// 沿抽象父边把CellList拼成格子路
    /// </summary>
    private void ReconstructPath()
    {
        data.PathList.Clear();
        var nodeIndexList = new List<int>();
        int nodeIndex = data.EndNodeIndex;
        while (true)
        {
            nodeIndexList.Add(nodeIndex);
            int edgeIndex = data.AbsParentEdgeIndex[nodeIndex];
            if (edgeIndex < 0)
                break;
            nodeIndex = data.EdgeList[edgeIndex].FromIndex;
        }

        nodeIndexList.Reverse();
        for (int i = 0; i < nodeIndexList.Count; i++)
        {
            if (i == 0)
            {
                data.PathList.Add(data.NodeList[nodeIndexList[i]].Cell);
                continue;
            }

            int edgeIndex = data.AbsParentEdgeIndex[nodeIndexList[i]];
            // 核心 抽象点序列倒回去 把每条边里存的格子路拼成最终路径
            List<Vector2Int> cellList = data.EdgeList[edgeIndex].CellList;
            for (int c = 1; c < cellList.Count; c++)
            {
                data.PathList.Add(cellList[c]);
                int prevIndex = data.ToIndex(cellList[c - 1]);
                data.ParentIndexList[data.ToIndex(cellList[c])] = prevIndex;
            }
        }
    }

    /// <summary>
    /// 本步沿父边格子上色
    /// </summary>
    private void FillStepCells(int nodeIndex)
    {
        data.StepCellList.Clear();
        int edgeIndex = data.AbsParentEdgeIndex[nodeIndex];
        if (edgeIndex < 0)
        {
            data.StepCellList.Add(data.NodeList[nodeIndex].Cell);
            return;
        }

        List<Vector2Int> cellList = data.EdgeList[edgeIndex].CellList;
        for (int i = 0; i < cellList.Count; i++)
        {
            data.StepCellList.Add(cellList[i]);
            if (i == 0)
                continue;
            data.ParentIndexList[data.ToIndex(cellList[i])] = data.ToIndex(cellList[i - 1]);
        }
    }

    #endregion

    #region 框内A*

    /// <summary>
    /// 输入同框两端 输出框内A*花费和格子路
    /// </summary>
    private bool TrySearchInCluster(Vector2Int start, Vector2Int goal, int clusterIndex, out float cost, out List<Vector2Int> cellList)
    {
        cost = 0;
        cellList = null;
        if (start == goal)
        {
            cellList = new List<Vector2Int> { start };
            return true;
        }

        int cellCount = data.Width * data.Height;
        data.GridOpenList.Clear();
        for (int i = 0; i < cellCount; i++)
        {
            data.GList[i] = -1;
            data.Computed[i] = false;
        }

        data.GridOpenList.Add(new HPAGridNode { Cell = start, G = 0, F = QiFangTo(start, goal) });
        data.GList[data.ToIndex(start)] = 0;
        int[] parentIndexList = new int[cellCount];
        for (int i = 0; i < cellCount; i++)
            parentIndexList[i] = -1;
        if (data.GridOpenList.Count > data.PeakOpenCount)
            data.PeakOpenCount = data.GridOpenList.Count;

        while (data.GridOpenList.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < data.GridOpenList.Count; i++)
            {
                if (data.GridOpenList[i].F < data.GridOpenList[best].F)
                    best = i;
            }

            HPAGridNode node = data.GridOpenList[best];
            data.GridOpenList.RemoveAt(best);
            int index = data.ToIndex(node.Cell);
            if (data.Computed[index] || data.GList[index] < node.G)
                continue;

            data.Computed[index] = true;
            data.PopCount++;
            if (node.Cell == goal)
            {
                cost = node.G;
                cellList = BuildGridPath(goal, parentIndexList);
                return true;
            }

            for (int i = 0; i < NeighborList.Length; i++)
            {
                Vector2Int next = node.Cell + NeighborList[i];
                if (!InBounds(next))
                    continue;
                // 核心 和普通A*相同 邻居出了本框就丢掉
                if (ToClusterIndex(next) != clusterIndex)
                    continue;
                int nextIndex = data.ToIndex(next);
                if (!data.Walkable[nextIndex])
                    continue;
                if (data.Computed[nextIndex])
                    continue;

                int dirX = next.x - node.Cell.x;
                int dirY = next.y - node.Cell.y;
                float dirCost = (dirX != 0 && dirY != 0) ? Mathf.Sqrt(2f) : 1f;
                float newG = node.G + dirCost * data.CostList[nextIndex];
                if (data.GList[nextIndex] >= 0 && newG >= data.GList[nextIndex])
                    continue;

                data.GList[nextIndex] = newG;
                parentIndexList[nextIndex] = index;
                data.GridOpenList.Add(new HPAGridNode
                {
                    Cell = next,
                    G = newG,
                    F = newG + QiFangTo(next, goal)
                });
                if (data.GridOpenList.Count > data.PeakOpenCount)
                    data.PeakOpenCount = data.GridOpenList.Count;
            }
        }

        return false;
    }

    /// <summary>
    /// 输入终点和框内父表 输出格子路
    /// </summary>
    private List<Vector2Int> BuildGridPath(Vector2Int goal, int[] parentIndexList)
    {
        var cellList = new List<Vector2Int>();
        Vector2Int cell = goal;
        while (true)
        {
            cellList.Add(cell);
            int parentIndex = parentIndexList[data.ToIndex(cell)];
            if (parentIndex < 0)
                break;
            cell = data.ToCell(parentIndex);
        }
        cellList.Reverse();
        return cellList;
    }

    #endregion

    /// <summary>
    /// 输入格子 返回所在抽象框索引
    /// </summary>
    private int ToClusterIndex(Vector2Int cell)
    {
        int clusterX = cell.x / data.ClusterSize;
        int clusterY = cell.y / data.ClusterSize;
        return clusterX + clusterY * data.ClusterCountX;
    }

    private bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0
                && cell.x < data.Width
                && cell.y >= 0
                && cell.y < data.Height;
    }

    /// <summary>
    /// 启发式函数 Octile距离
    /// </summary>
    private float QiFangFunction(Vector2Int cell)
    {
        return QiFangTo(cell, data.End);
    }

    /// <summary>
    /// 输入格子和目标 输出八邻估计
    /// </summary>
    private float QiFangTo(Vector2Int cell, Vector2Int goal)
    {
        int dirX = Mathf.Abs(cell.x - goal.x);
        int dirY = Mathf.Abs(cell.y - goal.y);
        int min = dirX < dirY ? dirX : dirY;
        int max = dirX > dirY ? dirX : dirY;
        return min * Mathf.Sqrt(2f) + (max - min) * 1f;
    }

    private static readonly Vector2Int[] NeighborList = new Vector2Int[]
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(1, 1),
    };
}
