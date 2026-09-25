using System.Collections.Generic;
using MmECS;

namespace Game.Simulation
{
    public partial class Simulation
    {
        /// <summary>
        /// 当前模拟世界
        /// </summary>
        private readonly World world;

        /// <summary>
        /// 按注册顺序执行的系统
        /// </summary>
        private readonly List<ISimulationSystem> systemList = new List<ISimulationSystem>();

        /// <summary>
        /// 注册系统
        /// </summary>
        public void Register(ISimulationSystem System)
        {
            systemList.Add(System);
        }

        /// <summary>
        /// 依次推进所有系统
        /// </summary>
        public void Tick()
        {
            world.BeginSimulation();
            
            // 处理输入
            ProcessInput();

            for (int index = 0; index < systemList.Count; index++)
                systemList[index].Tick(world, (float)stepSeconds);
            
            // 出队提交命令
            world.CommitStructural();
            // 完成当前 Tick
            world.CompleteTick();
        }
    }
}
