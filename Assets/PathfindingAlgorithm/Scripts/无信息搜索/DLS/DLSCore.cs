using UnityEngine;
/// <summary>
/// DLS算法的核心思想：
/// 有上限的DFS
/// </summary>
public class DLSCore
{
    private DLSData data;
    public void Init(DLSData data)
    {
        this.data = data;
    }
    /// <summary>
    /// 弹出一个格 终点或栈空则停 过期枝和触顶则不扩
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.Stack.Count == 0)
            return false;
        DLSNode node = data.Stack.Pop();
        data.CurrentCell = node.Cell;
        data.CurrentDepth = node.Depth;
        data.PopCount++;
        // 同格已被更浅的枝更新过 这次弹出作废
        if (node.Depth > data.DepthList[data.ToIndex(node.Cell)])
            return data.Stack.Count > 0;

        if (data.CurrentCell == data.Goal)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }
        // 触到深度上限 不再长枝
        if (data.CurrentDepth < data.Limit)
            Expand(data.CurrentCell);
        return data.Stack.Count > 0;
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
            data.Stack.Push(new DLSNode { Cell = next, Depth = nextDepth });
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
