namespace MmECS
{
    /// <summary>
    /// 保存创建请求编号与提交后正式实体的对应关系
    /// </summary>
    public readonly struct SpawnResult
    {
        public SpawnRequestId RequestId { get; }

        public Entity Entity { get; } 

        /// <summary>
        /// 关联创建请求与提交后生成的实体
        /// </summary>
        internal SpawnResult(
            SpawnRequestId RequestId, 
            Entity Entity)
        {
            this.RequestId = RequestId;
            this.Entity = Entity;
        }
    }
}
