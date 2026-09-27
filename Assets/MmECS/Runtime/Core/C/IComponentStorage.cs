namespace MmECS
{
    /// <summary>
    /// 为 World 提供跨组件类型的索引访问和移除及复制恢复契约
    /// </summary>
    public interface IComponentStorage
    {
        public int Count { get; }

        /// <summary>
        /// 根据稠密行号取得实体编号
        /// </summary>
        public int EntityIndexAt(int row);

        /// <summary>
        /// 判断实体是否拥有当前类型组件
        /// </summary>
        public bool Has(int entityIndex);

        /// <summary>
        /// 移除实体的当前类型组件
        /// </summary>
        public void Remove(int entityIndex);

        /// <summary>
        /// 复制组件数据和索引并返回独立组件池
        /// </summary>
        public IComponentStorage Clone();

        /// <summary>
        /// 从同类型组件池副本恢复数据和索引
        /// </summary>
        public void Restore(IComponentStorage Snapshot);

        /// <summary>
        /// 为低频诊断读取指定紧密行的装箱组件副本
        /// </summary>
        public object ReadBoxed(int row);
    }
}
