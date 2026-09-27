using System;

namespace MmECS
{
    /// <summary>
    /// 定义宿主可扩展的业务阶段 保留 Tick 提交和快照边界的框架控制权
    /// </summary>
    public partial class Simulation
    {
        public long CurrentTick => world.Tick;
        public Guid WorldId => world.WorldId;
        public ulong StateEpoch => world.StateEpoch;

        /// <summary>
        /// 在 Tick 开始且系统尚未执行时准备宿主输入 默认无额外工作
        /// </summary>
        protected virtual void BeforeTick() { }

        /// <summary>
        /// 在结构提交成功后完成宿主输出 此阶段不再允许发出模拟消息
        /// </summary>
        protected virtual void AfterStructuralCommit() { }

        /// <summary>
        /// 在系统与框架通信释放后清理宿主业务状态 默认无额外工作
        /// </summary>
        protected virtual void DisposeState() { }

        /// <summary>
        /// 要求宿主空闲且世界已经完成 Tick 并且没有待提交结构请求
        /// </summary>
        protected void EnsureCompletedTick()
        {
            EnsureIdle();
            world.EnsureSnapshotBoundary();
        }
    }
}
