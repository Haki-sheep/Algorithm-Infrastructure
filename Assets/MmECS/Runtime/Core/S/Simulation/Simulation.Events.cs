
namespace MmECS
{
    /// <summary>
    /// 向表现宿主提供本模拟的通用事件订阅与完整 Tick 输出消费入口
    /// </summary>
    public partial class Simulation
    {
        public EventBus Events => world.Events;

        /// <summary>
        /// 在模拟空闲边界按发送顺序派发表现事件
        /// </summary>
        public int DispatchEvents()
        {
            EnsureIdle();
            return world.Events.Dispatch();
        }
    }
}
