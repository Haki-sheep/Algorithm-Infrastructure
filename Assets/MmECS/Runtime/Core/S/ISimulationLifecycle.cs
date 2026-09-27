
namespace MmECS
{
    /// <summary>
    /// 为需要初始化和资源释放的系统提供可选生命周期契约
    /// </summary>
    public interface ISimulationLifecycle : ISimulationSystem, ISystemLifecycle
    {
        // 继承原更新接口并复用 Core 生命周期契约 保持既有系统的注册行为
    }
}
