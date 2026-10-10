using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JPS+先把每格八个方向的跳距算好 搜索时查表直接落到跳点
/// 跳点画蓝框 强迫邻居画橙点 落点写下跳了几步
/// 贵地仍按普通格走 剪枝要求每步代价相同
/// </summary>
public class JPSPlusCore
{
    private JPSPlusData data;
    private readonly int[] dirBuffer = new int[8];
    private readonly List<Vector2Int> pendingForced = new List<Vector2Int>();
    private bool expanding;
    private int expandDirCount;
    private int expandDirIndex;
    private Vector2Int expandCell;
    private int expandParentIndex;

    public void Init(JPSPlusData data)
    {
        this.data = data;
        expanding = false;
        expandDirCount = 0;
        expandDirIndex = 0;
        if (!data.Found)
            BuildJumpDist();
    }

    /// <summary>
    /// 一步只沿一个方向落到跳点 中间格子按直线补上
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
    /// 这一步只落一个方向 走不通的方向不单独占一步
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
            if (!TryJump(expandCell, dir, out Vector2Int landing, out int steps))
                continue;

            for (int s = 1; s <= steps; s++)
                data.StepCellList.Add(new Vector2Int(expandCell.x + dirX * s, expandCell.y + dirY * s));

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
    /// 先算四个直线跳距 再算斜向 斜向停在直线分量能跳到跳点的那一格
    /// </summary>
    private void BuildJumpDist()
    {
        int cellCount = data.Width * data.Height;
        data.JumpDist = new int[cellCount * 8];
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                if (JPSRules.Blocked(data.Walkable, data.Width, data.Height, x, y))
                    continue;
                int cellIndex = x + y * data.Width;
                for (int d = 0; d < 4; d++)
                    data.JumpDist[cellIndex * 8 + d] = ScanCardinal(x, y, JPSRules.DirX[d], JPSRules.DirY[d]);
            }
        }

        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++)
            {
                if (JPSRules.Blocked(data.Walkable, data.Width, data.Height, x, y))
                    continue;
                int cellIndex = x + y * data.Width;
                for (int d = 4; d < 8; d++)
                    data.JumpDist[cellIndex * 8 + d] = ScanDiagonal(x, y, JPSRules.DirX[d], JPSRules.DirY[d]);
            }
        }
    }

    private int ScanCardinal(int x, int y, int dx, int dy)
    {
        int steps = 0;
        while (JPSRules.CanStep(data.Walkable, data.Width, data.Height, x, y, dx, dy))
        {
            x += dx;
            y += dy;
            steps++;
            if (JPSRules.HasForced(data.Walkable, data.Width, data.Height, x, y, dx, dy))
                return -steps;
        }

        return steps;
    }

    private int ScanDiagonal(int x, int y, int dx, int dy)
    {
        int steps = 0;
        while (JPSRules.CanStep(data.Walkable, data.Width, data.Height, x, y, dx, dy))
        {
            x += dx;
            y += dy;
            steps++;
            if (JPSRules.HasForced(data.Walkable, data.Width, data.Height, x, y, dx, dy)
                || CardinalHasJump(x, y, dx, 0)
                || CardinalHasJump(x, y, 0, dy))
                return -steps;
        }

        return steps;
    }

    /// <summary>
    /// 查表落点 终点在跳点前就停在终点
    /// </summary>
    private bool TryJump(Vector2Int from, int dir, out Vector2Int landing, out int steps)
    {
        landing = from;
        steps = 0;
        pendingForced.Clear();
        int dist = data.JumpDist[data.ToIndex(from) * 8 + dir];
        if (dist == 0)
            return false;

        int dx = JPSRules.DirX[dir];
        int dy = JPSRules.DirY[dir];
        int maxWalk = dist < 0 ? -dist : dist;
        bool diagonal = dx != 0 && dy != 0;
        // 核心 不再逐格找强迫邻居 距离表直接告诉能落到哪
        for (int s = 1; s <= maxWalk; s++)
        {
            var mid = new Vector2Int(from.x + dx * s, from.y + dy * s);
            bool structural = dist < 0 && s == maxWalk;
            bool goalStop = mid == data.Goal || (diagonal && (CardinalReachesGoal(mid, dx, 0) || CardinalReachesGoal(mid, 0, dy)));
            if (!goalStop && !structural)
                continue;

            if (JPSRules.HasForced(data.Walkable, data.Width, data.Height, mid.x, mid.y, dx, dy))
                JPSRules.AppendForcedCells(pendingForced, data.Walkable, data.Width, data.Height, mid.x, mid.y, dx, dy);
            if (structural)
                ExplainCardinalForced(mid, dx, dy);
            landing = mid;
            steps = s;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 斜向跳点若是因为直线分量上还有跳点 把那边的强迫邻居也记下来
    /// </summary>
    private void ExplainCardinalForced(Vector2Int from, int dx, int dy)
    {
        if (dx == 0 || dy == 0)
            return;
        ExplainOneCardinal(from, dx, 0);
        ExplainOneCardinal(from, 0, dy);
    }

    private void ExplainOneCardinal(Vector2Int from, int dx, int dy)
    {
        int dist = data.JumpDist[data.ToIndex(from) * 8 + JPSRules.ToDir(dx, dy)];
        if (dist >= 0)
            return;
        int x = from.x + dx * -dist;
        int y = from.y + dy * -dist;
        if (!JPSRules.HasForced(data.Walkable, data.Width, data.Height, x, y, dx, dy))
            return;
        JPSRules.AppendForcedCells(pendingForced, data.Walkable, data.Width, data.Height, x, y, dx, dy);
    }

    private bool CardinalHasJump(int x, int y, int dx, int dy)
    {
        int dist = data.JumpDist[(x + y * data.Width) * 8 + JPSRules.ToDir(dx, dy)];
        return dist < 0;
    }

    private bool CardinalReachesGoal(Vector2Int from, int dx, int dy)
    {
        int goalDx = data.Goal.x - from.x;
        int goalDy = data.Goal.y - from.y;
        int goalSteps;
        if (dx != 0)
        {
            if (goalDy != 0 || goalDx * dx <= 0)
                return false;
            goalSteps = goalDx * dx;
        }
        else
        {
            if (goalDx != 0 || goalDy * dy <= 0)
                return false;
            goalSteps = goalDy * dy;
        }

        int dist = data.JumpDist[data.ToIndex(from) * 8 + JPSRules.ToDir(dx, dy)];
        if (dist == 0)
            return false;
        int maxWalk = dist < 0 ? -dist : dist;
        return goalSteps <= maxWalk;
    }

    private void CommitLanding(Vector2Int landing, int steps)
    {
        JPSRules.AddUnique(data.JumpPointList, landing);
        JPSRules.SetDistance(data.DistanceCellList, data.DistanceValueList, landing, steps);
        for (int i = 0; i < pendingForced.Count; i++)
            JPSRules.AddUnique(data.ForcedCellList, pendingForced[i]);
    }
}
