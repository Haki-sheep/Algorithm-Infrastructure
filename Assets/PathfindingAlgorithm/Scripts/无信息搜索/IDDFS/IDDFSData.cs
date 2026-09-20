using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 记录要走的格子和那次入栈的深度
/// </summary>
public struct IDDFSNode
{
    public Vector2Int Cell;
    public int Depth;
}

public class IDDFSData
{
    // Logic:
    public int Width;
    public int Height;
    public bool[] Walkable;
    public Vector2Int Start;
    public Vector2Int Goal;
    // 本轮深度上限 从0往上加
    public int Limit;
    // 本轮是否有格子因触顶没扩 全轮都没触顶说明图搜完了
    public bool HitLimit;

    // 和DLS同用栈
    public Stack<IDDFSNode> Stack;
    // 该格在本轮达到的最小深度 -1表示没来过
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
    // 弹出次数 跨轮累计 对应时间复杂度里处理过的点数
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

    public void Init(int width, int height, bool[] walkable, Vector2Int start, Vector2Int goal)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        Start = start;
        Goal = goal;
        Limit = 0;

        int cellCount = width * height;
        Stack = new Stack<IDDFSNode>();
        DepthList = new int[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        PopCount = 0;
        PeakOpenCount = 0;
        ResetRound();
    }

    /// <summary>
    /// 本轮没找到 上限加1 清枝重搜 统计不清
    /// </summary>
    public void NextLimit()
    {
        Limit++;
        ResetRound();
    }

    /// <summary>
    /// 清本轮栈和深度 起点重新入栈
    /// </summary>
    public void ResetRound()
    {
        Found = false;
        HitLimit = false;
        CurrentCell = Start;
        CurrentDepth = 0;
        PathList.Clear();
        Stack.Clear();

        int cellCount = Width * Height;
        // 重新初始化深度列表和前驱格子索引列表
        for (int i = 0; i < cellCount; i++)
        {
            DepthList[i] = -1;
            ParentIndexList[i] = -1;
        }

        // 起点重新入栈
        Stack.Push(new IDDFSNode { Cell = Start, Depth = 0 });
        DepthList[ToIndex(Start)] = 0;
        // 更新开集最大长度
        if (Stack.Count > PeakOpenCount)
            PeakOpenCount = Stack.Count;
    }
}
