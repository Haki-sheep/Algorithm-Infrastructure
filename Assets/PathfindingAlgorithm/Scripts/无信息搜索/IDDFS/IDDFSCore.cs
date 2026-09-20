using UnityEngine;

/// <summary>
/// Iterative Deepening Depth-First Search
/// 迭代加深深度优先搜索
/// 是一种结合了深度优先搜索和广度优先搜索的算法
/// 通过逐步增加搜索深度来避免陷入无限循环
/// 同时保证找到最短路径
/// </summary>
public class IDDFSCore
{
    private IDDFSData data;

    public void Init(IDDFSData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出一个格 本轮栈空且还能加深则换上限 终点或搜完则停
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;

        if (data.Stack.Count == 0)
        {
            if (!data.HitLimit)
                return false;
            data.NextLimit();
        }

        IDDFSNode node = data.Stack.Pop();
        data.CurrentCell = node.Cell;
        data.CurrentDepth = node.Depth;
        data.PopCount++;
        
        // 同格已被更浅的枝更新过 这次弹出作废
        if (node.Depth > data.DepthList[data.ToIndex(node.Cell)])
            return data.Stack.Count > 0 || data.HitLimit;

        if (data.CurrentCell == data.Goal)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }

        // 触到深度上限 不再长枝 记一下还能加深
        if (data.CurrentDepth < data.Limit)
            Expand(data.CurrentCell);
        else
            data.HitLimit = true;

        if (data.Stack.Count > 0)
            return true;
        return data.HitLimit;
    }

    public bool Search()
    {
        while (Step()) ;
        return data.PathList.Count > 0;
    }

    private void Expand(Vector2Int cell)
    {
        int parentIndex = data.ToIndex(cell);
        int nextDepth = data.CurrentDepth + 1;
        for (int i = 0; i < NeighborList.Length; i++)
        {
            Vector2Int next = cell + NeighborList[i];
            if (!InBounds(next))
                continue;
            int nextIndex = data.ToIndex(next);
            if (!data.Walkable[nextIndex])
                continue;

            // 本格来过
            if (data.DepthList[nextIndex] >= 0
                // 而且这次没有更浅
                && nextDepth >= data.DepthList[nextIndex])
                continue;

            data.DepthList[nextIndex] = nextDepth;
            data.ParentIndexList[nextIndex] = parentIndex;
            data.Stack.Push(new IDDFSNode { Cell = next, Depth = nextDepth });
            if (data.Stack.Count > data.PeakOpenCount)
                data.PeakOpenCount = data.Stack.Count;
        }
    }

    /// <summary>
    /// 重建路径
    /// </summary>
    private void ReconstructPath()
    {
        data.PathList.Clear();
        Vector2Int cell = data.Goal;
        while (true)
        {
            // 添加当前格子
            data.PathList.Add(cell);
            // 获取父索引
            int parentIndex = data.ParentIndexList[data.ToIndex(cell)];
            if (parentIndex < 0)
                break;

            // 获取父格子
            cell = data.ToCell(parentIndex);
        }
        // 反转路径
        data.PathList.Reverse();
    }

    private bool InBounds(Vector2Int cell)
    {
        return cell.x >= 0
                && cell.x < data.Width
                && cell.y >= 0
                && cell.y < data.Height;
    }

    private static readonly Vector2Int[] NeighborList = new Vector2Int[]
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
    };
}
