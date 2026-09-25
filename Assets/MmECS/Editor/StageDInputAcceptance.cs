using System;
using Game.Simulation;
using MmECS;
using UnityEditor;
using UnityEngine;

namespace MmECS.Editor
{
    public static class StageDInputAcceptance
    {
        /// <summary>
        /// 验证输入按目标 Tick 消费且本 Tick 冻结后拒绝迟到输入
        /// </summary>
        [MenuItem("MmECS/验收阶段 D 输入队列")]
        public static void Run()
        {
            var World = new World();
            World.Register<Position>();
            World.Register<Velocity>();

            var Entity = World.Create();
            World.Add(Entity, new Position { X = 0f });
            World.Add(Entity, new Velocity { X = 0f });

            var Runner = new Simulation(World, 0.5d);
            Runner.Register(new FrozenInputProbe(Runner, Entity));
            Runner.Register(new MovementSystem());

            Runner.SubmitInput(new MoveInput(3, Entity, -1f));
            Runner.SubmitInput(new MoveInput(1, Entity, 1f));
            Runner.SubmitInput(new MoveInput(2, Entity, 0f));

            int firstCount = Runner.Advance(0.5d);
            if (firstCount != 1 || World.Tick != 1 || World.Get<Position>(Entity).X != 0.5f)
                throw new InvalidOperationException("Tick 1 input was not applied before systems");

            ExpectClosed(Runner, new MoveInput(1, Entity, 9f));

            int secondCount = Runner.Advance(0.5d);
            if (secondCount != 1 || World.Tick != 2 || World.Get<Position>(Entity).X != 0.5f)
                throw new InvalidOperationException("Tick 2 input was not applied at its target Tick");

            int thirdCount = Runner.Advance(0.5d);
            if (thirdCount != 1 || World.Tick != 3 || World.Get<Position>(Entity).X != 0f)
                throw new InvalidOperationException("Tick 3 input was not applied at its target Tick");

            Debug.Log("MmECS 阶段 D 输入队列验收通过 目标 Tick 1 2 3 顺序生效 迟到输入拒绝");
        }

        /// <summary>
        /// 验证目标 Tick 的输入入口已关闭
        /// </summary>
        private static void ExpectClosed(Simulation Runner, MoveInput Input)
        {
            try
            {
                Runner.SubmitInput(Input);
            }
            catch (InvalidOperationException Exception)
            {
                if (Exception.Message == "Input target Tick is closed")
                    return;

                throw;
            }

            throw new InvalidOperationException("Frozen Tick accepted a late input");
        }

        private sealed class FrozenInputProbe : ISimulationSystem
        {
            /// <summary>
            /// 接收输入的模拟实例
            /// </summary>
            private readonly Simulation runner;

            /// <summary>
            /// 测试输入的目标实体
            /// </summary>
            private readonly Entity target;

            /// <summary>
            /// 保存模拟实例和目标实体
            /// </summary>
            public FrozenInputProbe(Simulation Runner, Entity Target)
            {
                runner = Runner;
                target = Target;
            }

            /// <summary>
            /// 验证系统运行时不能补交本 Tick 输入
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                if (World.Tick == 0)
                    ExpectClosed(runner, new MoveInput(World.Tick + 1, target, 9f));
            }
        }
    }
}
