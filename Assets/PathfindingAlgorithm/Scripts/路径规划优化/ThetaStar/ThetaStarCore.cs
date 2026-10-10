using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Theta*先按 A* 跑 弹出一格时多问一次父格
/// 父格能看见邻居就把邻居直接挂到父格上 路可以斜着穿格子
/// 看不见墙后的格子时 仍从当前格走一步
/// 斜向两侧都是墙就不许跨 只堵一侧仍可走
/// 启发式用欧氏距离 八方向的 octile 会把斜线估贵
/// 视线代价是两端欧氏距离乘落点地形 中间格只挡墙
/// </summary>
public class ThetaStarCore
{
    private ThetaStarData data;
    private readonly List<Vector2Int> sightBuffer = new List<Vector2Int>();

    public void Init(ThetaStarData data)
    {
        this.data = data;
    }

    /// <summary>
    /// 弹出 f 最小的格 是终点或开集空则停 否则下一步再扩邻居
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;
        if (data.OpenList.Count == 0)
            return false;

        int best = 0;
        for (int i = 1; i < data.OpenList.Count; i++)
        {
            float f = data.OpenList[i].F;
            float bestF = data.OpenList[best].F;
            // f 相同就先弹离终点更近的 少看一些空格
            if (f < bestF || (f == bestF && data.OpenList[i].G > data.OpenList[best].G))
                best = i;
        }

        ThetaStarNode node = data.OpenList[best];
        data.OpenList.RemoveAt(best);

        int index = data.ToIndex(node.Cell);
        if (data.Computed[index] || data.GList[index] < node.G)
            return data.OpenList.Count > 0;

        data.Current = node.Cell;
        data.CurrentG = node.G;
        data.Computed[index] = true;
        data.PopCount++;

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
    /// 输入当前格 能斜连父格的邻居就斜连 否则按这一步的长度入开集
    /// </summary>
    private void Expand(Vector2Int cell)
    {
        int currentIndex = data.ToIndex(cell);
        int parentIndex = data.ParentIndexList[currentIndex];
        Vector2Int parent = data.ToCell(parentIndex);

        for (int i = 0; i < NeighborList.Length; i++)
        {
            Vector2Int next = cell + NeighborList[i];
            if (!CanStep(cell, next))
                continue;
            int nextIndex = data.ToIndex(next);
            if (data.Computed[nextIndex])
                continue;

            // 核心 父格看见邻居就比一条斜线 看不见或斜线更贵就退回当前格
            bool useParent = parentIndex != currentIndex && LineOfSight(parent, next);
            if (useParent)
            {
                float viaParent = data.GList[parentIndex] + MoveCost(parent, next);
                float viaCell = data.GList[currentIndex] + MoveCost(cell, next);
                if (viaParent <= viaCell)
                {
                    Relax(parent, parentIndex, next, nextIndex, viaParent);
                    continue;
                }
            }

            Relax(cell, currentIndex, next, nextIndex, data.GList[currentIndex] + MoveCost(cell, next));
        }
    }

    private void Relax(Vector2Int from, int fromIndex, Vector2Int next, int nextIndex, float newG)
    {
        if (data.GList[nextIndex] >= 0f && newG >= data.GList[nextIndex])
            return;

        data.GList[nextIndex] = newG;
        data.ParentIndexList[nextIndex] = fromIndex;
        data.OpenList.Add(new ThetaStarNode
        {
            Cell = next,
            G = newG,
            F = newG + Heuristic(next)
        });
        if (data.OpenList.Count > data.PeakOpenCount)
            data.PeakOpenCount = data.OpenList.Count;
    }

