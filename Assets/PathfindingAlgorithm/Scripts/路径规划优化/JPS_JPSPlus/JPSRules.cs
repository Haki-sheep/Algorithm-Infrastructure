using System.Collections.Generic;
using UnityEngine;

/// <summary> 开集里一个跳点 </summary>
public struct JPSNode
{
    public Vector2Int Cell;
    public float G;
    public float F;
}

/// <summary>
/// JPS和JPS+共用的八邻规则 斜向两侧都堵死才不许跨
/// </summary>
public static class JPSRules
{
    public static readonly float Sqrt2 = Mathf.Sqrt(2f);

    // 0西 1东 2南 3北 4西南 5西北 6东南 7东北
    public static readonly int[] DirX = { -1, 1, 0, 0, -1, -1, 1, 1 };
    public static readonly int[] DirY = { 0, 0, -1, 1, -1, 1, -1, 1 };

    public static int ToDir(int dx, int dy)
    {
        for (int i = 0; i < 8; i++)
        {
            if (DirX[i] == dx && DirY[i] == dy)
                return i;
        }

        return 0;
    }

    public static int Sign(int value)
    {
        if (value < 0)
            return -1;
        if (value > 0)
            return 1;
        return 0;
    }

    public static bool Blocked(bool[] walkable, int width, int height, int x, int y)
    {
        if ((uint)x >= (uint)width || (uint)y >= (uint)height)
            return true;
        return !walkable[x + y * width];
    }

    public static bool CanStep(bool[] walkable, int width, int height, int x, int y, int dx, int dy)
    {
        if (Blocked(walkable, width, height, x + dx, y + dy))
            return false;
        if (dx != 0 && dy != 0
            && Blocked(walkable, width, height, x + dx, y)
            && Blocked(walkable, width, height, x, y + dy))
            return false;
        return true;
    }

    /// <summary>
    /// 障碍把旁路堵住且对角那格还能走 这里就是强迫邻居
    /// </summary>
    public static bool HasForced(bool[] walkable, int width, int height, int x, int y, int dx, int dy)
    {
        if (dx != 0 && dy != 0)
        {
            if (Blocked(walkable, width, height, x - dx, y) && !Blocked(walkable, width, height, x - dx, y + dy))
                return true;
            if (Blocked(walkable, width, height, x, y - dy) && !Blocked(walkable, width, height, x + dx, y - dy))
                return true;
            return false;
        }

        if (dx != 0)
        {
            if (Blocked(walkable, width, height, x, y + 1) && !Blocked(walkable, width, height, x + dx, y + 1))
                return true;
            if (Blocked(walkable, width, height, x, y - 1) && !Blocked(walkable, width, height, x + dx, y - 1))
                return true;
            return false;
        }

        if (Blocked(walkable, width, height, x + 1, y) && !Blocked(walkable, width, height, x + 1, y + dy))
            return true;
        if (Blocked(walkable, width, height, x - 1, y) && !Blocked(walkable, width, height, x - 1, y + dy))
            return true;
        return false;
    }

    /// <summary>
    /// 把强迫邻居那一格写进列表
    /// </summary>
    public static void AppendForcedCells(List<Vector2Int> list, bool[] walkable, int width, int height, int x, int y, int dx, int dy)
    {
        if (dx != 0 && dy != 0)
        {
            if (Blocked(walkable, width, height, x - dx, y) && !Blocked(walkable, width, height, x - dx, y + dy))
                AddUnique(list, new Vector2Int(x - dx, y + dy));
            if (Blocked(walkable, width, height, x, y - dy) && !Blocked(walkable, width, height, x + dx, y - dy))
                AddUnique(list, new Vector2Int(x + dx, y - dy));
            return;
        }

        if (dx != 0)
        {
            if (Blocked(walkable, width, height, x, y + 1) && !Blocked(walkable, width, height, x + dx, y + 1))
                AddUnique(list, new Vector2Int(x + dx, y + 1));
            if (Blocked(walkable, width, height, x, y - 1) && !Blocked(walkable, width, height, x + dx, y - 1))
                AddUnique(list, new Vector2Int(x + dx, y - 1));
            return;
        }

        if (Blocked(walkable, width, height, x + 1, y) && !Blocked(walkable, width, height, x + 1, y + dy))
            AddUnique(list, new Vector2Int(x + 1, y + dy));
        if (Blocked(walkable, width, height, x - 1, y) && !Blocked(walkable, width, height, x - 1, y + dy))
            AddUnique(list, new Vector2Int(x - 1, y + dy));
    }

