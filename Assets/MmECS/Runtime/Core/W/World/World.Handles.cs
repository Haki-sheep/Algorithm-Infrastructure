using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 管理不随快照回退的宿主身份并验证及重新绑定外部引用
    /// </summary>
    public sealed partial class World
    {
        public Guid WorldId { get; } = Guid.NewGuid();
        public ulong StateEpoch { get; private set; } = 1;

        /// <summary>
        /// 将当前世界内的存活实体绑定为宿主句柄
        /// </summary>
        public EntityHandle GetEntityHandle(Entity Entity)
        {
            if (!IsAlive(Entity))
                throw new InvalidOperationException("Entity is not alive");

            return new EntityHandle(WorldId, StateEpoch, Entity);
        }

        /// <summary>
        /// 验证宿主句柄的世界与恢复批次及实体存活状态
        /// </summary>
        public bool TryResolve(EntityHandle Handle, out Entity Entity)
        {
            Entity = default;
            if (Handle.WorldId != WorldId || Handle.StateEpoch != StateEpoch || !IsAlive(Handle.Entity))
                return false;

            Entity = Handle.Entity;
            return true;
        }

        /// <summary>
        /// 将当前世界已分配的创建请求绑定为宿主句柄
        /// </summary>
        public SpawnRequestHandle GetSpawnRequestHandle(SpawnRequestId RequestId)
        {
            if (RequestId.Value <= 0 || RequestId.Value > lastSpawnRequestId)
                throw new InvalidOperationException("Spawn request is not allocated");

            return new SpawnRequestHandle(WorldId, StateEpoch, RequestId);
        }

        /// <summary>
        /// 验证宿主创建请求句柄并取走结果对应的实体句柄
        /// </summary>
        public bool TryConsumeSpawnResult(SpawnRequestHandle Handle, out EntityHandle EntityHandle)
        {
            EntityHandle = default;
            if (Handle.WorldId != WorldId || Handle.StateEpoch != StateEpoch)
                return false;

            if (!TryConsumeSpawnResult(Handle.RequestId, out var Result))
                return false;

            // 创建结果记录已发生的事实 实体是否仍存活由 TryResolve 检查
            EntityHandle = new EntityHandle(WorldId, StateEpoch, Result.Entity);
            return true;
        }

        /// <summary>
        /// 在完成 Tick 边界读取当前存活实体句柄用于宿主重新绑定
        /// </summary>
        public void ReadEntityHandles(List<EntityHandle> ResultList)
        {
            EnsureSnapshotBoundary();
            ResultList.Clear();
            for (int index = 0; index < aliveList.Count; index++)
            {
                if (aliveList[index])
                    ResultList.Add(new EntityHandle(WorldId, StateEpoch,
                        new Entity(index, generationList[index])));
            }
        }

        /// <summary>
        /// 在完成 Tick 边界读取未消费创建结果的当前宿主句柄
        /// </summary>
        public void ReadSpawnRequestHandles(List<SpawnRequestHandle> ResultList)
        {
            EnsureSnapshotBoundary();
            ResultList.Clear();
            foreach (var Result in spawnResultDict.Values)
                ResultList.Add(new SpawnRequestHandle(WorldId, StateEpoch, Result.RequestId));
        }
    }
}
