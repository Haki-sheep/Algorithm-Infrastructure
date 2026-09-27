namespace MmECS
{
    /// <summary>
    /// 约定模拟系统接收所属世界与固定步长执行一次逻辑更新
    /// </summary>
    public interface ISimulationSystem : ISystem
    {
        /// <summary>
        /// 在当前逻辑步执行一次系统更新
        /// </summary>
        public void Tick(World World, float stepSeconds);
    }
}