    /// <summary>
    /// 没父格就八向都跳 有父格只留来向的自然邻居和强迫邻居
    /// </summary>
    public static int CollectDirs(int x, int y, int dx, int dy, bool fromStart, int[] dirList, bool[] walkable, int width, int height)
    {
        if (fromStart)
        {
            for (int i = 0; i < 8; i++)
                dirList[i] = i;
            return 8;
        }

        int count = 0;
        if (dx != 0 && dy != 0)
        {
            dirList[count++] = ToDir(dx, dy);
            dirList[count++] = ToDir(dx, 0);
            dirList[count++] = ToDir(0, dy);
        }
        else
        {
            dirList[count++] = ToDir(dx, dy);
        }

        if (dx != 0 && dy != 0)
        {
            if (Blocked(walkable, width, height, x - dx, y) && !Blocked(walkable, width, height, x - dx, y + dy))
                dirList[count++] = ToDir(-dx, dy);
            if (Blocked(walkable, width, height, x, y - dy) && !Blocked(walkable, width, height, x + dx, y - dy))
                dirList[count++] = ToDir(dx, -dy);
            return count;
        }

        if (dx != 0)
        {
            if (Blocked(walkable, width, height, x, y + 1) && !Blocked(walkable, width, height, x + dx, y + 1))
                dirList[count++] = ToDir(dx, 1);
            if (Blocked(walkable, width, height, x, y - 1) && !Blocked(walkable, width, height, x + dx, y - 1))
                dirList[count++] = ToDir(dx, -1);
            return count;
        }

        if (Blocked(walkable, width, height, x + 1, y) && !Blocked(walkable, width, height, x + 1, y + dy))
            dirList[count++] = ToDir(1, dy);
        if (Blocked(walkable, width, height, x - 1, y) && !Blocked(walkable, width, height, x - 1, y + dy))
            dirList[count++] = ToDir(-1, dy);
        return count;
    }

    public static float Heuristic(Vector2Int cell, Vector2Int goal)
    {
        int dirX = Mathf.Abs(cell.x - goal.x);
        int dirY = Mathf.Abs(cell.y - goal.y);
        int min = dirX < dirY ? dirX : dirY;
        int max = dirX > dirY ? dirX : dirY;
        return min * Sqrt2 + (max - min);
    }

    public static void AddUnique(List<Vector2Int> list, Vector2Int cell)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == cell)
                return;
        }

        list.Add(cell);
    }

    public static void SetDistance(List<Vector2Int> cells, List<int> values, Vector2Int cell, int steps)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            if (cells[i] != cell)
                continue;
            values[i] = steps;
            return;
        }

        cells.Add(cell);
        values.Add(steps);
    }

    /// <summary>
    /// 沿跳点父链把中间格子补成连续路径
    /// </summary>
    public static void Reconstruct(int width, int height, int[] parentIndexList, Vector2Int goal, List<Vector2Int> pathList)
    {
        var jumpList = new List<Vector2Int>();
        Vector2Int cell = goal;
        while (true)
        {
            jumpList.Add(cell);
            int parentIndex = parentIndexList[cell.x + cell.y * width];
            if (parentIndex < 0)
                break;
            cell = new Vector2Int(parentIndex % width, parentIndex / width);
        }

        jumpList.Reverse();
        pathList.Clear();
        pathList.Add(jumpList[0]);
        int guardMax = width * height;
        for (int i = 1; i < jumpList.Count; i++)
        {
            Vector2Int from = jumpList[i - 1];
            Vector2Int to = jumpList[i];
            int dx = Sign(to.x - from.x);
            int dy = Sign(to.y - from.y);
            if (dx == 0 && dy == 0)
                continue;

            Vector2Int cursor = from;
            int guard = guardMax;
            while (cursor != to && guard-- > 0)
            {
                var next = new Vector2Int(cursor.x + dx, cursor.y + dy);
                parentIndexList[next.x + next.y * width] = cursor.x + cursor.y * width;
                pathList.Add(next);
                cursor = next;
            }
        }
    }
}
