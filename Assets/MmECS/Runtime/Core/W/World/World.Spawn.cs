using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 分配创建请求编号并保存和消费请求对应的实体创建结果
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 最近分配的创建请求编号 </summary>
        private long lastSpawnRequestId;

        /// <summary> 请求编号对应的未消费创建结果 </summary>
        private readonly Dictionary<long, SpawnResult> spawnResultDict =
            new Dictionary<long, SpawnResult>();

        /// <summary>
        /// 分配当前世界内的下一个创建请求编号
        /// </summary>
        internal SpawnRequestId AllocateSpawnRequestId()
        {
            lastSpawnRequestId++;
            return new SpawnRequestId(lastSpawnRequestId);
        }

        /// <summary>
        /// 保存创建请求与正式实体的对应结果
        /// </summary>
        internal void PublishSpawnResult(
            SpawnRequestId RequestId,
            Entity Entity)
        {
            var Result = new SpawnResult(RequestId, Entity);
            spawnResultDict.Add(RequestId.Value, Result);
        }

        /// <summary>
        /// 按请求编号取走创建结果并返回是否成功
        /// </summary>
        public bool TryConsumeSpawnResult(
            SpawnRequestId RequestId,
            out SpawnResult Result)
        {
            if (!spawnResultDict.TryGetValue(RequestId.Value, out Result))
                return false;

            spawnResultDict.Remove(RequestId.Value);
            return true;
        }
    }
}
