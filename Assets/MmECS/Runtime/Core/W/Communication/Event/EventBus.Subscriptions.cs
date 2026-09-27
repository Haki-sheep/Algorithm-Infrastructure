using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 管理强类型表现订阅和显式取消 不将表现监听者放入模拟状态
    /// </summary>
    public sealed partial class EventBus
    {
        /// <summary> 按事件类型绑定的表现订阅列表 </summary>
        private readonly Dictionary<Type, ISubscriptions> subscriberDict = new Dictionary<Type, ISubscriptions>();

        /// <summary>
        /// 逐条订阅纯数据事件并提供所属批次身份 返回由表现对象释放的订阅凭据
        /// </summary>
        public IDisposable Subscribe<T>(Action<EventFrame<T>, T> Handler) where T : unmanaged
        {
            EnsureIdle();
            if (!subscriberDict.TryGetValue(typeof(T), out var Value))
            {
                Value = new Subscriptions<T>();
                subscriberDict.Add(typeof(T), Value);
            }
            return ((Subscriptions<T>)Value).Add(Handler);
        }

        /// <summary>
        /// 将批次交给当前类型的处理器 按订阅顺序执行
        /// </summary>
        internal void Deliver<T>(EventFrame<T> Frame) where T : unmanaged
        {
            if (subscriberDict.TryGetValue(typeof(T), out var Group))
                ((Subscriptions<T>)Group).Deliver(Frame);
        }

        /// <summary>
        /// 为模拟释放提供异构订阅列表的统一清理入口
        /// </summary>
        private interface ISubscriptions
        {
            /// <summary>
            /// 断开全部订阅持有的表现回调
            /// </summary>
            public void Clear();
        }

        /// <summary>
        /// 保存同类型订阅并延后遍历期间的物理删除
        /// </summary>
        private sealed class Subscriptions<T> : ISubscriptions where T : unmanaged
        {
            /// <summary> 按订阅顺序调用的凭据列表 </summary>
            private readonly List<Subscription> subscriptionList = new List<Subscription>();
            /// <summary> 当前列表是否正在派发 </summary>
            private bool delivering;

            /// <summary>
            /// 建立订阅并返回独立的取消凭据
            /// </summary>
            internal IDisposable Add(Action<EventFrame<T>, T> Handler)
            {
                var Token = new Subscription(this, Handler);
                subscriptionList.Add(Token);
                return Token;
            }

            /// <summary>
            /// 立即跳过已取消订阅且异常时也清理失效凭据
            /// </summary>
            internal void Deliver(EventFrame<T> Frame)
            {
                delivering = true;
                try
                {
                    for (int row = 0; row < Frame.Events.Count; row++)
                        for (int index = 0; index < subscriptionList.Count; index++)
                            subscriptionList[index].handler?.Invoke(Frame, Frame.Events[row]);
                }
                finally
                {
                    delivering = false;
                    for (int index = subscriptionList.Count - 1; index >= 0; index--)
                        if (subscriptionList[index].handler == null)
                            subscriptionList.RemoveAt(index);
                }
            }

            /// <summary>
            /// 解除全部监听者引用 旧凭据随后释放仍安全
            /// </summary>
            public void Clear()
            {
                foreach (var Token in subscriptionList)
                {
                    Token.handler = null;
                    Token.owner = null;
                }
                subscriptionList.Clear();
            }

            /// <summary>
            /// 管理一个表现回调的显式且幂等的取消
            /// </summary>
            private sealed class Subscription : IDisposable
            {
                /// <summary> 订阅所属的强类型列表 </summary>
                internal Subscriptions<T> owner;
                /// <summary> 尚未取消的表现回调 </summary>
                internal Action<EventFrame<T>, T> handler;

                /// <summary>
                /// 保存订阅归属和回调
                /// </summary>
                internal Subscription(Subscriptions<T> Owner, Action<EventFrame<T>, T> Handler)
                {
                    owner = Owner;
                    handler = Handler;
                }

                /// <summary>
                /// 立即解除回调引用 遍历期间保留空位直到派发结束
                /// </summary>
                public void Dispose()
                {
                    var Owner = owner;
                    owner = null;
                    handler = null;
                    if (Owner != null && !Owner.delivering)
                        Owner.subscriptionList.Remove(this);
                }
            }
        }
    }
}
