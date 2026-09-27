using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 拥有通用系统调度与 Tick 边界 派生宿主仅在指定阶段接入业务
    /// </summary>
    public partial class Simulation
    {
        /// <summary>
        /// 当前模拟世界
        /// </summary>
        protected readonly World world;

        /// <summary> 当前模拟入口是否正在执行 </summary>
        private bool executionInProgress;

        /// <summary>
        /// 按注册顺序执行的系统
        /// </summary>
        private readonly List<ISystem> systemList = new List<ISystem>();

        /// <summary>
        /// 注册系统
        /// </summary>
        public void Register(ISystem System)
        {
            EnsureIdle();
            if (initialized)
                throw new InvalidOperationException("Simulation systems are initialized");
            for (int index = 0; index < systemList.Count; index++)
                if (ReferenceEquals(systemList[index], System))
                    throw new InvalidOperationException("System is already registered");
            systemList.Add(System);
        }

        /// <summary>
        /// 依次推进所有系统
        /// </summary>
        public void Tick()
        {
            EnsureIdle();
            executionInProgress = true;
            try
            {
                InitCore();
                TickCore();
            }
            finally
            {
                executionInProgress = false;
            }
        }

        /// <summary>
        /// 完成宿主前置处理与系统执行和结构提交及后置处理
        /// </summary>
        private void TickCore()
        {
            world.BeginSimulation();
            try
            {
                BeforeTick();

                for (int index = 0; index < systemList.Count; index++)
                    if (systemList[index] is ISimulationSystem System)
                        ExecuteSystem(System);

                // 出队提交命令
                world.CommitStructural();
                AfterStructuralCommit();
                // 完成当前 Tick
                world.CompleteTick();
            }
            catch
            {
                world.AbortMessages();
                throw;
            }
        }

        /// <summary>
        /// 阻止执行中重入推进或操作快照
        /// </summary>
        protected void EnsureIdle()
        {
            if (executionInProgress)
                throw new InvalidOperationException("Simulation is executing");
            if (disposed)
                throw new ObjectDisposedException(nameof(Simulation));
            world.Events.EnsureIdle();
        }
    }
}
