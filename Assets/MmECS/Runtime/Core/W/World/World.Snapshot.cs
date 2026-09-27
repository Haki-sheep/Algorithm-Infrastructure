using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 在完整 Tick 边界捕获和恢复当前世界的核心状态并验证快照归属
    /// </summary>
    public sealed partial class World
    {
        /// <summary> 当前世界专属的快照归属标记 </summary>
        private readonly object snapshotOwner = new object();

        /// <summary>
        /// 独立保存实体分配器与组件及 Tick 和创建请求结果状态
        /// </summary>
        public sealed class Snapshot
        {
            /// <summary> 创建此快照的世界标记 </summary>
            internal readonly object owner;

            /// <summary> 世界单例的独立副本 </summary>
            internal readonly Dictionary<Type, IWorldResource> resourceDict;

            /// <summary> 可重复恢复的随机序列状态 </summary>
            internal readonly DeterministicRandom random;

            /// <summary> 实体槽位对应的代际 </summary>
            internal readonly uint[] generationList;

            /// <summary> 实体槽位对应的存活状态 </summary>
            internal readonly bool[] aliveList;

            /// <summary> 保留原始顺序的空闲槽位 </summary>
            internal readonly int[] freeIndexList;

            /// <summary> 各组件类型对应的独立组件池副本 </summary>
            internal readonly Dictionary<Type, IComponentStorage>
                storageDict;

            /// <summary> 按实体及附加顺序连续保存组件归属 不复制每实体列表容器 </summary>
            internal readonly KeyValuePair<int, Type>[] ownedTypeList;

            /// <summary> 最近分配的创建请求编号 </summary>
            internal readonly long lastSpawnRequestId;

            /// <summary> 尚未消费的创建结果副本 </summary>
            internal readonly Dictionary<long, SpawnResult>
                spawnResultDict;

            public long Tick { get; }

            /// <summary>
            /// 复制指定世界的完整核心状态
            /// </summary>
            internal Snapshot(World World)
            {
                owner = World.snapshotOwner;
                Tick = World.Tick;
                random = World.random;
                resourceDict = new Dictionary<Type, IWorldResource>(World.resourceDict.Count);
                foreach (var Pair in World.resourceDict)
                    resourceDict.Add(Pair.Key, Pair.Value.Clone());

                generationList = World.generationList.ToArray();
                aliveList = World.aliveList.ToArray();
                freeIndexList = World.freeIndexList.ToArray();

                storageDict =
                    new Dictionary<Type, IComponentStorage>(
                        World.storageDict.Count);

                foreach (var Pair in World.storageDict)
                    storageDict.Add(Pair.Key, Pair.Value.Clone());

                int count = 0;
                foreach (var TypeList in World.ownedTypeDict.Values)
                    count += TypeList.Count;
                ownedTypeList = new KeyValuePair<int, Type>[count];
                int offset = 0;
                foreach (var Pair in World.ownedTypeDict)
                    foreach (var ComponentType in Pair.Value)
                        ownedTypeList[offset++] = new KeyValuePair<int, Type>(Pair.Key, ComponentType);

                lastSpawnRequestId = World.lastSpawnRequestId;

                spawnResultDict =
                    new Dictionary<long, SpawnResult>(
                        World.spawnResultDict);
            }
        }

        /// <summary>
        /// 在完整逻辑步边界捕获核心状态
        /// </summary>
        internal Snapshot CaptureState()
        {
            EnsureSnapshotBoundary();
            return new Snapshot(this);
        }

        /// <summary>
        /// 将当前世界恢复到自身保存的核心状态
        /// </summary>
        internal void RestoreState(Snapshot Snapshot)
        {
            EnsureSnapshotBoundary();

            if (!ReferenceEquals(snapshotOwner, Snapshot.owner))
                throw new InvalidOperationException(
                    "Snapshot belongs to another World");

            // 宿主批次不随模拟状态回退 溢出时在修改世界前拒绝恢复
            ulong nextEpoch = checked(StateEpoch + 1);

            foreach (var Pair in Snapshot.resourceDict)
                resourceDict[Pair.Key].Restore(Pair.Value);
            random = Snapshot.random;

            // 保留组件池对象 由各池复制快照数组
            foreach (var Pair in Snapshot.storageDict)
                storageDict[Pair.Key].Restore(Pair.Value);

            generationList.Clear();
            generationList.AddRange(Snapshot.generationList);

            aliveList.Clear();
            aliveList.AddRange(Snapshot.aliveList);

            freeIndexList.Clear();
            freeIndexList.AddRange(Snapshot.freeIndexList);

            // 清空旧时间线成员并复用活动列表 快照只持有独立的连续归属数据
            foreach (var TypeList in ownedTypeDict.Values)
                TypeList.Clear();
            // 槽位列表只增不删 同源快照中的槽位必然已有列表
            foreach (var Pair in Snapshot.ownedTypeList)
                ownedTypeDict[Pair.Key].Add(Pair.Value);

            spawnResultDict.Clear();
            foreach (var Pair in Snapshot.spawnResultDict)
                spawnResultDict.Add(Pair.Key, Pair.Value);

            lastSpawnRequestId = Snapshot.lastSpawnRequestId;
            Tick = Snapshot.Tick;
            StateEpoch = nextEpoch;
            Events.Reset();
        }

        /// <summary>
        /// 限制快照操作只能发生在成功完成的逻辑步边界
        /// </summary>
        internal void EnsureSnapshotBoundary()
        {
            Events.EnsureIdle();
            EnsureNotIterating();
            if (!simulationStarted ||
                tickInProgress ||
                !Structural.IsEmpty)
            {
                throw new InvalidOperationException(
                    "Snapshot requires a completed Tick and empty structural buffer");
            }
        }
    }
}
