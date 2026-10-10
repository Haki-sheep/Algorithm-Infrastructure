using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JPS在等权八邻上逐格跳 只把跳点放进开集
/// 跳点画蓝框 强迫邻居画橙点 落点写下跳了几步
/// 贵地仍按普通格走 剪枝要求每步代价相同
/// </summary>
public class JPSCore
{
    private JPSData data;
    private readonly int[] dirBuffer = new int[8];
    private readonly List<Vector2Int> pendingForced = new List<Vector2Int>();
    private bool expanding;
    private int expandDirCount;
    private int expandDirIndex;
    private Vector2Int expandCell;
    private int expandParentIndex;

    public void Init(JPSData data)
    {
        this.data = data;
        expanding = false;
        expandDirCount = 0;
        expandDirIndex = 0;
    }

    /// <summary>
    /// 一步只跳一个方向 斜向时旁边的直线检查不上色
    /// </summary>
    public bool Step()
    {
        if (data.Found)
            return false;

        if (!expanding && !BeginNode())
            return false;
        if (data.Found)
            return false;
        if (!expanding)
            return data.OpenList.Count > 0;

        JumpOneDirection();
        expanding = expandDirIndex < expandDirCount;
        data.Expanding = expanding;
        return expanding || data.OpenList.Count > 0;
    }

    public bool Search()
    {
        while (Step()) ;
        return data.PathList.Count > 0;
    }

    /// <summary>
    /// 弹出一个跳点并记下还要跳的方向 过期节点直接丢掉
    /// </summary>
    private bool BeginNode()
    {
        while (data.OpenList.Count > 0)
        {
            int best = 0;
            for (int i = 1; i < data.OpenList.Count; i++)
            {
                if (data.OpenList[i].F < data.OpenList[best].F)
                    best = i;
            }

            JPSNode node = data.OpenList[best];
            data.OpenList.RemoveAt(best);
            int index = data.ToIndex(node.Cell);
            if (data.Computed[index] || data.GList[index] < node.G)
                continue;

            data.Current = node.Cell;
            data.CurrentG = node.G;
            data.Computed[index] = true;
            data.PopCount++;
            data.StepCellList.Clear();
            data.StepCellList.Add(node.Cell);
            JPSRules.AddUnique(data.JumpPointList, node.Cell);
            if (node.Cell == data.Goal)
            {
                data.Found = true;
                data.Expanding = false;
                expanding = false;
                JPSRules.Reconstruct(data.Width, data.Height, data.ParentIndexList, data.Goal, data.PathList);
                return true;
            }

            int fromParent = data.ParentIndexList[index];
            int dx = 0;
            int dy = 0;
            bool fromStart = fromParent < 0;
            if (!fromStart)
            {
                Vector2Int parent = data.ToCell(fromParent);
                dx = JPSRules.Sign(node.Cell.x - parent.x);
                dy = JPSRules.Sign(node.Cell.y - parent.y);
            }

            expandCell = node.Cell;
            expandParentIndex = index;
            expandDirCount = JPSRules.CollectDirs(node.Cell.x, node.Cell.y, dx, dy, fromStart, dirBuffer, data.Walkable, data.Width, data.Height);
            expandDirIndex = 0;
            expanding = expandDirCount > 0;
            data.Expanding = expanding;
            return true;
        }

        expanding = false;
        data.Expanding = false;
        return false;
    }

    /// <summary>
    /// 这一步只走出一条射线 第一步就撞墙的方向不单独占一步
    /// </summary>
    private void JumpOneDirection()
    {
        data.StepCellList.Clear();
        data.StepCellList.Add(expandCell);
        while (expandDirIndex < expandDirCount)
        {
            int dir = dirBuffer[expandDirIndex++];
            int dirX = JPSRules.DirX[dir];
            int dirY = JPSRules.DirY[dir];
            int before = data.StepCellList.Count;
            if (!TryJump(expandCell, dirX, dirY, out Vector2Int landing, out int steps))
            {
                if (data.StepCellList.Count == before)
                    continue;
                return;
            }

            int nextIndex = data.ToIndex(landing);
            if (!data.Computed[nextIndex])
            {
                float stepCost = (dirX != 0 && dirY != 0) ? JPSRules.Sqrt2 : 1f;
                float newG = data.CurrentG + steps * stepCost;
                if (data.GList[nextIndex] < 0f || newG < data.GList[nextIndex])
                {
                    data.GList[nextIndex] = newG;
                    data.ParentIndexList[nextIndex] = expandParentIndex;
                    CommitLanding(landing, steps);
                    data.OpenList.Add(new JPSNode
                    {
                        Cell = landing,
                        G = newG,
                        F = newG + JPSRules.Heuristic(landing, data.Goal)
                    });
                    if (data.OpenList.Count > data.PeakOpenCount)
                        data.PeakOpenCount = data.OpenList.Count;
                }
            }

            return;
        }
    }

    /// <summary>
    /// 在线跳 一直走到终点 强迫邻居 或斜向分量上出现跳点
    /// </summary>
    private bool TryJump(Vector2Int from, int dx, int dy, out Vector2Int landing, out int steps)
    {
        landing = from;
        steps = 0;
        pendingForced.Clear();
        int x = from.x;
        int y = from.y;
        bool diagonal = dx != 0 && dy != 0;
        while (JPSRules.CanStep(data.Walkable, data.Width, data.Height, x, y, dx, dy))
        {
            x += dx;
            y += dy;
            steps++;
            var mid = new Vector2Int(x, y);
            data.StepCellList.Add(mid);
            bool forced = JPSRules.HasForced(data.Walkable, data.Width, data.Height, x, y, dx, dy);
            if (forced)
                JPSRules.AppendForcedCells(pendingForced, data.Walkable, data.Width, data.Height, x, y, dx, dy);
            // 核心 空地上一直跳 直到终点或这里有强迫邻居才落成跳点
            if (mid == data.Goal || forced)
            {
                landing = mid;
                return true;
            }

            if (!diagonal)
                continue;

            bool cardinal = CardinalFinds(mid, dx, 0);
            if (CardinalFinds(mid, 0, dy))
                cardinal = true;
            if (cardinal)
            {
                landing = mid;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 直线上碰到终点或强迫邻居 当前斜向格也是跳点
    /// </summary>
    private bool CardinalFinds(Vector2Int from, int dx, int dy)
    {
        int x = from.x;
        int y = from.y;
        while (JPSRules.CanStep(data.Walkable, data.Width, data.Height, x, y, dx, dy))
        {
            x += dx;
            y += dy;
            bool forced = JPSRules.HasForced(data.Walkable, data.Width, data.Height, x, y, dx, dy);
            if (forced)
                JPSRules.AppendForcedCells(pendingForced, data.Walkable, data.Width, data.Height, x, y, dx, dy);
            if ((x == data.Goal.x && y == data.Goal.y) || forced)
                return true;
        }

        return false;
    }

    private void CommitLanding(Vector2Int landing, int steps)
    {
        JPSRules.AddUnique(data.JumpPointList, landing);
        JPSRules.SetDistance(data.DistanceCellList, data.DistanceValueList, landing, steps);
        for (int i = 0; i < pendingForced.Count; i++)
            JPSRules.AddUnique(data.ForcedCellList, pendingForced[i]);
    }
}
