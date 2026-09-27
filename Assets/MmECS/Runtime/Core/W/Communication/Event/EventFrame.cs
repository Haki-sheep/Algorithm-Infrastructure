using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 标识一个世界中按发送顺序发布的表现批次 不属于模拟快照
    /// </summary>
    public abstract class EventFrame
    {
        public Guid WorldId { get; }
        public ulong StateEpoch { get; }
        public long Tick { get; }
        public long Sequence { get; }

        /// <summary>
        /// 记录世界与恢复批次及目标 Tick 和表现批次顺序号
        /// </summary>
        internal EventFrame(World World, long sequence)
        {
            WorldId = World.WorldId;
            StateEpoch = World.StateEpoch;
            Tick = World.Tick + 1;
            Sequence = sequence;
        }

        /// <summary>
        /// 在表现消费阶段调用当前类型的订阅者
        /// </summary>
        internal abstract void Deliver(EventBus Events);
    }

    /// <summary>
    /// 保存同 Tick 内连续同类型事件的值副本 发布后只读且保持原始跨类型顺序
    /// </summary>
    public sealed class EventFrame<T> : EventFrame where T : unmanaged
    {
        /// <summary> 由当前批次独占的事件数据 发布后不再追加或复用 </summary>
        private readonly List<T> eventList = new List<T>();

        public IReadOnlyList<T> Events { get; }

        /// <summary>
        /// 创建独立输出批次并建立无法修改底层列表的只读视图
        /// </summary>
        internal EventFrame(World World, long sequence) : base(World, sequence)
        {
            Events = eventList.AsReadOnly();
        }

        /// <summary>
        /// 仅供模拟步骤向尚未发布的批次追加事件副本
        /// </summary>
        internal void Add(in T Event) => eventList.Add(Event);

        /// <summary>
        /// 将批次交给强类型订阅列表而不装箱事件数据
        /// </summary>
        internal override void Deliver(EventBus Events) => Events.Deliver(this);
    }
}
