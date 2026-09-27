using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 保存模拟输出并在完整 Tick 边界按发送顺序交给表现层 支持订阅或手动确认
    /// </summary>
    public sealed partial class EventBus
    {
        /// <summary> 表现输出所属世界 </summary>
        private readonly World world;
        /// <summary> 当前 Tick 尚未成功发布的连续同类型批次 </summary>
        private readonly List<EventFrame> pendingList = new List<EventFrame>();
        /// <summary> 按发送顺序等待表现消费的已完成批次 </summary>
        private readonly Queue<EventFrame> frameQueue = new Queue<EventFrame>();
        /// <summary> 当前恢复批次内最近分配的输出顺序号 </summary>
        private long lastSequence;
        /// <summary> 当前是否正在调用表现订阅者 </summary>
        private bool dispatching;
        /// <summary> 模拟释放后禁止继续使用旧输出入口 </summary>
        private bool disposed;

        /// <summary>
        /// 绑定唯一所属世界
        /// </summary>
        internal EventBus(World World) => world = World;

        /// <summary>
        /// 在模拟步骤内复制纯数据事件 连续同类型事件共用一个批次
        /// </summary>
        public void Emit<T>(in T Event) where T : unmanaged
        {
            world.EnsureMessageEmission();
            var Frame = pendingList.Count == 0 ? null : pendingList[pendingList.Count - 1] as EventFrame<T>;
            if (Frame == null)
            {
                Frame = new EventFrame<T>(world, checked(++lastSequence));
                pendingList.Add(Frame);
            }
            Frame.Add(in Event);
        }

        /// <summary>
        /// 读取队首已发布批次 不移除以支持手动确认
        /// </summary>
        public bool TryPeek(out EventFrame Frame)
        {
            EnsureReadable();
            Frame = frameQueue.Count == 0 ? null : frameQueue.Peek();
            return Frame != null;
        }

        /// <summary>
        /// 仅确认当前队首对象 拒绝跨世界 旧恢复批次及重复确认
        /// </summary>
        public bool Acknowledge(EventFrame Frame)
        {
            EnsureReadable();
            if (frameQueue.Count == 0 || !ReferenceEquals(frameQueue.Peek(), Frame))
                return false;
            frameQueue.Dequeue();
            return true;
        }

        /// <summary>
        /// 同步消费已发布批次 无订阅者也会消费 回调异常时当前批次不重播且后续批次保留
        /// </summary>
        public int Dispatch()
        {
            EnsureReadable();
            dispatching = true;
            int count = 0;
            try
            {
                while (frameQueue.Count != 0)
                {
                    // 先领取再回调 避免异常重试让已经执行的表现重复播放
                    frameQueue.Dequeue().Deliver(this);
                    count++;
                }
                return count;
            }
            finally { dispatching = false; }
        }

        /// <summary>
        /// 在结构提交成功后发布独立批次 下个 Tick 不复用已发布的事件容器
        /// </summary>
        internal void Publish()
        {
            foreach (var Frame in pendingList)
                frameQueue.Enqueue(Frame);
            pendingList.Clear();
        }

        /// <summary>
        /// Tick 失败时丢弃未完成输出 已完成输出保持原样
        /// </summary>
        internal void DiscardPending() => pendingList.Clear();

        /// <summary>
        /// 恢复时清除旧时间线输出 保留表现订阅配置
        /// </summary>
        internal void Reset()
        {
            pendingList.Clear();
            frameQueue.Clear();
            lastSequence = 0;
        }

        /// <summary>
        /// 阻止回调重入及使用已释放的输出入口
        /// </summary>
        internal void EnsureIdle()
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(EventBus));
            if (dispatching)
                throw new InvalidOperationException("Event callbacks are executing");
        }

        /// <summary>
        /// 要求在完整逻辑步边界读取输出
        /// </summary>
        private void EnsureReadable()
        {
            EnsureIdle();
            world.EnsureSnapshotBoundary();
        }

        /// <summary>
        /// 模拟释放时丢弃输出并断开全部表现回调
        /// </summary>
        internal void Dispose()
        {
            Reset();
            foreach (var Group in subscriberDict.Values)
                Group.Clear();
            subscriberDict.Clear();
            disposed = true;
        }
    }
}
