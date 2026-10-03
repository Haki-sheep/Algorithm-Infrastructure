using System;
using System.Collections.Generic;

namespace MmECS.Samples
{
    /// <summary>
    /// 验证组件索引与实体身份及结构提交顺序
    /// </summary>
    public static partial class CoreVerification
    {
        /// <summary>
        /// 多轮删除非末行并添加新实体 每轮与独立数据模型核对
        /// </summary>
        private static void VerifyStorage()
        {
            var World = CreateWorld();
            var EntityList = new List<Entity>();
            var ExpectedDict = new Dictionary<Entity, int>();
            var ResultList = new List<Entity>();
            for (int Index = 0; Index < 64; Index++)
            {
                var Entity = CreateEntity(World, Index);
                EntityList.Add(Entity);
                ExpectedDict.Add(Entity, Index);
            }
            for (int Round = 0; Round < 256; Round++)
            {
                int Slot = Round * 17 % EntityList.Count;
                var Removed = EntityList[Slot];
                World.Remove<Position>(Removed);
                ExpectedDict.Remove(Removed);
                Check(!World.Has<Position>(Removed), "删除后组件仍存在");
                World.Destroy(Removed);
                var Added = CreateEntity(World, Round + 1000);
                EntityList[Slot] = Added;
                ExpectedDict.Add(Added, Round + 1000);
                World.Query<Position>(ResultList);
                Check(ResultList.Count == ExpectedDict.Count, "查询数量与模型不一致");
                var SeenHashList = new HashSet<Entity>();
                foreach (var Entity in ResultList)
                {
                    Check(SeenHashList.Add(Entity), "查询重复返回实体");
                    Check(ExpectedDict.TryGetValue(Entity, out int Value), "查询返回模型外实体");
                    Check(World.Get<Position>(Entity).Value == Value, "填洞后组件归属错误");
                    Check(World.Get<Velocity>(Entity).Value == 2, "销毁与回收破坏其他组件");
                }
            }
        }

        /// <summary>
        /// 验证代际回收与跨世界句柄拒绝
        /// </summary>
        private static void VerifyEntityIdentity()
        {
            var World = CreateWorld();
            var OtherWorld = CreateWorld();
            var OldEntity = CreateEntity(World, 1);
            var OldHandle = World.GetEntityHandle(OldEntity);
            CreateEntity(OtherWorld, 1);
            Check(!OtherWorld.TryResolve(OldHandle, out _), "跨世界句柄被接受");
            World.Destroy(OldEntity);
            var NewEntity = CreateEntity(World, 2);
            Check(NewEntity.Index == OldEntity.Index, "没有复用空闲槽位");
            Check(NewEntity.Generation != OldEntity.Generation, "回收未更新代际");
            Check(!World.IsAlive(OldEntity), "旧实体代际仍有效");
            Check(!World.TryResolve(OldHandle, out _), "旧句柄指向新实体");
            Check(World.TryResolve(World.GetEntityHandle(NewEntity), out var Resolved) &&
                Resolved.Equals(NewEntity), "新句柄无法解析");
        }

        /// <summary>
        /// 验证遍历时禁止即时结构变化 且延迟销毁在 Tick 末生效
        /// </summary>
        private static void VerifyIteration()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 3);
            var Action = new DestroyAction { World = World };
            World.ForEach<Position, Velocity, DestroyAction>(ref Action);
            Check(World.IsAlive(Entity), "结构请求提前销毁实体");
            Check(World.Get<Position>(Entity).Value == 5, "遍历未写回组件值");
            Check(World.Structural.PendingCount == 1, "销毁请求未入队");
            using var Runner = new Simulation(World, 0.5d);
            Runner.Tick();
            Check(!World.IsAlive(Entity), "Tick 末未销毁实体");
            Check(Runner.LastStructuralCount == 1 && World.Structural.PendingCount == 0,
                "结构提交计数错误");
        }

        /// <summary>
        /// 在运行期先移除再添加同种组件以验证全局 FIFO
        /// </summary>
        private static void VerifyStructuralOrder()
        {
            var World = CreateWorld();
            var Entity = CreateEntity(World, 1);
            using var Runner = new Simulation(World, 0.5d);
            Runner.Tick();
            Throws<InvalidOperationException>(() => World.Remove<Position>(Entity));
            World.Structural.Remove<Position>(Entity);
            World.Structural.Add(Entity, new Position { Value = 9 });
            Check(World.Get<Position>(Entity).Value == 1, "入队时改变组件");
            Runner.Tick();
            Check(World.Get<Position>(Entity).Value == 9 && Runner.LastStructuralCount == 2,
                "未按先移除再添加的顺序提交");
        }

        /// <summary>
        /// 在组件引用有效期间修改数值并提交延迟销毁请求
        /// </summary>
        private struct DestroyAction : IComponentAction<Position, Velocity>
        {
            /// <summary> 当前遍历所属世界 </summary>
            public World World;

            /// <summary>
            /// 验证即时结构变更被拒绝后修改组件并排队销毁
            /// </summary>
            public void Execute(Entity Entity, ref Position Position, ref Velocity Velocity)
            {
                var CurrentWorld = World;
                Throws<InvalidOperationException>(() => CurrentWorld.Remove<Position>(Entity));
                Position.Value += Velocity.Value;
                World.Structural.Destroy(Entity);
            }
        }
    }
}
