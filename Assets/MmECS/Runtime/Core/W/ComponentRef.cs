namespace MmECS
{
    /// <summary>
    /// 绑定世界状态批次的组件引用 跨结构提交或恢复时自动失效
    /// </summary>
    public readonly struct ComponentRef<T> where T : unmanaged
    {
        private readonly World world;
        private readonly int entityIndex;
        private readonly ulong capturedEpoch;

        /// <summary>
        /// 绑定当前批次的组件引用
        /// </summary>
        internal ComponentRef(World World, int EntityIndex, ulong CapturedEpoch)
        {
            world = World;
            entityIndex = EntityIndex;
            capturedEpoch = CapturedEpoch;
        }

        /// <summary>
        /// 验证批次有效性并返回组件引用 跨结构提交或恢复后抛错
        /// </summary>
        public ref T Get()
        {
            if (world.StateEpoch != capturedEpoch)
                throw new System.InvalidOperationException(
                    $"ComponentRef captured at epoch {capturedEpoch} but world is now at {world.StateEpoch}");

            return ref world.GetStorage<T>().Get(entityIndex);
        }

        /// <summary>
        /// 检查引用是否仍然有效 不抛错
        /// </summary>
        public bool IsValid => world.StateEpoch == capturedEpoch;
    }
}
