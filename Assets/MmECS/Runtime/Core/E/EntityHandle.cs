using System;

namespace MmECS
{
    /// <summary>
    /// 以世界会话与恢复批次隔离宿主持有的实体身份
    /// </summary>
    public readonly struct EntityHandle
    {
        public Guid WorldId { get; }
        public ulong StateEpoch { get; }
        public Entity Entity { get; }

        /// <summary>
        /// 绑定实体所属世界及当前恢复批次
        /// </summary>
        internal EntityHandle(Guid worldId, ulong stateEpoch, Entity Entity)
        {
            WorldId = worldId;
            StateEpoch = stateEpoch;
            this.Entity = Entity;
        }
    }
}
