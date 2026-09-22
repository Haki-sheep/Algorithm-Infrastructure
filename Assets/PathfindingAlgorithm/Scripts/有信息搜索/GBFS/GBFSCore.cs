using UnityEngine;

/// <summary>
/// Greedy Best-First Search
/// GBFS弹出来一个格子时 要判断：
/// 1.开集里谁H最小
/// 2.是不是终点
/// 3.否则扩邻居 邻居只记H
/// </summary>
public class GBFSCore
{
    private GBFSData data;

    public void Init(GBFSData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出H最小的格 是终点或开集空则停 否则下一步再扩邻居
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.OpenList.Count == 0)
            return false;

        int best = 0;
        // 遍历开集 找到H最小的格子
        for (int i = 1; i < data.OpenList.Count; i++)
        {
            if (data.OpenList[i].H < data.OpenList[best].H)
                best = i;
        }

        // 弹出H最小的格子
        GBFSNode node = data.OpenList[best];
        data.OpenList.RemoveAt(best);

        data.Current = node.Cell;
        data.CurrentH = node.H;
        data.PopCount++;

        // 如果当前格是终点 则结束搜索
        if (data.Current == data.Goal)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }

        // 扩邻居
        Expand(node.Cell);

        // 如果开集不为空 则继续搜索
        return data.OpenList.Count > 0;
    }

    public bool Search()
    {
        while (Step()) ;
        return data.PathList.Count > 0;
    }

    /// <summary>
    /// 输入当前格子 把没入过开集的可走邻居按H塞进开集
    /// </summary>
    private void Expand(Vector2Int cell)
    {
        int parentIndex = data.ToIndex(cell);
        for (int i = 0; i < NeighborList.Length; i++)
        {
            Vector2Int next = cell + NeighborList[i];
            if (!InBounds(next))
                continue;
            int nextIndex = data.ToIndex(next);
            if (!data.Walkable[nextIndex])
                continue;
            if (data.Generated[nextIndex])
                continue;

            data.ParentIndexList[nextIndex] = parentIndex;
            data.Generated[nextIndex] = true;
            data.OpenList.Add(new GBFSNode { Cell = next, H = QiFangFunction(next) });
            if (data.OpenList.Count > data.PeakOpenCount)
                data.PeakOpenCount = data.OpenList.Count;
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
            data.PathList.Add(cell);
            int parentIndex = data.ParentIndexList[data.ToIndex(cell)];
            if (parentIndex < 0)
                break;
            cell = data.ToCell(parentIndex);
        }
        data.PathList.Reverse();
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
        int dirX = Mathf.Abs(cell.x - data.Goal.x);
        int dirY = Mathf.Abs(cell.y - data.Goal.y);
        int min = dirX < dirY ? dirX : dirY;
        int max = dirX > dirY ? dirX : dirY;

        // Core:斜着走一步 = min和max都-1 min走完以后剩下max-min步直走
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
