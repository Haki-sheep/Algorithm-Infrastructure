using System;
using System.Collections.Generic;

namespace MmECS
{
    /// <summary>
    /// 提供模拟状态投影和可选的逐系统字段差异追踪及最近 Tick 计数
    /// </summary>
    public partial class Simulation
    {
        public Action<SimulationTrace> Trace { get; set; }
        public int LastStructuralCount => world.Structural.LastCommittedCount;

        /// <summary>
        /// 在空闲边界复制完整模拟状态用于报告和重演比较
        /// </summary>
        public virtual SortedDictionary<string, string> InspectState()
        {
            EnsureIdle();
            var StateDict = world.InspectState();
            StateDiagnostics.WriteValue(StateDict, "Simulation/AccumulatedSeconds", accumulatedSeconds);
            StateDiagnostics.WriteValue(StateDict, "Simulation/StepSeconds", stepSeconds);
            for (int index = 0; index < systemList.Count; index++)
                StateDict[$"System/{index}"] = systemList[index].GetType().AssemblyQualifiedName;
            return StateDict;
        }

        /// <summary>
        /// 默认直接执行系统且仅在启用追踪时复制前后状态并报告字段变化
        /// </summary>
        private void ExecuteSystem(ISimulationSystem System)
        {
            var Sink = Trace;
            if (Sink == null)
            {
                System.Tick(world, (float)stepSeconds);
                return;
            }
            var BeforeDict = world.InspectState();
            int pendingBefore = world.Structural.PendingCount;
            System.Tick(world, (float)stepSeconds);
            Sink(new SimulationTrace(world.Tick + 1, System.GetType().FullName,
                StateDiagnostics.Compare(BeforeDict, world.InspectState()),
                world.Structural.PendingCount - pendingBefore));
        }
    }

    /// <summary>
    /// 描述某 Tick 某系统执行时的字段变化与新增结构请求数量
    /// </summary>
    public sealed class SimulationTrace
    {
        public long Tick { get; }
        public string SystemName { get; }
        public IReadOnlyList<string> Differences { get; }
        public int StructuralRequestCount { get; }

        /// <summary>
        /// 保存独立诊断结果供宿主记录
        /// </summary>
        internal SimulationTrace(long tick, string systemName, List<string> DifferenceList, int structuralRequestCount)
        {
            Tick = tick;
            SystemName = systemName;
            Differences = DifferenceList.AsReadOnly();
            StructuralRequestCount = structuralRequestCount;
        }
    }
}
