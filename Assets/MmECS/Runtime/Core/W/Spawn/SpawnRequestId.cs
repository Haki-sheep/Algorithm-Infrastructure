namespace MmECS
{
    /// <summary>
    /// 标识所属 World 内的一次延迟创建申请并用于关联结果
    /// </summary>
    public readonly struct SpawnRequestId
    {
        public long Value { get; }

        /// <summary>
        /// 保存世界分配的创建请求编号
        /// </summary>
        internal SpawnRequestId(long value) => Value = value;
    }
}
