using System.Collections.Generic;
using MmECS;

namespace Game.Simulation
{
    public struct Position
    {
        public float X { get; set; }
    }

    public struct Velocity
    {
        public float X { get; set; }
    }

    public sealed class MovementSystem : ISimulationSystem
    {
        /// <summary>
        /// 复用的查询结果列表
        /// </summary>
        private readonly List<Entity> entityList = new List<Entity>();


        public void Tick(World World, float stepSeconds)
        {
            World.Query<Position, Velocity>(entityList);

            for (int index = 0; index < entityList.Count; index++)
            {
                var Entity = entityList[index];
                ref Position PositionData = ref World.Get<Position>(Entity);
                Velocity VelocityData = World.Get<Velocity>(Entity);

                PositionData.X += VelocityData.X * stepSeconds;
            }
        }
    }
}