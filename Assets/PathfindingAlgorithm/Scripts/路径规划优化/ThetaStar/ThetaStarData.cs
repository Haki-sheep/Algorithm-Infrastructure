using System.Collections.Generic;
using UnityEngine;

/// <summary> 开集里一格 带着塞进去时的 g 和 f </summary>
public struct ThetaStarNode
{
    public Vector2Int Cell;
    public float G;
    public float F;
}

public class ThetaStarData
{
    public int Width;
    public int Height;
    public bool[] Walkable;

    // 该格地形权重 普通1 贵地1.5/2/3
    public float[] CostList;
    public Vector2Int Start;
    public Vector2Int Goal;

    // 等待计算的格子 每步取 f 最小
    public List<ThetaStarNode> OpenList;
    // 从起点到该格当前的 g 没到过为 -1
    public float[] GList;
    // 弹出后才 true
    public bool[] Computed;
    public int[] ParentIndexList;

    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentG;

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

    public void Init(int width, int height, bool[] walkable, float[] costList, Vector2Int start, Vector2Int goal)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        CostList = costList;
        Start = start;
        Goal = goal;

        int cellCount = width * height;
        OpenList = new List<ThetaStarNode>();
        GList = new float[cellCount];
        Computed = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        Current = start;
        CurrentG = 0f;
        Found = false;
        PopCount = 0;
        PeakOpenCount = 0;

        for (int i = 0; i < cellCount; i++)
        {
            GList[i] = -1f;
            Computed[i] = false;
            ParentIndexList[i] = -1;
        }

        int startIndex = ToIndex(start);
        // 起点的父亲是自己 后面才能问「父格能不能看见邻居」
        ParentIndexList[startIndex] = startIndex;
        GList[startIndex] = 0f;
        OpenList.Add(new ThetaStarNode { Cell = start, G = 0f, F = 0f });
        PeakOpenCount = 1;
    }
}
