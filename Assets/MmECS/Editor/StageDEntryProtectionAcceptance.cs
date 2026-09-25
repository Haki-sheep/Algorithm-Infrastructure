using System;
using Game.Simulation;
using MmECS;
using UnityEditor;
using UnityEngine;

namespace MmECS.Editor
{
    public static class StageDEntryProtectionAcceptance
    {
        /// <summary>
        /// 验证首次 Tick 起公开结构入口关闭且缓冲仍能提交
        /// </summary>
        [MenuItem("MmECS/验收阶段 D 入口保护")]
        public static void Run()
        {
            var World = new World();
            World.Register<Position>();
            World.Register<Velocity>();
            var Entity = World.Create();
            World.Add(Entity, new Position { X = 1f });

            var Temporary = World.Create();
            World.Add(Temporary, new Position { X = 2f });
            World.Remove<Position>(Temporary);
            World.Destroy(Temporary);

            var Probe = new DirectRemovalProbe(Entity);
            var Runner = new Simulation(World, 1d);
            Runner.Register(Probe);
            Runner.Tick();

            if (!Probe.Rejected || !Probe.ComponentStillPresent)
                throw new InvalidOperationException("Direct removal was allowed during Tick");

            ExpectBlocked(() => World.Create(), "Create");
            ExpectBlocked(() => World.Register<int>(), "Register");
            ExpectBlocked(() => World.Add(Entity, new Velocity { X = 1f }), "Add");
            ExpectBlocked(() => World.Remove<Position>(Entity), "Remove");
            ExpectBlocked(() => World.Destroy(Entity), "Destroy");

            World.Structural.Remove<Position>(Entity);
            Runner.Tick();
            if (World.Has<Position>(Entity))
                throw new InvalidOperationException("Buffered removal did not commit after Tick");

            World.Structural.Destroy(Entity);
            Runner.Tick();
            if (World.IsAlive(Entity))
                throw new InvalidOperationException("Buffered destruction did not commit after Tick");

            Debug.Log("MmECS 阶段 D 入口保护验收通过 初始化后即时结构入口关闭 缓冲提交正常");
        }

        /// <summary>
        /// 验证公开入口在运行期抛出调用错误
        /// </summary>
        private static void ExpectBlocked(Action Attempt, string operation)
        {
            try
            {
                Attempt();
            }
            catch (InvalidOperationException Exception)
            {
                if (Exception.Message != "Runtime structural changes must use StructuralBuffer")
                    throw;

                return;
            }

            throw new InvalidOperationException($"Runtime {operation} was allowed");
        }

        private sealed class DirectRemovalProbe : ISimulationSystem
        {
            /// <summary>
            /// 尝试直接移除组件的实体
            /// </summary>
            private readonly Entity target;

            public bool Rejected { get; private set; }
            public bool ComponentStillPresent { get; private set; }

            /// <summary>
            /// 保存目标实体
            /// </summary>
            public DirectRemovalProbe(Entity Target)
            {
                target = Target;
            }

            /// <summary>
            /// 验证当前系统无法绕过结构缓冲
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                try
                {
                    World.Remove<Position>(target);
                }
                catch (InvalidOperationException Exception)
                {
                    if (Exception.Message != "Runtime structural changes must use StructuralBuffer")
                        throw;

                    Rejected = true;
                }

                ComponentStillPresent = World.Has<Position>(target);
            }
        }
    }
}
