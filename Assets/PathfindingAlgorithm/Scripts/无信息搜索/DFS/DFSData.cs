using System.Collections.Generic;
using UnityEngine;
public class DFSData
{
    // Logic:
    public int Width;
    public int Height;
    public bool[] Walkable;
    public Vector2Int Start;
    public Vector2Int Goal;



    // 记录要走的格子 后进先出
    public Stack<Vector2Int> Stack;
    // 记录已经走过的格子
    public bool[] Visited;
    // 前驱格子索引列表
    public int[] ParentIndexList;
    // 记录路径
    public List<Vector2Int> PathList;
    // 本步弹出的格子
    public Vector2Int Current;
    
    //ui
    // 是否已碰到终点
    public bool Found;
    // 弹出次数 对应时间复杂度里处理过的点数
    public int PopCount;
    // 开集曾经达到的最大长度 对应额外空间
    public int PeakOpenCount;

    public int ToIndex(Vector2Int cell){
        return cell.x + cell.y * Width;
    }

    public Vector2Int ToCell(int index){
        return new Vector2Int(index % Width, index / Width);
    }

    public void Init(int width, int height,bool[] walkable,Vector2Int start,Vector2Int goal){
        Width = width;
        Height = height;
        Walkable = walkable;
        Start = start;
        Goal = goal;

        int cellCount = width * height;
        Stack = new Stack<Vector2Int>();
        Visited = new bool[cellCount];
        ParentIndexList = new int[cellCount];
        PathList = new List<Vector2Int>();
        Current = start;
        Found = false;

        for(int i = 0; i < cellCount; i++){
            Visited[i] = false;
            ParentIndexList[i] = -1;
        }

        Stack.Push(start);
        Visited[ToIndex(start)] = true;
        PopCount = 0;
        PeakOpenCount = 1;
    }
}
