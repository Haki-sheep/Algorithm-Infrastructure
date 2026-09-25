using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 抽象框图的一个点
/// </summary>
public struct HPANode
{
    public Vector2Int Cell;
    // 上层框索引
    public int ClusterIndex;
}

/// <summary>
/// 抽象框图的一条边
/// </summary>
public class HPAEdge
{
    public int FromIndex;
    public int ToIndex;

    public float Cost;
    // 从From格子走到To格子的路径
    public List<Vector2Int> CellList;
}

/// <summary> 抽象开集里一个点 带着塞进去时的g和f </summary>
public struct HPAAbsNode
{
    public int NodeIndex;
    public float G;
    public float F;
}

/// <summary> 框内A*开集里一格 </summary>
public struct HPAGridNode
{
    public Vector2Int Cell;
    public float G;
    public float F;
}

public class HPAStarData
{
    public int Width;
    public int Height;

    public bool[] Walkable;
    public float[] CostList;

    public Vector2Int Start;
    public Vector2Int End;

    // 框的数据
    public int ClusterSize;
    public int ClusterCountX;
    public int ClusterCountY;

    // 抽象出来的点总表
    public List<HPANode> NodeList;
    // 抽象出来的边总表
    public List<HPAEdge> EdgeList;
    // 每个抽象点连出去的边下标
    public List<List<int>> NodeEdgeList;
    // 该格对应抽象点下标 不是入口为-1
    public int[] CellToNodeIndex;

    public int StartNodeIndex;
    public int EndNodeIndex;

    // 抽象层开集 每步取F最小
    public List<HPAAbsNode> OpenList;
    public float[] AbsGList;
    public bool[] AbsComputed;
    public int[] AbsParentEdgeIndex;

    // 框内A*工作区
    public List<HPAGridNode> GridOpenList;
    public float[] GList;
    public bool[] Computed;
    public int[] ParentIndexList;

    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentG;
    // 本步要上色的格子 沿刚结算的抽象边
    public List<Vector2Int> StepCellList;

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

    public void Init(int width, int height, bool[] walkable, float[] costList, Vector2Int start, Vector2Int end, int clusterSize)
    {
        Width = width;
        Height = height;
        Walkable = walkable;
        CostList = costList;
        Start = start;
        End = end;
        ClusterSize = clusterSize < 1 ? 5 : clusterSize;
        ClusterCountX = (width + ClusterSize - 1) / ClusterSize;
        ClusterCountY = (height + ClusterSize - 1) / ClusterSize;

        int cellCount = width * height;
        NodeList = new List<HPANode>();
        EdgeList = new List<HPAEdge>();
        NodeEdgeList = new List<List<int>>();
        CellToNodeIndex = new int[cellCount];
        OpenList = new List<HPAAbsNode>();
        GridOpenList = new List<HPAGridNode>();
        GList = new float[cellCount];
        Computed = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        StepCellList = new List<Vector2Int>();
        Current = start;
        CurrentG = 0;
        Found = false;
        PopCount = 0;
        PeakOpenCount = 0;
        StartNodeIndex = -1;
        EndNodeIndex = -1;

        for (int i = 0; i < cellCount; i++)
        {
            CellToNodeIndex[i] = -1;
            GList[i] = -1;
            Computed[i] = false;
            ParentIndexList[i] = -1;
        }
    }
}
