using System.Collections.Generic;
using UnityEngine;

/// <summary> 开集里一格 带着塞进去时的g和f </summary>
public struct WAStarNode
{
    public Vector2Int Cell;
    public float G;
    public float F;
}

public class WAStarData
{
    public int Width;
    public int Height;
    public bool[] Walkable;
    public float[] CostList;
    public Vector2Int Start;
    public Vector2Int Goal;

    // 启发权重 F = G + W * H
    public float Weight;

    // 等待计算的格子 每步取F最小
    public List<WAStarNode> OpenList;
    // 从起点到该格当前的g 没到过为-1
    public float[] GList;
    // 是否计算过 弹出后才true 入开集不
    public bool[] Computed;
    public int[] ParentIndexList;

    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentG;
    public float CurrentF;

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

    public void Init(int width, int height, bool[] walkable, float[] costList, Vector2Int start, Vector2Int goal, float weight)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        CostList = costList;
        Start = start;
        Goal = goal;
        Weight = weight;

        int cellCount = width * height;
        OpenList = new List<WAStarNode>();
        GList = new float[cellCount];
        Computed = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        Current = start;
        CurrentG = 0;
        CurrentF = 0;
        Found = false;

        for (int i = 0; i < cellCount; i++)
        {
            GList[i] = -1;
            Computed[i] = false;
            ParentIndexList[i] = -1;
        }

        OpenList.Add(new WAStarNode { Cell = start, G = 0, F = 0 });
        GList[ToIndex(start)] = 0;
        PopCount = 0;
        PeakOpenCount = 1;
    }
}