    /// <summary>
    /// 沿父指针收回折点 再把每段视线铺成格子路
    /// </summary>
    private void ReconstructPath()
    {
        var bendList = new List<Vector2Int>();
        Vector2Int cell = data.Goal;
        int guard = data.Width * data.Height + 1;
        while (guard-- > 0)
        {
            bendList.Add(cell);
            int index = data.ToIndex(cell);
            int parentIndex = data.ParentIndexList[index];
            if (parentIndex < 0 || parentIndex == index)
                break;
            cell = data.ToCell(parentIndex);
        }

        bendList.Reverse();
        data.PathList.Clear();
        if (bendList.Count == 0)
            return;

        data.PathList.Add(bendList[0]);
        for (int i = 1; i < bendList.Count; i++)
            AppendSight(bendList[i - 1], bendList[i]);
    }

    /// <summary>
    /// 把 from 到 to 视线穿过的格子接进路径 箭头指向路径上的前一格
    /// </summary>
    private void AppendSight(Vector2Int from, Vector2Int to)
    {
        sightBuffer.Clear();
        if (!WalkLine(from, to, sightBuffer))
            return;

        int prevIndex = data.ToIndex(from);
        for (int i = 0; i < sightBuffer.Count; i++)
        {
            Vector2Int cell = sightBuffer[i];
            int index = data.ToIndex(cell);
            data.ParentIndexList[index] = prevIndex;
            data.PathList.Add(cell);
            prevIndex = index;
        }
    }

    /// <summary>
    /// 两端格子中心的连线不穿墙
    /// 刚好擦过拐角时 两侧正交格都是墙才挡住 只堵一侧仍可斜穿
    /// </summary>
    private bool LineOfSight(Vector2Int from, Vector2Int to)
    {
        return WalkLine(from, to, null);
    }

    /// <summary>
    /// 从 from 走到 to 不含 from 含 to
    /// sink 不为空时记下经过的格子
    /// </summary>
    private bool WalkLine(Vector2Int from, Vector2Int to, List<Vector2Int> sink)
    {
        int x = from.x;
        int y = from.y;
        int dx = to.x - x;
        int dy = to.y - y;
        if (dx == 0 && dy == 0)
            return true;

        int nx = dx >= 0 ? dx : -dx;
        int ny = dy >= 0 ? dy : -dy;
        int signX = dx >= 0 ? 1 : -1;
        int signY = dy >= 0 ? 1 : -1;
        int ix = 0;
        int iy = 0;

        while (ix < nx || iy < ny)
        {
            int cmpX = (ix * 2 + 1) * ny;
            int cmpY = (iy * 2 + 1) * nx;
            if (cmpX == cmpY)
            {
                if (!CanWalk(x + signX, y) && !CanWalk(x, y + signY))
                    return false;
                x += signX;
                y += signY;
                ix++;
                iy++;
            }
            else if (cmpX < cmpY)
            {
                x += signX;
                ix++;
            }
            else
            {
                y += signY;
                iy++;
            }

            if (!CanWalk(x, y))
                return false;
            if (sink != null)
                sink.Add(new Vector2Int(x, y));
        }

        return x == to.x && y == to.y;
    }

    private bool CanWalk(int x, int y)
    {
        if ((uint)x >= (uint)data.Width || (uint)y >= (uint)data.Height)
            return false;
        return data.Walkable[x + y * data.Width];
    }

    /// <summary>
    /// 斜向两侧都是墙就不许跨 只堵一侧仍可走
    /// </summary>
    private bool CanStep(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        if (!CanWalk(to.x, to.y))
            return false;
        if (dx != 0 && dy != 0 && !CanWalk(from.x + dx, from.y) && !CanWalk(from.x, from.y + dy))
            return false;
        return true;
    }

    /// <summary>
    /// 欧氏长度乘落点地形 邻格时就是直线 1 斜线 1.414
    /// </summary>
    private float MoveCost(Vector2Int from, Vector2Int to)
    {
        int dx = to.x - from.x;
        int dy = to.y - from.y;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return dist * data.CostList[data.ToIndex(to)];
    }

    /// <summary>
    /// 到终点的欧氏距离 地形至少为 1 所以不会估高
    /// </summary>
    private float Heuristic(Vector2Int cell)
    {
        int dx = cell.x - data.Goal.x;
        int dy = cell.y - data.Goal.y;
        return Mathf.Sqrt(dx * dx + dy * dy);
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
