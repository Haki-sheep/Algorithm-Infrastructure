using System;
using System.Collections.Generic;
using Game.Simulation;
using MmECS;
using UnityEditor;
using UnityEngine;

namespace MmECS.Editor
{
    public static class StageDStructuralAcceptance
    {
        /// <summary>
        /// 验证结构变更在 Tick 末按入队顺序生效
        /// </summary>
        [MenuItem("MmECS/验收阶段 D 结构提交")]
        public static void Run()
        {
            VerifyDeferredRemoval();
            VerifyFifo();
            VerifyInvalidOrder();
            Debug.Log("MmECS 阶段 D 结构提交验收通过 同 Tick 可见 Tick 末移除 FIFO 替换");
        }

        #region 验证用例
        /// <summary>
        /// 验证遍历不漏项且后续系统仍能读取待移除组件
        /// </summary>
        private static void VerifyDeferredRemoval()
        {
            var World = new World();
            World.Register<Position>();
            World.Register<Velocity>();

            for (int index = 0; index < 3; index++)
            {
                var Entity = World.Create();
                World.Add(Entity, new Position { X = index + 1 });
                World.Add(Entity, new Velocity { X = 1f });
            }

            var Producer = new QueueRemovalSystem();
            var Observer = new ObservePendingRemovalSystem();
            var Runner = new Simulation(World, 1d);
            Runner.Register(Producer);
            Runner.Register(Observer);
            Runner.Tick();

            var RemainingEntityList = new List<Entity>();
            World.Query<Position, Velocity>(RemainingEntityList);
            if (Producer.VisitedCount != 3 || Producer.UniqueCount != 3 ||
                Observer.VisibleCount != 3 || Observer.PositionSum != 9f ||
                RemainingEntityList.Count != 0)
                throw new InvalidOperationException("Deferred removal changed query members before Tick end");
        }

        /// <summary>
        /// 验证同一组件先移除再添加按顺序提交
        /// </summary>
        private static void VerifyFifo()
        {
            var World = new World();
            World.Register<Position>();
            var Entity = World.Create();
            World.Add(Entity, new Position { X = 1f });

            var Runner = new Simulation(World, 1d);
            Runner.Register(new ReplacePositionSystem(Entity));
            Runner.Tick();

            if (!World.Has<Position>(Entity) || World.Get<Position>(Entity).X != 7f)
                throw new InvalidOperationException("Structural commands did not commit in FIFO order");
        }

        /// <summary>
        /// 验证销毁后的组件添加会报告调用错误
        /// </summary>
        private static void VerifyInvalidOrder()
        {
            var World = new World();
            World.Register<Position>();
            var Entity = World.Create();
            World.Add(Entity, new Position { X = 1f });

            var Runner = new Simulation(World, 1d);
            Runner.Register(new DestroyThenAddSystem(Entity));

            bool rejected = false;
            try
            {
                Runner.Tick();
            }
            catch (InvalidOperationException Exception)
            {
                if (Exception.Message != "Entity is not alive")
                    throw;

                rejected = true;
            }

            if (!rejected || World.IsAlive(Entity) || World.Tick != 0)
                throw new InvalidOperationException("Destroy followed by Add did not report an invalid order");

            try
            {
                World.Add(Entity, new Position { X = 2f });
            }
            catch (InvalidOperationException Exception)
            {
                if (Exception.Message == "Runtime structural changes must use StructuralBuffer")
                    return;

                throw;
            }

            throw new InvalidOperationException("Structural write remained enabled after failed commit");
        }
        #endregion

        private sealed class QueueRemovalSystem : ISimulationSystem
        {
            /// <summary>
            /// 复用的查询结果
            /// </summary>
            private readonly List<Entity> entityList = new List<Entity>();

            /// <summary>
            /// 已遍历的实体编号
            /// </summary>
            private readonly HashSet<int> visitedIndexHashList = new HashSet<int>();

            public int VisitedCount { get; private set; }
            public int UniqueCount => visitedIndexHashList.Count;

            /// <summary>
            /// 修改已有字段并排队移除速度组件
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                World.Query<Position, Velocity>(entityList);
                for (int index = 0; index < entityList.Count; index++)
                {
                    var Entity = entityList[index];
                    VisitedCount++;
                    visitedIndexHashList.Add(Entity.Index);
                    ref Position PositionData = ref World.Get<Position>(Entity);
                    PositionData.X += 1f;
                    World.Structural.Remove<Velocity>(Entity);
                }
            }
        }

        private sealed class ObservePendingRemovalSystem : ISimulationSystem
        {
            /// <summary>
            /// 复用的查询结果
            /// </summary>
            private readonly List<Entity> entityList = new List<Entity>();

            public int VisibleCount { get; private set; }
            public float PositionSum { get; private set; }

            /// <summary>
            /// 读取本 Tick 已改字段与尚未提交的查询成员
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                World.Query<Position, Velocity>(entityList);
                VisibleCount = entityList.Count;
                for (int index = 0; index < entityList.Count; index++)
                    PositionSum += World.Get<Position>(entityList[index]).X;
            }
        }

        private sealed class ReplacePositionSystem : ISimulationSystem
        {
            /// <summary>
            /// 待替换组件的实体
            /// </summary>
            private readonly Entity target;

            /// <summary>
            /// 保存目标实体
            /// </summary>
            public ReplacePositionSystem(Entity Target)
            {
                target = Target;
            }

            /// <summary>
            /// 依次入队移除和添加请求
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                World.Structural.Remove<Position>(target);
                World.Structural.Add(target, new Position { X = 7f });
            }
        }

        private sealed class DestroyThenAddSystem : ISimulationSystem
        {
            /// <summary>
            /// 待销毁的实体
            /// </summary>
            private readonly Entity target;

            /// <summary>
            /// 保存目标实体
            /// </summary>
            public DestroyThenAddSystem(Entity Target)
            {
                target = Target;
            }

            /// <summary>
            /// 排队销毁后再排队添加组件
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                World.Structural.Destroy(target);
                World.Structural.Add(target, new Position { X = 2f });
            }
        }
    }
}
