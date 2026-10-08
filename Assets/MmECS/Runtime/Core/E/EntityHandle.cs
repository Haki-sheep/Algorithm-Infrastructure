using System;

namespace MmECS
{
    /// <summary>
    /// 区分World之中 Entiry和所属时间线(StateEpoch)批次
    /// 比如读档前时间线 实体A 的 Position为(10,10) StateEpoch = 1,正常游玩时间线 A 的 Position为(20,20),则StateEpoch为2
    /// 如果玩家回到读档前 则 应该正确读取其Position为(10,10) 而不是正常游玩时候的(20,20) 哪怕其index和代际都相同
    /// </summary>
    public readonly struct EntityHandle
    {
        public Guid WorldId { get; }
        
        /// <summary>  世界状态批次号 用于不同时间线下的实体区分 /// </summary>
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
