using System;

namespace MmECS
{
    /// <summary>
    /// 绑定世界与固定步长并累计经过时间以推进零到多个逻辑 Tick
    /// </summary>
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
            if (stepSeconds <= 0d || double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds) ||
                stepSeconds > float.MaxValue || (float)stepSeconds == 0f)
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
            EnsureIdle();
            if (elapsedSeconds < 0d || double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) ||
                double.IsInfinity(accumulatedSeconds + elapsedSeconds))
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            executionInProgress = true;
            try
            {
                InitCore();
                accumulatedSeconds += elapsedSeconds;
                int tickCount = 0;

                while (accumulatedSeconds >= stepSeconds)
                {
                    TickCore();
                    accumulatedSeconds -= stepSeconds;
                    tickCount++;
                }

                return tickCount;
            }
            finally
            {
                executionInProgress = false;
            }
        }
    }
}
