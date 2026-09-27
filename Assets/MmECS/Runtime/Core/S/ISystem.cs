namespace MmECS
{
    /// <summary>
    /// 标识由模拟宿主持有并按显式顺序注册的系统
    /// </summary>
    public interface ISystem { }

    /// <summary>
    /// 为更新系统和仅信号系统提供可选的一次初始化与释放契约
    /// </summary>
    public interface ISystemLifecycle : ISystem
    {
        /// <summary>
        /// 在首次逻辑步之前初始化系统
        /// </summary>
        public void Init(World World);

        /// <summary>
        /// 在模拟结束时释放系统持有的资源
        /// </summary>
        public void Dispose(World World);
    }

    /// <summary>
    /// 在初始化时显式绑定本系统响应的信号 无需实现逐 Tick 更新
    /// </summary>
    public interface ISignalSystem : ISystem
    {
        /// <summary>
        /// 将本系统的强类型处理接口登记到所属世界
        /// </summary>
        public void BindSignals(SignalBus Signals);
    }

    /// <summary>
    /// 同步处理一种纯数据信号 影响未来的状态保存在世界内
    /// </summary>
    public interface ISignalHandler<T> where T : unmanaged
    {
        /// <summary>
        /// 在信号发出点读取消息并处理当前世界 不保留消息引用
        /// </summary>
        public void OnSignal(World World, in T Signal);
    }
}
