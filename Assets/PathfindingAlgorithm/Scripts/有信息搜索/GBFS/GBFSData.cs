using System.Collections.Generic;
using UnityEngine;

/// <summary> 开集里一格 只带这格到终点的H </summary>
public struct GBFSNode
{
    public Vector2Int Cell;
    public float H;
}

public class GBFSData
{
    public int Width;
    public int Height;
    public bool[] Walkable;
    public Vector2Int Start;
    public Vector2Int Goal;

    // 等待计算的格子 每步取H最小
    public List<GBFSNode> OpenList;
    // 入过开集 同一格H不变 不重复塞
    public bool[] Generated;
    public int[] ParentIndexList;

    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentH;

    //ui
    public bool Found;
    public int PopCount;
    public int PeakOpenCount;

    public int ToIndex(Vector2Int cell)
    {
        return cell.x + cell.y * Width;
    }

    public Vector2Int ToCell(int index)
    {
        return new Vector2Int(index % Width, index / Width);
    }

    public void Init(int width, int height, bool[] walkable, Vector2Int start, Vector2Int goal)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        Start = start;
        Goal = goal;

        int cellCount = width * height;
        OpenList = new List<GBFSNode>();
        Generated = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        Current = start;
        CurrentH = 0;
        Found = false;

        for (int i = 0; i < cellCount; i++)
        {
            Generated[i] = false;
            ParentIndexList[i] = -1;
        }

        int startIndex = ToIndex(start);
        float startH = 0;
        OpenList.Add(new GBFSNode { Cell = start, H = startH });
        Generated[startIndex] = true;
        PopCount = 0;
        PeakOpenCount = 1;
    }
}
