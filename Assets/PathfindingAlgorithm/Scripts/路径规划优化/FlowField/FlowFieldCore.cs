using System;

/// <summary>
/// 从终点反向生成累计代价与格子方向
/// </summary>
public sealed class FlowFieldCore
{
    /// <summary> 当前流场数据 </summary>
    private FlowFieldData Data;

    /// <summary>
    /// 输入流场数据 保存本次计算上下文
    /// </summary>
    public void Init(FlowFieldData data)
    {
        Data = data;
    }

    /// <summary>
    /// 从终点反向计算每格累计代价
    /// </summary>
    public void BuildIntegrationField()
    {
        throw new NotImplementedException("流场累计代价扩散尚未实现");
    }

    /// <summary>
    /// 根据累计代价计算每格方向
    /// </summary>
    public void BuildDirectionField()
    {
        throw new NotImplementedException("流场方向生成尚未实现");
    }
}
