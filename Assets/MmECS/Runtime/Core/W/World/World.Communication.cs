using System;

namespace MmECS
{
    /// <summary>
    /// 持有世界独立的信号与表现事件并限制其模拟执行和释放边界
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 当前是否处于允许发出模拟消息的系统执行阶段 </summary>
        private bool messageEmissionAllowed;
        /// <summary> 所属模拟释放后禁止再次使用通信入口 </summary>
        private bool communicationDisposed;

        public SignalBus Signals { get; }
        public EventBus Events { get; }

        /// <summary>
        /// 将信号注册限制在世界初始化配置阶段
        /// </summary>
        internal void EnsureCommunicationConfiguration()
        {
            if (communicationDisposed)
                throw new ObjectDisposedException(nameof(World));
            EnsureInitializing();
        }

        /// <summary>
        /// 只允许在有效模拟步骤的系统阶段发出信号或表现事件
        /// </summary>
        internal void EnsureMessageEmission()
        {
            if (communicationDisposed)
                throw new ObjectDisposedException(nameof(World));
            if (!messageEmissionAllowed)
                throw new InvalidOperationException("Messages require an executing simulation step");
        }

        /// <summary>
        /// 步骤异常时关闭消息入口并丢弃尚未完成的表现输出
        /// </summary>
        internal void AbortMessages()
        {
            messageEmissionAllowed = false;
            Events.DiscardPending();
        }

        /// <summary>
        /// 模拟结束时释放处理器和订阅者引用
        /// </summary>
        internal void DisposeCommunication()
        {
            messageEmissionAllowed = false;
            communicationDisposed = true;
            Signals.Clear();
            Events.Dispose();
        }
    }
}
