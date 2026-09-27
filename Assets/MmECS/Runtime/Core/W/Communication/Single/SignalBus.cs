using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 按固定注册顺序同步派发世界内信号 禁止同类型递归且不保存消息
    /// </summary>
    public sealed class SignalBus
    {
        /// <summary> 信号所属的世界 </summary>
        private readonly World world;
        /// <summary> 各信号类型的强类型处理器列表 </summary>
        private readonly Dictionary<Type, object> channelDict = new Dictionary<Type, object>();
        /// <summary> 初始化完成后不再允许更改处理器配置 </summary>
        private bool sealedRegistration;

        /// <summary>
        /// 绑定唯一所属世界
        /// </summary>
        internal SignalBus(World World) => world = World;

        /// <summary>
        /// 在初始化配置阶段按调用顺序登记处理器 同一实例同一类型只登记一次
        /// </summary>
        public void Register<T>(ISignalHandler<T> Handler) where T : unmanaged
        {
            world.EnsureCommunicationConfiguration();
            if (sealedRegistration)
                throw new InvalidOperationException("Signal registration is sealed");
            if (!channelDict.TryGetValue(typeof(T), out var Value))
            {
                Value = new Channel<T>();
                channelDict.Add(typeof(T), Value);
            }
            var Channel = (Channel<T>)Value;
            foreach (var Existing in Channel.handlerList)
                if (ReferenceEquals(Existing, Handler))
                    throw new InvalidOperationException("Signal handler is already registered");
            Channel.handlerList.Add(Handler);
        }

        /// <summary>
        /// 在模拟步骤内同步执行所有响应 无处理器时不产生工作或分配
        /// </summary>
        public void Emit<T>(in T Signal) where T : unmanaged
        {
            world.EnsureMessageEmission();
            if (channelDict.TryGetValue(typeof(T), out var Value))
                ((Channel<T>)Value).Dispatch(world, in Signal);
        }

        /// <summary>
        /// 固定处理器配置 恢复快照时保留原注册顺序
        /// </summary>
        internal void Seal() => sealedRegistration = true;

        /// <summary>
        /// 释放处理器引用并永久关闭注册
        /// </summary>
        internal void Clear()
        {
            channelDict.Clear();
            sealedRegistration = true;
        }

        /// <summary>
        /// 以泛型接口调用处理器 避免将消息装箱到非泛型容器
        /// </summary>
        private sealed class Channel<T> where T : unmanaged
        {
            /// <summary> 按注册顺序调用的处理器 </summary>
            internal readonly List<ISignalHandler<T>> handlerList = new List<ISignalHandler<T>>();
            /// <summary> 当前类型是否正在派发 用于拒绝直接与间接递归 </summary>
            private bool dispatching;

            /// <summary>
            /// 允许嵌套其他信号类型且在处理异常后释放递归保护
            /// </summary>
            internal void Dispatch(World World, in T Signal)
            {
                if (dispatching)
                    throw new InvalidOperationException("Recursive signal dispatch is not allowed");
                dispatching = true;
                try
                {
                    for (int index = 0; index < handlerList.Count; index++)
                        handlerList[index].OnSignal(World, in Signal);
                }
                finally { dispatching = false; }
            }
        }
    }
}
