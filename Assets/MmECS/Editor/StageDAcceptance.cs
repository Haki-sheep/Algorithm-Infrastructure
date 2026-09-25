using System;
using Game.Simulation;
using MmECS;
using UnityEditor;
using UnityEngine;

namespace MmECS.Editor
{
    public static class StageDAcceptance
    {
        /// <summary>
        /// 验证固定步长累计和单次多 Tick 推进
        /// </summary>
        [MenuItem("MmECS/验收阶段 D 固定 Tick")]
        public static void Run()
        {
            var World = new World();
            long initialTick = World.Tick;
            World.Register<Position>();
            World.Register<Velocity>();

            var Entity = World.Create();
            World.Add(Entity, new Position { X = 0f });
            World.Add(Entity, new Velocity { X = 1f });

            var Runner = new Simulation(World, 0.5d);
            Runner.Register(new MovementSystem());

            int firstCount = Runner.Advance(0.25d);
            float firstX = World.Get<Position>(Entity).X;
            long firstTick = World.Tick;

            int secondCount = Runner.Advance(0.25d);
            float secondX = World.Get<Position>(Entity).X;
            long secondTick = World.Tick;

            int thirdCount = Runner.Advance(1d);
            float thirdX = World.Get<Position>(Entity).X;
            long thirdTick = World.Tick;

            if (initialTick != 0 ||
                firstCount != 0 || firstX != 0f || firstTick != 0 ||
                secondCount != 1 || secondX != 0.5f || secondTick != 1 ||
                thirdCount != 2 || thirdX != 1.5f || thirdTick != 3)
                throw new InvalidOperationException(
                    $"Fixed Tick mismatch {firstCount}/{firstX}/{firstTick} {secondCount}/{secondX}/{secondTick} {thirdCount}/{thirdX}/{thirdTick}");

            Debug.Log($"MmECS 阶段 D 固定 Tick 验收通过 次数 {firstCount} {secondCount} {thirdCount} 位置 {firstX} {secondX} {thirdX} 已完成 Tick {firstTick} {secondTick} {thirdTick}");
        }
    }
}
