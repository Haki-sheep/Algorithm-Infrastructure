using System.Collections.Generic;
using UnityEngine;

public class JPSData
{
    public int Width;
    public int Height;
    public bool[] Walkable;

    public Vector2Int Start;
    public Vector2Int Goal;

    public List<JPSNode> OpenList;
    public float[] GList;
    public bool[] Computed;
    public int[] ParentIndexList;

    public List<Vector2Int> PathList;
    public Vector2Int Current;
    public float CurrentG;
    // 本步扫过或落到的格子
    public List<Vector2Int> StepCellList;

    // 已经确认的跳点
    public List<Vector2Int> JumpPointList;
    // 让跳停下来的强迫邻居
    public List<Vector2Int> ForcedCellList;
    // 落到该格的跳了几步
    public List<Vector2Int> DistanceCellList;
    public List<int> DistanceValueList;

    public bool Found;
    public int PopCount;
    public int PeakOpenCount;
    // 当前跳点还有方向没跳完 开集空了也要接着走
    public bool Expanding;

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
        OpenList = new List<JPSNode>();
        GList = new float[cellCount];
        Computed = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        StepCellList = new List<Vector2Int>();
        JumpPointList = new List<Vector2Int>();
        ForcedCellList = new List<Vector2Int>();
        DistanceCellList = new List<Vector2Int>();
        DistanceValueList = new List<int>();
        Current = start;
        CurrentG = 0;
        Found = false;
        PopCount = 0;
        PeakOpenCount = 0;
        Expanding = false;

        for (int i = 0; i < cellCount; i++)
        {
            GList[i] = -1;
            Computed[i] = false;
            ParentIndexList[i] = -1;
        }

        if (start == goal)
        {
            Found = true;
            PathList.Add(start);
            StepCellList.Add(start);
            JumpPointList.Add(start);
            return;
        }

        OpenList.Add(new JPSNode { Cell = start, G = 0, F = 0 });
        GList[ToIndex(start)] = 0;
        PeakOpenCount = 1;
    }
}
