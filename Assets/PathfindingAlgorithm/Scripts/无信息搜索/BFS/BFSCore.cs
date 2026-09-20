using UnityEngine;


/// <summary>
/// BFS算法的核心思想：
/// 弹出来一个格子时 要判断：
/// 1.这格能不能走
/// 2.这格有没有来过
/// 3.下一步该看谁（先进先出）
/// 4.找到终点后怎么退回起点
/// </summary>
public class BFSCore
{
    private BFSData data;

    public void Init(BFSData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出一个格 是终点或队列空则停 否则下一步再扩邻居
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;

        if (data.Queue.Count == 0)
            return false;

        // 弹出一个格子
        data.Current = data.Queue.Dequeue();
        data.PopCount++;
        // 找到终点就停
        if (data.Current == data.Goal)
        {
            data.Found = true;
            ReconstructPath();
            return false;
        }

        // 扩邻居
        Expand(data.Current);

        return data.Queue.Count > 0;
    }

    public bool Search(){
        while(Step());
        return data.PathList.Count > 0;
    }


    /// <summary>
    /// 输入当前格子 把未访问的可走邻居入队
    /// </summary>
    private void Expand(Vector2Int cell)
    {
        // 暂时定当前格子的索引为父索引
        int parentIndex = data.ToIndex(cell);

        for (int i = 0; i < NeighborList.Length; i++)
        {
            // 下一个格子
            Vector2Int next = cell + NeighborList[i];
            if (!InBounds(next))
                continue;

            int nextIndex = data.ToIndex(next);
            if (!data.Walkable[nextIndex])
                continue;

            if (data.Visited[nextIndex])
                continue;

            // 标记为已访问 入队 记录父索引
            data.Visited[nextIndex] = true;
            data.Queue.Enqueue(next);
            data.ParentIndexList[nextIndex] = parentIndex;
            if (data.Queue.Count > data.PeakOpenCount)
                data.PeakOpenCount = data.Queue.Count;
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


    private static readonly Vector2Int[] NeighborList = new Vector2Int[]{
        new Vector2Int(-1,0),
        new Vector2Int(1,0),
        new Vector2Int(0,-1),
        new Vector2Int(0,1),
    };
}
