using UnityEngine;

/// <summary>
/// WA*按A*跑 弹出来一个格子时 要判断：
/// 1.开集里谁F最小 F=G+W*H
/// 2.这格算过没有 过期则丢掉
/// 3.是不是终点
/// 4.否则扩邻居
/// </summary>
public class WAStarCore
{
    private WAStarData data;

    public void Init(WAStarData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出F最小的格 是终点或开集空则停 否则下一步再扩邻居
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.OpenList.Count == 0)
            return false;

        int best = 0;
        // 遍历开集 找到F最小的格子
        for (int i = 1; i < data.OpenList.Count; i++)
        {
            if (data.OpenList[i].F < data.OpenList[best].F)
                best = i;
        }

        // 弹出F最小的格子
        WAStarNode node = data.OpenList[best];
        data.OpenList.RemoveAt(best);

        int index = data.ToIndex(node.Cell);
        // 如果该格已经计算过 则丢弃
        if (data.Computed[index]
        // 或者该格的g比当前最优g还大 则丢弃
        || data.GList[index] < node.G)
            return data.OpenList.Count > 0;

        data.Current = node.Cell;
        data.CurrentG = node.G;
        data.CurrentF = node.F;
        data.Computed[index] = true;
        data.PopCount++;

        // 如果当前格是终点 则结束搜索
        if (data.Current == data.Goal)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }

        Expand(node.Cell);

        return data.OpenList.Count > 0;
    }

    public bool Search()
    {
        while (Step()) ;
        return data.PathList.Count > 0;
    }

    /// <summary>
    /// 输入当前格子 把更便宜的可走邻居按F=G+W*H塞进开集
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
            if (data.Computed[nextIndex])
                continue;

            int dirX = next.x - cell.x;
            int dirY = next.y - cell.y;
            float dirCost = (dirX != 0 && dirY != 0) ? Mathf.Sqrt(2f) : 1f;
            float newG = data.CurrentG + dirCost * data.CostList[nextIndex];

            // 到过且这次没更便宜
            if (data.GList[nextIndex] >= 0 && newG >= data.GList[nextIndex])
                continue;

            data.GList[nextIndex] = newG;
            data.ParentIndexList[nextIndex] = parentIndex;
            data.OpenList.Add(new WAStarNode
            {
                Cell = next,
                G = newG,
                F = newG + data.Weight * QiFangFunction(next)
            });
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
