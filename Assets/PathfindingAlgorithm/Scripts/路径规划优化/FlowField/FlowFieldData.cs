using UnityEngine;

/// <summary>
/// 流场的地图输入与计算结果
/// </summary>
public sealed class FlowFieldData
{
    /// <summary> 地图横向格数 </summary>
    public int Width;

    /// <summary> 地图纵向格数 </summary>
    public int Height;

    /// <summary> 每格是否可通行 </summary>
    public bool[] WalkableList;

    /// <summary> 进入每格的代价 </summary>
    public float[] CostList;

    /// <summary> 每格到终点的累计代价 </summary>
    public float[] IntegrationCostList;

    /// <summary> 每格指向下一格的方向 终点与不可达格为零向量 </summary>
    public Vector2Int[] DirectionList;

    /// <summary> 本次流场的终点 </summary>
    public Vector2Int Goal;

    /// <summary>
    /// 输入地图和终点 创建同尺寸结果数组
    /// </summary>
    public void Init(int width, int height, bool[] walkableList, float[] costList, Vector2Int goal)
    {
        Width = width;
        Height = height;
        WalkableList = walkableList;
        CostList = costList;
        Goal = goal;
        IntegrationCostList = new float[width * height];
        DirectionList = new Vector2Int[width * height];
    }

    /// <summary>
    /// 输入格子坐标 返回数组下标
    /// </summary>
    public int ToIndex(Vector2Int cell)
    {
        return cell.x + cell.y * Width;
    }

    /// <summary>
    /// 输入数组下标 返回格子坐标
    /// </summary>
    public Vector2Int ToCell(int index)
    {
        return new Vector2Int(index % Width, index / Width);
    }
}
