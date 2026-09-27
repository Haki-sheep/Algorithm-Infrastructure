using System;

namespace MmECS
{
    /// <summary>
    /// 以世界会话与恢复批次隔离宿主持有的创建请求编号
    /// </summary>
    public readonly struct SpawnRequestHandle
    {
        public Guid WorldId { get; }
        public ulong StateEpoch { get; }
        public SpawnRequestId RequestId { get; }

        /// <summary>
        /// 绑定创建请求所属世界及当前恢复批次
        /// </summary>
        internal SpawnRequestHandle(Guid worldId, ulong stateEpoch, SpawnRequestId RequestId)
        {
            WorldId = worldId;
            StateEpoch = stateEpoch;
            this.RequestId = RequestId;
        }
    }
}
