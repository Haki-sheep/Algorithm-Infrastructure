using System;
using MmECS;

namespace Game.Simulation
{
    public partial class Simulation
    {
        /// <summary>
        /// 每次逻辑推进的固定秒数
        /// </summary>
        private readonly double stepSeconds;

        /// <summary>
        /// 尚未用于逻辑推进的秒数
        /// </summary>
        private double accumulatedSeconds;

        /// <summary>
        /// 绑定模拟世界和固定步长
        /// </summary>
        public Simulation(World World, double stepSeconds)
        {
            if (stepSeconds <= 0d)
                throw new ArgumentOutOfRangeException(nameof(stepSeconds));

            world = World;
            this.stepSeconds = stepSeconds;
        }

        /// <summary>
        /// 累计时间并返回本次执行的逻辑 Tick 数
        /// </summary>
        /// <param name="elapsedSeconds">已过秒数</param>
        public int Advance(double elapsedSeconds)
        {
            accumulatedSeconds += elapsedSeconds;
            int tickCount = 0;

            while (accumulatedSeconds >= stepSeconds)
            {
                Tick();
                accumulatedSeconds -= stepSeconds;
                tickCount++;
            }

            return tickCount;
        }
    }
}
