/// <summary>
/// JPS+在JPS的状态上多一张八向跳距表
/// </summary>
public class JPSPlusData : JPSData
{
    // 每格8个方向 0走不通 正数是能走的步数 负数是跳点步数
    public int[] JumpDist;
}
