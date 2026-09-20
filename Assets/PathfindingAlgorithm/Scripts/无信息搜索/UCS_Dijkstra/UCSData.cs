
using System.Collections.Generic;
using UnityEngine;
/// <summary> 开集里一格 带着塞进去时的g </summary>
public struct UCSNode
{
    public Vector2Int Cell;
    public float G;
}

public class UCSData
{
    // Logic:
    public int Width;
    public int Height;
    public bool[] Walkable;

    // 该格地形权重 普通1 贵地1.5/2/3 
    public float[] CostList;
    public Vector2Int Start;
    public Vector2Int Goal;

    // 等待计算的格子 每步取g最小
    public List<UCSNode> OpenList;
    // 从起点到该格当前的g 没到过为-1
    public float[] GList;
    // 是否计算过 弹出后才true 入开集不算
    public bool[] Computed;

    public int[] ParentIndexList;
    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentG;


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

    public void Init(int width, int height, bool[] walkable, float[] costList, Vector2Int start, Vector2Int goal)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        CostList = costList;
        Start = start;
        Goal = goal;

        int cellCount = width * height;
        OpenList = new List<UCSNode>();
        GList = new float[cellCount];
        Computed = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        Current = start;
        CurrentG = 0;
        Found = false;

        for (int i = 0; i < cellCount; i++)
        {
            GList[i] = -1;
            Computed[i] = false;
            ParentIndexList[i] = -1;
        }

        OpenList.Add(new UCSNode { Cell = start, G = 0 });
        GList[ToIndex(start)] = 0;
        PopCount = 0;
        PeakOpenCount = 1;
    }
}
