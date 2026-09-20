using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 记录要走的格子和那次入栈的深度
/// </summary>
public struct DLSNode
{
    public Vector2Int Cell;
    public int Depth;
}

public class DLSData
{
    // Logic:
    public int Width;
    public int Height;
    public bool[] Walkable;
    public Vector2Int Start;
    public Vector2Int Goal;
    // 深度上限 从起点沿当前枝最多走几步
    public int Limit;

    // DLS和DFS同用栈
    public Stack<DLSNode> Stack;
    // 该格已达到的最小深度 -1表示没来过
    public int[] DepthList;
    // 前驱格子索引列表
    public int[] ParentIndexList;
    // 记录路径
    public List<Vector2Int> PathList;
    // 本步弹出的格子
    public Vector2Int CurrentCell;
    // 本步弹出时这条枝的深度
    public int CurrentDepth;


    //ui
    // 是否已碰到终点
    public bool Found;
    // 弹出次数 对应时间复杂度里处理过的点数
    public int PopCount;
    // 开集曾经达到的最大长度 对应额外空间
    public int PeakOpenCount;


    public int ToIndex(Vector2Int cell)
    {
        return cell.x + cell.y * Width;
    }

    public Vector2Int ToCell(int index)
    {
        return new Vector2Int(index % Width, index / Width);
    }

    public void Init(int width, int height, bool[] walkable, Vector2Int start, Vector2Int goal, int limit)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        Start = start;
        Goal = goal;
        Limit = limit;

        int cellCount = width * height;
        Stack = new Stack<DLSNode>();
        DepthList = new int[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        CurrentCell = start;
        CurrentDepth = 0;
        Found = false;

        for (int i = 0; i < cellCount; i++)
        {
            DepthList[i] = -1;
            ParentIndexList[i] = -1;
        }

        Stack.Push(new DLSNode { Cell = start, Depth = 0 });
        DepthList[ToIndex(start)] = 0;
        PopCount = 0;
        PeakOpenCount = 1;
    }
}
