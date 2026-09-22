using UnityEngine;

/// <summary>
/// A*先按UCS跑 弹出来一个格子时 要判断：
/// 1.开集里谁g最小
/// 2.这格算过没有 过期则丢掉
/// 3.是不是终点
/// 4.否则扩邻居
/// </summary>
public class AStarCore
{
    private AStarData data;

    public void Init(AStarData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出g最小的格 是终点或开集空则停 否则下一步再扩邻居
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.OpenList.Count == 0)
            return false;

        int best = 0;
        // 遍历开集 找到f最小的格子
        for (int i = 1; i < data.OpenList.Count; i++)
        {
            if (data.OpenList[i].F < data.OpenList[best].F)
                best = i;
        }

        // 弹出f最小的格子
        AStarNode node = data.OpenList[best];
        data.OpenList.RemoveAt(best);

        int index = data.ToIndex(node.Cell);
        // 如果该格已经计算过 则丢弃
        if (data.Computed[index]
        // 或者该格的g比当前最优g还大 则丢弃
        || data.GList[index] < node.G)
            return data.OpenList.Count > 0;

        // 更新当前格子和g
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
    /// 输入当前格子 把更便宜的可走邻居塞进开集
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

            // 计算方向
            int dirX = next.x - cell.x;
            int dirY = next.y - cell.y;

            // 计算方向代价 直线1 斜线1.414
            float dirCost = (dirX != 0 && dirY != 0) ? Mathf.Sqrt(2f) : 1f;

            // 计算新的g = 当前g + 方向代价 * 该格地形权重
            float newG = data.CurrentG + dirCost * data.CostList[nextIndex];

            // 到过且这次没更便宜
            if (data.GList[nextIndex] >= 0 && newG >= data.GList[nextIndex])
                continue;

            data.GList[nextIndex] = newG;
            data.ParentIndexList[nextIndex] = parentIndex;
            
            data.OpenList.Add(new AStarNode { 
                        Cell = next, G = newG, 
                        F = newG + QiFangFunction(next) });
                        
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
    /// <param name="cell"></param>
    /// <returns></returns>
    private float QiFangFunction(Vector2Int cell){
        // 横向差
        int dirX = Mathf.Abs(cell.x - data.Goal.x);
        // 纵向差
        int dirY = Mathf.Abs(cell.y - data.Goal.y);
        int min = dirX<dirY?dirX:dirY;
        int max = dirX>dirY?dirX:dirY;
        
        // Core:斜着走一步 = min和max都-1,min走完了以后就剩下max-min步直走了
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
