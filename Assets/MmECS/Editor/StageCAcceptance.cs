using System;
using Game.Simulation;
using MmECS;
using UnityEditor;
using UnityEngine;

namespace MmECS.Editor
{
    public static class StageCAcceptance
    {
        /// <summary>
        /// 运行阶段 C 的系统顺序验收
        /// </summary>
        [MenuItem("MmECS/验收阶段 C")]
        public static void Run()
        {
            float moveFirst = RunCase(false);
            float setFirst = RunCase(true);

            if (moveFirst != 1f || setFirst != 2f)
                throw new InvalidOperationException($"System order mismatch {moveFirst} {setFirst}");

            Debug.Log($"MmECS 阶段 C 验收通过 移动优先 {moveFirst} 改速优先 {setFirst}");
        }

        /// <summary>
        /// 创建独立世界并返回推进后的位置
        /// </summary>
        private static float RunCase(bool setFirst)
        {
            var World = new World();
            World.Register<Game.Simulation.Position>();
            World.Register<Game.Simulation.Velocity>();

            var Entity = World.Create();
            World.Add(Entity, new Game.Simulation.Position { X = 0f });
            World.Add(Entity, new Game.Simulation.Velocity { X = 1f });

            var Runner = new Simulation(World, 1d);
            var Setter = new SetVelocitySystem(Entity, 2f);
            var Movement = new MovementSystem();

            if (setFirst)
            {
                Runner.Register(Setter);
                Runner.Register(Movement);
            }
            else
            {
                Runner.Register(Movement);
                Runner.Register(Setter);
            }

            Runner.Tick();
            return World.Get<Game.Simulation.Position>(Entity).X;
        }

        private sealed class SetVelocitySystem : ISimulationSystem
        {
            /// <summary>
            /// 需要修改速度的实体
            /// </summary>
            private readonly Entity target;

            /// <summary>
            /// 要写入的速度
            /// </summary>
            private readonly float speed;

            /// <summary>
            /// 指定目标实体和新速度
            /// </summary>
            public SetVelocitySystem(Entity Target, float speed)
            {
                target = Target;
                this.speed = speed;
            }

            /// <summary>
            /// 将目标实体的速度写入组件池
            /// </summary>
            public void Tick(World World, float stepSeconds)
            {
                ref Game.Simulation.Velocity VelocityData = ref World.Get<Game.Simulation.Velocity>(target);
                VelocityData.X = speed;
            }
        }
    }
}
